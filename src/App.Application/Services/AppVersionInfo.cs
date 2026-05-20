using System.Reflection;

namespace App.Application.Services;

public static class AppVersionInfo
{
    public static string Display { get; } = Resolve();

    private static string Resolve()
    {
        var attribute = typeof(AppVersionInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (!string.IsNullOrWhiteSpace(attribute?.InformationalVersion))
        {
            return attribute.InformationalVersion.Split('+')[0].Trim();
        }

        var version = typeof(AppVersionInfo).Assembly.GetName().Version;
        return version is null ? "0.0.0" : version.ToString(3);
    }
}
