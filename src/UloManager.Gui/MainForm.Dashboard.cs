using UloManager.Core;

namespace UloManager.Gui;

public sealed partial class MainForm
{
    private TabPage BuildDashboardTab()
    {
        var page = new TabPage("Dashboard") { Padding = new Padding(10) };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
        };

        // The left column keeps a fixed, compact width; all extra space from resizing or
        // maximising the window goes to the camera picture on the right.
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));

        layout.Controls.Add(BuildStatusGroup(), 0, 0);
        layout.Controls.Add(BuildControlGroup(), 0, 1);
        layout.Controls.Add(BuildSnapshotGroup(), 1, 0);
        layout.SetRowSpan(_snapshotGroup, 2);

        page.Controls.Add(layout);
        return page;
    }

    private Control BuildStatusGroup()
    {
        var group = new GroupBox
        {
            Text = "Camera status",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
        };

        _statusView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _statusView.Columns.Add("Property", 150);
        _statusView.Columns.Add("Value", 250);
        FillLastColumn(_statusView);

        group.Controls.Add(_statusView);
        return group;
    }

    private Control BuildControlGroup()
    {
        var group = new GroupBox
        {
            Text = "Actions",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
        };

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };

        var modeLabel = new Label { Text = "Recording mode:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) };
        flow.Controls.Add(modeLabel);

        foreach (var mode in new[] { UloMode.Standard, UloMode.Spy, UloMode.Alert })
        {
            var button = new Button { Text = mode.ToApiValue(), Width = 90 };
            button.Click += async (_, _) => await RunAsync($"Switching to {mode.ToApiValue()}", async ct =>
            {
                await RequireDevice().SetModeAsync(mode, ct);
                await RefreshDashboardAsync();
            });
            flow.Controls.Add(button);
        }

        flow.SetFlowBreak(flow.Controls[^1], true);

        AddActionButton(flow, "Refresh status", async ct =>
        {
            await RefreshDashboardAsync();
            await Task.CompletedTask;
        });

        AddActionButton(flow, "Take snapshot", async ct => await TakeSnapshotAsync(ct));

        AddActionButton(flow, "Sync camera clock", async ct =>
        {
            await RequireDevice().SetDeviceTimeAsync(DateTime.Now, ct);
            await RefreshDashboardAsync();
        });

        AddActionButton(flow, "Move files to SD card", async ct =>
        {
            var device = RequireDevice();
            var storage = await device.GetStorageAsync(ct);
            if (!storage.SdCard.Inserted)
            {
                throw new InvalidOperationException("There is no SD card in the camera.");
            }

            await device.StartMoveToCardAsync(ct);
        });

        AddActionButton(flow, "Save log to file...", async ct =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Where should the camera log be saved?" };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var file = await RequireDevice().Log.SaveAsync(dialog.SelectedPath, ct);
            SetStatus($"Log saved to {file}");
        });

        group.Controls.Add(flow);
        return group;
    }

    private void AddActionButton(Control parent, string text, Func<CancellationToken, Task> action)
    {
        var button = new Button { Text = text, Width = 160, Margin = new Padding(3, 3, 3, 3) };
        button.Click += async (_, _) => await RunAsync(text.TrimEnd('.'), action);
        parent.Controls.Add(button);
        ((FlowLayoutPanel)parent).SetFlowBreak(button, true);
    }

    private Control BuildSnapshotGroup()
    {
        _snapshotGroup = new GroupBox
        {
            Text = "What the camera sees",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
        };

        _snapshotBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(32, 32, 32),
        };

        // One row that never wraps and keeps everything on the same centre line.
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 6,
            RowCount = 1,
        };
        for (var i = 0; i < 5; i++)
        {
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }

        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _autoSnapshotBox = new CheckBox
        {
            Text = "Refresh every",
            AutoSize = true,
            Checked = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 3, 4, 3),
        };
        _autoSnapshotBox.CheckedChanged += (_, _) => ApplySnapshotTimer();
        bottom.Controls.Add(_autoSnapshotBox, 0, 0);

        _snapshotIntervalBox = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 3600,
            Value = 15,
            Width = 60,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 3, 4, 3),
        };
        _snapshotIntervalBox.ValueChanged += (_, _) => ApplySnapshotTimer();
        bottom.Controls.Add(_snapshotIntervalBox, 1, 0);

        bottom.Controls.Add(
            new Label { Text = "s", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 3) },
            2,
            0);

        var now = new Button { Text = "Refresh now", Width = 92, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 5, 3) };
        now.Click += async (_, _) => await RunAsync("Taking a snapshot", TakeSnapshotAsync);
        bottom.Controls.Add(now, 3, 0);

        var save = new Button { Text = "Save picture...", Width = 98, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 0, 3) };
        save.Click += (_, _) => SaveSnapshot();
        bottom.Controls.Add(save, 4, 0);

        _snapshotTimer = new System.Windows.Forms.Timer { Interval = 15000 };
        _snapshotTimer.Tick += async (_, _) =>
        {
            if (_device is null || _snapshotBusy)
            {
                return;
            }

            _snapshotBusy = true;
            try
            {
                await TakeSnapshotAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                SetStatus($"Snapshot failed: {ex.Message}");
            }
            finally
            {
                _snapshotBusy = false;
            }
        };

        _snapshotGroup.Controls.Add(_snapshotBox);
        _snapshotGroup.Controls.Add(bottom);
        return _snapshotGroup;
    }

    private void ApplySnapshotTimer()
    {
        _snapshotTimer.Interval = (int)_snapshotIntervalBox.Value * 1000;
        _snapshotTimer.Enabled = _autoSnapshotBox.Checked && _device is not null;
    }

    private void SaveSnapshot()
    {
        if (_snapshotBox.Image is null)
        {
            MessageBox.Show(this, "There is no picture yet. Press 'Refresh now' first.", "Nothing to save");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "JPEG image (*.jpg)|*.jpg|PNG image (*.png)|*.png",
            FileName = $"ulo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _snapshotBox.Image.Save(dialog.FileName);
            SetStatus($"Picture saved to {dialog.FileName}");
        }
    }

    private async Task TakeSnapshotAsync(CancellationToken ct)
    {
        var device = RequireDevice();
        var folder = Path.Combine(Path.GetTempPath(), "UloManager");

        // storeOnCamera: false keeps the preview out of the camera's own recordings.
        var file = await device.DownloadCurrentSnapshotAsync(folder, storeOnCamera: false, ct);

        // Load through a copy so the file is not locked by the picture box. Image.FromStream keeps
        // using the stream for the lifetime of the image, so decode into an independent bitmap.
        Image image;
        using (var stream = new MemoryStream(await File.ReadAllBytesAsync(file, ct)))
        using (var decoded = Image.FromStream(stream))
        {
            image = new Bitmap(decoded);
        }

        void Apply()
        {
            _snapshotBox.Image?.Dispose();
            _snapshotBox.Image = image;
            _snapshotGroup.Text = $"What the camera sees - {DateTime.Now:HH:mm:ss}";
        }

        if (InvokeRequired)
        {
            BeginInvoke(Apply);
        }
        else
        {
            Apply();
        }

        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
            // The temporary copy is harmless if it cannot be removed.
        }
    }

    private bool _snapshotBusy;
    private System.Windows.Forms.Timer _snapshotTimer = null!;
    private NumericUpDown _snapshotIntervalBox = null!;

    private async Task RefreshDashboardAsync()
    {
        var device = _device;
        if (device is null)
        {
            return;
        }

        var monitor = _monitor ?? new UloActivityMonitor(device);
        var snapshot = await monitor.CaptureAsync(_cts.Token);
        var config = await device.GetConfigurationAsync(_cts.Token);

        void Apply()
        {
            _statusView.BeginUpdate();
            _statusView.Items.Clear();

            void Add(string name, string value) => _statusView.Items.Add(new ListViewItem(new[] { name, value }));

            Add("Name", config.Device.Name);
            Add("Device ID", string.IsNullOrEmpty(_info?.DeviceId) ? "—" : _info.DeviceId);
            Add("Session", IsAdminSession ? "administrator (setup allowed)" : "standard user");
            Add("Recording mode", snapshot.Mode.ToApiValue());
            Add("Battery", $"{snapshot.State.BatteryLevel}% ({(snapshot.State.Plugged ? "plugged in" : "on battery")})");
            Add("Camera clock", snapshot.DeviceTime.ToString("yyyy-MM-dd HH:mm:ss"));
            Add("Firmware", FormatFirmwareVersion(config.Firmware.CurrentVersion, config.Firmware.CloudVersion));
            Add("Update available", config.Firmware.UpdateAvailable ? "yes" : "no");
            Add("Wi-Fi", config.Wifi.Ssid);
            Add("Video quality", config.Video.Quality);
            Add("Time zone", $"{config.Time.TimeZone} (auto: {config.Time.Auto})");
            Add("Internal memory", $"{snapshot.Storage.Internal.FreeMb} MB free of {snapshot.Storage.Internal.TotalMb} MB ({snapshot.Storage.Internal.UsedPercent}% used)");
            Add("SD card", snapshot.Storage.SdCard.Inserted
                ? $"{snapshot.Storage.SdCard.FreeMb} MB free of {snapshot.Storage.SdCard.TotalMb} MB"
                : "not inserted");
            Add("Recordings", snapshot.MediaFileCount.ToString());
            Add("Move to card", snapshot.BackupRunning ? "running" : "idle");
            Add("Device mode", snapshot.State.DeviceMode == UloDeviceMode.Setup
                ? "SETUP / configuration mode"
                : "USAGE mode");

            _statusView.EndUpdate();
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

    private ListView _statusView = null!;
    private GroupBox _snapshotGroup = null!;
    private PictureBox _snapshotBox = null!;
    private CheckBox _autoSnapshotBox = null!;

    /// <summary>
    /// Decodes the firmware version. The update logic in the vendor's own source reveals that
    /// <c>10.1308</c> is three layers: APQ <c>10</c> (Android image), APK <c>13</c> (application),
    /// STM <c>08</c> (head/motor microcontroller).
    /// </summary>
    private static string FormatFirmwareVersion(string current, string cloud)
    {
        static string Decode(string version)
        {
            var parts = version.Split('.');
            if (parts.Length == 2 && parts[1].Length == 4)
            {
                return $"{version} (Android {parts[0]}, app {parts[1][..2]}, head {parts[1][2..]})";
            }

            return version;
        }

        var result = Decode(current);
        if (!string.IsNullOrWhiteSpace(cloud) && cloud != current)
        {
            result += $", cloud {Decode(cloud)}";
        }

        return result;
    }
}
