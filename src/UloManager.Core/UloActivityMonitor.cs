namespace UloManager.Core;

public enum UloActivityKind
{
    Connected,
    Disconnected,
    Log,
    DeviceModeChanged,
    ModeChanged,
    PowerChanged,
    BatteryChanged,
    StorageChanged,
    NewRecording,
    PushEvent,
    Error,
}

public sealed class UloActivityEventArgs : EventArgs
{
    public required UloActivityKind Kind { get; init; }

    public required string Message { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public UloLogSeverity Severity { get; init; } = UloLogSeverity.Info;

    public UloLogEntry? LogEntry { get; init; }

    public UloDeviceSnapshot? Snapshot { get; init; }

    /// <summary>Camera state at the moment of the event, when one was read.</summary>
    public UloState? State { get; init; }

    public override string ToString() => $"[{Kind}] {Message}";
}

/// <summary>Everything the dashboard needs about the camera at one point in time.</summary>
public sealed record UloDeviceSnapshot(
    DateTimeOffset TakenAtUtc,
    UloMode Mode,
    UloState State,
    UloStorageStats Storage,
    DateTime DeviceTime,
    int MediaFileCount,
    bool BackupRunning)
{
    public UloDeviceMode DeviceMode => State.DeviceMode;

    public string Summary =>
        $"{(DeviceMode == UloDeviceMode.Setup ? "setup mode (upside down)" : "usage mode")}, " +
        $"recording={Mode.ToApiValue()}, battery={State.BatteryLevel}%, " +
        $"{(State.Plugged ? "plugged" : "on battery")}, internal free={Storage.Internal.FreeMb} MB, files={MediaFileCount}";
}

/// <summary>
/// Polls the camera and reports what changes, merging device state with the system log
/// and the real-time push channel.
/// <para>
/// Two loops run side by side: a fast one that only reads <c>/api/v1/state</c> - which is how the
/// camera reports whether it stands upright (usage mode) or upside down (admin/setup mode),
/// something the user can change at any moment - and a slower one that collects storage,
/// recordings and the log.
/// </para>
/// </summary>
public sealed class UloActivityMonitor : IDisposable
{
    private readonly UloDevice _device;
    private readonly UloEventStream _events;
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private Task? _stateWorker;
    private UloDeviceSnapshot? _previous;
    private UloState? _previousState;
    private UloMode? _previousMode;

    public UloActivityMonitor(UloDevice device)
    {
        _device = device;
        _events = new UloEventStream(device.Client);
        _events.EventReceived += OnPushEvent;
        _events.ConnectionChanged += (_, message) => Raise(UloActivityKind.PushEvent, message, UloLogSeverity.Info);
    }

    /// <summary>How often the full picture (storage, recordings, log) is collected.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How often the camera state is checked. The camera switches between admin/setup mode and
    /// usage mode when it is turned upside down, so this needs to stay short.
    /// </summary>
    public TimeSpan StatePollInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>How many log lines to replay when monitoring starts.</summary>
    public int InitialLogLines { get; set; } = 20;

    /// <summary>How many polls pass between two full recording listings.</summary>
    private const int MediaCountEveryNthPoll = 6;

    /// <summary>
    /// Subscribe to the camera push channel (WebSocket) for instant events.
    /// Polling still runs, because orientation, battery and storage are not always pushed.
    /// </summary>
    public bool UseRealtimeEvents { get; set; } = true;

    public bool IsRunning => _worker is { IsCompleted: false };

    public bool IsRealtimeConnected => _events.IsConnected;

    public UloDeviceSnapshot? Latest => _previous;

    /// <summary>Last known camera mode (setup while upside down, usage while upright).</summary>
    public UloDeviceMode? DeviceMode => _previousState?.DeviceMode;

    public event EventHandler<UloActivityEventArgs>? Activity;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _device.Log.ResetTail();
        _worker = RunAsync(_cts.Token);
        _stateWorker = WatchStateAsync(_cts.Token);

        if (UseRealtimeEvents)
        {
            _events.Start();
        }
    }

    public async Task StopAsync()
    {
        await _events.StopAsync().ConfigureAwait(false);

        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            var tasks = new[] { _worker, _stateWorker }.Where(task => task is not null).Cast<Task>().ToArray();
            if (tasks.Length > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _worker = null;
            _stateWorker = null;
        }
    }

    // ------------------------------------------------------- fast state watch

    private async Task WatchStateAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var state = await _device.GetStateAsync(ct).ConfigureAwait(false);
                var mode = await _device.GetModeAsync(ct).ConfigureAwait(false);

                if (_previousState is not null)
                {
                    CompareState(_previousState, state);
                }

                if (_previousMode is not null && _previousMode != mode)
                {
                    Raise(
                        UloActivityKind.ModeChanged,
                        $"Recording mode changed from {_previousMode.Value.ToApiValue()} to {mode.ToApiValue()}.",
                        UloLogSeverity.Notice,
                        state: state);
                }

                _previousState = state;
                _previousMode = mode;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Raise(UloActivityKind.Error, $"State check failed: {ex.Message}", UloLogSeverity.Warning);
            }

            try
            {
                await Task.Delay(StatePollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void CompareState(UloState before, UloState now)
    {
        if (before.DeviceMode != now.DeviceMode)
        {
            Raise(
                UloActivityKind.DeviceModeChanged,
                now.DeviceMode == UloDeviceMode.Setup
                    ? "Camera turned UPSIDE DOWN - it switched to admin / setup mode."
                    : "Camera stood back UP - it switched to usage mode.",
                UloLogSeverity.Notice,
                state: now);
        }

        if (before.Plugged != now.Plugged)
        {
            Raise(
                UloActivityKind.PowerChanged,
                now.Plugged ? "Power cable connected." : "Power cable removed - running on battery.",
                now.Plugged ? UloLogSeverity.Info : UloLogSeverity.Warning,
                state: now);
        }

        if (before.BatteryLevel != now.BatteryLevel)
        {
            Raise(
                UloActivityKind.BatteryChanged,
                $"Battery {before.BatteryLevel}% -> {now.BatteryLevel}%.",
                now.BatteryLevel <= 20 && !now.Plugged ? UloLogSeverity.Warning : UloLogSeverity.Info,
                state: now);
        }
    }

    // -------------------------------------------------------------- full poll

    private async Task RunAsync(CancellationToken ct)
    {
        var firstPass = true;
        var poll = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Listing every recording is expensive, so refresh it about once a minute instead of
                // on every poll; new recordings are also announced on the push channel.
                var refreshMedia = firstPass || poll % MediaCountEveryNthPoll == 0;
                var snapshot = await CaptureAsync(refreshMedia, ct).ConfigureAwait(false);
                poll++;

                if (firstPass)
                {
                    Raise(
                        UloActivityKind.Connected,
                        $"Connected to camera: {snapshot.Summary}",
                        UloLogSeverity.Notice,
                        snapshot: snapshot);
                }
                else
                {
                    CompareSnapshot(_previous!, snapshot);
                }

                _previous = snapshot;

                foreach (var entry in await _device.Log.GetNewEntriesAsync(firstPass ? InitialLogLines : 0, ct).ConfigureAwait(false))
                {
                    Raise(UloActivityKind.Log, entry.Activity ?? entry.Message, entry.Severity, entry);
                }

                firstPass = false;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Raise(UloActivityKind.Error, $"Polling failed: {ex.Message}", UloLogSeverity.Error);
            }

            try
            {
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Raise(UloActivityKind.Disconnected, "Monitoring stopped.", UloLogSeverity.Notice);
    }

    public async Task<UloDeviceSnapshot> CaptureAsync(CancellationToken ct = default)
        => await CaptureAsync(refreshMediaCount: true, ct).ConfigureAwait(false);

    /// <summary>
    /// Collects the slow moving picture of the camera.
    /// </summary>
    /// <param name="refreshMediaCount">
    /// Listing every recording is by far the most expensive call the camera serves - several seconds
    /// and hundreds of kilobytes once a few thousand files have accumulated, which is enough to make
    /// the polling loop time out and to disturb the camera itself. The repeated polls therefore reuse
    /// the previous count and only refresh it now and then.
    /// </param>
    private async Task<UloDeviceSnapshot> CaptureAsync(bool refreshMediaCount, CancellationToken ct)
    {
        var mode = await _device.GetModeAsync(ct).ConfigureAwait(false);
        var state = await _device.GetStateAsync(ct).ConfigureAwait(false);
        var storage = await _device.GetStorageAsync(ct).ConfigureAwait(false);
        var time = await _device.GetDeviceTimeAsync(ct).ConfigureAwait(false);
        var backup = await _device.IsMoveToCardRunningAsync(ct).ConfigureAwait(false);

        var mediaCount = _previous?.MediaFileCount ?? 0;
        if (refreshMediaCount || _previous is null)
        {
            mediaCount = (await _device.Media.ListAsync(UloMediaType.Video, ct).ConfigureAwait(false)).Count;
        }

        return new UloDeviceSnapshot(DateTimeOffset.UtcNow, mode, state, storage, time, mediaCount, backup);
    }

    /// <summary>
    /// Only slow moving values are compared here - orientation, power, battery and the recording
    /// mode are handled by the fast state watch so they are reported without delay.
    /// </summary>
    private void CompareSnapshot(UloDeviceSnapshot before, UloDeviceSnapshot now)
    {
        if (now.MediaFileCount > before.MediaFileCount)
        {
            Raise(
                UloActivityKind.NewRecording,
                $"{now.MediaFileCount - before.MediaFileCount} new recording(s) on the camera.",
                UloLogSeverity.Notice,
                snapshot: now);
        }

        if (Math.Abs(before.Storage.Internal.FreeMb - now.Storage.Internal.FreeMb) >= 5)
        {
            Raise(
                UloActivityKind.StorageChanged,
                $"Internal free space {before.Storage.Internal.FreeMb} MB -> {now.Storage.Internal.FreeMb} MB.",
                now.Storage.Internal.FreeMb < 100 ? UloLogSeverity.Warning : UloLogSeverity.Info,
                snapshot: now);
        }

        if (before.BackupRunning != now.BackupRunning)
        {
            Raise(
                UloActivityKind.StorageChanged,
                now.BackupRunning
                    ? "Move to SD card started - the camera cannot record now."
                    : "Move to SD card finished.",
                UloLogSeverity.Notice,
                snapshot: now);
        }
    }

    // ------------------------------------------------------------ push events

    private void OnPushEvent(object? sender, UloEventArgs e)
    {
        var (message, severity) = DescribePush(e);
        var kind = UloActivityKind.PushEvent;

        // A pushed orientation change is the fastest signal that the camera moved between
        // admin/setup mode and usage mode - reflect it before the next state poll arrives.
        if (e.Event.Equals("state:config", StringComparison.OrdinalIgnoreCase) && _previousState is not null)
        {
            var flipped = IsTrue(e.Data?.Trim('"'));
            if (_previousState.InSetupMode != flipped)
            {
                _previousState.InSetupMode = flipped;
                kind = UloActivityKind.DeviceModeChanged;
            }
        }

        Raise(kind, message, severity, state: kind == UloActivityKind.DeviceModeChanged ? _previousState : null);
    }

    /// <summary>
    /// Turns a pushed camera event into something readable.
    /// The camera sends its state changes as <c>state:&lt;field&gt;</c>, for example
    /// <c>{"event":"state:mode","data":"spy"}</c> or <c>state:config</c> when it is turned over.
    /// </summary>
    private static (string Message, UloLogSeverity Severity) DescribePush(UloEventArgs e)
    {
        var name = e.Event.ToLowerInvariant();
        var field = name.StartsWith("state:", StringComparison.Ordinal) ? name[6..] : name;
        var data = e.Data?.Trim('"');

        return field switch
        {
            "failure" => ($"Camera refused the session: {data}", UloLogSeverity.Error),
            "config" => (IsTrue(data)
                ? "Camera turned UPSIDE DOWN - it switched to admin / setup mode."
                : "Camera stood back UP - it switched to usage mode.", UloLogSeverity.Notice),
            "mode" => ($"Recording mode is now '{data}'.", UloLogSeverity.Notice),
            "plugged" => (IsTrue(data) ? "Power cable connected." : "Power cable removed.", UloLogSeverity.Info),
            "batterylevel" or "battery" => ($"Battery level {data}%.", UloLogSeverity.Info),
            "firmwarestatus" => ($"Firmware status '{data}'.", UloLogSeverity.Info),
            "movement" or "motion" => ("Movement detected by the camera.", UloLogSeverity.Notice),
            "displacement" or "orientation" => ("The camera was moved or turned over.", UloLogSeverity.Warning),
            "video" or "record" => ($"Recording event: {data}", UloLogSeverity.Notice),
            _ => (data is null ? $"Camera event '{e.Event}'." : $"Camera event '{e.Event}': {data}", UloLogSeverity.Info),
        };
    }

    private static bool IsTrue(string? value)
        => value is not null &&
           (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private void Raise(
        UloActivityKind kind,
        string message,
        UloLogSeverity severity,
        UloLogEntry? entry = null,
        UloDeviceSnapshot? snapshot = null,
        UloState? state = null)
    {
        Activity?.Invoke(this, new UloActivityEventArgs
        {
            Kind = kind,
            Message = message,
            Severity = severity,
            LogEntry = entry,
            Snapshot = snapshot,
            State = state ?? snapshot?.State,
        });
    }

    public void Dispose()
    {
        try
        {
            // Never block a caller forever: a sleeping camera can keep a poll pending.
            if (!StopAsync().Wait(TimeSpan.FromSeconds(5)))
            {
                _cts?.Cancel();
            }
        }
        catch (Exception)
        {
            // Nothing useful to do while disposing.
        }
    }
}
