namespace FileFlow;

internal static class Theme
{
    internal static readonly Color Background = Color.FromArgb(244, 246, 250);
    internal static readonly Color Ink = Color.FromArgb(25, 35, 53);
    internal static readonly Color Muted = Color.FromArgb(105, 116, 133);
    internal static readonly Color Accent = Color.FromArgb(54, 91, 221);
    internal static readonly Color Border = Color.FromArgb(227, 232, 240);

    internal static Label Label(string text, float size = 10, bool bold = false, Color? color = null) => new()
    {
        Text = text, AutoSize = false, Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = color ?? Ink, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty,
        AutoEllipsis = true
    };

    internal static Button Button(string text, bool primary = false) => new StyledButton()
    {
        Text = text, AutoSize = false, Height = 40, Width = 150, FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Accent : Color.White,
        ForeColor = primary ? Color.White : Ink, Cursor = Cursors.Hand,
        Font = new Font("Segoe UI", 10, FontStyle.Bold), Margin = new Padding(5, 0, 0, 0),
        FlatAppearance = { BorderSize = primary ? 0 : 1, BorderColor = Border }
    };

    internal static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
        RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false,
        ColumnHeadersHeight = 42, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Border,
        RowTemplate = { Height = 42 }, Font = new Font("Segoe UI", 10),
        DefaultCellStyle = new() { ForeColor = Ink, BackColor = Color.White, SelectionBackColor = Color.FromArgb(233, 239, 255),
            SelectionForeColor = Ink, Padding = new Padding(8, 0, 4, 0) },
        ColumnHeadersDefaultCellStyle = new() { BackColor = Color.FromArgb(247, 249, 252), ForeColor = Muted,
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Padding = new Padding(8, 0, 4, 0) }
    };

    internal static string Size(long bytes) => bytes >= 1024 * 1024 * 1024 ? $"{bytes / (1024d * 1024 * 1024):0.#} GB" :
        bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024):0.#} MB" : bytes >= 1024 ? $"{bytes / 1024d:0.#} KB" : $"{bytes} B";

    private sealed class StyledButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Enabled) return;
            using var background = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(background, ClientRectangle);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                (TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
            var bounds = Rectangle.Inflate(ClientRectangle, -8, 0);
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds,
                BackColor.GetBrightness() < .3 ? Color.FromArgb(140, 156, 180) : Color.FromArgb(130, 141, 158), flags);
        }
    }
}
