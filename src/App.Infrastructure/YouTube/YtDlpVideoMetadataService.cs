using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using App.Application.Dtos;
using App.Application.Ports;
using App.Application.Services;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.YouTube;

public sealed class YtDlpVideoMetadataService : IVideoMetadataService
{
    private readonly ILogger<YtDlpVideoMetadataService> _logger;

    public YtDlpVideoMetadataService(ILogger<YtDlpVideoMetadataService> logger)
    {
        _logger = logger;
    }

    public async Task<VideoMetadataDto> FetchMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!YoutubeUrlValidator.IsValid(url))
        {
            throw new InvalidOperationException("That does not look like a supported YouTube URL.");
        }

        var ytDlp = YtDlpLocator.ResolveExecutable();
        if (ytDlp is null)
        {
            throw new InvalidOperationException("yt-dlp was not found. Bundle yt-dlp.exe with the app or install it on PATH.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = ytDlp,
            Arguments = $"--dump-single-json --no-warnings {Quote(url)}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                stderr.AppendLine(e.Data);
            }
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Could not start yt-dlp process.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to start yt-dlp process: " + ex.Message, ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignored
            }
        });

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var error = stderr.ToString().Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? $"yt-dlp metadata fetch failed with exit code {process.ExitCode}."
                : error);
        }

        var json = stdout.ToString().Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("yt-dlp returned empty metadata.");
        }

        return ParseMetadata(json);
    }

    private VideoMetadataDto ParseMetadata(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var videoId = GetString(root, "id") ?? throw new InvalidOperationException("Video ID was not found in metadata.");
        var title = GetString(root, "title") ?? $"Video {videoId}";
        var channel = GetString(root, "channel") ?? GetString(root, "uploader") ?? "Unknown channel";
        var durationDisplay = FormatDuration(root);
        var thumbnail = ExtractThumbnail(root, videoId);
        var formats = ExtractFormats(root);
        var subtitleLanguages = ExtractSubtitleLanguages(root);

        _logger.LogInformation("Fetched metadata for {VideoId} ({FormatCount} formats)", videoId, formats.Count);

        return new VideoMetadataDto(
            videoId,
            title,
            channel,
            durationDisplay,
            thumbnail,
            formats,
            subtitleLanguages);
    }

    private static string FormatDuration(JsonElement root)
    {
        if (!root.TryGetProperty("duration", out var durationProp))
        {
            return "Unknown";
        }

        if (!TryGetDouble(durationProp, out var secondsRaw))
        {
            return "Unknown";
        }

        var seconds = Math.Max(0, (int)Math.Round(secondsRaw, MidpointRounding.AwayFromZero));
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : ts.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private static string ExtractThumbnail(JsonElement root, string videoId)
    {
        var topLevel = GetString(root, "thumbnail");
        if (!string.IsNullOrWhiteSpace(topLevel))
        {
            return topLevel;
        }

        if (root.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array)
        {
            string? last = null;
            foreach (var thumb in thumbs.EnumerateArray())
            {
                var url = GetString(thumb, "url");
                if (!string.IsNullOrWhiteSpace(url))
                {
                    last = url;
                }
            }

            if (!string.IsNullOrWhiteSpace(last))
            {
                return last;
            }
        }

        return $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg";
    }

    private static IReadOnlyList<VideoFormatOption> ExtractFormats(JsonElement root)
    {
        if (!root.TryGetProperty("formats", out var formatsEl) || formatsEl.ValueKind != JsonValueKind.Array)
        {
            return new List<VideoFormatOption> { new("best", "Best available") };
        }

        var candidates = new List<(string Id, string Label, int Score)>();
        foreach (var f in formatsEl.EnumerateArray())
        {
            var formatId = GetString(f, "format_id");
            if (string.IsNullOrWhiteSpace(formatId))
            {
                continue;
            }

            var vcodec = GetString(f, "vcodec");
            var hasVideo = !string.Equals(vcodec, "none", StringComparison.OrdinalIgnoreCase);
            if (!hasVideo)
            {
                continue;
            }

            var ext = GetString(f, "ext") ?? "unknown";
            var height = TryGetInt(f, "height");
            var fps = TryGetInt(f, "fps");
            var note = GetString(f, "format_note");
            var tbr = TryGetDouble(f, "tbr");

            var parts = new List<string> { ext.ToUpperInvariant() };
            if (height.HasValue)
            {
                parts.Add($"{height.Value}p");
            }

            if (fps.HasValue && fps.Value > 0)
            {
                parts.Add($"{fps.Value}fps");
            }

            if (!string.IsNullOrWhiteSpace(note))
            {
                parts.Add(note);
            }

            if (tbr.HasValue && tbr.Value > 0)
            {
                parts.Add($"{Math.Round(tbr.Value)}kbps");
            }

            var label = string.Join(" · ", parts);
            var score = (height ?? 0) * 10 + (fps ?? 0);
            candidates.Add((formatId, label, score));
        }

        if (candidates.Count == 0)
        {
            return new List<VideoFormatOption> { new("best", "Best available") };
        }

        var distinct = candidates
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .Take(20)
            .Select(x => new VideoFormatOption(x.Id, x.Label))
            .ToList();

        return distinct;
    }

    private static IReadOnlyList<string> ExtractSubtitleLanguages(JsonElement root)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddLanguages(root, "subtitles", set);
        AddLanguages(root, "automatic_captions", set);

        return set.Count == 0 ? Array.Empty<string>() : set.OrderBy(x => x).ToList();
    }

    private static void AddLanguages(JsonElement root, string propertyName, HashSet<string> set)
    {
        if (!root.TryGetProperty(propertyName, out var subs) || subs.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var prop in subs.EnumerateObject())
        {
            if (!string.IsNullOrWhiteSpace(prop.Name))
            {
                set.Add(prop.Name);
            }
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        if (prop.TryGetInt32(out var value))
        {
            return value;
        }

        if (TryGetDouble(prop, out var asDouble))
        {
            return (int)Math.Round(asDouble, MidpointRounding.AwayFromZero);
        }

        return null;
    }

    private static bool TryGetDouble(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return TryGetDouble(prop, out var value) ? value : null;
    }

    private static string Quote(string value) =>
        value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
}
