using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UloManager.Core;

public sealed class UloEventArgs : EventArgs
{
    public required string Event { get; init; }

    public string? Data { get; init; }

    public JsonNode? Raw { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool IsAuthenticationFailure =>
        string.Equals(Event, "failure", StringComparison.OrdinalIgnoreCase) &&
        Data?.Contains("authenticate", StringComparison.OrdinalIgnoreCase) == true;

    public override string ToString() => Data is null ? Event : $"{Event}: {Data}";
}

/// <summary>
/// Real-time event channel of the camera.
/// <para>
/// The official web/mobile app does not poll at all - it opens a WebSocket on
/// <c>ws://&lt;host&gt;/api/v1</c> using the <c>mudesign.ulo.json</c> sub-protocol, sends
/// <c>{"token":"&lt;session token&gt;"}</c> as the first frame and then receives
/// <c>{"event":"...","data":...}</c> messages (movement, orientation, mode changes...).
/// An invalid token is answered with <c>{"event":"failure"}</c>; a valid one stays silent.
/// </para>
/// </summary>
public sealed class UloEventStream : IAsyncDisposable
{
    public const string SubProtocol = "mudesign.ulo.json";

    private readonly UloClient _client;
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private bool _sessionRejected;

    public UloEventStream(UloClient client) => _client = client;

    /// <summary>Delay before re-opening a dropped connection (the official app uses 2 seconds).</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(2);

    public bool IsRunning => _worker is { IsCompleted: false };

    public bool IsConnected { get; private set; }

    public event EventHandler<UloEventArgs>? EventReceived;

    public event EventHandler<string>? ConnectionChanged;

    public Uri BuildEndpoint()
    {
        var http = _client.Options.BaseAddress;
        var scheme = http.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
        return new Uri($"{scheme}://{http.Authority}/api/v1");
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
            // Expected while shutting down.
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
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAndListenAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ConnectionChanged?.Invoke(this, $"Event channel error: {ex.Message}");
            }

            IsConnected = false;

            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(ReconnectDelay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConnectAndListenAsync(CancellationToken ct)
    {
        await _client.EnsureLoggedInAsync(ct).ConfigureAwait(false);

        using var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(SubProtocol);

        // Over wss the camera presents the same unverifiable certificate as the REST API, so the
        // same pin-only policy applies - see UloConnectionOptions.IsAcceptableCertificate.
        socket.Options.RemoteCertificateValidationCallback =
            (_, certificate, _, _) => _client.Options.IsAcceptableCertificate(certificate);

        await socket.ConnectAsync(BuildEndpoint(), ct).ConfigureAwait(false);
        IsConnected = true;
        ConnectionChanged?.Invoke(this, "Event channel connected.");

        _sessionRejected = false;

        var hello = JsonSerializer.Serialize(new { token = _client.Token ?? "" });
        await socket.SendAsync(
            Encoding.UTF8.GetBytes(hello),
            WebSocketMessageType.Text,
            endOfMessage: true,
            ct).ConfigureAwait(false);

        var buffer = new byte[8192];
        var message = new StringBuilder();

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                    .ConfigureAwait(false);
                break;
            }

            message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            if (!result.EndOfMessage)
            {
                continue;
            }

            var payload = message.ToString();
            message.Clear();
            Dispatch(payload);

            if (_sessionRejected)
            {
                // The token is dead (the camera rebooted, or the account signed in elsewhere).
                // Reconnecting with the same token would only be rejected again, so drop the
                // session and let the outer loop open a new socket with a fresh login.
                _client.InvalidateSession();
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                    .ConfigureAwait(false);
                break;
            }
        }

        ConnectionChanged?.Invoke(this, "Event channel disconnected.");
    }

    private void Dispatch(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        JsonNode? node = null;
        try
        {
            node = JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            // Some firmware builds push plain text.
        }

        var args = new UloEventArgs
        {
            Event = node?["event"]?.GetValue<string>() ?? "message",
            Data = node?["data"] is { } data
                ? data.GetValueKind() == System.Text.Json.JsonValueKind.String ? data.GetValue<string>() : data.ToJsonString()
                : node is null ? payload : null,
            Raw = node,
        };

        if (args.IsAuthenticationFailure)
        {
            // The session died - drop the token so the next call performs a fresh login.
            _sessionRejected = true;
            ConnectionChanged?.Invoke(this, "Event channel rejected the session token, re-authenticating.");
        }

        EventReceived?.Invoke(this, args);
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
