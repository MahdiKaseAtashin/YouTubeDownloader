using System.Linq;

namespace App.Infrastructure.YouTube;

internal static class YtDlpAuthErrorClassifier
{
    private static readonly string[] AuthTokens =
    {
        "sign in to confirm you're not a bot",
        "this video requires sign in",
        "http error 403",
        "requested format is not available. use --list-formats",
        "authentication",
        "cookies"
    };

    public static bool IsAuthenticationFailure(string? errorText)
    {
        if (string.IsNullOrWhiteSpace(errorText))
        {
            return false;
        }

        return AuthTokens.Any(token => errorText.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
