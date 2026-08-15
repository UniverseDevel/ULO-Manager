using System.Text.Json;
using UloManager.Core;

namespace UloManager.Gui;

public sealed partial class MainForm
{
    private TabPage BuildSetupTab()
    {
        _setupTab = new TabPage("Setup / Admin") { Padding = new Padding(10), AutoScroll = true };

        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
        };

        _setupHintLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            Margin = new Padding(0, 0, 0, 8),
            Text = "Connect with an administrator account to change the setup.",
        };
        root.Controls.Add(_setupHintLabel);

        root.Controls.Add(BuildGeneralGroup());
        root.Controls.Add(BuildWifiGroup());
        root.Controls.Add(BuildDetectionGroup());
        root.Controls.Add(BuildUsersGroup());
        root.Controls.Add(BuildMaintenanceGroup());

        _setupTab.Controls.Add(root);
        return _setupTab;
    }

    private Control BuildGeneralGroup()
    {
        var group = NewGroup("General", 250);
        var grid = NewGrid();

        grid.Controls.Add(NewLabel("Camera name"), 0, 0);
        _nameBox = new TextBox { Width = 220 };
        grid.Controls.Add(_nameBox, 1, 0);

        grid.Controls.Add(NewLabel("Video quality"), 0, 1);
        _qualityBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        _qualityBox.Items.AddRange(new object[] { "480p", "720p", "1080p" });
        grid.Controls.Add(_qualityBox, 1, 1);

        grid.Controls.Add(NewLabel("Language"), 0, 2);
        _languageBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        grid.Controls.Add(_languageBox, 1, 2);

        grid.Controls.Add(NewLabel("Time zone"), 0, 3);
        _timeZoneBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 220, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
        grid.Controls.Add(_timeZoneBox, 1, 3);

        _autoTimeBox = new CheckBox { Text = "Synchronise time automatically", AutoSize = true };
        grid.Controls.Add(_autoTimeBox, 1, 4);

        var save = new Button { Text = "Apply general settings", Width = 220 };
        save.Click += async (_, _) => await RunAsync("Saving general settings", async ct =>
        {
            var device = RequireDevice();
            await device.SetDeviceNameAsync(_nameBox.Text.Trim(), ct);

            if (_qualityBox.SelectedItem is string quality)
            {
                await device.SetVideoQualityAsync(quality, ct);
            }

            if (_languageBox.SelectedItem is UloLanguageInfo language)
            {
                await device.SetLanguageAsync(language.Code, ct);
            }

            if (!string.IsNullOrWhiteSpace(_timeZoneBox.Text))
            {
                await device.SetTimeSettingsAsync(_autoTimeBox.Checked, _timeZoneBox.Text.Trim(), ct);
            }

            await RefreshDashboardAsync();
        });
        grid.Controls.Add(save, 1, 5);

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildWifiGroup()
    {
        var group = NewGroup("Wi-Fi", 190);
        var grid = NewGrid();

        grid.Controls.Add(NewLabel("Network (SSID)"), 0, 0);
        _ssidBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 220 };
        grid.Controls.Add(_ssidBox, 1, 0);

        grid.Controls.Add(NewLabel("Password"), 0, 1);
        _wifiPasswordBox = new TextBox { Width = 220, UseSystemPasswordChar = true };
        grid.Controls.Add(_wifiPasswordBox, 1, 1);

        var scan = new Button { Text = "Scan for networks", Width = 220 };
        scan.Click += async (_, _) => await RunAsync("Scanning for Wi-Fi networks", async ct =>
        {
            var networks = await RequireDevice().ScanWifiAsync(ct);
            _ssidBox.Items.Clear();
            foreach (var network in networks)
            {
                _ssidBox.Items.Add(network.Ssid);
            }

            SetStatus(networks.Count == 0
                ? "The camera reported no networks (it only scans while awake)."
                : $"Found {networks.Count} network(s).");
        });
        grid.Controls.Add(scan, 1, 2);

        var connect = new Button { Text = "Join network", Width = 220 };
        connect.Click += async (_, _) =>
        {
            var answer = MessageBox.Show(
                this,
                "The camera will reconnect and may disappear from this network for a while.\r\n" +
                "A wrong password sends it back to its ad-hoc setup network. Continue?",
                "Change Wi-Fi",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (answer == DialogResult.Yes)
            {
                await RunAsync("Changing Wi-Fi", ct =>
                    RequireDevice().ConnectWifiAsync(_ssidBox.Text.Trim(), _wifiPasswordBox.Text, ct));
            }
        };
        grid.Controls.Add(connect, 1, 3);

        _wifiHintLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
            Text = "Type the network name - the camera only lists networks during its own setup.",
        };
        grid.Controls.Add(_wifiHintLabel, 1, 4);

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildDetectionGroup()
    {
        var group = NewGroup("Alert behaviour and detection", 260);
        var grid = NewGrid();

        _disableOnAppBox = new CheckBox { Text = "Alert mode can be disabled from the app", AutoSize = true };
        _disableOnDoubleTapBox = new CheckBox { Text = "Alert mode can be disabled by double tap", AutoSize = true };
        _disableOnFaceBox = new CheckBox { Text = "Alert mode can be disabled by a recognised face", AutoSize = true };
        grid.Controls.Add(_disableOnAppBox, 1, 0);
        grid.Controls.Add(_disableOnDoubleTapBox, 1, 1);
        grid.Controls.Add(_disableOnFaceBox, 1, 2);

        grid.Controls.Add(NewLabel("Exclusion zone"), 0, 3);
        var zone = new FlowLayoutPanel { AutoSize = true, Width = 320 };
        _exclusionTop = NewSpin("top");
        _exclusionLeft = NewSpin("left");
        _exclusionBottom = NewSpin("bottom");
        _exclusionRight = NewSpin("right");
        foreach (var (label, spin) in new[]
                 {
                     ("T", _exclusionTop), ("L", _exclusionLeft), ("B", _exclusionBottom), ("R", _exclusionRight),
                 })
        {
            zone.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(4, 6, 0, 0) });
            zone.Controls.Add(spin);
        }

        grid.Controls.Add(zone, 1, 3);

        _exclusionReverse = new CheckBox { Text = "Detect only inside the zone (reverse)", AutoSize = true };
        _exclusionReset = new CheckBox { Text = "Reset the zone when the camera is moved", AutoSize = true };
        grid.Controls.Add(_exclusionReverse, 1, 4);
        grid.Controls.Add(_exclusionReset, 1, 5);

        var save = new Button { Text = "Apply detection settings", Width = 220 };
        save.Click += async (_, _) => await RunAsync("Saving detection settings", async ct =>
        {
            var device = RequireDevice();

            await device.SetAlertBehaviourAsync(new UloAlertConfig
            {
                DisableOnAppRequest = _disableOnAppBox.Checked,
                DisableOnDoubleTap = _disableOnDoubleTapBox.Checked,
                DisableOnRecognizedUser = _disableOnFaceBox.Checked,
            }, ct);

            await device.SetExclusionZoneAsync(new UloExclusionConfig
            {
                Top = (int)_exclusionTop.Value,
                Left = (int)_exclusionLeft.Value,
                Bottom = (int)_exclusionBottom.Value,
                Right = (int)_exclusionRight.Value,
                Reverse = _exclusionReverse.Checked,
                ResetOnDisplacement = _exclusionReset.Checked,
            }, ct);
        });
        grid.Controls.Add(save, 1, 6);

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildUsersGroup()
    {
        var group = NewGroup("Accounts", 220);

        _usersView = new ListView
        {
            Dock = DockStyle.Top,
            Height = 150,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
        };
        _usersView.Columns.Add("Id", 40);
        _usersView.Columns.Add("E-mail", 240);
        _usersView.Columns.Add("Type", 70);
        _usersView.Columns.Add("Paired devices", 110);
        FillLastColumn(_usersView);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };

        var reload = new Button { Text = "Reload", Width = 90 };
        reload.Click += async (_, _) => await RunAsync("Loading accounts", async _ => await LoadUsersAsync());
        toolbar.Controls.Add(reload);

        var add = new Button { Text = "Add account...", Width = 120 };
        add.Click += async (_, _) => await AddUserAsync();
        toolbar.Controls.Add(add);

        var remove = new Button { Text = "Delete account", Width = 120 };
        remove.Click += async (_, _) => await DeleteUserAsync();
        toolbar.Controls.Add(remove);

        group.Controls.Add(toolbar);
        group.Controls.Add(_usersView);
        return group;
    }

    private Control BuildMaintenanceGroup()
    {
        var group = NewGroup("Maintenance", 260);
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };

        _firmwareLabel = new Label { AutoSize = true, Width = 420, Margin = new Padding(0, 4, 0, 8) };
        flow.Controls.Add(_firmwareLabel);
        flow.SetFlowBreak(_firmwareLabel, true);

        AddSetupButton(flow, "Check for updates", async ct =>
        {
            var report = await RequireDevice().CheckForUpdatesAsync(ct);

            _firmwareLabel.Text =
                $"Installed {report.Firmware.CurrentVersion}, cloud {report.Firmware.CloudVersion}, " +
                $"{(report.UpdateAvailable ? "update available" : "up to date")}, " +
                $"{(report.Fota.IsDownloading ? $"downloading {report.Fota.PercentageDownload}%" : "no download running")}, " +
                $"{(report.InstallAvailable ? "a downloaded firmware is ready to install" : "nothing waiting to be installed")}.";

            AppendActivity(new UloActivityEventArgs
            {
                Kind = UloActivityKind.PushEvent,
                Message = "Update check: " + _firmwareLabel.Text,
                Severity = report.UpdateAvailable ? UloLogSeverity.Notice : UloLogSeverity.Info,
            });

            MessageBox.Show(this, report.Describe(), "Update check", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });

        AddSetupButton(flow, "Download firmware update", async ct =>
        {
            await RequireDevice().StartFotaDownloadAsync(ct);
            _firmwareLabel.Text = "Firmware download requested - press 'Check for updates' to follow the progress.";
        });

        AddSetupButton(flow, "Install downloaded firmware", async ct =>
        {
            var device = RequireDevice();
            if (!await device.IsFirmwareInstallAvailableAsync(ct))
            {
                MessageBox.Show(
                    this,
                    "The camera reports that there is no downloaded firmware ready to install.",
                    "Nothing to install",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var answer = MessageBox.Show(
                this,
                "Install the downloaded firmware now? The camera restarts during the update.",
                "Install firmware",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer == DialogResult.Yes)
            {
                await device.InstallFirmwareAsync(ct);
            }
        });

        AddSetupButton(flow, "Back up settings on camera", async ct =>
        {
            await RequireDevice().CreateBackupAsync(null, ct);
            await LoadBackupsAsync(ct);
        });

        flow.Controls.Add(NewLabel("Backups"));
        _backupBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        flow.Controls.Add(_backupBox);

        AddSetupButton(flow, "List backups", async ct => await LoadBackupsAsync(ct));
        flow.SetFlowBreak(_backupBox, false);

        AddSetupButton(flow, "Restore selected backup", async ct =>
        {
            if (_backupBox.SelectedItem is not string name)
            {
                throw new InvalidOperationException("Select a backup first.");
            }

            await RequireDevice().RestoreBackupAsync(name, ct);
        });

        AddSetupButton(flow, "Delete recordings on camera...", async ct =>
        {
            using var dialog = new Form
            {
                Text = "Delete recordings",
                Width = 320,
                Height = 160,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
            };

            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 12, Top = 15, Width = 280 };
            combo.Items.AddRange(Enum.GetNames<UloCleanPeriod>().Cast<object>().ToArray());
            combo.SelectedIndex = 0;

            var ok = new Button { Text = "Delete", DialogResult = DialogResult.OK, Left = 130, Top = 55, Width = 80 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 215, Top = 55, Width = 80 };
            dialog.Controls.AddRange(new Control[] { combo, ok, cancel });
            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var period = Enum.Parse<UloCleanPeriod>((string)combo.SelectedItem!);
            await RequireDevice().CleanStorageAsync(period, ct);
            await RefreshMediaAsync();
        });

        AddSetupButton(flow, "Factory reset the camera...", async ct =>
        {
            var answer = MessageBox.Show(
                this,
                "A factory reset erases all users, the Wi-Fi setup and the recordings. " +
                "The camera restarts into its ad-hoc setup network.\r\n\r\nAre you absolutely sure?",
                "Factory reset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Stop,
                MessageBoxDefaultButton.Button2);

            if (answer == DialogResult.Yes)
            {
                await RequireDevice().FactoryResetAsync(ct);
            }
        });

        group.Controls.Add(flow);
        return group;
    }

    private void AddSetupButton(FlowLayoutPanel parent, string text, Func<CancellationToken, Task> action)
    {
        var button = new Button { Text = text, Width = 240, Margin = new Padding(0, 3, 0, 3) };
        button.Click += async (_, _) => await RunAsync(text.TrimEnd('.'), action);
        parent.Controls.Add(button);
        parent.SetFlowBreak(button, true);
    }

    private async Task LoadSetupAsync()
    {
        var device = _device;
        if (device is null || !device.IsAdminSession)
        {
            return;
        }

        var config = await device.GetConfigurationAsync(_cts.Token);

        // The language list is a separate call and some firmware refuses it; the rest of the tab
        // must still be filled in, so load every part on its own.
        IReadOnlyList<UloLanguageInfo> languages;
        try
        {
            languages = await device.GetLanguagesAsync(_cts.Token);
        }
        catch (UloApiException)
        {
            languages = Array.Empty<UloLanguageInfo>();
        }

        // The camera knows every time zone it accepts and sends them all with the country table, so
        // the dropdown can offer the real list instead of only the value already configured.
        IReadOnlyList<string> timeZones;
        try
        {
            timeZones = await device.GetAllTimeZonesAsync(_cts.Token);
        }
        catch (UloApiException)
        {
            timeZones = Array.Empty<string>();
        }

        void Apply()
        {
            _nameBox.Text = config.Device.Name;
            _qualityBox.SelectedItem = config.Video.Quality;
            if (!_qualityBox.Items.Contains(config.Video.Quality))
            {
                _qualityBox.Items.Add(config.Video.Quality);
                _qualityBox.SelectedItem = config.Video.Quality;
            }

            _languageBox.DisplayMember = nameof(UloLanguageInfo.Name);
            _languageBox.Items.Clear();
            foreach (var language in languages)
            {
                _languageBox.Items.Add(language);
                if (language.Code == config.Language.Language)
                {
                    _languageBox.SelectedItem = language;
                }
            }

            _timeZoneBox.BeginUpdate();
            _timeZoneBox.Items.Clear();
            foreach (var zone in timeZones)
            {
                _timeZoneBox.Items.Add(zone);
            }

            if (!string.IsNullOrWhiteSpace(config.Time.TimeZone) &&
                !_timeZoneBox.Items.Contains(config.Time.TimeZone))
            {
                _timeZoneBox.Items.Add(config.Time.TimeZone);
            }

            _timeZoneBox.EndUpdate();

            _timeZoneBox.Text = config.Time.TimeZone;
            _autoTimeBox.Checked = config.Time.Auto;

            _ssidBox.Text = config.Wifi.Ssid;

            _disableOnAppBox.Checked = config.Alert.DisableOnAppRequest;
            _disableOnDoubleTapBox.Checked = config.Alert.DisableOnDoubleTap;
            _disableOnFaceBox.Checked = config.Alert.DisableOnRecognizedUser;

            _exclusionTop.Value = Math.Clamp(config.Exclusion.Top, 0, 100);
            _exclusionLeft.Value = Math.Clamp(config.Exclusion.Left, 0, 100);
            _exclusionBottom.Value = Math.Clamp(config.Exclusion.Bottom, 0, 100);
            _exclusionRight.Value = Math.Clamp(config.Exclusion.Right, 0, 100);
            _exclusionReverse.Checked = config.Exclusion.Reverse;
            _exclusionReset.Checked = config.Exclusion.ResetOnDisplacement;

            _firmwareLabel.Text =
                $"Installed {config.Firmware.CurrentVersion}, cloud {config.Firmware.CloudVersion}, " +
                $"update {(config.Firmware.UpdateAvailable ? "available" : "none")}.";
        }

        if (InvokeRequired)
        {
            BeginInvoke(Apply);
        }
        else
        {
            Apply();
        }

        // Users are their own call as well: a camera that refuses the list still shows everything
        // else the account is allowed to see.
        try
        {
            await LoadUsersAsync();
        }
        catch (UloApiException ex)
        {
            SetStatus($"User list could not be loaded: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads the settings backups stored on the camera. Kept out of the connect sequence on purpose:
    /// the camera refuses <c>GET /api/v1/system/backups</c> with
    /// <c>"Please switch to Standard mode to do this operation."</c> whenever it records in alert or
    /// spy mode, and that must not cost the user the whole session.
    /// </summary>
    private async Task LoadBackupsAsync(CancellationToken ct)
    {
        var device = _device;
        if (device is null)
        {
            return;
        }

        var backups = await device.GetBackupsAsync(ct);

        void Apply()
        {
            _backupBox.Items.Clear();
            foreach (var backup in backups)
            {
                _backupBox.Items.Add(backup);
            }

            if (_backupBox.Items.Count > 0)
            {
                _backupBox.SelectedIndex = 0;
            }
        }

        if (InvokeRequired)
        {
            BeginInvoke(Apply);
        }
        else
        {
            Apply();
        }
    }

    private async Task LoadUsersAsync()
    {
        var device = _device;
        if (device is null)
        {
            return;
        }

        var users = await device.GetUsersAsync(_cts.Token);

        void Apply()
        {
            _usersView.BeginUpdate();
            _usersView.Items.Clear();

            foreach (var user in users)
            {
                _usersView.Items.Add(new ListViewItem(new[]
                {
                    user.Id.ToString(),
                    user.Email,
                    user.IsAdmin ? "admin" : "user",
                    user.Devices.Count.ToString(),
                })
                {
                    Tag = user,
                });
            }

            _usersView.EndUpdate();
        }

        if (InvokeRequired)
        {
            BeginInvoke(Apply);
        }
        else
        {
            Apply();
        }
    }

    private async Task AddUserAsync()
    {
        using var dialog = new Form
        {
            Text = "New account",
            Width = 380,
            Height = 210,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
        };

        var email = new TextBox { Left = 110, Top = 15, Width = 230 };
        var password = new TextBox { Left = 110, Top = 45, Width = 230, UseSystemPasswordChar = true };
        var admin = new CheckBox { Text = "Administrator", Left = 110, Top = 75, Width = 230 };
        var ok = new Button { Text = "Create", DialogResult = DialogResult.OK, Left = 175, Top = 110, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 260, Top = 110, Width = 80 };

        dialog.Controls.AddRange(new Control[]
        {
            new Label { Text = "E-mail", Left = 12, Top = 18, Width = 90 },
            email,
            new Label { Text = "Password", Left = 12, Top = 48, Width = 90 },
            password,
            admin, ok, cancel,
        });
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await RunAsync("Creating account", async ct =>
        {
            await RequireDevice().CreateUserAsync(new UloUser
            {
                Email = email.Text.Trim(),
                Name = email.Text.Trim(),
                Password = password.Text,
                Account = admin.Checked ? "admin" : "user",
            }, ct);

            await LoadUsersAsync();
        });
    }

    private async Task DeleteUserAsync()
    {
        if (_usersView.SelectedItems.Count == 0 || _usersView.SelectedItems[0].Tag is not UloUser user)
        {
            MessageBox.Show(this, "Select the account you want to remove.", "Nothing selected");
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Delete the account '{user.Email}'?",
            "Delete account",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        await RunAsync("Deleting account", async ct =>
        {
            await RequireDevice().DeleteUserAsync(user.Id, ct);
            await LoadUsersAsync();
        });
    }

    private static GroupBox NewGroup(string text, int height) => new()
    {
        Text = text,
        // Auto sizing prevents the buttons at the bottom of a group from being clipped.
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(640, 0),
        Padding = new Padding(10, 6, 10, 12),
        Margin = new Padding(0, 0, 0, 12),
    };

    private static TableLayoutPanel NewGrid() => new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        GrowStyle = TableLayoutPanelGrowStyle.AddRows,
    };

    private static Label NewLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 6, 10, 0),
        Width = 130,
    };

    private static NumericUpDown NewSpin(string name) => new()
    {
        Minimum = 0,
        Maximum = 100,
        Width = 55,
        Name = name,
    };

    private TabPage _setupTab = null!;
    private Label _setupHintLabel = null!;
    private TextBox _nameBox = null!;
    private ComboBox _qualityBox = null!;
    private ComboBox _languageBox = null!;
    private ComboBox _timeZoneBox = null!;
    private CheckBox _autoTimeBox = null!;
    private ComboBox _ssidBox = null!;
    private TextBox _wifiPasswordBox = null!;
    private CheckBox _disableOnAppBox = null!;
    private CheckBox _disableOnDoubleTapBox = null!;
    private CheckBox _disableOnFaceBox = null!;
    private NumericUpDown _exclusionTop = null!;
    private NumericUpDown _exclusionLeft = null!;
    private NumericUpDown _exclusionBottom = null!;
    private NumericUpDown _exclusionRight = null!;
    private CheckBox _exclusionReverse = null!;
    private CheckBox _exclusionReset = null!;
    private ListView _usersView = null!;
    private Label _firmwareLabel = null!;
    private Label _wifiHintLabel = null!;
    private ComboBox _backupBox = null!;
}
