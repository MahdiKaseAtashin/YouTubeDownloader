using System.Diagnostics;
using System.Text;
using App.Application.Dtos;
using App.Application.Ports;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.YouTube;

public sealed class YtDlpYouTubeSessionValidator : IYouTubeSessionValidator
{
    private const string CookieLoadProbeUrl = "https://www.youtube.com/watch?v=jNQXAC9IVRw";
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(45);

    private readonly ILogger<YtDlpYouTubeSessionValidator> _logger;

    public YtDlpYouTubeSessionValidator(ILogger<YtDlpYouTubeSessionValidator> logger)
    {
        _logger = logger;
    }

    public async Task<YouTubeSessionValidationResult> ValidateAsync(
        YouTubeAuthSettings settings,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(ValidationTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var effectiveToken = linkedCts.Token;

        if (settings.Mode == YouTubeAuthMode.None)
        {
            return new YouTubeSessionValidationResult(
                true,
                "Sign-in is turned off. Public videos only.",
                IsAuthenticationRelated: false);
        }

        if (settings.Mode == YouTubeAuthMode.CookieFile)
        {
            if (string.IsNullOrWhiteSpace(settings.CookieFilePath) || !File.Exists(settings.CookieFilePath))
            {
                return new YouTubeSessionValidationResult(
                    false,
                    "Cookie file not found. Choose a valid cookies.txt file.",
                    IsAuthenticationRelated: true);
            }

            return FileContainsYouTubeSession(settings.CookieFilePath)
                ? new YouTubeSessionValidationResult(
                    true,
                    "Cookie file contains a YouTube sign-in session.",
                    IsAuthenticationRelated: false)
                : new YouTubeSessionValidationResult(
                    false,
                    "Cookie file was found but does not contain a YouTube login session.",
                    IsAuthenticationRelated: true);
        }

        var ytDlp = YtDlpLocator.ResolveExecutable();
        if (ytDlp is null)
        {
            return new YouTubeSessionValidationResult(
                false,
                "yt-dlp was not found. Bundle yt-dlp.exe with the app or install it on PATH.",
                IsAuthenticationRelated: false);
        }

        var tempCookies = Path.Combine(Path.GetTempPath(), $"ytdlp-auth-{Guid.NewGuid():N}.txt");
        try
        {
            var args = new List<string>
            {
                "--ignore-config",
                "--no-warnings",
                "--skip-download",
                "-f", "b",
                "--cookies", tempCookies
            };
            YtDlpAuthArgumentsBuilder.AppendAuthArguments(args, settings);
            args.Add(CookieLoadProbeUrl);

            var psi = new ProcessStartInfo
            {
                FileName = ytDlp,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = psi };
            var stderr = new StringBuilder();

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stderr.AppendLine(e.Data);
                }
            };

            try
            {
                if (!process.Start())
                {
                    return new YouTubeSessionValidationResult(false, "Could not start yt-dlp.", false);
                }
            }
            catch (Exception ex)
            {
                return new YouTubeSessionValidationResult(false, "Failed to start yt-dlp: " + ex.Message, false);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var registration = effectiveToken.Register(() =>
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
                await process.WaitForExitAsync(effectiveToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    return new YouTubeSessionValidationResult(
                        false,
                        "Connection test timed out. Close Firefox completely and try again, or switch to Edge/Chrome.",
                        IsAuthenticationRelated: false);
                }

                return new YouTubeSessionValidationResult(false, "Connection test was cancelled.", false);
            }

            var error = stderr.ToString().Trim();
            if (error.Contains("could not find firefox cookies database", StringComparison.OrdinalIgnoreCase))
            {
                return new YouTubeSessionValidationResult(
                    false,
                    "Firefox profile was not found. Pick the account that shows the full folder name (for example: fyagig4q.default-release).",
                    IsAuthenticationRelated: true);
            }

            if (error.Contains("Could not copy", StringComparison.OrdinalIgnoreCase) &&
                error.Contains("cookie", StringComparison.OrdinalIgnoreCase))
            {
                return new YouTubeSessionValidationResult(
                    false,
                    "Could not read browser cookies. Close the browser completely, then test again.",
                    IsAuthenticationRelated: true);
            }

            if (!File.Exists(tempCookies))
            {
                var message = string.IsNullOrWhiteSpace(error)
                    ? "Could not read cookies from the browser."
                    : error;
                return new YouTubeSessionValidationResult(false, message, IsAuthenticationRelated: true);
            }

            if (YouTubeCookieJarInspector.HasYouTubeSessionCookies(tempCookies))
            {
                _logger.LogInformation("YouTube session cookies detected in browser profile");
                return new YouTubeSessionValidationResult(
                    true,
                    "Signed in. Your browser session is ready for restricted videos.",
                    IsAuthenticationRelated: false);
            }

            return new YouTubeSessionValidationResult(
                false,
                "Browser cookies were read, but no YouTube login was found. Click Open YouTube in browser, sign in, then test again.",
                IsAuthenticationRelated: true);
        }
        finally
        {
            TryDelete(tempCookies);
        }
    }

    private static bool FileContainsYouTubeSession(string path)
    {
        try
        {
            return YouTubeCookieJarInspector.HasYouTubeSessionCookies(path);
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }

}
