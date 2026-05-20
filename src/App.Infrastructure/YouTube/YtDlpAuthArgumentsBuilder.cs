using App.Application.Dtos;
using System.IO;

namespace App.Infrastructure.YouTube;

internal static class YtDlpAuthArgumentsBuilder
{
    public static void AppendAuthArguments(List<string> args, YouTubeAuthSettings? settings)
    {
        if (settings is null || settings.Mode == YouTubeAuthMode.None)
        {
            return;
        }

        if (settings.Mode == YouTubeAuthMode.BrowserCookies)
        {
            if (string.IsNullOrWhiteSpace(settings.Browser))
            {
                return;
            }

            args.Add("--cookies-from-browser");
            var browser = settings.Browser.Trim();
            var profile = settings.BrowserProfile?.Trim();
            if (string.IsNullOrWhiteSpace(profile) && !string.IsNullOrWhiteSpace(settings.BrowserProfileDirectoryPath))
            {
                profile = Path.GetFileName(settings.BrowserProfileDirectoryPath.Trim().TrimEnd('\\', '/'));
            }

            args.Add(string.IsNullOrWhiteSpace(profile)
                ? browser
                : $"{browser}:{profile}");

            return;
        }

        if (settings.Mode == YouTubeAuthMode.CookieFile && !string.IsNullOrWhiteSpace(settings.CookieFilePath))
        {
            args.Add("--cookies");
            args.Add(settings.CookieFilePath.Trim());
        }
    }
}
