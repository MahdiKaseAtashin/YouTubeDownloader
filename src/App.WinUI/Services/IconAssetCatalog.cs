using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Yt.Client.Services;

/// <summary>
/// Centralized icon registry for theme and DPI-aware icon resolution.
/// Keep icon file references out of UI markup/code-behind to enforce consistency.
/// </summary>
public static class IconAssetCatalog
{
    private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<int> SupportedSizes = new[] { 16, 24, 32, 48, 64, 128 };

    public const string Brand = "brand";
    public const string Download = "download";
    public const string Folder = "folder";
    public const string Clipboard = "clipboard";
    public const string Fetch = "fetch";
    public const string Clear = "clear";
    public const string Cancel = "cancel";

    public static BitmapImage Get(string iconKey, int requestedSize = 24, ElementTheme theme = ElementTheme.Default)
    {
        var size = NormalizeSize(requestedSize);
        var themeSegment = ResolveThemeSegment(theme);
        var uri = BuildAssetUri(iconKey, size, themeSegment);

        return Cache.GetOrAdd(uri, static key => new BitmapImage(new Uri(key)));
    }

    public static string BuildAssetUri(string iconKey, int requestedSize, string themeSegment)
    {
        var size = NormalizeSize(requestedSize);
        return $"ms-appx:///Assets/Icons/generated/{themeSegment}/{size}/{iconKey}.png";
    }

    public static int GetSizeForDpi(double rasterizationScale, int baseSize = 24)
    {
        var scaled = (int)Math.Round(baseSize * rasterizationScale, MidpointRounding.AwayFromZero);
        return NormalizeSize(scaled);
    }

    private static string ResolveThemeSegment(ElementTheme theme) =>
        theme switch
        {
            ElementTheme.Dark => "dark",
            _ => "light"
        };

    private static int NormalizeSize(int requestedSize)
    {
        var best = SupportedSizes[0];
        var bestDistance = Math.Abs(requestedSize - best);
        for (var i = 1; i < SupportedSizes.Count; i++)
        {
            var candidate = SupportedSizes[i];
            var distance = Math.Abs(requestedSize - candidate);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }
}
