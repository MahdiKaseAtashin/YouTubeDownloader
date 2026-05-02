using App.Application.Dtos;
using App.Application.Ports;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.YouTube;

/// <summary>
/// Simulated download pipeline — replace with yt-dlp process execution.
/// </summary>
public sealed class SimulatedVideoDownloadService : IVideoDownloadService
{
    private readonly ILogger<SimulatedVideoDownloadService> _logger;

    public SimulatedVideoDownloadService(ILogger<SimulatedVideoDownloadService> logger)
    {
        _logger = logger;
    }

    public async Task DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(request.OutputDirectory))
        {
            throw new DirectoryNotFoundException("Output folder does not exist or is not reachable.");
        }

        _logger.LogInformation("Simulated download to {Folder}", request.OutputDirectory);

        Report(progress, 0.02, "Preparing…");
        await Task.Delay(400, cancellationToken).ConfigureAwait(false);

        if (request.DownloadVideo)
        {
            Report(progress, 0.15, "Downloading video…");
            await Task.Delay(600, cancellationToken).ConfigureAwait(false);
            Report(progress, 0.35, "Downloading audio…");
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            Report(progress, 0.55, "Merging audio + video…");
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);

            var ext = request.VideoContainer.Equals("WEBM", StringComparison.OrdinalIgnoreCase) ? "webm" : "mp4";
            var videoPath = Path.Combine(request.OutputDirectory, $"video_{request.SelectedFormatId}.{ext}");
            await File.WriteAllTextAsync(
                videoPath,
                $"Simulated video export.\nSource: {request.SourceUrl}\nFormat: {request.SelectedFormatId}\nQuality: {request.QualityLabel}\n",
                cancellationToken).ConfigureAwait(false);
        }

        if (request.DownloadThumbnail)
        {
            Report(progress, 0.72, "Saving thumbnail…");
            await Task.Delay(350, cancellationToken).ConfigureAwait(false);
            var thumbPath = Path.Combine(request.OutputDirectory, "thumbnail.jpg");
            await File.WriteAllTextAsync(
                thumbPath,
                "Simulated JPEG placeholder (replace with real image bytes).",
                cancellationToken).ConfigureAwait(false);
        }

        if (request.DownloadSubtitles)
        {
            Report(progress, 0.85, "Saving subtitles…");
            await Task.Delay(350, cancellationToken).ConfigureAwait(false);
            var lang = string.IsNullOrWhiteSpace(request.SubtitleLanguage) ? "en" : request.SubtitleLanguage;
            var srtPath = Path.Combine(request.OutputDirectory, $"subtitles_{lang}.srt");
            await File.WriteAllTextAsync(
                srtPath,
                "1\n00:00:00,000 --> 00:00:02,000\nSimulated subtitle line.\n",
                cancellationToken).ConfigureAwait(false);
        }

        Report(progress, 1.0, "Finished.");
    }

    private static void Report(IProgress<DownloadProgressUpdate> progress, double fraction, string message) =>
        progress.Report(new DownloadProgressUpdate(fraction, message));
}
