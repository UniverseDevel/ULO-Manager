using System.Text.Json.Serialization;

namespace UloManager.Core;

/// <summary>
/// Parsed firmware version string (e.g. "06.0601", "08.0904", "10.1308").
/// The first two digits are the major version and the last four are the minor version.
/// Versions are comparable so that firmware-specific behaviour can be gated cleanly.
/// </summary>
public readonly struct UloFirmwareVersion : IComparable<UloFirmwareVersion>, IEquatable<UloFirmwareVersion>
{
    /// <summary>The raw version string as returned by the camera.</summary>
    public string Raw { get; }

    /// <summary>Numeric value used for ordering (e.g. 06.0601 → 60601).</summary>
    public int Numeric { get; }

    public UloFirmwareVersion(string? raw)
    {
        Raw = raw?.Trim() ?? "";
        Numeric = ParseNumeric(Raw);
    }

    private static int ParseNumeric(string raw)
    {
        // "08.0904" → 80904,  "06.0601" → 60601,  "10.1308" → 101308
        var cleaned = raw.Replace(".", "");
        return int.TryParse(cleaned, out var n) ? n : 0;
    }

    // ── Known version thresholds ──────────────────────────────────────────

    /// <summary>Firmware 06.0601 — earliest version tested with this tooling.</summary>
    public static readonly UloFirmwareVersion V06_0601 = new("06.0601");

    /// <summary>Firmware 08.0701.</summary>
    public static readonly UloFirmwareVersion V08_0701 = new("08.0701");

    /// <summary>Firmware 08.0904 — /logs/ exposed, POST on system/log, accessEverywhere present.</summary>
    public static readonly UloFirmwareVersion V08_0904 = new("08.0904");

    /// <summary>Firmware 10.1308 — latest known version from the vendor.</summary>
    public static readonly UloFirmwareVersion V10_1308 = new("10.1308");

    /// <summary>Threshold at or above which the log download uses POST → ZIP instead of GET → text.</summary>
    public static readonly UloFirmwareVersion LogPostThreshold = new("08.0000");

    // ── Version capability checks ─────────────────────────────────────────
    // For fine-grained endpoint compatibility, use UloEndpointRegistry instead.

    /// <summary>True when POST /api/v1/system/log returns a ZIP file name rather than GET returning raw text.</summary>
    public bool UsesLogPost => this >= LogPostThreshold;

    /// <summary>True when /api/v1/accessEverywhere exists.</summary>
    public bool HasAccessEverywhere
        => UloEndpointRegistry.Get("AccessEverywhere")?.IsAvailableOn(this) == true;

    public bool IsKnown => Numeric > 0;

    // ── Comparison / equality ─────────────────────────────────────────────

    public int CompareTo(UloFirmwareVersion other) => Numeric.CompareTo(other.Numeric);
    public bool Equals(UloFirmwareVersion other) => Numeric == other.Numeric;
    public override bool Equals(object? obj) => obj is UloFirmwareVersion v && Equals(v);
    public override int GetHashCode() => Numeric;
    public override string ToString() => Raw;

    public static bool operator ==(UloFirmwareVersion a, UloFirmwareVersion b) => a.Numeric == b.Numeric;
    public static bool operator !=(UloFirmwareVersion a, UloFirmwareVersion b) => a.Numeric != b.Numeric;
    public static bool operator <(UloFirmwareVersion a, UloFirmwareVersion b) => a.Numeric < b.Numeric;
    public static bool operator >(UloFirmwareVersion a, UloFirmwareVersion b) => a.Numeric > b.Numeric;
    public static bool operator <=(UloFirmwareVersion a, UloFirmwareVersion b) => a.Numeric <= b.Numeric;
    public static bool operator >=(UloFirmwareVersion a, UloFirmwareVersion b) => a.Numeric >= b.Numeric;
}

/// <summary>Recording mode of the camera (usage mode).</summary>
public enum UloMode
{
    /// <summary>Awake, not recording.</summary>
    Standard,

    /// <summary>Awake and recording.</summary>
    Spy,

    /// <summary>Asleep and recording, reacts to movement.</summary>
    Alert,
}

/// <summary>How the application is currently talking to the camera.</summary>
public enum UloOperatingMode
{
    /// <summary>Signed in with an administrator account - full setup/configuration surface available.</summary>
    AdminSetup,

    /// <summary>Signed in with a normal account - day to day operations only.</summary>
    Usage,
}

/// <summary>
/// The mode the camera itself reports through <c>state.config</c>.
/// It flips to <see cref="Setup"/> while the camera is being configured
/// (admin/setup mode) and back to <see cref="Usage"/> for normal operation.
/// </summary>
public enum UloDeviceMode
{
    /// <summary>Camera is in its configuration / setup mode.</summary>
    Setup,

    /// <summary>Camera is in normal usage mode.</summary>
    Usage,
}

public static class UloModeExtensions
{
    public static string ToApiValue(this UloMode mode) => mode switch
    {
        UloMode.Standard => "standard",
        UloMode.Spy => "spy",
        UloMode.Alert => "alert",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown ULO mode."),
    };

    public static UloMode ParseMode(string value) => value?.Trim().ToLowerInvariant() switch
    {
        "standard" => UloMode.Standard,
        "spy" => UloMode.Spy,
        "alert" => UloMode.Alert,
        _ => throw new UloApiException($"Camera reported unknown mode '{value}'."),
    };
}

/// <summary>Files that can be purged from the internal storage.</summary>
public enum UloCleanPeriod
{
    OldestDay = 0,
    OldestWeek = 1,
    OldestYear = 2,
    LatestDay = 3,
    LatestWeek = 4,
    LatestYear = 5,
    All = 6,
}

public sealed class UloState
{
    [JsonPropertyName("batteryLevel")]
    public int BatteryLevel { get; set; }

    /// <summary>
    /// True while the camera sits in its configuration / setup mode.
    /// It is false during normal usage - it is NOT an "initial setup finished" flag.
    /// </summary>
    [JsonPropertyName("config")]
    public bool InSetupMode { get; set; }

    [JsonPropertyName("firmwareStatus")]
    public string FirmwareStatus { get; set; } = "none";

    /// <summary>True once an administrator account exists on the camera.</summary>
    [JsonPropertyName("hasAdmin")]
    public bool HasAdmin { get; set; }

    [JsonPropertyName("plugged")]
    public bool Plugged { get; set; }

    /// <summary>
    /// Device language, present on firmware 08.0904+. Null on older firmware.
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonIgnore]
    public UloDeviceMode DeviceMode => InSetupMode ? UloDeviceMode.Setup : UloDeviceMode.Usage;
}

public sealed class UloStorageStats
{
    [JsonPropertyName("internal")]
    public UloStorageArea Internal { get; set; } = new();

    [JsonPropertyName("sdcard")]
    public UloSdCard SdCard { get; set; } = new();
}

public class UloStorageArea
{
    [JsonPropertyName("freeMB")]
    public int FreeMb { get; set; }

    [JsonPropertyName("totalMB")]
    public int TotalMb { get; set; }

    [JsonIgnore]
    public int UsedMb => Math.Max(0, TotalMb - FreeMb);

    [JsonIgnore]
    public double UsedPercent => TotalMb <= 0 ? 0 : Math.Round(UsedMb * 100.0 / TotalMb, 1);
}

public sealed class UloSdCard : UloStorageArea
{
    [JsonPropertyName("inserted")]
    public bool Inserted { get; set; }
}

public sealed class UloConfiguration
{
    [JsonPropertyName("access")]
    public UloAccessConfig Access { get; set; } = new();

    [JsonPropertyName("alert")]
    public UloAlertConfig Alert { get; set; } = new();

    [JsonPropertyName("device")]
    public UloDeviceConfig Device { get; set; } = new();

    [JsonPropertyName("email")]
    public UloEmailConfig Email { get; set; } = new();

    [JsonPropertyName("exclusion")]
    public UloExclusionConfig Exclusion { get; set; } = new();

    [JsonPropertyName("eyes")]
    public UloEyesConfig Eyes { get; set; } = new();

    [JsonPropertyName("face")]
    public UloNotificationConfig Face { get; set; } = new();

    [JsonPropertyName("firmware")]
    public UloFirmwareConfig Firmware { get; set; } = new();

    [JsonPropertyName("language")]
    public UloLanguageConfig Language { get; set; } = new();

    [JsonPropertyName("time")]
    public UloTimeConfig Time { get; set; } = new();

    [JsonPropertyName("video")]
    public UloVideoConfig Video { get; set; } = new();

    [JsonPropertyName("voice")]
    public UloVoiceConfig Voice { get; set; } = new();

    [JsonPropertyName("wifi")]
    public UloWifiConfig Wifi { get; set; } = new();
}

public sealed class UloAccessConfig
{
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "private";
}

/// <summary>
/// Remote access information returned by <c>/api/v1/accessEverywhere</c>.
/// Contains the device's unique identifier (<see cref="TrimmedMac"/>), which
/// is the last four hex digits of the MAC address and forms the <c>ulo_xxxx</c> device ID.
/// Available on firmware 08.0904 and later.
/// </summary>
public sealed class UloAccessEverywhereInfo
{
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = "";

    /// <summary>Last 4 hex digits of the device MAC address — the device identifier (e.g. "ab12" → "ulo_ab12").</summary>
    [JsonPropertyName("trimmedMac")]
    public string TrimmedMac { get; set; } = "";

    [JsonPropertyName("externalIP")]
    public string ExternalIp { get; set; } = "";

    [JsonPropertyName("externalPort")]
    public int ExternalPort { get; set; }

    [JsonPropertyName("externalHttpPort")]
    public int ExternalHttpPort { get; set; }

    [JsonPropertyName("externalStreamingPort")]
    public int ExternalStreamingPort { get; set; }

    [JsonPropertyName("iOSAgentPresent")]
    public bool IOSAgentPresent { get; set; }

    [JsonPropertyName("inError")]
    public bool InError { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    /// <summary>The full device identifier, e.g. "ulo_ab12".</summary>
    [JsonIgnore]
    public string DeviceId => string.IsNullOrEmpty(TrimmedMac) ? "" : $"ulo_{TrimmedMac}";
}

public sealed class UloAlertConfig
{
    [JsonPropertyName("disableOnAppRequest")]
    public bool DisableOnAppRequest { get; set; }

    [JsonPropertyName("disableOnDoubleTap")]
    public bool DisableOnDoubleTap { get; set; }

    [JsonPropertyName("disableOnRecognizedUser")]
    public bool DisableOnRecognizedUser { get; set; }
}

public sealed class UloDeviceConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class UloEmailConfig
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = "";

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; } = 25;

    [JsonPropertyName("server")]
    public string Server { get; set; } = "";

    [JsonPropertyName("ssl")]
    public bool Ssl { get; set; }
}

/// <summary>Region of the picture ignored by movement detection.</summary>
public sealed class UloExclusionConfig
{
    [JsonPropertyName("top")]
    public int Top { get; set; }

    [JsonPropertyName("left")]
    public int Left { get; set; }

    [JsonPropertyName("bottom")]
    public int Bottom { get; set; }

    [JsonPropertyName("right")]
    public int Right { get; set; }

    [JsonPropertyName("reverse")]
    public bool Reverse { get; set; }

    [JsonPropertyName("resetOnDisplacement")]
    public bool ResetOnDisplacement { get; set; }
}

public sealed class UloEyesConfig
{
    [JsonPropertyName("irisHue")]
    public int IrisHue { get; set; }

    [JsonPropertyName("irisSize")]
    public int IrisSize { get; set; }

    [JsonPropertyName("pupilSize")]
    public int PupilSize { get; set; }

    [JsonPropertyName("reflection")]
    public string Reflection { get; set; } = "circles";
}

/// <summary>Per-mode notification switches (used by both the face and voice sections).</summary>
public class UloNotificationConfig
{
    [JsonPropertyName("alert")]
    public bool Alert { get; set; }

    [JsonPropertyName("battery")]
    public bool Battery { get; set; }

    [JsonPropertyName("spy")]
    public bool Spy { get; set; }

    [JsonPropertyName("standard")]
    public bool Standard { get; set; }
}

public sealed class UloVoiceConfig : UloNotificationConfig
{
    [JsonPropertyName("commands")]
    public UloVoiceCommands Commands { get; set; } = new();
}

public sealed class UloVoiceCommands
{
    [JsonPropertyName("alertOff")]
    public bool AlertOff { get; set; }

    [JsonPropertyName("alertOn")]
    public bool AlertOn { get; set; }

    [JsonPropertyName("goToSleep")]
    public bool GoToSleep { get; set; }

    [JsonPropertyName("startVideo")]
    public bool StartVideo { get; set; }

    [JsonPropertyName("stopVideo")]
    public bool StopVideo { get; set; }

    [JsonPropertyName("takePicture")]
    public bool TakePicture { get; set; }
}

public sealed class UloFirmwareConfig
{
    [JsonPropertyName("cloudversion")]
    public string CloudVersion { get; set; } = "";

    [JsonPropertyName("currentversion")]
    public string CurrentVersion { get; set; } = "";

    [JsonPropertyName("firmwareStatus")]
    public string FirmwareStatus { get; set; } = "none";

    [JsonPropertyName("updateAvailable")]
    public bool UpdateAvailable { get; set; }
}

/// <summary>Over-the-air firmware download status.</summary>
public sealed class UloFotaStatus
{
    /// <summary>-1 when no download is in progress.</summary>
    [JsonPropertyName("isDownload")]
    public int IsDownload { get; set; } = -1;

    [JsonPropertyName("percentageDownload")]
    public int PercentageDownload { get; set; }

    [JsonIgnore]
    public bool IsDownloading => IsDownload >= 0;
}

/// <summary>Complete answer to "is there an update?", gathered from every endpoint the camera offers.</summary>
public sealed record UloUpdateReport(
    UloFirmwareConfig Firmware,
    UloFotaStatus Fota,
    int PendingDownloads,
    bool InstallAvailable)
{
    public bool UpdateAvailable => Firmware.UpdateAvailable || PendingDownloads > 0;

    /// <summary>Human readable summary, suitable for a message box or a log line.</summary>
    public string Describe()
    {
        var lines = new List<string>
        {
            $"Installed firmware  : {Firmware.CurrentVersion}",
            $"Version in cloud    : {(string.IsNullOrWhiteSpace(Firmware.CloudVersion) ? "unknown" : Firmware.CloudVersion)}",
            $"Camera reports      : {(Firmware.UpdateAvailable ? "an update is available" : "no update available")}",
            $"Updates to download : {PendingDownloads}",
            $"Download running    : {(Fota.IsDownloading ? $"yes, {Fota.PercentageDownload}%" : "no")}",
            $"Ready to install    : {(InstallAvailable ? "yes" : "no")}",
            $"Firmware status     : {Firmware.FirmwareStatus}",
            string.Empty,
            UpdateAvailable
                ? "An update is waiting - use 'Download firmware update'."
                : "The camera is up to date. Mu Design's update servers are long gone, so the cloud " +
                  "version normally just mirrors the installed one.",
        };

        return string.Join(Environment.NewLine, lines);
    }
}

public sealed class UloLanguageConfig
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";
}

public sealed class UloTimeConfig
{
    [JsonPropertyName("auto")]
    public bool Auto { get; set; }

    [JsonPropertyName("timeZone")]
    public string TimeZone { get; set; } = "";
}

public sealed class UloVideoConfig
{
    [JsonPropertyName("quality")]
    public string Quality { get; set; } = "720p";
}

public sealed class UloWifiConfig
{
    [JsonPropertyName("ssid")]
    public string Ssid { get; set; } = "";

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

public sealed class UloWifiNetwork
{
    [JsonPropertyName("ssid")]
    public string Ssid { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("secured")]
    public bool Secured { get; set; }
}

public sealed class UloLanguageInfo
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>
/// One country from <c>GET /api/v1/config/time/countries</c>. The camera sends the whole table in a
/// single response, each country carrying the time zones it accepts, so the zone list never needs a
/// second call - confirmed on both 06.0601 and 10.1308.
/// </summary>
public sealed class UloCountryInfo
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("timeZones")]
    public List<string> TimeZones { get; set; } = new();

    public override string ToString() => Name;
}

public sealed class UloUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("account")]
    public string Account { get; set; } = "user";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("emailAlert")]
    public bool EmailAlert { get; set; }

    [JsonPropertyName("emailSpy")]
    public bool EmailSpy { get; set; }

    [JsonPropertyName("pushAlert")]
    public bool PushAlert { get; set; }

    [JsonPropertyName("pushSpy")]
    public bool PushSpy { get; set; }

    [JsonPropertyName("faceConnect")]
    public bool FaceConnect { get; set; }

    [JsonPropertyName("devices")]
    public List<UloUserDevice> Devices { get; set; } = new();

    [JsonPropertyName("notifications")]
    public List<UloUserNotification> Notifications { get; set; } = new();

    [JsonIgnore]
    public bool IsAdmin => string.Equals(Account, "admin", StringComparison.OrdinalIgnoreCase);
}

public sealed class UloUserDevice
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("notify")]
    public bool Notify { get; set; }
}

public sealed class UloUserNotification
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "";

    [JsonPropertyName("enabledInAlert")]
    public bool EnabledInAlert { get; set; }

    [JsonPropertyName("enabledInSpy")]
    public bool EnabledInSpy { get; set; }
}

public enum UloMediaType
{
    All,
    Video,
    Snapshot,
}

public sealed class UloMediaFile
{
    public required string Path { get; init; }

    public required string Day { get; init; }

    public required string FileName { get; init; }

    public DateTime? Timestamp { get; init; }

    public UloMediaType Type { get; init; }

    public override string ToString() => Path;
}

/// <summary>A single line of the camera system log.</summary>
public sealed class UloLogEntry
{
    public required string RawLine { get; init; }

    /// <summary>Timestamp as reported by the camera, null when the line could not be parsed.</summary>
    public DateTime? Timestamp { get; init; }

    public required string Message { get; init; }

    public UloLogSeverity Severity { get; init; } = UloLogSeverity.Info;

    /// <summary>Short human friendly description of what the camera is doing, when recognised.</summary>
    public string? Activity { get; init; }

    public override string ToString() => RawLine;
}

public enum UloLogSeverity
{
    Info,
    Notice,
    Warning,
    Error,
}
