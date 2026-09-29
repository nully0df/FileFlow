using FileFlow.Core;

namespace FileFlow;

public sealed class MainForm : Form
{
    private Preferences _preferences = new();
    private readonly JournalStore _history;
    private readonly Func<string, string, bool> _confirm;
    private readonly TextBox _folder = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11), PlaceholderText = "Choose a folder to organize…", Margin = new Padding(0, 9, 12, 0) };
    private readonly DataGridView _files = Theme.Grid();
    private readonly Button _browse = Theme.Button("Choose folder");
    private readonly Button _preview = Theme.Button("Preview files", true);
    private readonly Button _apply = Theme.Button("Move selected", true);
    private readonly Button _undo = Theme.Button("Undo latest");
    private readonly Button _rules = Theme.Button("Sorting rules");
    private readonly Button _demo = Theme.Button("Try a sample folder");
    private readonly Button _cancel = Theme.Button("Cancel");
    private readonly Label _status = Theme.Label("Choose a folder, then preview where each file will go.", 9, color: Theme.Muted);
    private readonly Label _summary = Theme.Label("0 files selected", 13, true);
    private readonly Label _categorySummary = Theme.Label("6 built-in categories · editable rules", 9, color: Theme.Muted);
    private readonly Label _empty = Theme.Label("A little order goes a long way.\n\nYour file preview will appear here.\nStart with a sample folder to see how it works.", 12, color: Theme.Muted);
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 4, Visible = false };
    private readonly Label _step = Theme.Label("01   CHOOSE     /     02   PREVIEW     /     03   ORGANIZE", 9, true, Theme.Muted);
    private SortingPlan? _plan;
    private CancellationTokenSource? _cancellation;
    private bool _busy;
    private bool _hasHistory;

    public MainForm(string? dataDirectory = null, Func<string, string, bool>? confirm = null)
    {
        if (dataDirectory is not null) Preferences.DataDirectory = Path.GetFullPath(dataDirectory);
        _history = new(Path.Combine(Preferences.DataDirectory, "history"));
        _confirm = confirm ?? ((message, title) => MessageBox.Show(this, message, title,
            MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK);
        Text = "FileFlow · A place for every file";
        ClientSize = new Size(1240, 810);
        MinimumSize = new Size(1050, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildLayout();
        _browse.Click += (_, _) => Browse();
        _preview.Click += async (_, _) => await PreviewAsync();
        _apply.Click += async (_, _) => await ApplyAsync();
        _undo.Click += async (_, _) => await UndoAsync();
        _rules.Click += (_, _) => EditRules();
        _demo.Click += async (_, _) => await DemoAsync();
        _cancel.Click += (_, _) => { _cancellation?.Cancel(); _status.Text = "Stopping safely after the current file…"; };
        _folder.TextChanged += (_, _) => { if (!_busy) InvalidatePreview(); };
        _files.CurrentCellDirtyStateChanged += (_, _) => { if (_files.IsCurrentCellDirty) _files.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _files.CellValueChanged += (_, _) => UpdateSelection();
        FormClosing += (_, e) => { if (_busy) { e.Cancel = true; _cancellation?.Cancel(); _status.Text = "Stopping safely. Close the window once the operation has finished."; } };
        try { _preferences = Preferences.Load(); _folder.Text = _preferences.LastFolder; }
        catch (Exception ex) when (Expected(ex)) { _status.Text = "Settings could not be loaded. Default rules are active."; }
        RefreshHistory();
        UpdateRulesSummary();
        UpdateButtons();
    }

    private void BuildLayout()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        shell.ColumnStyles.Add(new(SizeType.Absolute, 222));
        shell.ColumnStyles.Add(new(SizeType.Percent, 100));
        shell.RowStyles.Add(new(SizeType.Percent, 100));
        var side = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 34, 52), Padding = new Padding(22, 28, 22, 22), ColumnCount = 1, RowCount = 9, Margin = Padding.Empty };
        foreach (var height in new[] { 56, 42, 52, 48, 48, 22 }) side.RowStyles.Add(new(SizeType.Absolute, height));
        side.RowStyles.Add(new(SizeType.Percent, 100));
        side.RowStyles.Add(new(SizeType.Absolute, 92));
        side.RowStyles.Add(new(SizeType.Absolute, 28));
        side.Controls.Add(Theme.Label("FileFlow", 21, true, Color.White), 0, 0);
        side.Controls.Add(Theme.Label("LESS CLUTTER. MORE FLOW.", 7, true, Color.FromArgb(145, 159, 184)), 0, 1);
        var selected = Theme.Label("  Organize files", 10, true, Color.White);
        selected.BackColor = Color.FromArgb(48, 64, 92);
        selected.Margin = new Padding(0, 4, 0, 8);
        side.Controls.Add(selected, 0, 2);
        foreach (var button in new[] { _rules, _undo })
        {
            button.Dock = DockStyle.Fill;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.BackColor = Color.FromArgb(24, 34, 52);
            button.ForeColor = Color.FromArgb(199, 209, 227);
            button.FlatAppearance.BorderSize = 0;
            button.Margin = new Padding(0, 2, 0, 2);
        }
        side.Controls.Add(_rules, 0, 3);
        side.Controls.Add(_undo, 0, 4);
        side.Controls.Add(Theme.Label("YOU'RE IN CONTROL\n\nPreview every move.\nUndo when you need to.", 9, false, Color.FromArgb(155, 171, 195)), 0, 7);
        side.Controls.Add(Theme.Label("WINDOWS  /  v0.1.0", 8, true, Color.FromArgb(113, 132, 160)), 0, 8);
        shell.Controls.Add(side, 0, 0);

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 20, 28, 22), ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
        foreach (var height in new[] { 104, 116, 84 }) main.RowStyles.Add(new(SizeType.Absolute, height));
        main.RowStyles.Add(new(SizeType.Percent, 100));
        main.RowStyles.Add(new(SizeType.Absolute, 44));
        main.RowStyles.Add(new(SizeType.Absolute, 68));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        heading.RowStyles.Add(new(SizeType.Absolute, 24));
        heading.RowStyles.Add(new(SizeType.Absolute, 44));
        heading.RowStyles.Add(new(SizeType.Percent, 100));
        heading.Controls.Add(_step, 0, 0);
        heading.Controls.Add(Theme.Label("A place for every file.", 25, true), 0, 1);
        heading.Controls.Add(Theme.Label("Turn a crowded folder into a clear workspace, one preview at a time.", 10, color: Theme.Muted), 0, 2);
        main.Controls.Add(heading, 0, 0);

        var folderCard = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 8, 16, 10), ColumnCount = 3, RowCount = 3, Margin = new Padding(0, 12, 0, 0) };
        folderCard.ColumnStyles.Add(new(SizeType.Percent, 100));
        folderCard.ColumnStyles.Add(new(SizeType.Absolute, 142));
        folderCard.ColumnStyles.Add(new(SizeType.Absolute, 142));
        folderCard.RowStyles.Add(new(SizeType.Absolute, 25));
        folderCard.RowStyles.Add(new(SizeType.Absolute, 43));
        folderCard.RowStyles.Add(new(SizeType.Percent, 100));
        var folderTitle = Theme.Label("SOURCE FOLDER", 8, true, Theme.Muted);
        folderCard.Controls.Add(folderTitle, 0, 0); folderCard.SetColumnSpan(folderTitle, 3);
        folderCard.Controls.Add(_folder, 0, 1);
        _browse.Dock = DockStyle.Fill; _preview.Dock = DockStyle.Fill;
        folderCard.Controls.Add(_browse, 1, 1); folderCard.Controls.Add(_preview, 2, 1);
        var hint = Theme.Label("Files in this folder only. Subfolders and unmatched files stay in place.", 8, color: Theme.Muted);
        folderCard.Controls.Add(hint, 0, 2); folderCard.SetColumnSpan(hint, 3);
        main.Controls.Add(folderCard, 0, 1);

        var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 14, 0, 12), Margin = Padding.Empty };
        summary.ColumnStyles.Add(new(SizeType.Percent, 100)); summary.ColumnStyles.Add(new(SizeType.Absolute, 188));
        summary.RowStyles.Add(new(SizeType.Percent, 55)); summary.RowStyles.Add(new(SizeType.Percent, 45));
        summary.Controls.Add(_summary, 0, 0); summary.Controls.Add(_categorySummary, 0, 1);
        _demo.Dock = DockStyle.Fill; _demo.Margin = new Padding(0, 5, 0, 5);
        summary.Controls.Add(_demo, 1, 0); summary.SetRowSpan(_demo, 2);
        main.Controls.Add(summary, 0, 2);

        _files.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "", Width = 38, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
        foreach (var column in new[] { ("File", "File name", 115f), ("Category", "Category", 65f), ("Size", "Size", 48f), ("Destination", "Destination", 155f), ("Status", "Status", 68f) })
            _files.Columns.Add(new DataGridViewTextBoxColumn { Name = column.Item1, HeaderText = column.Item2, FillWeight = column.Item3, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = Padding.Empty };
        gridHost.Controls.Add(_files);
        _empty.TextAlign = ContentAlignment.MiddleCenter;
        gridHost.Controls.Add(_empty); _empty.BringToFront();
        main.Controls.Add(gridHost, 0, 3);
        main.Controls.Add(_status, 0, 4);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = Padding.Empty };
        bottom.ColumnStyles.Add(new(SizeType.Percent, 100)); bottom.ColumnStyles.Add(new(SizeType.Absolute, 104)); bottom.ColumnStyles.Add(new(SizeType.Absolute, 174));
        bottom.RowStyles.Add(new(SizeType.Absolute, 7)); bottom.RowStyles.Add(new(SizeType.Percent, 100));
        bottom.Controls.Add(_progress, 0, 0); bottom.SetColumnSpan(_progress, 3);
        bottom.Controls.Add(Theme.Label("No overwrites. No background sorting.", 9, color: Theme.Muted), 0, 1);
        _cancel.Dock = DockStyle.Fill; _apply.Dock = DockStyle.Fill;
        _cancel.Margin = new Padding(0, 10, 8, 5); _apply.Margin = new Padding(0, 10, 0, 5);
        bottom.Controls.Add(_cancel, 1, 1); bottom.Controls.Add(_apply, 2, 1);
        main.Controls.Add(bottom, 0, 5);
        shell.Controls.Add(main, 1, 0);
        Controls.Add(shell);
    }

    private void Browse()
    {
        using var picker = new FolderBrowserDialog { Description = "Choose a folder to organize", UseDescriptionForTitle = true, ShowNewFolderButton = false };
        if (Directory.Exists(_folder.Text)) picker.SelectedPath = _folder.Text;
        if (picker.ShowDialog(this) == DialogResult.OK) _folder.Text = picker.SelectedPath;
    }

    private async Task PreviewAsync()
    {
        var path = _folder.Text.Trim();
        if (path.Length == 0) { Browse(); path = _folder.Text.Trim(); if (path.Length == 0) return; }
        await BusyAsync("Reading files…", async token =>
        {
            _plan = null;
            var rules = _preferences.Rules.ToArray();
            var plan = await Task.Run(() => new Planner().Preview(path, rules, token), token);
            _plan = plan;
            _folder.Text = plan.Root;
            _files.Rows.Clear();
            foreach (var item in plan.Items)
            {
                var index = _files.Rows.Add(true, Path.GetFileName(item.Source), item.Category, Theme.Size(item.Length),
                    Path.GetRelativePath(plan.Root, item.Destination), item.Renamed ? "New name" : "Ready");
                _files.Rows[index].Tag = item;
                _files.Rows[index].Cells[4].ToolTipText = item.Destination;
                if (item.Renamed) _files.Rows[index].Cells[5].Style.ForeColor = Color.FromArgb(166, 111, 19);
            }
            foreach (var skipped in plan.Skipped)
            {
                var index = _files.Rows.Add(false, skipped.Name, "—", "—", skipped.Reason, "Skipped");
                _files.Rows[index].ReadOnly = true;
                _files.Rows[index].DefaultCellStyle.ForeColor = Theme.Muted;
                _files.Rows[index].Cells[4].ToolTipText = skipped.Reason;
            }
            _empty.Visible = _files.Rows.Count == 0;
            _empty.Text = "Nothing to organize here.\n\nChoose another folder or adjust your sorting rules.";
            _files.ClearSelection();
            _preferences.LastFolder = plan.Root;
            _status.Text = $"Preview ready · {plan.Items.Count} ready · {plan.Skipped.Count} skipped · No files have moved.";
            SavePreferences();
            UpdateSelection();
        });
    }

    private List<PlanItem> SelectedItems() => _files.Rows.Cast<DataGridViewRow>()
        .Where(r => r.Tag is PlanItem && r.Cells[0].Value is true).Select(r => (PlanItem)r.Tag!).ToList();

    private void UpdateSelection()
    {
        var selected = SelectedItems();
        _summary.Text = $"{selected.Count} files selected  ·  {Theme.Size(selected.Sum(i => i.Length))}";
        _apply.Text = selected.Count == 0 ? "Move selected" : $"Move {selected.Count} files";
        UpdateButtons();
    }

    private async Task ApplyAsync()
    {
        if (_plan is null) return;
        var selected = SelectedItems();
        if (selected.Count == 0) return;
        var plan = _plan with { Items = selected };
        if (!_confirm($"Move {selected.Count} files into the folders shown in the preview?\n\nExisting files will not be overwritten. You can undo this operation.", "Organize files")) return;
        await BusyAsync("Organizing files…", async token =>
        {
            _plan = null;
            var progress = Progress();
            var result = await Task.Run(() => new Organizer(_history).Apply(plan, progress, token), token);
            InvalidatePreview();
            _status.Text = $"{result.Succeeded} files moved{(result.Cancelled ? " · Cancelled" : "")} · {result.Problems.Count} skipped. Preview again to see what remains.";
            ShowProblems(result);
        });
    }

    private async Task UndoAsync()
    {
        MoveJournal? journal;
        try { journal = _history.LatestUndoable(); }
        catch (Exception ex) when (Expected(ex))
        {
            MessageBox.Show(this, ex.Message, "History unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshHistory(); UpdateButtons(); return;
        }
        if (journal is null) return;
        if (!_confirm($"Restore the latest remaining operation?\n\nFolder: {journal.Root}\nStarted: {journal.CreatedUtc.ToLocalTime():g}\n\nEdited files and occupied original names will be left untouched.", "Undo latest")) return;
        await BusyAsync("Restoring files…", async token =>
        {
            var progress = Progress();
            var result = await Task.Run(() => new Organizer(_history).Undo(progress, token), token);
            InvalidatePreview();
            _status.Text = $"{result.Succeeded} files restored{(result.Cancelled ? " · Cancelled" : "")} · {result.Problems.Count} skipped. Empty category folders are kept.";
            ShowProblems(result);
        });
    }

    private IProgress<OperationProgress> Progress() => new Progress<OperationProgress>(p =>
    {
        if (!_busy) return;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Value = Math.Clamp((int)(100L * p.Completed / Math.Max(1, p.Total)), 0, 100);
        _status.Text = $"{p.Completed} / {p.Total}  ·  {p.FileName}";
    });

    private async Task BusyAsync(string message, Func<CancellationToken, Task> operation)
    {
        if (_busy) return;
        _busy = true;
        _cancellation = new();
        _status.Text = message;
        _progress.Visible = true;
        _progress.Style = ProgressBarStyle.Marquee;
        UpdateButtons();
        try { await operation(_cancellation.Token); }
        catch (OperationCanceledException) { InvalidatePreview(); _status.Text = "Cancelled. Create a new preview to continue."; }
        catch (Exception ex) when (Expected(ex))
        {
            InvalidatePreview();
            _status.Text = "Could not finish. Your operation history is kept for Undo.";
            MessageBox.Show(this, ex.Message, "FileFlow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _busy = false; _cancellation.Dispose(); _cancellation = null; _progress.Visible = false;
            RefreshHistory(); UpdateButtons();
        }
    }

    private static bool Expected(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException;

    private void RefreshHistory()
    {
        try { _hasHistory = _history.LatestUndoable() is not null; }
        catch (Exception ex) when (Expected(ex)) { _hasHistory = false; _status.Text = "History could not be read. Keep the history folder and check its files before using Undo."; }
    }

    private void InvalidatePreview()
    {
        _plan = null; _files.Rows.Clear(); _empty.Visible = true;
        _empty.Text = "Ready when you are.\n\nChoose a folder and click Preview files.";
        _status.Text = "Create a fresh preview before organizing files.";
        UpdateSelection();
    }

    private void UpdateButtons()
    {
        _browse.Enabled = _preview.Enabled = _rules.Enabled = _demo.Enabled = _folder.Enabled = _files.Enabled = !_busy;
        _undo.Enabled = !_busy && _hasHistory;
        _apply.Enabled = !_busy && _plan is not null && SelectedItems().Count > 0;
        _cancel.Visible = _busy;
    }

    private void EditRules()
    {
        using var dialog = new RulesForm(_preferences.Rules);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _preferences.Rules = dialog.Result;
        InvalidatePreview(); UpdateRulesSummary(); SavePreferences();
    }

    private void UpdateRulesSummary() => _categorySummary.Text = string.Join("  /  ", _preferences.Rules.Where(r => r.Enabled).Select(r => r.Folder));

    private void SavePreferences()
    {
        try { _preferences.Save(); }
        catch (Exception ex) when (Expected(ex)) { _status.Text += " Settings could not be saved."; }
    }

    private async Task DemoAsync()
    {
        try
        {
            var directory = Path.Combine(Preferences.DataDirectory, "Samples", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(directory);
            foreach (var name in new[] { "Project proposal.pdf", "Autumn photo.jpg", "Meeting notes.txt", "Design assets.zip", "Interview.mp4", "Playlist.mp3", "readme.unknown" })
                File.WriteAllText(Path.Combine(directory, name), "FileFlow sample content. Synthetic demo file; not real media.\n");
            Directory.CreateDirectory(Path.Combine(directory, "Documents"));
            File.WriteAllText(Path.Combine(directory, "Documents", "Meeting notes.txt"), "An existing sample file. This must not be overwritten.");
            _folder.Text = directory;
            await PreviewAsync();
        }
        catch (Exception ex) when (Expected(ex)) { MessageBox.Show(this, ex.Message, "Sample folder", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void ShowProblems(OperationResult result)
    {
        if (result.Problems.Count == 0) return;
        using var details = new Form { Text = "FileFlow · Skipped files", StartPosition = FormStartPosition.CenterParent, Size = new Size(760, 440), MinimumSize = new Size(500, 300) };
        details.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false,
            Font = new Font("Segoe UI", 10), Text = string.Join(Environment.NewLine + Environment.NewLine, result.Problems) });
        details.ShowDialog(this);
    }
}
