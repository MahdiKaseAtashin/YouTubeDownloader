using System.Text.Json;
using App.Application.Dtos;
using App.Application.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace App.Infrastructure.Auth;

public sealed class WindowsBrowserProfileDiscovery : IBrowserProfileDiscovery
{
    private static readonly (string Id, string DisplayName, Func<string?> UserDataPath)[] Browsers =
    {
        ("edge", "Microsoft Edge", () => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data")),
        ("chrome", "Google Chrome", () => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google", "Chrome", "User Data")),
        ("firefox", "Mozilla Firefox", () => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mozilla", "Firefox"))
    };

    private readonly ILogger<WindowsBrowserProfileDiscovery> _logger;

    public WindowsBrowserProfileDiscovery(ILogger<WindowsBrowserProfileDiscovery> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<BrowserOption> GetInstalledBrowsers()
    {
        var list = new List<BrowserOption>();
        foreach (var (id, displayName, pathFactory) in Browsers)
        {
            var path = pathFactory();
            var installed = id == "firefox"
                ? File.Exists(Path.Combine(path, "profiles.ini"))
                : !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

            if (installed)
            {
                list.Add(new BrowserOption(id, displayName, true));
            }
        }

        if (list.Count == 0)
        {
            list.Add(new BrowserOption("edge", "Microsoft Edge", false));
        }

        return list;
    }

    public IReadOnlyList<BrowserProfileOption> GetProfiles(string browserId)
    {
        var normalized = browserId.Trim().ToLowerInvariant();
        try
        {
            return normalized switch
            {
                "edge" or "chrome" => GetChromiumProfiles(normalized),
                "firefox" => GetFirefoxProfiles(),
                _ => new List<BrowserProfileOption> { new(normalized, "Default", "Default profile") }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enumerate profiles for {Browser}", browserId);
            return new List<BrowserProfileOption> { new(normalized, "Default", "Default profile") };
        }
    }

    public string GetDefaultBrowserId()
    {
        try
        {
            var progId = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice",
                "ProgId",
                null) as string;

            if (!string.IsNullOrWhiteSpace(progId))
            {
                if (progId.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
                {
                    return "chrome";
                }

                if (progId.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
                {
                    return "firefox";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read default browser from registry");
        }

        return "edge";
    }

    private static List<BrowserProfileOption> GetChromiumProfiles(string browserId)
    {
        var userData = browserId switch
        {
            "edge" => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Edge", "User Data"),
            "chrome" => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "User Data"),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(userData) || !Directory.Exists(userData))
        {
            return new List<BrowserProfileOption> { new(browserId, "Default", "Default profile", userData) };
        }

        var profiles = new List<BrowserProfileOption>();
        foreach (var dir in Directory.EnumerateDirectories(userData))
        {
            var name = Path.GetFileName(dir);
            if (name is "System Profile" or "Guest Profile" or "Crashpad" or "GrShaderCache" or "ShaderCache" or "WidevineCdm")
            {
                continue;
            }

            if (!File.Exists(Path.Combine(dir, "Preferences")) &&
                !File.Exists(Path.Combine(dir, "Cookies")) &&
                name != "Default")
            {
                continue;
            }

            var label = FormatChromiumProfileLabel(name, dir);
            profiles.Add(new BrowserProfileOption(browserId, name, label, dir));
        }

        if (profiles.Count == 0)
        {
            profiles.Add(new BrowserProfileOption(browserId, "Default", "Default profile", userData));
        }

        return profiles
            .OrderBy(p => p.ProfileId == "Default" ? 0 : 1)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatChromiumProfileLabel(string profileId, string profileDir)
    {
        var accountName = TryReadChromiumAccountName(profileDir);
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            return $"{accountName} ({profileId})";
        }

        return profileId switch
        {
            "Default" => "Default profile",
            _ => profileId
        };
    }

    private static string? TryReadChromiumAccountName(string profileDir)
    {
        try
        {
            var prefsPath = Path.Combine(profileDir, "Preferences");
            if (!File.Exists(prefsPath))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(prefsPath));
            if (doc.RootElement.TryGetProperty("account_info", out var accounts) &&
                accounts.ValueKind == JsonValueKind.Array)
            {
                foreach (var account in accounts.EnumerateArray())
                {
                    if (account.TryGetProperty("given_name", out var given) &&
                        given.ValueKind == JsonValueKind.String)
                    {
                        return given.GetString();
                    }

                    if (account.TryGetProperty("email", out var email) &&
                        email.ValueKind == JsonValueKind.String)
                    {
                        return email.GetString();
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("profile", out var profile) &&
                profile.TryGetProperty("name", out var profileName) &&
                profileName.ValueKind == JsonValueKind.String)
            {
                return profileName.GetString();
            }
        }
        catch
        {
            // optional enrichment only
        }

        return null;
    }

    private static List<BrowserProfileOption> GetFirefoxProfiles()
    {
        var iniPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mozilla", "Firefox", "profiles.ini");

        if (!File.Exists(iniPath))
        {
            return new List<BrowserProfileOption> { new("firefox", "default-release", "Default profile") };
        }

        var profiles = new List<BrowserProfileOption>();
        var firefoxRoot = Path.GetDirectoryName(iniPath)!;
        string? currentSection = null;
        var sectionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(iniPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                AddFirefoxProfile(profiles, firefoxRoot, sectionData);
                sectionData.Clear();
                currentSection = line[1..^1];
                continue;
            }

            if (currentSection is null)
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq > 0)
            {
                sectionData[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }

        AddFirefoxProfile(profiles, firefoxRoot, sectionData);

        if (profiles.Count == 0)
        {
            profiles.Add(new BrowserProfileOption("firefox", "default-release", "Default profile"));
        }

        return profiles;
    }

    private static void AddFirefoxProfile(
        List<BrowserProfileOption> profiles,
        string firefoxRoot,
        Dictionary<string, string> section)
    {
        if (!section.TryGetValue("Path", out var relativePath))
        {
            return;
        }

        var isRelative = !section.TryGetValue("IsRelative", out var rel) || rel != "0";
        var fullPath = isRelative ? Path.Combine(firefoxRoot, relativePath) : relativePath;
        if (!Directory.Exists(fullPath))
        {
            return;
        }

        var profileId = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var display = section.TryGetValue("Name", out var displayName) && !string.IsNullOrWhiteSpace(displayName)
            ? $"{displayName} ({profileId})"
            : profileId;

        profiles.Add(new BrowserProfileOption("firefox", profileId, display, fullPath));
    }
}
