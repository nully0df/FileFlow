namespace FileFlow.Core;

public sealed class Planner
{
    public SortingPlan Preview(string directory, IReadOnlyList<SortingRule> rules, CancellationToken cancellation = default)
    {
        Rules.Validate(rules);
        var root = PathSafety.Root(directory);
        var lookup = rules.Where(r => r.Enabled).SelectMany(r => r.Extensions.Select(e => (Extension: e, r.Folder)))
            .ToDictionary(r => r.Extension, r => r.Folder, StringComparer.OrdinalIgnoreCase);
        List<PlanItem> items = [];
        List<SkippedFile> skipped = [];
        HashSet<string> reserved = new(PathSafety.Comparer);
        foreach (var source in Directory.EnumerateFiles(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellation.ThrowIfCancellationRequested();
            var name = Path.GetFileName(source);
            try
            {
                PathSafety.RegularFile(source);
                if (!lookup.TryGetValue(Path.GetExtension(source), out var folder))
                {
                    skipped.Add(new(name, "No matching enabled rule"));
                    continue;
                }
                var targetFolder = Path.Combine(root, folder);
                PathSafety.CheckParents(targetFolder);
                if (File.Exists(targetFolder)) throw new IOException($"'{folder}' is a file, not a folder.");
                var destination = Path.Combine(targetFolder, name);
                var renamed = false;
                for (int suffix = 1; PathSafety.Exists(destination) || reserved.Contains(destination); suffix++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (suffix > 10000) throw new IOException("Too many name conflicts.");
                    destination = Path.Combine(targetFolder, $"{Path.GetFileNameWithoutExtension(source)} ({suffix}){Path.GetExtension(source)}");
                    renamed = true;
                }
                reserved.Add(destination);
                var info = new FileInfo(source);
                items.Add(new(source, destination, info.Length, info.LastWriteTimeUtc.Ticks, folder, renamed));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                skipped.Add(new(name, ex.Message));
            }
        }
        return new(root, items.AsReadOnly(), skipped.AsReadOnly());
    }
}
