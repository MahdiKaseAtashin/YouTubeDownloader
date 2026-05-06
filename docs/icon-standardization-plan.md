# Icon Standardization Refactoring Plan

This plan standardizes icons across .NET desktop UI stacks (WPF, WinForms, .NET MAUI, WinUI) without touching application logic.

## Scope and Constraints

- UI/icon layer only.
- Transparent icon assets only.
- Consistent style and visual language.
- High-DPI support (100%, 125%, 150%, 200%).
- Light/dark theme compatibility.
- Backward-compatible migration path.

## Recommended Asset Structure

```text
/Assets/Icons
  /src
    download.svg
    folder.svg
    ...
  /generated
    /light
      /16/*.png
      /24/*.png
      /32/*.png
      /48/*.png
      /64/*.png
      /128/*.png
    /dark
      /16/*.png
      /24/*.png
      /32/*.png
      /48/*.png
      /64/*.png
      /128/*.png
  icon-manifest.json
```

## Format Guidance

- Source-of-truth: SVG.
- Runtime fallback: PNG (16, 24, 32, 48, 64, 128).
- Windows shell/app icon: ICO generated from approved PNG sizes.

## Visual Consistency Rules

- Single primary icon style (outline or filled).
- Fixed stroke scaling rule (for example, 1.5 at 24 base grid).
- Common corner radius and spacing rhythm.
- Shared color tokens, not ad hoc hex values.
- No icon background rectangles.

## Replacing Windows/System Icons

### WPF

- Replace direct `SystemIcons` and ad hoc glyph usage with `ResourceDictionary` icon keys.
- Use `DynamicResource` for theme adaptation.
- Keep icon lookup centralized via key names (`Icon.Download`, `Icon.Folder`, ...).

### WinForms

- Replace direct `SystemIcons` and default image list entries with app-owned icons.
- Use centralized `IIconProvider` and optionally DPI-aware `ImageList` groups.
- Keep tray/shortcut icons in ICO, UI icons from generated PNG assets.

### .NET MAUI

- Use shared resources and `AppThemeBinding` for icon variants.
- Keep per-platform overrides minimal and rooted in shared icon keys.
- Centralize icon lookup in one service/helper class.

## Enforcing Consistent Usage

- Introduce an icon registry (`icon-manifest.json` + strongly typed key constants).
- Ban direct icon file paths in feature UI code.
- Reference only registry keys in styles/templates/views.
- Add CI checks:
  - background transparency validation for PNGs,
  - required size set presence,
  - required light/dark variants,
  - orphaned assets not present in manifest.

## Backward Compatibility Strategy

- Keep legacy icon filenames during migration and map them to registry keys.
- Replace usage incrementally screen-by-screen.
- Keep fallback logic in the icon provider to prevent missing icon crashes.

## Optional Samples

### WPF Resource Usage

```xml
<Image Width="24"
       Height="24"
       Source="{DynamicResource Icon.Download}" />
```

### WinForms Provider Usage

```csharp
toolStripButtonDownload.Image = iconProvider.Get("download", 24, isDarkTheme, DeviceDpi / 96f);
```

### MAUI Theme-Aware Usage

```xml
<Image Source="{AppThemeBinding Light=download_light.png, Dark=download_dark.png}" />
```
