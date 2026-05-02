using App.Application.Ports;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Execution;

public sealed class PowerShellScriptExecutorPlugin : IScriptExecutorPlugin
{
    private readonly ILogger<PowerShellScriptExecutorPlugin> _logger;

    public PowerShellScriptExecutorPlugin(ILogger<PowerShellScriptExecutorPlugin> logger)
    {
        _logger = logger;
    }

    public IReadOnlyCollection<string> SupportedExtensions => new[] { ".ps1" };

    public int Priority => 100;

    public Task<ScriptExecutionResult> ExecuteAsync(
        ScriptExecutionRequest request,
        IProgress<string>? outputProgress,
        CancellationToken cancellationToken)
    {
        var shell = ResolvePowerShellExecutable();
        if (shell is null)
        {
            return Task.FromResult(new ScriptExecutionResult(-1, false, "PowerShell (pwsh.exe or powershell.exe) was not found on PATH."));
        }

        var escapedPath = "\"" + request.ScriptPath.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var args = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -File {escapedPath}";
        if (!string.IsNullOrWhiteSpace(request.Arguments))
        {
            args += " " + request.Arguments;
        }

        _logger.LogDebug("Launching {Shell} {Args}", shell, args);
        return ProcessOutputPump.RunAsync(shell, args, request.WorkingDirectory, outputProgress, cancellationToken);
    }

    private static string? ResolvePowerShellExecutable()
    {
        foreach (var candidate in new[] { "pwsh.exe", "powershell.exe" })
        {
            var full = FindOnPath(candidate);
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
