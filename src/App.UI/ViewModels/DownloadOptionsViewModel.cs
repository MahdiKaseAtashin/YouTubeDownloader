using System.Collections.ObjectModel;
using App.Application.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace App.UI.ViewModels;

public sealed partial class DownloadOptionsViewModel : ObservableObject
{
    public ObservableCollection<string> VideoContainers { get; } = new() { "MP4", "WEBM" };

    public ObservableCollection<string> QualityOptions { get; } = new() { "1080p", "720p", "480p", "Best available" };
    public ObservableCollection<YouTubeAuthMode> AuthModes { get; } = new() { YouTubeAuthMode.None, YouTubeAuthMode.BrowserCookies, YouTubeAuthMode.CookieFile };
    public ObservableCollection<string> BrowserOptions { get; } = new() { "edge", "chrome", "firefox" };

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

    [ObservableProperty]
    private YouTubeAuthMode _selectedAuthMode = YouTubeAuthMode.BrowserCookies;

    [ObservableProperty]
    private string _selectedBrowser = "edge";

    [ObservableProperty]
    private string _browserProfile = "Default";

    [ObservableProperty]
    private string _cookieFilePath = string.Empty;
}
