using Microsoft.UI.Xaml;

namespace Yt.Client.Services;

/// <summary>
/// Switches light/dark by updating <see cref="FrameworkElement.RequestedTheme"/> so
/// <see cref="ThemeResource"/> brushes resolve from <c>ThemeDictionaries</c>.
/// </summary>
/// <remarks>
/// Avoid mutating SolidColorBrush.Color on brushes created from XAML resources; they can be read-only and unsafe to change in place.
/// </remarks>
public static class ThemeSwitcher
{
    public static void Apply(bool dark, FrameworkElement? themeRoot = null)
    {
        if (themeRoot is null)
        {
            return;
        }

        themeRoot.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
    }
}
