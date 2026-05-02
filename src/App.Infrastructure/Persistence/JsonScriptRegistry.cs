using System.Text.Json;
using App.Application.Ports;
using App.Domain.Entities;
using App.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Persistence;

public sealed class JsonScriptRegistry : IScriptRegistry
{
    private readonly IAppPaths _paths;
    private readonly ILogger<JsonScriptRegistry> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public JsonScriptRegistry(IAppPaths paths, ILogger<JsonScriptRegistry> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RegisteredScript>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_paths.RegistryFilePath))
            {
                return Array.Empty<RegisteredScript>();
            }

            await using var stream = File.OpenRead(_paths.RegistryFilePath);
            var dto = await JsonSerializer.DeserializeAsync<RegistryDto>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return dto?.Scripts?.Select(Map).ToList() ?? (IReadOnlyList<RegisteredScript>)Array.Empty<RegisteredScript>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<RegisteredScript?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(s => s.Id == id);
    }

    public Task AddOrUpdateAsync(RegisteredScript script, CancellationToken cancellationToken = default) =>
        MutateAsync(list =>
        {
            var idx = list.FindIndex(s => s.Id == script.Id);
            if (idx >= 0)
            {
                list[idx] = script;
            }
            else
            {
                list.Add(script);
            }
        }, cancellationToken);

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(list => list.RemoveAll(s => s.Id == id), cancellationToken);

    private async Task MutateAsync(Action<List<RegisteredScript>> change, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = (await ReadAllInternalAsync(cancellationToken).ConfigureAwait(false)).ToList();
            change(list);
            await WriteAllInternalAsync(list, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<RegisteredScript>> ReadAllInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.RegistryFilePath))
        {
            return Array.Empty<RegisteredScript>();
        }

        await using var stream = File.OpenRead(_paths.RegistryFilePath);
        var dto = await JsonSerializer.DeserializeAsync<RegistryDto>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return dto?.Scripts?.Select(Map).ToList() ?? (IReadOnlyList<RegisteredScript>)Array.Empty<RegisteredScript>();
    }

    private async Task WriteAllInternalAsync(IReadOnlyList<RegisteredScript> scripts, CancellationToken cancellationToken)
    {
        var dto = new RegistryDto { Scripts = scripts.Select(Map).ToList() };
        var temp = _paths.RegistryFilePath + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Copy(temp, _paths.RegistryFilePath, overwrite: true);
        File.Delete(temp);
        _logger.LogDebug("Persisted {Count} scripts", scripts.Count);
    }

    private static RegisteredScript Map(ScriptDto s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        FilePath = s.FilePath,
        Extension = s.Extension,
        LastRunAt = s.LastRunAt,
        LastStatus = s.LastStatus,
        LastErrorMessage = s.LastErrorMessage
    };

    private static ScriptDto Map(RegisteredScript s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        FilePath = s.FilePath,
        Extension = s.Extension,
        LastRunAt = s.LastRunAt,
        LastStatus = s.LastStatus,
        LastErrorMessage = s.LastErrorMessage
    };

    private sealed class RegistryDto
    {
        public List<ScriptDto> Scripts { get; set; } = new();
    }

    private sealed class ScriptDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public DateTimeOffset? LastRunAt { get; set; }
        public ScriptRunStatus LastStatus { get; set; }
        public string? LastErrorMessage { get; set; }
    }
}
