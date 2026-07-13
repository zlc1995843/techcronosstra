$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$input = Join-Path $root '.work\character_story_source.json'
$output = Join-Path $root 'translations\zh-Hans.json'
$glossary = 'C:\Users\曾罗畅\Downloads\铁扣对照\glossary.json'
$deadline = (Get-Date).Date.AddHours(9)
if ($deadline -le (Get-Date)) {
    $deadline = $deadline.AddDays(1)
}
while ((Get-Date) -lt $deadline) {
    & python (Join-Path $PSScriptRoot 'translate_scheduler.py') '--input' $input '--output' $output '--until' '09:00' '--glossary' $glossary
    $schedulerExitCode = $LASTEXITCODE
    git -C $root add -- translations/zh-Hans.json
    git -C $root diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        git -C $root commit -m "Update character story translations"
        git -C $root push origin main
    }
    if ($schedulerExitCode -ne 0) {
        Write-Error "Translation scheduler stopped with exit code $schedulerExitCode"
    }
    if ((Get-Date) -ge $deadline) { break }
    Start-Sleep -Seconds 5
}
