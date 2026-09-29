using System.Diagnostics;
using System.Drawing.Imaging;
using FileFlow;
using FileFlow.Core;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Control.CheckForIllegalCrossThreadCalls = true;
        string testRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "FileFlow.UiChecks", Guid.NewGuid().ToString("N"));
        string data = Path.Combine(testRoot, "session-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(data);
        Branding.Export(Path.Combine(testRoot, "brand"));
        int result = 1;
        using var form = new MainForm(data, (_, _) => true);
        form.Shown += async (_, _) =>
        {
            try
            {
                var controls = Descendants(form).ToArray();
                var sample = controls.OfType<Button>().Single(b => b.Text == "Try a sample folder");
                var preview = controls.OfType<Button>().Single(b => b.Text == "Preview files");
                var undo = controls.OfType<Button>().Single(b => b.Text == "Undo latest");
                var move = controls.OfType<Button>().Single(b => b.Text == "Move selected");
                var grid = controls.OfType<DataGridView>().Single();
                var folder = controls.OfType<TextBox>().Single();
                Console.WriteLine($"Rendered at {form.DeviceDpi} DPI, client {form.ClientSize}");
                Check(!move.Enabled && !undo.Enabled, "Initial state cannot move or undo");
                await Task.Delay(80);
                Save(form, Path.Combine(testRoot, "fileflow-empty.png"));
                sample.PerformClick();
                await Until(() => preview.Enabled && grid.Rows.Count == 7);
                Check(move.Text == "Move 6 files", "Sample preview has six matching files");
                Check(grid.Rows.Cast<DataGridViewRow>().Count(r => Convert.ToString(r.Cells[5].Value) == "New name") == 1, "Collision is visible in preview");
                // Show the end of the real fixture path without changing the previewed folder.
                folder.SelectionStart = folder.TextLength; folder.ScrollToCaret(); grid.Focus();
                Save(form, Path.Combine(testRoot, "fileflow-preview.png"));
                var originalSize = form.ClientSize;
                form.ClientSize = new Size((int)(1034 * form.DeviceDpi / 96f), (int)(681 * form.DeviceDpi / 96f));
                await Task.Delay(100);
                Save(form, Path.Combine(testRoot, "fileflow-compact.png"));
                form.ClientSize = originalSize;
                var root = folder.Text;
                var first = grid.Rows.Cast<DataGridViewRow>().First(r => r.Tag is PlanItem);
                controls.OfType<Button>().Single(b => b.Text == "Clear").PerformClick();
                Check(!move.Enabled, "Clear selection disables moving");
                controls.OfType<Button>().Single(b => b.Text == "Select all").PerformClick();
                Check(move.Text == "Move 6 files", "Select all excludes the skipped file");
                first.Cells[0].Value = false;
                Check(move.Text == "Move 5 files", "Selection updates the move count");
                first.Cells[0].Value = true;
                move.PerformClick();
                await Until(() => preview.Enabled && undo.Enabled && grid.Rows.Count == 0);
                Check(Directory.GetFiles(root).Length == 1, "Move action left only unmatched file at root");
                Check(File.ReadAllText(Path.Combine(root, "Documents", "Meeting notes.txt")).StartsWith("An existing sample"), "Collision preserved the existing file");
                Check(File.Exists(Path.Combine(root, "Documents", "Meeting notes (1).txt")), "Renamed file moved to the previewed name");
                using (var confirmPreview = new DecisionForm("Move 6 files into the folders shown in the preview?\n\nExisting files will not be overwritten. You can undo this operation.", "Organize files"))
                {
                    confirmPreview.Show(form); await Task.Delay(80);
                    Save(confirmPreview, Path.Combine(testRoot, "fileflow-confirm.png"));
                    Descendants(confirmPreview).OfType<Button>().Single(b => b.Text == "Cancel").PerformClick();
                    Check(confirmPreview.DialogResult == DialogResult.Cancel, "Confirmation cancellation remains available");
                }
                undo.PerformClick();
                await Until(() => preview.Enabled && !undo.Enabled);
                Check(Directory.GetFiles(root).Length == 7, "Undo action restored all six files");
                preview.PerformClick();
                await Until(() => preview.Enabled && grid.Rows.Count == 7);
                folder.Text = root + "-different";
                Check(!move.Enabled && grid.Rows.Count == 0, "Changing folder invalidates preview");
                folder.Text = root;
                Exception? ruleError = null;
                using var timer = new System.Windows.Forms.Timer { Interval = 100 };
                timer.Tick += (_, _) =>
                {
                    var dialog = Application.OpenForms.Cast<Form>().SingleOrDefault(f => f.Text == "FileFlow · Sorting rules");
                    if (dialog is null) return;
                    timer.Stop();
                    try
                    {
                        Save(dialog, Path.Combine(testRoot, "fileflow-rules.png"));
                        var fields = Descendants(dialog).ToArray();
                        var rulesGrid = fields.OfType<DataGridView>().Single();
                        var count = rulesGrid.Rows.Count;
                        fields.OfType<Button>().Single(b => b.Text == "Add rule").PerformClick();
                        Check(rulesGrid.Rows.Count == count + 1, "Add rule creates an editable row");
                        rulesGrid.EndEdit();
                        fields.OfType<Button>().Single(b => b.Text == "Remove selected").PerformClick();
                        Check(rulesGrid.Rows.Count == count, "Remove selected removes the added rule");
                        rulesGrid.Rows.Clear();
                        rulesGrid.Rows.Add(true, "Notes", ".txt");
                        rulesGrid.Rows.Add(true, "Duplicate", ".txt");
                        fields.OfType<Button>().Single(b => b.Text == "Save rules").PerformClick();
                        Check(dialog.Visible && fields.OfType<Label>().Any(l => l.Text.Contains(".txt") && l.Text.Contains("more than one")),
                            "Duplicate extensions keep the editor open with an inline explanation");
                        rulesGrid.Rows.Clear(); rulesGrid.Rows.Add(true, "Notes", ".txt");
                        fields.OfType<Button>().Single(b => b.Text == "Save rules").PerformClick();
                    }
                    catch (Exception ex) { ruleError = ex; dialog.Close(); }
                };
                timer.Start();
                controls.OfType<Button>().Single(b => b.Text == "Sorting rules").PerformClick();
                if (ruleError is not null) throw ruleError;
                Check(!move.Enabled, "Saving rules invalidates the previous preview");
                using var restarted = new MainForm(data, (_, _) => true);
                restarted.Show();
                var fresh = Descendants(restarted).ToArray();
                var freshPreview = fresh.OfType<Button>().Single(b => b.Text == "Preview files");
                var freshGrid = fresh.OfType<DataGridView>().Single();
                freshPreview.PerformClick();
                await Until(() => freshPreview.Enabled && freshGrid.Rows.Count == 7);
                Check(freshGrid.Rows.Cast<DataGridViewRow>().Count(r => r.Tag is PlanItem) == 1 &&
                    freshGrid.Rows.Cast<DataGridViewRow>().Any(r => Convert.ToString(r.Cells[2].Value) == "Notes"), "Custom rules persist across restart");
                restarted.Close();
                result = 0;
                Console.WriteLine("UI workflow checks passed; previews saved.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { form.Close(); }
        };
        Application.Run(form);
        return result;
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("UI operation did not finish.");
            await Task.Delay(30);
        }
    }

    private static void Save(Form form, string path)
    {
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(path, ImageFormat.Png);
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
