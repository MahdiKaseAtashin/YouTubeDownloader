using App.Application.Dtos;

namespace App.Application.Ports;

public interface IVideoMetadataService
{
    Task<VideoMetadataDto> FetchMetadataAsync(string url, CancellationToken cancellationToken = default);
}
