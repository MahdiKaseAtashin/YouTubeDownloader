using App.Application.Dtos;

namespace App.Application.Ports;

public interface IBrowserProfileDiscovery
{
    IReadOnlyList<BrowserOption> GetInstalledBrowsers();

    IReadOnlyList<BrowserProfileOption> GetProfiles(string browserId);

    string GetDefaultBrowserId();
}
