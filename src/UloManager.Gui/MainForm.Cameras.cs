using UloManager.Core;

namespace UloManager.Gui;

/// <summary>
/// The camera list on the left. Every camera the user has stored credentials for keeps its own
/// session, so the list can show at a glance whether each one is reachable, what it calls itself,
/// which firmware it runs and which modes it is in. Only the selected camera produces live video,
/// pictures and log output - that is what "active" means throughout the form.
/// </summary>
public sealed partial class MainForm
{
    private readonly UloCameraPool _pool = new();

    private Control BuildCameraSidebar()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0, 0, 8, 0),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Without this the single column is sized to its widest child instead of to the panel, and
        // the list and the buttons spill out to the right where the tabs cut them off.
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.Controls.Add(
            new Label
            {
                Text = "Cameras",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(2, 0, 0, 4),
            },
            0,
            0);

        _cameraList = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 46,
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _cameraList.DrawItem += DrawCameraItem;
        _cameraList.SelectedIndexChanged += async (_, _) => await OnCameraSelectedAsync();
        panel.Controls.Add(_cameraList, 0, 1);

        // Three equal columns so the buttons never wrap, whatever the sidebar width. The row has a
        // fixed height because an auto-sized one clipped the captions at the bottom.
        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 30,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 0),
        };
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));

        var discover = new Button { Text = "Discover", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 3, 0) };
        discover.Click += async (_, _) => await DiscoverCamerasAsync();
        buttons.Controls.Add(discover, 0, 0);

        var add = new Button { Text = "Add...", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 3, 0) };
        add.Click += (_, _) => AddCameraByAddress();
        buttons.Controls.Add(add, 1, 0);

        var forget = new Button { Text = "Forget", Dock = DockStyle.Fill, Margin = new Padding(0) };
        forget.Click += async (_, _) => await ForgetSelectedCameraAsync();
        buttons.Controls.Add(forget, 2, 0);

        panel.Controls.Add(buttons, 0, 2);
        panel.Controls.Add(BuildCredentialsPanel(), 0, 3);

        // Hovering a camera explains what the row is showing, the same way the status bar dot does.
        _cameraList.MouseMove += (_, e) =>
        {
            var index = _cameraList.IndexFromPoint(e.Location);
            if (index == _cameraTooltipIndex)
            {
                return;
            }

            _cameraTooltipIndex = index;
            _toolTip.SetToolTip(
                _cameraList,
                index >= 0 && index < _cameraList.Items.Count && _cameraList.Items[index] is UloCamera hovered
                    ? DescribeCameraTooltip(hovered)
                    : "");
        };

        _pool.Changed += (_, _) => RefreshCameraList();
        return panel;
    }

    /// <summary>
    /// Credentials of the camera picked in the list. They belong to that camera, not to the
    /// application, so each camera can use its own account - which is what the firmware wants
    /// anyway, since it keeps only one session per account.
    /// </summary>
    private Control BuildCredentialsPanel()
    {
        _credentialsGroup = new GroupBox
        {
            Text = "Selected camera",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(6, 4, 6, 6),
            Margin = new Padding(0, 8, 0, 0),
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 5,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.MaximumSize = new Size(0, 0);

        grid.Controls.Add(new Label { Text = "Address", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 3) }, 0, 0);
        _addressLabel = new Label { Text = "-", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 3) };
        grid.Controls.Add(_addressLabel, 1, 0);

        grid.Controls.Add(new Label { Text = "User", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 3) }, 0, 1);
        _userBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "user@example.com", Margin = new Padding(0, 4, 0, 4) };
        _userBox.TextChanged += (_, _) => StoreCredentialsOnSelectedCamera();
        grid.Controls.Add(_userBox, 1, 1);

        grid.Controls.Add(new Label { Text = "Password", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 3) }, 0, 2);
        _passwordBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, Margin = new Padding(0, 4, 0, 4) };
        _passwordBox.TextChanged += (_, _) => StoreCredentialsOnSelectedCamera();
        grid.Controls.Add(_passwordBox, 1, 2);

        // HTTPS sits with the credentials, above the buttons: it is part of how the session is
        // opened, not a separate action. The camera's certificate is never validated (there is no
        // authority to validate it against), so this only protects against a passive observer.
        _httpsBox = new CheckBox
        {
            Text = "Use HTTPS",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 3, 0, 3),
        };
        _httpsBox.CheckedChanged += (_, _) =>
        {
            if (_suppressCameraSelection || _cameraList.SelectedItem is not UloCamera camera)
            {
                return;
            }

            camera.UseHttps = _httpsBox.Checked;

            // Remember it straight away - the setting must survive even if the camera is never
            // connected from this window again.
            SaveCameraCredentials();

            SetStatus(camera.IsConnected
                ? $"{camera.DisplayName}: press Reconnect to switch to {(camera.UseHttps ? "HTTPS" : "HTTP")}."
                : $"{camera.DisplayName} will use {(camera.UseHttps ? "HTTPS" : "plain HTTP")}.");
        };
        grid.Controls.Add(_httpsBox, 1, 3);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 30,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 2),
        };
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _connectButton = new Button { Text = "Connect", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 3, 0) };
        _connectButton.Click += async (_, _) => await ConnectSelectedCameraAsync();
        actions.Controls.Add(_connectButton, 0, 0);

        _disconnectButton = new Button { Text = "Disconnect", Dock = DockStyle.Fill, Margin = new Padding(0) };
        _disconnectButton.Click += async (_, _) => await DisconnectAsync();
        actions.Controls.Add(_disconnectButton, 1, 0);

        grid.Controls.Add(actions, 1, 4);

        // Enter in either field connects, so the flow stays on the keyboard.
        _userBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = ConnectSelectedCameraAsync(); } };
        _passwordBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = ConnectSelectedCameraAsync(); } };

        _credentialsGroup.Controls.Add(grid);
        return _credentialsGroup;
    }

    /// <summary>Adds a camera by address for the case where discovery cannot see it.</summary>
    private void AddCameraByAddress()
    {
        using var dialog = new Form
        {
            Text = "Add camera",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(340, 120),
        };

        var label = new Label
        {
            Text = "IP address or host name of the camera:",
            AutoSize = true,
            Location = new Point(12, 15),
        };

        var input = new TextBox { Location = new Point(12, 40), Width = 316 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(172, 78), Width = 75 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(253, 78), Width = 75 };

        dialog.Controls.AddRange(new Control[] { label, input, ok, cancel });
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        Theme.Apply(dialog);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var address = input.Text.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        if (_pool.Find(address) is not null)
        {
            SetStatus($"{address} is already in the list.");
        }

        var settings = _defaultSettings;
        var camera = _pool.Add(address, settings.UserName ?? "", settings.GetPassword());
        SaveCameraCredentials();
        RefreshCameraList();
        SelectCamera(camera);

        SetStatus($"{address} added - fill in the account and press Connect.");
        _userBox.Focus();
    }

    private void SelectCamera(UloCamera camera)
    {
        var index = _pool.Cameras.ToList().IndexOf(camera);
        if (index >= 0)
        {
            _cameraList.SelectedIndex = index;
        }
    }

    /// <summary>Keeps what is typed in the fields on the camera the list is pointing at.</summary>
    private void StoreCredentialsOnSelectedCamera()
    {
        if (_suppressCameraSelection || _cameraList.SelectedItem is not UloCamera camera)
        {
            return;
        }

        camera.UserName = _userBox.Text.Trim();
        camera.Password = _passwordBox.Text;
    }

    /// <summary>Shows the credentials of the camera picked in the list.</summary>
    private void ShowCredentialsFor(UloCamera? camera)
    {
        _suppressCameraSelection = true;
        try
        {
            _addressLabel.Text = camera?.Address ?? "-";
            _httpsBox.Checked = camera?.UseHttps == true;
            _userBox.Text = camera?.UserName ?? "";
            _passwordBox.Text = camera?.Password ?? "";
            _credentialsGroup.Text = camera is null
                ? "Selected camera"
                : $"Selected: {camera.DisplayName}";

            _connectButton.Enabled = camera is not null;
            _connectButton.Text = camera is { IsConnected: true } ? "Reconnect" : "Connect";
            _disconnectButton.Enabled = camera is { IsConnected: true };
            _userBox.Enabled = _passwordBox.Enabled = _httpsBox.Enabled = camera is not null;
        }
        finally
        {
            _suppressCameraSelection = false;
        }
    }

    /// <summary>Connects the camera picked in the list using the credentials in the panel.</summary>
    private async Task ConnectSelectedCameraAsync()
    {
        if (_cameraList.SelectedItem is not UloCamera camera)
        {
            SetStatus("Pick a camera in the list first, or add one with 'Add...'.");
            return;
        }

        camera.UserName = _userBox.Text.Trim();
        camera.Password = _passwordBox.Text;

        if (string.IsNullOrWhiteSpace(camera.UserName) || string.IsNullOrEmpty(camera.Password))
        {
            SetStatus($"Enter the account for {camera.DisplayName} first.");
            _userBox.Focus();
            return;
        }

        SaveCameraCredentials();

        try
        {
            // Already connected means the user changed something - the account, or the HTTPS tick -
            // so the session is torn down and opened again with what the panel now says.
            if (camera.IsConnected)
            {
                SetStatus($"Reconnecting to {camera.DisplayName}...");

                if (ReferenceEquals(camera, _pool.Active))
                {
                    await DetachActiveCameraAsync();
                    UpdateConnectionState(connected: false);
                }

                await camera.DisconnectAsync();
                RefreshCameraList();
            }

            SetStatus($"Connecting to {camera.DisplayName}...");
            RefreshCameraList();

            // The camera drops off Wi-Fi regularly and refuses connections while it is busy,
            // so a first failure is not worth bothering the user with.
            const int attempts = 3;
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                if (await camera.ConnectAsync(_cts.Token))
                {
                    break;
                }

                if (attempt == attempts)
                {
                    RefreshCameraList();
                    ShowCredentialsFor(camera);
                    ShowError("Connection failed", new InvalidOperationException(camera.LastError ?? "Unknown error."));
                    return;
                }

                SetStatus($"Connecting to {camera.DisplayName} (attempt {attempt + 1} of {attempts})...");
                await Task.Delay(TimeSpan.FromSeconds(3), _cts.Token);
            }

            SaveCameraCredentials();
            await ActivateCameraAsync(camera);
            _pool.StartMonitoring();
        }
        catch (OperationCanceledException)
        {
            // The window is closing or another camera was picked.
        }
        catch (Exception ex)
        {
            ShowError("Connection failed", ex);
        }
    }

    /// <summary>The long form shown on hover, mirroring the wording of the status bar indicator.</summary>
    private static string DescribeCameraTooltip(UloCamera camera)
    {
        var lines = new List<string> { $"{camera.DisplayName}  ({camera.Address})" };

        if (!string.IsNullOrEmpty(camera.DeviceId))
        {
            lines.Add($"Device ID: {camera.DeviceId}");
        }

        if (!string.IsNullOrWhiteSpace(camera.Firmware))
        {
            lines.Add($"Firmware: {camera.Firmware}");
        }

        if (!camera.IsConnected)
        {
            lines.Add(camera.IsConnecting ? "Connecting..." : "NOT CONNECTED");
            if (!string.IsNullOrWhiteSpace(camera.LastError))
            {
                lines.Add(camera.LastError);
            }

            return string.Join(Environment.NewLine, lines);
        }

        lines.Add(camera.DeviceMode == UloDeviceMode.Setup
            ? "CAMERA: SETUP MODE (upside down)"
            : "CAMERA: USAGE MODE (upright)");
        lines.Add($"Session: {(camera.IsAdmin ? "administrator" : "standard user")} - {camera.UserName}");

        if (camera.RecordingMode is { } mode)
        {
            lines.Add($"Recording mode: {mode.ToApiValue()}");
        }

        lines.Add($"Battery: {camera.BatteryLevel}% ({(camera.Plugged ? "plugged in" : "on battery")})");
        return string.Join(Environment.NewLine, lines);
    }

    // --------------------------------------------------------------- drawing

    private void DrawCameraItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _cameraList.Items.Count || _cameraList.Items[e.Index] is not UloCamera camera)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var active = ReferenceEquals(camera, _pool.Active);

        var background = selected
            ? Theme.Selection
            : active
                ? Theme.ActiveRow
                : Theme.ListBackground;

        using (var fill = new SolidBrush(background))
        {
            e.Graphics.FillRectangle(fill, e.Bounds);
        }

        var primary = selected ? Theme.SelectionText : Theme.PrimaryText;
        var secondary = selected ? Theme.SelectionText : Theme.SecondaryText;

        // Status dot: grey when there is no session, green when the camera is upright and watching,
        // purple when it is upside down in setup mode.
        var indicator = !camera.IsConnected
            ? DeviceIndicator.Offline
            : !string.IsNullOrWhiteSpace(camera.LastError)
                ? DeviceIndicator.Warning
                : camera.DeviceMode == UloDeviceMode.Setup
                    ? DeviceIndicator.Setup
                    : DeviceIndicator.Usage;

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var dot = new SolidBrush(IndicatorColour(indicator)))
        {
            e.Graphics.FillEllipse(dot, e.Bounds.Left + 8, e.Bounds.Top + 9, 10, 10);
        }

        if (camera.IsConnecting)
        {
            using var pen = new Pen(Color.Gray);
            e.Graphics.DrawEllipse(pen, e.Bounds.Left + 6, e.Bounds.Top + 7, 14, 14);
        }

        var textLeft = e.Bounds.Left + 26;
        var width = e.Bounds.Width - 30;

        using var titleFont = new Font(_cameraList.Font, FontStyle.Bold);
        using var detailFont = new Font(_cameraList.Font.FontFamily, 7.8F);
        using var titleBrush = new SolidBrush(primary);
        using var detailBrush = new SolidBrush(secondary);

        var title = camera.DisplayName;
        if (!string.IsNullOrEmpty(camera.DeviceId))
        {
            title += $"  ({camera.DeviceId})";
        }

        e.Graphics.DrawString(
            title,
            titleFont,
            titleBrush,
            new RectangleF(textLeft, e.Bounds.Top + 4, width, 16));

        e.Graphics.DrawString(
            DescribeCamera(camera),
            detailFont,
            detailBrush,
            new RectangleF(textLeft, e.Bounds.Top + 21, width, 14));

        e.Graphics.DrawString(
            DescribeCameraStorage(camera),
            detailFont,
            detailBrush,
            new RectangleF(textLeft, e.Bounds.Top + 32, width, 14));

        e.DrawFocusRectangle();
    }

    /// <summary>The second line of a camera row: firmware, session rights and both camera modes.</summary>
    private static string DescribeCamera(UloCamera camera)
    {
        if (!camera.IsConnected)
        {
            return camera.IsConnecting
                ? "connecting..."
                : string.IsNullOrWhiteSpace(camera.LastError)
                    ? "not connected"
                    : $"not connected - {Shorten(camera.LastError)}";
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(camera.Firmware))
        {
            parts.Add($"fw {camera.Firmware}");
        }

        parts.Add(camera.IsAdmin ? "admin" : "user");
        parts.Add(camera.DeviceMode == UloDeviceMode.Setup ? "setup" : "usage");

        if (camera.RecordingMode is { } mode)
        {
            parts.Add(mode.ToApiValue());
        }

        if (!string.IsNullOrWhiteSpace(camera.LastError))
        {
            parts.Add($"⚠ {Shorten(camera.LastError)}");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Third line of a camera row: where it lives and how full it is. The figure is for whichever
    /// volume the camera records to - the SD card when one is inserted, the internal memory when
    /// not - because that is the one that runs out and stops the recordings.
    /// </summary>
    private static string DescribeCameraStorage(UloCamera camera)
    {
        if (camera.ActiveVolume is not { TotalMb: > 0 } volume)
        {
            return camera.Address;
        }

        return $"{camera.Address}   ·   {(camera.Storage!.SdCard.Inserted ? "SD" : "int")} {volume.UsedPercent:0}% full";
    }

    private static string Shorten(string text)
        => text.Length <= 40 ? text : text[..40] + "...";

    private static Color IndicatorColour(DeviceIndicator indicator) => indicator switch
    {
        DeviceIndicator.Usage => Color.SeaGreen,
        DeviceIndicator.Setup => Color.MediumPurple,
        DeviceIndicator.Warning => Color.Orange,
        _ => Color.Gray,
    };

    // ------------------------------------------------------------ list state

    private void RefreshCameraList()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(RefreshCameraList);
            }
            catch (Exception)
            {
                // The window is closing.
            }

            return;
        }

        var selected = _cameraList.SelectedItem as UloCamera;

        _suppressCameraSelection = true;
        try
        {
            _cameraList.BeginUpdate();
            _cameraList.Items.Clear();
            foreach (var camera in _pool.Cameras)
            {
                _cameraList.Items.Add(camera);
            }

            var wanted = selected ?? _pool.Active;
            if (wanted is not null)
            {
                var index = _pool.Cameras.ToList().IndexOf(wanted);
                if (index >= 0)
                {
                    _cameraList.SelectedIndex = index;
                }
            }

            _cameraList.EndUpdate();
        }
        finally
        {
            _suppressCameraSelection = false;
        }
    }

    private async Task OnCameraSelectedAsync()
    {
        if (_suppressCameraSelection || _cameraList.SelectedItem is not UloCamera camera)
        {
            return;
        }

        ShowCredentialsFor(camera);

        // A camera that is not connected waits for its credentials and the Connect button; only a
        // live one is worth switching the tabs to.
        if (!camera.IsConnected)
        {
            SetStatus(string.IsNullOrWhiteSpace(camera.UserName)
                ? $"{camera.DisplayName}: enter the account and press Connect."
                : $"{camera.DisplayName} is not connected - press Connect.");
            return;
        }

        if (ReferenceEquals(camera, _pool.Active) && _device is not null)
        {
            return;
        }

        await ActivateCameraAsync(camera);
    }

    // ------------------------------------------------------------ activation

    /// <summary>
    /// Makes one camera the active one: the previous camera keeps its session but stops producing
    /// video, pictures and log output, and every tab is rebound to the new camera.
    ///
    /// <para>
    /// Switching is serialised. Clicking through the list quickly used to start several activations
    /// at once, and they fought over the same cancellation source, device field and tabs.
    /// A newer request now cancels the one in flight and waits for it to unwind first.
    /// </para>
    /// </summary>
    private async Task ActivateCameraAsync(UloCamera camera)
    {
        _activationRequest?.Cancel();

        await _activationGate.WaitAsync();

        using var request = new CancellationTokenSource();
        _activationRequest = request;

        try
        {
            await ActivateCameraCoreAsync(camera, request.Token);
        }
        catch (OperationCanceledException)
        {
            // Another camera was picked while this one was still loading.
        }
        catch (Exception ex)
        {
            SetStatus($"{camera.DisplayName}: {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_activationRequest, request))
            {
                _activationRequest = null;
            }

            _activationGate.Release();
        }
    }

    private async Task ActivateCameraCoreAsync(UloCamera camera, CancellationToken switching)
    {
        if (!camera.IsConnected)
        {
            SetStatus($"Connecting to {camera.DisplayName}...");
            if (!await camera.ConnectAsync(switching) || camera.Device is null)
            {
                SetStatus($"{camera.DisplayName}: {camera.LastError}");
                RefreshCameraList();
                return;
            }
        }

        switching.ThrowIfCancellationRequested();

        await DetachActiveCameraAsync();
        ClearCameraViews(camera.DisplayName);

        _pool.SetActive(camera);
        _device = camera.Device;
        _info = camera.Info;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(switching);

        if (_device is not null)
        {
            _recorder.Clear();
            _recorder.Attach(_device.Client);
        }

        UpdateConnectionState(connected: _device is not null);
        ShowCredentialsFor(camera);
        RefreshCameraList();

        // Nothing is trustworthy until each tab has its own data, so keep them locked while loading.
        SetTabsLoading();

        await TryRefreshAsync("Dashboard", RefreshDashboardAsync);
        MarkTabLoaded(_dashboardTab);
        switching.ThrowIfCancellationRequested();

        await TryRefreshAsync("Recordings", RefreshMediaAsync);
        MarkTabLoaded(_mediaTab);
        switching.ThrowIfCancellationRequested();

        await TryRefreshAsync("Eyes", () => LoadEyeSettingsAsync(_cts.Token));
        MarkTabLoaded(_eyesTab);
        switching.ThrowIfCancellationRequested();

        await TryRefreshAsync("Setup", LoadSetupAsync);
        MarkTabLoaded(_setupTab, camera.IsAdmin);
        switching.ThrowIfCancellationRequested();

        StartMonitoring();
        ApplySnapshotTimer();
        RefreshKnownEndpoints();
        MarkTabLoaded(_activityTab);
        MarkTabLoaded(_liveTab);
        MarkTabLoaded(_apiTab);

        try
        {
            await TakeSnapshotAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetStatus($"{camera.DisplayName}: the first picture failed - {ex.Message}");
        }

        SetStatus(_info is null
            ? $"Connected to {camera.DisplayName}."
            : $"Connected to {_info.DeviceName} - {_info.ModeSummary}.");

        RefreshCameraList();
    }

    /// <summary>
    /// Marks every tab as loading while the new camera is being read.
    ///
    /// <para>
    /// Only the controls that can actually do something - buttons, fields, lists to pick from - are
    /// disabled, so nothing can be applied to the wrong camera. Labels, group boxes and tables stay
    /// enabled on purpose: Windows draws disabled text in grey, which on a dark window is
    /// unreadable, and greying out the entire page made the whole tab illegible.
    /// </para>
    /// </summary>
    private void SetTabsLoading()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            Invoke(SetTabsLoading);
            return;
        }

        foreach (TabPage page in _tabs.TabPages)
        {
            var disabled = new List<Control>();
            DisableInputs(page, disabled);
            _loadingDisabled[page] = disabled;

            if (!page.Text.EndsWith(LoadingSuffix, StringComparison.Ordinal))
            {
                page.Text += LoadingSuffix;
            }
        }

        _tabs.Invalidate();
    }

    private static void DisableInputs(Control parent, List<Control> disabled)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Button)
            {
                if (child.Enabled)
                {
                    child.Enabled = false;
                    disabled.Add(child);
                }

                continue;
            }

            DisableInputs(child, disabled);
        }
    }

    /// <summary>Puts one tab back in working order once its own data has arrived.</summary>
    private void MarkTabLoaded(TabPage page, bool enabled = true)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            Invoke(() => MarkTabLoaded(page, enabled));
            return;
        }

        if (page.Text.EndsWith(LoadingSuffix, StringComparison.Ordinal))
        {
            page.Text = page.Text[..^LoadingSuffix.Length];
        }

        if (_loadingDisabled.TryGetValue(page, out var disabled))
        {
            foreach (var control in disabled)
            {
                // Only what this method switched off is switched back on, so a control that is
                // meant to stay disabled (a non-admin session, for instance) is left alone.
                control.Enabled = enabled;
            }

            _loadingDisabled.Remove(page);
        }

        page.Enabled = enabled;
        _tabs.Invalidate();
    }

    private readonly Dictionary<TabPage, List<Control>> _loadingDisabled = new();

    private const string LoadingSuffix = " ...";

    /// <summary>
    /// Empties every tab the moment a switch starts. Without this the previous camera's status,
    /// recordings, picture and settings stay on screen under the new camera's name until its own
    /// data arrives, which is both confusing and a good way to change the wrong camera's settings.
    /// </summary>
    private void ClearCameraViews(string cameraName)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            Invoke(() => ClearCameraViews(cameraName));
            return;
        }

        _statusView.Items.Clear();
        _statusView.Items.Add(new ListViewItem(new[] { "Loading", $"{cameraName}..." }));

        _snapshotBox.Image?.Dispose();
        _snapshotBox.Image = null;
        _snapshotGroup.Text = "What the camera sees";

        _mediaView.Items.Clear();
        _activityView.Items.Clear();
        _usersView.Items.Clear();

        // Setup / admin: blank every field so nothing can be saved to the wrong camera.
        _nameBox.Clear();
        _qualityBox.SelectedIndex = -1;
        _qualityBox.Text = "";
        _languageBox.Items.Clear();
        _languageBox.Text = "";
        _timeZoneBox.Text = "";
        _autoTimeBox.Checked = false;
        _ssidBox.Text = "";
        _wifiPasswordBox.Clear();
        _disableOnAppBox.Checked = false;
        _disableOnDoubleTapBox.Checked = false;
        _disableOnFaceBox.Checked = false;
        _exclusionTop.Value = 0;
        _exclusionLeft.Value = 0;
        _exclusionBottom.Value = 0;
        _exclusionRight.Value = 0;
        _exclusionReverse.Checked = false;
        _exclusionReset.Checked = false;
        _backupBox.Items.Clear();
        _firmwareLabel.Text = "";

        // Eyes tab
        _eyeHueSlider.Value = 0;
        _eyeHueLabel.Text = "0";
        _eyeIrisSpin.Value = 0;
        _eyePupilSpin.Value = 0;
        _eyeReflectionBox.SelectedIndex = -1;
        _behaviorsView.Items.Clear();

        // Voice commands
        _voiceStandard.Checked = false;
        _voiceAlert.Checked = false;
        _voiceSpy.Checked = false;
        _voiceBattery.Checked = false;
        _cmdGoToSleep.Checked = false;
        _cmdAlertOn.Checked = false;
        _cmdAlertOff.Checked = false;
        _cmdTakePicture.Checked = false;
        _cmdStartVideo.Checked = false;
        _cmdStopVideo.Checked = false;

        _responseBox.Clear();
    }

    /// <summary>Stops everything that belongs to the camera being left, without closing its session.</summary>
    private async Task DetachActiveCameraAsync()
    {
        _snapshotTimer.Enabled = false;

        await StopLiveAsync();

        if (_monitor is not null)
        {
            await _monitor.StopAsync();
            _monitor.Dispose();
            _monitor = null;
        }

        var previous = _cts;
        _cts = new CancellationTokenSource();

        try
        {
            await previous.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // Already gone.
        }

        previous.Dispose();

        if (_device is not null)
        {
            _recorder.Detach(_device.Client);
        }

        _device = null;
        _info = null;
    }

    // ------------------------------------------------------------- lifecycle

    /// <summary>Loads the stored cameras into the list without connecting yet.</summary>
    private void LoadCamerasFromSettings()
    {
        var settings = UloSettings.Load();

        foreach (var known in settings.KnownCameras)
        {
            if (string.IsNullOrWhiteSpace(known.Address))
            {
                continue;
            }

            var user = string.IsNullOrWhiteSpace(known.UserName) ? settings.UserName ?? "" : known.UserName;
            var password = string.IsNullOrEmpty(known.EncodedPassword) ? settings.GetPassword() : known.GetPassword();

            var camera = _pool.Add(known.Address, user, password);
            camera.Seed(known.DeviceName, known.DeviceId, known.FirmwareVersion);
            camera.UseHttps = known.UseHttps;
            camera.PinnedThumbprint = known.PinnedCertificateThumbprint;
        }

        RefreshCameraList();
    }

    /// <summary>Connects every stored camera at once and activates the first one that answers.</summary>
    private async Task ConnectAllCamerasAsync()
    {
        if (_pool.Cameras.Count == 0)
        {
            SetStatus("No cameras stored yet - use Discover, or type an address and press Connect.");
            return;
        }

        SetStatus($"Connecting to {_pool.Cameras.Count} camera(s)...");
        await _pool.ConnectAllAsync(_cts.Token);
        _pool.StartMonitoring();

        SaveCameraCredentials();
        RefreshCameraList();

        var connected = _pool.Cameras.Count(c => c.IsConnected);
        SetStatus($"{connected} of {_pool.Cameras.Count} camera(s) connected.");

        if (_device is null && _pool.Active is { IsConnected: true } active)
        {
            await ActivateCameraAsync(active);
        }
    }

    private async Task ForgetSelectedCameraAsync()
    {
        if (_cameraList.SelectedItem is not UloCamera camera)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Remove {camera.DisplayName} ({camera.Address}) from the list and forget its credentials?",
            "Forget camera",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        if (ReferenceEquals(camera, _pool.Active))
        {
            await DetachActiveCameraAsync();
            UpdateConnectionState(connected: false);
        }

        await _pool.RemoveAsync(camera);

        var settings = UloSettings.Load();
        settings.KnownCameras.RemoveAll(
            c => string.Equals(c.Address, camera.Address, StringComparison.OrdinalIgnoreCase));
        settings.Save();

        RefreshCameraList();

        if (_device is null && _pool.Cameras.FirstOrDefault(c => c.IsConnected) is { } next)
        {
            await ActivateCameraAsync(next);
        }
    }

    /// <summary>Writes the credentials and the last known identity of every camera back to disk.</summary>
    private void SaveCameraCredentials()
    {
        var settings = UloSettings.Load();

        foreach (var camera in _pool.Cameras)
        {
            var known = settings.KnownCameras.FirstOrDefault(
                c => string.Equals(c.Address, camera.Address, StringComparison.OrdinalIgnoreCase));

            if (known is null)
            {
                known = new UloSettings.KnownCamera { Address = camera.Address };
                settings.KnownCameras.Add(known);
            }

            if (!string.IsNullOrWhiteSpace(camera.UserName))
            {
                known.UserName = camera.UserName;
                known.SetPassword(camera.Password);
            }

            if (!string.IsNullOrWhiteSpace(camera.DeviceName))
            {
                known.DeviceName = camera.DeviceName;
            }

            if (!string.IsNullOrWhiteSpace(camera.DeviceId))
            {
                known.DeviceId = camera.DeviceId;
            }

            if (!string.IsNullOrWhiteSpace(camera.Firmware))
            {
                known.FirmwareVersion = camera.Firmware;
            }

            known.UseHttps = camera.UseHttps;
            known.PinnedCertificateThumbprint = camera.PinnedThumbprint;
        }

        settings.Save();
    }

    private ListBox _cameraList = null!;
    private GroupBox _credentialsGroup = null!;
    private CheckBox _httpsBox = null!;
    private Label _addressLabel = null!;
    private bool _suppressCameraSelection;
    private int _cameraTooltipIndex = -1;
    private readonly SemaphoreSlim _activationGate = new(1, 1);
    private CancellationTokenSource? _activationRequest;
}
