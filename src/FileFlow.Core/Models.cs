namespace FileFlow.Core;

public sealed record SortingRule(string Folder, string[] Extensions, bool Enabled = true);
public sealed record PlanItem(string Source, string Destination, long Length, long ModifiedUtcTicks,
    string Category, bool Renamed);
public sealed record SkippedFile(string Name, string Reason);
public sealed record SortingPlan(string Root, IReadOnlyList<PlanItem> Items, IReadOnlyList<SkippedFile> Skipped);
public sealed record OperationProgress(int Completed, int Total, string FileName);
public sealed record OperationResult(int Succeeded, IReadOnlyList<string> Problems, bool Cancelled);
public enum MoveState { Prepared, Moved, Undone, Failed }

public sealed class MoveEntry
{
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public MoveState State { get; set; }
}

public sealed class MoveJournal
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Root { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<MoveEntry> Entries { get; set; } = [];
}
