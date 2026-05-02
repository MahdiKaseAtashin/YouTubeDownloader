using App.Domain.Entities;
using App.Domain.Enums;

namespace App.Application.Services;

public interface IScriptOrchestrator
{
    Task<ScriptRunOutcome> RunAsync(
        Guid scriptId,
        string? parameters,
        IProgress<string> outputProgress,
        CancellationToken cancellationToken);

    void Cancel(Guid scriptId);
}

public sealed record ScriptRunOutcome(
    ScriptRunStatus Status,
    string? ErrorMessage,
    int? ExitCode);
