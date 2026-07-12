$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$deadline = (Get-Date).Date.AddDays(1).AddHours(9)
while ((Get-Date) -lt $deadline) {
    & python (Join-Path $PSScriptRoot 'translate_scheduler.py') '--until' '09:00'
    if ((Get-Date) -ge $deadline) { break }
    Start-Sleep -Seconds 5
}
