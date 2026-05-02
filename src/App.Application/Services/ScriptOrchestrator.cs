using App.Application.Ports;
using App.Domain.Entities;
using App.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace App.Application.Services;

public sealed class ScriptOrchestrator : IScriptOrchestrator
{
    private readonly IScriptRegistry _registry;
    private readonly IScriptExecutorRouter _executorRouter;
    private readonly IExecutionLogStore _logStore;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ScriptOrchestrator> _logger;
    private readonly object _sync = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _running = new();

    public ScriptOrchestrator(
        IScriptRegistry registry,
        IScriptExecutorRouter executorRouter,
        IExecutionLogStore logStore,
        IFileSystem fileSystem,
        ILogger<ScriptOrchestrator> logger)
    {
        _registry = registry;
        _executorRouter = executorRouter;
        _logStore = logStore;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public void Cancel(Guid scriptId)
    {
        CancellationTokenSource? cts;
        lock (_sync)
        {
            _running.TryGetValue(scriptId, out cts);
        }

        cts?.Cancel();
    }

    public async Task<ScriptRunOutcome> RunAsync(
        Guid scriptId,
        string? parameters,
        IProgress<string> outputProgress,
        CancellationToken cancellationToken)
    {
        var script = await _registry.GetByIdAsync(scriptId, cancellationToken).ConfigureAwait(false);
        if (script is null)
        {
            return new ScriptRunOutcome(ScriptRunStatus.Failed, "Script was not found.", null);
        }

        if (!_fileSystem.FileExists(script.FilePath))
        {
            var missing = $"Script file does not exist: {script.FilePath}";
            await PersistScriptStateAsync(script, DateTimeOffset.UtcNow, ScriptRunStatus.Failed, missing, cancellationToken)
                .ConfigureAwait(false);
            return new ScriptRunOutcome(ScriptRunStatus.Failed, missing, null);
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_sync)
        {
            _running[scriptId] = linkedCts;
        }

        var startedAt = DateTimeOffset.UtcNow;
        var logId = Guid.NewGuid();
        var outputBuilder = new System.Text.StringBuilder(capacity: 4096);
        var progress = new Progress<string>(line =>
        {
            outputProgress.Report(line);
            lock (outputBuilder)
            {
                if (outputBuilder.Length > 200_000)
                {
                    outputBuilder.Remove(0, outputBuilder.Length - 150_000);
                }

                outputBuilder.AppendLine(line);
            }
        });

        try
        {
            _logger.LogInformation("Starting script {ScriptName} ({ScriptId})", script.Name, scriptId);
            var request = new ScriptExecutionRequest(
                script.FilePath,
                parameters,
                _fileSystem.GetDirectoryName(script.FilePath));

            var result = await _executorRouter.ExecuteAsync(
                request,
                script.Extension,
                progress,
                linkedCts.Token).ConfigureAwait(false);

            var finishedAt = DateTimeOffset.UtcNow;
            var status = result.Cancelled
                ? ScriptRunStatus.Cancelled
                : result.ExitCode == 0
                    ? ScriptRunStatus.Succeeded
                    : ScriptRunStatus.Failed;

            var message = result.ErrorMessage
                ?? (status == ScriptRunStatus.Failed ? $"Process exited with code {result.ExitCode}." : null);

            await PersistScriptStateAsync(script, finishedAt, status, message, cancellationToken).ConfigureAwait(false);
            await AppendLogAsync(
                logId,
                script,
                startedAt,
                finishedAt,
                status,
                parameters,
                message,
                outputBuilder.ToString(),
                cancellationToken).ConfigureAwait(false);

            return new ScriptRunOutcome(status, message, result.ExitCode);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            var finishedAt = DateTimeOffset.UtcNow;
            await PersistScriptStateAsync(script, finishedAt, ScriptRunStatus.Cancelled, "Cancelled by user.", cancellationToken)
                .ConfigureAwait(false);
            await AppendLogAsync(
                logId,
                script,
                startedAt,
                finishedAt,
                ScriptRunStatus.Cancelled,
                parameters,
                "Cancelled by user.",
                outputBuilder.ToString(),
                cancellationToken).ConfigureAwait(false);

            return new ScriptRunOutcome(ScriptRunStatus.Cancelled, "Cancelled by user.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script execution failed for {ScriptName}", script.Name);
            var finishedAt = DateTimeOffset.UtcNow;
            var friendly = $"An unexpected error occurred: {ex.Message}";
            await PersistScriptStateAsync(script, finishedAt, ScriptRunStatus.Failed, friendly, cancellationToken)
                .ConfigureAwait(false);
            await AppendLogAsync(
                logId,
                script,
                startedAt,
                finishedAt,
                ScriptRunStatus.Failed,
                parameters,
                friendly,
                outputBuilder.ToString(),
                cancellationToken).ConfigureAwait(false);

            return new ScriptRunOutcome(ScriptRunStatus.Failed, friendly, null);
        }
        finally
        {
            lock (_sync)
            {
                _running.Remove(scriptId);
            }

            linkedCts.Dispose();
        }
    }

    private async Task PersistScriptStateAsync(
        RegisteredScript script,
        DateTimeOffset runAt,
        ScriptRunStatus status,
        string? error,
        CancellationToken cancellationToken)
    {
        var updated = script.WithRunResult(runAt, status, error);
        await _registry.AddOrUpdateAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    private async Task AppendLogAsync(
        Guid logId,
        RegisteredScript script,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        ScriptRunStatus status,
        string? parameters,
        string? errorMessage,
        string fullOutput,
        CancellationToken cancellationToken)
    {
        var tail = fullOutput.Length > 16_000 ? fullOutput[^16_000..] : fullOutput;
        var entry = new ScriptExecutionLogEntry
        {
            Id = logId,
            ScriptId = script.Id,
            ScriptName = script.Name,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            Status = status,
            Parameters = parameters,
            ErrorMessage = errorMessage,
            OutputTail = tail
        };

        await _logStore.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
    }
}
