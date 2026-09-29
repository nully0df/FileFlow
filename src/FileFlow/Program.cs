namespace FileFlow;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--data-dir") Preferences.DataDirectory = Path.GetFullPath(args[1]);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
