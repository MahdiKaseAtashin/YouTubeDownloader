using App.Application.Dtos;

namespace App.Infrastructure.YouTube;

internal static class YtDlpJsArgumentsBuilder
{
    public static void AppendYouTubeExtractionSupport(IList<string> args)
    {
        var runtime = JsRuntimeLocator.Resolve();
        if (runtime is null)
        {
            return;
        }

        args.Add("--js-runtimes");
        args.Add($"{runtime.Kind}:{runtime.ExecutablePath}");
        args.Add("--remote-components");
        args.Add("ejs:github");
    }

    public static void EnsureAvailableForBrowserCookies(YouTubeAuthSettings? settings)
    {
        if (settings is null || settings.Mode != YouTubeAuthMode.BrowserCookies)
        {
            return;
        }

        if (JsRuntimeLocator.Resolve() is not null)
        {
            return;
        }

        throw new InvalidOperationException(
            "Signed-in YouTube downloads require a JavaScript runtime (Node.js or Deno). " +
            "Install Node.js, add it to PATH, or place node.exe in the app tools folder.");
    }
}
