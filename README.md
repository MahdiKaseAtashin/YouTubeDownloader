# YouTube Downloader

WinUI 3 desktop app (.NET 8) for **YouTube Downloader**: paste a URL, fetch metadata (simulated today), pick video / thumbnail / subtitles, choose folder, and download. Replace `IVideoMetadataService` / `IVideoDownloadService` in `App.Infrastructure` with real `yt-dlp` or API calls when ready.

Architecture: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Requirements

- Windows 10 version 1809 or later (x64), with WebView2 / Windows App Runtime prerequisites as needed for WinUI
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (optional, for the installer)

## Build & run

```powershell
cd D:\Projects\windows\YouTubeDownloader
dotnet restore
dotnet build .\YouTubeDownloader.sln -c Release
dotnet run --project .\src\App.WinUI\App.WinUI.csproj -c Release
```

## Tests

```powershell
dotnet test .\YouTubeDownloader.sln -c Release
```

## Portable release (default: self-contained folder)

WinUI needs packaged resources. This repo sets **`EnableMsixTooling`** so `dotnet publish` produces **`resources.pri`** and a working UI without Visual Studio.

**Unpackaged WinUI + `PublishSingleFile` is not reliable** (startup may fail with COM `0x80040111` at `Application.Start`). The default script publishes a **self-contained folder**: run `YouTubeDownloader.exe` from that folder and keep all files beside it (xcopy / zip the whole directory).

Install bundled tools first (required for a complete release — yt-dlp, ffmpeg, ffprobe, node):

```powershell
.\scripts\install-tools.ps1
.\scripts\publish-release.ps1
.\scripts\build-installer.ps1
```

`publish-release.ps1` auto-downloads any missing tools before publishing.

Output:

- **`artifacts\portable\`** — `YouTubeDownloader.exe`, `yt-dlp.exe`, `ffmpeg.exe`, `ffprobe.exe`, `node.exe`, `resources.pri`, and runtime files.
- **`artifacts\YouTubeDownloader-portable-win-x64.zip`** — zip of the whole portable folder.
- **`artifacts\installer\YouTubeDownloader-Setup-1.0.4.exe`** — Windows installer (bundles everything; no separate tool installs needed).

Optional (not recommended): single-file exe — `.\scripts\publish-release.ps1 -SingleFile`

Skip the zip: `.\scripts\publish-release.ps1 -SkipZip`

## Installer (Inno Setup)

The installer copies the full portable folder into `{app}` — users get the app plus yt-dlp, ffmpeg, and Node.js in one setup.

1. Run `.\scripts\install-tools.ps1` and `.\scripts\publish-release.ps1`.
2. Run `.\scripts\build-installer.ps1` (requires [Inno Setup 6](https://jrsoftware.org/isinfo.php)), or open `installer\YouTubeDownloader.iss` and compile manually.
3. Output: `artifacts\installer\YouTubeDownloader-Setup-1.0.4.exe`.

## Data locations

App data: `%LocalAppData%\YouTubeDownloader\` (registry JSON, JSONL history, Serilog logs).

## Example scripts

See `examples\scripts\` (legacy samples from the earlier script-runner prototype).
