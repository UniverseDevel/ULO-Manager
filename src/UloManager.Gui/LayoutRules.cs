namespace UloManager.Gui;

/// <summary>
/// One pass over the finished window that makes every button and field the same height and lines
/// them up with the labels beside them.
///
/// <para>
/// The tabs were each built with their own margins, so a button sitting next to a label ended up a
/// few pixels higher than the one below it, and buttons whose height came from auto-sizing clipped
/// their captions at the bottom. Rather than hand-tune every panel, the rules live here and are
/// applied to the whole control tree once the layout exists.
/// </para>
/// </summary>
internal static class LayoutRules
{
    /// <summary>Height every free-standing button gets, tall enough that no caption is clipped.</summary>
    private const int ButtonHeight = 26;

    /// <summary>Height of a text box, combo or spinner - what Windows gives them by default.</summary>
    private const int FieldHeight = 23;

    public static void Normalise(Control root)
    {
        foreach (Control child in root.Controls)
        {
            Apply(child);
            Normalise(child);
        }
    }

    private static void Apply(Control control)
    {
        // Docked or anchored-to-fill controls are laid out by their container; leave them alone.
        if (control.Dock != DockStyle.None)
        {
            return;
        }

        switch (control)
        {
            case Button button:
                button.AutoSize = false;
                button.Height = ButtonHeight;
                button.Margin = new Padding(button.Margin.Left, 3, button.Margin.Right, 3);
                button.TextAlign = ContentAlignment.MiddleCenter;
                break;

            case TextBoxBase or ComboBox or NumericUpDown or DateTimePicker:
                control.Margin = new Padding(
                    control.Margin.Left,
                    3 + ((ButtonHeight - FieldHeight) / 2),
                    control.Margin.Right,
                    3);
                break;

            case CheckBox or RadioButton:
                // Centre the box on the same line as the buttons and fields next to it.
                control.Margin = new Padding(
                    control.Margin.Left,
                    3 + ((ButtonHeight - control.PreferredSize.Height) / 2),
                    control.Margin.Right,
                    3);
                break;

            case Label label when label.Parent is FlowLayoutPanel or TableLayoutPanel:
                label.Margin = new Padding(
                    label.Margin.Left,
                    3 + ((ButtonHeight - label.PreferredSize.Height) / 2),
                    label.Margin.Right,
                    3);
                break;
        }
    }
}
