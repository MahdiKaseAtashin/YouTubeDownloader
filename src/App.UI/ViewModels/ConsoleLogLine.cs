namespace App.UI.ViewModels;

public sealed class ConsoleLogLine
{
    public ConsoleLogLine(DateTime timestamp, string message, bool isStdErr)
    {
        Timestamp = timestamp;
        Message = message;
        IsStdErr = isStdErr;
    }

    public DateTime Timestamp { get; }
    public string Message { get; }
    public bool IsStdErr { get; }

    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");

    public string FormattedLine => $"[{TimestampText}] {(IsStdErr ? "[stderr] " : string.Empty)}{Message}";
}
