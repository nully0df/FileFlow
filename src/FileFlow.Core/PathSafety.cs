namespace FileFlow.Core;

internal static class PathSafety
{
    internal static StringComparer Comparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static string Root(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException("Choose an existing folder.");
        CheckParents(full);
        return full;
    }

    // Check the complete existing chain, including junctions and cloud placeholders.
    internal static void CheckParents(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Symbolic links, junctions and cloud placeholders are not supported.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    internal static bool Exists(string path)
    {
        try { File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    internal static void ValidatePair(string root, string source, string destination)
    {
        if (!Path.IsPathFullyQualified(source) || !Path.IsPathFullyQualified(destination))
            throw new IOException("Invalid relative path in operation.");
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        var category = Path.GetDirectoryName(destination);
        if (!Comparer.Equals(Path.GetDirectoryName(source), root) || category is null ||
            !Comparer.Equals(Path.GetDirectoryName(category), root))
            throw new IOException("An operation points outside the selected folder.");
        Rules.ValidateFolder(Path.GetFileName(category));
        CheckParents(source);
        CheckParents(destination);
    }

    internal static void RegularFile(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory |
                           FileAttributes.Hidden | FileAttributes.System)) != 0)
            throw new IOException("Hidden, system and linked files are left untouched.");
    }
}
