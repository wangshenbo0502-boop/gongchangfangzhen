param(
    [string]$UnityExecutable = 'D:\unity\Editor\Unity.exe',
    [string]$BlenderExecutable = 'E:\steam\steamapps\common\Blender\blender.exe',
    [switch]$BuildWindows
)
$ErrorActionPreference = 'Stop'
$modelDirectory = Split-Path -Parent $PSScriptRoot
$projectDirectory = Join-Path $modelDirectory 'FanViewerProject'
$blendFile = Join-Path $modelDirectory 'source\industrial-fan.blend'
$exportScript = Join-Path $PSScriptRoot 'export_fan_viewer.py'
$reportDirectory = Join-Path $modelDirectory 'reports'
foreach ($required in @($UnityExecutable, $BlenderExecutable, $blendFile, $exportScript)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing required file: $required"
    }
}
$assets = Join-Path $projectDirectory 'Assets\IndustrialFan'
New-Item -ItemType Directory -Force -Path $assets, $reportDirectory | Out-Null
& $BlenderExecutable --background $blendFile --python $exportScript
if ($LASTEXITCODE -ne 0) { throw 'Fan FBX/manifest export failed.' }
$stage = if ($BuildWindows) { 'windows' } else { 'stage3' }
$logFile = Join-Path $reportDirectory ('industrial-fan-' + $stage + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$method = if ($BuildWindows) { 'FanViewerBuild.BuildWindows' } else { 'FanViewerBuild.BuildReviewScene' }
$process = Start-Process -FilePath $UnityExecutable -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode', '-force-d3d11', '-projectPath', ('"' + $projectDirectory + '"'),
    '-executeMethod', $method, '-logFile', ('"' + $logFile + '"'), '-quit'
)
while (-not $process.WaitForExit(1000)) { }
if ($process.ExitCode -ne 0) { throw "Fan review scene generation failed. See $logFile" }
if ($BuildWindows) {
    Write-Output "Windows x64 viewer built at $(Join-Path $modelDirectory 'IndustrialFanViewer\IndustrialFanViewer.exe')"
} else {
    Write-Output "Stage 1 export and Stage 2 setup complete. Stage 3 review scene: $projectDirectory"
}
