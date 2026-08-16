using System.Runtime.InteropServices;
using System.Text.Json;
using UloManager.Core;

namespace UloManager.Gui;

public sealed partial class MainForm
{
    private TabPage BuildEyesTab()
    {
        _eyesTab = new TabPage("Eyes") { Padding = new Padding(10), AutoScroll = true };

        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
        };

        root.Controls.Add(BuildEyeAppearanceGroup());
        root.Controls.Add(BuildEyeBehaviorsGroup());

        _eyesTab.Controls.Add(root);
        return _eyesTab;
    }

    // ── Eye appearance (iris colour, sizes, reflection) ──────────────

    private Control BuildEyeAppearanceGroup()
    {
        var group = new GroupBox
        {
            Text = "Eye Appearance",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(640, 0),
            Padding = new Padding(10, 6, 10, 12),
            Margin = new Padding(0, 0, 0, 12),
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Iris hue (0–359, with 360 as optional "black")
        grid.Controls.Add(new Label { Text = "Iris colour (hue)", AutoSize = true }, 0, 0);
        _eyeHueSlider = new TrackBar { Minimum = 0, Maximum = 359, TickFrequency = 30, Width = 300 };
        _eyeHueLabel = new Label { AutoSize = true, Text = "0" };
        _eyeHueSlider.Scroll += (_, _) =>
        {
            if (!_eyeHueBlackBox.Checked)
            {
                _lastNonBlackHue = _eyeHueSlider.Value;
                _eyeHueLabel.Text = _eyeHueSlider.Value.ToString();
                UpdateHuePreview();
            }
        };
        var hueFlow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };

        _eyeHueBlackBox = new CheckBox
        {
            Text = "Black",
            AutoSize = true,
            Margin = new Padding(10, 5, 0, 0),
        };
        _eyeHueBlackBox.CheckedChanged += (_, _) =>
        {
            if (_suppressHueChangeEvents)
            {
                return;
            }

            if (_eyeHueBlackBox.Checked)
            {
                _lastNonBlackHue = _eyeHueSlider.Value;
                _eyeHueSlider.Enabled = false;
                _eyeHueLabel.Text = "360";
                UpdateHuePreview();
                return;
            }

            _eyeHueSlider.Enabled = true;
            _eyeHueSlider.Value = Math.Clamp(_lastNonBlackHue, _eyeHueSlider.Minimum, _eyeHueSlider.Maximum);
            _eyeHueLabel.Text = _eyeHueSlider.Value.ToString();
            UpdateHuePreview();
        };

        // Row 1: the slider itself, its value and the black switch.
        _eyeHueSlider.Margin = new Padding(0);
        hueFlow.Controls.Add(_eyeHueSlider);
        hueFlow.Controls.Add(_eyeHueLabel);
        hueFlow.Controls.Add(_eyeHueBlackBox);

        // Row 2: the colour guide directly under the slider, with the current colour
        // beside it. The strip spans the slider width; PaintHueGuide insets the gradient
        // so hue positions line up with the thumb rather than with the control edges.
        _eyeHueGuide = new Panel
        {
            Width = _eyeHueSlider.Width,
            Height = 14,
            Margin = new Padding(0),
        };
        _eyeHueGuide.Paint += (_, e) => PaintHueGuide(e.Graphics, _eyeHueGuide.ClientRectangle, _eyeHueSlider);
        _eyeHueGuide.Resize += (_, _) => _eyeHueGuide.Invalidate();

        _eyeHuePreview = new Panel
        {
            Width = 26,
            Height = 14,
            Margin = new Padding(6, 0, 0, 0),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.Red,
        };

        var guideRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
        guideRow.Controls.Add(_eyeHueGuide);
        guideRow.Controls.Add(_eyeHuePreview);

        var hueStack = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        hueStack.Controls.Add(hueFlow, 0, 0);
        hueStack.Controls.Add(guideRow, 0, 1);

        grid.Controls.Add(hueStack, 1, 0);

        SetSelectedIrisHue(0);

        // Iris size
        grid.Controls.Add(new Label { Text = "Iris size", AutoSize = true }, 0, 1);
        _eyeIrisSpin = new NumericUpDown { Minimum = 0, Maximum = 100, Width = 70 };
        grid.Controls.Add(_eyeIrisSpin, 1, 1);

        // Pupil size
        grid.Controls.Add(new Label { Text = "Pupil size", AutoSize = true }, 0, 2);
        _eyePupilSpin = new NumericUpDown { Minimum = 0, Maximum = 100, Width = 70 };
        grid.Controls.Add(_eyePupilSpin, 1, 2);

        // Reflection
        grid.Controls.Add(new Label { Text = "Reflection", AutoSize = true }, 0, 3);
        _eyeReflectionBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        _eyeReflectionBox.Items.AddRange(new object[] { "none", "triangle", "circles", "rectangle" });
        grid.Controls.Add(_eyeReflectionBox, 1, 3);

        // Buttons
        var btnFlow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };

        var loadBtn = new Button { Text = "Load from camera", Width = 160 };
        loadBtn.Click += async (_, _) => await RunAsync("Loading eye settings", LoadEyeSettingsAsync);
        btnFlow.Controls.Add(loadBtn);

        var applyBtn = new Button { Text = "Apply to camera", Width = 160 };
        applyBtn.Click += async (_, _) => await RunAsync("Applying eye settings", async ct =>
        {
            var body = JsonSerializer.Serialize(new
            {
                irisHue = GetSelectedIrisHue(),
                irisSize = (int)_eyeIrisSpin.Value,
                pupilSize = (int)_eyePupilSpin.Value,
                reflection = (string)_eyeReflectionBox.SelectedItem!,
            }, UloJson.Options);

            await RequireDevice().Client.SendJsonAsync(
                new HttpMethod("PATCH"), "/api/v1/config/eyes", body, ct);
        });
        btnFlow.Controls.Add(applyBtn);

        grid.Controls.Add(btnFlow, 1, 4);

        group.Controls.Add(grid);
        return group;
    }

    private async Task LoadEyeSettingsAsync(CancellationToken ct)
    {
        var eyes = await RequireDevice().Client.GetAsync<UloEyesConfig>("/api/v1/config/eyes", ct);
        if (eyes is null) return;

        void Apply()
        {
            SetSelectedIrisHue(eyes.IrisHue);
            _eyeIrisSpin.Value = Math.Clamp(eyes.IrisSize, 0, 100);
            _eyePupilSpin.Value = Math.Clamp(eyes.PupilSize, 0, 100);
            var idx = _eyeReflectionBox.Items.IndexOf(eyes.Reflection);
            _eyeReflectionBox.SelectedIndex = idx >= 0 ? idx : 0;
        }

        if (InvokeRequired) BeginInvoke(Apply); else Apply();

        try { await LoadBehaviorsAsync(ct); } catch { /* non-critical */ }
    }

    // ── Face-recognition behaviors (expression per user) ─────────────

    private Control BuildEyeBehaviorsGroup()
    {
        var group = new GroupBox
        {
            Text = "Behaviors (expression on face recognition)",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(640, 0),
            Padding = new Padding(10, 6, 10, 12),
            Margin = new Padding(0, 0, 0, 12),
        };

        _behaviorsView = new ListView
        {
            Dock = DockStyle.Top,
            Height = 120,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
        };
        _behaviorsView.Columns.Add("Id", 40);
        _behaviorsView.Columns.Add("User Id", 60);
        _behaviorsView.Columns.Add("Expression", 120);
        FillLastColumn(_behaviorsView);

        var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };

        var reload = new Button { Text = "Reload", Width = 90 };
        reload.Click += async (_, _) => await RunAsync("Loading behaviors", LoadBehaviorsAsync);
        btnFlow.Controls.Add(reload);

        // Add behavior
        _behaviorUserSpin = new NumericUpDown { Minimum = 1, Maximum = 100, Width = 55, Value = 1 };
        _behaviorExpressionBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        _behaviorExpressionBox.Items.AddRange(new object[] { "happy", "unhappy", "surprised" });
        _behaviorExpressionBox.SelectedIndex = 0;

        btnFlow.Controls.Add(new Label { Text = "User:", AutoSize = true, Margin = new Padding(8, 5, 2, 0) });
        btnFlow.Controls.Add(_behaviorUserSpin);
        btnFlow.Controls.Add(new Label { Text = "Expression:", AutoSize = true, Margin = new Padding(8, 5, 2, 0) });
        btnFlow.Controls.Add(_behaviorExpressionBox);

        var addBtn = new Button { Text = "Add", Width = 70 };
        addBtn.Click += async (_, _) => await RunAsync("Adding behavior", async ct =>
        {
            var body = JsonSerializer.Serialize(new
            {
                expression = (string)_behaviorExpressionBox.SelectedItem!,
                user = (int)_behaviorUserSpin.Value,
            }, UloJson.Options);

            await RequireDevice().Client.SendJsonAsync(HttpMethod.Post, "/api/v1/behaviors", body, ct);
            await LoadBehaviorsAsync(ct);
        });
        btnFlow.Controls.Add(addBtn);

        var deleteBtn = new Button { Text = "Delete", Width = 70 };
        deleteBtn.Click += async (_, _) => await RunAsync("Deleting behavior", async ct =>
        {
            if (_behaviorsView.SelectedItems.Count == 0)
            {
                throw new InvalidOperationException("Select a behavior first.");
            }

            var id = _behaviorsView.SelectedItems[0].Text;
            await RequireDevice().Client.SendAsync(HttpMethod.Delete, $"/api/v1/behaviors/{id}", null, ct);
            await LoadBehaviorsAsync(ct);
        });
        btnFlow.Controls.Add(deleteBtn);

        group.Controls.Add(btnFlow);
        group.Controls.Add(_behaviorsView);
        return group;
    }

    private async Task LoadBehaviorsAsync(CancellationToken ct)
    {
        var json = await RequireDevice().Client.GetJsonAsync("/api/v1/behaviors", ct);
        var behaviors = json?["behaviors"];

        void Apply()
        {
            _behaviorsView.BeginUpdate();
            _behaviorsView.Items.Clear();

            if (behaviors is not null)
            {
                foreach (var b in behaviors.AsArray())
                {
                    if (b is null) continue;
                    _behaviorsView.Items.Add(new ListViewItem(new[]
                    {
                        b["id"]?.ToString() ?? "",
                        b["user"]?.ToString() ?? "",
                        b["expression"]?.ToString() ?? "",
                    }));
                }
            }

            _behaviorsView.EndUpdate();
        }

        if (InvokeRequired) BeginInvoke(Apply); else Apply();
    }

    // ── Fields ───────────────────────────────────────────────────────

    private TabPage _eyesTab = null!;
    private TrackBar _eyeHueSlider = null!;
    private Label _eyeHueLabel = null!;
    private Panel _eyeHueGuide = null!;
    private Panel _eyeHuePreview = null!;
    private CheckBox _eyeHueBlackBox = null!;
    private NumericUpDown _eyeIrisSpin = null!;
    private NumericUpDown _eyePupilSpin = null!;
    private ComboBox _eyeReflectionBox = null!;
    private ListView _behaviorsView = null!;
    private NumericUpDown _behaviorUserSpin = null!;
    private ComboBox _behaviorExpressionBox = null!;
    private bool _suppressHueChangeEvents;
    private int _lastNonBlackHue = 359;

    // A TrackBar does not paint its channel across its whole width: the thumb travels
    // between fixed insets, so a gradient drawn edge to edge does not line up with the
    // slider positions. The control itself knows where the channel and thumb are, so ask
    // it rather than guessing - that also keeps the strip correct at any DPI or theme.
    private const int WM_USER = 0x0400;
    private const int TBM_GETTHUMBRECT = WM_USER + 25;
    private const int TBM_GETCHANNELRECT = WM_USER + 26;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref NativeRect lParam);

    /// <summary>
    /// The horizontal span the thumb centre covers, in client coordinates of the slider.
    /// Falls back to the full width while the handle does not exist yet.
    /// </summary>
    private static (int Left, int Right) GetSliderTravel(TrackBar slider)
    {
        if (!slider.IsHandleCreated)
        {
            return (0, Math.Max(0, slider.Width - 1));
        }

        var channel = default(NativeRect);
        SendMessage(slider.Handle, TBM_GETCHANNELRECT, IntPtr.Zero, ref channel);

        var thumb = default(NativeRect);
        SendMessage(slider.Handle, TBM_GETTHUMBRECT, IntPtr.Zero, ref thumb);

        var half = Math.Max(0, thumb.Right - thumb.Left) / 2;
        var left = channel.Left + half;
        var right = channel.Right - half;

        return right > left ? (left, right) : (0, Math.Max(0, slider.Width - 1));
    }

    private static void PaintHueGuide(Graphics graphics, Rectangle bounds, TrackBar slider)
    {
        if (bounds.Width <= 1 || bounds.Height <= 0)
        {
            return;
        }

        var (left, right) = GetSliderTravel(slider);
        left = Math.Clamp(left, 0, bounds.Width - 1);
        right = Math.Clamp(right, 0, bounds.Width - 1);

        if (right <= left)
        {
            return;
        }

        for (var x = left; x <= right; x++)
        {
            var hue = 359d * (x - left) / (right - left);
            using var pen = new Pen(ColorFromHue(hue));
            graphics.DrawLine(pen, x, 0, x, bounds.Height - 1);
        }

        using var border = new Pen(Color.FromArgb(90, 90, 90));
        graphics.DrawRectangle(border, left, 0, right - left, bounds.Height - 1);
    }

    private static Color ColorFromHue(double hue)
    {
        var sector = (hue % 360d) / 60d;
        var index = (int)Math.Floor(sector);
        var f = sector - index;
        const double v = 1d;
        var p = 0d;
        var q = v * (1d - f);
        var t = v * f;

        (double r, double g, double b) = index switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };

        return Color.FromArgb(
            (int)Math.Round(r * 255d),
            (int)Math.Round(g * 255d),
            (int)Math.Round(b * 255d));
    }

    private int GetSelectedIrisHue() => _eyeHueBlackBox.Checked ? 360 : _eyeHueSlider.Value;

    private void SetSelectedIrisHue(int hue)
    {
        _suppressHueChangeEvents = true;
        try
        {
            var isBlack = hue >= 360;
            _eyeHueBlackBox.Checked = isBlack;
            _eyeHueSlider.Enabled = !isBlack;
            _eyeHueSlider.Value = isBlack
                ? _eyeHueSlider.Maximum
                : Math.Clamp(hue, _eyeHueSlider.Minimum, _eyeHueSlider.Maximum);
            if (!isBlack)
            {
                _lastNonBlackHue = _eyeHueSlider.Value;
            }
            _eyeHueLabel.Text = isBlack ? "360" : _eyeHueSlider.Value.ToString();
            UpdateHuePreview();
        }
        finally
        {
            _suppressHueChangeEvents = false;
        }
    }

    private void UpdateHuePreview()
    {
        var hue = GetSelectedIrisHue();
        _eyeHuePreview.BackColor = hue >= 360 ? Color.Black : ColorFromHue(hue);
    }
}
