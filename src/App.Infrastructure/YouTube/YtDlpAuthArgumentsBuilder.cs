using App.Application.Dtos;

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
            args.Add(string.IsNullOrWhiteSpace(settings.BrowserProfile)
                ? settings.Browser.Trim()
                : $"{settings.Browser.Trim()}:{settings.BrowserProfile.Trim()}");
            return;
        }

        if (settings.Mode == YouTubeAuthMode.CookieFile && !string.IsNullOrWhiteSpace(settings.CookieFilePath))
        {
            args.Add("--cookies");
            args.Add(settings.CookieFilePath.Trim());
        }
    }
}
