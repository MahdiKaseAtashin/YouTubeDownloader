using System.Linq;

namespace App.Infrastructure.YouTube;

internal static class YtDlpAuthErrorClassifier
{
    private static readonly string[] AuthTokens =
    {
        "sign in to confirm you're not a bot",
        "this video requires sign in",
        "use --cookies-from-browser",
        "http error 401",
        "login required",
        "private video",
        "members-only",
        "join this channel"
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
