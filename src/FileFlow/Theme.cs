using System.Drawing.Drawing2D;

namespace FileFlow;

// Stillwater: FileFlow's shared palette, type and drawing primitives.
internal static class Theme
{
    internal static bool HighContrast => SystemInformation.HighContrast;
    internal static Color Background => HighContrast ? SystemColors.Control : Color.FromArgb(246, 248, 247);
    internal static Color Surface => HighContrast ? SystemColors.Window : Color.White;
    internal static Color Sidebar => HighContrast ? SystemColors.Control : Color.FromArgb(233, 241, 237);
    internal static Color Ink => HighContrast ? SystemColors.WindowText : Color.FromArgb(31, 49, 48);
    internal static Color Muted => HighContrast ? SystemColors.WindowText : Color.FromArgb(103, 123, 119);
    internal static Color Accent => HighContrast ? SystemColors.Highlight : Color.FromArgb(10, 120, 109);
    internal static Color AccentSoft => HighContrast ? SystemColors.Control : Color.FromArgb(225, 242, 235);
    internal static Color Border => HighContrast ? SystemColors.WindowText : Color.FromArgb(224, 231, 227);

    internal static Label Label(string text, float size = 10, bool bold = false, Color? color = null) => new()
    {
        Text = text, AutoSize = false, Dock = DockStyle.Fill, BackColor = Color.Transparent,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = color ?? Ink, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty,
        AutoEllipsis = true
    };

    internal static FlowButton Button(string text, bool primary = false) => new()
    {
        Text = text, Height = 42, Width = 150, Appearance = primary ? ButtonAppearance.Primary : ButtonAppearance.Secondary,
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Margin = new Padding(4, 0, 0, 0)
    };

    internal static DataGridView Grid() => new BufferedGrid
    {
        Dock = DockStyle.Fill, BackgroundColor = Surface, BorderStyle = BorderStyle.None,
        RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false,
        ColumnHeadersHeight = 38, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Border,
        RowTemplate = { Height = 48 }, Font = new Font("Segoe UI", 9.5f),
        DefaultCellStyle = new() { ForeColor = Ink, BackColor = Surface, SelectionBackColor = AccentSoft,
            SelectionForeColor = Ink, Padding = new Padding(10, 0, 5, 0) },
        ColumnHeadersDefaultCellStyle = new() { BackColor = Background, ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5f), Padding = new Padding(10, 0, 5, 0) }
    };

    internal static GraphicsPath Round(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        if (d <= 0) { path.AddRectangle(rect); return path; }
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void FillRound(Graphics g, RectangleF rect, float radius, Color color)
    {
        using var path = Round(rect, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    internal static (Color Ink, Color Tint) Category(string name) => name switch
    {
        "Documents" => (Color.FromArgb(66, 110, 171), Color.FromArgb(232, 240, 252)),
        "Images" => (Color.FromArgb(161, 104, 59), Color.FromArgb(252, 239, 221)),
        "Video" => (Color.FromArgb(127, 94, 162), Color.FromArgb(241, 234, 251)),
        "Audio" => (Color.FromArgb(175, 94, 122), Color.FromArgb(251, 232, 239)),
        "Archives" => (Color.FromArgb(54, 132, 110), Color.FromArgb(226, 244, 236)),
        "Installers" => (Color.FromArgb(100, 116, 138), Color.FromArgb(233, 239, 246)),
        _ => (Muted, Background)
    };

    internal static string Size(long bytes) => bytes >= 1024 * 1024 * 1024 ? $"{bytes / (1024d * 1024 * 1024):0.#} GB" :
        bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024):0.#} MB" : bytes >= 1024 ? $"{bytes / 1024d:0.#} KB" : $"{bytes} B";

    private sealed class BufferedGrid : DataGridView
    {
        internal BufferedGrid() => DoubleBuffered = true;
    }
}
