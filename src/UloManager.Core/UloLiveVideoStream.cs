using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace UloManager.Core;

public sealed class UloVideoChunkEventArgs : EventArgs
{
    public required ReadOnlyMemory<byte> Data { get; init; }

    /// <summary>True for the first chunk, which carries the MP4 initialisation segment (ftyp + moov).</summary>
    public bool IsInitialSegment { get; init; }

    public long TotalBytes { get; init; }
}

/// <summary>
/// Live video from the camera.
/// <para>
/// The camera streams <b>fragmented MP4</b> over a WebSocket on <c>ws://&lt;host&gt;/api/v1/live</c>
/// using the sub-protocol <c>mudesign.ulo.mp4</c>. As with the event channel, the first frame sent
/// must be <c>{"token":"&lt;session token&gt;"}</c>. The camera then pushes binary chunks: an
/// initialisation segment (<c>ftyp</c> + <c>moov</c>) followed by <c>moof</c>/<c>mdat</c> fragments,
/// so the data can be written straight to an .mp4 file or piped into a player.
/// </para>
/// </summary>
public sealed class UloLiveVideoStream : IAsyncDisposable
{
    public const string SubProtocol = "mudesign.ulo.mp4";

    private readonly UloClient _client;
    private CancellationTokenSource? _cts;
    private Task? _worker;

    public UloLiveVideoStream(UloClient client) => _client = client;

    public bool IsRunning => _worker is { IsCompleted: false };

    public bool IsConnected { get; private set; }

    public long TotalBytes { get; private set; }

    public event EventHandler<UloVideoChunkEventArgs>? ChunkReceived;

    public event EventHandler<string>? StatusChanged;

    public Uri BuildEndpoint()
    {
        var http = _client.Options.BaseAddress;
        var scheme = http.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
        return new Uri($"{scheme}://{http.Authority}/api/v1/live");
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _worker = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            if (_worker is not null)
            {
                await _worker.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected while stopping.
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _worker = null;
            IsConnected = false;
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await ReceiveAsync(chunk => { }, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal stop.
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Live video stopped: {ex.Message}");
        }
        finally
        {
            IsConnected = false;
        }
    }

    /// <summary>
    /// Opens the stream and pumps chunks into <paramref name="onChunk"/> until cancelled.
    /// Every chunk is also published through <see cref="ChunkReceived"/>.
    /// </summary>
    public async Task ReceiveAsync(Action<ReadOnlyMemory<byte>> onChunk, CancellationToken ct)
    {
        await _client.EnsureLoggedInAsync(ct).ConfigureAwait(false);

        using var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(SubProtocol);

        // Same unverifiable certificate as everywhere else: pin or nothing, never a chain check.
        socket.Options.RemoteCertificateValidationCallback =
            (_, certificate, _, _) => _client.Options.IsAcceptableCertificate(certificate);

        await socket.ConnectAsync(BuildEndpoint(), ct).ConfigureAwait(false);
        IsConnected = true;
        StatusChanged?.Invoke(this, "Live video connected.");

        var hello = JsonSerializer.Serialize(new { token = _client.Token ?? "" });
        await socket.SendAsync(
            Encoding.UTF8.GetBytes(hello),
            WebSocketMessageType.Text,
            endOfMessage: true,
            ct).ConfigureAwait(false);

        var buffer = new byte[64 * 1024];
        var message = new MemoryStream();
        var first = true;
        TotalBytes = 0;

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                    .ConfigureAwait(false);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                // The camera reports problems (expired session, busy) as text frames.
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                StatusChanged?.Invoke(this, $"Camera says: {text}");
                continue;
            }

            message.Write(buffer, 0, result.Count);

            if (!result.EndOfMessage)
            {
                continue;
            }

            var chunk = message.ToArray();
            message.SetLength(0);
            TotalBytes += chunk.Length;

            onChunk(chunk);
            ChunkReceived?.Invoke(this, new UloVideoChunkEventArgs
            {
                Data = chunk,
                IsInitialSegment = first,
                TotalBytes = TotalBytes,
            });

            first = false;
        }

        StatusChanged?.Invoke(this, "Live video disconnected.");
    }

    /// <summary>Records the live stream into a playable .mp4 file until the token is cancelled.</summary>
    public async Task<long> RecordToFileAsync(string destinationFile, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var file = File.Create(destinationFile);

        try
        {
            await ReceiveAsync(chunk => file.Write(chunk.Span), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping is the normal way to end a recording.
        }

        await file.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        return file.Length;
    }

    /// <summary>Copies the live stream into any writable stream, for example a player's standard input.</summary>
    public async Task PipeToAsync(Stream destination, CancellationToken ct)
    {
        try
        {
            await ReceiveAsync(
                chunk =>
                {
                    destination.Write(chunk.Span);
                    destination.Flush();
                },
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal stop.
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
