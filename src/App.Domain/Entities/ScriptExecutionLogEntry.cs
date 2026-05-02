using App.Domain.Enums;

namespace App.Domain.Entities;

public sealed class ScriptExecutionLogEntry
{
    public Guid Id { get; init; }
    public Guid ScriptId { get; init; }
    public string ScriptName { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public ScriptRunStatus Status { get; init; }
    public string? Parameters { get; init; }
    public string? ErrorMessage { get; init; }
    public string? OutputTail { get; init; }
}
