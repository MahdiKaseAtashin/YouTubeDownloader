namespace App.Infrastructure.YouTube;

internal static class YouTubeCookieJarInspector
{
    private static readonly HashSet<string> SessionCookieNames = new(StringComparer.Ordinal)
    {
        "LOGIN_INFO",
        "__Secure-1PSID",
        "__Secure-3PSID",
        "SID"
    };

    public static bool HasYouTubeSessionCookies(string cookieFilePath)
    {
        if (!File.Exists(cookieFilePath))
        {
            return false;
        }

        foreach (var line in File.ReadLines(cookieFilePath))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length < 7)
            {
                continue;
            }

            var domain = parts[0];
            var name = parts[5];
            if (!domain.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (SessionCookieNames.Contains(name) && !string.IsNullOrWhiteSpace(parts[6]))
            {
                return true;
            }
        }

        return false;
    }
}
