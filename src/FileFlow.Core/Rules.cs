namespace FileFlow.Core;

public static class Rules
{
    public static List<SortingRule> Defaults() =>
    [
        new("Documents", [".pdf", ".doc", ".docx", ".txt", ".rtf", ".odt", ".md", ".csv", ".xlsx", ".xls", ".ppt", ".pptx"]),
        new("Images", [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".heic", ".avif", ".tiff"]),
        new("Video", [".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v"]),
        new("Audio", [".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac"]),
        new("Archives", [".zip", ".7z", ".rar", ".gz", ".tar"]),
        new("Installers", [".exe", ".msi", ".msix"])
    ];

    public static void Validate(IReadOnlyList<SortingRule> rules)
    {
        if (rules is null || rules.Count == 0) throw new ArgumentException("Add at least one rule.");
        HashSet<string> extensions = new(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            if (rule is null) throw new ArgumentException("A sorting rule cannot be empty.");
            ValidateFolder(rule.Folder);
            if (rule.Extensions is null || rule.Extensions.Length == 0)
                throw new ArgumentException($"{rule.Folder}: add at least one extension.");
            foreach (var extension in rule.Extensions)
            {
                if (string.IsNullOrWhiteSpace(extension) || extension.Length < 2 || extension[0] != '.' ||
                    extension[1..].Any(c => !char.IsAsciiLetterOrDigit(c)))
                    throw new ArgumentException($"Invalid extension '{extension}'. Use .pdf, .jpg, etc.");
                if (rule.Enabled && !extensions.Add(extension))
                    throw new ArgumentException($"Extension {extension} appears in more than one enabled rule.");
            }
        }
    }

    public static void ValidateFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || folder.Length > 80 || folder != folder.Trim() ||
            folder.EndsWith('.') || folder is "." or ".." || folder.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)))
            throw new ArgumentException("Folder names must be simple names, such as Documents or Photos.");
        string stem = folder.Split('.')[0];
        if (new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                                  stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
             "123456789¹²³".Contains(stem[3])))
            throw new ArgumentException($"'{folder}' is a reserved Windows name.");
    }
}
