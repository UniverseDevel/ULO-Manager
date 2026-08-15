using UloManager.Core;

namespace UloManager.Gui;

public sealed partial class MainForm
{
    private TabPage BuildActivityTab()
    {
        var page = new TabPage("Activity") { Padding = new Padding(10) };

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };

        _monitorButton = new Button { Text = "Stop monitoring", Width = 130 };
        _monitorButton.Click += (_, _) =>
        {
            if (_monitor?.IsRunning == true)
            {
                _ = StopMonitoringAsync();
            }
            else
            {
                StartMonitoring();
            }
        };
        toolbar.Controls.Add(_monitorButton);

        toolbar.Controls.Add(new Label { Text = "Poll every", AutoSize = true, Margin = new Padding(14, 8, 4, 0) });
        _intervalBox = new NumericUpDown { Minimum = 2, Maximum = 600, Value = 10, Width = 60 };
        _intervalBox.ValueChanged += (_, _) =>
        {
            if (_monitor is not null)
            {
                _monitor.PollInterval = TimeSpan.FromSeconds((double)_intervalBox.Value);
            }
        };
        toolbar.Controls.Add(_intervalBox);
        toolbar.Controls.Add(new Label { Text = "seconds", AutoSize = true, Margin = new Padding(4, 8, 4, 0) });

        _importantOnlyBox = new CheckBox { Text = "Important events only", AutoSize = true, Margin = new Padding(14, 6, 0, 0) };
        toolbar.Controls.Add(_importantOnlyBox);

        _autoScrollBox = new CheckBox
        {
            Text = "Follow new events",
            AutoSize = true,
            Checked = true,
            Margin = new Padding(14, 6, 0, 0),
        };
        toolbar.Controls.Add(_autoScrollBox);

        var clearButton = new Button { Text = "Clear", Width = 80, Margin = new Padding(14, 0, 0, 0) };
        clearButton.Click += (_, _) => _activityView.Items.Clear();
        toolbar.Controls.Add(clearButton);

        var exportButton = new Button { Text = "Export...", Width = 90 };
        exportButton.Click += (_, _) => ExportActivity();
        toolbar.Controls.Add(exportButton);

        var fullLogButton = new Button { Text = "Load full camera log", Width = 150, Margin = new Padding(14, 0, 0, 0) };
        fullLogButton.Click += async (_, _) => await RunAsync("Loading the whole camera log", async ct =>
        {
            var entries = await RequireDevice().Log.GetEntriesAsync(ct);

            _activityView.BeginUpdate();
            _activityView.Items.Clear();

            foreach (var entry in entries)
            {
                AppendActivity(new UloActivityEventArgs
                {
                    Kind = UloActivityKind.Log,
                    Message = entry.Activity ?? entry.Message,
                    Severity = entry.Severity,
                    LogEntry = entry,
                });
            }

            _activityView.EndUpdate();
            SetStatus($"Loaded {entries.Count} log entries from the camera.");
        });
        toolbar.Controls.Add(fullLogButton);

        _activityView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Consolas", 9F),
        };
        _activityView.Columns.Add("Time", 160);
        _activityView.Columns.Add("Event", 100);
        _activityView.Columns.Add("What the camera is doing", 350);
        _activityView.Columns.Add("Raw", 350);
        FillLastColumn(_activityView);

        // Keep the "What" and "Raw" columns equal when the window resizes.
        _activityView.Resize += (_, _) =>
        {
            var remaining = _activityView.ClientSize.Width - 160 - 100 - 4;
            if (remaining > 200)
            {
                var half = remaining / 2;
                _activityView.Columns[2].Width = half;
                _activityView.Columns[3].Width = half;
            }
        };

        page.Controls.Add(_activityView);
        page.Controls.Add(toolbar);
        return page;
    }

    private void StartMonitoring()
    {
        var device = _device;
        if (device is null || _monitor?.IsRunning == true)
        {
            return;
        }

        _monitor?.Dispose();
        _monitor = new UloActivityMonitor(device)
        {
            PollInterval = TimeSpan.FromSeconds((double)_intervalBox.Value),
            InitialLogLines = 200,
        };

        _monitor.Activity += OnActivity;
        _monitor.Start();
        _monitorButton.Text = "Stop monitoring";
        SetStatus("Monitoring the camera.");
    }

    private async Task StopMonitoringAsync()
    {
        if (_monitor is null)
        {
            return;
        }

        await _monitor.StopAsync();
        _monitor.Activity -= OnActivity;
        _monitorButton.Text = "Start monitoring";
        SetStatus("Monitoring stopped.");
    }

    private void OnActivity(object? sender, UloActivityEventArgs e)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        try
        {
            BeginInvoke(() => AppendActivity(e));
        }
        catch (ObjectDisposedException)
        {
            // Window closed while a poll was in flight.
        }
    }

    private void AppendActivity(UloActivityEventArgs e)
    {
        if (_importantOnlyBox.Checked && e.Severity == UloLogSeverity.Info && e.Kind == UloActivityKind.Log)
        {
            return;
        }

        // The camera flips between admin/setup and usage mode whenever it is turned over,
        // so the banner has to follow it while the app is running.
        if (e.State is not null)
        {
            ApplyDeviceMode(e.State);
        }

        var local = e.LogEntry?.Timestamp ?? e.TimestampUtc.ToLocalTime().DateTime;

        // Only jump to the newest entry when the list is already sitting at the bottom.
        // Scrolling up therefore pauses following, and scrolling back down resumes it,
        // instead of every refresh yanking the view away from what you were reading.
        var stickToBottom = _autoScrollBox.Checked && IsScrolledToBottom();

        var item = new ListViewItem(new[]
        {
            local.ToString("yyyy-MM-dd HH:mm:ss"),
            e.Kind.ToString(),
            e.Message,
            e.LogEntry?.Message ?? "",
        })
        {
            ForeColor = Theme.Severity(e.Severity),
        };

        _activityView.Items.Add(item);

        // Trimming removes rows from the top, which would drag the view along with it,
        // so it only happens while the list is following the newest entries.
        if (stickToBottom)
        {
            while (_activityView.Items.Count > MaxActivityRows)
            {
                _activityView.Items.RemoveAt(0);
            }

            item.EnsureVisible();
        }

        if (e.Snapshot is not null)
        {
            _ = RefreshDashboardAsync();
        }

        if (e.Kind == UloActivityKind.DeviceModeChanged)
        {
            _ = RefreshDashboardAsync();
        }
    }

    /// <summary>True when the last row is visible, i.e. the user has not scrolled up.</summary>
    private bool IsScrolledToBottom()
    {
        var count = _activityView.Items.Count;
        if (count == 0)
        {
            return true;
        }

        var top = _activityView.TopItem;
        if (top is null)
        {
            return true;
        }

        var rowHeight = Math.Max(1, _activityView.Items[0].Bounds.Height);
        var visibleRows = Math.Max(1, _activityView.ClientSize.Height / rowHeight);

        // One row of tolerance so a partially visible last row still counts as "at the bottom".
        return top.Index + visibleRows >= count - 1;
    }

    private const int MaxActivityRows = 5000;

    private void ExportActivity()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"ulo_activity_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var lines = _activityView.Items
            .Cast<ListViewItem>()
            .Select(item => string.Join("\t", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text)));

        File.WriteAllLines(dialog.FileName, lines);
        SetStatus($"Activity exported to {dialog.FileName}");
    }

    private ListView _activityView = null!;
    private Button _monitorButton = null!;
    private NumericUpDown _intervalBox = null!;
    private CheckBox _importantOnlyBox = null!;
    private CheckBox _autoScrollBox = null!;
}
