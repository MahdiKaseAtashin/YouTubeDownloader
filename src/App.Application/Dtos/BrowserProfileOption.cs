namespace App.Application.Dtos;

public sealed record BrowserProfileOption(
    string BrowserId,
    string ProfileId,
    string DisplayName,
    string? ProfileDirectoryPath = null);
