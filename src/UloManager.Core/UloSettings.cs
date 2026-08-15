using System.Text.Json;

namespace UloManager.Core;

/// <summary>
/// Persists the last-used connection settings (host, user, optionally password) to a JSON file
/// in the user's app-data folder. The password is base-64 encoded — not encrypted — so that it
/// is not stored in plain text but is trivially recoverable. This is acceptable because the
/// camera itself transmits credentials in the clear over HTTP.
/// </summary>
public sealed class UloSettings
{
    private static readonly string DefaultFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UloManager");

    private static readonly string DefaultPath = Path.Combine(DefaultFolder, "settings.json");

    public string? Host { get; set; }
    public string? UserName { get; set; }
    public string? EncodedPassword { get; set; }
    public bool UseHttps { get; set; }
    public string? PinnedCertificateThumbprint { get; set; }

    /// <summary>Colour theme: <c>System</c> (default), <c>Light</c> or <c>Dark</c>.</summary>
    public string Theme { get; set; } = "System";

    /// <summary>Recently discovered ULO camera addresses.</summary>
    public List<KnownCamera> KnownCameras { get; set; } = new();

    /// <summary>A discovered camera entry with display metadata.</summary>
    public sealed class KnownCamera
    {
        public string Address { get; set; } = "";
        public string? Hostname { get; set; }
        public string? DeviceName { get; set; }
        public string? DeviceId { get; set; }
        public string? FirmwareVersion { get; set; }

        /// <summary>Account used for this camera. Falls back to the global user name when empty.</summary>
        public string? UserName { get; set; }

        /// <summary>Base-64 encoded password for this camera, see the note on the class.</summary>
        public string? EncodedPassword { get; set; }

        /// <summary>Try to connect to this camera when the application starts.</summary>
        public bool AutoConnect { get; set; } = true;

        /// <summary>Use HTTPS for this camera.</summary>
        public bool UseHttps { get; set; }

        /// <summary>SHA-1 thumbprint pinned for this camera, or null to accept any certificate.</summary>
        public string? PinnedCertificateThumbprint { get; set; }

        public string GetPassword() => Decode(EncodedPassword);

        public void SetPassword(string? password) => EncodedPassword = Encode(password);

        /// <summary>Label shown in dropdowns.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayLabel
        {
            get
            {
                var parts = new List<string> { Address };
                if (!string.IsNullOrEmpty(Hostname))
                    parts.Add(Hostname);
                if (!string.IsNullOrEmpty(DeviceName))
                    parts.Add($"\"{DeviceName}\"");
                if (!string.IsNullOrEmpty(DeviceId))
                    parts.Add(DeviceId);
                if (!string.IsNullOrEmpty(FirmwareVersion))
                    parts.Add($"fw {FirmwareVersion}");
                return string.Join("  —  ", parts);
            }
        }

        public override string ToString() => DisplayLabel;
    }

    /// <summary>Decodes the stored password, or returns empty when none is stored.</summary>
    public string GetPassword() => Decode(EncodedPassword);

    /// <summary>Encodes and stores the password.</summary>
    public void SetPassword(string? password) => EncodedPassword = Encode(password);

    private static string Decode(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
            return "";

        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch
        {
            return "";
        }
    }

    private static string? Encode(string? password)
        => string.IsNullOrEmpty(password)
            ? null
            : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));

    /// <summary>Updates the settings from the given connection options and saves.</summary>
    public void SaveFrom(UloConnectionOptions options)
    {
        Host = options.Host;
        UserName = options.UserName;
        SetPassword(options.Password);
        UseHttps = options.UseHttps;
        PinnedCertificateThumbprint = options.PinnedCertificateThumbprint;
        Save();
    }

    /// <summary>Applies the stored settings to a connection options instance (fills blanks only).</summary>
    public void ApplyTo(UloConnectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host) && !string.IsNullOrWhiteSpace(Host))
            options.Host = Host;
        if (string.IsNullOrWhiteSpace(options.UserName) && !string.IsNullOrWhiteSpace(UserName))
            options.UserName = UserName;
        if (string.IsNullOrWhiteSpace(options.Password))
            options.Password = GetPassword();
        if (!options.UseHttps && UseHttps)
            options.UseHttps = true;
        if (options.PinnedCertificateThumbprint is null && PinnedCertificateThumbprint is not null)
            options.PinnedCertificateThumbprint = PinnedCertificateThumbprint;
        if (options.UseHttps)
            options.AcceptDeviceCertificate = true;
    }

    /// <summary>Adds or updates a discovered camera in the known list.</summary>
    public void AddKnownCamera(UloDiscovery.UloFoundDevice found)
    {
        var address = found.Address.ToString();
        var existing = KnownCameras.FirstOrDefault(
            c => string.Equals(c.Address, address, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            // Update metadata.
            existing.Hostname = found.Hostname ?? existing.Hostname;
            existing.DeviceName = found.DeviceName ?? existing.DeviceName;
            if (found.Firmware.IsKnown)
                existing.FirmwareVersion = found.Firmware.Raw;
        }
        else
        {
            KnownCameras.Add(new KnownCamera
            {
                Address = address,
                Hostname = found.Hostname,
                DeviceName = found.DeviceName,
                FirmwareVersion = found.Firmware.IsKnown ? found.Firmware.Raw : null,
            });
        }
    }

    // ── Persistence ───────────────────────────────────────────────────────

    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(file, json);
    }

    public static UloSettings Load(string? path = null)
    {
        var file = path ?? DefaultPath;
        if (!File.Exists(file))
            return new UloSettings();

        try
        {
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<UloSettings>(json) ?? new UloSettings();
        }
        catch
        {
            return new UloSettings();
        }
    }
}
