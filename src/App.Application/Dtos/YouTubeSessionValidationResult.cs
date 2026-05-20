namespace App.Application.Dtos;

public sealed record YouTubeSessionValidationResult(
    bool Success,
    string Message,
    bool IsAuthenticationRelated);
