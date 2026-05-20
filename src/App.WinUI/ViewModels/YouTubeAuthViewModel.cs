using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using App.Application.Dtos;
using App.Application.Ports;
using App.Application.Services;
using Yt.Client.Commands;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Yt.Client.ViewModels;

public sealed partial class YouTubeAuthViewModel : ObservableObject
{
    private readonly IBrowserProfileDiscovery _discovery;
    private readonly IYouTubeSessionValidator _validator;
    private readonly IUserPreferencesStore _preferences;
    private readonly Window _ownerWindow;
    private readonly DispatcherQueue _dispatcherQueue;

    public YouTubeAuthViewModel(
        IBrowserProfileDiscovery discovery,
        IYouTubeSessionValidator validator,
        IUserPreferencesStore preferences,
        DispatcherQueue dispatcherQueue,
        Window ownerWindow)
    {
        _discovery = discovery;
        _validator = validator;
        _preferences = preferences;
        _dispatcherQueue = dispatcherQueue;
        _ownerWindow = ownerWindow;

        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, () => !IsChecking);
        OpenYouTubeCommand = new AsyncRelayCommand(OpenYouTubeAsync, () => !IsChecking);
        BrowseCookieFileCommand = new AsyncRelayCommand(BrowseCookieFileAsync, () => !IsChecking);
    }

    public ObservableCollection<BrowserOption> Browsers { get; } = new();
    public ObservableCollection<BrowserProfileOption> Profiles { get; } = new();

    public ICommand TestConnectionCommand { get; }
    public ICommand OpenYouTubeCommand { get; }
    public ICommand BrowseCookieFileCommand { get; }

    [ObservableProperty]
    private bool _useSignIn = true;

    [ObservableProperty]
    private bool _isAdvancedExpanded;

    [ObservableProperty]
    private bool _useCookieFile;

    [ObservableProperty]
    private BrowserOption? _selectedBrowser;

    [ObservableProperty]
    private BrowserProfileOption? _selectedProfile;

    [ObservableProperty]
    private string _manualProfileOverride = string.Empty;

    [ObservableProperty]
    private string _cookieFilePath = string.Empty;

    [ObservableProperty]
    private YouTubeSignInState _signInState = YouTubeSignInState.Unknown;

    [ObservableProperty]
    private string _statusMessage = "Sign in to your browser, then click Test connection.";

    [ObservableProperty]
    private bool _isChecking;

    public bool ShowBrowserFields => UseSignIn && !UseCookieFile;

    public bool ShowCookieFields => UseSignIn && UseCookieFile;

    public string SignInStateDisplay => SignInState switch
    {
        YouTubeSignInState.SignedIn => "Signed in",
        YouTubeSignInState.NotSignedIn => "Not signed in",
        YouTubeSignInState.Expired => "Session expired",
        YouTubeSignInState.Checking => "Checking…",
        YouTubeSignInState.Disabled => "Sign-in off",
        YouTubeSignInState.Error => "Error",
        _ => "Not tested"
    };

    partial void OnUseSignInChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBrowserFields));
        OnPropertyChanged(nameof(ShowCookieFields));
        if (!value)
        {
            SignInState = YouTubeSignInState.Disabled;
            StatusMessage = "Sign-in is off. Only public videos will be used.";
        }
        else
        {
            SignInState = YouTubeSignInState.Unknown;
            StatusMessage = "Sign in to your browser, then click Test connection.";
        }
    }

    partial void OnUseCookieFileChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBrowserFields));
        OnPropertyChanged(nameof(ShowCookieFields));
    }

    partial void OnSelectedBrowserChanged(BrowserOption? value)
    {
        if (value is not null)
        {
            ReloadProfilesForBrowser(value.Id);
        }
    }

    partial void OnIsCheckingChanged(bool value)
    {
        ((AsyncRelayCommand)TestConnectionCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)OpenYouTubeCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)BrowseCookieFileCommand).RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(SignInStateDisplay));
    }

    partial void OnSignInStateChanged(YouTubeSignInState value) =>
        OnPropertyChanged(nameof(SignInStateDisplay));

    public Task InitializeAsync()
    {
        Browsers.Clear();
        foreach (var browser in _discovery.GetInstalledBrowsers())
        {
            Browsers.Add(browser);
        }

        YouTubeAuthSettingsMapper.ApplyToUi(
            _preferences.YouTubeAuthSettings,
            out var useSignIn,
            out var useCookieFile,
            out var cookiePath,
            out var browserId,
            out var profileId,
            out _);

        UseSignIn = useSignIn;
        UseCookieFile = useCookieFile;
        CookieFilePath = cookiePath;

        SelectedBrowser = Browsers.FirstOrDefault(b => b.Id == browserId)
            ?? Browsers.FirstOrDefault(b => b.Id == _discovery.GetDefaultBrowserId())
            ?? Browsers.FirstOrDefault();

        if (SelectedBrowser is not null)
        {
            ReloadProfilesForBrowser(SelectedBrowser.Id);
            SelectedProfile = Profiles.FirstOrDefault(p => p.ProfileId == profileId)
                ?? Profiles.FirstOrDefault();
        }

        if (!UseSignIn)
        {
            SignInState = YouTubeSignInState.Disabled;
            StatusMessage = "Sign-in is off. Only public videos will be used.";
        }

        return Task.CompletedTask;
    }

    public YouTubeAuthSettings BuildSettings()
    {
        var settings = YouTubeAuthSettingsMapper.Build(
            UseSignIn,
            UseCookieFile,
            CookieFilePath,
            SelectedBrowser?.Id,
            SelectedProfile?.ProfileId,
            ManualProfileOverride,
            SelectedProfile?.ProfileDirectoryPath);

        return YouTubeAuthSettingsEnricher.Enrich(settings, _discovery);
    }

    public async Task SavePreferencesAsync()
    {
        await _preferences.SaveYouTubeAuthSettingsAsync(BuildSettings()).ConfigureAwait(true);
    }

    private void ReloadProfilesForBrowser(string browserId)
    {
        var previousId = SelectedProfile?.ProfileId;
        Profiles.Clear();
        foreach (var profile in _discovery.GetProfiles(browserId))
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault(p => p.ProfileId == previousId)
            ?? Profiles.FirstOrDefault();
    }

    private async Task TestConnectionAsync()
    {
        IsChecking = true;
        SignInState = YouTubeSignInState.Checking;
        StatusMessage = "Testing your YouTube session…";

        try
        {
            await SavePreferencesAsync().ConfigureAwait(true);
            var result = await _validator.ValidateAsync(BuildSettings()).ConfigureAwait(true);
            ApplyValidationResult(result);
        }
        catch (Exception ex)
        {
            SignInState = YouTubeSignInState.Error;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsChecking = false;
        }
    }

    private void ApplyValidationResult(YouTubeSessionValidationResult result)
    {
        if (!UseSignIn)
        {
            SignInState = YouTubeSignInState.Disabled;
            StatusMessage = result.Message;
            return;
        }

        if (result.Success)
        {
            SignInState = YouTubeSignInState.SignedIn;
            StatusMessage = result.Message;
            return;
        }

        SignInState = result.IsAuthenticationRelated
            ? YouTubeSignInState.NotSignedIn
            : YouTubeSignInState.Error;
        StatusMessage = result.Message;
    }

    private static Task OpenYouTubeAsync()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.youtube.com",
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    private async Task BrowseCookieFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads
        };
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(_ownerWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            CookieFilePath = file.Path;
            UseCookieFile = true;
        }
    }
}
