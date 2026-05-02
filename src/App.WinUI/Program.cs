using Microsoft.UI.Dispatching;
using WinRT;

namespace Yt.Client;

/// <summary>
/// Ensures the self-contained Windows App SDK native payload is discoverable before WinRT / WinUI starts.
/// Unpackaged WinUI combined with dotnet PublishSingleFile is unreliable (COM 0x80040111 at Application.Start); release publish uses a self-contained folder instead.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var baseDir = Path.GetDirectoryName(Environment.ProcessPath);
        Environment.SetEnvironmentVariable(
            "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
            !string.IsNullOrEmpty(baseDir) ? baseDir : AppContext.BaseDirectory);

        ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(static (Microsoft.UI.Xaml.ApplicationInitializationCallbackParams p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
