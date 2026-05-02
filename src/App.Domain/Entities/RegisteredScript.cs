using App.Domain.Enums;

namespace App.Domain.Entities;

public sealed class RegisteredScript
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;
    public DateTimeOffset? LastRunAt { get; init; }
    public ScriptRunStatus LastStatus { get; init; }
    public string? LastErrorMessage { get; init; }

    public RegisteredScript WithRunResult(
        DateTimeOffset runAt,
        ScriptRunStatus status,
        string? errorMessage)
    {
        return new RegisteredScript
        {
            Id = Id,
            Name = Name,
            Description = Description,
            FilePath = FilePath,
            Extension = Extension,
            LastRunAt = runAt,
            LastStatus = status,
            LastErrorMessage = errorMessage
        };
    }
}
