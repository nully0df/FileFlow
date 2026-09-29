namespace FileFlow;

public sealed class DecisionForm : FlowForm
{
    public DecisionForm(string message, string title)
    {
        SuspendLayout();
        Text = "FileFlow · " + title;
        ClientSize = new Size(580, 385); MinimumSize = new Size(500, 380);
        StartPosition = FormStartPosition.CenterParent; MinimizeBox = MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new(SizeType.Absolute, 65)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 66));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        heading.ColumnStyles.Add(new(SizeType.Absolute, 60)); heading.ColumnStyles.Add(new(SizeType.Percent, 100));
        heading.Controls.Add(new BrandView { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 16, 18) }, 0, 0);
        heading.Controls.Add(Theme.Label(title, 20, true), 1, 0); layout.Controls.Add(heading, 0, 0);
        var card = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(18), Margin = Padding.Empty };
        card.Controls.Add(new TextBox { Dock = DockStyle.Fill, Text = message.ReplaceLineEndings(Environment.NewLine), Multiline = true, ReadOnly = true, WordWrap = true,
            ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Ink,
            Font = new Font("Segoe UI", 10), AccessibleName = "Operation details", TabStop = true });
        layout.Controls.Add(card, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 20, 0, 0), Margin = Padding.Empty };
        var proceed = Theme.Button(title == "Undo latest" ? "Restore files" : "Move files", true);
        var cancel = Theme.Button("Cancel"); cancel.Appearance = ButtonAppearance.Quiet;
        proceed.Click += (_, _) => DialogResult = DialogResult.OK;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.AddRange([proceed, cancel]); layout.Controls.Add(buttons, 0, 2); Controls.Add(layout);
        AcceptButton = proceed; CancelButton = cancel;
        // Cancellation starts focused, so opening a confirmation never commits a move by accident.
        Shown += (_, _) => cancel.Focus();
        AutoScaleDimensions = new SizeF(96, 96);
        ResumeLayout(true);
    }

    internal static bool Confirm(IWin32Window owner, string message, string title)
    {
        using var form = new DecisionForm(message, title);
        return form.ShowDialog(owner) == DialogResult.OK;
    }
}
