using System.Diagnostics;
using System.Threading.Channels;
using UloManager.Core;

namespace UloManager.Gui;

public sealed partial class MainForm
{
    private TabPage BuildLiveTab()
    {
        var page = new TabPage("Live video") { Padding = new Padding(10) };

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };

        _liveStartButton = new Button { Text = "Start live video", Width = 130 };
        _liveStartButton.Click += async (_, _) => await StartLiveAsync();
        toolbar.Controls.Add(_liveStartButton);

        _liveStopButton = new Button { Text = "Stop", Width = 80, Enabled = false };
        _liveStopButton.Click += async (_, _) => await StopLiveAsync();
        toolbar.Controls.Add(_liveStopButton);

        _liveWindowBox = new CheckBox
        {
            Text = "Open in a separate player window",
            AutoSize = true,
            Margin = new Padding(14, 6, 0, 0),
        };
        toolbar.Controls.Add(_liveWindowBox);

        _liveSaveBox = new CheckBox
        {
            Text = "Also save to file",
            AutoSize = true,
            Margin = new Padding(14, 6, 0, 0),
        };
        toolbar.Controls.Add(_liveSaveBox);

        // The video is drawn straight into this panel, so leaving the separate window
        // unticked still shows the picture inside the application.
        _videoHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(24, 24, 24),
        };

        _liveStatusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 24,
            Text = "Live video is stopped.",
        };

        _liveLog = new TextBox
        {
            Dock = DockStyle.Bottom,
            Multiline = true,
            ReadOnly = true,
            Height = 90,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 8.5F),
            Text = "The camera streams H.264 in fragmented MP4 over a WebSocket " +
                   "(ws://<host>/api/v1/live, sub-protocol 'mudesign.ulo.mp4').",
        };

        // The stream reports its size continuously, so the status line needs its own tick;
        // the WebSocket only raises events when it connects or drops.
        _liveStatsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _liveStatsTimer.Tick += (_, _) => UpdateLiveStats();

        page.Controls.Add(_videoHost);
        page.Controls.Add(_liveStatusLabel);
        page.Controls.Add(_liveLog);
        page.Controls.Add(toolbar);
        return page;
    }

    private void UpdateLiveStats()
    {
        var stream = _device?.LiveVideo;
        if (stream is null || _liveCts is null)
        {
            return;
        }

        // Counted here rather than on the stream, because reconnecting resets the stream counter.
        var bytes = Interlocked.Read(ref _liveBytes);
        var now = DateTime.UtcNow;
        var sinceLast = (now - _liveLastTick).TotalSeconds;

        if (sinceLast >= 0.5)
        {
            // Fragments arrive in bursts, so an unsmoothed rate flickers to zero between them.
            var instant = (bytes - _liveLastBytes) * 8 / sinceLast;
            _liveBitsPerSecond = _liveBitsPerSecond <= 0
                ? instant
                : (_liveBitsPerSecond * 0.6) + (instant * 0.4);

            _liveLastBytes = bytes;
            _liveLastTick = now;
        }

        var elapsed = now - _liveStartedAt;
        var state = !stream.IsConnected ? "reconnecting"
            : bytes == 0 ? "connected, waiting for the camera to send"
            : "running";

        _liveStatusLabel.Text = bytes == 0
            ? $"Live video {state} - {elapsed:hh\\:mm\\:ss}"
            : $"Live video {state} - {UloMediaService.FormatBytes(bytes)} streamed, " +
              $"{_liveBitsPerSecond / 1_000_000:0.0} Mbit/s, {elapsed:hh\\:mm\\:ss}";
    }

    private async Task StartLiveAsync()
    {
        var device = _device;
        if (device is null || _liveCts is not null)
        {
            return;
        }

        string? file = null;
        if (_liveSaveBox.Checked)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "MP4 video (*.mp4)|*.mp4",
                FileName = $"ulo_live_{DateTime.Now:yyyyMMdd_HHmmss}.mp4",
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            file = dialog.FileName;
        }

        var playerPath = FindPlayer();

        if (playerPath is null && file is null)
        {
            MessageBox.Show(
                this,
                "VLC or ffplay was not found, so the picture cannot be shown.\r\n\r\n" +
                "Install VLC to watch the stream, or tick 'Also save to file' to record it instead.",
                "No player available",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (playerPath is not null)
        {
            _livePlayer = Process.Start(new ProcessStartInfo
            {
                FileName = playerPath,
                Arguments = BuildPlayerArguments(playerPath, _liveWindowBox.Checked ? null : _videoHost.Handle),
                RedirectStandardInput = true,
                UseShellExecute = false,
            });

            _liveRestarts = 0;
        }

        _liveCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _liveStartButton.Enabled = false;
        _liveStopButton.Enabled = true;
        _liveInitSegment = null;
        _livePlayerPath = playerPath;
        _liveEmbed = !_liveWindowBox.Checked;

        _liveStartedAt = DateTime.UtcNow;
        _liveLastTick = _liveStartedAt;
        _liveLastBytes = 0;
        _liveBytes = 0;
        _liveBitsPerSecond = 0;
        _liveStatsTimer.Start();

        // The camera has one video pipeline: asking it for a still picture while the live stream
        // runs cuts the stream off. The dashboard refresh therefore pauses for the duration.
        _snapshotTimer.Enabled = false;

        device.LiveVideo.StatusChanged -= OnLiveStatus;
        device.LiveVideo.StatusChanged += OnLiveStatus;

        AppendLive(playerPath is null
            ? "Starting live video (recording only)..."
            : _liveWindowBox.Checked
                ? "Starting live video in a separate player window..."
                : "Starting live video inside the application...");

        _liveTask = Task.Run(async () =>
        {
            FileStream? output = null;
            var written = 0L;

            // Feeding the player straight from the receive callback is what stalled the stream:
            // when VLC stops reading, its pipe fills up, Write blocks, and the camera socket backs
            // up until it drops. The player now has its own pump thread behind a bounded queue, so
            // a slow player costs dropped fragments instead of the whole stream.
            var pipe = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(120)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            });

            var pump = Task.Run(() => PumpPlayerAsync(pipe.Reader, _liveCts.Token));

            try
            {
                if (file is not null)
                {
                    output = File.Create(file);
                }

                while (!_liveCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await device.LiveVideo.ReceiveAsync(
                            chunk =>
                            {
                                written += chunk.Length;
                                Interlocked.Add(ref _liveBytes, chunk.Length);
                                output?.Write(chunk.Span);

                                // The first chunk carries ftyp + moov; a restarted player needs it again.
                                _liveInitSegment ??= chunk.ToArray();

                                pipe.Writer.TryWrite(chunk.ToArray());
                            },
                            _liveCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        AppendLive($"Live video interrupted: {ex.Message}");
                    }

                    if (_liveCts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    // The camera ends the stream now and then - pick it straight back up.
                    AppendLive("Live video reconnecting...");

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), _liveCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            finally
            {
                pipe.Writer.TryComplete();

                try
                {
                    await pump;
                }
                catch (Exception)
                {
                    // Already handled inside the pump.
                }

                output?.Dispose();
                StopPlayer();

                AppendLive(file is null
                    ? $"Live video stopped ({UloMediaService.FormatBytes(written)} streamed)."
                    : $"Live video stopped, {UloMediaService.FormatBytes(written)} saved to {file}");

                try
                {
                    BeginInvoke(() =>
                    {
                        _liveStatsTimer.Stop();
                        _liveStartButton.Enabled = true;
                        _liveStopButton.Enabled = false;
                        _liveStatusLabel.Text =
                            $"Live video stopped - {UloMediaService.FormatBytes(written)} streamed.";

                        // Still pictures can resume now that the video pipeline is free again.
                        ApplySnapshotTimer();
                    });
                }
                catch (Exception)
                {
                    // The window may already be closing.
                }
            }
        });

        await Task.CompletedTask;
    }

    /// <summary>
    /// Writes queued fragments into the player on its own thread, restarting the player when it
    /// has gone away and re-priming it with the MP4 initialisation segment so the picture returns.
    /// </summary>
    private async Task PumpPlayerAsync(ChannelReader<byte[]> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var chunk in reader.ReadAllAsync(ct))
            {
                if (_livePlayerPath is null)
                {
                    continue;
                }

                if (_livePlayer is null || _livePlayer.HasExited)
                {
                    if (_livePlayer is not null)
                    {
                        if (_liveRestarts >= MaxPlayerRestarts)
                        {
                            continue;
                        }

                        _liveRestarts++;
                        AppendLive($"The player stopped, restarting it ({_liveRestarts}/{MaxPlayerRestarts}).");
                        StopPlayer();
                    }

                    _livePlayer = Process.Start(new ProcessStartInfo
                    {
                        FileName = _livePlayerPath,
                        Arguments = BuildPlayerArguments(_livePlayerPath, _liveEmbed ? _videoHost.Handle : null),
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                    });

                    if (_liveInitSegment is not null)
                    {
                        TryWrite(_liveInitSegment);
                    }
                }

                TryWrite(chunk);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping.
        }
    }

    private void TryWrite(ReadOnlySpan<byte> data)
    {
        try
        {
            var input = _livePlayer?.StandardInput.BaseStream;
            if (input is null)
            {
                return;
            }

            input.Write(data);
            input.Flush();
        }
        catch (Exception)
        {
            // The player went away; the next fragment restarts it.
        }
    }

    /// <summary>
    /// Arguments for the external player.
    /// <para>
    /// Hardware decoding is switched off on purpose: the camera sends a fragmented MP4 that starts
    /// mid-sequence and D3D11VA fails to allocate pictures for it ("hardware acceleration picture
    /// allocation failed"), which leaves the window black. <c>--play-and-exit</c> is deliberately
    /// NOT used either, because VLC would quit on the first hiccup of the piped stream.
    /// </para>
    /// <para>
    /// When <paramref name="embedInto"/> is supplied, VLC draws inside that window instead of
    /// opening its own, which is how the picture appears on this tab.
    /// </para>
    /// </summary>
    private static string BuildPlayerArguments(string playerPath, IntPtr? embedInto)
    {
        if (playerPath.EndsWith("ffplay.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "-loglevel warning -fflags nobuffer -flags low_delay -autoexit -";
        }

        var arguments =
            "--no-plugins-cache --avcodec-hw=none --no-video-title-show --quiet " +
            "--file-caching=500 --network-caching=500";

        if (embedInto.HasValue)
        {
            arguments += $" --intf dummy --drawable-hwnd={embedInto.Value.ToInt64()}";
        }

        return arguments + " -";
    }

    private async Task StopLiveAsync()
    {
        if (_liveCts is null)
        {
            return;
        }

        await _liveCts.CancelAsync();

        _livePlayerPath = null;

        // Writing into a player that stopped reading blocks, so the player goes first.
        StopPlayer();

        if (_liveTask is not null)
        {
            try
            {
                await Task.WhenAny(_liveTask, Task.Delay(TimeSpan.FromSeconds(3)));
            }
            catch (Exception)
            {
                // Already reported by the worker.
            }
        }

        _liveCts.Dispose();
        _liveCts = null;
        _liveTask = null;
    }

    private void StopPlayer()
    {
        var player = _livePlayer;
        _livePlayer = null;

        if (player is null)
        {
            return;
        }

        try
        {
            if (!player.HasExited)
            {
                player.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // The player may have been closed by the user already.
        }
        finally
        {
            player.Dispose();
        }
    }

    private void OnLiveStatus(object? sender, string message) => AppendLive(message);

    private void AppendLive(string message)
    {
        if (IsDisposed || _shutdownStarted)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => AppendLive(message));
            }
            catch (Exception)
            {
                // Window closed.
            }

            return;
        }

        _liveLog.AppendText(Environment.NewLine + $"{DateTime.Now:HH:mm:ss}  {message}");

        // While the stream runs the status line belongs to the counter, which ticks every second.
        if (_liveCts is null)
        {
            _liveStatusLabel.Text = message;
        }
    }

    private static string? FindPlayer()
    {
        var candidates = new[]
        {
            @"C:\Program Files\VideoLAN\VLC\vlc.exe",
            @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe",
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Environment.GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator)
            .SelectMany(dir => new[] { "ffplay.exe", "vlc.exe" }.Select(name => Path.Combine(dir.Trim(), name)))
            .FirstOrDefault(File.Exists);
    }

    private Button _liveStartButton = null!;
    private Button _liveStopButton = null!;
    private CheckBox _liveWindowBox = null!;
    private CheckBox _liveSaveBox = null!;
    private Label _liveStatusLabel = null!;
    private TextBox _liveLog = null!;
    private Panel _videoHost = null!;
    private Process? _livePlayer;
    private string? _livePlayerPath;
    private bool _liveEmbed;
    private byte[]? _liveInitSegment;
    private int _liveRestarts;
    private const int MaxPlayerRestarts = 20;
    private System.Windows.Forms.Timer _liveStatsTimer = null!;
    private DateTime _liveStartedAt;
    private DateTime _liveLastTick;
    private long _liveLastBytes;
    private long _liveBytes;
    private double _liveBitsPerSecond;
    private CancellationTokenSource? _liveCts;
    private Task? _liveTask;
}
