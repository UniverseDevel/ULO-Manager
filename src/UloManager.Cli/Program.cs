using System.Text.Json;
using UloManager.Core;

namespace UloManager.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var cli = CommandLine.Parse(args);

        if (cli.Command.Length == 0 || cli.Command is "help" or "-h" or "--help")
        {
            PrintHelp();
            return 0;
        }

        // Commands that do not need a camera connection.
        if (cli.Command is "discover" or "scan")
        {
            return await DiscoverAsync(cli);
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        UloDevice? device = null;

        try
        {
            var options = BuildOptions(cli);
            device = new UloDevice(options);

            if (cli.HasFlag("trace"))
            {
                device.Client.Trace += (_, e) => Console.Error.WriteLine($"  http> {e}");
            }

            var info = await device.ConnectAsync(cancellation.Token);

            if (!cli.HasFlag("quiet"))
            {
                Console.WriteLine($"Connected to '{info.DeviceName}' ({options.BaseAddress}) as {info.CurrentUser.Email}");
                Console.WriteLine($"Camera mode : {(info.DeviceMode == UloDeviceMode.Setup ? "SETUP / configuration (upside down)" : "USAGE (upright)")}");
                Console.WriteLine($"Session     : {(info.OperatingMode == UloOperatingMode.AdminSetup ? "administrator (setup allowed)" : "standard user")}" +
                                  $"  firmware {info.FirmwareVersion}");
                Console.WriteLine();
            }

            return await RunCommandAsync(device, cli, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine("Cancelled.");
            return 130;
        }
        catch (UloPermissionException ex)
        {
            Console.Error.WriteLine($"Not allowed: {ex.Message}");
            return 3;
        }
        catch (UloApiException ex)
        {
            Console.Error.WriteLine($"Camera error: {ex.Message}");
            if (!string.IsNullOrWhiteSpace(ex.ResponseBody))
            {
                Console.Error.WriteLine(ex.ResponseBody);
            }

            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            if (device is not null)
            {
                try
                {
                    await device.DisconnectAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    // Best effort logout.
                }

                device.Dispose();
            }
        }
    }

    private static UloConnectionOptions BuildOptions(CommandLine cli)
    {
        var settings = UloSettings.Load();
        var options = new UloConnectionOptions
        {
            Host = cli.GetOption("host") ?? Environment.GetEnvironmentVariable("ULO_HOST") ?? "",
            UserName = cli.GetOption("user") ?? Environment.GetEnvironmentVariable("ULO_USER") ?? "",
            Password = cli.GetOption("password") ?? Environment.GetEnvironmentVariable("ULO_PASSWORD") ?? "",
            UseHttps = cli.HasFlag("https"),
            AcceptDeviceCertificate = cli.HasFlag("https") || cli.HasFlag("accept-device-cert"),
            PinnedCertificateThumbprint = cli.GetOption("pin-cert"),
        };

        // Fill blanks from saved settings.
        settings.ApplyTo(options);

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new ArgumentException("No camera host. Use --host or set ULO_HOST.");
        }

        if (string.IsNullOrWhiteSpace(options.UserName))
        {
            throw new ArgumentException("No user name. Use --user or set ULO_USER.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            Console.Write("Password: ");
            options.Password = ReadPassword();
        }

        // Save for next time.
        settings.SaveFrom(options);

        return options;
    }

    private static string ReadPassword()
    {
        var password = "";

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password = password[..^1];
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
            }
        }
    }

    private static async Task<int> RunCommandAsync(UloDevice device, CommandLine cli, CancellationToken ct)
    {
        switch (cli.Command)
        {
            case "status":
                return await StatusAsync(device, ct);

            case "watch":
                return await WatchAsync(device, cli, ct);

            case "mode":
                if (cli.Positional.Count == 0)
                {
                    Console.WriteLine((await device.GetModeAsync(ct)).ToApiValue());
                }
                else
                {
                    var target = UloModeExtensions.ParseMode(cli.GetPositional(0));
                    await device.SetModeAsync(target, ct);
                    Console.WriteLine($"Mode set to {target.ToApiValue()}.");
                }

                return 0;

            case "snapshot":
            {
                var folder = cli.GetOption("out") ?? Directory.GetCurrentDirectory();
                var file = await device.DownloadCurrentSnapshotAsync(folder, cli.HasFlag("store"), ct);
                Console.WriteLine($"Snapshot saved to {file}");
                return 0;
            }

            case "live":
                return await LiveAsync(device, cli, ct);

            case "record":
            {
                if (cli.Positional.Count == 0)
                {
                    Console.WriteLine(await device.IsRecordingAsync(ct) ? "recording" : "idle");
                    return 0;
                }

                var start = cli.GetPositional(0).Equals("start", StringComparison.OrdinalIgnoreCase);
                await device.SetRecordingAsync(start, ct);
                Console.WriteLine(start ? "Recording started on the camera." : "Recording stopped.");
                return 0;
            }

            case "media":
                return await MediaAsync(device, cli, ct);

            case "download":
            {
                var target = cli.GetOption("out") ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
                var type = ParseMediaType(cli.GetOption("type", "all")!);
                TimeSpan? age = cli.GetInt("age", 0) > 0 ? TimeSpan.FromHours(cli.GetInt("age", 0)) : null;

                using var destination = UloDestination.Create(
                    target,
                    cli.GetOption("dest-user"),
                    cli.GetOption("dest-password"));

                Console.WriteLine($"Destination: {destination.Description}");

                var progress = new Progress<string>(Console.WriteLine);
                var result = await device.Media.DownloadAsync(destination, type, age, !cli.HasFlag("flat"), progress, ct);
                Console.WriteLine(result.ToString());

                var retention = cli.GetInt("retention", 0);
                if (retention > 0)
                {
                    var removed = await destination.ApplyRetentionAsync(TimeSpan.FromHours(retention), ct);
                    Console.WriteLine($"Retention removed {removed} file(s) from the destination.");
                }

                return 0;
            }

            case "availability":
            {
                var hosts = (cli.GetOption("hosts") ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (hosts.Length == 0)
                {
                    Console.Error.WriteLine("Use --hosts with a comma separated list of addresses.");
                    return 1;
                }

                var rule = (cli.GetOption("rule", "any") ?? "any").Equals("all", StringComparison.OrdinalIgnoreCase)
                    ? UloAvailabilityRule.All
                    : UloAvailabilityRule.Any;

                var upMode = cli.GetOption("if-up");
                var downMode = cli.GetOption("if-down");
                var progress = new Progress<string>(Console.WriteLine);

                if (upMode is null || downMode is null)
                {
                    // Reporting only - no mode change.
                    var availability = await device.Availability.CheckAsync(hosts, rule, ct);
                    Console.WriteLine(availability.ToString());
                    return availability.IsAvailable ? 0 : 1;
                }

                await device.Availability.ApplyModeAsync(
                    hosts,
                    UloModeExtensions.ParseMode(upMode),
                    UloModeExtensions.ParseMode(downMode),
                    rule,
                    cli.HasFlag("force"),
                    progress,
                    ct);

                return 0;
            }

            case "log":
                return await LogAsync(device, cli, ct);

            case "storage":
            {
                var stats = await device.GetStorageAsync(ct);
                Console.WriteLine($"Internal : {stats.Internal.FreeMb} MB free of {stats.Internal.TotalMb} MB ({stats.Internal.UsedPercent}% used)");
                Console.WriteLine(stats.SdCard.Inserted
                    ? $"SD card  : {stats.SdCard.FreeMb} MB free of {stats.SdCard.TotalMb} MB"
                    : "SD card  : not inserted");
                Console.WriteLine($"Move to card running: {await device.IsMoveToCardRunningAsync(ct)}");
                return 0;
            }

            case "movetocard":
                await device.StartMoveToCardAsync(ct);
                Console.WriteLine("Move to SD card started. The camera cannot record while it runs.");

                if (cli.HasFlag("wait"))
                {
                    await device.WaitForMoveToCardAsync(TimeSpan.FromSeconds(5), ct);
                    Console.WriteLine("Move finished.");
                }

                return 0;

            case "clean":
            {
                var period = Enum.Parse<UloCleanPeriod>(cli.GetPositional(0, "OldestDay")!, ignoreCase: true);
                if (!cli.HasFlag("yes"))
                {
                    Console.Write($"Delete '{period}' from the camera storage? [y/N] ");
                    if (!string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Aborted.");
                        return 0;
                    }
                }

                await device.CleanStorageAsync(period, ct);
                Console.WriteLine($"Requested deletion of '{period}'.");
                return 0;
            }

            case "config":
                return await ConfigAsync(device, cli, ct);

            case "wifi":
                return await WifiAsync(device, cli, ct);

            case "users":
                return await UsersAsync(device, cli, ct);

            case "time":
                if (cli.Positional.Count > 0 && cli.GetPositional(0).Equals("sync", StringComparison.OrdinalIgnoreCase))
                {
                    await device.SetDeviceTimeAsync(DateTime.Now, ct);
                    Console.WriteLine($"Camera clock set to {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");
                }
                else
                {
                    Console.WriteLine((await device.GetDeviceTimeAsync(ct)).ToString("yyyy-MM-dd HH:mm:ss"));
                }

                return 0;

            case "firmware":
            {
                var report = await device.CheckForUpdatesAsync(ct);
                Console.WriteLine(report.Describe());

                if (cli.HasFlag("download"))
                {
                    await device.StartFotaDownloadAsync(ct);
                    Console.WriteLine();
                    Console.WriteLine("Over-the-air download requested.");
                }

                if (cli.HasFlag("install"))
                {
                    if (!report.InstallAvailable)
                    {
                        Console.WriteLine();
                        Console.WriteLine("There is no downloaded firmware ready to install.");
                        return 0;
                    }

                    await device.InstallFirmwareAsync(ct);
                    Console.WriteLine();
                    Console.WriteLine("Firmware installation requested - the camera will restart.");
                }

                return 0;
            }

            case "backup":
            {
                var sub = cli.GetPositional(0, "list")!.ToLowerInvariant();

                switch (sub)
                {
                    case "create":
                        await device.CreateBackupAsync(cli.GetOption("name"), ct);
                        Console.WriteLine("Settings backup created on the camera.");
                        return 0;

                    case "restore":
                        await device.RestoreBackupAsync(cli.GetPositional(1), ct);
                        Console.WriteLine("Settings restored.");
                        return 0;

                    default:
                        var backups = await device.GetBackupsAsync(ct);
                        if (backups.Count == 0)
                        {
                            Console.WriteLine("The camera holds no settings backups.");
                        }

                        foreach (var backup in backups)
                        {
                            Console.WriteLine(backup);
                        }

                        return 0;
                }
            }

            case "reset":
            {
                Console.WriteLine("A factory reset erases all users, the Wi-Fi setup and the recordings.");
                Console.Write("Type RESET to continue: ");

                if (Console.ReadLine() != "RESET")
                {
                    Console.WriteLine("Aborted.");
                    return 0;
                }

                await device.FactoryResetAsync(ct);
                Console.WriteLine("Factory reset requested. The camera will restart into its setup network.");
                return 0;
            }

            case "api":
            {
                var path = cli.GetPositional(0);
                var method = cli.GetOption("method", "GET")!;
                var body = cli.GetOption("body");
                var response = await device.CallApiAsync(path, method, body, ct);
                Console.WriteLine(UloJson.Pretty(response));
                return 0;
            }

            default:
                Console.Error.WriteLine($"Unknown command '{cli.Command}'. Run 'ulo help'.");
                return 1;
        }
    }

    private static async Task<int> StatusAsync(UloDevice device, CancellationToken ct)
    {
        using var monitor = new UloActivityMonitor(device);
        var snapshot = await monitor.CaptureAsync(ct);
        var config = await device.GetConfigurationAsync(ct);

        Console.WriteLine($"Name          : {config.Device.Name}");
        Console.WriteLine($"Recording mode: {snapshot.Mode.ToApiValue()}");
        Console.WriteLine($"Battery       : {snapshot.State.BatteryLevel}% ({(snapshot.State.Plugged ? "plugged in" : "on battery")})");
        Console.WriteLine($"Camera clock  : {snapshot.DeviceTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Firmware      : {config.Firmware.CurrentVersion} (cloud {config.Firmware.CloudVersion}, update {(config.Firmware.UpdateAvailable ? "available" : "none")})");
        Console.WriteLine($"Wi-Fi         : {config.Wifi.Ssid}");
        Console.WriteLine($"Video quality : {config.Video.Quality}");
        Console.WriteLine($"Time zone     : {config.Time.TimeZone} (auto: {config.Time.Auto})");
        Console.WriteLine($"Internal      : {snapshot.Storage.Internal.FreeMb} MB free of {snapshot.Storage.Internal.TotalMb} MB");
        Console.WriteLine(snapshot.Storage.SdCard.Inserted
            ? $"SD card       : {snapshot.Storage.SdCard.FreeMb} MB free of {snapshot.Storage.SdCard.TotalMb} MB"
            : "SD card       : not inserted");
        Console.WriteLine($"Recordings    : {snapshot.MediaFileCount} video file(s)");
        Console.WriteLine($"Backup job    : {(snapshot.BackupRunning ? "running" : "idle")}");
        Console.WriteLine($"Device mode   : {(snapshot.State.DeviceMode == UloDeviceMode.Setup ? "SETUP / configuration (camera is upside down)" : "USAGE (camera is upright)")}");

        return 0;
    }

    private static async Task<int> WatchAsync(UloDevice device, CommandLine cli, CancellationToken ct)
    {
        using var monitor = new UloActivityMonitor(device)
        {
            PollInterval = TimeSpan.FromSeconds(cli.GetInt("interval", 10)),
            StatePollInterval = TimeSpan.FromSeconds(cli.GetInt("state-interval", 3)),
            InitialLogLines = cli.GetInt("lines", 15),
        };

        monitor.Activity += (_, e) =>
        {
            var colour = e.Kind == UloActivityKind.DeviceModeChanged
                ? ConsoleColor.Magenta
                : e.Severity switch
                {
                    UloLogSeverity.Error => ConsoleColor.Red,
                    UloLogSeverity.Warning => ConsoleColor.Yellow,
                    UloLogSeverity.Notice => ConsoleColor.Cyan,
                    _ => ConsoleColor.Gray,
                };

            var mode = e.State is null
                ? "     "
                : e.State.DeviceMode == UloDeviceMode.Setup ? "SETUP" : "USAGE";

            var previous = Console.ForegroundColor;
            Console.ForegroundColor = colour;
            Console.WriteLine($"{e.TimestampUtc.ToLocalTime():HH:mm:ss} {mode} {e.Kind,-17} {e.Message}");
            Console.ForegroundColor = previous;
        };

        Console.WriteLine("Watching the camera. The mode column shows SETUP while the camera is upside down.");
        Console.WriteLine("Press Ctrl+C to stop.");
        monitor.Start();

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C
        }

        await monitor.StopAsync();
        return 0;
    }

    /// <summary>
    /// Live video. The camera streams fragmented MP4 over a WebSocket, so the data can either be
    /// written to a file or piped straight into a player such as VLC or ffplay.
    /// </summary>
    private static async Task<int> LiveAsync(UloDevice device, CommandLine cli, CancellationToken ct)
    {
        var seconds = cli.GetInt("seconds", 0);
        using var limit = seconds > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (seconds > 0)
        {
            limit.CancelAfter(TimeSpan.FromSeconds(seconds));
        }

        device.LiveVideo.StatusChanged += (_, message) => Console.WriteLine(message);

        if (cli.HasFlag("play"))
        {
            var player = FindPlayer(cli.GetOption("player"));
            if (player is null)
            {
                Console.Error.WriteLine("No player found. Install VLC or ffplay, or use --out to record to a file.");
                return 1;
            }

            Console.WriteLine($"Streaming live video into {Path.GetFileName(player)}. Press Ctrl+C to stop.");

            // Hardware decoding is disabled on purpose: the stream is a fragmented MP4 that starts
            // mid-sequence and D3D11VA fails to allocate pictures for it, leaving a black window.
            // --play-and-exit is avoided as well, since VLC quits on the first hiccup of the pipe.
            var arguments = player.EndsWith("ffplay.exe", StringComparison.OrdinalIgnoreCase)
                ? "-loglevel warning -fflags nobuffer -flags low_delay -"
                : "--no-plugins-cache --avcodec-hw=none --no-video-title-show --quiet " +
                  "--file-caching=500 --network-caching=500 -";

            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = player,
                Arguments = arguments,
                RedirectStandardInput = true,
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException("The player could not be started.");

            await device.LiveVideo.PipeToAsync(process.StandardInput.BaseStream, limit.Token);
            process.StandardInput.Close();
            return 0;
        }

        var file = cli.GetOption("out") ?? $"ulo_live_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        Console.WriteLine($"Recording live video to {file}" + (seconds > 0 ? $" for {seconds}s." : ". Press Ctrl+C to stop."));

        var bytes = await device.LiveVideo.RecordToFileAsync(file, limit.Token);
        Console.WriteLine($"Wrote {UloMediaService.FormatBytes(bytes)} to {file}");
        return 0;
    }

    private static string? FindPlayer(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return File.Exists(preferred) ? preferred : null;
        }

        var candidates = OperatingSystem.IsWindows()
            ? new[]
            {
                @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe",
            }
            : new[]
            {
                "/usr/bin/vlc",
                "/usr/local/bin/vlc",
                "/snap/bin/vlc",
                "/usr/bin/ffplay",
                "/usr/local/bin/ffplay",
                "/opt/homebrew/bin/vlc",
                "/opt/homebrew/bin/ffplay",
            };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var names = OperatingSystem.IsWindows()
            ? new[] { "ffplay.exe", "vlc.exe" }
            : new[] { "ffplay", "vlc" };

        foreach (var name in names)
        {
            var path = Environment.GetEnvironmentVariable("PATH")?
                .Split(Path.PathSeparator)
                .Select(dir => Path.Combine(dir.Trim(), name))
                .FirstOrDefault(File.Exists);

            if (path is not null)
            {
                return path;
            }
        }

        return null;
    }

    private static async Task<int> MediaAsync(UloDevice device, CommandLine cli, CancellationToken ct)
    {
        var sub = cli.GetPositional(0, "list")!.ToLowerInvariant();

        switch (sub)
        {
            case "days":
                foreach (var day in await device.Media.ListDaysAsync(ct))
                {
                    Console.WriteLine(day);
                }

                return 0;

            case "delete":
            {
                var day = cli.GetPositional(1);
                if (!cli.HasFlag("yes"))
                {
                    Console.Write($"Delete every recording of {day}? [y/N] ");
                    if (!string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Aborted.");
                        return 0;
                    }
                }

                await device.DeleteMediaDayAsync(day, ct);
                Console.WriteLine($"Deleted {day}.");
                return 0;
            }

            default:
            {
                var files = await device.Media.ListAsync(ParseMediaType(cli.GetOption("type", "all")!), ct);
                foreach (var file in files)
                {
                    Console.WriteLine($"{file.Timestamp:yyyy-MM-dd HH:mm:ss}  {file.Type,-8} {file.Path}");
                }

                Console.WriteLine($"{files.Count} file(s).");
                return 0;
            }
        }
    }

    private static async Task<int> LogAsync(UloDevice device, CommandLine cli, CancellationToken ct)
    {
        var sub = cli.GetPositional(0, "show")!.ToLowerInvariant();

        switch (sub)
        {
            case "save":
            {
                var file = await device.Log.SaveAsync(cli.GetOption("out") ?? Directory.GetCurrentDirectory(), ct);
                Console.WriteLine($"Log saved to {file}");
                return 0;
            }

            case "tail":
            {
                Console.WriteLine("Tailing the camera log. Press Ctrl+C to stop.");
                var interval = TimeSpan.FromSeconds(cli.GetInt("interval", 10));

                await foreach (var entry in device.Log.TailAsync(interval, cli.GetInt("lines", 20), ct))
                {
                    PrintLogEntry(entry);
                }

                return 0;
            }

            default:
            {
                var entries = await device.Log.GetEntriesAsync(ct);
                var take = cli.GetInt("lines", 50);
                foreach (var entry in take > 0 ? entries.Skip(Math.Max(0, entries.Count - take)) : entries)
                {
                    PrintLogEntry(entry);
                }

                return 0;
            }
        }
    }

    private static void PrintLogEntry(UloLogEntry entry)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = entry.Severity switch
        {
            UloLogSeverity.Error => ConsoleColor.Red,
            UloLogSeverity.Warning => ConsoleColor.Yellow,
            UloLogSeverity.Notice => ConsoleColor.Cyan,
            _ => ConsoleColor.Gray,
        };

        var stamp = entry.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "??? ";
        Console.WriteLine(entry.Activity is null
            ? $"{stamp}  {entry.Message}"
            : $"{stamp}  {entry.Activity}  ({entry.Message})");

        Console.ForegroundColor = previous;
    }

    private static async Task<int> ConfigAsync(UloDevice device, CommandLine cli, CancellationToken ct)
    {
        var sub = cli.GetPositional(0, "show")!.ToLowerInvariant();

        switch (sub)
        {
            case "set":
            {
                var section = cli.GetPositional(1);
                var json = cli.GetPositional(2);
                await device.UpdateConfigSectionAsync(section, json, ct);
                Console.WriteLine($"Section '{section}' updated.");
                return 0;
            }

            case "name":
            {
                var name = cli.GetPositional(1);
                await device.SetDeviceNameAsync(name, ct);
                Console.WriteLine($"Camera renamed to '{name}'.");
                return 0;
            }

            case "quality":
            {
                var quality = cli.GetPositional(1);
                await device.SetVideoQualityAsync(quality, ct);
                Console.WriteLine($"Video quality set to {quality}.");
                return 0;
            }

            default:
            {
                if (cli.Positional.Count > 1)
                {
                    var section = cli.GetPositional(1);
                    Console.WriteLine(UloJson.Pretty(await device.CallApiAsync($"api/v1/config/{section}", "GET", null, ct)));
                    return 0;
                }

                var config = await device.GetConfigurationAsync(ct);
                Console.WriteLine(JsonSerializer.Serialize(config, UloJson.Indented));
                return 0;
            }
        }
    }

    private static async Task<int> WifiAsync(UloDevice device, CommandLine cli, CancellationToken ct)
    {
        var sub = cli.GetPositional(0, "show")!.ToLowerInvariant();

        switch (sub)
        {
            case "scan":
            {
                var networks = await device.ScanWifiAsync(ct);
                if (networks.Count == 0)
                {
                    Console.WriteLine("The camera reported no networks (it only scans while awake in setup mode).");
                }

                foreach (var network in networks)
                {
                    Console.WriteLine($"{network.Ssid,-32} level={network.Level} secured={network.Secured}");
                }

                return 0;
            }

            case "connect":
            {
                var ssid = cli.GetPositional(1);
                var password = cli.GetOption("password-wifi") ?? cli.GetPositional(2, "");
                await device.ConnectWifiAsync(ssid, password, ct);
                Console.WriteLine($"Camera asked to join '{ssid}'. It may drop off the network while reconnecting.");
                return 0;
            }

            default:
            {
                var config = await device.GetConfigSectionAsync<UloWifiConfig>("wifi", ct);
                Console.WriteLine($"Current SSID: {config?.Ssid}");
                return 0;
            }
        }
    }

    private static async Task<int> UsersAsync(UloDevice device, CommandLine cli, CancellationToken ct)
    {
        var sub = cli.GetPositional(0, "list")!.ToLowerInvariant();

        switch (sub)
        {
            case "add":
            {
                var user = new UloUser
                {
                    Email = cli.GetPositional(1),
                    Name = cli.GetOption("name") ?? cli.GetPositional(1),
                    Password = cli.GetOption("user-password") ?? throw new ArgumentException("Use --user-password for the new account."),
                    Account = cli.HasFlag("admin") ? "admin" : "user",
                };

                var created = await device.CreateUserAsync(user, ct);
                Console.WriteLine($"User '{user.Email}' created (id {created?.Id}).");
                return 0;
            }

            case "delete":
            {
                var id = int.Parse(cli.GetPositional(1));
                await device.DeleteUserAsync(id, ct);
                Console.WriteLine($"User {id} deleted.");
                return 0;
            }

            case "show":
            {
                var id = int.Parse(cli.GetPositional(1));
                var user = await device.GetUserAsync(id, ct);
                Console.WriteLine(JsonSerializer.Serialize(user, UloJson.Indented));
                return 0;
            }

            default:
            {
                foreach (var user in await device.GetUsersAsync(ct))
                {
                    Console.WriteLine($"{user.Id,3}  {user.Email,-34} {(user.IsAdmin ? "admin" : "user")}  devices={user.Devices.Count}");
                }

                return 0;
            }
        }
    }

    private static UloMediaType ParseMediaType(string value) => value.ToLowerInvariant() switch
    {
        "video" or "videos" => UloMediaType.Video,
        "snapshot" or "snapshots" or "image" => UloMediaType.Snapshot,
        _ => UloMediaType.All,
    };

    private static async Task<int> DiscoverAsync(CommandLine cli)
    {
        var timeout = TimeSpan.FromSeconds(cli.GetInt("timeout", 2));
        Console.WriteLine("Scanning local network for ULO cameras...");
        Console.WriteLine();

        var results = await UloDiscovery.ScanAsync(
            progress: found =>
            {
                Console.WriteLine($"  Found: {found}");
            },
            timeout: timeout);

        Console.WriteLine();
        if (results.Count == 0)
        {
            Console.WriteLine("No ULO cameras found on the network.");
            Console.WriteLine("Make sure the camera is powered on, connected to Wi-Fi and on the same network.");
        }
        else
        {
            Console.WriteLine($"{results.Count} camera(s) found:");
            Console.WriteLine();
            foreach (var camera in results)
            {
                Console.WriteLine($"  {camera}");
            }

            // Save discovered cameras to settings.
            var settings = UloSettings.Load();
            foreach (var camera in results)
            {
                settings.AddKnownCamera(camera);
            }
            settings.Save();
        }

        return results.Count > 0 ? 0 : 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
ULO Manager - command line control for the ULO camera (admin/setup mode and usage mode)

Usage:
  ulo <command> [arguments] --host <ip> --user <email> [--password <pass>]

  Connection settings are remembered between runs. Omit --host/--user/--password
  to reuse the previous values.

Network discovery (no credentials needed):
  discover               Scan the local network for ULO cameras (--timeout <s>)

Connection options (or use ULO_HOST / ULO_USER / ULO_PASSWORD):
  --host <ip|url>        Camera address, e.g. 192.168.0.10
  --user <email>         Account used to log in
  --password <pass>      Password (prompted when omitted)
  --https                Use https instead of http. The camera's certificate is
                         self-signed, so it is accepted automatically; add
                         --pin-cert to accept only the one you expect
  --pin-cert <sha1>      Require this certificate SHA-1 thumbprint over https
  --trace                Print every API call
  --quiet                Do not print the connection banner

Usage mode commands:
  status                 Everything about the camera in one screen
  watch                  Live view of what the camera is doing (Ctrl+C to stop)
                         Detects when the camera is turned upside down (admin/setup mode)
                         --interval <seconds> --state-interval <seconds> --lines <n>
  mode [standard|spy|alert]
                         Show or change the recording mode
  snapshot               Take a picture now and download it (--out <folder>)
                         The picture is not stored on the camera unless --store is used
  live                   Live video from the camera (fragmented MP4)
                         --play opens it in VLC/ffplay, otherwise it is recorded
                         --out <file.mp4> --seconds <n> --player <path to player>
  record [start|stop]    On-demand recording on the camera itself
  media [list|days|delete <day>]
                         Browse recordings (--type video|snapshot, --yes)
  download               Download recordings to a folder, network share or FTP server
                         --out <folder | \\server\share | ftp://host/path>
                         --dest-user <user> --dest-password <pass>   (share or FTP)
                         --type video|snapshot --age <hours> --retention <hours> --flat
  availability           Ping devices and optionally set the mode from the result
                         --hosts <a,b,c> --rule any|all
                         --if-up <mode> --if-down <mode> [--force]
                         Without --if-up/--if-down it only reports (exit code 0 = available)
  log [show|tail|save]   Read the camera log (--lines <n>, --interval <s>, --out <folder>)
  storage                Internal memory and SD card usage
  movetocard             Move recordings to the SD card (--wait)
  time [sync]            Show or synchronise the camera clock

Admin / setup mode commands (need an admin account):
  config [show [section]|set <section> <json>|name <name>|quality <q>]
  wifi [show|scan|connect <ssid> <password>]
  users [list|show <id>|add <email> --user-password <p> [--admin]|delete <id>]
  clean <period>         Delete recordings from the camera storage
                         OldestDay|OldestWeek|OldestYear|LatestDay|LatestWeek|LatestYear|All
  firmware               Firmware and over-the-air status
                         --download fetches an update, --install applies a downloaded one
  backup [list|create|restore <name>]
                         Settings backups stored on the camera
  reset                  Factory reset (asks for confirmation)
  api <path>             Call any endpoint directly (--method, --body)

Examples:
  ulo status --host 192.168.0.10 --user admin@example.com --password secret
  ulo watch --host 192.168.0.10 --user admin@example.com --interval 5
  ulo mode alert --host 192.168.0.10 --user admin@example.com
  ulo download --out D:\ulo\media --type video --age 24 --host 192.168.0.10 --user admin@example.com
  ulo api api/v1/state --host 192.168.0.10 --user admin@example.com
""");
    }
}
