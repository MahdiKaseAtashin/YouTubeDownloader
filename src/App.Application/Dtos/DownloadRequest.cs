namespace App.Application.Dtos;

public sealed record DownloadRequest(
    string SourceUrl,
    string OutputDirectory,
    string SelectedFormatId,
    string VideoContainer,
    string QualityLabel,
    bool DownloadVideo,
    bool DownloadThumbnail,
    bool DownloadSubtitles,
    string? SubtitleLanguage,
    YouTubeAuthSettings? AuthSettings);
