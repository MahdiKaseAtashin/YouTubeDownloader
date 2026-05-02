using App.Application.Ports;

namespace App.Infrastructure.FileSystem;

public sealed class DefaultFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);

    public string GetExtension(string path) => Path.GetExtension(path);

    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
}
