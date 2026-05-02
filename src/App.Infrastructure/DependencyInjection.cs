using App.Application.Ports;
using App.Infrastructure.FileSystem;
using App.Infrastructure.Persistence;
using App.Infrastructure.Paths;
using App.Infrastructure.YouTube;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, WindowsAppPaths>();
        services.AddSingleton<IFileSystem, DefaultFileSystem>();
        services.AddSingleton<IUserPreferencesStore, JsonUserPreferencesStore>();

        services.AddSingleton<IVideoMetadataService, SimulatedVideoMetadataService>();
        services.AddSingleton<IVideoDownloadService, SimulatedVideoDownloadService>();

        return services;
    }
}
