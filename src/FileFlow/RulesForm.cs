using FileFlow.Core;

namespace FileFlow;

internal sealed class RulesForm : FlowForm
{
    private readonly DataGridView _grid = Theme.Grid();
    private readonly Label _validation = Theme.Label("", 9, color: Color.FromArgb(155, 83, 44));
    public List<SortingRule> Result { get; private set; } = [];

    public RulesForm(IReadOnlyList<SortingRule> rules)
    {
        SuspendLayout();
        Text = "FileFlow · Sorting rules";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(860, 625); MinimumSize = new Size(700, 510);
        MinimizeBox = MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new(SizeType.Absolute, 86)); layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 32)); layout.RowStyles.Add(new(SizeType.Absolute, 62));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        header.RowStyles.Add(new(SizeType.Absolute, 42)); header.RowStyles.Add(new(SizeType.Percent, 100));
        header.Controls.Add(Theme.Label("A home for every type.", 22, true), 0, 0);
        header.Controls.Add(Theme.Label("Choose where files belong. Separate extensions with commas: .jpg, .png", 10, color: Theme.Muted), 0, 1);
        layout.Controls.Add(header, 0, 0);
        var card = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(14), Margin = Padding.Empty };
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
        editor.RowStyles.Add(new(SizeType.Percent, 100)); editor.RowStyles.Add(new(SizeType.Absolute, 45));
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AccessibleName = "Sorting rules editor";
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", Width = 48, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Folder", HeaderText = "Destination folder", FillWeight = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Extensions", HeaderText = "File extensions", FillWeight = 70 });
        foreach (DataGridViewColumn column in _grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        _grid.DefaultValuesNeeded += (_, e) => e.Row.Cells[0].Value = true;
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        Fill(rules); editor.Controls.Add(_grid, 0, 0);
        var editButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0), BackColor = Color.Transparent, Margin = Padding.Empty };
        var add = Theme.Button("Add rule"); var remove = Theme.Button("Remove selected");
        add.Appearance = remove.Appearance = ButtonAppearance.Quiet;
        add.Width = 110; remove.Width = 170;
        add.Height = remove.Height = 38;
        add.Click += (_, _) => { var index = _grid.Rows.Add(true, "New folder", ".ext"); _grid.CurrentCell = _grid.Rows[index].Cells[1]; _grid.BeginEdit(true); };
        remove.Click += (_, _) => { if (_grid.CurrentRow is { IsNewRow: false } row) _grid.Rows.Remove(row); };
        editButtons.Controls.AddRange([add, remove]); editor.Controls.Add(editButtons, 0, 1); card.Controls.Add(editor); layout.Controls.Add(card, 0, 1);
        layout.Controls.Add(_validation, 0, 2);
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 14, 0, 0), Margin = Padding.Empty };
        buttons.ColumnStyles.Add(new(SizeType.Absolute, 174)); buttons.ColumnStyles.Add(new(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new(SizeType.Absolute, 115)); buttons.ColumnStyles.Add(new(SizeType.Absolute, 150));
        var save = Theme.Button("Save rules", true); var cancel = Theme.Button("Cancel"); var defaults = Theme.Button("Restore defaults");
        cancel.Appearance = ButtonAppearance.Quiet;
        foreach (var button in new[] { save, cancel, defaults }) button.Dock = DockStyle.Fill;
        save.Click += (_, _) => SaveRules(); cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        defaults.Click += (_, _) => { Fill(Rules.Defaults()); _validation.Text = "Defaults restored. Save to apply."; };
        buttons.Controls.Add(defaults, 0, 0); buttons.Controls.Add(cancel, 2, 0); buttons.Controls.Add(save, 3, 0);
        layout.Controls.Add(buttons, 0, 3); Controls.Add(layout);
        AcceptButton = save; CancelButton = cancel;
        AutoScaleDimensions = new SizeF(96, 96);
        ResumeLayout(true);
    }

    private void Fill(IReadOnlyList<SortingRule> rules)
    {
        _grid.Rows.Clear();
        foreach (var rule in rules) _grid.Rows.Add(rule.Enabled, rule.Folder, string.Join(", ", rule.Extensions));
        _grid.ClearSelection();
    }

    private void SaveRules()
    {
        _grid.EndEdit();
        try
        {
            Result = _grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => new SortingRule(
                Convert.ToString(r.Cells[1].Value)?.Trim() ?? "",
                (Convert.ToString(r.Cells[2].Value) ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim().ToLowerInvariant()).ToArray(), r.Cells[0].Value is true)).ToList();
            Rules.Validate(Result); DialogResult = DialogResult.OK;
        }
        catch (ArgumentException ex) { _validation.Text = ex.Message; }
    }
}
