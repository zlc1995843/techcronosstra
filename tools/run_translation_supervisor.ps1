$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$deadline = (Get-Date).Date.AddHours(9)
if ($deadline -le (Get-Date)) {
    $deadline = $deadline.AddDays(1)
}
while ((Get-Date) -lt $deadline) {
    & python (Join-Path $PSScriptRoot 'translate_scheduler.py') '--until' '09:00'
    if ((Get-Date) -ge $deadline) { break }
    Start-Sleep -Seconds 5
}
