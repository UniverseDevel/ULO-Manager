using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace UloManager.Core;

/// <summary>
/// Reads and interprets the camera system log so the UI can show what the camera is actually doing.
/// The log is a rolling buffer, entries are not always chronological and the clock can be wrong
/// (the camera boots at 01/01/70 until it syncs time), so tailing is done by content overlap.
/// </summary>
public sealed class UloLogService
{
    private static readonly Regex LineRegex = new(
        @"^(?<ts>\d{2}/\d{2}/\d{2}\s+\d{2}:\d{2}:\d{2}(?:\.\d+)?)\s*\|\s*(?<msg>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex VideoEventRegex = new(@"notifyVideoEvent\s*-\s*event=(?<event>\d+)", RegexOptions.Compiled);
    private static readonly Regex PlugStateRegex = new(@"NotifyPlugState=(?<state>\d+)", RegexOptions.Compiled);
    private static readonly Regex ModeChangeRegex = new(@"Ulo mode changing to (?<mode>\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WifiConnectRegex = new(@"Connected to network ""(?<ssid>[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex WifiDropRegex = new(@"disconnected from network ""(?<ssid>[^""]*)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AdHocRegex = new(@"Ad'hoc mode started with SSID ""(?<ssid>[^""]*)""", RegexOptions.Compiled);

    private readonly UloDevice _device;
    private IReadOnlyList<string>? _previous;

    /// <summary>How many trailing lines are used to recognise where the previous read ended.</summary>
    private const int SignatureLines = 25;

    public UloLogService(UloDevice device) => _device = device;

    /// <summary>Downloads the raw system log exactly as the camera returns it.</summary>
    public async Task<string> GetRawAsync(CancellationToken ct = default)
    {
        // Firmware >= 08.0000: GET /api/v1/system/log may not return usable text.
        // Try reading from the exposed /logs/log.txt or /logs/debug.txt first (no auth needed),
        // then fall back to the API endpoint.
        if (_device.FirmwareVersion.UsesLogPost)
        {
            // Try the exposed log files (available without authentication on 08.0904+).
            foreach (var logPath in new[] { "logs/log.txt", "logs/debug.txt" })
            {
                try
                {
                    var content = await _device.Client.GetRawAsync(logPath, ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(content) && content.Contains('|'))
                        return content;
                }
                catch
                {
                    // Not available on this firmware version — continue.
                }
            }
        }

        var raw = await _device.Client.GetRawAsync("api/v1/system/log", ct).ConfigureAwait(false);

        // Some firmware versions wrap the log in a JSON object like {"log":"..."}.
        var trimmed = raw.TrimStart();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                var json = System.Text.Json.Nodes.JsonNode.Parse(raw);
                if (json is System.Text.Json.Nodes.JsonObject obj)
                {
                    foreach (var key in new[] { "log", "text", "content", "data" })
                    {
                        var value = obj[key]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(value))
                            return value;
                    }
                }
            }
            catch
            {
                // Not valid JSON — treat as raw text.
            }
        }

        return raw;
    }

    /// <summary>Downloads and parses the whole system log.</summary>
    public async Task<IReadOnlyList<UloLogEntry>> GetEntriesAsync(CancellationToken ct = default)
    {
        var raw = await GetRawAsync(ct).ConfigureAwait(false);
        return Parse(raw);
    }

    /// <summary>
    /// Saves the raw log to disk. On firmware &gt;= 08.0000 the camera supports POST to
    /// <c>/api/v1/system/log</c> which returns a ZIP file name that can be downloaded from
    /// <c>/logs/</c>. On older firmware only GET is available, returning raw text.
    /// </summary>
    public async Task<string> SaveAsync(string destinationFolder, CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationFolder);

        if (_device.FirmwareVersion.UsesLogPost)
        {
            // Firmware >= 08.0000: POST returns {"fileName": "UloLogs_xxxx_yyyy.zip"}
            try
            {
                var json = await _device.Client.SendJsonAsync(HttpMethod.Post, "api/v1/system/log", "{}", ct)
                    .ConfigureAwait(false);
                var fileName = json?["fileName"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    var remotePath = $"logs/{fileName.TrimStart('/')}";
                    var localPath = Path.Combine(destinationFolder, Path.GetFileName(fileName));
                    await _device.Client.DownloadFileAsync(remotePath, localPath, ct).ConfigureAwait(false);
                    return localPath;
                }
            }
            catch (UloApiException)
            {
                // Fall through to the GET path if POST fails.
            }
        }

        // Firmware < 08.0000 or POST fallback: GET returns raw text.
        var raw = await GetRawAsync(ct).ConfigureAwait(false);
        var file = Path.Combine(destinationFolder, $"system_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        await File.WriteAllTextAsync(file, raw, ct).ConfigureAwait(false);
        return file;
    }

    /// <summary>
    /// Returns only the entries that appeared since the previous call.
    /// The first call returns the tail of the log (<paramref name="initialLines"/> entries,
    /// or everything when it is zero or negative).
    /// </summary>
    public async Task<IReadOnlyList<UloLogEntry>> GetNewEntriesAsync(int initialLines = 50, CancellationToken ct = default)
    {
        var entries = await GetEntriesAsync(ct).ConfigureAwait(false);
        if (entries.Count == 0)
        {
            return entries;
        }

        IReadOnlyList<UloLogEntry> result;
        if (_previous is null)
        {
            result = initialLines <= 0 || entries.Count <= initialLines
                ? entries
                : entries.Skip(entries.Count - initialLines).ToList();
        }
        else
        {
            var index = FindOverlapEnd(_previous, entries);
            result = index < 0
                ? entries // the ring buffer wrapped past everything we had - treat it all as new
                : entries.Skip(index + 1).ToList();
        }

        _previous = entries.Select(entry => entry.RawLine).ToList();
        return result;
    }

    /// <summary>
    /// Finds where the previously seen lines end inside the new snapshot.
    /// <para>
    /// The log repeats identical lines constantly (<c>MCU NotifyPlugState=1</c> appears hundreds of
    /// times), so matching a single line picks the wrong position and whole blocks get skipped or
    /// repeated. Matching the longest possible run of trailing lines makes the position unambiguous.
    /// </para>
    /// </summary>
    private static int FindOverlapEnd(IReadOnlyList<string> previous, IReadOnlyList<UloLogEntry> current)
    {
        var maxRun = Math.Min(SignatureLines, Math.Min(previous.Count, current.Count));

        for (var run = maxRun; run >= 1; run--)
        {
            var tail = previous.Skip(previous.Count - run).ToArray();

            for (var end = current.Count - 1; end >= run - 1; end--)
            {
                var matches = true;
                for (var i = 0; i < run; i++)
                {
                    if (!string.Equals(current[end - run + 1 + i].RawLine, tail[i], StringComparison.Ordinal))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return end;
                }
            }
        }

        return -1;
    }

    /// <summary>Continuously yields new log entries. Ideal for a live "what is the camera doing" view.</summary>
    public async IAsyncEnumerable<UloLogEntry> TailAsync(
        TimeSpan pollInterval,
        int initialLines = 50,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            IReadOnlyList<UloLogEntry> batch;
            try
            {
                batch = await GetNewEntriesAsync(initialLines, ct).ConfigureAwait(false);
            }
            catch (UloApiException) when (!ct.IsCancellationRequested)
            {
                // The camera drops connections while it sleeps in alert mode - just retry.
                batch = Array.Empty<UloLogEntry>();
            }

            foreach (var entry in batch)
            {
                yield return entry;
            }

            try
            {
                await Task.Delay(pollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    public void ResetTail() => _previous = null;

    public static IReadOnlyList<UloLogEntry> Parse(string raw)
    {
        var result = new List<UloLogEntry>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            result.Add(ParseLine(trimmed));
        }

        return result;
    }

    public static UloLogEntry ParseLine(string line)
    {
        var match = LineRegex.Match(line);
        DateTime? timestamp = null;
        var message = line;

        if (match.Success)
        {
            // The camera prints dd/MM/yy once its clock is synchronised and falls back to
            // 01/01/70 right after a boot, so both day-first and month-first are accepted.
            // Firmware 08.0904+ adds milliseconds (dd/MM/yy HH:mm:ss.fff).
            string[] formats =
            {
                "dd/MM/yy HH:mm:ss.fff", "dd/MM/yy HH:mm:ss",
                "MM/dd/yy HH:mm:ss.fff", "MM/dd/yy HH:mm:ss",
            };
            if (DateTime.TryParseExact(
                    match.Groups["ts"].Value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                timestamp = parsed;
            }

            message = match.Groups["msg"].Value.Trim();
        }

        var (activity, severity) = Classify(message);

        return new UloLogEntry
        {
            RawLine = line,
            Timestamp = timestamp,
            Message = message,
            Activity = activity,
            Severity = severity,
        };
    }

    /// <summary>Turns a raw log message into a short human readable activity.</summary>
    public static (string? Activity, UloLogSeverity Severity) Classify(string message)
    {
        if (message.Contains("Ulo started", StringComparison.OrdinalIgnoreCase))
        {
            return ("Camera booted (mode resets to standard)", UloLogSeverity.Notice);
        }

        if (message.Contains("initialized successfully", StringComparison.OrdinalIgnoreCase))
        {
            return ("Camera ready", UloLogSeverity.Notice);
        }

        if (message.Contains("displacement detected", StringComparison.OrdinalIgnoreCase))
        {
            return ("Camera was moved / displacement detected", UloLogSeverity.Warning);
        }

        var modeChange = ModeChangeRegex.Match(message);
        if (modeChange.Success)
        {
            return ($"Mode changing to {modeChange.Groups["mode"].Value}", UloLogSeverity.Notice);
        }

        var videoEvent = VideoEventRegex.Match(message);
        if (videoEvent.Success)
        {
            // Event codes are not documented; the mapping below was derived by correlating
            // the log with the recordings the camera produced at the same moment.
            var code = videoEvent.Groups["event"].Value;
            return (code switch
            {
                "0" => "Video pipeline stopped",
                "1" => "Video pipeline started",
                "2" => "Recording finished",
                "3" => "Recording started (motion detected)",
                _ => $"Video event {code}",
            }, UloLogSeverity.Info);
        }

        var plug = PlugStateRegex.Match(message);
        if (plug.Success)
        {
            return (plug.Groups["state"].Value == "1" ? "Running on mains power" : "Running on battery", UloLogSeverity.Info);
        }

        var adhoc = AdHocRegex.Match(message);
        if (adhoc.Success)
        {
            return ($"Setup (ad-hoc) network started: {adhoc.Groups["ssid"].Value}", UloLogSeverity.Warning);
        }

        if (message.Contains("Ad'hoc mode was unexpectedly stopped", StringComparison.OrdinalIgnoreCase))
        {
            return ("Setup (ad-hoc) network stopped unexpectedly", UloLogSeverity.Warning);
        }

        var wifiDrop = WifiDropRegex.Match(message);
        if (wifiDrop.Success)
        {
            return ($"Wi-Fi lost: {wifiDrop.Groups["ssid"].Value}", UloLogSeverity.Warning);
        }

        var wifi = WifiConnectRegex.Match(message);
        if (wifi.Success)
        {
            return ($"Wi-Fi connected: {wifi.Groups["ssid"].Value}", UloLogSeverity.Info);
        }

        if (message.Contains("updateExclusionConfiguration", StringComparison.OrdinalIgnoreCase))
        {
            return ("Exclusion zone updated", UloLogSeverity.Info);
        }

        if (message.Contains("configuration file is missing or corrupt", StringComparison.OrdinalIgnoreCase))
        {
            return ("Configuration was corrupt, backup restored", UloLogSeverity.Error);
        }

        if (message.Contains("ready for software update", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("FirmwareVersion", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("completeFlashJob", StringComparison.OrdinalIgnoreCase))
        {
            return ("Firmware / MCU housekeeping", UloLogSeverity.Info);
        }

        if (message.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            return (null, UloLogSeverity.Error);
        }

        return (null, UloLogSeverity.Info);
    }
}
