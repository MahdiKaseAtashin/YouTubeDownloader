using System.Windows;
using App.Infrastructure;
using App.UI.ViewModels;
using App.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace App.UI;

public partial class AppHost : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureServices(
                (_, services) =>
                {
                    services.AddInfrastructure();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
            .UseSerilog(
                (_, _, configuration) =>
                {
                    var paths = new App.Infrastructure.Paths.WindowsAppPaths();
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

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var vm = _host.Services.GetRequiredService<MainViewModel>();
        mainWindow.DataContext = vm;
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(true);
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
