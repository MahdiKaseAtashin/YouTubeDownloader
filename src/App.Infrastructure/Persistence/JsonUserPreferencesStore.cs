using System.Text.Json;
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
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load preferences");
            LastOutputFolder = null;
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
            var dto = new PrefsDto { LastOutputFolder = folderPath };
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

    private sealed class PrefsDto
    {
        public string? LastOutputFolder { get; set; }
    }
}
