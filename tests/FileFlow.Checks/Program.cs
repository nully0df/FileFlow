using System.Security.Cryptography;
using FileFlow.Core;

var checks = new List<(string Name, Action<Fixture> Run)>
{
    ("Preview classifies case-insensitively without modifying files", f =>
    {
        f.Write("Photo.JPG", "photo"); f.Write("notes.txt", "notes");
        var plan = f.Preview();
        Check(plan.Items.Count == 2 && plan.Items.Any(i => i.Category == "Images"), "Classification failed");
        Check(File.Exists(f.At("Photo.JPG")) && !Directory.Exists(f.At("Images")), "Preview mutated disk");
    }),
    ("Unmatched files and subfolders stay untouched", f =>
    {
        f.Write("data.unknown", "unknown"); f.Write("nested/notes.txt", "nested");
        var plan = f.Preview();
        Check(plan.Items.Count == 0 && plan.Skipped.Count == 1, "Nested or unmatched file was included");
    }),
    ("Existing names get a stable collision suffix", f =>
    {
        f.Write("a.txt", "new"); f.Write("Documents/a.txt", "old"); f.Write("Documents/a (1).txt", "older");
        var plan = f.Preview();
        Check(plan.Items.Single().Destination == f.At("Documents/a (2).txt"), "Wrong suffix");
        Check(plan.Items.Single().Renamed, "Rename not displayed");
        Check(f.Apply(plan).Succeeded == 1 && f.Read("Documents/a.txt") == "old", "Existing content overwritten");
    }),
    ("A folder with the destination filename is also a conflict", f =>
    {
        f.Write("a.txt", "a"); Directory.CreateDirectory(f.At("Documents/a.txt"));
        Check(f.Preview().Items.Single().Renamed, "Directory collision missed");
    }),
    ("Category path occupied by a file is skipped", f =>
    {
        f.Write("Documents", "reserved"); f.Write("a.txt", "a");
        Check(f.Preview().Items.Count == 0, "Invalid destination accepted");
    }),
    ("Disabled rules do not classify files", f =>
    {
        f.Write("a.txt", "a");
        Check(new Planner().Preview(f.Root, [new("Docs", [".txt"], false)]).Items.Count == 0, "Disabled rule used");
    }),
    ("Custom rules use the requested folder", f =>
    {
        f.Write("drawing.psd", "design");
        var plan = new Planner().Preview(f.Root, [new("Design", [".psd"])]);
        Check(f.Apply(plan).Succeeded == 1 && f.Read("Design/drawing.psd") == "design", "Custom rule failed");
    }),
    ("Unicode and multiple dots are preserved", f =>
    {
        f.Write("Заметки.сентябрь.txt", "Привет");
        Check(f.Apply(f.Preview()).Succeeded == 1, "Unicode move failed");
        Check(f.Undo().Succeeded == 1 && f.Read("Заметки.сентябрь.txt") == "Привет", "Unicode undo failed");
    }),
    ("Undo survives service restart and preserves contents", f =>
    {
        f.Write("a.txt", "content"); f.Write("b.jpg", "picture");
        Check(f.Apply(f.Preview()).Succeeded == 2, "Move failed");
        Check(new Organizer(new JournalStore(f.History)).Undo().Succeeded == 2, "Persisted undo failed");
        Check(f.Read("a.txt") == "content" && f.Read("b.jpg") == "picture", "Content changed");
        Check(f.Store.LatestUndoable() is null, "Finished history still active");
    }),
    ("Changed source after preview is skipped", f =>
    {
        f.Write("a.txt", "before"); var plan = f.Preview(); f.Write("a.txt", "after: changed length");
        var result = f.Apply(plan);
        Check(result.Succeeded == 0 && result.Problems.Count == 1 && File.Exists(f.At("a.txt")), "Stale preview moved");
    }),
    ("Deleted source does not interrupt other moves", f =>
    {
        f.Write("a.txt", "a"); f.Write("b.txt", "b"); var plan = f.Preview(); File.Delete(f.At("a.txt"));
        var result = f.Apply(plan);
        Check(result.Succeeded == 1 && result.Problems.Count == 1 && f.Read("Documents/b.txt") == "b", "Partial result wrong");
    }),
    ("A late destination conflict never overwrites or silently renames", f =>
    {
        f.Write("a.txt", "source"); var plan = f.Preview(); f.Write("Documents/a.txt", "arrived later");
        Check(f.Apply(plan).Succeeded == 0 && f.Read("a.txt") == "source" && f.Read("Documents/a.txt") == "arrived later", "Late conflict unsafe");
    }),
    ("Selected subset moves only selected files", f =>
    {
        f.Write("a.txt", "a"); f.Write("b.txt", "b"); var plan = f.Preview();
        Check(f.Apply(plan with { Items = [plan.Items[0]] }).Succeeded == 1 && File.Exists(f.At("b.txt")), "Selection ignored");
    }),
    ("Undo refuses modified files even when timestamp and length match", f =>
    {
        f.Write("a.txt", "one"); f.Apply(f.Preview()); var timestamp = File.GetLastWriteTimeUtc(f.At("Documents/a.txt"));
        f.Write("Documents/a.txt", "two"); File.SetLastWriteTimeUtc(f.At("Documents/a.txt"), timestamp);
        Check(f.Undo().Succeeded == 0 && f.Read("Documents/a.txt") == "two", "Hash guard failed");
        Check(f.Store.LatestUndoable() is not null, "Unresolved operation lost");
    }),
    ("Undo never overwrites a recreated original", f =>
    {
        f.Write("a.txt", "original"); f.Apply(f.Preview()); f.Write("a.txt", "new file");
        Check(f.Undo().Succeeded == 0 && f.Read("a.txt") == "new file" && f.Read("Documents/a.txt") == "original", "Undo overwrote data");
    }),
    ("Partial undo can be retried after a conflict is resolved", f =>
    {
        f.Write("a.txt", "a"); f.Write("b.txt", "b"); f.Apply(f.Preview()); f.Write("a.txt", "conflict");
        Check(f.Undo().Succeeded == 1, "Unaffected file not restored");
        File.Delete(f.At("a.txt"));
        Check(f.Undo().Succeeded == 1 && f.Read("a.txt") == "a", "Retry failed");
    }),
    ("Undo proceeds from newest batch to older batches", f =>
    {
        f.Write("a.txt", "a"); f.Apply(f.Preview());
        var first = f.Store.LatestUndoable()!; first.CreatedUtc = DateTime.UtcNow.AddDays(-1); f.Store.Save(first);
        f.Write("b.txt", "b"); f.Apply(f.Preview());
        Check(f.Undo().Succeeded == 1 && File.Exists(f.At("b.txt")) && !File.Exists(f.At("a.txt")), "Wrong batch undone");
        Check(f.Undo().Succeeded == 1 && File.Exists(f.At("a.txt")), "Previous batch unavailable");
    }),
    ("Prepared journal recovers a crash after moving", f =>
    {
        f.Write("a.txt", "a"); var plan = f.Preview();
        var journal = f.Prepared(plan.Items.Single());
        Directory.CreateDirectory(f.At("Documents")); File.Move(f.At("a.txt"), f.At("Documents/a.txt"));
        Check(f.Undo().Succeeded == 1 && f.Read("a.txt") == "a", "Crash recovery failed");
    }),
    ("Prepared journal with no move is reconciled without touching content", f =>
    {
        f.Write("a.txt", "a"); f.Prepared(f.Preview().Items.Single());
        var result = f.Undo();
        Check(result.Succeeded == 0 && result.Problems.Count == 0 && f.Read("a.txt") == "a" && f.Store.LatestUndoable() is null, "Prepared state wrong");
    }),
    ("Undo recovers a crash after restoring but before saving history", f =>
    {
        f.Write("a.txt", "a"); f.Apply(f.Preview()); File.Move(f.At("Documents/a.txt"), f.At("a.txt"));
        Check(f.Undo().Problems.Count == 0 && f.Store.LatestUndoable() is null, "Undo crash state unrecoverable");
    }),
    ("Cancelled execution leaves files untouched", f =>
    {
        f.Write("a.txt", "a"); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var result = new Organizer(f.Store).Apply(f.Preview(), cancellation: cancel.Token);
        Check(result.Cancelled && result.Succeeded == 0 && File.Exists(f.At("a.txt")), "Cancellation ignored");
    }),
    ("Cancellation after first move preserves a usable undo journal", f =>
    {
        f.Write("a.txt", "a"); f.Write("b.txt", "b"); using var cancel = new CancellationTokenSource();
        var result = new Organizer(f.Store).Apply(f.Preview(), new InlineProgress(_ => cancel.Cancel()), cancel.Token);
        Check(result.Cancelled && result.Succeeded == 1 && f.Undo().Succeeded == 1, "Partial cancel lost history");
    }),
    ("Concurrent operations are refused before moving files", f =>
    {
        f.Write("a.txt", "a"); Directory.CreateDirectory(f.History);
        using var held = new FileStream(Path.Combine(f.History, ".operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Throws<IOException>(() => f.Apply(f.Preview())); Check(File.Exists(f.At("a.txt")), "Lock failed");
    }),
    ("A forged outside-root plan cannot move a file", f =>
    {
        f.Write("a.txt", "a"); var plan = f.Preview();
        var bad = plan.Items[0] with { Destination = Path.Combine(f.Base, "outside.txt") };
        Check(f.Apply(plan with { Items = [bad] }).Succeeded == 0 && File.Exists(f.At("a.txt")), "Path containment failed");
    }),
    ("A forged journal cannot restore outside the source folder", f =>
    {
        f.Write("a.txt", "a"); f.Apply(f.Preview()); var journal = f.Store.LatestUndoable()!;
        journal.Entries[0].Source = Path.Combine(f.Base, "outside.txt"); f.Store.Save(journal);
        Check(f.Undo().Succeeded == 0 && File.Exists(f.At("Documents/a.txt")), "Unsafe history accepted");
    }),
    ("Invalid folders and duplicate extensions are rejected", _ =>
    {
        foreach (var folder in new[] { "..", "../outside", "a/b", "a\\b", "CON", "NUL.txt", "COM1", "docs.", " docs" })
            Throws<ArgumentException>(() => Rules.Validate([new(folder, [".txt"])]));
        Throws<ArgumentException>(() => Rules.Validate([new("A", [".TXT"]), new("B", [".txt"])]));
        Throws<ArgumentException>(() => Rules.Validate([new("A", [".*"])]));
    }),
    ("Corrupt history reports an error instead of silently undoing older work", f =>
    {
        Directory.CreateDirectory(f.History); File.WriteAllText(Path.Combine(f.History, "broken.json"), "{nope");
        Throws<System.Text.Json.JsonException>(() => f.Store.LatestUndoable());
    }),
    ("Undo retains category folders and unrelated new content", f =>
    {
        f.Write("a.txt", "a"); f.Apply(f.Preview()); f.Write("Documents/new.txt", "new"); f.Undo();
        Check(f.Read("Documents/new.txt") == "new" && Directory.Exists(f.At("Documents")), "Unrelated data removed");
    }),
    ("Hidden files are skipped on Windows", f =>
    {
        if (!OperatingSystem.IsWindows()) return;
        f.Write("hidden.txt", "secret"); File.SetAttributes(f.At("hidden.txt"), FileAttributes.Hidden);
        Check(f.Preview().Items.Count == 0 && f.Preview().Skipped.Count == 1, "Hidden file included");
    }),
    ("Missing folder produces an actionable error", f => Throws<DirectoryNotFoundException>(() => new Planner().Preview(f.At("missing"), Rules.Defaults())))
};

int failures = 0;
foreach (var check in checks)
{
    using var fixture = new Fixture();
    try { check.Run(fixture); Console.WriteLine($"PASS {check.Name}"); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {check.Name}: {ex.Message}"); }
}
Console.WriteLine($"\n{checks.Count - failures}/{checks.Count} checks passed.");
return failures == 0 ? 0 : 1;

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

internal sealed class InlineProgress(Action<OperationProgress> action) : IProgress<OperationProgress>
{
    public void Report(OperationProgress value) => action(value);
}

internal sealed class Fixture : IDisposable
{
    public string Base { get; } = Path.Combine(Path.GetTempPath(), "FileFlow.Checks", Guid.NewGuid().ToString("N"));
    public string Root => Path.Combine(Base, "source");
    public string History => Path.Combine(Base, "history");
    public JournalStore Store => new(History);
    public Fixture() => Directory.CreateDirectory(Root);
    public string At(string relative) => Path.GetFullPath(Path.Combine(Root, relative));
    public void Write(string relative, string content)
    {
        var path = At(relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content);
    }
    public string Read(string relative) => File.ReadAllText(At(relative));
    public SortingPlan Preview() => new Planner().Preview(Root, Rules.Defaults());
    public OperationResult Apply(SortingPlan plan) => new Organizer(Store).Apply(plan);
    public OperationResult Undo() => new Organizer(Store).Undo();
    public MoveJournal Prepared(PlanItem item)
    {
        var journal = new MoveJournal { Root = Root, Entries = [new() { Source = item.Source, Destination = item.Destination,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(item.Source))), State = MoveState.Prepared }] };
        Store.Save(journal); return journal;
    }
    public void Dispose()
    {
        foreach (var path in Directory.EnumerateFiles(Base, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(Base, recursive: true);
    }
}
