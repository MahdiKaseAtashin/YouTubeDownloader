namespace App.Infrastructure.YouTube;

internal static class YtDlpLocator
{
    public static string? ResolveExecutable()
    {
        foreach (var candidate in GetLocalCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        foreach (var name in new[] { "yt-dlp.exe", "yt-dlp" })
        {
            var fromPath = ResolveFromPath(name);
            if (fromPath is not null)
            {
                return fromPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetLocalCandidates()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "yt-dlp.exe");
        yield return Path.Combine(baseDir, "tools", "yt-dlp.exe");
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
