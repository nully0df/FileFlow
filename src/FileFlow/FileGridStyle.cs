using System.Drawing.Drawing2D;

namespace FileFlow;

internal static class FileGridStyle
{
    internal static void Attach(DataGridView grid)
    {
        grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is null) return;
            var row = grid.Rows[e.RowIndex];
            var scale = grid.DeviceDpi / 96f;
            var g = e.Graphics; var state = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias;
            if (e.ColumnIndex == 0)
            {
                e.PaintBackground(e.ClipBounds, true);
                float side = 17 * scale;
                var box = new RectangleF(e.CellBounds.X + (e.CellBounds.Width - side) / 2, e.CellBounds.Y + (e.CellBounds.Height - side) / 2, side, side);
                bool selected = e.Value is true;
                using var path = Theme.Round(box, 5 * scale);
                using var fill = new SolidBrush(selected ? Theme.Accent : Theme.Surface);
                using var border = new Pen(row.ReadOnly ? Theme.Border : Theme.Muted, 1);
                g.FillPath(fill, path);
                if (!selected) g.DrawPath(border, path);
                else
                {
                    using var pen = new Pen(Theme.HighContrast ? SystemColors.HighlightText : Color.White, 1.6f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLines(pen, [new PointF(box.X + 4 * scale, box.Y + 8 * scale), new(box.X + 7 * scale, box.Y + 11 * scale), new(box.X + 13 * scale, box.Y + 5 * scale)]);
                }
                e.Handled = true;
            }
            else if (e.ColumnIndex == 1)
            {
                e.PaintBackground(e.ClipBounds, true);
                var category = Convert.ToString(row.Cells[2].Value) ?? "";
                var (ink, tint) = Theme.HighContrast ? (Theme.Ink, Theme.Surface) : Theme.Category(category);
                var icon = new RectangleF(e.CellBounds.X + 10 * scale, e.CellBounds.Y + (e.CellBounds.Height - 32 * scale) / 2, 28 * scale, 32 * scale);
                Theme.FillRound(g, icon, 7 * scale, tint);
                using var pen = new Pen(ink, 1.2f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                var page = new RectangleF(icon.X + 8 * scale, icon.Y + 7 * scale, 12 * scale, 18 * scale);
                using var path = Theme.Round(page, 2 * scale); g.DrawPath(pen, path);
                g.DrawLine(pen, page.X + 3 * scale, page.Y + 7 * scale, page.Right - 3 * scale, page.Y + 7 * scale);
                g.DrawLine(pen, page.X + 3 * scale, page.Y + 11 * scale, page.Right - 3 * scale, page.Y + 11 * scale);
                var text = e.CellBounds; text.X += (int)(47 * scale); text.Width -= (int)(51 * scale);
                TextRenderer.DrawText(g, Convert.ToString(e.Value), e.CellStyle!.Font ?? grid.Font, text, row.ReadOnly ? Theme.Muted : Theme.Ink,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                e.Handled = true;
            }
            else if (e.ColumnIndex == 5)
            {
                e.PaintBackground(e.ClipBounds, true);
                var value = Convert.ToString(e.Value) ?? "";
                var ink = value == "New name" ? Color.FromArgb(145, 100, 35) : value == "Ready" ? Theme.Accent : Theme.Muted;
                var tint = value == "New name" ? Color.FromArgb(252, 243, 219) : value == "Ready" ? Theme.AccentSoft : Theme.Background;
                if (Theme.HighContrast) { ink = Theme.Ink; tint = Theme.Surface; }
                var badge = new RectangleF(e.CellBounds.X + 7 * scale, e.CellBounds.Y + (e.CellBounds.Height - 25 * scale) / 2,
                    Math.Min(e.CellBounds.Width - 14 * scale, 85 * scale), 25 * scale);
                Theme.FillRound(g, badge, 8 * scale, tint);
                using var font = new Font("Segoe UI", 8.5f);
                TextRenderer.DrawText(g, value, font, Rectangle.Round(badge), ink, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                e.Handled = true;
            }
            g.Restore(state);
        };
    }
}
