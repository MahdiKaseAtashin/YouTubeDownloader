using App.Application.Ports;
using App.Domain.Entities;
using App.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace App.Application.Services;

public sealed class ScriptCatalogService : IScriptCatalogService
{
    private readonly IScriptRegistry _registry;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ScriptCatalogService> _logger;

    public ScriptCatalogService(
        IScriptRegistry registry,
        IFileSystem fileSystem,
        ILogger<ScriptCatalogService> logger)
    {
        _registry = registry;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public Task<IReadOnlyList<RegisteredScript>> GetScriptsAsync(CancellationToken cancellationToken = default) =>
        _registry.GetAllAsync(cancellationToken);

    public async Task<RegisteredScript?> RegisterFromFileAsync(
        string filePath,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var fullPath = _fileSystem.GetFullPath(filePath);
        if (!_fileSystem.FileExists(fullPath))
        {
            _logger.LogWarning("Cannot register missing file {Path}", fullPath);
            return null;
        }

        var ext = _fileSystem.GetExtension(fullPath);
        var script = new RegisteredScript
        {
            Id = Guid.NewGuid(),
            Name = _fileSystem.GetFileNameWithoutExtension(fullPath),
            Description = description ?? string.Empty,
            FilePath = fullPath,
            Extension = ext,
            LastRunAt = null,
            LastStatus = ScriptRunStatus.NeverRun,
            LastErrorMessage = null
        };

        await _registry.AddOrUpdateAsync(script, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Registered script {Name} at {Path}", script.Name, fullPath);
        return script;
    }

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) =>
        _registry.RemoveAsync(id, cancellationToken);
}
