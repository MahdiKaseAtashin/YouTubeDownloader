using System.Runtime.InteropServices;
using App.Application.Ports;
using App.Infrastructure;
using Yt.Client.Services;
using Yt.Client.ViewModels;
using Yt.Client.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using Windows.Graphics;
using WinUiApplication = Microsoft.UI.Xaml.Application;

namespace Yt.Client;

public partial class App : WinUiApplication
{
    private IHost? _host;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await LaunchApplicationAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            try
            {
                Log.Fatal(ex, "Startup failed before or during main window setup.");
            }
            catch
            {
                // Serilog may not be configured yet
            }

            ShowStartupFailureDialog(ex);
        }
    }

    private async Task LaunchApplicationAsync()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) => services.AddInfrastructure())
            .UseSerilog(
                (_, _, configuration) =>
                {
                    var paths = new global::App.Infrastructure.Paths.WindowsAppPaths();
                    configuration
                        .MinimumLevel.Information()
                        .Enrich.FromLogContext()
                        .WriteTo.File(
                            paths.ApplicationLogFilePath,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 14);
                })
            .Build();

        await _host.StartAsync().ConfigureAwait(true);

        var mainWindow = new MainWindow();
        mainWindow.Closed += async (_, _) => await ShutdownHostAsync();
        mainWindow.AppWindow.Resize(new SizeInt32(1080, 820));

        var vm = new MainViewModel(
            _host.Services.GetRequiredService<IVideoMetadataService>(),
            _host.Services.GetRequiredService<IVideoDownloadService>(),
            _host.Services.GetRequiredService<IUserPreferencesStore>(),
            mainWindow.DispatcherQueue,
            mainWindow);
        if (mainWindow.Content is FrameworkElement root)
        {
            root.DataContext = vm;
        }

        ThemeSwitcher.Apply(vm.IsDarkTheme, mainWindow.Content as FrameworkElement);
        // Show the window before awaiting preferences so a slow or failing init does not leave a hidden shell.
        mainWindow.Activate();

        try
        {
            await vm.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup initialization failed after window was shown.");
        }
    }

    private static void ShowStartupFailureDialog(Exception ex)
    {
        var logHint = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YouTubeDownloader",
            "logs");
        var text =
            "YouTube Downloader could not start.\r\n\r\n" +
            ex.GetType().Name + ": " + ex.Message + "\r\n\r\n" +
            "If the window is blank (no UI), rebuild a portable release (embeds WinUI resources):\r\n" +
            "  scripts\\publish-release.ps1\r\n\r\n" +
            "Details may be logged under:\r\n" + logHint;

        _ = MessageBoxW(IntPtr.Zero, text, "YouTube Downloader — startup error", 0x00000010); // MB_ICONERROR
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    public async Task ShutdownHostAsync()
    {
        if (_host is null)
        {
            return;
        }

        try
        {
            await _host.StopAsync().ConfigureAwait(true);
            _host.Dispose();
        }
        finally
        {
            _host = null;
            Log.CloseAndFlush();
        }
    }
}
