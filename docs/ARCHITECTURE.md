# Architecture (YouTube Downloader)

## UI flow

`MainViewModel` (in **App.WinUI**) drives **paste → fetch → preview → options → folder → download → logs**. Child VMs: `VideoInfoViewModel` (preview + format + subtitles list), `DownloadOptionsViewModel` (toggles and container/quality). UI is **WinUI 3** (`Microsoft.WindowsAppSDK`).

## Replace simulation with real downloads

| Port | Role | Current implementation |
|------|------|-------------------------|
| `IVideoMetadataService` | Resolve URL, title, thumb, formats, subtitle langs | `SimulatedVideoMetadataService` |
| `IVideoDownloadService` | Write files, report `DownloadProgressUpdate` | `SimulatedVideoDownloadService` |
| `IUserPreferencesStore` | Remember last output folder | `JsonUserPreferencesStore` |

Register your real services in `App.Infrastructure.DependencyInjection`.

## URL validation

`YoutubeUrlValidator` in **App.Application** (`IsValid`, `TryExtractVideoId`) — covered by unit tests.

## Persistence

- Preferences: `%LocalAppData%\YouTubeDownloader\preferences.json`
- Logs: Serilog under the same root as before (`logs\`).

Legacy script-runner infrastructure files may still exist under `src/App.Infrastructure` but are **not** registered in DI.
