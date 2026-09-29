using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileFlow.Core;

public sealed class JournalStore(string directory)
{
    private readonly string _directory = Path.GetFullPath(directory);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };

    internal FileStream Lock()
    {
        Directory.CreateDirectory(_directory);
        return new FileStream(Path.Combine(_directory, ".operation.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
    }

    public void Save(MoveJournal journal)
    {
        if (!Guid.TryParseExact(journal.Id, "N", out _)) throw new IOException("Invalid journal ID.");
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, journal.Id + ".json");
        var temporary = target + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, journal, Options);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, target, overwrite: true);
    }

    public MoveJournal? LatestUndoable()
    {
        if (!Directory.Exists(_directory)) return null;
        List<MoveJournal> journals = [];
        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            // Fail visibly on damaged history; silently ignoring it could undo an older batch.
            var journal = JsonSerializer.Deserialize<MoveJournal>(File.ReadAllText(file), Options)
                ?? throw new IOException("The operation history could not be read.");
            if (journal.Version != 1 || !Guid.TryParseExact(journal.Id, "N", out _) ||
                !string.Equals(Path.GetFileNameWithoutExtension(file), journal.Id, StringComparison.Ordinal) ||
                journal.Entries is null || journal.Entries.Any(e => e is null || !Enum.IsDefined(e.State)))
                throw new IOException("Unsupported or damaged operation history.");
            if (journal.Entries.Any(e => e.State is MoveState.Moved or MoveState.Prepared)) journals.Add(journal);
        }
        return journals.OrderByDescending(j => j.CreatedUtc).FirstOrDefault();
    }
}
