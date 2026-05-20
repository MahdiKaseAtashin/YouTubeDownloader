using App.Application.Dtos;
using App.Application.Ports;
using App.Application.Services;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.YouTube;

/// <summary>
/// Simulated metadata — replace with yt-dlp / YouTube Data API implementation.
/// </summary>
public sealed class SimulatedVideoMetadataService : IVideoMetadataService
{
    private readonly ILogger<SimulatedVideoMetadataService> _logger;

    public SimulatedVideoMetadataService(ILogger<SimulatedVideoMetadataService> logger)
    {
        _logger = logger;
    }

    public async Task<VideoMetadataDto> FetchMetadataAsync(
        string url,
        YouTubeAuthSettings? authSettings = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(Random.Shared.Next(450, 900), cancellationToken).ConfigureAwait(false);

        if (!YoutubeUrlValidator.IsValid(url))
        {
            throw new InvalidOperationException("That does not look like a supported YouTube URL.");
        }

        var id = YoutubeUrlValidator.TryExtractVideoId(url)
            ?? throw new InvalidOperationException("Could not read the video ID from the URL.");

        _logger.LogInformation("Simulated metadata fetch for {VideoId}", id);

        var formats = new List<VideoFormatOption>
        {
            new("mp4-1080", "MP4 · 1080p (simulated)"),
            new("mp4-720", "MP4 · 720p (simulated)"),
            new("webm-1080", "WEBM · 1080p (simulated)"),
            new("webm-720", "WEBM · 720p (simulated)")
        };

        var subs = new List<string> { "en", "fa", "de" };

        var thumb = $"https://i.ytimg.com/vi/{id}/hqdefault.jpg";

        var shortId = id.Length >= 6 ? id[..6] : id;
        return new VideoMetadataDto(
            id,
            Title: $"Preview · Video {shortId}… (simulated)",
            ChannelName: "Demo Channel (simulated)",
            DurationDisplay: "12:34",
            ThumbnailUrl: thumb,
            Formats: formats,
            SubtitleLanguages: subs);
    }
}
