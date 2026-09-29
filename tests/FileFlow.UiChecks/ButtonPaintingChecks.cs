using System.Reflection;

internal static class ButtonPaintingChecks
{
    private static readonly MethodInfo Print = typeof(Control).GetMethod("OnPrint", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static void Run(IEnumerable<Button> buttons)
    {
        int count = 0;
        foreach (var button in buttons.Where(b => b.Visible))
        {
            var text = button.Text;
            var enabled = button.Enabled;
            using var reused = new Bitmap(button.Width, button.Height);
            using (var graphics = Graphics.FromImage(reused)) graphics.Clear(Color.Magenta);
            try
            {
                Paint(button, reused);
                AssertFresh(button, reused, "initial paint");
                if (enabled)
                {
                    Raise(button, "OnMouseEnter"); Paint(button, reused);
                    AssertFresh(button, reused, "hover");
                    Raise(button, "OnMouseLeave"); Paint(button, reused);
                    AssertFresh(button, reused, "hover exit");
                }
                button.Text = ""; Paint(button, reused);
                AssertFresh(button, reused, "clearing old text");
                button.Text = text; button.Enabled = !enabled; Paint(button, reused);
                AssertFresh(button, reused, "enabled state change");
                count++;
            }
            finally
            {
                button.Text = text; button.Enabled = enabled;
                Raise(button, "OnMouseLeave");
            }
        }
        if (count == 0) throw new InvalidOperationException("No visible buttons were checked.");
        Console.WriteLine($"PASS {count} buttons repaint every pixel across state and text changes");
    }

    // Exercise the framework's background/foreground paint pipeline in the test's own
    // controls. Whole-form DrawToBitmap can hide stale pixels by painting the parent first.
    private static void Paint(Control control, Bitmap bitmap)
    {
        using var graphics = Graphics.FromImage(bitmap);
        using var args = new PaintEventArgs(graphics, control.ClientRectangle);
        Print.Invoke(control, [args]);
    }

    private static void AssertFresh(Control control, Bitmap reused, string state)
    {
        using var fresh = new Bitmap(reused.Width, reused.Height);
        using (var graphics = Graphics.FromImage(fresh)) graphics.Clear(Color.Lime);
        Paint(control, fresh);
        for (int y = 0; y < fresh.Height; y++)
            for (int x = 0; x < fresh.Width; x++)
                if (fresh.GetPixel(x, y) != reused.GetPixel(x, y))
                    throw new InvalidOperationException($"Button '{control.Text}' retains old pixels during {state} at ({x}, {y}).");
    }

    private static void Raise(Control control, string method) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [EventArgs.Empty]);
}
