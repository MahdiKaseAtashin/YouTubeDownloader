param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRelative = "artifacts/portable",
    # WinUI 3 unpackaged + PublishSingleFile is a known-broken combo (COM 0x80040111 at Application.Start). Default is folder layout.
    [switch]$SingleFile,
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src/App.WinUI/App.WinUI.csproj"
$out = Join-Path $root $OutputRelative
$bundledYtDlp = Join-Path $root "src/App.WinUI/tools/yt-dlp.exe"
$bundledFfmpeg = Join-Path $root "src/App.WinUI/tools/ffmpeg.exe"
$bundledNode = Join-Path $root "src/App.WinUI/tools/node.exe"

function Get-VsMsBuildExe {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path -LiteralPath $vswhere)) {
        return $null
    }

    $candidates = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null
    if (-not $candidates) {
        $candidates = & $vswhere -latest -products * -find "MSBuild\**\Bin\MSBuild.exe" 2>$null
    }
    if ($candidates -is [string]) {
        return $candidates
    }

    return @($candidates)[0]
}

function Write-PortableReadme {
    param([string]$DestinationDir, [string]$Layout)
    $readme = Join-Path $DestinationDir "README-PORTABLE.txt"
    $builtWith = if ($Layout -eq "single-file") {
        "PublishSingleFile (experimental for WinUI unpackaged; may fail with COM errors on some PCs)."
    }
    else {
        "Self-contained folder publish: run YouTubeDownloader.exe from this directory (do not delete companion DLLs)."
    }
    $extra = if ($Layout -eq "single-file") {
        "`r`n`r`nWarning: Microsoft does not fully support unpackaged WinUI + single-file. Prefer the default folder publish if this build fails."
    }
    else {
        "`r`n`r`nProgram.cs sets MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY before WinUI starts (self-contained runtime)."
    }
    $body = @"
YouTube Downloader - self-contained portable build (Windows x64).

Layout: $Layout

Run: double-click YouTubeDownloader.exe (from this folder).

Requirements: Windows 10 version 1903 (build 18362) or later. Install the x64 Visual C++ Redistributable if the app fails to start: https://aka.ms/vs/17/release/vc_redist.x64.exe

Data and logs: %LOCALAPPDATA%\YouTubeDownloader\

Built with: $builtWith$extra
"@
    Set-Content -LiteralPath $readme -Value $body -Encoding UTF8
}

New-Item -ItemType Directory -Force -Path $out | Out-Null
if (-not $SkipZip) {
    $zipPath = Join-Path $root "artifacts/YouTubeDownloader-portable-win-x64.zip"
}

$msbuild = Get-VsMsBuildExe

if ($msbuild) {
    Write-Host "Using Visual Studio MSBuild: $msbuild"
}

if (-not (Test-Path -LiteralPath $bundledYtDlp)) {
    Write-Warning "Bundled yt-dlp not found at: $bundledYtDlp"
    Write-Warning "Run scripts/install-yt-dlp.ps1 to include yt-dlp in setup."
}

if (-not (Test-Path -LiteralPath $bundledFfmpeg)) {
    Write-Warning "Bundled ffmpeg not found at: $bundledFfmpeg"
    Write-Warning "Run scripts/install-ffmpeg.ps1 to include ffmpeg in setup."
}

if (-not (Test-Path -LiteralPath $bundledNode)) {
    Write-Warning "Bundled Node.js not found at: $bundledNode"
    Write-Warning "Run scripts/install-node.ps1 for signed-in YouTube downloads."
}

if ($SingleFile) {
    Write-Warning "PublishSingleFile with unpackaged WinUI is unreliable (e.g. COM 0x80040111). Prefer default folder publish."
    Write-Host "Publishing single-file -> $out"
    $publishArgs = @(
        "/restore",
        "/t:Publish",
        "/p:Configuration=$Configuration",
        "/p:Platform=x64",
        "/p:RuntimeIdentifier=$Runtime",
        "/p:SelfContained=true",
        "/p:PublishSingleFile=true",
        "/p:IncludeAllContentForSelfExtract=true",
        "/p:WindowsAppSDKSelfContained=true",
        "/p:DebugType=None",
        "/p:DebugSymbols=false",
        "/p:PublishDir=$out\"
    )
    if ($msbuild) {
        & $msbuild $proj @publishArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    else {
        dotnet publish $proj -c $Configuration -p:Platform=x64 -r $Runtime --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeAllContentForSelfExtract=true `
            -p:WindowsAppSDKSelfContained=true `
            -p:DebugType=None -p:DebugSymbols=false `
            -o $out
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    Write-PortableReadme $out "single-file"
}
else {
    Write-Host "Publishing self-contained folder (recommended portable layout) -> $out"
    $publishArgs = @(
        "/restore",
        "/t:Publish",
        "/p:Configuration=$Configuration",
        "/p:Platform=x64",
        "/p:RuntimeIdentifier=$Runtime",
        "/p:SelfContained=true",
        "/p:PublishSingleFile=false",
        "/p:IncludeNativeLibrariesForSelfExtract=true",
        "/p:WindowsAppSDKSelfContained=true",
        "/p:DebugType=None",
        "/p:DebugSymbols=false",
        "/p:PublishDir=$out\"
    )
    if ($msbuild) {
        & $msbuild $proj @publishArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    else {
        dotnet publish $proj -c $Configuration -p:Platform=x64 -r $Runtime --self-contained true `
            -p:PublishSingleFile=false `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:WindowsAppSDKSelfContained=true `
            -p:DebugType=None -p:DebugSymbols=false `
            -o $out
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    Write-PortableReadme $out "folder"
}

Get-ChildItem -LiteralPath $out -Filter "*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host ""
Write-Host "Done. Main executable:"
Write-Host "  $(Join-Path $out 'YouTubeDownloader.exe')"
if (Test-Path -LiteralPath (Join-Path $out 'yt-dlp.exe')) {
    Write-Host "  $(Join-Path $out 'yt-dlp.exe')"
}
Write-Host ""

if (-not $SkipZip) {
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zipPath -Force
    Write-Host "Zip (portable folder contents): $zipPath"
}
