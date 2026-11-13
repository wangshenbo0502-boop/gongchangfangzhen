param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) `
        ('reports\cabinet-viewer-automation-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [int]$Width = 1920,
    [int]$Height = 1080,
    [string]$Executable = (Join-Path (Split-Path -Parent $PSScriptRoot) `
        'CabinetViewer\CabinetViewer.exe')
)
$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$Executable = [IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw "Cabinet viewer not found: $Executable"
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$statePath = Join-Path $OutputDirectory 'state.json'
$resultPath = Join-Path $OutputDirectory 'automation-result.txt'
$logPath = Join-Path $OutputDirectory 'Player.log'
Remove-Item -LiteralPath @($statePath, $resultPath) -Force -ErrorAction SilentlyContinue
$arguments = @(
    '-screen-width', "$Width", '-screen-height', "$Height", '-screen-fullscreen', '0',
    '-fanReviewOutput', ('"' + $OutputDirectory + '"'), '-fanReviewAutomation',
    '-logFile', ('"' + $logPath + '"')
)
$viewer = Start-Process -FilePath $Executable -ArgumentList $arguments -PassThru
try {
    $deadline = (Get-Date).AddMinutes(2)
    do {
        Start-Sleep -Milliseconds 250
        $viewer.Refresh()
        if ($viewer.HasExited) { throw 'Cabinet viewer exited during automation.' }
        if (Test-Path -LiteralPath $statePath) {
            $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
            if ($state.automationStatus -eq 'passed') { break }
            if ($state.automationStatus -eq 'failed') {
                throw "Cabinet viewer automation failed: $($state.automationError)"
            }
        }
    } while ((Get-Date) -lt $deadline)
    if (-not $state -or $state.automationStatus -ne 'passed') {
        throw 'Timed out waiting for cabinet viewer automation.'
    }
    $runtimeErrors = Select-String -LiteralPath $logPath `
        -Pattern 'Exception:|NullReferenceException|error CS[0-9]+' `
        -ErrorAction SilentlyContinue
    if ($runtimeErrors) {
        throw "Cabinet Player errors: $($runtimeErrors | Select-Object -First 3 | ForEach-Object Line)"
    }
    Get-Content -LiteralPath $resultPath -Raw
    [pscustomobject]@{
        executable = $Executable
        resolution = "$($state.width) x $($state.height)"
        automation = $state.automationStatus
        progress = $state.progress
        maxHomeError = $state.maxHomeError
        targetFrameRate = $state.targetFrameRate
        renderScale = $state.renderScale
        msaaSamples = $state.msaaSamples
        graphicsDevice = $state.graphicsDevice
        runtimeErrors = 0
    } | Format-List
}
finally {
    if ($viewer -and -not $viewer.HasExited) {
        $viewer.CloseMainWindow() | Out-Null
        if (-not $viewer.WaitForExit(5000)) {
            Stop-Process -Id $viewer.Id
        }
    }
}
