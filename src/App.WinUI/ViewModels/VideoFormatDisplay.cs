namespace Yt.Client.ViewModels;

public sealed class VideoFormatDisplay
{
    public VideoFormatDisplay(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }
}
