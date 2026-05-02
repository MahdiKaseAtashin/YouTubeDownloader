using System.Windows;

namespace App.UI.Services;

public static class ThemeSwitcher
{
    public static void Apply(bool dark)
    {
        var app = System.Windows.Application.Current;
        if (app is null || app.Resources.MergedDictionaries.Count == 0)
        {
            return;
        }

        var merged = app.Resources.MergedDictionaries;
        merged.Clear();
        var source = dark
            ? new Uri("Themes/FluentDark.xaml", UriKind.Relative)
            : new Uri("Themes/FluentLight.xaml", UriKind.Relative);
        merged.Add(new ResourceDictionary { Source = source });
    }
}
