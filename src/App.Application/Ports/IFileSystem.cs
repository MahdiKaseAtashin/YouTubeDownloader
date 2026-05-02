namespace App.Application.Ports;

public interface IFileSystem
{
    bool FileExists(string path);
    string GetFullPath(string path);
    string GetFileNameWithoutExtension(string path);
    string GetExtension(string path);
    string? GetDirectoryName(string path);
}
