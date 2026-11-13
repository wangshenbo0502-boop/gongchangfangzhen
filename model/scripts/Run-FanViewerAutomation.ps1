param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'reports\industrial-fan-player-20261112'),
    [int]$Width = 1280,
    [int]$Height = 720,
    [switch]$MotionOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'IndustrialFanViewer\IndustrialFanViewer.exe'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$statePath = Join-Path $OutputDirectory 'state.json'
$logPath = Join-Path $OutputDirectory 'Player.log'
Remove-Item -LiteralPath @($statePath, (Join-Path $OutputDirectory 'automation-result.txt')) `
    -Force -ErrorAction SilentlyContinue
$arguments = @(
    '-screen-width', "$Width", '-screen-height', "$Height", '-screen-fullscreen', '0',
    '-fanReviewOutput', ('"' + $OutputDirectory + '"'), '-fanReviewAutomation',
    '-logFile', ('"' + $logPath + '"')
)
if ($MotionOnly) { $arguments += '-fanMotionOnly' }
$viewer = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru
try {
    $deadline = (Get-Date).AddMinutes(2)
    do {
        Start-Sleep -Milliseconds 250
        $viewer.Refresh()
        if ($viewer.HasExited) { throw 'Viewer exited during automation.' }
        if (Test-Path -LiteralPath $statePath) {
            $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
            if ($state.automationStatus -eq 'passed') { break }
            if ($state.automationStatus -eq 'failed')
                { throw "Viewer automation failed: $($state.automationError)" }
        }
    } while ((Get-Date) -lt $deadline)
    if ($state.automationStatus -ne 'passed') { throw 'Timed out waiting for viewer automation.' }
    Start-Sleep -Seconds 1
    $runtimeErrors = Select-String -LiteralPath $logPath `
        -Pattern 'Exception:|NullReferenceException|error CS[0-9]+' -ErrorAction SilentlyContinue
    if ($runtimeErrors) { throw "Player errors: $($runtimeErrors | Select-Object -First 3 | ForEach-Object Line)" }
    $result = [ordered]@{
        date = (Get-Date -Format o)
        executable = $exe
        requestedResolution = "$Width x $Height"
        actualResolution = "$($state.width) x $($state.height)"
        automation = Get-Content -LiteralPath (Join-Path $OutputDirectory 'automation-result.txt') -Raw
        runtimeErrors = 0
        averageFps = $state.averageFps
        motionPerformance = $state.motionPerformance
        targetFrameRate = $state.targetFrameRate
        displayRefreshHz = $state.displayRefreshHz
        vSyncCount = $state.vSyncCount
        renderScale = $state.renderScale
        msaaSamples = $state.msaaSamples
        allocatedMemoryBytes = $state.allocatedMemoryBytes
        graphicsDevice = $state.graphicsDevice
        note = 'UI Button.onClick event handlers and runtime raycast/animation logic exercised; OS physical mouse click is not covered.'
    }
    $result | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $OutputDirectory 'interaction-verification.json') -Encoding UTF8
    $result | ConvertTo-Json -Depth 6
} finally {
    if ($viewer -and -not $viewer.HasExited) { $viewer.CloseMainWindow() | Out-Null }
}
