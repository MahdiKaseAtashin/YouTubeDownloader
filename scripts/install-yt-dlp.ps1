param(
    [string]$Destination = "src/App.WinUI/tools/yt-dlp.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root $Destination
$targetDir = Split-Path -Parent $target

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
Write-Host "Downloading yt-dlp from: $url"
Invoke-WebRequest -Uri $url -OutFile $target

Write-Host "yt-dlp installed at: $target"
