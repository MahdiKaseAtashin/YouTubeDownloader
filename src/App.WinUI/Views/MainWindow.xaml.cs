using System.IO;
using Microsoft.UI.Xaml;

namespace Yt.Client.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TrySetTaskbarAndTitleIcon();
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
}
