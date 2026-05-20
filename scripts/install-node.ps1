param(
    [string]$ToolsDir = "src/App.WinUI/tools",
    [string]$NodeVersion = "v22.15.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $root $ToolsDir
$nodePath = Join-Path $targetDir "node.exe"

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$url = "https://nodejs.org/dist/$NodeVersion/node-$NodeVersion-win-x64.zip"
$zipPath = Join-Path $env:TEMP "node-$NodeVersion-win-x64.zip"
$extractDir = Join-Path $env:TEMP "node-$NodeVersion-win-x64"

Write-Host "Downloading Node.js from: $url"
Invoke-WebRequest -Uri $url -OutFile $zipPath

if (Test-Path -LiteralPath $extractDir) {
    Remove-Item -LiteralPath $extractDir -Recurse -Force
}

Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force

$nodeExe = Get-ChildItem -LiteralPath $extractDir -Recurse -Filter "node.exe" |
    Select-Object -First 1

if (-not $nodeExe) {
    throw "node.exe not found inside downloaded archive."
}

Copy-Item -LiteralPath $nodeExe.FullName -Destination $nodePath -Force

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Node.js installed at: $nodePath"
