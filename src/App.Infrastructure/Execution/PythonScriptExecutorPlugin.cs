using App.Application.Ports;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Execution;

public sealed class PythonScriptExecutorPlugin : IScriptExecutorPlugin
{
    private readonly ILogger<PythonScriptExecutorPlugin> _logger;

    public PythonScriptExecutorPlugin(ILogger<PythonScriptExecutorPlugin> logger)
    {
        _logger = logger;
    }

    public IReadOnlyCollection<string> SupportedExtensions => new[] { ".py" };

    public int Priority => 80;

    public Task<ScriptExecutionResult> ExecuteAsync(
        ScriptExecutionRequest request,
        IProgress<string>? outputProgress,
        CancellationToken cancellationToken)
    {
        var interpreter = ResolvePython();
        if (interpreter is null)
        {
            return Task.FromResult(new ScriptExecutionResult(-1, false, "Python interpreter (python.exe or py.exe) was not found on PATH."));
        }

        var quotedScript = "\"" + request.ScriptPath.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        string arguments;
        if (interpreter.EndsWith("py.exe", StringComparison.OrdinalIgnoreCase))
        {
            arguments = $"-3 {quotedScript}";
        }
        else
        {
            arguments = quotedScript;
        }

        if (!string.IsNullOrWhiteSpace(request.Arguments))
        {
            arguments += " " + request.Arguments;
        }

        _logger.LogDebug("Launching {Interpreter} {Arguments}", interpreter, arguments);
        return ProcessOutputPump.RunAsync(interpreter, arguments, request.WorkingDirectory, outputProgress, cancellationToken);
    }

    private static string? ResolvePython()
    {
        foreach (var name in new[] { "python.exe", "python3.exe", "py.exe" })
        {
            var full = FindOnPath(name);
            if (full is not null)
            {
                return full;
            }
        }

        return null;
    }

    private static string? FindOnPath(string fileName)
    {
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            var candidate = Path.Combine(folder.Trim('"'), fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
