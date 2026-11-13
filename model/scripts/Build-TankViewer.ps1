param(
    [string]$UnityExecutable = 'D:\unity\Editor\Unity.exe',
    [string]$BlenderExecutable = 'E:\steam\steamapps\common\Blender\blender.exe',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$modelRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $modelRoot 'TankViewerProject'
$source = Join-Path $modelRoot 'source\silo-viewer-fit-review.blend'
$report = Join-Path $modelRoot 'reports'
foreach ($path in @($UnityExecutable,$BlenderExecutable,$source)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing required file: $path" }
}
New-Item -ItemType Directory -Force -Path $report | Out-Null
$exportLog = Join-Path $report ('tank-viewer-export-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
& $BlenderExecutable --background $source --python-exit-code 1 --python (Join-Path $PSScriptRoot 'export_tank_viewer.py') 2>&1 |
    Tee-Object -FilePath $exportLog
if ($LASTEXITCODE -ne 0) { throw "Tank FBX export failed: $exportLog" }
$buildLog = Join-Path $report ('tank-viewer-unity-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $modelRoot 'TankViewer' }
$env:WORKSHOP_TANK_BUILD_DIRECTORY = [IO.Path]::GetFullPath($OutputDirectory)
$proc = Start-Process -FilePath $UnityExecutable -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode','-force-d3d11','-projectPath',('"' + $project + '"'),
    '-executeMethod','TankViewerBuild.Build','-logFile',('"' + $buildLog + '"'),'-quit'
)
while (-not $proc.WaitForExit(1000)) { }
if ($proc.ExitCode -ne 0) { throw "Unity build failed. See $buildLog" }
& $BlenderExecutable --background --python-exit-code 1 --python (Join-Path $PSScriptRoot 'audit_tank_motion.py') -- `
    --geometry (Join-Path $report 'tank-unity-motion-geometry-20261113.json') `
    --output (Join-Path $report 'tank-unity-motion-audit-20261113.json') --samples 2401
if ($LASTEXITCODE -ne 0) { throw 'Tank imported mesh motion verification failed.' }
Write-Output "Build log: $buildLog"
Write-Output (Join-Path $OutputDirectory 'TankViewer.exe')
