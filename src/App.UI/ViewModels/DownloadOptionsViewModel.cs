using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace App.UI.ViewModels;

public sealed partial class DownloadOptionsViewModel : ObservableObject
{
    public ObservableCollection<string> VideoContainers { get; } = new() { "MP4", "WEBM" };

    public ObservableCollection<string> QualityOptions { get; } = new() { "1080p", "720p", "480p", "Best available" };

    [ObservableProperty]
    private string _selectedVideoContainer = "MP4";

    [ObservableProperty]
    private string _selectedQuality = "1080p";

    [ObservableProperty]
    private bool _downloadVideo = true;

    [ObservableProperty]
    private bool _downloadThumbnail = true;

    [ObservableProperty]
    private bool _downloadSubtitles;
}
