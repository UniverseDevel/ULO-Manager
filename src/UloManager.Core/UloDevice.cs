using System.Text.Json;
using System.Text.Json.Nodes;

namespace UloManager.Core;

/// <summary>Thrown when an operation requires the admin/setup mode but the session is a normal user.</summary>
public sealed class UloPermissionException : UloApiException
{
    public UloPermissionException(string operation)
        : base($"Operation '{operation}' requires an administrator account (admin/setup mode).")
    {
    }
}

public sealed record UloConnectionInfo(
    UloOperatingMode OperatingMode,
    UloUser CurrentUser,
    UloState State,
    UloConfiguration Configuration,
    DateTime DeviceTime,
    UloAccessEverywhereInfo? AccessEverywhere = null,
    string? MacDeviceId = null)
{
    public string DeviceName => Configuration.Device.Name;

    public string FirmwareVersion => Configuration.Firmware.CurrentVersion;

    /// <summary>Parsed firmware version for capability checks.</summary>
    public UloFirmwareVersion Firmware => new(Configuration.Firmware.CurrentVersion);

    /// <summary>
    /// Device identifier like "ulo_ab12". The camera reports it as <c>trimmedMac</c> from firmware
    /// 08.0904 onwards; on older firmware it is derived from the MAC address on the local network,
    /// which produces the same value.
    /// </summary>
    public string DeviceId => string.IsNullOrEmpty(AccessEverywhere?.DeviceId)
        ? MacDeviceId ?? ""
        : AccessEverywhere!.DeviceId;

    /// <summary>What the camera itself reports: setup mode or normal usage mode.</summary>
    public UloDeviceMode DeviceMode => State.DeviceMode;

    /// <summary>Short description of both the camera mode and the rights of this session.</summary>
    public string ModeSummary =>
        $"camera in {(DeviceMode == UloDeviceMode.Setup ? "SETUP" : "USAGE")} mode, " +
        $"signed in as {(OperatingMode == UloOperatingMode.AdminSetup ? "administrator" : "standard user")}, " +
        $"firmware {FirmwareVersion}" +
        (string.IsNullOrEmpty(DeviceId) ? "" : $", {DeviceId}");
}

/// <summary>Thrown when the camera dropped our session because the account logged in elsewhere.</summary>
public sealed class UloSessionEvictedException : UloApiException
{
    public UloSessionEvictedException(string account)
        : base($"The camera closed the session of '{account}'. ULO keeps only one session per account, " +
               "so a phone app, the web UI or another copy of this tool signing in with the same account " +
               "logs this one out. Use a dedicated account for this application.")
    {
    }
}

/// <summary>
/// High level API over a ULO camera. Groups every known operation into the
/// admin/setup surface and the usage surface.
/// </summary>
public sealed class UloDevice : IDisposable
{
    private readonly bool _ownsClient;

    public UloDevice(UloConnectionOptions options)
        : this(new UloClient(options), ownsClient: true)
    {
    }

    public UloDevice(UloClient client, bool ownsClient = false)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
        Log = new UloLogService(this);
        Media = new UloMediaService(this);
        LiveVideo = new UloLiveVideoStream(client);
        Availability = new UloAvailabilityService(this);
    }

    public UloClient Client { get; }

    public UloLogService Log { get; }

    public UloMediaService Media { get; }

    /// <summary>Live fragmented-MP4 video straight from the camera.</summary>
    public UloLiveVideoStream LiveVideo { get; }

    /// <summary>Presence checking and availability driven mode switching.</summary>
    public UloAvailabilityService Availability { get; }

    /// <summary>
    /// Parsed firmware version, set after <see cref="ConnectAsync"/>. Use for capability checks
    /// (e.g. <c>FirmwareVersion.UsesLogPost</c>). Default (unknown) before connect.
    /// </summary>
    public UloFirmwareVersion FirmwareVersion { get; private set; }

    public UloUser? CurrentUser { get; private set; }

    public UloOperatingMode OperatingMode =>
        CurrentUser?.IsAdmin == true ? UloOperatingMode.AdminSetup : UloOperatingMode.Usage;

    public bool IsAdminSession => OperatingMode == UloOperatingMode.AdminSetup;

    // ---------------------------------------------------------------- session

    /// <summary>Logs in and collects everything needed to drive the UI.</summary>
    public async Task<UloConnectionInfo> ConnectAsync(CancellationToken ct = default)
    {
        var login = await Client.LoginAsync(ct).ConfigureAwait(false);

        CurrentUser = await Client.GetAsync<UloUser>($"api/v1/users/{login.UserId}", ct).ConfigureAwait(false)
                      ?? new UloUser { Id = login.UserId, Account = "user" };

        var state = await GetStateAsync(ct).ConfigureAwait(false);
        var config = await GetConfigurationAsync(ct).ConfigureAwait(false);
        var time = await GetDeviceTimeAsync(ct).ConfigureAwait(false);

        FirmwareVersion = new UloFirmwareVersion(config.Firmware.CurrentVersion);

        // accessEverywhere provides the device identifier (trimmed MAC) — available on 08.0904+.
        UloAccessEverywhereInfo? accessEverywhere = null;
        if (FirmwareVersion.HasAccessEverywhere)
        {
            try
            {
                accessEverywhere = await Client.GetAsync<UloAccessEverywhereInfo>("api/v1/accessEverywhere", ct)
                    .ConfigureAwait(false);
            }
            catch (UloApiException)
            {
                // Endpoint absent or requires different permissions — not fatal.
            }
        }

        // Firmware 06.0601 has no such endpoint, but the identifier is just the last four hex digits
        // of the MAC address, which the local network knows anyway.
        string? macDeviceId = null;
        if (string.IsNullOrEmpty(accessEverywhere?.DeviceId))
        {
            macDeviceId = UloNetwork.TryGetDeviceIdFromMac(Client.Options.Host);
        }

        return new UloConnectionInfo(OperatingMode, CurrentUser, state, config, time, accessEverywhere, macDeviceId);
    }

    public Task DisconnectAsync(CancellationToken ct = default) => Client.LogoutAsync(ct);

    private void RequireAdmin(string operation)
    {
        if (!IsAdminSession)
        {
            throw new UloPermissionException(operation);
        }
    }

    // ------------------------------------------------------------ usage mode

    public async Task<UloMode> GetModeAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/mode", ct).ConfigureAwait(false);
        var value = json?["mode"]?.GetValue<string>() ?? throw new UloApiException("Camera did not report a mode.");
        return UloModeExtensions.ParseMode(value);
    }

    public async Task<UloMode> SetModeAsync(UloMode mode, CancellationToken ct = default)
    {
        var payload = $"{{ \"mode\": \"{mode.ToApiValue()}\" }}";
        var json = await Client.SendJsonAsync(HttpMethod.Put, "api/v1/mode", payload, ct).ConfigureAwait(false);
        var value = json?["mode"]?.GetValue<string>();

        // Some firmware revisions answer with an empty body, so verify by reading back.
        var applied = value is null ? await GetModeAsync(ct).ConfigureAwait(false) : UloModeExtensions.ParseMode(value);
        if (applied != mode)
        {
            throw new UloApiException($"Camera refused to switch to '{mode.ToApiValue()}' and stayed in '{applied.ToApiValue()}'.");
        }

        return applied;
    }

    public async Task<UloState> GetStateAsync(CancellationToken ct = default)
        => await Client.GetAsync<UloState>("api/v1/state", ct).ConfigureAwait(false)
           ?? throw new UloApiException("Camera did not report its state.");

    public async Task<UloStorageStats> GetStorageAsync(CancellationToken ct = default)
        => await Client.GetAsync<UloStorageStats>("api/v1/files/stats", ct).ConfigureAwait(false)
           ?? throw new UloApiException("Camera did not report storage statistics.");

    public async Task<DateTime> GetDeviceTimeAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/time", ct).ConfigureAwait(false);
        var value = json?["time"]?.GetValue<string>();
        return value is not null && DateTime.TryParse(value, out var parsed) ? parsed : DateTime.MinValue;
    }

    public Task SetDeviceTimeAsync(DateTime time, CancellationToken ct = default)
        => Client.SendAsync(HttpMethod.Put, "api/v1/time", $"{{ \"time\": \"{time:yyyy-MM-ddTHH:mm:ss}\" }}", ct);

    /// <summary>
    /// Asks the camera to grab a picture of what it sees right now and returns the camera side path.
    /// </summary>
    /// <param name="storeOnCamera">
    /// When false the picture is not added to the camera's own recordings, which keeps its
    /// internal memory free when the picture is only used for a live preview.
    /// </param>
    public async Task<string> TakeSnapshotAsync(bool storeOnCamera = false, CancellationToken ct = default)
    {
        var ep = UloEndpointRegistry.GetBest("Snapshot", FirmwareVersion)
                 ?? throw new UloApiException("No snapshot endpoint is known for this firmware.");

        var (path, response) = await RequestSnapshotAsync(ep, storeOnCamera, ct).ConfigureAwait(false);
        if (path is not null)
        {
            return path;
        }

        // The camera reported no usable file name (firmware 10.1308 answers {"filename": "media/"}).
        // Some units only keep the picture when it is stored as a recording, so ask again with
        // 'savePicture' enabled before giving up.
        if (!storeOnCamera)
        {
            (path, response) = await RequestSnapshotAsync(ep, storeOnCamera: true, ct).ConfigureAwait(false);
            if (path is not null)
            {
                return path;
            }
        }

        var trimmed = response.Body.Trim();
        if (trimmed.StartsWith('{') || trimmed.Equals("success", StringComparison.OrdinalIgnoreCase) || response.MalformedHeaders)
        {
            throw new UloApiException(
                "The camera confirmed the picture but did not say where it stored it, and no matching " +
                "file appeared in its media folder. Its internal memory may be full - free space with " +
                "'clean' or move the recordings to the SD card.",
                null,
                ep.Path,
                trimmed);
        }

        throw new UloApiException($"Unexpected snapshot response from {ep.Path}: {trimmed[..Math.Min(100, trimmed.Length)]}");
    }

    /// <summary>
    /// Takes one picture and works out where it landed. Returns a null path when the camera neither
    /// reported a file name nor produced a fresh file.
    /// </summary>
    private async Task<(string? Path, UloResponse Response)> RequestSnapshotAsync(
        UloEndpointRegistry.Endpoint ep, bool storeOnCamera, CancellationToken ct)
    {
        var body = storeOnCamera ? "{}" : "{ \"savePicture\": 0 }";

        // The camera's clock is read before the picture so a file created afterwards can be told
        // apart from an older one, whatever the offset between the camera and this machine.
        var takenAt = await TryGetDeviceTimeAsync(ct).ConfigureAwait(false);

        var response = await Client.SendDetailedAsync(HttpMethod.Post, ep.Path, body, ct).ConfigureAwait(false);
        var raw = response.Body.Trim();

        // Firmware 06.0601 returns JSON: {"filename": "media/20260814/snapshot_….jpg"}
        // Firmware 10.1308 returns {"filename": "media/"} - the name is simply missing - and puts a
        // bare "success" line in the header block.
        if (raw.StartsWith('{'))
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(raw) as System.Text.Json.Nodes.JsonObject;
            var filename = json?["filename"]?.GetValue<string>();
            if (IsUsableMediaPath(filename))
            {
                return (filename!.TrimStart('/'), response);
            }
        }
        else if (!raw.Equals("success", StringComparison.OrdinalIgnoreCase) && !response.MalformedHeaders)
        {
            return (null, response);
        }

        // No usable name: look for the picture the camera has just written. Writing it takes a moment,
        // so give it a few tries before concluding that this firmware discarded it.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            }

            var latest = await FindLatestSnapshotAsync(takenAt, ct).ConfigureAwait(false);
            if (latest is not null)
            {
                return (latest, response);
            }
        }

        return (null, response);
    }

    private async Task<DateTime> TryGetDeviceTimeAsync(CancellationToken ct)
    {
        try
        {
            var time = await GetDeviceTimeAsync(ct).ConfigureAwait(false);
            // The clock reads 01/01/70 until NTP succeeds after a reboot; the file names follow it.
            return time == DateTime.MinValue ? DateTime.Now : time;
        }
        catch (UloApiException)
        {
            return DateTime.Now;
        }
    }


    /// <summary>True for a media path that actually points at a file rather than at the folder.</summary>
    private static bool IsUsableMediaPath(string? path)
        => !string.IsNullOrWhiteSpace(path) &&
           !path.EndsWith('/') &&
           Path.HasExtension(path);

    /// <summary>
    /// Locates the picture the camera has just written. Used when the firmware takes the snapshot but
    /// does not report its file name. Only files stamped at or after <paramref name="notBefore"/>
    /// count, so a previous picture is never mistaken for the current one.
    /// </summary>
    private async Task<string?> FindLatestSnapshotAsync(DateTime notBefore, CancellationToken ct)
    {
        // File names are stamped with the camera's own clock, which is not necessarily this machine's
        // date (it resets to 01/01/70 after a reboot until NTP succeeds), so search the day the camera
        // believes it is, plus the day before in case the search straddles midnight.
        var days = new List<string> { notBefore.ToString("yyyyMMdd") };
        var nextDay = notBefore.AddDays(1).ToString("yyyyMMdd");
        if (nextDay != days[0])
        {
            days.Add(nextDay);
        }

        // A second of slack: the camera stamps the name when it starts writing the file.
        var threshold = notBefore.AddSeconds(-1);
        string? newest = null;

        foreach (var day in days)
        {
            System.Text.Json.Nodes.JsonNode? mediaJson;
            try
            {
                mediaJson = await Client.GetJsonAsync($"api/v1/files/media/{day}", ct).ConfigureAwait(false);
            }
            catch (UloApiException)
            {
                // Day folder missing (404) - try the next one.
                continue;
            }

            if (mediaJson?["files"] is not System.Text.Json.Nodes.JsonArray files)
            {
                continue;
            }

            foreach (var entry in files)
            {
                // The camera lists plain strings; older firmware wrapped them in an object.
                var file = entry is System.Text.Json.Nodes.JsonObject obj
                    ? obj["file"]?.GetValue<string>()
                    : entry?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(file) ||
                    !file.Contains("snapshot", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var stamp = ParseMediaTimestamp(file);
                if (stamp is null || stamp < threshold)
                {
                    continue;
                }

                // The listing order is not guaranteed, so compare instead of taking the first hit.
                if (newest is null || string.CompareOrdinal(file, newest) > 0)
                {
                    newest = file;
                }
            }
        }

        return newest?.TrimStart('/');
    }

    /// <summary>Reads the <c>yyyyMMdd_HHmmss</c> stamp out of a media file name such as
    /// <c>media/20260814/snapshot_20260814_181315.jpg</c>.</summary>
    private static DateTime? ParseMediaTimestamp(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var parts = name.Split('_');
        for (var i = 0; i + 1 < parts.Length; i++)
        {
            if (DateTime.TryParseExact(
                    parts[i] + parts[i + 1],
                    "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }


    /// <summary>Takes a snapshot and stores it locally.</summary>
    public async Task<string> DownloadCurrentSnapshotAsync(
        string destinationFolder,
        bool storeOnCamera = false,
        CancellationToken ct = default)
    {
        var remote = await TakeSnapshotAsync(storeOnCamera, ct).ConfigureAwait(false);
        var local = Path.Combine(destinationFolder, Path.GetFileName(remote));
        await Client.DownloadFileAsync(remote, local, ct).ConfigureAwait(false);
        return local;
    }

    /// <summary>True while the camera is recording a video on request (not the automatic alert recording).</summary>
    public async Task<bool> IsRecordingAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/record", ct).ConfigureAwait(false);
        return json?["running"]?.GetValue<bool>() ?? false;
    }

    /// <summary>Starts or stops an on-demand recording on the camera.</summary>
    public Task SetRecordingAsync(bool running, CancellationToken ct = default)
        => Client.SendAsync(HttpMethod.Put, "api/v1/record", $"{{ \"running\": {(running ? "true" : "false")} }}", ct);

    // ----------------------------------------------------------- storage jobs

    /// <summary>Moves the recordings from internal memory onto the SD card. The camera cannot record meanwhile.</summary>
    public Task StartMoveToCardAsync(CancellationToken ct = default)
        => Client.SendAsync(HttpMethod.Put, "api/v1/files/backup?filename=all", "{ \"running\": true }", ct);

    public async Task<bool> IsMoveToCardRunningAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/files/backup", ct).ConfigureAwait(false);
        return json?["running"]?.GetValue<bool>() ?? false;
    }

    /// <summary>Waits until a running move-to-card job finishes.</summary>
    public async Task WaitForMoveToCardAsync(TimeSpan pollInterval, CancellationToken ct = default)
    {
        while (await IsMoveToCardRunningAsync(ct).ConfigureAwait(false))
        {
            await Task.Delay(pollInterval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Deletes recordings from the internal memory. Requires an admin session.</summary>
    public Task CleanStorageAsync(UloCleanPeriod period, CancellationToken ct = default)
    {
        RequireAdmin("clean storage");
        return Client.SendAsync(HttpMethod.Delete, $"api/v1/files/delete?removeType={(int)period}", null, ct);
    }

    /// <summary>Deletes every recording of a single day (folder name in yyyyMMdd form).</summary>
    public Task DeleteMediaDayAsync(string day, CancellationToken ct = default)
    {
        RequireAdmin("delete media day");
        return Client.SendAsync(HttpMethod.Delete, $"api/v1/files/media/{day}", null, ct);
    }

    // ------------------------------------------------------ admin/setup mode

    public async Task<UloConfiguration> GetConfigurationAsync(CancellationToken ct = default)
        => await Client.GetAsync<UloConfiguration>("api/v1/config", ct).ConfigureAwait(false)
           ?? throw new UloApiException("Camera did not return its configuration.");

    public Task<T?> GetConfigSectionAsync<T>(string section, CancellationToken ct = default)
        => Client.GetAsync<T>($"api/v1/config/{section}", ct);

    /// <summary>Writes one configuration section (device, wifi, time, video, eyes, exclusion, alert, face, voice, email, access, language).</summary>
    public async Task UpdateConfigSectionAsync(string section, object payload, CancellationToken ct = default)
    {
        RequireAdmin($"update configuration '{section}'");
        var body = payload as string ?? JsonSerializer.Serialize(payload, UloJson.Options);
        await Client.SendAsync(HttpMethod.Put, $"api/v1/config/{section}", body, ct).ConfigureAwait(false);
    }

    public Task SetDeviceNameAsync(string name, CancellationToken ct = default)
        => UpdateConfigSectionAsync("device", new UloDeviceConfig { Name = name }, ct);

    public Task SetVideoQualityAsync(string quality, CancellationToken ct = default)
        => UpdateConfigSectionAsync("video", new UloVideoConfig { Quality = quality }, ct);

    public Task SetLanguageAsync(string code, CancellationToken ct = default)
        => UpdateConfigSectionAsync("language", new UloLanguageConfig { Language = code }, ct);

    public Task SetTimeSettingsAsync(bool auto, string timeZone, CancellationToken ct = default)
        => UpdateConfigSectionAsync("time", new UloTimeConfig { Auto = auto, TimeZone = timeZone }, ct);

    public Task SetEyesAsync(UloEyesConfig eyes, CancellationToken ct = default)
        => UpdateConfigSectionAsync("eyes", eyes, ct);

    public Task SetExclusionZoneAsync(UloExclusionConfig exclusion, CancellationToken ct = default)
        => UpdateConfigSectionAsync("exclusion", exclusion, ct);

    public Task SetAlertBehaviourAsync(UloAlertConfig alert, CancellationToken ct = default)
        => UpdateConfigSectionAsync("alert", alert, ct);

    public Task SetFaceNotificationsAsync(UloNotificationConfig face, CancellationToken ct = default)
        => UpdateConfigSectionAsync("face", face, ct);

    public Task SetVoiceSettingsAsync(UloVoiceConfig voice, CancellationToken ct = default)
        => UpdateConfigSectionAsync("voice", voice, ct);

    public Task SetEmailAsync(UloEmailConfig email, CancellationToken ct = default)
        => UpdateConfigSectionAsync("email", email, ct);

    public Task SetAccessAsync(UloAccessConfig access, CancellationToken ct = default)
        => UpdateConfigSectionAsync("access", access, ct);

    /// <summary>Joins the camera to a Wi-Fi network. Beware: a wrong password makes the camera fall back to its ad-hoc setup network.</summary>
    public Task ConnectWifiAsync(string ssid, string? password, CancellationToken ct = default)
        => UpdateConfigSectionAsync("wifi", new UloWifiConfig { Ssid = ssid, Password = password }, ct);

    public async Task<IReadOnlyList<UloWifiNetwork>> ScanWifiAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/config/wifi/networks", ct).ConfigureAwait(false);
        var array = json?["networks"] as JsonArray;
        return array is null
            ? Array.Empty<UloWifiNetwork>()
            : array.Deserialize<List<UloWifiNetwork>>(UloJson.Options) ?? new List<UloWifiNetwork>();
    }

    public async Task<IReadOnlyList<UloLanguageInfo>> GetLanguagesAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/config/language/languages", ct).ConfigureAwait(false);
        var array = json?["languages"] as JsonArray;
        return array?.Deserialize<List<UloLanguageInfo>>(UloJson.Options) ?? new List<UloLanguageInfo>();
    }

    /// <summary>
    /// Countries with their time zones. One call returns the whole table - each entry carries its
    /// own <c>timeZones</c> list - so the separate <c>config/time/zones</c> round trip is only
    /// needed when a single country is wanted.
    /// </summary>
    public async Task<IReadOnlyList<UloCountryInfo>> GetCountriesWithZonesAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/config/time/countries", ct).ConfigureAwait(false);
        var array = json?["countries"] as JsonArray;
        return array?.Deserialize<List<UloCountryInfo>>(UloJson.Options) ?? new List<UloCountryInfo>();
    }

    /// <summary>Every time zone the camera accepts, sorted and without duplicates.</summary>
    public async Task<IReadOnlyList<string>> GetAllTimeZonesAsync(CancellationToken ct = default)
    {
        var countries = await GetCountriesWithZonesAsync(ct).ConfigureAwait(false);
        return countries
            .SelectMany(c => c.TimeZones)
            .Where(zone => !string.IsNullOrWhiteSpace(zone))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(zone => zone, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetCountriesAsync(CancellationToken ct = default)
    {
        var countries = await GetCountriesWithZonesAsync(ct).ConfigureAwait(false);
        return countries.Select(c => c.Code).Where(code => !string.IsNullOrEmpty(code)).ToList();
    }

    /// <summary>
    /// Time zones of one country. Confirmed <c>POST</c> only on both 06.0601 and 10.1308 - a
    /// <c>GET</c> answers <c>405</c> on each - so the whole table from
    /// <see cref="GetCountriesWithZonesAsync"/> is usually the cheaper way.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetTimeZonesAsync(string countryCode, CancellationToken ct = default)
    {
        var json = await Client.SendJsonAsync(HttpMethod.Post, "api/v1/config/time/zones", $"{{ \"code\": \"{countryCode}\" }}", ct)
            .ConfigureAwait(false);
        var array = json?["zones"] as JsonArray;
        return array?.Select(node => node!.GetValue<string>()).ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    public async Task<UloFirmwareConfig> GetFirmwareAsync(CancellationToken ct = default)
        => await Client.GetAsync<UloFirmwareConfig>("api/v1/config/firmware", ct).ConfigureAwait(false)
           ?? throw new UloApiException("Camera did not return firmware information.");

    /// <summary>
    /// Over-the-air update status, as used by the official app
    /// (<c>isDownload</c> is -1 when idle, <c>percentageDownload</c> is the progress).
    /// </summary>
    public async Task<UloFotaStatus> GetFotaStatusAsync(CancellationToken ct = default)
        => await Client.GetAsync<UloFotaStatus>("api/v1/interface/fotaStatus", ct).ConfigureAwait(false)
           ?? new UloFotaStatus();

    /// <summary>Number of firmware updates the camera has waiting for download.</summary>
    public async Task<int> GetPendingUpdateCountAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/interface/fotaNumberOfUpdates", ct).ConfigureAwait(false);
        return json?["downloadCount"]?.GetValue<int>() ?? 0;
    }

    /// <summary>True when a downloaded firmware can be installed.</summary>
    public async Task<bool> IsFirmwareInstallAvailableAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/interface/fotaIsInstallAvailable", ct).ConfigureAwait(false);
        return (json?["isInstall"]?.GetValue<int>() ?? 0) == 1;
    }

    /// <summary>Installs a firmware that has already been downloaded.</summary>
    public Task InstallFirmwareAsync(CancellationToken ct = default)
    {
        RequireAdmin("firmware install");
        return Client.SendAsync(HttpMethod.Post, "api/v1/interface/fotaInstallFirmware", "{}", ct);
    }

    /// <summary>Everything the camera knows about updates, collected in one call.</summary>
    public async Task<UloUpdateReport> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var firmware = await GetFirmwareAsync(ct).ConfigureAwait(false);
        var status = await GetFotaStatusAsync(ct).ConfigureAwait(false);
        var pending = await GetPendingUpdateCountAsync(ct).ConfigureAwait(false);
        var installable = await IsFirmwareInstallAvailableAsync(ct).ConfigureAwait(false);

        return new UloUpdateReport(firmware, status, pending, installable);
    }

    /// <summary>Starts the over-the-air firmware download.</summary>
    public Task StartFotaDownloadAsync(CancellationToken ct = default)
    {
        RequireAdmin("firmware download");
        return Client.SendAsync(HttpMethod.Get, "api/v1/interface/fotaStartDownload", null, ct);
    }

    // -------------------------------------------------- configuration backups

    /// <summary>Configuration backups stored on the camera.</summary>
    public async Task<IReadOnlyList<string>> GetBackupsAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/system/backups", ct).ConfigureAwait(false);
        var array = json?["backups"] as JsonArray;
        return array?
            .Select(node => node is JsonValue value ? value.ToString() : node?["name"]?.GetValue<string>() ?? node?.ToJsonString() ?? "")
            .Where(name => name.Length > 0)
            .ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    /// <summary>Creates a configuration backup on the camera.</summary>
    public Task CreateBackupAsync(string? name = null, CancellationToken ct = default)
    {
        RequireAdmin("create settings backup");
        var body = name is null ? "{}" : JsonSerializer.Serialize(new { name }, UloJson.Options);
        return Client.SendAsync(HttpMethod.Post, "api/v1/system/backup", body, ct);
    }

    /// <summary>Restores a configuration backup previously stored on the camera.</summary>
    public Task RestoreBackupAsync(string name, CancellationToken ct = default)
    {
        RequireAdmin("restore settings backup");
        return Client.SendAsync(HttpMethod.Post, "api/v1/system/restore", JsonSerializer.Serialize(new { name }, UloJson.Options), ct);
    }

    /// <summary>
    /// Resets the camera to factory settings. Every user, the Wi-Fi configuration and the
    /// recordings are lost and the camera comes back up in its ad-hoc setup mode.
    /// </summary>
    public Task FactoryResetAsync(CancellationToken ct = default)
    {
        RequireAdmin("factory reset");
        return Client.SendAsync(HttpMethod.Post, "api/v1/system/reset", "{}", ct);
    }

    /// <summary>Number of files the camera stores for one day (yyyyMMdd).</summary>
    public async Task<int> GetMediaCountAsync(string day, CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync($"api/v1/files/media/{day}/count", ct).ConfigureAwait(false);
        return json?["count"]?.GetValue<int>() ?? 0;
    }

    /// <summary>Asks the camera to start the firmware update it already advertises.</summary>
    public Task StartFirmwareUpdateAsync(CancellationToken ct = default)
    {
        RequireAdmin("firmware update");
        return Client.SendAsync(HttpMethod.Put, "api/v1/config/firmware", "{ \"firmwareStatus\": \"update\" }", ct);
    }

    // ------------------------------------------------------------ user admin

    public async Task<IReadOnlyList<UloUser>> GetUsersAsync(CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync("api/v1/users", ct).ConfigureAwait(false);
        var array = json?["users"] as JsonArray;
        return array?.Deserialize<List<UloUser>>(UloJson.Options) ?? new List<UloUser>();
    }

    public Task<UloUser?> GetUserAsync(int id, CancellationToken ct = default)
        => Client.GetAsync<UloUser>($"api/v1/users/{id}", ct);

    public async Task<UloUser?> CreateUserAsync(UloUser user, CancellationToken ct = default)
    {
        RequireAdmin("create user");
        return await Client.SendAsync<UloUser>(HttpMethod.Post, "api/v1/users", user, ct).ConfigureAwait(false);
    }

    public async Task UpdateUserAsync(UloUser user, CancellationToken ct = default)
    {
        if (!IsAdminSession && user.Id != CurrentUser?.Id)
        {
            throw new UloPermissionException("update another user");
        }

        await Client.SendAsync<JsonNode>(HttpMethod.Put, $"api/v1/users/{user.Id}", user, ct).ConfigureAwait(false);
    }

    public Task DeleteUserAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin("delete user");
        return Client.SendAsync(HttpMethod.Delete, $"api/v1/users/{id}", null, ct);
    }

    public Task SetUserNotificationsAsync(int userId, IEnumerable<UloUserNotification> notifications, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { notifications = notifications.ToArray() }, UloJson.Options);
        return Client.SendAsync(HttpMethod.Put, $"api/v1/users/{userId}/notifications", body, ct);
    }

    public async Task<IReadOnlyList<UloUserDevice>> GetUserDevicesAsync(int userId, CancellationToken ct = default)
    {
        var json = await Client.GetJsonAsync($"api/v1/users/{userId}/devices", ct).ConfigureAwait(false);
        var array = (json?["devices"] ?? json) as JsonArray;
        return array?.Deserialize<List<UloUserDevice>>(UloJson.Options) ?? new List<UloUserDevice>();
    }

    // ------------------------------------------------------------ escape hatch

    /// <summary>Calls any API path directly - handy for firmware revisions that expose more than we model.</summary>
    public Task<string> CallApiAsync(string path, string method = "GET", string? body = null, CancellationToken ct = default)
        => Client.SendAsync(new HttpMethod(method.ToUpperInvariant()), path, body, ct);

    public void Dispose()
    {
        if (_ownsClient)
        {
            Client.Dispose();
        }
    }
}
