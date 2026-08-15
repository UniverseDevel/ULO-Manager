using System.Net.Http;

namespace UloManager.Core;

/// <summary>
/// Central registry of all known ULO API endpoints with their firmware version compatibility.
///
/// <para>
/// Each entry records the earliest confirmed firmware version where the endpoint works,
/// the latest confirmed version, and optionally the version where it was removed.
/// This lets the application pick the right endpoint for the connected firmware and show
/// only endpoints known to work on that version.
/// </para>
///
/// <para><b>How to update:</b> when a user confirms an endpoint works (or doesn't) on a
/// firmware version not yet recorded, update the <c>From</c> / <c>To</c> / <c>Removed</c>
/// fields in <see cref="All"/>. No other code needs to change — everything reads from this
/// table.</para>
/// </summary>
public static class UloEndpointRegistry
{
    // ── Endpoint descriptor ───────────────────────────────────────────────

    /// <summary>Status of an endpoint on a particular firmware.</summary>
    public enum EndpointStatus
    {
        /// <summary>Not tested on this firmware.</summary>
        Unknown,

        /// <summary>Confirmed working.</summary>
        Supported,

        /// <summary>Confirmed absent (404) or broken.</summary>
        Unsupported,

        /// <summary>Exists but requires authentication.</summary>
        RequiresAuth,
    }

    /// <summary>Describes a single API endpoint and its version range.</summary>
    public sealed class Endpoint
    {
        /// <summary>A short identifier used in code, e.g. "Snapshot", "BackgroundImage".</summary>
        public required string Id { get; init; }

        /// <summary>HTTP method.</summary>
        public required string Method { get; init; }

        /// <summary>Path relative to the API root, e.g. "api/v1/snapshot".</summary>
        public required string Path { get; init; }

        /// <summary>Earliest firmware version where this endpoint was confirmed working.</summary>
        public required UloFirmwareVersion From { get; init; }

        /// <summary>
        /// Latest firmware version where this endpoint still works. Anything newer is treated as
        /// **not available**, so only set this once a newer firmware has been confirmed to have lost
        /// the endpoint. While it is still present on the newest firmware known, leave it null -
        /// "not tested yet" must never be recorded as an upper limit.
        /// </summary>
        public UloFirmwareVersion? To { get; init; }

        /// <summary>Firmware version where this endpoint was confirmed removed (404). Null = not known to be removed.</summary>
        public UloFirmwareVersion? Removed { get; init; }

        /// <summary>
        /// True for paths that appear in the camera's own web application but answer <c>404</c> or
        /// <c>405</c> on every firmware tested, so they are never worth offering.
        /// </summary>
        public bool NotRouted { get; init; }

        /// <summary>
        /// Newest firmware on which the endpoint was *measured* to be missing (404/403). Anything at
        /// or below this version does not offer it. Left null when no older version was tested -
        /// "not tested" must not be confused with "not there".
        /// </summary>
        public UloFirmwareVersion? AbsentUpTo { get; init; }

        /// <summary>True when no authentication is needed.</summary>
        public bool Unauthenticated { get; init; }

        /// <summary>Human-readable description.</summary>
        public string Description { get; init; } = "";

        /// <summary>Category for grouping in the UI.</summary>
        public string Category { get; init; } = "General";

        /// <summary>An alternative endpoint that provides the same function on other firmware.</summary>
        public string? AlternativeId { get; init; }

        /// <summary>
        /// True when the camera answers this endpoint with a response that is not valid HTTP, so it
        /// has to be read with the tolerant transport instead of <see cref="HttpClient"/>.
        /// </summary>
        public bool MalformedResponse { get; init; }

        /// <summary>Returns whether this endpoint is expected to work on the given firmware.</summary>
        public EndpointStatus StatusOn(UloFirmwareVersion firmware)
        {
            if (NotRouted)
                return EndpointStatus.Unsupported;
            if (!firmware.IsKnown)
                return EndpointStatus.Unknown;
            if (AbsentUpTo.HasValue && firmware <= AbsentUpTo.Value)
                return EndpointStatus.Unsupported;
            if (Removed.HasValue && firmware >= Removed.Value)
                return EndpointStatus.Unsupported;
            if (To.HasValue && firmware > To.Value)
                return EndpointStatus.Unsupported;
            if (firmware >= From)
                return EndpointStatus.Supported;

            // Older than the earliest confirmed version: it may well work, it simply has not been
            // measured on anything older.
            return EndpointStatus.Unknown;
        }

        /// <summary>True when the endpoint is expected to work on the given firmware.</summary>
        public bool IsAvailableOn(UloFirmwareVersion firmware)
            => StatusOn(firmware) is EndpointStatus.Supported or EndpointStatus.Unknown;
    }

    // ── The registry ──────────────────────────────────────────────────────
    //
    // Update this table when users confirm endpoints on new firmware versions.
    //
    // Only record what was actually measured. Every field below either states a fact or stays null;
    // "we have not tried it on that firmware" is never written down as a limit, because that would
    // hide a call that works perfectly well.
    //
    // Columns:
    //   Id              - code identifier
    //   Method          - HTTP method
    //   Path            - URL path (relative)
    //   From            - earliest firmware confirmed to have it
    //   To              - newest firmware that still has it; anything newer is hidden. Set this only
    //                     once a newer firmware was confirmed to have LOST the endpoint. As long as
    //                     it is present on the newest firmware known (10.1308), leave it null.
    //   Removed         - firmware where it was confirmed gone (null = not removed)
    //   AbsentUpTo      - newest firmware measured NOT to have it; that version and older hide it
    //   NotRouted       - listed by the camera's web app but 404/405 on every firmware tested
    //   Unauthenticated - true if no login needed
    //   Category        - grouping
    //   AlternativeId   - fallback endpoint for the same function
    //   Description     - what it does
    //
    // ──────────────────────────────────────────────────────────────────────

    private static readonly UloFirmwareVersion V06 = UloFirmwareVersion.V06_0601;
    private static readonly UloFirmwareVersion V08 = UloFirmwareVersion.V08_0904;
    private static readonly UloFirmwareVersion V10 = UloFirmwareVersion.V10_1308;

    public static readonly Endpoint[] All =
    {
        // ── Session ──────────────────────────────────────────────────
        new() { Id = "Login",      Method = "POST", Path = "api/v1/login",  From = V06, Description = "Authenticate and get bearer token", Category = "Session" },
        new() { Id = "Logout",     Method = "POST", Path = "api/v1/logout", From = V06, Description = "Invalidate the session token",      Category = "Session" },

        // ── State (unauthenticated) ──────────────────────────────────
        new() { Id = "State",      Method = "GET",  Path = "api/v1/state",  From = V06, Unauthenticated = true, Description = "Battery, power, setup mode flag", Category = "State" },
        new() { Id = "FotaInstallAvailable", Method = "GET", Path = "api/v1/interface/fotaIsInstallAvailable", From = V06, Unauthenticated = true, Description = "Is a firmware install pending", Category = "State" },
        new() { Id = "Import",     Method = "GET",  Path = "api/v1/import", From = V06, Unauthenticated = true, Description = "List backup files on SD card", Category = "Backup" },
        new() { Id = "ImportPost", Method = "POST", Path = "api/v1/import", From = V06, Unauthenticated = true, Description = "Import backup from SD card (needs 'name' field)", Category = "Backup" },

        // ── Mode ─────────────────────────────────────────────────────
        new() { Id = "ModeGet",    Method = "GET",  Path = "api/v1/mode",   From = V06, Description = "Get recording mode", Category = "Mode" },
        new() { Id = "ModeSet",    Method = "PUT",  Path = "api/v1/mode",   From = V06, Description = "Set recording mode (standard/spy/alert)", Category = "Mode" },

        // ── Snapshot / camera ────────────────────────────────────────
        new() { Id = "Snapshot",        Method = "POST", Path = "api/v1/snapshot",        From = V06,             MalformedResponse = true, Description = "Take a picture (confirmed on 06.0601 and 10.1308). On 10.1308 the response carries a bare 'success' header line and no file name", Category = "Camera" },
        new() { Id = "BackgroundImage", Method = "POST", Path = "api/v1/backgroundImage", From = V10, AbsentUpTo = V06,             Description = "Take a picture and keep it as the login background - returns media/loginPicture.jpg (measured: 201 on 10.1308, 404 on 06.0601)", Category = "Camera" },
        new() { Id = "RecordGet",       Method = "GET",  Path = "api/v1/record",          From = V06,         Description = "Is an on-demand recording running", Category = "Camera" },
        new() { Id = "RecordSet",       Method = "PUT",  Path = "api/v1/record",          From = V06,         Description = "Start/stop on-demand recording", Category = "Camera" },

        // ── Live stream ──────────────────────────────────────────────
        new() { Id = "LiveWs",     Method = "WS",   Path = "api/v1/live",             From = V06, Unauthenticated = true, Description = "Live H.264 video via WebSocket", Category = "Live" },
        new() { Id = "LiveRtsp",   Method = "RTSP", Path = "rtsp://<host>:8901/live", From = V06, Unauthenticated = true, Description = "Live RTSP stream", Category = "Live" },
        new() { Id = "RtspWs",     Method = "WS",   Path = "api/v1/rtsp",             From = V06, Description = "RTSP signalling WebSocket (sub-protocol mudesign.ulo.rtsp); sends 'Started' text then pings — not a video source", Category = "Live" },

        // ── Configuration ────────────────────────────────────────────
        new() { Id = "Config",          Method = "GET",  Path = "api/v1/config",          From = V06, Description = "Full configuration tree", Category = "Config" },
        new() { Id = "ConfigSet",       Method = "PUT",  Path = "api/v1/config",          From = V06, Description = "Update configuration",   Category = "Config" },
        new() { Id = "ConfigAccess",    Method = "GET",  Path = "api/v1/config/access",   From = V06, Description = "Access config section",  Category = "Config" },
        new() { Id = "ConfigAlert",     Method = "GET",  Path = "api/v1/config/alert",    From = V06, Description = "Alert config section",   Category = "Config" },
        new() { Id = "ConfigDevice",    Method = "GET",  Path = "api/v1/config/device",   From = V06, Description = "Device name",            Category = "Config" },
        new() { Id = "ConfigEmail",     Method = "GET",  Path = "api/v1/config/email",    From = V06, Description = "Email config",           Category = "Config" },
        new() { Id = "ConfigExclusion", Method = "GET",  Path = "api/v1/config/exclusion",From = V06, Description = "Exclusion zone",         Category = "Config" },
        new() { Id = "ConfigEyes",      Method = "GET",   Path = "api/v1/config/eyes",     From = V06, Description = "Eye appearance config",  Category = "Config" },
        new() { Id = "ConfigEyesPatch", Method = "PATCH", Path = "api/v1/config/eyes",     From = V06, Description = "Update eye appearance (irisHue, irisSize, pupilSize, reflection) — instant effect on physical LEDs", Category = "Config" },
        new() { Id = "ConfigFace",      Method = "GET",  Path = "api/v1/config/face",     From = V06, Description = "Face recognition config",Category = "Config" },
        new() { Id = "ConfigFirmware",  Method = "GET",  Path = "api/v1/config/firmware", From = V06, Description = "Firmware version info",  Category = "Config" },
        new() { Id = "ConfigLanguage",  Method = "GET",  Path = "api/v1/config/language", From = V06, Description = "Language setting",       Category = "Config" },
        new() { Id = "ConfigTime",      Method = "GET",  Path = "api/v1/config/time",     From = V06, Description = "Time zone config",       Category = "Config" },
        new() { Id = "ConfigVideo",     Method = "GET",  Path = "api/v1/config/video",    From = V06, Description = "Video quality config",   Category = "Config" },
        new() { Id = "ConfigVoice",     Method = "GET",  Path = "api/v1/config/voice",    From = V06, Description = "Voice config",           Category = "Config" },
        new() { Id = "ConfigVoicePut",  Method = "PUT",  Path = "api/v1/config/voice",    From = V06, Description = "Update voice command and mode settings", Category = "Config" },
        new() { Id = "ConfigFacePut",   Method = "PUT",  Path = "api/v1/config/face",     From = V06, Description = "Update face recognition mode settings", Category = "Config" },
        new() { Id = "ConfigWifi",      Method = "GET",  Path = "api/v1/config/wifi",     From = V06, Description = "WiFi SSID",              Category = "Config" },
        new() { Id = "ConfigReset",     Method = "GET",  Path = "api/v1/config/reset",    From = V06, Description = "Advertised by OPTIONS but 404", Category = "Config" },
        new() { Id = "WifiNetworks",    Method = "GET",  Path = "api/v1/config/wifi/networks",      From = V06, Description = "WiFi scan (only in ad-hoc mode)", Category = "Config" },
        new() { Id = "TimeZonesPost",   Method = "POST", Path = "api/v1/config/time/zones",         From = V06, Description = "Time zones of one country, body { \"code\": \"SK\" } (measured: POST works on 06.0601 and 10.1308; GET answers 405 on both)", Category = "Config" },
        new() { Id = "Countries",       Method = "GET",  Path = "api/v1/config/time/countries",     From = V06, Description = "Every country with its time zones - the whole table in one call", Category = "Config" },

        new() { Id = "Languages",       Method = "GET",  Path = "api/v1/config/language/languages", From = V06, Description = "Available languages", Category = "Config" },

        // ── Users ────────────────────────────────────────────────────
        new() { Id = "UsersList",       Method = "GET",    Path = "api/v1/users",       From = V06, Description = "List all user accounts",  Category = "Users" },
        new() { Id = "UserCreate",      Method = "POST",   Path = "api/v1/users",       From = V06, Description = "Create a user account",   Category = "Users" },
        new() { Id = "UserGet",         Method = "GET",    Path = "api/v1/users/{id}",  From = V06, Description = "Get one user",            Category = "Users" },
        new() { Id = "UserUpdate",      Method = "PUT",    Path = "api/v1/users/{id}",  From = V06, Description = "Update a user",           Category = "Users" },
        new() { Id = "UserDelete",      Method = "DELETE", Path = "api/v1/users/{id}",  From = V06, Description = "Delete a user",           Category = "Users" },
        new() { Id = "UserNotifications", Method = "PUT",  Path = "api/v1/users/{id}/notifications", From = V06, Description = "Notification matrix", Category = "Users" },
        new() { Id = "UserDevices",     Method = "GET",    Path = "api/v1/users/{id}/devices",       From = V06, Description = "Paired phones", Category = "Users" },

        // ── Admin (unauthenticated!) ─────────────────────────────────
        new() { Id = "AdminCreate",     Method = "POST",   Path = "api/v1/admin",       From = V06, Unauthenticated = true, Description = "Create admin account (returns 422 if exists)", Category = "Admin" },

        // ── Files / media ────────────────────────────────────────────
        new() { Id = "MediaList",       Method = "GET",    Path = "api/v1/files/media",              From = V06, Description = "All recordings",   Category = "Media" },
        new() { Id = "MediaByDay",      Method = "GET",    Path = "api/v1/files/media/{day}",        From = V06, Description = "Recordings for one day", Category = "Media" },
        new() { Id = "MediaCount",      Method = "GET",    Path = "api/v1/files/media/{day}/count",  From = V06, Description = "File count for a day", Category = "Media" },
        new() { Id = "MediaSnapshotCount", Method = "GET", Path = "api/v1/files/media/{day}/snapshotCount", From = V10, AbsentUpTo = V06, Description = "Snapshot count for a day (measured: 200 on 10.1308, 403 on 06.0601 - use …/count there)", Category = "Media", AlternativeId = "MediaCount" },
        new() { Id = "MediaDelete",     Method = "DELETE", Path = "api/v1/files/delete",             From = V06, Description = "Purge recordings by period", Category = "Media" },
        new() { Id = "DirCount",        Method = "GET",    Path = "api/v1/files/directoryCount",     From = V06, Description = "Number of recording folders", Category = "Media" },
        new() { Id = "Stats",           Method = "GET",    Path = "api/v1/files/stats",              From = V06, Description = "Storage usage",    Category = "Media" },
        new() { Id = "BackupToCard",    Method = "PUT",    Path = "api/v1/files/backup",             From = V06, Description = "Move recordings to SD card", Category = "Media" },
        new() { Id = "MediaDir",        Method = "GET",    Path = "media/",                          From = V06, Unauthenticated = true, Description = "Directory listing of all recordings (no auth!)", Category = "Media" },

        // ── System ───────────────────────────────────────────────────
        new() { Id = "SystemLogGet",    Method = "GET",  Path = "api/v1/system/log",     From = V06, Description = "System log (plain text on 06, may differ on newer)", Category = "System" },
        new() { Id = "SystemLogPost",   Method = "POST", Path = "api/v1/system/log",     From = V08, AbsentUpTo = V06, Description = "Trigger log export → ZIP file name",   Category = "System" },
        new() { Id = "SystemBackups",   Method = "GET",  Path = "api/v1/system/backups", From = V06, Description = "List settings backups on camera",      Category = "System" },
        new() { Id = "SystemBackup",    Method = "POST", Path = "api/v1/system/backup",  From = V06, Description = "Create a settings backup",             Category = "System" },
        new() { Id = "SystemRestore",   Method = "POST", Path = "api/v1/system/restore", From = V06, Description = "Restore a settings backup",            Category = "System" },
        new() { Id = "SystemReset",     Method = "POST", Path = "api/v1/system/reset",   From = V06, Description = "Factory reset",                        Category = "System" },

        // ── Firmware / FOTA ──────────────────────────────────────────
        new() { Id = "FotaStatus",      Method = "GET",  Path = "api/v1/interface/fotaStatus",            From = V06, Description = "Download status (-1 = idle)", Category = "Firmware" },
        new() { Id = "FotaUpdates",     Method = "GET",  Path = "api/v1/interface/fotaNumberOfUpdates",   From = V06, Description = "Pending update count",       Category = "Firmware" },
        new() { Id = "FotaDownload",    Method = "GET",  Path = "api/v1/interface/fotaStartDownload",     From = V06, Description = "Start OTA download",         Category = "Firmware" },
        new() { Id = "FotaInstall",     Method = "POST", Path = "api/v1/interface/fotaInstallFirmware",   From = V06, Description = "Install downloaded firmware", Category = "Firmware" },
        new() { Id = "FotaVersion",     Method = "GET",  Path = "api/v1/interface/fotaVersion",           From = V10, NotRouted = true, Description = "Listed by the web app but measured 404 on both 06.0601 and 10.1308", Category = "Firmware" },
        new() { Id = "CheckVersionOnCloud", Method = "POST", Path = "api/v1/interface/CheckVersionOnCloud", From = V10, AbsentUpTo = V06, Description = "Cloud update check - answers {\"status\":\"success\"} on 10.1308, 404 on 06.0601 (the cloud host itself is long gone)", Category = "Firmware" },
        new() { Id = "FirmwareUpdate",  Method = "PUT",  Path = "api/v1/config/firmware",                 From = V06, Description = "Trigger firmware update from config", Category = "Firmware" },

        // ── Access Everywhere ────────────────────────────────────────
        new() { Id = "AccessEverywhere", Method = "GET", Path = "api/v1/accessEverywhere", From = V08, AbsentUpTo = V06, Description = "Device ID (trimmedMac), remote access info", Category = "Device" },

        // ── Undocumented / unknown payload ────────────────────────────
        new() { Id = "Behaviors",      Method = "GET",    Path = "api/v1/behaviors",    From = V06, Description = "List face-recognition behavior rules (expression per user)", Category = "Config" },
        new() { Id = "BehaviorsPost",  Method = "POST",   Path = "api/v1/behaviors",    From = V06, Description = "Create a behavior rule {expression, user}", Category = "Config" },
        new() { Id = "BehaviorsPatch", Method = "PATCH",  Path = "api/v1/behaviors/:id", From = V06, Description = "Update a behavior rule", Category = "Config" },
        new() { Id = "BehaviorsDelete",Method = "DELETE", Path = "api/v1/behaviors/:id", From = V06, Description = "Delete a behavior rule", Category = "Config" },
        new() { Id = "Neighbors",  Method = "GET",  Path = "api/v1/neighbors", From = V06, Description = "Unknown — responds to OPTIONS", Category = "Undocumented" },
        new() { Id = "Eyes",       Method = "GET",  Path = "api/v1/eyes",      From = V10, NotRouted = true, Description = "Listed by the web app but measured 404 on both 06.0601 and 10.1308", Category = "Undocumented" },
        new() { Id = "Faces",      Method = "GET",  Path = "api/v1/faces",     From = V10, NotRouted = true, Description = "Listed by the web app but measured 404 on both 06.0601 and 10.1308", Category = "Undocumented" },

        // ── Static files (unauthenticated) ───────────────────────────
        new() { Id = "LogsDir",       Method = "GET", Path = "logs/",          From = V08, AbsentUpTo = V06, Unauthenticated = true, Description = "/logs/ directory listing", Category = "Static" },
        new() { Id = "LogsSystemTxt", Method = "GET", Path = "logs/system.txt",From = V08, AbsentUpTo = V06, Unauthenticated = true, Description = "Full Android logcat (22+ MB, contains WiFi passwords!)", Category = "Static" },
        new() { Id = "LogsDebugTxt",  Method = "GET", Path = "logs/debug.txt", From = V08, AbsentUpTo = V06, Unauthenticated = true, Description = "Application debug log", Category = "Static" },
        new() { Id = "LogsLogTxt",    Method = "GET", Path = "logs/log.txt",   From = V08, AbsentUpTo = V06, Unauthenticated = true, Description = "Application log",       Category = "Static" },
        new() { Id = "WebApp",        Method = "GET", Path = "build/main.js",  From = V06, Unauthenticated = true, Description = "Compiled Ionic/Angular web app", Category = "Static" },

        // ── Time ─────────────────────────────────────────────────────
        new() { Id = "TimeGet", Method = "GET", Path = "api/v1/time", From = V06, Description = "Camera clock",     Category = "Time" },
        new() { Id = "TimeSet", Method = "PUT", Path = "api/v1/time", From = V06, Description = "Set camera clock", Category = "Time" },
    };

    // ── Lookup helpers ────────────────────────────────────────────────────

    /// <summary>Find an endpoint by its code ID.</summary>
    public static Endpoint? Get(string id)
        => Array.Find(All, e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Find the best endpoint for a function on a given firmware, trying alternatives.</summary>
    public static Endpoint? GetBest(string id, UloFirmwareVersion firmware)
    {
        var primary = Get(id);
        if (primary is null)
            return null;

        if (primary.IsAvailableOn(firmware))
            return primary;

        // Try the alternative.
        if (primary.AlternativeId is not null)
        {
            var alt = Get(primary.AlternativeId);
            if (alt is not null && alt.IsAvailableOn(firmware))
                return alt;
        }

        return primary; // Return primary anyway — caller can try and handle the error.
    }

    /// <summary>All endpoints expected to work on a given firmware version.</summary>
    public static IEnumerable<Endpoint> ForFirmware(UloFirmwareVersion firmware)
        => All.Where(e => e.IsAvailableOn(firmware));

    /// <summary>All endpoints in a category.</summary>
    public static IEnumerable<Endpoint> InCategory(string category)
        => All.Where(e => string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase));

    /// <summary>All unique categories.</summary>
    public static IEnumerable<string> Categories
        => All.Select(e => e.Category).Distinct();
}
