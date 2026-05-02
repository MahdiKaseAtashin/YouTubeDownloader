using System.Text.RegularExpressions;

namespace App.Application.Services;

public static class YoutubeUrlValidator
{
    private static readonly Regex[] Patterns =
    {
        new(@"^(https?://)?(www\.)?youtube\.com/watch\?v=[\w-]{11}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(https?://)?(www\.)?youtube\.com/shorts/[\w-]{11}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(https?://)?(www\.)?m\.youtube\.com/shorts/[\w-]{11}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(https?://)?(www\.)?youtu\.be/[\w-]{11}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(https?://)?(www\.)?youtube\.com/embed/[\w-]{11}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(https?://)?(www\.)?m\.youtube\.com/watch\?v=[\w-]{11}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^(https?://)?(www\.)?youtube-nocookie\.com/embed/[\w-]{11}", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    public static bool IsValid(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var trimmed = url.Trim();
        return Patterns.Any(p => p.IsMatch(trimmed));
    }

    public static string? TryExtractVideoId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var u = url.Trim();

        var watch = Regex.Match(u, @"[?&]v=([\w-]{11})", RegexOptions.IgnoreCase);
        if (watch.Success)
        {
            return watch.Groups[1].Value;
        }

        var shortLink = Regex.Match(u, @"youtu\.be/([\w-]{11})", RegexOptions.IgnoreCase);
        if (shortLink.Success)
        {
            return shortLink.Groups[1].Value;
        }

        var shorts = Regex.Match(
            u,
            @"(?:youtube\.com|m\.youtube\.com)/shorts/([\w-]{11})",
            RegexOptions.IgnoreCase);
        if (shorts.Success)
        {
            return shorts.Groups[1].Value;
        }

        var embed = Regex.Match(u, @"/embed/([\w-]{11})", RegexOptions.IgnoreCase);
        if (embed.Success)
        {
            return embed.Groups[1].Value;
        }

        return null;
    }
}
