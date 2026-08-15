namespace UloManager.Gui;

/// <summary>
/// A tab control that can paint itself.
///
/// <para>
/// Windows Forms hands the tab strip to the operating system's theme renderer, which keeps drawing
/// it light no matter what <see cref="Control.BackColor"/> says - so on a dark desktop the tabs stay
/// a white band above dark pages. When <see cref="Theme.IsDark"/> is set, the strip, the tabs and
/// the border around the page are drawn here instead.
/// </para>
/// </summary>
internal sealed class ThemedTabControl : TabControl
{
    public ThemedTabControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!Theme.IsDark)
        {
            base.OnPaintBackground(e);
            return;
        }

        using var background = new SolidBrush(Theme.TabStrip);
        e.Graphics.FillRectangle(background, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!Theme.IsDark)
        {
            base.OnPaint(e);
            return;
        }

        var graphics = e.Graphics;

        using (var background = new SolidBrush(Theme.TabStrip))
        {
            graphics.FillRectangle(background, ClientRectangle);
        }

        // The page itself, plus a hairline so the content is separated from the strip.
        var page = DisplayRectangle;
        using (var pageBrush = new SolidBrush(Theme.TabPage))
        {
            graphics.FillRectangle(pageBrush, Rectangle.Inflate(page, 2, 2));
        }

        using (var border = new Pen(Theme.TabBorder))
        {
            graphics.DrawRectangle(border, Rectangle.Inflate(page, 2, 2));
        }

        for (var index = 0; index < TabCount; index++)
        {
            DrawTab(graphics, index);
        }
    }

    private void DrawTab(Graphics graphics, int index)
    {
        var bounds = GetTabRect(index);
        var selected = SelectedIndex == index;
        var page = TabPages[index];

        using (var fill = new SolidBrush(selected ? Theme.TabPage : Theme.TabStrip))
        {
            graphics.FillRectangle(fill, bounds);
        }

        using (var border = new Pen(Theme.TabBorder))
        {
            graphics.DrawRectangle(border, bounds.Left, bounds.Top, bounds.Width, bounds.Height);

            if (selected)
            {
                // Erase the line between the selected tab and its page.
                using var joint = new Pen(Theme.TabPage);
                graphics.DrawLine(joint, bounds.Left + 1, bounds.Bottom, bounds.Right - 1, bounds.Bottom);
            }
        }

        // A tab still waiting for its data is marked with the loading suffix; grey it so the wait
        // is visible without making the label unreadable.
        var loading = page.Text.EndsWith(" ...", StringComparison.Ordinal);
        var colour = loading
            ? Theme.SecondaryText
            : selected
                ? Theme.TabTextSelected
                : Theme.TabText;

        TextRenderer.DrawText(
            graphics,
            page.Text,
            Font,
            bounds,
            colour,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
