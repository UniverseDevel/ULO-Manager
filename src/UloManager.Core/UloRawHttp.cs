using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace UloManager.Core;

/// <summary>Result of a request sent through <see cref="UloRawHttp"/>.</summary>
public sealed record UloRawHttpResponse(
    HttpStatusCode Status,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body,
    IReadOnlyList<string> MalformedHeaderLines)
{
    /// <summary>True when the camera emitted header lines that are not valid HTTP.</summary>
    public bool HasMalformedHeaders => MalformedHeaderLines.Count > 0;

    public string BodyText => Encoding.UTF8.GetString(Body);
}

/// <summary>
/// A deliberately forgiving HTTP/1.1 client used for the camera endpoints whose responses are not
/// valid HTTP.
///
/// <para>
/// Firmware 10.1308 answers <c>POST /api/v1/snapshot</c> with a bare <c>success</c> line inside the
/// header block:
/// </para>
/// <code>
/// HTTP/1.1 201 Created
/// Content-Type: application/json; charset=utf-8
/// Content-Length: 29
/// success
///
/// { "filename": "media/" }
/// </code>
/// <para>
/// A header line without a colon is a protocol violation, so <see cref="HttpClient"/> aborts the
/// whole response with <c>Received an invalid header line: 'success'</c> and the picture can never be
/// retrieved — even though the camera took it. .NET offers no leniency switch for this, so the
/// request is replayed over a plain socket here and the offending lines are simply recorded.
/// </para>
/// </summary>
internal static class UloRawHttp
{
    public static async Task<UloRawHttpResponse> SendAsync(
        Uri baseAddress,
        string method,
        string relativePath,
        string? body,
        string? bearerToken,
        UloConnectionOptions options,
        CancellationToken ct)
    {
        var uri = new Uri(baseAddress, relativePath);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(options.Timeout);
        var token = timeoutCts.Token;

        using var tcp = new TcpClient { NoDelay = true };
        try
        {
            await tcp.ConnectAsync(uri.Host, uri.Port, token).ConfigureAwait(false);

            await using var network = tcp.GetStream();
            Stream stream = network;
            SslStream? ssl = null;

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                ssl = new SslStream(network, leaveInnerStreamOpen: true, CreateValidationCallback(options));
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = uri.Host }, token)
                    .ConfigureAwait(false);
                stream = ssl;
            }

            try
            {
                var payload = body is null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);

                var request = new StringBuilder()
                    .Append(method).Append(' ').Append(uri.PathAndQuery).Append(" HTTP/1.1\r\n")
                    .Append("Host: ").Append(uri.IdnHost).Append(uri.IsDefaultPort ? "" : ":" + uri.Port).Append("\r\n")
                    .Append("Accept: */*\r\n")
                    // The camera closes the socket at the end of the body, which is also how the end of
                    // the payload is detected when it lies about (or omits) Content-Length.
                    .Append("Connection: close\r\n");

                if (bearerToken is not null)
                {
                    request.Append("Authorization: Bearer ").Append(bearerToken).Append("\r\n");
                }

                if (payload.Length > 0)
                {
                    // No charset parameter: the camera answers 415 when one is present.
                    request.Append("Content-Type: application/json\r\n")
                           .Append("Content-Length: ").Append(payload.Length).Append("\r\n");
                }

                request.Append("\r\n");

                await stream.WriteAsync(Encoding.ASCII.GetBytes(request.ToString()), token).ConfigureAwait(false);
                if (payload.Length > 0)
                {
                    await stream.WriteAsync(payload, token).ConfigureAwait(false);
                }

                await stream.FlushAsync(token).ConfigureAwait(false);

                var raw = await ReadToEndAsync(stream, token).ConfigureAwait(false);
                return Parse(raw, uri);
            }
            finally
            {
                if (ssl is not null)
                {
                    await ssl.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new UloApiException($"Call '{method} /{relativePath}' timed out after {options.Timeout}.", null, relativePath);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException)
        {
            throw new UloApiException($"Call '{method} /{relativePath}' failed: {ex.Message}", null, relativePath, null, ex);
        }
    }

    /// <summary>
    /// Certificate policy for the raw transport. The camera's certificate is never validated - it
    /// cannot be, since nothing trusts <c>Mu Design CA</c> or a self-signed <c>CN=localhost</c>, and
    /// both certificates carry a fixed expiry (2027 and 2028) that the device will never renew. Only
    /// a pinned thumbprint is compared, and a thumbprint does not expire.
    /// </summary>
    private static RemoteCertificateValidationCallback CreateValidationCallback(UloConnectionOptions options)
        => (_, certificate, _, _) => options.IsAcceptableCertificate(certificate);

    private static async Task<byte[]> ReadToEndAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static UloRawHttpResponse Parse(byte[] raw, Uri uri)
    {
        var separator = IndexOf(raw, "\r\n\r\n"u8);
        var separatorLength = 4;
        if (separator < 0)
        {
            separator = IndexOf(raw, "\n\n"u8);
            separatorLength = 2;
        }

        if (separator < 0)
        {
            throw new UloApiException($"The camera returned no readable response for '{uri.PathAndQuery}'.");
        }

        var headerText = Encoding.ASCII.GetString(raw, 0, separator);
        var bodyStart = separator + separatorLength;
        var body = new byte[raw.Length - bodyStart];
        Array.Copy(raw, bodyStart, body, 0, body.Length);

        var lines = headerText.Split('\n');
        var statusLine = lines.Length > 0 ? lines[0].TrimEnd('\r') : "";
        var parts = statusLine.Split(' ', 3);
        if (parts.Length < 2 || !parts[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[1], out var statusCode))
        {
            throw new UloApiException($"The camera returned an unreadable status line '{statusLine}' for '{uri.PathAndQuery}'.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var malformed = new List<string>();

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                // The firmware quirk this whole class exists for.
                malformed.Add(line.Trim());
                continue;
            }

            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            headers[name] = headers.TryGetValue(name, out var existing) ? existing + ", " + value : value;
        }

        if (headers.TryGetValue("Transfer-Encoding", out var encoding) &&
            encoding.Contains("chunked", StringComparison.OrdinalIgnoreCase))
        {
            body = DecodeChunked(body);
        }
        else if (headers.TryGetValue("Content-Length", out var lengthText) &&
                 int.TryParse(lengthText, out var length) && length >= 0 && length < body.Length)
        {
            var trimmed = new byte[length];
            Array.Copy(body, trimmed, length);
            body = trimmed;
        }

        return new UloRawHttpResponse((HttpStatusCode)statusCode, headers, body, malformed);
    }

    private static byte[] DecodeChunked(byte[] body)
    {
        using var output = new MemoryStream();
        var offset = 0;

        while (offset < body.Length)
        {
            var lineEnd = IndexOf(body, "\r\n"u8, offset);
            if (lineEnd < 0)
            {
                break;
            }

            var sizeText = Encoding.ASCII.GetString(body, offset, lineEnd - offset).Split(';')[0].Trim();
            if (!int.TryParse(sizeText, System.Globalization.NumberStyles.HexNumber, null, out var size) || size <= 0)
            {
                break;
            }

            offset = lineEnd + 2;
            size = Math.Min(size, body.Length - offset);
            if (size <= 0)
            {
                break;
            }

            output.Write(body, offset, size);
            offset += size + 2;
        }

        return output.ToArray();
    }

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle, int start = 0)
    {
        var index = haystack.AsSpan(start).IndexOf(needle);
        return index < 0 ? -1 : index + start;
    }
}
