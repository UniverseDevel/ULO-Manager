using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace UloManager.Core;

public sealed record UloDownloadResult(int Downloaded, int Skipped, int Failed, long Bytes)
{
    public override string ToString() =>
        $"downloaded {Downloaded}, skipped {Skipped}, failed {Failed} ({UloMediaService.FormatBytes(Bytes)})";
}

/// <summary>Lists and downloads the recordings and snapshots stored on the camera.</summary>
public sealed class UloMediaService
{
    private static readonly Regex TimestampRegex = new(@"(?<ts>\d{8}_\d{6})", RegexOptions.Compiled);

    private readonly UloDevice _device;

    public UloMediaService(UloDevice device) => _device = device;

    public async Task<IReadOnlyList<UloMediaFile>> ListAsync(UloMediaType type = UloMediaType.All, CancellationToken ct = default)
    {
        var query = type switch
        {
            UloMediaType.Video => "?type=video",
            UloMediaType.Snapshot => "?type=snapshot",
            _ => "",
        };

        var json = await _device.Client.GetJsonAsync($"api/v1/files/media{query}", ct).ConfigureAwait(false);
        var days = json?["files"] as JsonArray;
        var files = new List<UloMediaFile>();

        if (days is null)
        {
            return files;
        }

        foreach (var day in days)
        {
            var dayName = day?["date"]?.GetValue<string>() ?? "";
            if (day?["files"] is not JsonArray dayFiles)
            {
                continue;
            }

            foreach (var node in dayFiles)
            {
                var path = node?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var fileName = Path.GetFileName(path);
                var fileType = fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                    ? UloMediaType.Video
                    : UloMediaType.Snapshot;

                if (type != UloMediaType.All && fileType != type)
                {
                    continue;
                }

                files.Add(new UloMediaFile
                {
                    Path = "/" + path.TrimStart('/'),
                    Day = dayName,
                    FileName = fileName,
                    Type = fileType,
                    Timestamp = ParseTimestamp(fileName),
                });
            }
        }

        return files
            .OrderBy(f => f.Timestamp ?? DateTime.MinValue)
            .ThenBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Available recording days, newest first.</summary>
    public async Task<IReadOnlyList<string>> ListDaysAsync(CancellationToken ct = default)
    {
        var json = await _device.Client.GetJsonAsync("api/v1/files/media", ct).ConfigureAwait(false);
        var days = json?["files"] as JsonArray;
        return days?
            .Select(node => node?["date"]?.GetValue<string>() ?? "")
            .Where(day => day.Length > 0)
            .OrderByDescending(day => day, StringComparer.Ordinal)
            .ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    /// <summary>
    /// Downloads media into any destination - a local folder, a network share or an FTP server.
    /// Files that already exist are skipped, and the newest file is left alone for a minute because
    /// the camera may still be writing into it.
    /// </summary>
    public async Task<UloDownloadResult> DownloadAsync(
        UloDestination destination,
        UloMediaType type = UloMediaType.All,
        TimeSpan? maxAge = null,
        bool organiseByDay = true,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        await destination.PrepareAsync(ct).ConfigureAwait(false);

        var staging = destination.CreateStagingFolder();
        var files = await ListAsync(type, ct).ConfigureAwait(false);
        var deviceTime = await _device.GetDeviceTimeAsync(ct).ConfigureAwait(false);
        if (deviceTime == DateTime.MinValue)
        {
            deviceTime = DateTime.Now;
        }

        var oldestWanted = maxAge.HasValue ? deviceTime - maxAge.Value : (DateTime?)null;
        var stillRecordingAfter = deviceTime.AddMinutes(-1);
        var newest = files.LastOrDefault();

        int downloaded = 0, skipped = 0, failed = 0;
        long bytes = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            if (oldestWanted.HasValue && file.Timestamp.HasValue && file.Timestamp < oldestWanted)
            {
                skipped++;
                continue;
            }

            if (ReferenceEquals(file, newest) && file.Timestamp.HasValue && file.Timestamp > stillRecordingAfter)
            {
                progress?.Report($"Skipping {file.FileName} - the camera may still be writing it.");
                skipped++;
                continue;
            }

            var relative = organiseByDay && file.Day.Length > 0
                ? Path.Combine(file.Day, file.FileName)
                : file.FileName;

            if (await destination.ExistsAsync(relative, ct).ConfigureAwait(false))
            {
                skipped++;
                continue;
            }

            var staged = Path.Combine(staging, relative);

            try
            {
                var size = await _device.Client.DownloadFileAsync(file.Path, staged, ct).ConfigureAwait(false);
                await destination.StoreAsync(staged, relative, ct).ConfigureAwait(false);

                bytes += size;
                downloaded++;
                progress?.Report($"Downloaded {file.FileName} ({FormatBytes(size)})");
            }
            catch (Exception ex) when (ex is UloApiException or IOException)
            {
                failed++;
                progress?.Report($"Failed {file.FileName}: {ex.Message}");
            }
        }

        return new UloDownloadResult(downloaded, skipped, failed, bytes);
    }

    /// <summary>Downloads media into a local folder.</summary>
    public async Task<UloDownloadResult> DownloadAsync(
        string destinationFolder,
        UloMediaType type = UloMediaType.All,
        TimeSpan? maxAge = null,
        bool organiseByDay = true,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        using var destination = UloDestination.Create(destinationFolder);
        return await DownloadAsync(destination, type, maxAge, organiseByDay, progress, ct).ConfigureAwait(false);
    }

    /// <summary>Removes local files older than the retention period (0 keeps everything).</summary>
    public static int ApplyRetention(string folder, TimeSpan retention)
    {
        if (retention <= TimeSpan.Zero || !Directory.Exists(folder))
        {
            return 0;
        }

        var limit = DateTime.Now - retention;
        var removed = 0;

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var stamp = ParseTimestamp(Path.GetFileName(file)) ?? File.GetLastWriteTime(file);
            if (stamp < limit)
            {
                try
                {
                    File.Delete(file);
                    removed++;
                }
                catch (IOException)
                {
                    // File in use - try again next run.
                }
            }
        }

        return removed;
    }

    public static DateTime? ParseTimestamp(string fileName)
    {
        var match = TimestampRegex.Match(fileName);
        return match.Success && DateTime.TryParseExact(
            match.Groups["ts"].Value,
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
