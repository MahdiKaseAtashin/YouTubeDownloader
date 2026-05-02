using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Yt.Client.Converters;

/// <summary>WinUI has no DataTrigger; map stderr lines to a distinct foreground brush.</summary>
public sealed class StdErrToForegroundBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        var isErr = value is true;
        if (isErr)
        {
            return new SolidColorBrush(Color.FromArgb(255, 255, 92, 92));
        }

        var dark = Microsoft.UI.Xaml.Application.Current?.RequestedTheme == ApplicationTheme.Dark;
        return new SolidColorBrush(
            dark ? Color.FromArgb(255, 243, 243, 243) : Color.FromArgb(255, 31, 31, 31));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException();
}
