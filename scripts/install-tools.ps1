param(
    [switch]$SkipYtDlp,
    [switch]$SkipFfmpeg,
    [switch]$SkipNode
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$installYtDlp = Join-Path $PSScriptRoot "install-yt-dlp.ps1"
$installFfmpeg = Join-Path $PSScriptRoot "install-ffmpeg.ps1"
$installNode = Join-Path $PSScriptRoot "install-node.ps1"

if (-not $SkipYtDlp) {
    & $installYtDlp
}

if (-not $SkipFfmpeg) {
    & $installFfmpeg
}

if (-not $SkipNode) {
    & $installNode
}

$toolsDir = Join-Path $root "src/App.WinUI/tools"
Write-Host ""
Write-Host "Bundled tools in: $toolsDir"
Get-ChildItem -LiteralPath $toolsDir -Filter "*.exe" | ForEach-Object {
    Write-Host ("  {0} ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB))
}
