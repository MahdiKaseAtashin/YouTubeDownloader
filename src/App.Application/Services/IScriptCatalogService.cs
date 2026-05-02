using App.Domain.Entities;

namespace App.Application.Services;

public interface IScriptCatalogService
{
    Task<IReadOnlyList<RegisteredScript>> GetScriptsAsync(CancellationToken cancellationToken = default);
    Task<RegisteredScript?> RegisterFromFileAsync(string filePath, string? description, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}
