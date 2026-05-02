namespace App.Domain.Enums;

public enum ScriptRunStatus
{
    NeverRun = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}
