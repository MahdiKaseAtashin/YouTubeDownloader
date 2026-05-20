param(
    [string]$ToolsDir = "src/App.WinUI/tools"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $root $ToolsDir
$ffmpegPath = Join-Path $targetDir "ffmpeg.exe"
$ffprobePath = Join-Path $targetDir "ffprobe.exe"

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
$zipPath = Join-Path $env:TEMP "ffmpeg-master-latest-win64-gpl.zip"
$extractDir = Join-Path $env:TEMP "ffmpeg-master-latest-win64-gpl"

Write-Host "Downloading ffmpeg from: $url"
Invoke-WebRequest -Uri $url -OutFile $zipPath

if (Test-Path -LiteralPath $extractDir) {
    Remove-Item -LiteralPath $extractDir -Recurse -Force
}

Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force

$binDir = Get-ChildItem -LiteralPath $extractDir -Directory -Recurse |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "ffmpeg.exe") } |
    Select-Object -First 1

if (-not $binDir) {
    throw "ffmpeg.exe not found inside downloaded archive."
}

Copy-Item -LiteralPath (Join-Path $binDir.FullName "ffmpeg.exe") -Destination $ffmpegPath -Force
Copy-Item -LiteralPath (Join-Path $binDir.FullName "ffprobe.exe") -Destination $ffprobePath -Force

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "ffmpeg installed at: $ffmpegPath"
Write-Host "ffprobe installed at: $ffprobePath"
