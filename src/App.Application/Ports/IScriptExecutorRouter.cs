namespace App.Application.Ports;

public interface IScriptExecutorRouter
{
    Task<ScriptExecutionResult> ExecuteAsync(
        ScriptExecutionRequest request,
        string fileExtension,
        IProgress<string>? outputProgress,
        CancellationToken cancellationToken);
}
