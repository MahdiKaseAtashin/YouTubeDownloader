using App.Application.Ports;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Execution;

public sealed class BatchScriptExecutorPlugin : IScriptExecutorPlugin
{
    private readonly ILogger<BatchScriptExecutorPlugin> _logger;

    public BatchScriptExecutorPlugin(ILogger<BatchScriptExecutorPlugin> logger)
    {
        _logger = logger;
    }

    public IReadOnlyCollection<string> SupportedExtensions => new[] { ".bat", ".cmd" };

    public int Priority => 90;

    public Task<ScriptExecutionResult> ExecuteAsync(
        ScriptExecutionRequest request,
        IProgress<string>? outputProgress,
        CancellationToken cancellationToken)
    {
        var escaped = "\"" + request.ScriptPath.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var args = "/d /s /c " + escaped;
        if (!string.IsNullOrWhiteSpace(request.Arguments))
        {
            args += " " + request.Arguments;
        }

        _logger.LogDebug("Launching cmd.exe {Args}", args);
        return ProcessOutputPump.RunAsync(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            args,
            request.WorkingDirectory,
            outputProgress,
            cancellationToken);
    }
}
