namespace App.Application.Services;

public static class YoutubeUrlParser
{
    public static IReadOnlyList<string> ParseLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            foreach (var part in trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = part.Trim().TrimEnd(',', ';');
                if (!YoutubeUrlValidator.IsValid(candidate))
                {
                    continue;
                }

                if (seen.Add(candidate))
                {
                    results.Add(candidate);
                }
            }
        }

        return results;
    }
}
