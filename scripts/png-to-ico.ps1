param(
    [Parameter(Mandatory = $true)][string]$PngPath,
    [Parameter(Mandatory = $true)][string]$IcoPath
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile($PngPath)
try {
    $icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
    try {
        $fs = [System.IO.File]::OpenWrite($IcoPath)
        try {
            $icon.Save($fs)
        }
        finally {
            $fs.Dispose()
        }
    }
    finally {
        $icon.Dispose()
    }
}
finally {
    $bmp.Dispose()
}
