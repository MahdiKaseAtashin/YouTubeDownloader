param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $root "YouTubeDownloader.sln"

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

$msbuild = Get-VsMsBuildExe
$bin = Join-Path $root "src\App.WinUI\bin\x64\$Configuration\net8.0-windows10.0.19041.0"

if ($msbuild) {
    Write-Host "Using Visual Studio MSBuild: $msbuild"
    & $msbuild $sln /restore /t:Build /p:Configuration=$Configuration /p:Platform=x64 /v:minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
else {
    Write-Host "Using dotnet build (WinUI PRI is enabled via EnableMsixTooling in App.WinUI.csproj)"
    dotnet build $sln -c $Configuration -p:Platform=x64 -v minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Build succeeded. Run: $bin\YouTubeDownloader.exe"
Write-Host "For a single portable exe, run: scripts\publish-release.ps1"
