using App.Application.Dtos;

namespace App.Infrastructure.YouTube;

internal static class YtDlpFfmpegArgumentsBuilder
{
    public static void AppendFfmpegLocationIfAvailable(IList<string> args)
    {
        var location = FfmpegLocator.ResolveLocation();
        if (string.IsNullOrWhiteSpace(location))
        {
            return;
        }

        location = location.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        args.Add("--ffmpeg-location");
        // Keep raw value; Join() applies quoting once for process arguments.
        args.Add(location);
    }

    public static bool RequiresFfmpeg(DownloadRequest request) =>
        request.DownloadVideo || request.DownloadThumbnail || request.DownloadSubtitles;

    public static void EnsureAvailableOrThrow(DownloadRequest request)
    {
        if (!RequiresFfmpeg(request))
        {
            return;
        }

        if (FfmpegLocator.ResolveLocation() is not null)
        {
            return;
        }

        throw new InvalidOperationException(
            "ffmpeg was not found. Place ffmpeg.exe next to the app (or in a tools folder), install ffmpeg on PATH, or rebuild with scripts/install-ffmpeg.ps1.");
    }
}
