param(
    [string]$PortableDir = "artifacts/portable",
    [string]$IssFile = "installer/YouTubeDownloader.iss"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$portable = Join-Path $root $PortableDir
$iss = Join-Path $root $IssFile

if (-not (Test-Path -LiteralPath (Join-Path $portable "YouTubeDownloader.exe"))) {
    throw "Portable publish not found. Run scripts/publish-release.ps1 first."
}

foreach ($tool in @('yt-dlp.exe', 'ffmpeg.exe', 'ffprobe.exe', 'node.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $portable $tool))) {
        throw "Portable publish is missing required tool: $tool"
    }
}

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)

$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 (ISCC.exe) not found. Install from https://jrsoftware.org/isinfo.php then re-run this script."
}

Write-Host "Compiling installer with: $iscc"
& $iscc $iss
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Installer build complete. See artifacts/installer/"
