using System.Text.Json;
using App.Application.Dtos;
using App.Application.Ports;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Persistence;

public sealed class JsonUserPreferencesStore : IUserPreferencesStore
{
    private readonly IAppPaths _paths;
    private readonly ILogger<JsonUserPreferencesStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public JsonUserPreferencesStore(IAppPaths paths, ILogger<JsonUserPreferencesStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public string? LastOutputFolder { get; private set; }
    public YouTubeAuthSettings YouTubeAuthSettings { get; private set; } = new(YouTubeAuthMode.None, null, null, null);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_paths.PreferencesFilePath))
            {
                LastOutputFolder = null;
                return;
            }

            await using var stream = File.OpenRead(_paths.PreferencesFilePath);
            var dto = await JsonSerializer.DeserializeAsync<PrefsDto>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            LastOutputFolder = string.IsNullOrWhiteSpace(dto?.LastOutputFolder) ? null : dto.LastOutputFolder;
            YouTubeAuthSettings = new YouTubeAuthSettings(
                dto?.AuthMode ?? YouTubeAuthMode.None,
                Normalize(dto?.AuthBrowser),
                Normalize(dto?.AuthBrowserProfile),
                Normalize(dto?.AuthCookieFilePath),
                Normalize(dto?.AuthBrowserProfileDirectoryPath));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load preferences");
            LastOutputFolder = null;
            YouTubeAuthSettings = new YouTubeAuthSettings(YouTubeAuthMode.None, null, null, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveLastOutputFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LastOutputFolder = folderPath;
            var dto = new PrefsDto
            {
                LastOutputFolder = folderPath,
                AuthMode = YouTubeAuthSettings.Mode,
                AuthBrowser = YouTubeAuthSettings.Browser,
                AuthBrowserProfile = YouTubeAuthSettings.BrowserProfile,
                AuthCookieFilePath = YouTubeAuthSettings.CookieFilePath,
                AuthBrowserProfileDirectoryPath = YouTubeAuthSettings.BrowserProfileDirectoryPath
            };
            var temp = _paths.PreferencesFilePath + ".tmp";
            await using (var stream = File.Create(temp))
            {
                await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Copy(temp, _paths.PreferencesFilePath, overwrite: true);
            File.Delete(temp);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveYouTubeAuthSettingsAsync(YouTubeAuthSettings settings, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            YouTubeAuthSettings = new YouTubeAuthSettings(
                settings.Mode,
                Normalize(settings.Browser),
                Normalize(settings.BrowserProfile),
                Normalize(settings.CookieFilePath),
                Normalize(settings.BrowserProfileDirectoryPath));

            var dto = new PrefsDto
            {
                LastOutputFolder = LastOutputFolder,
                AuthMode = YouTubeAuthSettings.Mode,
                AuthBrowser = YouTubeAuthSettings.Browser,
                AuthBrowserProfile = YouTubeAuthSettings.BrowserProfile,
                AuthCookieFilePath = YouTubeAuthSettings.CookieFilePath,
                AuthBrowserProfileDirectoryPath = YouTubeAuthSettings.BrowserProfileDirectoryPath
            };

            var temp = _paths.PreferencesFilePath + ".tmp";
            await using (var stream = File.Create(temp))
            {
                await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Copy(temp, _paths.PreferencesFilePath, overwrite: true);
            File.Delete(temp);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class PrefsDto
    {
        public string? LastOutputFolder { get; set; }
        public YouTubeAuthMode AuthMode { get; set; } = YouTubeAuthMode.None;
        public string? AuthBrowser { get; set; }
        public string? AuthBrowserProfile { get; set; }
        public string? AuthCookieFilePath { get; set; }
        public string? AuthBrowserProfileDirectoryPath { get; set; }
    }
}
