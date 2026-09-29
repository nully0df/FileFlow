using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace FileFlow;

internal enum ButtonAppearance { Primary, Secondary, Quiet, Navigation }
internal enum Glyph { None, Folder, Scan, Arrow, Undo, Sliders, Grid, Shield }

internal sealed class FlowButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal ButtonAppearance Appearance { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Glyph Symbol { get; set; }
    private bool _hover;
    private bool _pressed;

    internal FlowButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { _pressed = _hover = false; Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f;
        var bounds = new RectangleF(1, 1, Width - 3, Height - 3);
        bool primary = Appearance == ButtonAppearance.Primary;
        bool quiet = Appearance is ButtonAppearance.Quiet or ButtonAppearance.Navigation;
        var fill = primary ? Theme.Accent : quiet ? Color.Transparent : Theme.Surface;
        var ink = primary ? (Theme.HighContrast ? SystemColors.HighlightText : Color.White) : Theme.Ink;
        if (!Enabled) { fill = primary ? Theme.Border : fill; ink = Theme.Muted; }
        else if (_pressed) fill = primary ? Theme.HighContrast ? Theme.Accent : Color.FromArgb(7, 93, 86) : Theme.AccentSoft;
        else if (_hover) fill = primary ? Theme.HighContrast ? Theme.Accent : Color.FromArgb(13, 139, 126) : Theme.AccentSoft;
        if (fill != Color.Transparent) Theme.FillRound(g, bounds, 12 * scale, fill);
        if (!quiet && !primary)
        {
            using var path = Theme.Round(bounds, 12 * scale);
            using var border = new Pen(Theme.Border);
            g.DrawPath(border, path);
        }
        bool left = Appearance == ButtonAppearance.Navigation;
        var textBounds = Rectangle.Inflate(ClientRectangle, -(int)(13 * scale), -1);
        if (Symbol != Glyph.None)
        {
            float size = 18 * scale;
            var symbolX = left ? 14 * scale : (Width - TextRenderer.MeasureText(Text, Font).Width - 28 * scale) / 2;
            if (symbolX < 10 * scale) symbolX = 10 * scale;
            FlowDrawing.Icon(g, Symbol, new RectangleF(symbolX, (Height - size) / 2, size, size), ink);
            textBounds.X = (int)(symbolX + 27 * scale);
            textBounds.Width = Width - textBounds.X - (int)(10 * scale);
            left = true;
        }
        TextRenderer.DrawText(g, Text, Font, textBounds, ink,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            (left ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
        if (Focused && ShowFocusCues)
        {
            using var focus = Theme.Round(RectangleF.Inflate(bounds, -3 * scale, -3 * scale), 9 * scale);
            using var pen = new Pen(primary ? Color.White : Theme.Accent, 1.5f) { DashStyle = DashStyle.Dot };
            g.DrawPath(pen, focus);
        }
    }
}

internal sealed class SurfacePanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color SurfaceColor { get; set; } = Theme.Surface;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color? EndColor { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int Radius { get; set; } = 20;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Outline { get; set; } = true;

    internal SurfacePanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(.5f, .5f, Width - 1.5f, Height - 1.5f);
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var path = Theme.Round(rect, Radius * DeviceDpi / 96f);
        using Brush brush = EndColor is { } end && !Theme.HighContrast
            ? new LinearGradientBrush(rect, SurfaceColor, end, 35f)
            : new SolidBrush(SurfaceColor);
        e.Graphics.FillPath(brush, path);
        if (Outline) { using var pen = new Pen(Theme.Border); e.Graphics.DrawPath(pen, path); }
    }
}

internal sealed class CategoryChip : Control
{
    internal string CategoryName { get; }
    private int? _count;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int? Count { get => _count; set { _count = value; AccessibleName = $"{CategoryName}: {value?.ToString() ?? "no preview"}"; Invalidate(); } }
    internal CategoryChip(string category)
    {
        CategoryName = category;
        Text = category;
        AccessibleName = category;
        Font = new Font("Segoe UI", 8.5f);
        Size = new Size(category is "Documents" or "Installers" ? 118 : 102, 30);
        Margin = new Padding(0, 0, 7, 0);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var (ink, tint) = Theme.HighContrast ? (Theme.Ink, Theme.Surface) : Theme.Category(CategoryName);
        Theme.FillRound(e.Graphics, new RectangleF(0, 0, Width - 1, Height - 1), 10 * DeviceDpi / 96f, tint);
        var label = _count.HasValue ? $"{CategoryName}  {_count}" : CategoryName;
        TextRenderer.DrawText(e.Graphics, label, Font, ClientRectangle, ink,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class EmptyState : Control
{
    internal EmptyState()
    {
        Dock = DockStyle.Fill; BackColor = Theme.Surface;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f;
        float center = Width / 2f;
        float top = Math.Max(12 * scale, (Height - 190 * scale) / 2);
        Theme.FillRound(g, new RectangleF(center - 38 * scale, top, 76 * scale, 76 * scale), 25 * scale, Theme.AccentSoft);
        FlowDrawing.Icon(g, Glyph.Folder, new RectangleF(center - 18 * scale, top + 20 * scale, 36 * scale, 36 * scale), Theme.Accent);
        var lines = Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        using var title = new Font("Segoe UI", 16, FontStyle.Bold);
        using var copy = new Font("Segoe UI", 10);
        TextRenderer.DrawText(g, lines.FirstOrDefault() ?? "Space for a fresh start.", title,
            new Rectangle(16, (int)(top + 88 * scale), Width - 32, (int)(35 * scale)), Theme.Ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, string.Join("\n", lines.Skip(1)), copy,
            new Rectangle(24, (int)(top + 128 * scale), Width - 48, Math.Max(1, Height - (int)(top + 128 * scale))), Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
    }
}

internal static class FlowDrawing
{
    internal static void Icon(Graphics g, Glyph glyph, RectangleF bounds, Color color)
    {
        var state = g.Save();
        g.TranslateTransform(bounds.X, bounds.Y); g.ScaleTransform(bounds.Width / 24, bounds.Height / 24);
        using var pen = new Pen(color, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (glyph)
        {
            case Glyph.Folder:
                g.DrawLines(pen, [new PointF(3, 8), new(3, 5), new(9, 5), new(12, 8), new(21, 8), new(21, 19), new(3, 19), new(3, 8)]);
                g.DrawLine(pen, 3, 10, 21, 10); break;
            case Glyph.Scan:
                g.DrawEllipse(pen, 3, 3, 12, 12); g.DrawLine(pen, 14, 14, 21, 21); break;
            case Glyph.Arrow:
                g.DrawLine(pen, 4, 12, 20, 12); g.DrawLines(pen, [new PointF(14, 6), new(20, 12), new(14, 18)]); break;
            case Glyph.Undo:
                g.DrawArc(pen, 5, 6, 15, 14, 205, 295); g.DrawLines(pen, [new PointF(3, 5), new(3, 11), new(9, 11)]); break;
            case Glyph.Sliders:
                for (int i = 0; i < 3; i++) { int y = 5 + i * 7; g.DrawLine(pen, 3, y, 21, y); using var fill = new SolidBrush(Theme.Surface); g.FillEllipse(fill, (i == 1 ? 13 : 6), y - 2, 4, 4); g.DrawEllipse(pen, (i == 1 ? 13 : 6), y - 2, 4, 4); } break;
            case Glyph.Grid:
                foreach (var p in new[] { new Point(3, 3), new(14, 3), new(3, 14), new(14, 14) })
                    using (var path = Theme.Round(new RectangleF(p.X, p.Y, 7, 7), 2)) g.DrawPath(pen, path);
                break;
            case Glyph.Shield:
                g.DrawLines(pen, [new PointF(12, 3), new(20, 6), new(19, 15), new(12, 21), new(5, 15), new(4, 6), new(12, 3)]);
                g.DrawLines(pen, [new PointF(8, 12), new(11, 15), new(16, 9)]); break;
        }
        g.Restore(state);
    }
}
