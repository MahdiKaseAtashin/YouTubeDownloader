using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using App.Application.Dtos;
using App.Application.Ports;
using App.Application.Services;
using App.UI.Commands;
using App.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;

namespace App.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const int MaxLogLines = 4000;
    private static readonly HttpClient Http = new();

    private readonly IVideoMetadataService _metadata;
    private readonly IVideoDownloadService _downloader;
    private readonly IUserPreferencesStore _preferences;
    private readonly Dispatcher _dispatcher;

    private CancellationTokenSource? _downloadCts;
    private VideoInfoViewModel? _videoInfoSubscription;

    public DownloadOptionsViewModel Options { get; } = new();
    public ObservableCollection<ConsoleLogLine> ActivityLog { get; } = new();

    public ICommand LoadedCommand { get; }
    public ICommand FetchInfoCommand { get; }
    public ICommand PasteFromClipboardCommand { get; }
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand CancelDownloadCommand { get; }
    public ICommand ClearLogCommand { get; }

    public MainViewModel(
        IVideoMetadataService metadata,
        IVideoDownloadService downloader,
        IUserPreferencesStore preferences)
    {
        _metadata = metadata;
        _downloader = downloader;
        _preferences = preferences;
        _dispatcher = System.Windows.Application.Current!.Dispatcher;

        LoadedCommand = new AsyncRelayCommand(InitializeAsync, () => true);
        FetchInfoCommand = new AsyncRelayCommand(FetchMetadataAsync, () => IsUrlValid && !IsFetching && !IsDownloading);
        PasteFromClipboardCommand = new DelegateCommand(_ => PasteFromClipboard(), _ => !IsFetching && !IsDownloading);
        BrowseOutputFolderCommand = new DelegateCommand(_ => BrowseFolder(), _ => !IsDownloading);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, CanDownload);
        CancelDownloadCommand = new DelegateCommand(_ => CancelDownload(), _ => IsDownloading);
        ClearLogCommand = new DelegateCommand(_ => ActivityLog.Clear(), _ => true);

        Options.PropertyChanged += (_, _) =>
        {
            RaiseDownloadCanExecute();
            OnPropertyChanged(nameof(IsSubtitlePickerEnabled));
        };
    }

    public bool IsSubtitlePickerEnabled =>
        VideoInfo is not null
        && VideoInfo.SubtitleLanguages.Count > 0
        && Options.DownloadSubtitles;

    private void RaiseFetchCanExecute() => ((AsyncRelayCommand)FetchInfoCommand).RaiseCanExecuteChanged();

    private void RaiseDownloadCanExecute() => ((AsyncRelayCommand)DownloadCommand).RaiseCanExecuteChanged();

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private bool _isUrlValid;

    [ObservableProperty]
    private bool _isFetching;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private VideoInfoViewModel? _videoInfo;

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    private bool _isOutputFolderValid;

    [ObservableProperty]
    private string _statusMessage = "Paste a YouTube link and choose Fetch info.";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    [ObservableProperty]
    private bool _isDarkTheme;

    public bool HasVideoInfo => VideoInfo is not null;

    partial void OnIsDarkThemeChanged(bool value) => ThemeSwitcher.Apply(value);

    partial void OnUrlChanged(string value)
    {
        IsUrlValid = YoutubeUrlValidator.IsValid(value);
        RaiseFetchCanExecute();
        RaiseDownloadCanExecute();
    }

    partial void OnOutputFolderChanged(string value)
    {
        IsOutputFolderValid = Directory.Exists(value?.Trim());
        RaiseDownloadCanExecute();
    }

    partial void OnVideoInfoChanged(VideoInfoViewModel? value)
    {
        if (_videoInfoSubscription is not null)
        {
            _videoInfoSubscription.PropertyChanged -= OnVideoInfoChildPropertyChanged;
        }

        _videoInfoSubscription = value;
        if (_videoInfoSubscription is not null)
        {
            _videoInfoSubscription.PropertyChanged += OnVideoInfoChildPropertyChanged;
        }

        OnPropertyChanged(nameof(HasVideoInfo));
        OnPropertyChanged(nameof(IsSubtitlePickerEnabled));
        RaiseDownloadCanExecute();
    }

    private void OnVideoInfoChildPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        RaiseDownloadCanExecute();

    partial void OnIsFetchingChanged(bool value)
    {
        RaiseFetchCanExecute();
        RaiseDownloadCanExecute();
        ((DelegateCommand)PasteFromClipboardCommand).RaiseCanExecuteChanged();
    }

    partial void OnIsDownloadingChanged(bool value)
    {
        RaiseFetchCanExecute();
        RaiseDownloadCanExecute();
        ((DelegateCommand)CancelDownloadCommand).RaiseCanExecuteChanged();
        ((DelegateCommand)PasteFromClipboardCommand).RaiseCanExecuteChanged();
        ((DelegateCommand)BrowseOutputFolderCommand).RaiseCanExecuteChanged();
    }

    private bool CanDownload() =>
        VideoInfo is not null
        && IsOutputFolderValid
        && !IsDownloading
        && !IsFetching
        && (Options.DownloadVideo || Options.DownloadThumbnail || Options.DownloadSubtitles);

    private async Task InitializeAsync()
    {
        await _preferences.LoadAsync().ConfigureAwait(true);
        var folder = _preferences.LastOutputFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "YouTube Downloads");
            Directory.CreateDirectory(folder);
        }

        OutputFolder = folder ?? string.Empty;
        IsOutputFolderValid = Directory.Exists(OutputFolder.Trim());
        AppendLog("Ready. Paste a URL and fetch metadata (simulated).", false);
    }

    private void PasteFromClipboard()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                Url = System.Windows.Clipboard.GetText().Trim();
            }
        }
        catch (Exception ex)
        {
            AppendLog("Clipboard: " + ex.Message, true);
        }
    }

    private void BrowseFolder()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose download folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(OutputFolder.Trim()) ? OutputFolder.Trim() : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            OutputFolder = dlg.SelectedPath;
            _ = SaveFolderPreferenceAsync();
        }
    }

    private async Task SaveFolderPreferenceAsync()
    {
        try
        {
            await _preferences.SaveLastOutputFolderAsync(OutputFolder.Trim()).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Could not save folder preference: " + ex.Message, true);
        }
    }

    private async Task FetchMetadataAsync()
    {
        if (!IsUrlValid)
        {
            return;
        }

        VideoInfo = null;
        IsFetching = true;
        IsProgressIndeterminate = true;
        StatusMessage = "Fetching video information…";
        ProgressPercent = 0;
        ActivityLog.Clear();

        try
        {
            var dto = await _metadata.FetchMetadataAsync(Url.Trim(), CancellationToken.None).ConfigureAwait(true);
            var vm = MapToVideoInfo(dto);
            VideoInfo = vm;
            await LoadThumbnailAsync(vm, dto.ThumbnailUrl).ConfigureAwait(true);
            StatusMessage = "Metadata loaded. Choose options and download.";
            AppendLog($"Loaded: {dto.Title}", false);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not fetch metadata.";
            AppendLog(ex.Message, true);
        }
        finally
        {
            IsFetching = false;
            IsProgressIndeterminate = false;
            ProgressPercent = 0;
            RaiseFetchCanExecute();
            RaiseDownloadCanExecute();
        }
    }

    private static VideoInfoViewModel MapToVideoInfo(VideoMetadataDto dto)
    {
        var vm = new VideoInfoViewModel
        {
            VideoId = dto.VideoId,
            Title = dto.Title,
            ChannelName = dto.ChannelName,
            DurationDisplay = dto.DurationDisplay
        };

        vm.Formats.Clear();
        foreach (var f in dto.Formats)
        {
            vm.Formats.Add(new VideoFormatDisplay(f.Id, f.Label));
        }

        vm.SelectedFormat = vm.Formats.FirstOrDefault();

        vm.SubtitleLanguages.Clear();
        foreach (var lang in dto.SubtitleLanguages)
        {
            vm.SubtitleLanguages.Add(lang);
        }

        vm.SelectedSubtitleLanguage = vm.SubtitleLanguages.FirstOrDefault();
        return vm;
    }

    private async Task LoadThumbnailAsync(VideoInfoViewModel target, string thumbnailUrl)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(new Uri(thumbnailUrl)).ConfigureAwait(false);

            await _dispatcher.InvokeAsync(() =>
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                target.Thumbnail = bmp;
            });
        }
        catch (Exception ex)
        {
            AppendLog("Thumbnail: " + ex.Message, true);
        }
    }

    private async Task DownloadAsync()
    {
        if (VideoInfo?.SelectedFormat is null || !IsOutputFolderValid)
        {
            return;
        }

        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();
        var token = _downloadCts.Token;

        IsDownloading = true;
        ProgressPercent = 0;
        StatusMessage = "Downloading…";
        AppendLog("Starting download (simulated)…", false);

        try
        {
            var request = new DownloadRequest(
                Url.Trim(),
                OutputFolder.Trim(),
                VideoInfo.SelectedFormat.Id,
                Options.SelectedVideoContainer,
                Options.SelectedQuality,
                Options.DownloadVideo,
                Options.DownloadThumbnail,
                Options.DownloadSubtitles,
                VideoInfo.SelectedSubtitleLanguage);

            var progress = new Progress<DownloadProgressUpdate>(u =>
            {
                _dispatcher.Invoke(() =>
                {
                    ProgressPercent = Math.Clamp(u.Fraction * 100.0, 0, 100);
                    StatusMessage = u.StepMessage;
                });
            });

            await _downloader.DownloadAsync(request, progress, token).ConfigureAwait(true);

            StatusMessage = "All done.";
            AppendLog("Download completed successfully.", false);
            await SaveFolderPreferenceAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
            AppendLog("Download cancelled.", true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Download failed.";
            AppendLog(ex.Message, true);
        }
        finally
        {
            IsDownloading = false;
            ProgressPercent = 0;
            _downloadCts?.Dispose();
            _downloadCts = null;
            RaiseDownloadCanExecute();
            ((DelegateCommand)CancelDownloadCommand).RaiseCanExecuteChanged();
        }
    }

    private void CancelDownload() => _downloadCts?.Cancel();

    private void AppendLog(string message, bool isError)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var line = new ConsoleLogLine(DateTime.Now, message, isError);
        void Add()
        {
            while (ActivityLog.Count > MaxLogLines)
            {
                ActivityLog.RemoveAt(0);
            }

            ActivityLog.Add(line);
        }

        if (_dispatcher.CheckAccess())
        {
            Add();
        }
        else
        {
            _dispatcher.Invoke(Add);
        }
    }
}
