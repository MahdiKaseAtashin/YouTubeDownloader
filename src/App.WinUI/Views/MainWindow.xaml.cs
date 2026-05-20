using System.IO;
using App.Application.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Yt.Client.Services;

namespace Yt.Client.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = $"YouTube Downloader v{AppVersionInfo.Display}";
        TrySetTaskbarAndTitleIcon();
        RootGrid.Loaded += (_, _) => ApplyActionIcons();
    }

    private void TrySetTaskbarAndTitleIcon()
    {
        var dir = AppContext.BaseDirectory;
        var png = Path.Combine(dir, "Assets", "AppLogo.png");
        var ico = Path.Combine(dir, "Assets", "AppIcon.ico");
        foreach (var path in new[] { png, ico })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                AppWindow.SetIcon(path);
                return;
            }
            catch
            {
                // Try next format (some builds prefer .ico).
            }
        }
    }

    private void ApplyActionIcons()
    {
        var iconSize = IconAssetCatalog.GetSizeForDpi(Content.XamlRoot?.RasterizationScale ?? 1.0, 24);
        var theme = RootGrid.ActualTheme;

        ApplyIconToButton(PasteFromClipboardButton, IconAssetCatalog.Clipboard, "Paste from clipboard", iconSize, theme);
        ApplyIconToButton(FetchInfoButton, IconAssetCatalog.Fetch, "Fetch info", iconSize, theme);
        ApplyIconToButton(BrowseOutputFolderButton, IconAssetCatalog.Folder, "Browse...", iconSize, theme);
        ApplyIconToButton(DownloadButton, IconAssetCatalog.Download, "Download", iconSize, theme);
        ApplyIconToButton(CancelDownloadButton, IconAssetCatalog.Cancel, "Cancel", iconSize, theme);
        ApplyIconToButton(ClearLogButton, IconAssetCatalog.Clear, "Clear log", iconSize, theme);
    }

    private static void ApplyIconToButton(Button button, string iconKey, string label, int size, ElementTheme theme)
    {
        var diskPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Icons",
            "generated",
            theme == ElementTheme.Dark ? "dark" : "light",
            size.ToString(),
            $"{iconKey}.png");

        if (!File.Exists(diskPath))
        {
            button.Content = label;
            return;
        }

        var image = new Image
        {
            Source = new BitmapImage(new Uri(IconAssetCatalog.BuildAssetUri(iconKey, size, theme == ElementTheme.Dark ? "dark" : "light"))),
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center
        };

        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center
        };

        button.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { image, text }
        };
    }
}
