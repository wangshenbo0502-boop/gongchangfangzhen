param(
    [string]$UnityExecutable = 'D:\unity\Editor\Unity.exe',
    [string]$BlenderExecutable = 'E:\steam\steamapps\common\Blender\blender.exe'
)
$ErrorActionPreference = 'Stop'
$modelDirectory = Split-Path -Parent $PSScriptRoot
$projectDirectory = Join-Path $modelDirectory 'PumpViewerProject'
$blendFile = Join-Path $modelDirectory 'source\water-pump.blend'
$exportScript = Join-Path $PSScriptRoot 'export_pump_viewer.py'
$reportDirectory = Join-Path $modelDirectory 'reports'
foreach ($required in @($UnityExecutable, $BlenderExecutable, $blendFile, $exportScript)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Missing file: $required" }
}
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
& $BlenderExecutable --background $blendFile --python $exportScript
if ($LASTEXITCODE -ne 0) { throw 'Blender export failed.' }
$studioMap = Join-Path $projectDirectory 'Assets\Textures\inspection-studio.exr'
if (-not (Test-Path -LiteralPath $studioMap -PathType Leaf) -or
    (Get-Item -LiteralPath $studioMap).LastWriteTime -lt (Get-Item -LiteralPath $blendFile).LastWriteTime) {
    & $BlenderExecutable --background $blendFile --python (Join-Path $PSScriptRoot 'bake_pump_studio.py')
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $studioMap -PathType Leaf)) {
        throw 'Blender studio reflection capture failed.'
    }
}
$logFile = Join-Path $reportDirectory ('pump-viewer-build-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$buildProcess = Start-Process -FilePath $UnityExecutable -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode', '-force-d3d11', '-projectPath', ('"' + $projectDirectory + '"'),
    '-executeMethod', 'PumpViewerBuild.Build', '-logFile', ('"' + $logFile + '"'), '-quit'
)
while (-not $buildProcess.WaitForExit(1000)) { }
if ($buildProcess.ExitCode -ne 0) { throw "Unity build failed. See $logFile" }
Write-Output (Join-Path $modelDirectory 'WaterPumpViewer\WaterPumpViewer.exe')
