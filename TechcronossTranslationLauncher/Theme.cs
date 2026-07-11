using System.Drawing.Drawing2D;

namespace TechcronossTranslationLauncher;

internal static class Theme
{
    internal static readonly Color Navy = Color.FromArgb(25, 55, 91);
    internal static readonly Color Cyan = Color.FromArgb(64, 196, 235);
    internal static readonly Color Mint = Color.FromArgb(114, 225, 194);
    internal static readonly Color Pink = Color.FromArgb(245, 110, 163);
    internal static readonly Color Paper = Color.FromArgb(249, 253, 255);

    internal static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class GameButton : Button
{
    internal Color Accent { get; set; } = Theme.Cyan;

    internal GameButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.White;
        ForeColor = Theme.Navy;
        Cursor = Cursors.Hand;
        Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
        Size = new Size(196, 58);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = Theme.RoundedRectangle(bounds, 8);
        using var fill = new SolidBrush(Enabled ? BackColor : Color.Gainsboro);
        using var border = new Pen(Accent, 3);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            bounds,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
        );
    }
}
