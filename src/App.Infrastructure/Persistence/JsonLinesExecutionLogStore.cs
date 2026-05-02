using System.Text.Json;
using App.Application.Ports;
using App.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Persistence;

public sealed class JsonLinesExecutionLogStore : IExecutionLogStore
{
    private readonly IAppPaths _paths;
    private readonly ILogger<JsonLinesExecutionLogStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonLinesExecutionLogStore(IAppPaths paths, ILogger<JsonLinesExecutionLogStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task AppendAsync(ScriptExecutionLogEntry entry, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var line = JsonSerializer.Serialize(Map(entry), JsonOptions);
            await File.AppendAllLinesAsync(_paths.ExecutionLogFilePath, new[] { line }, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ScriptExecutionLogEntry>> GetRecentAsync(
        int maxEntries,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_paths.ExecutionLogFilePath) || maxEntries <= 0)
            {
                return Array.Empty<ScriptExecutionLogEntry>();
            }

            var lines = await File.ReadAllLinesAsync(_paths.ExecutionLogFilePath, cancellationToken)
                .ConfigureAwait(false);

            var tail = lines.Length <= maxEntries ? lines : lines[^maxEntries..];
            var result = new List<ScriptExecutionLogEntry>(tail.Length);
            foreach (var line in tail)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var dto = JsonSerializer.Deserialize<LogDto>(line, JsonOptions);
                    if (dto is not null)
                    {
                        result.Add(Map(dto));
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Skipping malformed log line");
                }
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static LogDto Map(ScriptExecutionLogEntry e) => new()
    {
        Id = e.Id,
        ScriptId = e.ScriptId,
        ScriptName = e.ScriptName,
        StartedAt = e.StartedAt,
        FinishedAt = e.FinishedAt,
        Status = e.Status,
        Parameters = e.Parameters,
        ErrorMessage = e.ErrorMessage,
        OutputTail = e.OutputTail
    };

    private static ScriptExecutionLogEntry Map(LogDto d) => new()
    {
        Id = d.Id,
        ScriptId = d.ScriptId,
        ScriptName = d.ScriptName,
        StartedAt = d.StartedAt,
        FinishedAt = d.FinishedAt,
        Status = d.Status,
        Parameters = d.Parameters,
        ErrorMessage = d.ErrorMessage,
        OutputTail = d.OutputTail
    };

    private sealed class LogDto
    {
        public Guid Id { get; set; }
        public Guid ScriptId { get; set; }
        public string ScriptName { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public App.Domain.Enums.ScriptRunStatus Status { get; set; }
        public string? Parameters { get; set; }
        public string? ErrorMessage { get; set; }
        public string? OutputTail { get; set; }
    }
}
