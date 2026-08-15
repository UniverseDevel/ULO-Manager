namespace UloManager.Gui;

/// <summary>
/// Theme helpers. The application follows the Windows app theme (see <see cref="Program"/>), so
/// anything drawn by hand has to pick its colours from the current system palette instead of
/// assuming a light window.
/// </summary>
internal static class Theme
{
    /// <summary>How the application picks its colours.</summary>
    public enum Mode
    {
        /// <summary>Follow the Windows app theme (the default).</summary>
        System,

        Light,

        Dark,
    }

    /// <summary>The preference in force, set from the saved settings at start-up.</summary>
    public static Mode Preference { get; set; } = Mode.System;

    /// <summary>True while the application is painting itself dark.</summary>
    public static bool IsDark => Preference switch
    {
        Mode.Light => false,
        Mode.Dark => true,
        _ => Luminance(SystemColors.Window) < 0.5,
    };

    /// <summary>Reads a stored preference, falling back to following the system.</summary>
    public static Mode Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "light" => Mode.Light,
        "dark" => Mode.Dark,
        _ => Mode.System,
    };

    /// <summary>Background of the row belonging to the camera currently in use.</summary>
    public static Color ActiveRow => Blend(ListBackground, Selection, IsDark ? 0.35f : 0.12f);

    /// <summary>Secondary text, for the detail lines under a title.</summary>
    public static Color SecondaryText => IsDark ? Color.FromArgb(168, 168, 168) : SystemColors.GrayText;

    /// <summary>Colour of an activity line, readable on both a light and a dark background.</summary>
    public static Color Severity(UloManager.Core.UloLogSeverity severity) => severity switch
    {
        UloManager.Core.UloLogSeverity.Error => IsDark ? Color.FromArgb(255, 116, 108) : Color.Firebrick,
        UloManager.Core.UloLogSeverity.Warning => IsDark ? Color.FromArgb(255, 176, 70) : Color.DarkOrange,
        UloManager.Core.UloLogSeverity.Notice => IsDark ? Color.FromArgb(120, 176, 255) : Color.MediumBlue,
        _ => SystemColors.WindowText,
    };

    /// <summary>Mixes two colours, <paramref name="amount"/> being how much of <paramref name="over"/> to use.</summary>
    public static Color Blend(Color under, Color over, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(under.R + ((over.R - under.R) * amount)),
            (int)(under.G + ((over.G - under.G) * amount)),
            (int)(under.B + ((over.B - under.B) * amount)));
    }

    // ── Explicit dark palette ─────────────────────────────────────────────
    //
    // .NET 9's dark mode is still a preview and only reaches part of Windows Forms: text boxes,
    // lists, group boxes, spinners and buttons keep their light colours, which leaves white blocks
    // and unreadable text. Everything below is therefore painted by hand when Windows is dark.

    private static readonly Color Surface = Color.FromArgb(32, 32, 32);
    private static readonly Color Field = Color.FromArgb(45, 45, 48);
    private static readonly Color TextColour = Color.FromArgb(240, 240, 240);
    private static readonly Color Border = Color.FromArgb(70, 70, 74);

    /// <summary>Background of the selected row in a hand-drawn list.</summary>
    public static Color Selection => IsDark ? Color.FromArgb(38, 79, 120) : SystemColors.Highlight;

    /// <summary>Text on <see cref="Selection"/>. The system colour is not reliable in dark mode.</summary>
    public static Color SelectionText => IsDark ? Color.White : SystemColors.HighlightText;

    /// <summary>Normal text in a hand-drawn list.</summary>
    public static Color PrimaryText => IsDark ? TextColour : SystemColors.WindowText;

    /// <summary>Background of a hand-drawn list.</summary>
    public static Color ListBackground => IsDark ? Field : SystemColors.Window;

    // Tab strip, drawn by ThemedTabControl because the OS renderer keeps it light.
    public static Color TabStrip => IsDark ? Surface : SystemColors.Control;

    public static Color TabPage => IsDark ? Color.FromArgb(40, 40, 42) : SystemColors.Control;

    public static Color TabBorder => IsDark ? Border : SystemColors.ControlDark;

    public static Color TabText => IsDark ? Color.FromArgb(200, 200, 200) : SystemColors.ControlText;

    public static Color TabTextSelected => IsDark ? Color.White : SystemColors.ControlText;

    /// <summary>Paints a whole control tree with the dark palette. Does nothing on a light system.</summary>
    public static void Apply(Control root)
    {
        if (!IsDark || root is null)
        {
            return;
        }

        // Menus, status strips and their dropdowns are drawn by the ToolStrip renderer, which is
        // light by default - on hover it paints a pale highlight that swallows light text.
        ToolStripManager.Renderer = new DarkToolStripRenderer();

        ApplyToControl(root);

        foreach (Control child in root.Controls)
        {
            Apply(child);
        }

        // Controls added later (tab contents built on demand, dialogs) need the same treatment.
        root.ControlAdded -= OnControlAdded;
        root.ControlAdded += OnControlAdded;
    }

    private static void OnControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control is not null)
        {
            Apply(e.Control);
        }
    }

    private static void ApplyToControl(Control control)
    {
        switch (control)
        {
            case TextBoxBase or ListBox or ListView or TreeView or NumericUpDown or DateTimePicker:
                control.BackColor = Field;
                control.ForeColor = TextColour;
                break;

            case ComboBox combo:
                combo.BackColor = Field;
                combo.ForeColor = TextColour;
                combo.FlatStyle = FlatStyle.Flat;
                break;

            case Button button:
                button.BackColor = Field;
                button.ForeColor = TextColour;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Blend(Field, Color.White, 0.12f);
                button.FlatAppearance.MouseDownBackColor = Blend(Field, Color.Black, 0.2f);
                PaintDisabledText(button);
                break;

            case CheckBox or RadioButton or Label or LinkLabel or GroupBox:
                control.ForeColor = TextColour;
                break;

            case StatusStrip strip:
                strip.BackColor = Surface;
                strip.ForeColor = TextColour;
                break;

            case TabControl tabs:
                tabs.BackColor = Surface;
                tabs.ForeColor = TextColour;
                break;

            case PictureBox:
                // Keeps its own dark backdrop.
                return;

            default:
                control.BackColor = Surface;
                control.ForeColor = TextColour;
                break;
        }

        // A list view keeps a bright header strip unless it is drawn by hand.
        if (control is ListView list && list.View == View.Details)
        {
            DarkenHeader(list);
        }
    }

    /// <summary>
    /// Windows draws the caption of a disabled button in a dark grey that is meant for a light
    /// window - on the dark palette it comes out almost black. The caption is therefore repainted
    /// afterwards in a grey that can actually be read.
    /// </summary>
    private static void PaintDisabledText(Button button)
    {
        button.EnabledChanged += (_, _) => button.Invalidate();
        button.Paint += (_, e) =>
        {
            if (button.Enabled)
            {
                return;
            }

            var face = Blend(Field, Surface, 0.5f);
            using (var background = new SolidBrush(face))
            {
                e.Graphics.FillRectangle(background, button.ClientRectangle);
            }

            using (var border = new Pen(Blend(Border, Surface, 0.4f)))
            {
                e.Graphics.DrawRectangle(border, 0, 0, button.Width - 1, button.Height - 1);
            }

            TextRenderer.DrawText(
                e.Graphics,
                button.Text,
                button.Font,
                button.ClientRectangle,
                Color.FromArgb(150, 150, 150),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };
    }

    private static void DarkenHeader(ListView list)
    {
        if (list.OwnerDraw)
        {
            return;
        }

        list.OwnerDraw = true;

        list.DrawColumnHeader += (_, e) =>
        {
            using var background = new SolidBrush(Surface);
            e.Graphics.FillRectangle(background, e.Bounds);

            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            TextRenderer.DrawText(
                e.Graphics,
                e.Header?.Text ?? "",
                e.Font ?? list.Font,
                Rectangle.Inflate(e.Bounds, -4, 0),
                TextColour,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        };

        // Rows keep the normal rendering, including the per-item colours used by the activity log.
        list.DrawItem += (_, e) => e.DrawDefault = true;
        list.DrawSubItem += (_, e) => e.DrawDefault = true;
    }

    /// <summary>
    /// Applies the dark palette to a strip and everything it drops down. Setting
    /// <see cref="ToolStripManager.Renderer"/> alone is not enough: .NET's own dark mode puts the
    /// strip into system render mode, which ignores the manager's renderer and paints the hover
    /// highlight in a pale system colour that swallows light text.
    /// </summary>
    public static void ApplyToolStrip(ToolStrip strip)
    {
        if (!IsDark || strip is null)
        {
            return;
        }

        strip.RenderMode = ToolStripRenderMode.Professional;
        strip.Renderer = new DarkToolStripRenderer();
        strip.BackColor = Surface;
        strip.ForeColor = TextColour;

        foreach (ToolStripItem item in strip.Items)
        {
            ApplyToolStripItem(item);
        }
    }

    private static void ApplyToolStripItem(ToolStripItem item)
    {
        item.BackColor = Surface;
        item.ForeColor = TextColour;

        if (item is not ToolStripDropDownItem parent)
        {
            return;
        }

        parent.DropDown.RenderMode = ToolStripRenderMode.Professional;
        parent.DropDown.Renderer = new DarkToolStripRenderer();
        parent.DropDown.BackColor = Surface;
        parent.DropDown.ForeColor = TextColour;

        foreach (ToolStripItem child in parent.DropDownItems)
        {
            ApplyToolStripItem(child);
        }
    }

    private static double Luminance(Color colour)
        => ((0.299 * colour.R) + (0.587 * colour.G) + (0.114 * colour.B)) / 255.0;
}

/// <summary>
/// Dark colours for status strips, menus and their dropdowns. The stock renderer highlights a
/// hovered item with a pale wash, which makes the light caption on it unreadable.
/// </summary>
internal sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
{
    public DarkToolStripRenderer()
        : base(new DarkColourTable())
    {
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item?.Selected == true || e.Item?.Pressed == true
            ? Color.White
            : Color.FromArgb(232, 232, 232);

        base.OnRenderItemText(e);
    }

    // The hover and pressed backgrounds are painted here rather than left to the colour table, so
    // they cannot fall back to a pale system highlight that hides the light caption.
    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        => PaintItemBackground(e);

    protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        => PaintItemBackground(e);

    protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
        => PaintItemBackground(e);

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        => PaintItemBackground(e);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var background = new SolidBrush(Color.FromArgb(32, 32, 32));
        e.Graphics.FillRectangle(background, e.AffectedBounds);
    }

    private static void PaintItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        var hot = item.Selected || item.Pressed;

        using var fill = new SolidBrush(hot ? Color.FromArgb(56, 84, 118) : Color.FromArgb(32, 32, 32));
        e.Graphics.FillRectangle(fill, new Rectangle(Point.Empty, item.Size));

        if (!hot)
        {
            return;
        }

        using var border = new Pen(Color.FromArgb(96, 128, 168));
        e.Graphics.DrawRectangle(border, 0, 0, item.Width - 1, item.Height - 1);
    }

    private sealed class DarkColourTable : ProfessionalColorTable
    {
        private static readonly Color Surface = Color.FromArgb(32, 32, 32);
        private static readonly Color Hover = Color.FromArgb(56, 84, 118);
        private static readonly Color Edge = Color.FromArgb(80, 80, 84);

        public override Color MenuItemSelected => Hover;

        public override Color MenuItemSelectedGradientBegin => Hover;

        public override Color MenuItemSelectedGradientEnd => Hover;

        public override Color MenuItemPressedGradientBegin => Hover;

        public override Color MenuItemPressedGradientMiddle => Hover;

        public override Color MenuItemPressedGradientEnd => Hover;

        public override Color MenuItemBorder => Edge;

        public override Color MenuBorder => Edge;

        public override Color ButtonSelectedHighlight => Hover;

        public override Color ButtonSelectedHighlightBorder => Edge;

        public override Color ButtonPressedHighlight => Hover;

        public override Color ButtonSelectedGradientBegin => Hover;

        public override Color ButtonSelectedGradientMiddle => Hover;

        public override Color ButtonSelectedGradientEnd => Hover;

        public override Color ToolStripDropDownBackground => Surface;

        public override Color ImageMarginGradientBegin => Surface;

        public override Color ImageMarginGradientMiddle => Surface;

        public override Color ImageMarginGradientEnd => Surface;

        public override Color SeparatorDark => Edge;

        public override Color SeparatorLight => Edge;
    }
}
