namespace App.Infrastructure.YouTube;

internal static class FfmpegLocator
{
    public static string? ResolveLocation()
    {
        foreach (var directory in GetLocalDirectories())
        {
            if (DirectoryContainsFfmpeg(directory))
            {
                return directory;
            }
        }

        var fromPath = ResolveFromPath("ffmpeg.exe");
        if (fromPath is not null)
        {
            return Path.GetDirectoryName(fromPath);
        }

        return null;
    }

    private static IEnumerable<string> GetLocalDirectories()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return baseDir;
        yield return Path.Combine(baseDir, "tools");
    }

    private static bool DirectoryContainsFfmpeg(string directory)
    {
        try
        {
            return File.Exists(Path.Combine(directory, "ffmpeg.exe"));
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveFromPath(string executableName)
    {
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            try
            {
                var candidate = Path.Combine(folder.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // skip invalid path entry
            }
        }

        return null;
    }
}
