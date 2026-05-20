using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using App.Application.Dtos;
using App.Application.Ports;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.YouTube;

public sealed class YtDlpVideoDownloadService : IVideoDownloadService
{
    private static readonly Regex DownloadPercentRegex = new(@"\[download\]\s+(?<percent>\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled);
    private readonly ILogger<YtDlpVideoDownloadService> _logger;

    public YtDlpVideoDownloadService(ILogger<YtDlpVideoDownloadService> logger)
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

        var ytDlp = YtDlpLocator.ResolveExecutable();
        if (ytDlp is null)
        {
            throw new InvalidOperationException("yt-dlp was not found. Bundle yt-dlp.exe with the app or install it on PATH.");
        }

        var arguments = BuildArguments(request);
        _logger.LogInformation("Starting yt-dlp download to {Folder}", request.OutputDirectory);

        var psi = new ProcessStartInfo
        {
            FileName = ytDlp,
            Arguments = arguments,
            WorkingDirectory = request.OutputDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stderrLines = new List<string>();

        process.OutputDataReceived += (_, e) => HandleLine(e.Data, progress);
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (stderrLines)
                {
                    stderrLines.Add(e.Data);
                }
            }

            HandleLine(e.Data, progress);
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

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        if (process.ExitCode != 0)
        {
            string errorText;
            lock (stderrLines)
            {
                errorText = string.Join(Environment.NewLine, stderrLines);
            }

            if (YtDlpAuthErrorClassifier.IsAuthenticationFailure(errorText))
            {
                throw new InvalidOperationException(
                    "Authentication failed or expired. Reconnect your YouTube session and retry.");
            }

            throw new InvalidOperationException($"yt-dlp failed with exit code {process.ExitCode}. See activity log for details.");
        }

        progress.Report(new DownloadProgressUpdate(1.0, "Finished."));
    }

    private static string BuildArguments(DownloadRequest request)
    {
        var args = new List<string>
        {
            "--newline",
            "--no-warnings",
            "--ignore-errors",
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

        YtDlpAuthArgumentsBuilder.AppendAuthArguments(args, request.AuthSettings);

        var formatSelector = BuildFormatSelector(request);
        if (!string.IsNullOrWhiteSpace(formatSelector))
        {
            args.Add("-f");
            args.Add(formatSelector);
        }

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

        args.Add(request.SourceUrl);
        return string.Join(" ", args.Select(Quote));
    }

    private static string BuildFormatSelector(DownloadRequest request)
    {
        if (!request.DownloadVideo)
        {
            return string.Empty;
        }

        var container = request.VideoContainer.Equals("WEBM", StringComparison.OrdinalIgnoreCase) ? "webm" : "mp4";
        var audioExt = container == "webm" ? "webm" : "m4a";
        var maxHeight = ParseMaxHeight(request.QualityLabel);
        var heightFilter = maxHeight.HasValue ? $"[height<={maxHeight.Value}]" : string.Empty;

        return $"bestvideo[ext={container}]{heightFilter}+bestaudio[ext={audioExt}]/best[ext={container}]{heightFilter}/best{heightFilter}";
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

    private static void HandleLine(string? line, IProgress<DownloadProgressUpdate> progress)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var match = DownloadPercentRegex.Match(line);
        if (match.Success &&
            double.TryParse(match.Groups["percent"].Value, System.Globalization.CultureInfo.InvariantCulture, out var percent))
        {
            progress.Report(new DownloadProgressUpdate(Math.Clamp(percent / 100.0, 0.0, 1.0), line.Trim()));
            return;
        }

        progress.Report(new DownloadProgressUpdate(0.0, line.Trim()));
    }

    private static string Quote(string value) =>
        value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
}
