$ErrorActionPreference = 'Continue'

$root = Split-Path -Parent $PSScriptRoot
$input = Join-Path $root '.work\character_story_source.json'
$output = Join-Path $root 'translations\zh-Hans.json'
$glossary = 'C:\Users\曾罗畅\Downloads\铁扣对照\glossary.json'
$runtimeLog = Join-Path $root '.work\translation_supervisor_runtime.log'
$schedulerLog = Join-Path $root '.work\translation_scheduler_runtime.log'
$schedulerErr = Join-Path $root '.work\translation_scheduler_runtime.err.log'
$lockPath = Join-Path $root '.work\translation_supervisor.lock'

New-Item -ItemType Directory -Force -Path (Join-Path $root '.work') | Out-Null

# Prevent two supervisors from writing the same translation file concurrently.
if (Test-Path -LiteralPath $lockPath) {
    $lockPid = Get-Content -LiteralPath $lockPath -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($lockPid -and (Get-Process -Id ([int]$lockPid) -ErrorAction SilentlyContinue)) {
        Add-Content -LiteralPath $runtimeLog -Value "[$(Get-Date -Format s)] supervisor already running pid=$lockPid"
        exit 0
    }
}
$PID | Set-Content -LiteralPath $lockPath -Encoding UTF8

try {
    $deadline = (Get-Date).Date.AddHours(9)
    if ($deadline -le (Get-Date)) {
        $deadline = $deadline.AddDays(1)
    }
    Add-Content -LiteralPath $runtimeLog -Value "[$(Get-Date -Format s)] supervisor started pid=$PID deadline=$($deadline.ToString('s'))"

    $python = (Get-Command python).Source
    $scheduler = Join-Path $PSScriptRoot 'translate_scheduler.py'
    $arguments = @(
        $scheduler,
        '--input', $input,
        '--output', $output,
        '--until', '09:00',
        '--glossary', $glossary
    )
    $worker = Start-Process -FilePath $python -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $schedulerLog -RedirectStandardError $schedulerErr -PassThru
    Add-Content -LiteralPath $runtimeLog -Value "[$(Get-Date -Format s)] scheduler started pid=$($worker.Id)"

    function Sync-Translations {
        git -C $root add -- translations/zh-Hans.json
        git -C $root diff --cached --quiet
        if ($LASTEXITCODE -eq 0) {
            return
        }
        $message = "Update character story translations $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
        git -C $root commit -m $message | Out-File -FilePath $runtimeLog -Append -Encoding utf8
        if ($LASTEXITCODE -ne 0) {
            Add-Content -LiteralPath $runtimeLog -Value "[$(Get-Date -Format s)] commit failed exit=$LASTEXITCODE"
            return
        }
        git -C $root push origin main | Out-File -FilePath $runtimeLog -Append -Encoding utf8
        if ($LASTEXITCODE -eq 0) {
            Add-Content -LiteralPath $runtimeLog -Value "[$(Get-Date -Format s)] translations pushed"
        } else {
            Add-Content -LiteralPath $runtimeLog -Value "[$(Get-Date -Format s)] push failed exit=$LASTEXITCODE"
        }
    }

    while ((Get-Date) -lt $deadline -and (Get-Process -Id $worker.Id -ErrorAction SilentlyContinue)) {
        Sync-Translations
        Start-Sleep -Seconds 10
    }

    Sync-Translations
    $workerState = Get-Process -Id $worker.Id -ErrorAction SilentlyContinue
    if ($workerState) {
        Stop-Process -Id $worker.Id -Force -ErrorAction SilentlyContinue
        Add-Content -LiteralPath $runtimeLog -Value "[$(Get-Date -Format s)] scheduler stopped at deadline"
    } else {
        $exitCode = $worker.ExitCode
        Add-Content -LiteralPath $runtimeLog -Value "[$(Get-Date -Format s)] scheduler exited code=$exitCode"
    }
    Sync-Translations
} finally {
    Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue
    Add-Content -LiteralPath $runtimeLog -Value "[$(Get-Date -Format s)] supervisor stopped"
}
