using FileFlow.Core;

namespace FileFlow;

public sealed class MainForm : FlowForm
{
    private Preferences _preferences = new();
    private readonly JournalStore _history;
    private readonly Func<string, string, bool> _confirm;
    private readonly TextBox _folder = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11), PlaceholderText = "Choose a folder to organize…", Margin = new Padding(0, 9, 12, 0) };
    private readonly DataGridView _files = Theme.Grid();
    private readonly FlowButton _browse = Theme.Button("Choose folder");
    private readonly FlowButton _preview = Theme.Button("Preview files", true);
    private readonly FlowButton _apply = Theme.Button("Move selected", true);
    private readonly FlowButton _undo = Theme.Button("Undo latest");
    private readonly FlowButton _rules = Theme.Button("Sorting rules");
    private readonly FlowButton _demo = Theme.Button("Try a sample folder");
    private readonly FlowButton _cancel = Theme.Button("Cancel");
    private readonly FlowButton _selectAll = Theme.Button("Select all");
    private readonly FlowButton _selectNone = Theme.Button("Clear");
    private readonly FlowButton _organize = Theme.Button("Organize files");
    private readonly Label _reviewHeading = Theme.Label("Review your files", 11, true);
    private readonly Label _status = Theme.Label("Choose a folder, then preview where each file will go.", 9, color: Theme.Muted);
    private readonly Label _summary = Theme.Label("0 files selected", 13, true);
    private readonly FlowLayoutPanel _categories = new();
    private readonly EmptyState _empty = new() { Text = "Space for a fresh start.\nChoose a folder to see where everything belongs.\nOr take FileFlow for a spin with a sample folder." };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 4, Visible = false };
    private SortingPlan? _plan;
    private CancellationTokenSource? _cancellation;
    private bool _busy;
    private bool _hasHistory;
    private bool _selectionChanging;
    private string? _categoryFilter;

    public MainForm(string? dataDirectory = null, Func<string, string, bool>? confirm = null)
    {
        SuspendLayout();
        if (dataDirectory is not null) Preferences.DataDirectory = Path.GetFullPath(dataDirectory);
        _history = new(Path.Combine(Preferences.DataDirectory, "history"));
        _confirm = confirm ?? ((message, title) => DecisionForm.Confirm(this, message, title));
        Text = "FileFlow · A place for every file";
        ClientSize = new Size(1160, 800);
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
        _organize.Click += (_, _) => { SetCategoryFilter(null); if (_plan is null) _folder.Focus(); else _files.Focus(); };
        _cancel.Click += (_, _) => { _cancellation?.Cancel(); _status.Text = "Stopping safely after the current file…"; };
        _folder.TextChanged += (_, _) => { if (!_busy) InvalidatePreview(); };
        _files.CurrentCellDirtyStateChanged += (_, _) => { if (_files.IsCurrentCellDirty) _files.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _files.CellValueChanged += (_, _) => { if (!_selectionChanging) UpdateSelection(); };
        FormClosing += (_, e) => { if (_busy) { e.Cancel = true; _cancellation?.Cancel(); _status.Text = "Stopping safely. Close the window once the operation has finished."; } };
        try { _preferences = Preferences.Load(); _folder.Text = _preferences.LastFolder; }
        catch (Exception ex) when (Expected(ex)) { _status.Text = "Settings could not be loaded. Default rules are active."; }
        RefreshHistory();
        UpdateRulesSummary();
        UpdateButtons();
        AutoScaleDimensions = new SizeF(96, 96);
        ResumeLayout(true);
    }

    private void BuildLayout()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Theme.Background };
        shell.ColumnStyles.Add(new(SizeType.Absolute, 224));
        shell.ColumnStyles.Add(new(SizeType.Percent, 100));
        shell.RowStyles.Add(new(SizeType.Percent, 100));
        var side = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(18, 28, 18, 20), ColumnCount = 1, RowCount = 10, Margin = Padding.Empty };
        foreach (var height in new[] { 74, 44, 46, 48, 48, 12 }) side.RowStyles.Add(new(SizeType.Absolute, height));
        side.RowStyles.Add(new(SizeType.Percent, 100));
        side.RowStyles.Add(new(SizeType.Absolute, 104));
        side.RowStyles.Add(new(SizeType.Absolute, 22));
        side.RowStyles.Add(new(SizeType.Absolute, 30));
        var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty, BackColor = Color.Transparent };
        brand.ColumnStyles.Add(new(SizeType.Absolute, 52)); brand.ColumnStyles.Add(new(SizeType.Percent, 100));
        brand.RowStyles.Add(new(SizeType.Absolute, 36)); brand.RowStyles.Add(new(SizeType.Percent, 100));
        var mark = new BrandView { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 24) };
        brand.Controls.Add(mark, 0, 0); brand.SetRowSpan(mark, 2);
        brand.Controls.Add(Theme.Label("FileFlow", 18, true), 1, 0);
        var tagline = Theme.Label("A little more space.", 8.5f, color: Theme.Muted); tagline.TextAlign = ContentAlignment.TopLeft;
        brand.Controls.Add(tagline, 1, 1);
        side.Controls.Add(brand, 0, 0);
        side.Controls.Add(Theme.Label("WORKSPACE", 8, true, Theme.Muted), 0, 1);
        var active = new SurfacePanel { Dock = DockStyle.Fill, Radius = 12, SurfaceColor = Theme.Surface, Outline = false, Padding = new Padding(14, 0, 10, 0), Margin = Padding.Empty };
        _organize.Dock = DockStyle.Fill; _organize.Appearance = ButtonAppearance.Quiet; _organize.Margin = Padding.Empty;
        active.Controls.Add(_organize);
        side.Controls.Add(active, 0, 2);
        _rules.Appearance = _undo.Appearance = ButtonAppearance.Navigation;
        _rules.Symbol = Glyph.Sliders; _undo.Symbol = Glyph.Undo;
        foreach (var button in new[] { _rules, _undo }) { button.Dock = DockStyle.Fill; button.Margin = new Padding(0, 3, 0, 1); }
        side.Controls.Add(_rules, 0, 3); side.Controls.Add(_undo, 0, 4);
        var note = new SurfacePanel { Dock = DockStyle.Fill, Radius = 16, SurfaceColor = Theme.AccentSoft, Outline = false, Padding = new Padding(14), Margin = Padding.Empty };
        var noteText = Theme.Label("A little peace of mind.\n\nYour moves stay saved\nfor another day.", 8.5f, color: Theme.Muted);
        noteText.TextAlign = ContentAlignment.TopLeft; note.Controls.Add(noteText);
        side.Controls.Add(note, 0, 7);
        side.Controls.Add(Theme.Label("LOCAL FILES. YOUR CONTROL.", 7.5f, true, Theme.Muted), 0, 9);
        shell.Controls.Add(side, 0, 0);

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(30, 24, 30, 22), ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
        main.RowStyles.Add(new(SizeType.Absolute, 82));
        main.RowStyles.Add(new(SizeType.Absolute, 120));
        main.RowStyles.Add(new(SizeType.Absolute, 60));
        main.RowStyles.Add(new(SizeType.Percent, 100));
        main.RowStyles.Add(new(SizeType.Absolute, 34));
        main.RowStyles.Add(new(SizeType.Absolute, 76));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        heading.ColumnStyles.Add(new(SizeType.Percent, 100)); heading.ColumnStyles.Add(new(SizeType.Absolute, 205));
        heading.RowStyles.Add(new(SizeType.Absolute, 47)); heading.RowStyles.Add(new(SizeType.Percent, 100));
        heading.Controls.Add(Theme.Label("Everything, in its place.", 24, true), 0, 0);
        var subtitle = Theme.Label("A calmer folder starts with a clear preview.", 10, color: Theme.Muted);
        subtitle.TextAlign = ContentAlignment.TopLeft; heading.Controls.Add(subtitle, 0, 1);
        _demo.Dock = DockStyle.Fill; _demo.Margin = new Padding(0, 6, 0, 4);
        heading.Controls.Add(_demo, 1, 0);
        main.Controls.Add(heading, 0, 0);

        var source = new SurfacePanel { Dock = DockStyle.Fill, Radius = 20, Padding = new Padding(19, 12, 19, 11), Margin = Padding.Empty,
            SurfaceColor = Theme.Surface, EndColor = Theme.HighContrast ? null : Color.FromArgb(244, 251, 247) };
        var sourceLayout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 3, RowCount = 3, Margin = Padding.Empty };
        sourceLayout.ColumnStyles.Add(new(SizeType.Percent, 100)); sourceLayout.ColumnStyles.Add(new(SizeType.Absolute, 148)); sourceLayout.ColumnStyles.Add(new(SizeType.Absolute, 148));
        sourceLayout.RowStyles.Add(new(SizeType.Absolute, 26)); sourceLayout.RowStyles.Add(new(SizeType.Absolute, 46)); sourceLayout.RowStyles.Add(new(SizeType.Percent, 100));
        sourceLayout.Controls.Add(Theme.Label("YOUR FOLDER", 8, true, Theme.Muted), 0, 0);
        var scope = Theme.Label("Preview first. Move when ready.", 8.5f, color: Theme.Muted); scope.TextAlign = ContentAlignment.MiddleRight;
        sourceLayout.Controls.Add(scope, 1, 0); sourceLayout.SetColumnSpan(scope, 2);
        var input = new SurfacePanel { Dock = DockStyle.Fill, Radius = 11, SurfaceColor = Theme.Background, Padding = new Padding(13, 12, 10, 9), Margin = new Padding(0, 0, 8, 0) };
        _folder.BorderStyle = BorderStyle.None; _folder.BackColor = Theme.Background; _folder.ForeColor = Theme.Ink;
        _folder.Font = new Font("Segoe UI", 10); _folder.Margin = Padding.Empty; _folder.AccessibleName = "Source folder path";
        input.Controls.Add(_folder); sourceLayout.Controls.Add(input, 0, 1);
        _browse.Dock = _preview.Dock = DockStyle.Fill; _browse.Symbol = Glyph.Folder; _preview.Symbol = Glyph.Scan;
        sourceLayout.Controls.Add(_browse, 1, 1); sourceLayout.Controls.Add(_preview, 2, 1);
        var hint = Theme.Label("Files in this folder only · Subfolders stay as they are", 8.5f, color: Theme.Muted);
        sourceLayout.Controls.Add(hint, 0, 2); sourceLayout.SetColumnSpan(hint, 3);
        source.Controls.Add(sourceLayout); main.Controls.Add(source, 0, 1);

        _categories.Dock = DockStyle.Fill; _categories.Padding = new Padding(0, 10, 0, 0);
        _categories.Margin = Padding.Empty; _categories.WrapContents = false; _categories.AutoScroll = true;
        main.Controls.Add(_categories, 0, 2);

        var previewCard = new SurfacePanel { Dock = DockStyle.Fill, Radius = 20, Padding = new Padding(13, 5, 13, 13), Margin = Padding.Empty };
        var previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        previewLayout.RowStyles.Add(new(SizeType.Absolute, 52)); previewLayout.RowStyles.Add(new(SizeType.Percent, 100));
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = new Padding(8, 4, 0, 6) };
        toolbar.ColumnStyles.Add(new(SizeType.Percent, 100)); toolbar.ColumnStyles.Add(new(SizeType.Absolute, 104)); toolbar.ColumnStyles.Add(new(SizeType.Absolute, 76));
        toolbar.Controls.Add(_reviewHeading, 0, 0);
        _selectAll.Appearance = _selectNone.Appearance = ButtonAppearance.Quiet;
        _selectAll.Dock = _selectNone.Dock = DockStyle.Fill;
        _selectAll.Click += (_, _) => SetSelection(true); _selectNone.Click += (_, _) => SetSelection(false);
        toolbar.Controls.Add(_selectAll, 1, 0); toolbar.Controls.Add(_selectNone, 2, 0);
        previewLayout.Controls.Add(toolbar, 0, 0);
        _files.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _files.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "", Width = 36, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
        foreach (var column in new[] { ("File", "File name"), ("Category", "Category"), ("Size", "Size"), ("Destination", "Destination"), ("Status", "Status") })
            _files.Columns.Add(new DataGridViewTextBoxColumn { Name = column.Item1, HeaderText = column.Item2, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Automatic });
        _files.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _files.Columns[3].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        _files.Columns[1].MinimumWidth = 135; _files.Columns[5].MinimumWidth = 91;
        _files.SizeChanged += (_, _) => LayoutPreviewColumns();
        _files.DpiChangedAfterParent += (_, _) => LayoutPreviewColumns();
        _files.SortCompare += (_, e) =>
        {
            e.SortResult = e.Column.Name == "Size"
                ? ((_files.Rows[e.RowIndex1].Tag as PlanItem)?.Length ?? -1).CompareTo((_files.Rows[e.RowIndex2].Tag as PlanItem)?.Length ?? -1)
                : StringComparer.OrdinalIgnoreCase.Compare(Convert.ToString(e.CellValue1), Convert.ToString(e.CellValue2));
            if (e.SortResult == 0)
                e.SortResult = StringComparer.OrdinalIgnoreCase.Compare(Convert.ToString(_files.Rows[e.RowIndex1].Cells[1].Value), Convert.ToString(_files.Rows[e.RowIndex2].Cells[1].Value));
            e.Handled = true;
        };
        _files.AccessibleName = "File preview and selection";
        FileGridStyle.Attach(_files);
        var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Margin = Padding.Empty };
        gridHost.Controls.Add(_files); gridHost.Controls.Add(_empty); _empty.BringToFront();
        _files.Visible = false;
        previewLayout.Controls.Add(gridHost, 0, 1); previewCard.Controls.Add(previewLayout); main.Controls.Add(previewCard, 0, 3);
        _status.AccessibleName = "Operation status";
        main.Controls.Add(_status, 0, 4);

        var footer = new SurfacePanel { Dock = DockStyle.Fill, Radius = 18, Padding = new Padding(19, 10, 12, 10), Margin = Padding.Empty };
        var footerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
        footerLayout.ColumnStyles.Add(new(SizeType.Percent, 100)); footerLayout.ColumnStyles.Add(new(SizeType.Absolute, 92)); footerLayout.ColumnStyles.Add(new(SizeType.Absolute, 180));
        footerLayout.RowStyles.Add(new(SizeType.Absolute, 4)); footerLayout.RowStyles.Add(new(SizeType.Percent, 100));
        footerLayout.Controls.Add(_progress, 0, 0); footerLayout.SetColumnSpan(_progress, 3);
        var selection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
        selection.RowStyles.Add(new(SizeType.Percent, 55)); selection.RowStyles.Add(new(SizeType.Percent, 45));
        _summary.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        selection.Controls.Add(_summary, 0, 0); selection.Controls.Add(Theme.Label("Existing files are never overwritten.", 8, color: Theme.Muted), 0, 1);
        footerLayout.Controls.Add(selection, 0, 1);
        _cancel.Dock = _apply.Dock = DockStyle.Fill; _cancel.Appearance = ButtonAppearance.Quiet; _apply.Symbol = Glyph.Arrow;
        footerLayout.Controls.Add(_cancel, 1, 1); footerLayout.Controls.Add(_apply, 2, 1);
        footer.Controls.Add(footerLayout); main.Controls.Add(footer, 0, 5);
        shell.Controls.Add(main, 1, 0); Controls.Add(shell);
    }

    private void SetSelection(bool value)
    {
        _selectionChanging = true;
        try
        {
            foreach (DataGridViewRow row in _files.Rows)
                if (row.Visible && row.Tag is PlanItem) row.Cells[0].Value = value;
        }
        finally { _selectionChanging = false; }
        UpdateSelection();
    }

    private void LayoutPreviewColumns()
    {
        // Reserve compact metadata columns, then share the remaining space between
        // the two paths. Explicit widths avoid cached Fill proportions after DPI changes.
        var scale = _files.DeviceDpi / 96f;
        _files.Columns[0].Width = (int)(32 * scale);
        _files.Columns[2].Width = (int)(110 * scale);
        _files.Columns[3].Width = (int)(80 * scale);
        _files.Columns[5].Width = (int)(98 * scale);
        int fixedWidth = new[] { 0, 2, 3, 5 }.Sum(i => _files.Columns[i].Width);
        int available = _files.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - fixedWidth - 2;
        _files.Columns[1].Width = Math.Max((int)(175 * scale), (int)(available * .43));
        _files.Columns[4].Width = Math.Max((int)(190 * scale), available - _files.Columns[1].Width);
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
                _files.Rows[index].Cells[1].ToolTipText = Path.GetFileName(item.Source);
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
            _files.ClearSelection();
            _preferences.LastFolder = plan.Root;
            _status.Text = $"Preview ready · {plan.Items.Count} ready · {plan.Skipped.Count} skipped · No files have moved.";
            SavePreferences();
            UpdateCategoryCounts();
            SetCategoryFilter(_categoryFilter);
        });
    }

    private List<PlanItem> SelectedItems() => _files.Rows.Cast<DataGridViewRow>()
        .Where(r => r.Visible && r.Tag is PlanItem && r.Cells[0].Value is true).Select(r => (PlanItem)r.Tag!).ToList();

    private void SetCategoryFilter(string? category)
    {
        if (_busy && _plan is null) return;
        _categoryFilter = category;
        _files.CurrentCell = null;
        foreach (DataGridViewRow row in _files.Rows)
            row.Visible = category is null || row.Tag is PlanItem item && StringComparer.OrdinalIgnoreCase.Equals(item.Category, category);
        foreach (CategoryChip chip in _categories.Controls)
            chip.Active = StringComparer.OrdinalIgnoreCase.Equals(chip.CategoryName, category);
        _reviewHeading.Text = category is null ? "Review your files" : $"Review · {category}";
        bool any = _files.Rows.Cast<DataGridViewRow>().Any(r => r.Visible);
        _files.Visible = any; _empty.Visible = !any;
        _empty.Text = _plan is null ? "Ready when you are.\nChoose a folder and click Preview files."
            : category is null ? "Nothing to organize here.\nChoose another folder or adjust your sorting rules."
            : $"No {category} files here.\nChoose another category or return to All files.";
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        var selected = SelectedItems();
        var noun = selected.Count == 1 ? "file" : "files";
        _summary.Text = $"{selected.Count} {noun} selected  ·  {Theme.Size(selected.Sum(i => i.Length))}";
        _apply.Text = selected.Count == 0 ? "Move selected" : $"Move {selected.Count} {noun}";
        UpdateButtons();
    }

    private async Task ApplyAsync()
    {
        if (_plan is null) return;
        var selected = SelectedItems();
        if (selected.Count == 0) return;
        var plan = _plan with { Items = selected };
        var destinations = string.Join(", ", selected.Select(i => i.Category).Distinct());
        if (!_confirm($"Move {selected.Count} files into {destinations}?\n\nInside: {plan.Root}\n\nOnly checked files in the current view will move. You can undo this operation.", "Organize files")) return;
        await BusyAsync("Organizing files…", async token =>
        {
            _plan = null;
            var progress = Progress();
            var result = await Task.Run(() => new Organizer(_history).Apply(plan, progress, token), token);
            InvalidatePreview();
            _empty.Text = $"{result.Succeeded} files organized.\nOpen the category folders inside your chosen folder.\nUse Undo latest to restore the files.";
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
        _categoryFilter = null;
        foreach (CategoryChip chip in _categories.Controls) chip.Active = chip.CategoryName is null;
        _reviewHeading.Text = "Review your files";
        _plan = null; _files.Rows.Clear(); _empty.Visible = true; _files.Visible = false;
        _empty.Text = "Ready when you are.\n\nChoose a folder and click Preview files.";
        _status.Text = "Create a fresh preview before organizing files.";
        UpdateSelection();
        UpdateCategoryCounts();
    }

    private void UpdateButtons()
    {
        _browse.Enabled = _preview.Enabled = _rules.Enabled = _demo.Enabled = _folder.Enabled = _files.Enabled = _categories.Enabled = _organize.Enabled = !_busy;
        _undo.Enabled = !_busy && _hasHistory;
        _apply.Enabled = !_busy && _plan is not null && SelectedItems().Count > 0;
        _cancel.Visible = _busy;
        _selectAll.Enabled = _selectNone.Enabled = !_busy && _files.Rows.Cast<DataGridViewRow>().Any(r => r.Visible && r.Tag is PlanItem);
    }

    private void EditRules()
    {
        using var dialog = new RulesForm(_preferences.Rules);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _preferences.Rules = dialog.Result;
        InvalidatePreview(); UpdateRulesSummary(); SavePreferences();
    }

    private void UpdateRulesSummary()
    {
        while (_categories.Controls.Count > 0) _categories.Controls[0].Dispose();
        foreach (var category in new string?[] { null }.Concat(_preferences.Rules.Where(r => r.Enabled).Select(r => r.Folder).Distinct(StringComparer.OrdinalIgnoreCase)))
        {
            var chip = new CategoryChip(category) { Active = StringComparer.OrdinalIgnoreCase.Equals(category, _categoryFilter) };
            chip.Click += (_, _) => SetCategoryFilter(chip.CategoryName);
            if (IsHandleCreated) chip.Scale(new SizeF(DeviceDpi / 96f, DeviceDpi / 96f));
            _categories.Controls.Add(chip);
        }
        UpdateCategoryCounts();
    }

    private void UpdateCategoryCounts()
    {
        foreach (CategoryChip chip in _categories.Controls)
            chip.Count = _plan is null ? null : chip.CategoryName is null ? _files.Rows.Count : _plan.Items.Count(i => StringComparer.OrdinalIgnoreCase.Equals(i.Category, chip.CategoryName));
    }

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
