using App.Application.Dtos;

namespace App.Application.Ports;

public interface IVideoDownloadService
{
    Task DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken = default);
}
