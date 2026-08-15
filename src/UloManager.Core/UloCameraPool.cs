namespace UloManager.Core;

/// <summary>
/// One configured camera and everything the user interface needs to show about it: whether the
/// session is up, what the camera calls itself, and which modes it is running.
///
/// <para>
/// Every camera in the pool keeps its own session, but only the camera the user selected is asked
/// for the expensive things (live video, pictures, log). The rest are polled with the two cheapest
/// calls the firmware has - <c>GET /api/v1/state</c> and <c>GET /api/v1/mode</c> - so a wall of
/// cameras costs almost nothing.
/// </para>
/// </summary>
public sealed class UloCamera : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public UloCamera(string address, string userName, string password)
    {
        Address = address;
        UserName = userName;
        Password = password;
    }

    public string Address { get; }

    public string UserName { get; set; }

    public string Password { get; set; }

    /// <summary>
    /// Talk to this camera over HTTPS. The device presents a certificate that no chain check can
    /// pass (it is issued to <c>CN=localhost</c> on 06.0601 and to <c>CN=*.ulo.camera</c> by the
    /// vendor's own CA on 10.1308), so the certificate is accepted on trust unless a thumbprint is
    /// pinned as well.
    /// </summary>
    public bool UseHttps { get; set; }

    /// <summary>SHA-1 thumbprint the camera's certificate must match, or null to accept any.</summary>
    public string? PinnedThumbprint { get; set; }

    /// <summary>Live session, or null while the camera is not connected.</summary>
    public UloDevice? Device { get; private set; }

    public UloConnectionInfo? Info { get; private set; }

    public bool IsConnected => Device is not null;

    /// <summary>True while a connection attempt is in flight.</summary>
    public bool IsConnecting { get; private set; }

    /// <summary>Why the last connection attempt failed, or null when it succeeded.</summary>
    public string? LastError { get; private set; }

    public string DeviceName { get; private set; } = "";

    public string DeviceId { get; private set; } = "";

    public string Firmware { get; private set; } = "";

    /// <summary>Recording mode: standard, spy or alert.</summary>
    public UloMode? RecordingMode { get; private set; }

    /// <summary>Usage (upright) or setup (upside down).</summary>
    public UloDeviceMode? DeviceMode { get; private set; }

    /// <summary>True when the session may use the administrator surface.</summary>
    public bool IsAdmin { get; private set; }

    public int BatteryLevel { get; private set; }

    public bool Plugged { get; private set; }

    /// <summary>
    /// Storage as last polled. The SD card is what matters when one is inserted, because the camera
    /// records to it; otherwise the internal memory is the one that fills up.
    /// </summary>
    public UloStorageStats? Storage { get; private set; }

    /// <summary>The volume the camera is actually filling: the SD card when present, else internal.</summary>
    public UloStorageArea? ActiveVolume =>
        Storage is null ? null : Storage.SdCard.Inserted ? Storage.SdCard : Storage.Internal;

    /// <summary>"SD" or "internal", matching <see cref="ActiveVolume"/>.</summary>
    public string ActiveVolumeName => Storage?.SdCard.Inserted == true ? "SD" : "internal";

    /// <summary>Raised whenever anything shown in the camera list changed.</summary>
    public event EventHandler? StatusChanged;

    /// <summary>Name to show in the list, falling back to the address until the camera answers.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(DeviceName) ? Address : DeviceName;

    public void Seed(string? deviceName, string? deviceId, string? firmware)
    {
        DeviceName = deviceName ?? DeviceName;
        DeviceId = deviceId ?? DeviceId;
        Firmware = firmware ?? Firmware;
    }

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Device is not null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrEmpty(Password))
            {
                LastError = "No credentials stored for this camera.";
                Raise();
                return false;
            }

            IsConnecting = true;
            LastError = null;
            Raise();

            UloDevice? device = null;
            try
            {
                device = new UloDevice(new UloConnectionOptions
                {
                    Host = Address,
                    UserName = UserName,
                    Password = Password,
                    UseHttps = UseHttps,
                    AcceptDeviceCertificate = UseHttps,
                    PinnedCertificateThumbprint = UseHttps ? PinnedThumbprint : null,
                });

                var info = await device.ConnectAsync(ct).ConfigureAwait(false);

                Device = device;
                Info = info;
                DeviceName = info.DeviceName;
                Firmware = info.FirmwareVersion;
                if (!string.IsNullOrEmpty(info.DeviceId))
                {
                    DeviceId = info.DeviceId;
                }

                IsAdmin = info.OperatingMode == UloOperatingMode.AdminSetup;
                DeviceMode = info.State.DeviceMode;
                BatteryLevel = info.State.BatteryLevel;
                Plugged = info.State.Plugged;
                RecordingMode = await device.GetModeAsync(ct).ConfigureAwait(false);

                try
                {
                    Storage = await device.GetStorageAsync(ct).ConfigureAwait(false);
                }
                catch (UloApiException)
                {
                    // Not fatal - the list simply shows no storage figure for this camera.
                }

                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                device?.Dispose();
                LastError = ex.Message;
                return false;
            }
            finally
            {
                IsConnecting = false;
                Raise();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var device = Device;
            Device = null;
            Info = null;
            RecordingMode = null;
            DeviceMode = null;

            if (device is not null)
            {
                try
                {
                    await device.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Logging out is best effort.
                }

                device.Dispose();
            }
        }
        finally
        {
            _gate.Release();
            Raise();
        }
    }

    /// <summary>
    /// Refreshes only what the camera list shows. Two small calls, so this can run for every camera
    /// on a short timer without disturbing the camera or the active session.
    /// </summary>
    public async Task RefreshStatusAsync(CancellationToken ct = default)
    {
        var device = Device;
        if (device is null)
        {
            return;
        }

        try
        {
            var state = await device.GetStateAsync(ct).ConfigureAwait(false);
            var mode = await device.GetModeAsync(ct).ConfigureAwait(false);
            var storage = await device.GetStorageAsync(ct).ConfigureAwait(false);

            var previousUsed = ActiveVolume?.UsedPercent;

            var changed =
                DeviceMode != state.DeviceMode ||
                RecordingMode != mode ||
                BatteryLevel != state.BatteryLevel ||
                Plugged != state.Plugged ||
                LastError is not null;

            DeviceMode = state.DeviceMode;
            RecordingMode = mode;
            BatteryLevel = state.BatteryLevel;
            Plugged = state.Plugged;
            Storage = storage;
            LastError = null;

            if (changed || Math.Abs((ActiveVolume?.UsedPercent ?? 0) - (previousUsed ?? -1)) >= 0.1)
            {
                Raise();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The camera reboots on its own and drops off Wi-Fi; report it in the list and let the
            // pool retry rather than tearing the session down here.
            LastError = ex.Message;
            Raise();
        }
    }

    private void Raise() => StatusChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        Device?.Dispose();
        Device = null;
        _gate.Dispose();
    }
}

/// <summary>
/// Keeps a session open on every camera the user has stored credentials for and tracks which one is
/// the active camera - the only one that produces live video, pictures and log output.
/// </summary>
public sealed class UloCameraPool : IDisposable
{
    private readonly List<UloCamera> _cameras = new();
    private CancellationTokenSource? _cts;
    private Task? _poller;

    public IReadOnlyList<UloCamera> Cameras => _cameras;

    /// <summary>The camera the user is looking at, or null when none is connected.</summary>
    public UloCamera? Active { get; private set; }

    /// <summary>How often the camera list is refreshed.</summary>
    public TimeSpan StatusInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>How long to wait before trying a camera that refused or timed out again.</summary>
    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Raised when a camera is added, removed, or changes anything it shows.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the active camera changes.</summary>
    public event EventHandler? ActiveChanged;

    public UloCamera Add(string address, string userName, string password)
    {
        var existing = Find(address);
        if (existing is not null)
        {
            existing.UserName = userName;
            existing.Password = password;
            return existing;
        }

        var camera = new UloCamera(address, userName, password);
        camera.StatusChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        _cameras.Add(camera);
        Changed?.Invoke(this, EventArgs.Empty);
        return camera;
    }

    public UloCamera? Find(string address)
        => _cameras.FirstOrDefault(c => string.Equals(c.Address, address, StringComparison.OrdinalIgnoreCase));

    public async Task RemoveAsync(UloCamera camera)
    {
        _cameras.Remove(camera);

        if (Active == camera)
        {
            Active = _cameras.FirstOrDefault(c => c.IsConnected);
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        await camera.DisconnectAsync().ConfigureAwait(false);
        camera.Dispose();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Connects every camera at once and makes the first one that answers active.</summary>
    public async Task ConnectAllAsync(CancellationToken ct = default)
    {
        await Task.WhenAll(_cameras.Select(c => c.ConnectAsync(ct))).ConfigureAwait(false);

        if (Active is null or { IsConnected: false })
        {
            SetActive(_cameras.FirstOrDefault(c => c.IsConnected));
        }
    }

    public void SetActive(UloCamera? camera)
    {
        if (ReferenceEquals(Active, camera))
        {
            return;
        }

        Active = camera;
        ActiveChanged?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Starts the background refresh of the camera list.</summary>
    public void StartMonitoring()
    {
        if (_poller is { IsCompleted: false })
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _poller = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);

            try
            {
                if (_poller is not null)
                {
                    await _poller.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected while shutting down.
            }

            _cts.Dispose();
            _cts = null;
            _poller = null;
        }

        foreach (var camera in _cameras.ToList())
        {
            await camera.DisconnectAsync().ConfigureAwait(false);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var lastAttempt = new Dictionary<UloCamera, DateTimeOffset>();

        while (!ct.IsCancellationRequested)
        {
            foreach (var camera in _cameras.ToList())
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    if (camera.IsConnected)
                    {
                        await camera.RefreshStatusAsync(ct).ConfigureAwait(false);
                    }
                    else if (!camera.IsConnecting &&
                             (!lastAttempt.TryGetValue(camera, out var when) ||
                              DateTimeOffset.UtcNow - when > ReconnectInterval))
                    {
                        lastAttempt[camera] = DateTimeOffset.UtcNow;
                        await camera.ConnectAsync(ct).ConfigureAwait(false);

                        if (camera.IsConnected && Active is null)
                        {
                            SetActive(camera);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    // Never let one unhappy camera stop the others.
                }
            }

            try
            {
                await Task.Delay(StatusInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        foreach (var camera in _cameras)
        {
            camera.Dispose();
        }

        _cameras.Clear();
    }
}
