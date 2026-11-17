param(
    [Parameter(Mandatory)][string]$Candidate,
    [Parameter(Mandatory)][string]$Output,
    [ValidateSet('all','visuals','performance','regression')][string]$Suite = 'all'
)

$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FaultReviewWindow {
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
}
'@
$candidatePath = (Resolve-Path -LiteralPath $Candidate).Path
$outputPath = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$cases = @()
if ($Suite -in @('all','regression')) {
    $cases += @(
        @{ Name = 'regression-1080'; Width = 1920; Height = 1080; Flag = '' },
        @{ Name = 'regression-720'; Width = 1280; Height = 720; Flag = '' },
        @{ Name = 'pump-1080'; Width = 1920; Height = 1080; Flag = '-workshop-check-pump' },
        @{ Name = 'tank-1080'; Width = 1920; Height = 1080; Flag = '-workshop-check-tank' },
        @{ Name = 'platform-1080'; Width = 1920; Height = 1080; Flag = 'platform' }
    )
}
if ($Suite -in @('all','visuals')) {
    $cases += @(
        @{ Name = 'visuals-1080'; Width = 1920; Height = 1080; Flag = '-workshop-check-visuals' },
        @{ Name = 'visuals-720'; Width = 1280; Height = 720; Flag = '-workshop-check-visuals' }
    )
}
if ($Suite -in @('all','performance')) {
    $cases += @(
        @{ Name = 'performance-1080'; Width = 1920; Height = 1080; Flag = '-workshop-check-performance' },
        @{ Name = 'performance-720'; Width = 1280; Height = 720; Flag = '-workshop-check-performance' }
    )
}
foreach ($case in $cases) {
    $casePath = Join-Path $outputPath $case.Name
    if (Test-Path -LiteralPath $casePath) { throw "Evidence already exists: $casePath" }
    New-Item -ItemType Directory -Path $casePath | Out-Null
    $flag = if ($case.Flag -eq 'platform') { '-workshop-ui-verify' } else { '-workshop-fault-upgrade-verify' }
    $arguments = @('-screen-width', $case.Width, '-screen-height', $case.Height, '-screen-fullscreen', '0',
        $flag, ('"{0}"' -f $casePath), '-logFile', ('"{0}"' -f (Join-Path $casePath 'player.log')))
    if ($case.Flag -and $case.Flag -ne 'platform') { $arguments += $case.Flag }
    $renderedReview = $case.Name -like 'visuals-*' -or $case.Name -like 'performance-*'
    $windowStyle = if ($renderedReview) { 'Normal' } else { 'Hidden' }
    $player = Start-Process -FilePath $candidatePath -ArgumentList $arguments -WindowStyle $windowStyle -PassThru
    Write-Output ("{0:o} START {1} PID={2}" -f (Get-Date), $case.Name, $player.Id)
    $startedAt = Get-Date
    $windowRestored = $false
    $peakWorking = 0L
    $memorySamples = 0
    while (!$player.WaitForExit(1000)) {
        $player.Refresh()
        $peakWorking = [Math]::Max($peakWorking, $player.WorkingSet64)
        $memorySamples++
        if ($renderedReview -and !$windowRestored) {
            if ($player.MainWindowHandle) {
                [FaultReviewWindow]::ShowWindow($player.MainWindowHandle, 9) | Out-Null
                $windowRestored = $true
                Write-Output ("{0:o} RENDER WINDOW {1}" -f (Get-Date), $case.Name)
            }
        }
        if ($renderedReview -and $windowRestored -and [FaultReviewWindow]::IsIconic($player.MainWindowHandle)) {
            [FaultReviewWindow]::ShowWindow($player.MainWindowHandle, 9) | Out-Null
        }
        if (((Get-Date) - $startedAt).TotalMinutes -gt 12) {
            Stop-Process -Id $player.Id
            throw "Timed out: $($case.Name)"
        }
    }
    [ordered]@{
        candidate = $candidatePath; case = $case.Name; processId = $player.Id
        startedAt = $startedAt.ToString('o'); endedAt = (Get-Date).ToString('o')
        width = $case.Width; height = $case.Height; exitCode = $player.ExitCode
        peakWorkingSetBytes = $peakWorking; memorySamples = $memorySamples
        method = 'External Windows process working set sampled each second; peak is a sampled lower bound.'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $casePath 'process-metrics.json') -Encoding utf8
    $report = Get-Content -LiteralPath (Join-Path $casePath 'verification.txt') -Raw
    Write-Output ("{0:o} END {1} exit={2} pass_lines={3}" -f (Get-Date), $case.Name,
        $player.ExitCode, ([regex]::Matches($report, '(?m)^PASS ')).Count)
    if ($player.ExitCode -ne 0 -or $report -notmatch 'Result: PASS') {
        Write-Output $report
        throw "Failed: $($case.Name)"
    }
    if ($case.Name -like 'visuals-*') {
        & python (Join-Path $PSScriptRoot 'build_review_sheet.py') $casePath
        if ($LASTEXITCODE -ne 0) { throw "Visual capture validation failed: $($case.Name)" }
    }
}
