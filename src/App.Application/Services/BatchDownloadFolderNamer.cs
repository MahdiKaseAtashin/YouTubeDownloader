namespace App.Application.Services;

public static class BatchDownloadFolderNamer
{
    private const int MaxFolderNameLength = 120;

    public static string Create(int index, string title, string? videoId = null)
    {
        var safeTitle = SanitizeTitle(title);
        if (string.IsNullOrWhiteSpace(safeTitle))
        {
            safeTitle = string.IsNullOrWhiteSpace(videoId) ? "video" : videoId;
        }

        return Truncate($"{index}-{safeTitle}", MaxFolderNameLength);
    }

    public static string CreateUnique(string parentFolder, int index, string title, string videoId)
    {
        var name = Create(index, title, videoId);
        var path = Path.Combine(parentFolder, name);
        if (!Directory.Exists(path))
        {
            return name;
        }

        var suffix = string.IsNullOrWhiteSpace(videoId) ? index.ToString() : videoId;
        return Truncate($"{index}-{SanitizeTitle(title)} [{suffix}]", MaxFolderNameLength);
    }

    private static string SanitizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(title.Trim().Length);
        foreach (var ch in title.Trim().ToLowerInvariant())
        {
            if (invalid.Contains(ch))
            {
                builder.Append('-');
                continue;
            }

            builder.Append(char.IsWhiteSpace(ch) ? '-' : ch);
        }

        var parts = builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('-', parts).TrimEnd('.');
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength].TrimEnd();
    }
}
