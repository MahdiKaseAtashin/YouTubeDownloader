namespace App.Application.Services;

public interface IScriptSchedulerService
{
    bool IsEnabled { get; }
    void Configure(Guid scriptId, TimeSpan interval, string? parameters);
    void Stop();
}
