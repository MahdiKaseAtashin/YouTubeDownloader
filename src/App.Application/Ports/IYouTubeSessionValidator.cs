using App.Application.Dtos;

namespace App.Application.Ports;

public interface IYouTubeSessionValidator
{
    Task<YouTubeSessionValidationResult> ValidateAsync(
        YouTubeAuthSettings settings,
        CancellationToken cancellationToken = default);
}
