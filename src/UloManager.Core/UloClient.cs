using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UloManager.Core;

/// <summary>
/// Low level ULO REST client. Handles OAuth style login, bearer token lifetime,
/// transparent re-login and raw API access (both in setup/admin mode and usage mode).
/// </summary>
public sealed class UloClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    /// <summary>
    /// Paths whose responses cannot be parsed by <see cref="HttpClient"/> and have to go through
    /// <see cref="UloRawHttp"/>. Seeded from the endpoint registry and extended at run time whenever
    /// a camera is caught breaking the protocol.
    /// </summary>
    private readonly HashSet<string> _tolerantPaths = new(StringComparer.OrdinalIgnoreCase);

    private string? _token;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;
    private int _userId;

    public UloClient(UloConnectionOptions options, HttpClient? httpClient = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();

        if (httpClient is null)
        {
            _http = new HttpClient(CreateHandler(options));
            _ownsHttpClient = true;
        }
        else
        {
            _http = httpClient;
        }

        _http.BaseAddress = options.BaseAddress;
        _http.Timeout = options.Timeout;

        foreach (var endpoint in UloEndpointRegistry.All.Where(e => e.MalformedResponse))
        {
            _tolerantPaths.Add(endpoint.Path);
        }
    }

    public UloConnectionOptions Options { get; }

    /// <summary>
    /// Builds the handler for the camera's certificate.
    ///
    /// <para>
    /// The normal chain checks can never pass and are therefore never used: on 06.0601 the
    /// certificate is self-signed <c>CN=localhost</c>, and on 10.1308 it is issued to
    /// <c>CN=*.ulo.camera</c> by the vendor's own CA, which no machine trusts. Just as importantly,
    /// both have a fixed expiry - 2027-01-18 and 2028-07-07 - and nothing on the device ever renews
    /// them, so a client that honoured the expiry date would simply stop working on a camera that is
    /// otherwise perfectly healthy. Expiry, host name and chain are all ignored on purpose; a pinned
    /// thumbprint, which does not expire, is the only check applied.
    /// </para>
    /// </summary>
    private static HttpClientHandler CreateHandler(UloConnectionOptions options)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };

        if (!options.UsesTls)
        {
            return handler;
        }

        handler.ServerCertificateCustomValidationCallback =
            (_, certificate, _, _) => options.IsAcceptableCertificate(certificate);

        return handler;
    }

    public bool IsLoggedIn => _token is not null && DateTimeOffset.UtcNow < _tokenExpiresAt;

    public int UserId => _userId;

    public string? Token => _token;

    /// <summary>Raised for every API request/response, useful for the activity log in the UI.</summary>
    public event EventHandler<UloTraceEventArgs>? Trace;

    public async Task<UloLoginResult> LoginAsync(CancellationToken ct = default)
    {
        await _loginLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await LoginCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task<UloLoginResult> LoginCoreAsync(CancellationToken ct)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Options.UserName}:{Options.Password}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/login")
        {
            Content = CreateJsonContent("{ \"iOSAgent\": false }"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new UloAuthenticationException($"Camera at {Options.BaseAddress} could not be reached: {ex.Message}", inner: ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            OnTrace("POST", "/api/v1/login", response.StatusCode, "<credentials>");

            if (!response.IsSuccessStatusCode)
            {
                throw new UloAuthenticationException(
                    $"Login of '{Options.UserName}' failed with status {(int)response.StatusCode} {response.StatusCode}.",
                    response.StatusCode,
                    body);
            }

            var json = JsonNode.Parse(body) as JsonObject
                       ?? throw new UloAuthenticationException("Login response was not a JSON object.", response.StatusCode, body);

            _token = json["token"]?.GetValue<string>()
                     ?? throw new UloAuthenticationException("Login response did not contain a token.", response.StatusCode, body);
            var expiresIn = json["expiresIn"]?.GetValue<int>() ?? 3599;
            _userId = json["userId"]?.GetValue<int>() ?? 0;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn) - Options.TokenRefreshMargin;

            return new UloLoginResult(_token, _userId, TimeSpan.FromSeconds(expiresIn));
        }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (_token is null)
        {
            return;
        }

        try
        {
            await SendCoreAsync(HttpMethod.Post, "api/v1/logout", "{}", allowRetry: false, ct).ConfigureAwait(false);
        }
        catch (UloApiException)
        {
            // Logging out is best effort - the token dies with the session anyway.
        }
        finally
        {
            _token = null;
            _tokenExpiresAt = DateTimeOffset.MinValue;
        }
    }

    public async Task EnsureLoggedInAsync(CancellationToken ct = default)
    {
        if (IsLoggedIn)
        {
            return;
        }

        await _loginLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!IsLoggedIn)
            {
                await LoginCoreAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _loginLock.Release();
        }
    }

    /// <summary>Calls the API and returns the raw response body.</summary>
    public async Task<string> SendAsync(HttpMethod method, string path, string? body = null, CancellationToken ct = default)
        => (await SendDetailedAsync(method, path, body, ct).ConfigureAwait(false)).Body;

    /// <summary>
    /// Calls the API and reports both the body and whether the camera answered with a malformed
    /// response. Firmware 10.1308 breaks the HTTP framing of <c>POST /api/v1/snapshot</c> and drops
    /// the session at the same time, so callers need to know that it happened.
    /// </summary>
    public async Task<UloResponse> SendDetailedAsync(HttpMethod method, string path, string? body = null, CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);
        return await SendCoreAsync(method, path, body, allowRetry: true, ct).ConfigureAwait(false);
    }

    /// <summary>Forgets the current token so the next call logs in again.</summary>
    public void InvalidateSession()
    {
        _token = null;
        _tokenExpiresAt = DateTimeOffset.MinValue;
    }

    private async Task<UloResponse> SendCoreAsync(HttpMethod method, string path, string? body, bool allowRetry, CancellationToken ct)
        => await SendCoreAsync(method, path, body, allowRetry ? LoginRetries : 0, ct).ConfigureAwait(false);

    /// <summary>
    /// How often a call may log in again after a <c>401</c>. More than one attempt is needed because
    /// the camera keeps a single session per account: when another client signs in with the same
    /// credentials the two can evict each other for a moment.
    /// </summary>
    private const int LoginRetries = 3;

    private async Task<UloResponse> SendCoreAsync(HttpMethod method, string path, string? body, int retriesLeft, CancellationToken ct)

    {
        var relative = path.TrimStart('/');

        HttpStatusCode status;
        string responseBody;
        var malformed = false;

        if (RequiresTolerantTransport(relative))
        {
            var raw = await UloRawHttp.SendAsync(_http.BaseAddress!, method.Method, relative, body, _token, Options, ct)
                .ConfigureAwait(false);
            status = raw.Status;
            responseBody = raw.BodyText;
            malformed = raw.HasMalformedHeaders;
        }
        else
        {
            try
            {
                (status, responseBody) = await SendViaHttpClientAsync(method, relative, body, ct).ConfigureAwait(false);
            }
            catch (UloMalformedResponseException)
            {
                // The camera broke the HTTP framing. Remember it so the call is not made twice again
                // (a repeated snapshot would cost another picture) and replay it tolerantly.
                MarkTolerantTransport(relative);
                var raw = await UloRawHttp.SendAsync(_http.BaseAddress!, method.Method, relative, body, _token, Options, ct)
                    .ConfigureAwait(false);
                status = raw.Status;
                responseBody = raw.BodyText;
                malformed = raw.HasMalformedHeaders;
            }
        }

        OnTrace(method.Method, "/" + relative, status, body);

        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden && retriesLeft > 0)
        {
            // Session died (camera reboot, timeout, someone else logged in) - sign in again.
            InvalidateSession();

            if (retriesLeft < LoginRetries)
            {
                // Something else is holding the single session slot; give it a moment to finish.
                await Task.Delay(TimeSpan.FromMilliseconds(750), ct).ConfigureAwait(false);
            }

            await EnsureLoggedInAsync(ct).ConfigureAwait(false);
            return await SendCoreAsync(method, path, body, retriesLeft - 1, ct).ConfigureAwait(false);
        }

        if (status is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            if (status is HttpStatusCode.Unauthorized &&
                responseBody.Contains("Session does not exist", StringComparison.OrdinalIgnoreCase))
            {
                throw new UloSessionEvictedException(Options.UserName);
            }

            // Several operations (settings backups among them) are refused unless the camera sits in
            // the standard recording mode. The camera reports that as a 404, which on its own reads
            // like a missing endpoint.
            if (responseBody.Contains("switch to Standard mode", StringComparison.OrdinalIgnoreCase))
            {
                throw new UloModeRequiredException(path, status, responseBody);
            }

            throw new UloApiException(
                $"Call '{method} /{relative}' failed with status {(int)status} {status}.",
                status,
                path,
                responseBody);
        }

        return new UloResponse(responseBody, status, malformed);
    }

    private async Task<(HttpStatusCode Status, string Body)> SendViaHttpClientAsync(
        HttpMethod method, string relative, string? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, relative);
        if (!string.IsNullOrEmpty(body))
        {
            request.Content = CreateJsonContent(body);
        }

        if (_token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsMalformedResponse(ex))
        {
            throw new UloMalformedResponseException(relative, ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new UloApiException($"Call '{method} /{relative}' failed: {ex.Message}", null, relative, null, ex);
        }

        using (response)
        {
            return (response.StatusCode, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        }
    }

    /// <summary>
    /// Recognises the framing errors produced by the camera's own web server, as opposed to a real
    /// network failure. These are the responses <see cref="HttpClient"/> refuses to parse at all.
    /// </summary>
    private static bool IsMalformedResponse(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is not HttpRequestException && ex is not InvalidOperationException)
            {
                continue;
            }

            var message = ex.Message;
            if (message.Contains("invalid header", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("invalid response header", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unrecognized response", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("malformed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool RequiresTolerantTransport(string relative)
    {
        lock (_tolerantPaths)
        {
            return _tolerantPaths.Contains(relative);
        }
    }

    private void MarkTolerantTransport(string relative)
    {
        lock (_tolerantPaths)
        {
            _tolerantPaths.Add(relative);
        }
    }

    public async Task<JsonNode?> SendJsonAsync(HttpMethod method, string path, string? body = null, CancellationToken ct = default)
    {
        var raw = await SendAsync(method, path, body, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task<JsonNode?> GetJsonAsync(string path, CancellationToken ct = default)
        => SendJsonAsync(HttpMethod.Get, path, null, ct);

    public Task<string> GetRawAsync(string path, CancellationToken ct = default)
        => SendAsync(HttpMethod.Get, path, null, ct);

    public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var raw = await SendAsync(HttpMethod.Get, path, null, ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(raw) ? default : JsonSerializer.Deserialize<T>(raw, UloJson.Options);
    }

    public async Task<T?> SendAsync<T>(HttpMethod method, string path, object? payload, CancellationToken ct = default)
    {
        var body = payload is null ? null : JsonSerializer.Serialize(payload, UloJson.Options);
        var raw = await SendAsync(method, path, body, ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(raw) ? default : JsonSerializer.Deserialize<T>(raw, UloJson.Options);
    }

    /// <summary>Downloads a file served by the camera (media, snapshots, log exports).</summary>
    public Task<long> DownloadFileAsync(string path, string destinationFile, CancellationToken ct = default)
        => DownloadFileCoreAsync(path, destinationFile, LoginRetries, ct);

    private async Task<long> DownloadFileCoreAsync(string path, string destinationFile, int retriesLeft, CancellationToken ct)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        var relative = path.TrimStart('/');
        using var request = new HttpRequestMessage(HttpMethod.Get, relative);
        if (_token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        HttpResponseMessage responseMessage;
        try
        {
            responseMessage = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsMalformedResponse(ex))
        {
            // Same firmware quirk as the API calls - read the file with the tolerant transport.
            return await DownloadFileTolerantAsync(relative, destinationFile, retriesLeft, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new UloApiException($"Download of '/{relative}' failed: {ex.Message}", null, path, null, ex);
        }

        using var response = responseMessage;
        OnTrace("GET", "/" + relative, response.StatusCode, null);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden && retriesLeft > 0)
        {
            // The camera keeps a single session per account, so another login (phone app,
            // web UI, a second copy of this tool) silently evicts ours. Sign in again.
            InvalidateSession();

            if (retriesLeft < LoginRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), ct).ConfigureAwait(false);
            }

            await EnsureLoggedInAsync(ct).ConfigureAwait(false);
            return await DownloadFileCoreAsync(path, destinationFile, retriesLeft - 1, ct).ConfigureAwait(false);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UloApiException(
                $"Download of '/{relative}' failed with status {(int)response.StatusCode} {response.StatusCode}.",
                response.StatusCode,
                path);
        }

        var expected = response.Content.Headers.ContentLength;

        var directory = System.IO.Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = destinationFile + ".part";
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var target = File.Create(temp))
        {
            await source.CopyToAsync(target, ct).ConfigureAwait(false);
        }

        var written = new FileInfo(temp).Length;
        if (expected.HasValue && expected.Value != written)
        {
            File.Delete(temp);
            throw new UloApiException(
                $"Downloaded file size does not match the original (expected {expected}, received {written}).",
                response.StatusCode,
                path);
        }

        File.Move(temp, destinationFile, overwrite: true);
        return written;
    }

    /// <summary>Downloads a file from a camera whose response is not valid HTTP.</summary>
    private async Task<long> DownloadFileTolerantAsync(string relative, string destinationFile, int retriesLeft, CancellationToken ct)
    {
        var raw = await UloRawHttp.SendAsync(_http.BaseAddress!, "GET", relative, null, _token, Options, ct)
            .ConfigureAwait(false);

        OnTrace("GET", "/" + relative, raw.Status, null);

        if (raw.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden && retriesLeft > 0)
        {
            InvalidateSession();
            await EnsureLoggedInAsync(ct).ConfigureAwait(false);
            return await DownloadFileTolerantAsync(relative, destinationFile, retriesLeft - 1, ct).ConfigureAwait(false);
        }

        if (raw.Status is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            throw new UloApiException(
                $"Download of '/{relative}' failed with status {(int)raw.Status} {raw.Status}.",
                raw.Status,
                relative);
        }

        var directory = System.IO.Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(destinationFile, raw.Body, ct).ConfigureAwait(false);
        return raw.Body.Length;
    }

    /// <summary>
    /// The camera validates the content type strictly and rejects the charset parameter
    /// that <see cref="StringContent"/> adds by default (415 Unsupported Media Type).
    /// </summary>
    private static StringContent CreateJsonContent(string body)
    {
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private void OnTrace(string method, string path, HttpStatusCode status, string? requestBody)
        => Trace?.Invoke(this, new UloTraceEventArgs(method, path, status, requestBody));

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }

        _loginLock.Dispose();
    }
}

public sealed record UloLoginResult(string Token, int UserId, TimeSpan ExpiresIn);

/// <summary>An API response together with the framing quirks observed while reading it.</summary>
/// <param name="Body">The response body.</param>
/// <param name="Status">The HTTP status code.</param>
/// <param name="MalformedHeaders">
/// True when the camera emitted header lines that are not valid HTTP. On firmware 10.1308 this
/// accompanies <c>POST /api/v1/snapshot</c>, which also silently drops the session.
/// </param>
public sealed record UloResponse(string Body, HttpStatusCode Status, bool MalformedHeaders);

/// <summary>Raised internally when <see cref="HttpClient"/> refuses to parse the camera's response.</summary>
internal sealed class UloMalformedResponseException : Exception
{
    public UloMalformedResponseException(string path, Exception inner)
        : base($"The camera returned a malformed HTTP response for '/{path}': {inner.Message}", inner)
    {
    }
}

public sealed class UloTraceEventArgs : EventArgs
{
    public UloTraceEventArgs(string method, string path, HttpStatusCode status, string? requestBody)
    {
        Method = method;
        Path = path;
        Status = status;
        RequestBody = requestBody;
        TimestampUtc = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset TimestampUtc { get; }

    public string Method { get; }

    public string Path { get; }

    public HttpStatusCode Status { get; }

    public string? RequestBody { get; }

    public override string ToString() => $"{Method} {Path} -> {(int)Status}";
}
