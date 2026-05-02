param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

Write-Host "Hello from PowerShell"
if ($RemainingArgs -and $RemainingArgs.Count -gt 0) {
    Write-Host ("Arguments: " + ($RemainingArgs -join " "))
}

Start-Sleep -Seconds 1
Write-Host "Done."
