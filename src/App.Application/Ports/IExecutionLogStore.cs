using App.Domain.Entities;

namespace App.Application.Ports;

public interface IExecutionLogStore
{
    Task AppendAsync(ScriptExecutionLogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScriptExecutionLogEntry>> GetRecentAsync(int maxEntries, CancellationToken cancellationToken = default);
}
