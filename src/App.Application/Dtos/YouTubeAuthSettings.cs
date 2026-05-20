namespace App.Application.Dtos;

public sealed record YouTubeAuthSettings(
    YouTubeAuthMode Mode,
    string? Browser,
    string? BrowserProfile,
    string? CookieFilePath);
