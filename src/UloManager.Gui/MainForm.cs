using System.ComponentModel;
using UloManager.Core;

namespace UloManager.Gui;

public sealed partial class MainForm : Form
{
    private UloDevice? _device;
    private UloActivityMonitor? _monitor;
    private UloConnectionInfo? _info;
    private CancellationTokenSource _cts = new();

    public MainForm(LaunchOptions? launch = null)
    {
        Text = "ULO Manager";
        Width = 1180;
        Height = 820;
        MinimumSize = new Size(980, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        Icon = LoadApplicationIcon();

        BuildLayout();
        LayoutRules.Normalise(this);
        Theme.Apply(this);
        LoadSavedSettings();
        LoadCamerasFromSettings();
        RefreshKnownEndpoints();
        UpdateConnectionState(connected: false);

        if (launch is not null)
        {
            // Command line arguments add (or update) a camera in the list, exactly like the
            // Add button does.
            if (!string.IsNullOrWhiteSpace(launch.Host))
            {
                var settings = UloSettings.Load();
                var camera = _pool.Add(
                    launch.Host,
                    string.IsNullOrWhiteSpace(launch.User) ? settings.UserName ?? "" : launch.User,
                    string.IsNullOrEmpty(launch.Password) ? settings.GetPassword() : launch.Password);

                RefreshCameraList();
                SelectCamera(camera);
            }

            if (!string.IsNullOrWhiteSpace(launch.Tab))
            {
                SelectTab(launch.Tab);
            }

            if (launch.AutoConnect)
            {
                Shown += async (_, _) =>
                {
                    await ConnectSelectedCameraAsync();

                    if (launch.StartLive && _device is not null)
                    {
                        await StartLiveAsync();
                    }
                };

                return;
            }
        }

        // Every stored camera is brought up on start so the list shows what is running where; only
        // the active one produces video, pictures and log output.
        Shown += async (_, _) => await ConnectAllCamerasAsync();
    }

    /// <summary>Selects a tab by a short name, e.g. "live" or "activity".</summary>
    private void SelectTab(string name)
    {
        var wanted = name.Trim().ToLowerInvariant();

        foreach (TabPage page in _tabs.TabPages)
        {
            if (page.Text.ToLowerInvariant().Replace(" / ", "").Replace(" ", "").StartsWith(wanted))
            {
                _tabs.SelectedTab = page;
                return;
            }
        }
    }

    /// <summary>The ULO icon shared with the original projects, embedded into this assembly.</summary>
    private static Icon? LoadApplicationIcon()
    {
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("UloManager.Gui.icon.ico");
            return stream is null ? null : new Icon(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private bool IsAdminSession => _device?.IsAdminSession == true;

    // ---------------------------------------------------------------- layout

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildContent(), 0, 0);
        root.Controls.Add(BuildStatusStrip(), 0, 1);

        Controls.Add(root);
    }

    /// <summary>
    /// The camera list next to the tabs of whichever camera is active. The sidebar is docked rather
    /// than given a fixed grid column: a column has to be guessed at, and the default margins of the
    /// controls inside pushed the list a dozen pixels past its edge, where the tabs cut it off.
    /// Docking makes the tabs take exactly what is left, whatever the sidebar ends up needing.
    /// </summary>
    private Control BuildContent()
    {
        var content = new Panel { Dock = DockStyle.Fill };

        var tabs = BuildTabs();
        tabs.Dock = DockStyle.Fill;

        var sidebar = BuildCameraSidebar();
        sidebar.Dock = DockStyle.Left;
        sidebar.Width = 258;

        // The fill control goes in first so the docked one keeps its width.
        content.Controls.Add(tabs);
        content.Controls.Add(sidebar);
        return content;
    }

    private Control BuildTabs()
    {
        _tabs = new ThemedTabControl { Dock = DockStyle.Fill };
        _tabs.TabPages.Add(_dashboardTab = BuildDashboardTab());
        _tabs.TabPages.Add(_liveTab = BuildLiveTab());
        _tabs.TabPages.Add(_activityTab = BuildActivityTab());
        _tabs.TabPages.Add(_mediaTab = BuildMediaTab());
        _tabs.TabPages.Add(_eyesTab = BuildEyesTab());
        _tabs.TabPages.Add(BuildSetupTab());
        _tabs.TabPages.Add(_apiTab = BuildApiTab());
        return _tabs;
    }

    private Control BuildStatusStrip()
    {
        _statusStrip = new StatusStrip { Dock = DockStyle.Fill, SizingGrip = false, ShowItemToolTips = true };
        _statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusStrip.Items.Add(_statusLabel);

        // Camera state at a glance: grey when offline, green when it is upright and watching,
        // purple when it is upside down in setup mode. The details are on the tooltip.
        _deviceModeLabel = new ToolStripStatusLabel
        {
            AutoToolTip = false,
            Alignment = ToolStripItemAlignment.Right,
            ImageAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 6, 0),
        };
        _statusStrip.Items.Add(_deviceModeLabel);

        // Theme override, next to the camera indicator: System (default), Light or Dark.
        var theme = new ToolStripDropDownButton("Theme")
        {
            Alignment = ToolStripItemAlignment.Right,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = "Colour theme of this application",
        };

        foreach (var mode in new[] { Theme.Mode.System, Theme.Mode.Light, Theme.Mode.Dark })
        {
            var item = new ToolStripMenuItem(mode.ToString())
            {
                Checked = Theme.Preference == mode,
                CheckOnClick = false,
            };
            item.Click += (_, _) => ApplyThemePreference(mode);
            theme.DropDownItems.Add(item);
        }

        theme.Text = $"Theme: {Theme.Preference}";
        _themeButton = theme;
        _statusStrip.Items.Add(theme);

        // Strips need the renderer set on the instance, not only on ToolStripManager.
        Theme.ApplyToolStrip(_statusStrip);

        ApplyDeviceModeIndicator(DeviceIndicator.Offline, "Not connected to a camera.");
        return _statusStrip;
    }

    /// <summary>
    /// Stores the colour preference. Windows Forms fixes its colour mode before the first window is
    /// created, so the change is offered as a restart rather than pretended to be live.
    /// </summary>
    private void ApplyThemePreference(Theme.Mode mode)
    {
        var settings = UloSettings.Load();
        settings.Theme = mode.ToString();
        settings.Save();

        foreach (ToolStripMenuItem item in _themeButton.DropDownItems)
        {
            item.Checked = item.Text == mode.ToString();
        }

        _themeButton.Text = $"Theme: {mode}";

        if (mode == Theme.Preference)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"The {mode.ToString().ToLowerInvariant()} theme is applied when the application starts. Restart it now?",
            "Theme",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            SetStatus($"Theme set to {mode} - it takes effect the next time the application starts.");
            return;
        }

        var executable = Environment.ProcessPath;
        if (executable is not null)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = true });
        }

        Close();
    }

    private enum DeviceIndicator
    {
        /// <summary>No session - grey.</summary>
        Offline,

        /// <summary>Connected, camera upright and watching - green.</summary>
        Usage,

        /// <summary>Connected, camera upside down in admin/setup mode - purple.</summary>
        Setup,

        /// <summary>Connected but refresh is failing (camera rebooting, network issue) - orange.</summary>
        Warning,
    }

    /// <summary>Paints the status bar dot and puts the full description on its tooltip.</summary>
    private void ApplyDeviceModeIndicator(DeviceIndicator indicator, string tooltip)
    {
        var colour = indicator switch
        {
            DeviceIndicator.Usage => Color.SeaGreen,
            DeviceIndicator.Setup => Color.MediumPurple,
            DeviceIndicator.Warning => Color.Orange,
            _ => Color.Gray,
        };

        if (_deviceModeIndicator != indicator || _deviceModeLabel.Image is null)
        {
            _deviceModeIndicator = indicator;
            var previous = _deviceModeLabel.Image;
            _deviceModeLabel.Image = CreateStatusDot(colour);
            previous?.Dispose();
        }

        _deviceModeLabel.ToolTipText = tooltip;
    }

    private static Image CreateStatusDot(Color colour)
    {
        var dot = new Bitmap(14, 14);
        using var graphics = Graphics.FromImage(dot);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(colour);
        using var edge = new Pen(ControlPaint.Dark(colour, 0.2f));
        graphics.FillEllipse(fill, 2, 2, 10, 10);
        graphics.DrawEllipse(edge, 2, 2, 10, 10);
        return dot;
    }

    // ------------------------------------------------------------ connection

    /// <summary>Runs one of the post-connect refreshes without letting it break the connection.</summary>
    private async Task TryRefreshAsync(string what, Func<Task> refresh)
    {
        try
        {
            await refresh();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetStatus($"{what} could not be loaded: {ex.Message}");
        }
    }

    /// <summary>Closes the session of the active camera. The other cameras stay connected.</summary>
    private async Task DisconnectAsync()
    {
        var camera = _pool.Active;

        await DetachActiveCameraAsync();

        if (camera is not null)
        {
            await camera.DisconnectAsync();
            SetStatus($"Disconnected from {camera.DisplayName}.");
        }
        else
        {
            SetStatus("Disconnected.");
        }

        UpdateConnectionState(connected: false);
        RefreshCameraList();
    }

    private void UpdateConnectionState(bool connected)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateConnectionState(connected));
            return;
        }

        _connectButton.Enabled = _cameraList.SelectedItem is UloCamera { IsConnected: false };
        _disconnectButton.Enabled = connected;

        // Only the controls that act on the camera are switched off while nothing is connected.
        // Disabling whole pages greys their labels too, which is unreadable on a dark window.
        foreach (TabPage page in _tabs.TabPages)
        {
            page.Enabled = true;
            SetInputsEnabled(page, connected);
        }

        if (connected && _info is not null)
        {
            SetInputsEnabled(_setupTab, _info.OperatingMode == UloOperatingMode.AdminSetup);
            ApplyDeviceMode(_info.State);
        }
        else
        {
            ApplyDeviceModeIndicator(DeviceIndicator.Offline, "Not connected to a camera.");
        }

        _tabs.Invalidate();
    }

    /// <summary>Enables or disables everything the user can act with inside a container.</summary>
    private static void SetInputsEnabled(Control parent, bool enabled)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Button)
            {
                child.Enabled = enabled;
                continue;
            }

            SetInputsEnabled(child, enabled);
        }
    }

    /// <summary>
    /// Reflects the mode the camera reports right now. ULO enters admin/setup mode when it is
    /// turned upside down and returns to usage mode when it is put back upright, so this is
    /// refreshed continuously while the app is connected.
    /// </summary>
    private void ApplyDeviceMode(UloState state)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ApplyDeviceMode(state));
            return;
        }

        var admin = _device?.IsAdminSession == true;
        var setup = state.DeviceMode == UloDeviceMode.Setup;
        var account = _info?.CurrentUser.Email ?? "";
        var camera = _info?.DeviceName ?? _pool.Active?.Address ?? "";

        ApplyDeviceModeIndicator(
            setup ? DeviceIndicator.Setup : DeviceIndicator.Usage,
            (setup ? "CAMERA: SETUP MODE (upside down)" : "CAMERA: USAGE MODE (upright)") +
            $"  |  {(admin ? "ADMIN" : "USER")}" +
            Environment.NewLine + $"Signed in as {account}" +
            (string.IsNullOrWhiteSpace(camera) ? "" : $" on {camera}"));

        _setupHintLabel.Text = admin
            ? setup
                ? "The camera is upside down and in setup mode - this is when its own app shows the administrator screens."
                : "Administrator session. The camera is upright (usage mode); turn it upside down to use its own setup screens."
            : "This account is a standard user. Sign in with an administrator account to change the setup.";
    }
    private UloDevice RequireDevice()
        => _device ?? throw new InvalidOperationException("Connect to the camera first.");

    // ------------------------------------------------------------ settings

    /// <summary>Loads the account remembered for cameras that do not have their own yet.</summary>
    private void LoadSavedSettings() => _defaultSettings = UloSettings.Load();

    private UloSettings _defaultSettings = new();

    // ------------------------------------------------------------ discovery

    private async Task DiscoverCamerasAsync()
    {
        SetStatus("Scanning local network for ULO cameras...");
        UseWaitCursor = true;

        try
        {
            var results = await Task.Run(() => UloDiscovery.ScanAsync(timeout: TimeSpan.FromSeconds(2)));

            if (results.Count == 0)
            {
                SetStatus("No ULO cameras found on the network.");
                MessageBox.Show(this,
                    "No ULO cameras were found on the local network.\n\n" +
                    "Make sure the camera is powered on, connected to Wi-Fi\n" +
                    "and on the same network as this computer.",
                    "Discovery", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Save discovered cameras.
            var settings = UloSettings.Load();
            foreach (var camera in results)
                settings.AddKnownCamera(camera);

            settings.Save();

            // Put everything found into the camera list. Cameras that already have credentials are
            // connected; a new one waits with a grey dot until its account is filled in.
            UloCamera? first = null;
            foreach (var camera in results)
            {
                var address = camera.Address.ToString();
                var known = settings.KnownCameras.FirstOrDefault(
                    c => string.Equals(c.Address, address, StringComparison.OrdinalIgnoreCase));

                var entry = _pool.Add(
                    address,
                    string.IsNullOrWhiteSpace(known?.UserName) ? settings.UserName ?? "" : known.UserName,
                    string.IsNullOrEmpty(known?.EncodedPassword) ? settings.GetPassword() : known.GetPassword());

                entry.Seed(known?.DeviceName, known?.DeviceId, known?.FirmwareVersion);
                first ??= entry;
            }

            RefreshCameraList();

            var withCredentials = _pool.Cameras.Count(c => !string.IsNullOrWhiteSpace(c.UserName) && !string.IsNullOrEmpty(c.Password));
            SetStatus(withCredentials == 0
                ? $"Found {results.Count} ULO camera(s) - pick one on the left and fill in its account."
                : $"Found {results.Count} ULO camera(s) - see the list on the left.");

            if (first is not null && _cameraList.SelectedItem is null)
            {
                SelectCamera(first);
            }

            await ConnectAllCamerasAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Discovery failed: {ex.Message}");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    // --------------------------------------------------------------- helpers

    /// <summary>
    /// Lets the last column of a list view take whatever width is left, so it keeps filling the
    /// window when it is resized or maximised instead of leaving an empty strip on the right.
    /// </summary>
    private static void FillLastColumn(ListView view)
    {
        void Apply()
        {
            if (view.Columns.Count == 0 || view.ClientSize.Width <= 0)
            {
                return;
            }

            var used = 0;
            for (var i = 0; i < view.Columns.Count - 1; i++)
            {
                used += view.Columns[i].Width;
            }

            var remaining = view.ClientSize.Width - used - 4;
            var last = view.Columns[view.Columns.Count - 1];

            if (remaining > 60 && last.Width != remaining)
            {
                last.Width = remaining;
            }
        }

        view.Resize += (_, _) => Apply();
        view.ColumnWidthChanged += (_, _) => Apply();
        view.HandleCreated += (_, _) => Apply();
        Apply();
    }

    private void SetStatus(string text)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => SetStatus(text));
            }
            catch (Exception)
            {
                // The window is closing.
            }

            return;
        }

        // Keep the bar to one line so the camera indicator on the right stays visible; the full
        // text (camera errors can be very long) goes to the tooltip.
        _statusLabel.Text = text.Length > 160 ? text[..160] + "..." : text;
        _statusLabel.ToolTipText = text;
    }

    private void ShowError(string caption, Exception ex)
    {
        var message = ex is UloApiException api && !string.IsNullOrWhiteSpace(api.ResponseBody)
            ? $"{ex.Message}{Environment.NewLine}{Environment.NewLine}{api.ResponseBody}"
            : ex.Message;

        SetStatus(ex.Message);
        MessageBox.Show(this, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>Runs a camera operation, keeping the UI responsive and reporting failures.</summary>
    private async Task RunAsync(string description, Func<CancellationToken, Task> action)
    {
        try
        {
            SetStatus(description + "...");
            UseWaitCursor = true;
            await action(_cts.Token);
            SetStatus(description + " - done.");
        }
        catch (OperationCanceledException)
        {
            SetStatus(description + " - cancelled.");
        }
        catch (Exception ex)
        {
            ShowError(description + " failed", ex);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    /// <summary>
    /// Shutting down has to stay asynchronous: blocking the UI thread here would deadlock,
    /// because the background loops finish their work by posting back to this thread.
    /// The first close request cancels itself, cleans up and then closes for real.
    /// </summary>
    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if (_shutdownStarted)
        {
            base.OnFormClosing(e);
            return;
        }

        _shutdownStarted = true;
        e.Cancel = true;

        _snapshotTimer.Enabled = false;
        Enabled = false;
        SetStatus("Closing...");

        // A sleeping or unplugged camera must never keep the window alive.
        var cleanup = Task.Run(async () =>
        {
            await StopLiveAsync().ConfigureAwait(false);
            await DetachActiveCameraAsync().ConfigureAwait(false);
            await _pool.StopAsync().ConfigureAwait(false);
        });

        await Task.WhenAny(cleanup, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(true);

        base.OnFormClosing(e);
        Close();
    }

    private bool _shutdownStarted;

    // Controls created in BuildLayout
    private TextBox _userBox = null!;
    private TextBox _passwordBox = null!;
    private Button _connectButton = null!;
    private Button _disconnectButton = null!;
    private ToolStripStatusLabel _deviceModeLabel = null!;
    private ToolStripDropDownButton _themeButton = null!;
    private DeviceIndicator _deviceModeIndicator = DeviceIndicator.Offline;
    private TabPage _dashboardTab = null!;
    private TabPage _liveTab = null!;
    private TabPage _activityTab = null!;
    private TabPage _mediaTab = null!;
    private TabPage _apiTab = null!;
    private TabControl _tabs = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private readonly ToolTip _toolTip = new();

    /// <summary>Keeps the payloads this application sends, so the API console can reuse them.</summary>
    private readonly UloCallRecorder _recorder = new();
}
