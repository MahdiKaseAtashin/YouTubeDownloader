namespace App.Infrastructure.YouTube;

internal sealed record JsRuntimeInfo(string Kind, string ExecutablePath);

internal static class JsRuntimeLocator
{
    public static JsRuntimeInfo? Resolve()
    {
        foreach (var candidate in GetLocalCandidates())
        {
            if (File.Exists(candidate.Path))
            {
                return new JsRuntimeInfo(candidate.Kind, candidate.Path);
            }
        }

        foreach (var (kind, name) in new[] { ("node", "node.exe"), ("deno", "deno.exe") })
        {
            var fromPath = ResolveFromPath(name);
            if (fromPath is not null)
            {
                return new JsRuntimeInfo(kind, fromPath);
            }
        }

        return null;
    }

    private static IEnumerable<(string Kind, string Path)> GetLocalCandidates()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return ("node", Path.Combine(baseDir, "node.exe"));
        yield return ("node", Path.Combine(baseDir, "tools", "node.exe"));
        yield return ("deno", Path.Combine(baseDir, "deno.exe"));
        yield return ("deno", Path.Combine(baseDir, "tools", "deno.exe"));

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return ("node", Path.Combine(programFiles, "nodejs", "node.exe"));
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
