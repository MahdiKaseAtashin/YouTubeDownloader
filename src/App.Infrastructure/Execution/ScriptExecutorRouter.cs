using App.Application.Ports;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Execution;

public sealed class ScriptExecutorRouter : IScriptExecutorRouter
{
    private readonly IReadOnlyList<IScriptExecutorPlugin> _plugins;
    private readonly ILogger<ScriptExecutorRouter> _logger;

    public ScriptExecutorRouter(IEnumerable<IScriptExecutorPlugin> plugins, ILogger<ScriptExecutorRouter> logger)
    {
        _plugins = plugins
            .OrderByDescending(p => p.Priority)
            .ToList();
        _logger = logger;
    }

    public Task<ScriptExecutionResult> ExecuteAsync(
        ScriptExecutionRequest request,
        string fileExtension,
        IProgress<string>? outputProgress,
        CancellationToken cancellationToken)
    {
        var ext = string.IsNullOrWhiteSpace(fileExtension)
            ? Path.GetExtension(request.ScriptPath)
            : fileExtension;

        var plugin = _plugins.FirstOrDefault(p =>
            p.SupportedExtensions.Any(s => s.Equals(ext, StringComparison.OrdinalIgnoreCase)));

        if (plugin is null)
        {
            _logger.LogWarning("No executor plugin registered for extension {Extension}", ext);
            return Task.FromResult(new ScriptExecutionResult(-1, false, $"No executor is registered for '{ext}' scripts."));
        }

        return plugin.ExecuteAsync(request, outputProgress, cancellationToken);
    }
}
