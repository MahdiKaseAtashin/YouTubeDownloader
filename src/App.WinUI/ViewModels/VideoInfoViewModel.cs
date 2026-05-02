using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Yt.Client.ViewModels;

public sealed partial class VideoInfoViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _channelName = string.Empty;

    [ObservableProperty]
    private string _durationDisplay = string.Empty;

    [ObservableProperty]
    private string _videoId = string.Empty;

    [ObservableProperty]
    private ImageSource? _thumbnail;

    public bool HasThumbnail => Thumbnail is not null;

    partial void OnThumbnailChanged(ImageSource? value) => OnPropertyChanged(nameof(HasThumbnail));

    public ObservableCollection<VideoFormatDisplay> Formats { get; } = new();

    [ObservableProperty]
    private VideoFormatDisplay? _selectedFormat;

    public ObservableCollection<string> SubtitleLanguages { get; } = new();

    [ObservableProperty]
    private string? _selectedSubtitleLanguage;
}
