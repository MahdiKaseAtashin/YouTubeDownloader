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
    public YouTubeAuthViewModel Auth { get; }
    public ObservableCollection<ConsoleLogLine> ActivityLog { get; } = new();

    public string ActivityLogText => string.Join(Environment.NewLine, ActivityLog.Select(l => l.FormattedLine));

    public ICommand LoadedCommand { get; }
    public ICommand FetchInfoCommand { get; }
    public ICommand PasteFromClipboardCommand { get; }
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand BatchDownloadCommand { get; }
    public ICommand CancelDownloadCommand { get; }
    public ICommand ClearLogCommand { get; }

    public MainViewModel(
        IVideoMetadataService metadata,
        IVideoDownloadService downloader,
        IUserPreferencesStore preferences,
        IBrowserProfileDiscovery browserDiscovery,
        IYouTubeSessionValidator sessionValidator)
    {
        _metadata = metadata;
        _downloader = downloader;
        _preferences = preferences;
        _dispatcher = System.Windows.Application.Current!.Dispatcher;
        Auth = new YouTubeAuthViewModel(browserDiscovery, sessionValidator, preferences);

        LoadedCommand = new AsyncRelayCommand(InitializeAsync, () => true);
        FetchInfoCommand = new AsyncRelayCommand(FetchMetadataAsync, () => IsUrlValid && !IsFetching && !IsDownloading);
        PasteFromClipboardCommand = new DelegateCommand(_ => PasteFromClipboard(), _ => !IsFetching && !IsDownloading);
        BrowseOutputFolderCommand = new DelegateCommand(_ => BrowseFolder(), _ => !IsDownloading);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, CanDownload);
        BatchDownloadCommand = new AsyncRelayCommand(BatchDownloadAsync, CanBatchDownload);
        CancelDownloadCommand = new DelegateCommand(_ => CancelDownload(), _ => IsDownloading);
        ClearLogCommand = new DelegateCommand(_ =>
        {
            ActivityLog.Clear();
            OnPropertyChanged(nameof(ActivityLogText));
        }, _ => true);

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

    private void RaiseDownloadCanExecute()
    {
        ((AsyncRelayCommand)DownloadCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)BatchDownloadCommand).RaiseCanExecuteChanged();
    }

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _batchUrlsText = string.Empty;

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

    public int BatchLinkCount => YoutubeUrlParser.ParseLines(BatchUrlsText).Count;

    public bool HasBatchLinks => BatchLinkCount > 0;

    public string BatchLinkCountLabel => $"{BatchLinkCount} valid link(s) detected";

    public string AppVersion { get; } = AppVersionInfo.Display;

    public string AppSubtitle =>
        $"v{AppVersion} · Paste a link → Fetch info → Choose what to save → Download";

    public string WindowTitle => $"YouTube Downloader v{AppVersion}";

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

    partial void OnBatchUrlsTextChanged(string value)
    {
        OnPropertyChanged(nameof(BatchLinkCount));
        OnPropertyChanged(nameof(BatchLinkCountLabel));
        OnPropertyChanged(nameof(HasBatchLinks));
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

    private bool CanBatchDownload() =>
        HasBatchLinks
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
        await Auth.InitializeAsync().ConfigureAwait(true);
        AppendLog("Ready. Paste a URL and fetch metadata.", false);
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
            await Auth.SavePreferencesAsync().ConfigureAwait(true);
            var dto = await _metadata.FetchMetadataAsync(
                Url.Trim(),
                Auth.BuildSettings(),
                CancellationToken.None).ConfigureAwait(true);
            var vm = MapToVideoInfo(dto);
            VideoInfo = vm;
            await LoadThumbnailAsync(vm, dto.ThumbnailUrl).ConfigureAwait(true);
            StatusMessage = "Metadata loaded. Choose options and download.";
            AppendLog($"Loaded: {dto.Title}", false);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not fetch metadata.";
            AppendLog(GetFriendlyError(ex.Message), true);
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
        AppendLog("Starting download…", false);

        try
        {
            await Auth.SavePreferencesAsync().ConfigureAwait(true);

            var request = CreateDownloadRequest(
                Url.Trim(),
                OutputFolder.Trim(),
                VideoInfo.SelectedFormat.Id,
                VideoInfo.SelectedSubtitleLanguage);

            await RunDownloadAsync(request, token, logPrefix: null, overallFractionOffset: 0, overallFractionScale: 1)
                .ConfigureAwait(true);

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
            LogException(ex);
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

    private async Task BatchDownloadAsync()
    {
        var urls = YoutubeUrlParser.ParseLines(BatchUrlsText);
        if (urls.Count == 0 || !IsOutputFolderValid)
        {
            return;
        }

        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();
        var token = _downloadCts.Token;

        IsDownloading = true;
        ProgressPercent = 0;
        StatusMessage = $"Batch download: 0/{urls.Count}";
        AppendLog($"Starting batch download ({urls.Count} links)…", false);

        var parentFolder = OutputFolder.Trim();
        var succeeded = 0;
        var failed = 0;

        try
        {
            await Auth.SavePreferencesAsync().ConfigureAwait(true);

            for (var i = 0; i < urls.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var index = i + 1;
                var prefix = $"[{index}/{urls.Count}]";

                AppendLog($"{prefix} URL: {urls[i]}", false);

                try
                {
                    StatusMessage = $"{prefix} Fetching metadata…";
                    var dto = await _metadata.FetchMetadataAsync(
                        urls[i],
                        Auth.BuildSettings(),
                        token).ConfigureAwait(true);

                    var folderName = BatchDownloadFolderNamer.CreateUnique(
                        parentFolder,
                        index,
                        dto.Title,
                        dto.VideoId);
                    var itemFolder = Path.Combine(parentFolder, folderName);
                    Directory.CreateDirectory(itemFolder);

                    AppendLog($"{prefix} Folder: {itemFolder}", false);
                    AppendLog($"{prefix} Loaded: {dto.Title}", false);

                    var formatId = ResolveFormatId(dto);
                    var subtitleLanguage = dto.SubtitleLanguages.FirstOrDefault();
                    var request = CreateDownloadRequest(urls[i], itemFolder, formatId, subtitleLanguage);

                    StatusMessage = $"{prefix} Downloading…";
                    await RunDownloadAsync(
                            request,
                            token,
                            logPrefix: prefix,
                            overallFractionOffset: i,
                            overallFractionScale: urls.Count)
                        .ConfigureAwait(true);

                    succeeded++;
                    AppendLog($"{prefix} Completed.", false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    LogException(ex);
                    AppendLog($"{prefix} Failed.", true);
                }

                ProgressPercent = index * 100.0 / urls.Count;
            }

            StatusMessage = failed == 0
                ? $"Batch complete ({succeeded}/{urls.Count})."
                : $"Batch finished: {succeeded} ok, {failed} failed.";
            AppendLog(StatusMessage, failed > 0);
            await SaveFolderPreferenceAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
            AppendLog("Batch download cancelled.", true);
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

    private DownloadRequest CreateDownloadRequest(
        string sourceUrl,
        string outputDirectory,
        string formatId,
        string? subtitleLanguage) =>
        new(
            sourceUrl,
            outputDirectory,
            formatId,
            Options.SelectedVideoContainer,
            Options.SelectedQuality,
            Options.DownloadVideo,
            Options.DownloadThumbnail,
            Options.DownloadSubtitles,
            subtitleLanguage,
            Auth.BuildSettings());

    private static string ResolveFormatId(VideoMetadataDto dto)
    {
        var best = dto.Formats.FirstOrDefault(f => f.Id.Equals("best", StringComparison.OrdinalIgnoreCase));
        return best?.Id ?? dto.Formats.FirstOrDefault()?.Id ?? "best";
    }

    private async Task RunDownloadAsync(
        DownloadRequest request,
        CancellationToken token,
        string? logPrefix,
        double overallFractionOffset,
        double overallFractionScale)
    {
        var progress = new Progress<DownloadProgressUpdate>(u =>
        {
            _dispatcher.Invoke(() =>
            {
                var itemFraction = overallFractionScale <= 0 ? u.Fraction : u.Fraction / overallFractionScale;
                ProgressPercent = Math.Clamp((overallFractionOffset + itemFraction) * 100.0, 0, 100);
                StatusMessage = logPrefix is null ? u.StepMessage : $"{logPrefix} {u.StepMessage}";
                if (ShouldLogProgressLine(u))
                {
                    var line = logPrefix is null ? u.StepMessage : $"{logPrefix} {u.StepMessage}";
                    AppendLog(line, u.IsStdErr);
                }
            });
        });

        await _downloader.DownloadAsync(request, progress, token).ConfigureAwait(true);
    }

    private void CancelDownload() => _downloadCts?.Cancel();

    private static bool ShouldLogProgressLine(DownloadProgressUpdate update)
    {
        if (update.IsStdErr)
        {
            return true;
        }

        return update.StepMessage.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
            || update.StepMessage.Contains("WARNING", StringComparison.OrdinalIgnoreCase)
            || update.StepMessage.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    private void LogException(Exception ex)
    {
        AppendMultilineLog(GetFriendlyError(ex.Message), isError: true);
        if (ex.InnerException is not null && !string.IsNullOrWhiteSpace(ex.InnerException.Message))
        {
            AppendMultilineLog(ex.InnerException.Message, isError: true);
        }
    }

    private void AppendMultilineLog(string text, bool isError)
    {
        foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            AppendLog(line.Trim(), isError);
        }
    }

    private static string GetFriendlyError(string rawMessage)
    {
        if (rawMessage.Contains("Authentication is required", StringComparison.OrdinalIgnoreCase) ||
            rawMessage.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase))
        {
            return "YouTube sign-in is required or expired. Update authentication settings and retry.";
        }

        return rawMessage;
    }

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
            OnPropertyChanged(nameof(ActivityLogText));
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
