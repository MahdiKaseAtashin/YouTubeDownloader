using Microsoft.Extensions.Logging;

namespace App.Application.Services;

public sealed class ScriptSchedulerService : IScriptSchedulerService
{
    private readonly IScriptOrchestrator _orchestrator;
    private readonly ILogger<ScriptSchedulerService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private Guid _scriptId;
    private TimeSpan _interval = TimeSpan.FromMinutes(5);
    private string? _parameters;

    public ScriptSchedulerService(IScriptOrchestrator orchestrator, ILogger<ScriptSchedulerService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public bool IsEnabled => _loop is { IsCompleted: false };

    public void Configure(Guid scriptId, TimeSpan interval, string? parameters)
    {
        Stop();
        _scriptId = scriptId;
        _interval = interval <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : interval;
        _parameters = parameters;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunLoopAsync(_cts.Token), CancellationToken.None);
        _logger.LogInformation("Scheduler enabled for script {ScriptId} every {Interval}", scriptId, _interval);
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // ignored
        }

        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await _gate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    await _orchestrator.RunAsync(
                        _scriptId,
                        _parameters,
                        new Progress<string>(_ => { }),
                        token).ConfigureAwait(false);
                }
                finally
                {
                    _gate.Release();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled run failed");
            }

            try
            {
                await Task.Delay(_interval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

}
