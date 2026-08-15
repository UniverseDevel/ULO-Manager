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

        // Iris hue (0–360)
        grid.Controls.Add(new Label { Text = "Iris colour (hue)", AutoSize = true }, 0, 0);
        _eyeHueSlider = new TrackBar { Minimum = 0, Maximum = 360, TickFrequency = 30, Width = 300 };
        _eyeHueLabel = new Label { AutoSize = true, Text = "0" };
        _eyeHueSlider.Scroll += (_, _) => _eyeHueLabel.Text = _eyeHueSlider.Value.ToString();
        var hueFlow = new FlowLayoutPanel { AutoSize = true };
        hueFlow.Controls.Add(_eyeHueSlider);
        hueFlow.Controls.Add(_eyeHueLabel);
        grid.Controls.Add(hueFlow, 1, 0);

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
                irisHue = _eyeHueSlider.Value,
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
            _eyeHueSlider.Value = Math.Clamp(eyes.IrisHue, 0, 360);
            _eyeHueLabel.Text = eyes.IrisHue.ToString();
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
    private NumericUpDown _eyeIrisSpin = null!;
    private NumericUpDown _eyePupilSpin = null!;
    private ComboBox _eyeReflectionBox = null!;
    private ListView _behaviorsView = null!;
    private NumericUpDown _behaviorUserSpin = null!;
    private ComboBox _behaviorExpressionBox = null!;
}
