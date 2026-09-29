using System.Security.Cryptography;

namespace FileFlow.Core;

public sealed class Organizer(JournalStore store)
{
    public OperationResult Apply(SortingPlan plan, IProgress<OperationProgress>? progress = null,
        CancellationToken cancellation = default)
    {
        using var operationLock = store.Lock();
        var root = PathSafety.Root(plan.Root);
        MoveJournal journal = new() { Root = root };
        List<string> problems = [];
        var succeeded = 0;
        for (int index = 0; index < plan.Items.Count; index++)
        {
            if (cancellation.IsCancellationRequested) return new(succeeded, problems, true);
            var item = plan.Items[index];
            MoveEntry? entry = null;
            var moved = false;
            try
            {
                PathSafety.ValidatePair(root, item.Source, item.Destination);
                PathSafety.RegularFile(item.Source);
                using var source = OpenForMove(item.Source);
                if (source.Length != item.Length || File.GetLastWriteTimeUtc(item.Source).Ticks != item.ModifiedUtcTicks)
                    throw new IOException("The file changed after preview. Create a new preview.");
                var hash = Hash(source, cancellation);
                if (PathSafety.Exists(item.Destination)) throw new IOException("The destination is now occupied. Create a new preview.");
                Directory.CreateDirectory(Path.GetDirectoryName(item.Destination)!);
                PathSafety.ValidatePair(root, item.Source, item.Destination);
                entry = new() { Source = item.Source, Destination = item.Destination, Sha256 = hash, State = MoveState.Prepared };
                journal.Entries.Add(entry);
                // Persist intent before moving. A crash between move and save remains recoverable.
                store.Save(journal);
                File.Move(item.Source, item.Destination, overwrite: false);
                moved = true;
                succeeded++;
                entry.State = MoveState.Moved;
                store.Save(journal);
            }
            catch (OperationCanceledException) { return new(succeeded, problems, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Stop on a journal write failure after a successful move. The persisted Prepared
                // record is deliberately preserved for the next Undo attempt.
                if (moved) throw new IOException("A file moved, but its journal update failed. Stop and use Undo after restoring access to history.", ex);
                problems.Add($"{Path.GetFileName(item.Source)}: {ex.Message}");
                if (entry is not null) { entry.State = MoveState.Failed; store.Save(journal); }
            }
            progress?.Report(new(index + 1, plan.Items.Count, Path.GetFileName(item.Source)));
        }
        return new(succeeded, problems, false);
    }

    public OperationResult Undo(IProgress<OperationProgress>? progress = null, CancellationToken cancellation = default)
    {
        using var operationLock = store.Lock();
        var journal = store.LatestUndoable();
        if (journal is null) return new(0, ["No operations to undo."], false);
        var root = PathSafety.Root(journal.Root);
        var candidates = journal.Entries.Where(e => e.State is MoveState.Moved or MoveState.Prepared).Reverse().ToArray();
        List<string> problems = [];
        int succeeded = 0;
        for (int index = 0; index < candidates.Length; index++)
        {
            if (cancellation.IsCancellationRequested) return new(succeeded, problems, true);
            var entry = candidates[index];
            var restored = false;
            try
            {
                PathSafety.ValidatePair(root, entry.Source, entry.Destination);
                if (!PathSafety.Exists(entry.Destination))
                {
                    // Also recovers a crash after Undo's rename and before its journal save.
                    PathSafety.RegularFile(entry.Source);
                    using var original = OpenForMove(entry.Source);
                    if (Hash(original, cancellation) != entry.Sha256)
                        throw new IOException("The original no longer matches the operation history.");
                    entry.State = MoveState.Undone;
                    store.Save(journal);
                    continue;
                }
                if (PathSafety.Exists(entry.Source)) throw new IOException("The original location is occupied; nothing was overwritten.");
                PathSafety.RegularFile(entry.Destination);
                using var destination = OpenForMove(entry.Destination);
                if (Hash(destination, cancellation) != entry.Sha256)
                    throw new IOException("The sorted file was edited; it was left in place.");
                PathSafety.ValidatePair(root, entry.Source, entry.Destination);
                File.Move(entry.Destination, entry.Source, overwrite: false);
                restored = true;
                succeeded++;
                entry.State = MoveState.Undone;
                store.Save(journal);
            }
            catch (OperationCanceledException) { return new(succeeded, problems, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                if (restored) throw new IOException("A file was restored, but history could not be updated. Retry Undo after restoring access to history.", ex);
                problems.Add($"{Path.GetFileName(entry.Source)}: {ex.Message}");
            }
            progress?.Report(new(index + 1, candidates.Length, Path.GetFileName(entry.Source)));
        }
        // Keep empty category folders: they may contain user-created metadata or new files.
        return new(succeeded, problems, false);
    }

    private static FileStream OpenForMove(string path) => new(path, FileMode.Open, FileAccess.Read,
        FileShare.Read | FileShare.Delete, 128 * 1024, FileOptions.SequentialScan);

    private static string Hash(Stream stream, CancellationToken cancellation)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[128 * 1024];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, read);
        }
        cancellation.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
