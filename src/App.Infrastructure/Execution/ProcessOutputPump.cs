using System.Diagnostics;
using App.Application.Ports;

namespace App.Infrastructure.Execution;

internal static class ProcessOutputPump
{
    public static async Task<ScriptExecutionResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        IProgress<string>? outputProgress,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                outputProgress?.Report(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                outputProgress?.Report("[stderr] " + e.Data);
            }
        };

        try
        {
            if (!process.Start())
            {
                return new ScriptExecutionResult(-1, false, "Failed to start process.");
            }
        }
        catch (Exception ex)
        {
            return new ScriptExecutionResult(-1, false, ex.Message);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignored
            }
        });

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new ScriptExecutionResult(process.ExitCode, false, null);
        }
        catch (OperationCanceledException)
        {
            var code = process.HasExited ? process.ExitCode : -1;
            return new ScriptExecutionResult(code, true, null);
        }
    }
}
