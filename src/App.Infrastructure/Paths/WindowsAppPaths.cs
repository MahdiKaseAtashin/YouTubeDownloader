using App.Application.Ports;

namespace App.Infrastructure.Paths;

public sealed class WindowsAppPaths : IAppPaths
{
    public WindowsAppPaths()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YouTubeDownloader");

        DataDirectory = root;
        RegistryFilePath = Path.Combine(root, "scripts.json");
        ExecutionLogFilePath = Path.Combine(root, "execution-history.jsonl");
        ApplicationLogFilePath = Path.Combine(root, "logs", "app-.log");
        PreferencesFilePath = Path.Combine(root, "preferences.json");

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.GetDirectoryName(ApplicationLogFilePath)!);
    }

    public string DataDirectory { get; }
    public string RegistryFilePath { get; }
    public string ExecutionLogFilePath { get; }
    public string ApplicationLogFilePath { get; }
    public string PreferencesFilePath { get; }
}
