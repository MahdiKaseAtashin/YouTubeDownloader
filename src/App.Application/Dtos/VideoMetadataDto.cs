namespace App.Application.Dtos;

public sealed record VideoMetadataDto(
    string VideoId,
    string Title,
    string ChannelName,
    string DurationDisplay,
    string ThumbnailUrl,
    IReadOnlyList<VideoFormatOption> Formats,
    IReadOnlyList<string> SubtitleLanguages);
