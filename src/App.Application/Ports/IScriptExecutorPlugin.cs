namespace App.Application.Ports;

public interface IScriptExecutorPlugin
{
    IReadOnlyCollection<string> SupportedExtensions { get; }
    int Priority { get; }
    Task<ScriptExecutionResult> ExecuteAsync(
        ScriptExecutionRequest request,
        IProgress<string>? outputProgress,
        CancellationToken cancellationToken);
}

public sealed record ScriptExecutionRequest(
    string ScriptPath,
    string? Arguments,
    string? WorkingDirectory);

public sealed record ScriptExecutionResult(
    int ExitCode,
    bool Cancelled,
    string? ErrorMessage);
