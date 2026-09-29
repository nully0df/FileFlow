using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FileFlow;

// Original vector mark: two offset paths following one continuous direction.
public static class Branding
{
    public static Bitmap Render(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Draw(g, new RectangleF(0, 0, size, size));
        return bitmap;
    }

    internal static void Draw(Graphics g, RectangleF bounds)
    {
        var state = g.Save();
        g.TranslateTransform(bounds.X, bounds.Y); g.ScaleTransform(bounds.Width / 100, bounds.Height / 100);
        using var shape = Theme.Round(new RectangleF(1, 1, 98, 98), 28);
        using var gradient = new LinearGradientBrush(new PointF(0, 0), new PointF(100, 100), Color.FromArgb(31, 157, 137), Color.FromArgb(5, 91, 91));
        g.FillPath(gradient, shape);
        using var light = new Pen(Color.FromArgb(90, 231, 251, 221), 1); g.DrawPath(light, shape);
        using var first = new GraphicsPath(); first.AddBezier(26, 68, 24, 33, 43, 26, 71, 29);
        using var second = new GraphicsPath(); second.AddBezier(32, 76, 31, 49, 47, 43, 72, 46);
        using var white = new Pen(Color.White, 9) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var mint = new Pen(Color.FromArgb(161, 231, 185), 9) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawPath(white, first); g.DrawPath(mint, second);
        g.Restore(state);
    }

    public static void Export(string directory)
    {
        Directory.CreateDirectory(directory);
        using (var large = Render(256)) large.Save(Path.Combine(directory, "fileflow-mark.png"), ImageFormat.Png);
        int[] sizes = [16, 24, 32, 48, 64, 128, 256];
        var frames = sizes.Select(size =>
        {
            using var bitmap = Render(size); using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png); return stream.ToArray();
        }).ToArray();
        using var output = new BinaryWriter(File.Create(Path.Combine(directory, "fileflow.ico")));
        output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            output.Write((byte)0); output.Write((byte)0); output.Write((ushort)1); output.Write((ushort)32);
            output.Write(frames[i].Length); output.Write(offset); offset += frames[i].Length;
        }
        foreach (var frame in frames) output.Write(frame);
    }
}

internal sealed class BrandView : Control
{
    internal BrandView()
    {
        Size = new Size(44, 44);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; AccessibleName = "FileFlow";
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var size = Math.Min(Width, Height);
        Branding.Draw(e.Graphics, new RectangleF(0, (Height - size) / 2f, size, size));
    }
}
