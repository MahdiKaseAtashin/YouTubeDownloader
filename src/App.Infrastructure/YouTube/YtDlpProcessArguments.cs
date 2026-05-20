namespace App.Infrastructure.YouTube;

internal static class YtDlpProcessArguments
{
    public static string Join(IEnumerable<string> args) =>
        string.Join(" ", args.Select(Quote));

    public static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (value.Contains(' ') || value.Contains('\\') || value.Contains(':'))
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        return value;
    }
}
