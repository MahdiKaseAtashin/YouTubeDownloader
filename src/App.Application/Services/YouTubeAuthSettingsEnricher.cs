using App.Application.Dtos;
using App.Application.Ports;

namespace App.Application.Services;

public static class YouTubeAuthSettingsEnricher
{
    public static YouTubeAuthSettings Enrich(
        YouTubeAuthSettings? settings,
        IBrowserProfileDiscovery discovery)
    {
        if (settings is null || settings.Mode != YouTubeAuthMode.BrowserCookies)
        {
            return settings ?? new YouTubeAuthSettings(YouTubeAuthMode.None, null, null, null);
        }

        if (!string.IsNullOrWhiteSpace(settings.BrowserProfileDirectoryPath))
        {
            return settings;
        }

        if (string.IsNullOrWhiteSpace(settings.Browser) || string.IsNullOrWhiteSpace(settings.BrowserProfile))
        {
            return settings;
        }

        var profile = discovery
            .GetProfiles(settings.Browser)
            .FirstOrDefault(p => p.ProfileId.Equals(settings.BrowserProfile, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(profile?.ProfileDirectoryPath))
        {
            return settings;
        }

        return settings with { BrowserProfileDirectoryPath = profile.ProfileDirectoryPath };
    }
}
