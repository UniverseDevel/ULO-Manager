using System.Net;

namespace UloManager.Core;

public class UloApiException : Exception
{
    public UloApiException(string message, HttpStatusCode? statusCode = null, string? path = null, string? body = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Path = path;
        ResponseBody = body;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? Path { get; }

    public string? ResponseBody { get; }
}

public sealed class UloAuthenticationException : UloApiException
{
    public UloAuthenticationException(string message, HttpStatusCode? statusCode = null, string? body = null, Exception? inner = null)
        : base(message, statusCode, "/api/v1/login", body, inner)
    {
    }
}

/// <summary>
/// Thrown when the camera refuses an operation because it is not in the <c>standard</c> recording
/// mode. Firmware 10.1308 answers such calls with <c>404</c> and
/// <c>"Please switch to Standard mode to do this operation."</c>; settings backups are one example.
/// </summary>
public sealed class UloModeRequiredException : UloApiException
{
    public UloModeRequiredException(string? path, HttpStatusCode? statusCode, string? body)
        : base(
            "The camera allows this only while it is in the 'standard' recording mode. " +
            "Switch the recording mode to standard, run the operation, then set the mode back.",
            statusCode,
            path,
            body)
    {
    }
}
