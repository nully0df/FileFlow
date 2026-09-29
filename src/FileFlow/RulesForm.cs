using FileFlow.Core;

namespace FileFlow;

internal sealed class RulesForm : Form
{
    private readonly DataGridView _grid = Theme.Grid();
    public List<SortingRule> Result { get; private set; } = [];

    public RulesForm(IReadOnlyList<SortingRule> rules)
    {
        Text = "FileFlow · Sorting rules";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(780, 480);
        MinimumSize = new Size(660, 430);
        BackColor = Theme.Background;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new(SizeType.Absolute, 64));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 64));
        layout.Controls.Add(Theme.Label("Map extensions to a folder\nUse simple folder names and comma-separated extensions, such as .jpg, .png.", 10), 0, 0);
        _grid.AllowUserToAddRows = true;
        _grid.AllowUserToDeleteRows = true;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Folder", HeaderText = "Folder", FillWeight = 34 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Extensions", HeaderText = "Extensions", FillWeight = 100 });
        foreach (DataGridViewColumn column in _grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        _grid.DefaultValuesNeeded += (_, e) => e.Row.Cells[0].Value = true;
        Fill(rules);
        layout.Controls.Add(_grid, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 20, 0, 0) };
        var save = Theme.Button("Save rules", true);
        var cancel = Theme.Button("Cancel");
        var defaults = Theme.Button("Restore defaults");
        save.Click += (_, _) => SaveRules();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        defaults.Click += (_, _) => Fill(Rules.Defaults());
        buttons.Controls.AddRange([save, cancel, defaults]);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void Fill(IReadOnlyList<SortingRule> rules)
    {
        _grid.Rows.Clear();
        foreach (var rule in rules) _grid.Rows.Add(rule.Enabled, rule.Folder, string.Join(", ", rule.Extensions));
    }

    private void SaveRules()
    {
        _grid.EndEdit();
        try
        {
            Result = _grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => new SortingRule(
                Convert.ToString(r.Cells[1].Value)?.Trim() ?? "",
                (Convert.ToString(r.Cells[2].Value) ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim().ToLowerInvariant()).ToArray(),
                r.Cells[0].Value is true)).ToList();
            Rules.Validate(Result);
            DialogResult = DialogResult.OK;
        }
        catch (ArgumentException ex) { MessageBox.Show(this, ex.Message, "Check your rules", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }
}
