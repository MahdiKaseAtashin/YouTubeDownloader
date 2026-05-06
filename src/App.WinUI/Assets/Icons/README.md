# Icon System Standard

This folder defines a single icon pipeline for the desktop UI layer.

## Goals

- All icons must be transparent (no background fills in the canvas).
- One visual language only (shared stroke/fill/corner style).
- Theme-safe rendering for light and dark mode.
- Crisp high-DPI rendering at 100%, 125%, 150%, and 200%.

## Structure

- `src/`: editable master SVG files.
- `generated/light/{size}/`: generated light-theme PNG assets.
- `generated/dark/{size}/`: generated dark-theme PNG assets.
- `icon-manifest.json`: canonical icon keys and metadata used by UI.

## Supported Raster Sizes

- 16, 24, 32, 48, 64, 128

## Naming Convention

- Use lowercase kebab or simple lowercase names for file assets.
- Keep icon key and file name aligned (example: `download` => `download.svg` / `download.png`).

## Theme Policy

- Prefer a single vector geometry with theme-based color tokens.
- Keep dual generated folders (`light`, `dark`) for compatibility with controls that consume raster assets.

## Usage Policy (WinUI)

- Use resource keys (`Icon.*`) or `IconAssetCatalog`.
- Do not hardcode icon file paths directly in view XAML or code-behind.
- Avoid default/system glyphs unless intentionally mapped in the icon registry.

## Performance Policy

- Pre-generate PNGs; do not run heavy rasterization at runtime.
- Use cached `BitmapImage` instances from `IconAssetCatalog`.
