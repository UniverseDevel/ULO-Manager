namespace UloManager.Core;

/// <summary>Connection settings used to reach a ULO camera.</summary>
public sealed class UloConnectionOptions
{
    public string Host { get; set; } = "";

    public string UserName { get; set; } = "";

    public string Password { get; set; } = "";

    public bool UseHttps { get; set; }

    /// <summary>
    /// Accept the camera's own TLS certificate. There is nothing to validate against: on 06.0601 it
    /// is self-signed <c>CN=localhost</c>, and on 10.1308 it is issued by <c>Mu Design CA</c>, a
    /// private authority that is in no trust store on earth. The chain therefore cannot be built and
    /// the host name cannot match, so HTTPS is only usable at all when the certificate is taken on
    /// trust. Set <see cref="PinnedCertificateThumbprint"/> as well to accept only the certificate
    /// you expect.
    /// </summary>
    public bool AcceptDeviceCertificate { get; set; } = true;

    /// <summary>True when the connection runs over TLS, in which case the certificate is never validated.</summary>
    public bool UsesTls =>
        UseHttps ||
        Host.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// SHA-1 thumbprint the camera's certificate must match, with or without separators. When set,
    /// only that exact certificate is accepted, which restores protection against interception even
    /// though the certificate is self-signed. Requires <see cref="AcceptDeviceCertificate"/>.
    /// </summary>
    public string? PinnedCertificateThumbprint { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Re-login this long before the token expiry reported by the camera.</summary>
    public TimeSpan TokenRefreshMargin { get; set; } = TimeSpan.FromSeconds(60);

    public Uri BaseAddress
    {
        get
        {
            var host = Host.Trim();
            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(host.TrimEnd('/') + "/");
            }

            return new Uri($"{(UseHttps ? "https" : "http")}://{host.TrimEnd('/')}/");
        }
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Camera host must be provided.");
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            throw new InvalidOperationException("User name must be provided.");
        }
    }

    /// <summary>The pinned thumbprint reduced to comparable form, or null when no pin is set.</summary>
    internal string? NormalisedThumbprint =>
        PinnedCertificateThumbprint is null
            ? null
            : new string(PinnedCertificateThumbprint.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>
    /// The only certificate check this application makes: does it match the pinned thumbprint, if
    /// one was given. Chain, authority, host name and expiry are all ignored deliberately - none of
    /// them can ever succeed against this device, and the certificates carry fixed expiry dates
    /// (2027-01-18 on 06.0601, 2028-07-07 on 10.1308) that nothing on the camera will renew, so
    /// enforcing them would only break the application on a working camera. Unverified TLS is still
    /// worth having over plain HTTP: it stops a passive observer reading the password, the token and
    /// the video, which is what the local network threat actually looks like.
    /// </summary>
    public bool IsAcceptableCertificate(System.Security.Cryptography.X509Certificates.X509Certificate? certificate)
    {
        var pinned = NormalisedThumbprint;
        if (pinned is null)
        {
            return true;
        }

        var presented = certificate switch
        {
            null => null,
            System.Security.Cryptography.X509Certificates.X509Certificate2 typed => typed.GetCertHashString(),
            _ => new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate).GetCertHashString(),
        };

        return string.Equals(presented, pinned, StringComparison.OrdinalIgnoreCase);
    }
}
