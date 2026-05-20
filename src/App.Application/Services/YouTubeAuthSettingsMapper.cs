using App.Application.Dtos;

namespace App.Application.Services;

public static class YouTubeAuthSettingsMapper
{
    public static YouTubeAuthSettings Build(
        bool useSignIn,
        bool useCookieFile,
        string? cookieFilePath,
        string? browserId,
        string? profileId,
        string? manualProfileOverride,
        string? profileDirectoryPath = null)
    {
        if (!useSignIn)
        {
            return new YouTubeAuthSettings(YouTubeAuthMode.None, null, null, null);
        }

        if (useCookieFile && !string.IsNullOrWhiteSpace(cookieFilePath))
        {
            return new YouTubeAuthSettings(
                YouTubeAuthMode.CookieFile,
                null,
                null,
                cookieFilePath.Trim());
        }

        var browser = string.IsNullOrWhiteSpace(browserId) ? "edge" : browserId.Trim();
        var profile = !string.IsNullOrWhiteSpace(manualProfileOverride)
            ? manualProfileOverride.Trim()
            : string.IsNullOrWhiteSpace(profileId) ? "Default" : profileId.Trim();
        var profilePath = string.IsNullOrWhiteSpace(manualProfileOverride)
            ? profileDirectoryPath?.Trim()
            : null;

        return new YouTubeAuthSettings(
            YouTubeAuthMode.BrowserCookies,
            browser,
            profile,
            null,
            profilePath);
    }

    public static void ApplyToUi(
        YouTubeAuthSettings settings,
        out bool useSignIn,
        out bool useCookieFile,
        out string cookieFilePath,
        out string browserId,
        out string profileId,
        out string manualProfileOverride)
    {
        useSignIn = settings.Mode != YouTubeAuthMode.None;
        useCookieFile = settings.Mode == YouTubeAuthMode.CookieFile;
        cookieFilePath = settings.CookieFilePath ?? string.Empty;
        browserId = string.IsNullOrWhiteSpace(settings.Browser) ? "edge" : settings.Browser!;
        profileId = string.IsNullOrWhiteSpace(settings.BrowserProfile) ? "Default" : settings.BrowserProfile!;
        manualProfileOverride = string.Empty;
    }

    public static string GetBrowserDisplayName(string browserId) =>
        browserId.ToLowerInvariant() switch
        {
            "edge" => "Microsoft Edge",
            "chrome" => "Google Chrome",
            "firefox" => "Mozilla Firefox",
            _ => browserId
        };
}
