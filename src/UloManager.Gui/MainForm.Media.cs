using UloManager.Core;

namespace UloManager.Gui;

public sealed partial class MainForm
{
    private TabPage BuildMediaTab()
    {
        var page = new TabPage("Recordings") { Padding = new Padding(10) };

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };

        toolbar.Controls.Add(new Label { Text = "Show", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        _mediaTypeBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        _mediaTypeBox.Items.AddRange(new object[] { "All", "Video", "Snapshot" });
        _mediaTypeBox.SelectedIndex = 0;
        _mediaTypeBox.SelectedIndexChanged += async (_, _) => await RefreshMediaAsync();
        toolbar.Controls.Add(_mediaTypeBox);

        var refresh = new Button { Text = "Refresh", Width = 90, Margin = new Padding(10, 0, 0, 0) };
        refresh.Click += async (_, _) => await RunAsync("Listing recordings", async _ => await RefreshMediaAsync());
        toolbar.Controls.Add(refresh);

        var downloadSelected = new Button { Text = "Download selected...", Width = 150 };
        downloadSelected.Click += async (_, _) => await DownloadSelectedAsync();
        toolbar.Controls.Add(downloadSelected);

        var downloadAll = new Button { Text = "Download all...", Width = 130 };
        downloadAll.Click += async (_, _) => await DownloadAllAsync();
        toolbar.Controls.Add(downloadAll);

        var deleteDay = new Button { Text = "Delete day on camera...", Width = 160, Margin = new Padding(10, 0, 0, 0) };
        deleteDay.Click += async (_, _) => await DeleteDayAsync();
        toolbar.Controls.Add(deleteDay);

        _mediaView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = true,
        };
        _mediaView.Columns.Add("Recorded", 150);
        _mediaView.Columns.Add("Type", 80);
        _mediaView.Columns.Add("Day", 90);
        _mediaView.Columns.Add("File", 520);
        FillLastColumn(_mediaView);

        page.Controls.Add(_mediaView);
        page.Controls.Add(toolbar);
        return page;
    }

    private UloMediaType SelectedMediaType => _mediaTypeBox.SelectedIndex switch
    {
        1 => UloMediaType.Video,
        2 => UloMediaType.Snapshot,
        _ => UloMediaType.All,
    };

    private async Task RefreshMediaAsync()
    {
        var device = _device;
        if (device is null)
        {
            return;
        }

        var files = await device.Media.ListAsync(SelectedMediaType, _cts.Token);

        void Apply()
        {
            _mediaView.BeginUpdate();
            _mediaView.Items.Clear();

            foreach (var file in files.Reverse())
            {
                _mediaView.Items.Add(new ListViewItem(new[]
                {
                    file.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                    file.Type.ToString(),
                    file.Day,
                    file.Path,
                })
                {
                    Tag = file,
                });
            }

            _mediaView.EndUpdate();
            SetStatus($"{files.Count} file(s) on the camera.");
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

    private string? AskForFolder(string description)
    {
        using var dialog = new FolderBrowserDialog { Description = description, UseDescriptionForTitle = true };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private async Task DownloadSelectedAsync()
    {
        var selected = _mediaView.SelectedItems.Cast<ListViewItem>().Select(i => (UloMediaFile)i.Tag!).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Select the recordings you want to download first.", "Nothing selected");
            return;
        }

        var folder = AskForFolder("Where should the recordings be saved?");
        if (folder is null)
        {
            return;
        }

        await RunAsync($"Downloading {selected.Count} file(s)", async ct =>
        {
            var device = RequireDevice();
            long bytes = 0;

            foreach (var file in selected)
            {
                var target = Path.Combine(folder, file.Day, file.FileName);
                bytes += await device.Client.DownloadFileAsync(file.Path, target, ct);
                SetStatus($"Downloaded {file.FileName}");
            }

            SetStatus($"Downloaded {selected.Count} file(s), {UloMediaService.FormatBytes(bytes)}.");
        });
    }

    private async Task DownloadAllAsync()
    {
        var folder = AskForFolder("Where should the recordings be saved?");
        if (folder is null)
        {
            return;
        }

        await RunAsync("Downloading recordings", async ct =>
        {
            var progress = new Progress<string>(SetStatus);
            var result = await RequireDevice().Media.DownloadAsync(folder, SelectedMediaType, null, true, progress, ct);
            SetStatus(result.ToString());
        });
    }

    private async Task DeleteDayAsync()
    {
        var selected = _mediaView.SelectedItems.Cast<ListViewItem>().Select(i => (UloMediaFile)i.Tag!).FirstOrDefault();
        if (selected is null)
        {
            MessageBox.Show(this, "Select a recording from the day you want to delete.", "Nothing selected");
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Delete every recording from {selected.Day} on the camera? This cannot be undone.",
            "Delete recordings",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        await RunAsync($"Deleting {selected.Day}", async ct =>
        {
            await RequireDevice().DeleteMediaDayAsync(selected.Day, ct);
            await RefreshMediaAsync();
        });
    }

    private ListView _mediaView = null!;
    private ComboBox _mediaTypeBox = null!;
}
