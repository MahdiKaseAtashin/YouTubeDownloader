using App.Application.Dtos;

namespace App.Application.Ports;

public interface IVideoMetadataService
{
    Task<VideoMetadataDto> FetchMetadataAsync(
        string url,
        YouTubeAuthSettings? authSettings = null,
        CancellationToken cancellationToken = default);
}
