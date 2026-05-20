using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using App.Application.Dtos;
using App.Application.Ports;
using App.Application.Services;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.YouTube;

public sealed class YtDlpVideoDownloadService : IVideoDownloadService
{
    private static readonly Regex DownloadPercentRegex = new(@"\[download\]\s+(?<percent>\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled);
    private readonly IBrowserProfileDiscovery _browserDiscovery;
    private readonly ILogger<YtDlpVideoDownloadService> _logger;

    public YtDlpVideoDownloadService(
        IBrowserProfileDiscovery browserDiscovery,
        ILogger<YtDlpVideoDownloadService> logger)
    {
        _browserDiscovery = browserDiscovery;
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

        var ytDlp = YtDlpLocator.ResolveExecutable();
        if (ytDlp is null)
        {
            throw new InvalidOperationException("yt-dlp was not found. Bundle yt-dlp.exe with the app or install it on PATH.");
        }

        YtDlpFfmpegArgumentsBuilder.EnsureAvailableOrThrow(request);

        var enrichedAuth = YouTubeAuthSettingsEnricher.Enrich(request.AuthSettings, _browserDiscovery);
        YtDlpJsArgumentsBuilder.EnsureAvailableForBrowserCookies(enrichedAuth);

        var arguments = BuildArguments(request, enrichedAuth);
        _logger.LogInformation("Starting yt-dlp download to {Folder}", request.OutputDirectory);

        var result = await RunYtDlpAsync(ytDlp, request.OutputDirectory, arguments, progress, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            var errorText = result.ErrorText;
            if (IsYouTubeChallengeFailure(errorText) && JsRuntimeLocator.Resolve() is null)
            {
                throw new InvalidOperationException(
                    "YouTube requires a JavaScript runtime for this video. Install Node.js or Deno and retry.");
            }

            if (YtDlpAuthErrorClassifier.IsAuthenticationFailure(errorText))
            {
                throw new InvalidOperationException(
                    "Authentication failed or expired. Reconnect your YouTube session and retry.");
            }

            if (!string.IsNullOrWhiteSpace(errorText))
            {
                throw new InvalidOperationException(errorText);
            }

            throw new InvalidOperationException($"yt-dlp failed with exit code {result.ExitCode}.");
        }

        progress.Report(new DownloadProgressUpdate(1.0, "Finished."));
    }

    private string[] BuildArguments(DownloadRequest request, YouTubeAuthSettings? authSettings)
    {
        var args = new List<string>
        {
            "--newline",
            "--no-warnings",
            "--ignore-errors",
            "--ignore-no-formats-error",
            "--restrict-filenames",
            "--extractor-retries",
            "3",
            "--fragment-retries",
            "10",
            "--retry-sleep",
            "1:3",
            "-P", request.OutputDirectory,
            "-o", "%(title).180B [%(id)s].%(ext)s"
        };

        YtDlpAuthArgumentsBuilder.AppendAuthArguments(args, authSettings);
        YtDlpJsArgumentsBuilder.AppendYouTubeExtractionSupport(args);
        YtDlpFfmpegArgumentsBuilder.AppendFfmpegLocationIfAvailable(args);

        var formatSelector = BuildFormatSelector(request);
        if (!string.IsNullOrWhiteSpace(formatSelector))
        {
            args.Add("-f");
            args.Add(formatSelector);
        }
        args.Add("--merge-output-format");
        args.Add(request.VideoContainer.Equals("WEBM", StringComparison.OrdinalIgnoreCase) ? "webm" : "mp4");

        if (request.DownloadThumbnail)
        {
            args.Add("--write-thumbnail");
            args.Add("--convert-thumbnails");
            args.Add("jpg");
        }

        if (request.DownloadSubtitles)
        {
            args.Add("--write-subs");
            args.Add("--convert-subs");
            args.Add("srt");
            args.Add("--sub-langs");
            args.Add(string.IsNullOrWhiteSpace(request.SubtitleLanguage) ? "en" : request.SubtitleLanguage!.Trim());
        }

        if (!request.DownloadVideo)
        {
            args.Add("--skip-download");
        }

        args.Add(NormalizeUrl(request.SourceUrl));
        return args.ToArray();
    }

    private static bool IsYouTubeChallengeFailure(string errorText) =>
        errorText.Contains("No video formats found", StringComparison.OrdinalIgnoreCase) ||
        errorText.Contains("Only images are available", StringComparison.OrdinalIgnoreCase) ||
        errorText.Contains("n challenge solving failed", StringComparison.OrdinalIgnoreCase) ||
        errorText.Contains("JavaScript runtime", StringComparison.OrdinalIgnoreCase);

    private static async Task<(int ExitCode, string ErrorText)> RunYtDlpAsync(
        string ytDlp,
        string workingDirectory,
        string[] arguments,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ytDlp,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stderrLines = new List<string>();

        process.OutputDataReceived += (_, e) => HandleLine(e.Data, progress, isStdErr: false);
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (stderrLines)
                {
                    stderrLines.Add(e.Data);
                }
            }

            HandleLine(e.Data, progress, isStdErr: true);
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

        string errorText;
        lock (stderrLines)
        {
            errorText = string.Join(Environment.NewLine, stderrLines);
        }

        return (process.ExitCode, errorText);
    }

    private static string BuildFormatSelector(DownloadRequest request)
    {
        if (!request.DownloadVideo)
        {
            return string.Empty;
        }

        var maxHeight = ParseMaxHeight(request.QualityLabel);
        var heightFilter = maxHeight.HasValue ? $"[height<={maxHeight.Value}]" : string.Empty;

        // Keep selector permissive for Shorts/edge cases, then remux to user container.
        return $"bestvideo{heightFilter}+bestaudio/best{heightFilter}/best";
    }

    private static int? ParseMaxHeight(string qualityLabel)
    {
        if (string.IsNullOrWhiteSpace(qualityLabel) ||
            qualityLabel.Contains("best", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var digits = new string(qualityLabel.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var parsed) ? parsed : null;
    }

    private static void HandleLine(string? line, IProgress<DownloadProgressUpdate> progress, bool isStdErr)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var trimmed = line.Trim();
        var match = DownloadPercentRegex.Match(trimmed);
        if (match.Success &&
            double.TryParse(match.Groups["percent"].Value, System.Globalization.CultureInfo.InvariantCulture, out var percent))
        {
            progress.Report(new DownloadProgressUpdate(Math.Clamp(percent / 100.0, 0.0, 1.0), trimmed, isStdErr));
            return;
        }

        progress.Report(new DownloadProgressUpdate(0.0, trimmed, isStdErr));
    }

    private static string NormalizeUrl(string url)
    {
        var videoId = YoutubeUrlValidator.TryExtractVideoId(url);
        return videoId is null ? url.Trim() : $"https://www.youtube.com/watch?v={videoId}";
    }

}
