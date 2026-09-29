using System.Runtime.InteropServices;

namespace FileFlow;

public class FlowForm : Form
{
    public FlowForm()
    {
        DoubleBuffered = true; BackColor = Theme.Background;
        Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        using var icon = typeof(FlowForm).Assembly.GetManifestResourceStream("FileFlow.fileflow.ico");
        if (icon is not null) Icon = new Icon(icon);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || Theme.HighContrast) return;
        // Native caption buttons, resizing, Snap and system menu remain Windows-owned.
        int rounded = 2;
        int caption = Theme.Background.R | Theme.Background.G << 8 | Theme.Background.B << 16;
        int text = Theme.Ink.R | Theme.Ink.G << 8 | Theme.Ink.B << 16;
        _ = DwmSetWindowAttribute(Handle, 33, ref rounded, sizeof(int));
        _ = DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(Handle, 36, ref text, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
