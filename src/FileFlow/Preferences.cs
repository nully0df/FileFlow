using System.Text.Json;
using FileFlow.Core;

namespace FileFlow;

internal sealed class Preferences
{
    public string LastFolder { get; set; } = "";
    public List<SortingRule> Rules { get; set; } = Core.Rules.Defaults();
    public static string DataDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileFlow");
    private static string FilePath => Path.Combine(DataDirectory, "settings.json");

    public static Preferences Load()
    {
        if (!File.Exists(FilePath)) return new();
        var settings = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath))
            ?? throw new IOException("Settings are empty.");
        Core.Rules.Validate(settings.Rules);
        return settings;
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
