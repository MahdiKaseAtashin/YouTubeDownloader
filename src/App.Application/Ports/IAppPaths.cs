namespace App.Application.Ports;

public interface IAppPaths
{
    string DataDirectory { get; }
    string RegistryFilePath { get; }
    string ExecutionLogFilePath { get; }
    string ApplicationLogFilePath { get; }
    string PreferencesFilePath { get; }
}
