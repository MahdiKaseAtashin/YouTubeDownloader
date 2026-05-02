using App.Domain.Entities;

namespace App.Application.Ports;

public interface IScriptRegistry
{
    Task<IReadOnlyList<RegisteredScript>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RegisteredScript?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddOrUpdateAsync(RegisteredScript script, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}
