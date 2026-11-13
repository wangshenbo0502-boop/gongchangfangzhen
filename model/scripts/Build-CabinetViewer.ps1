param(
    [string]$UnityExecutable = 'D:\unity\Editor\Unity.exe',
    [string]$BlenderExecutable = 'E:\steam\steamapps\common\Blender\blender.exe',
    [string]$OutputDirectory,
    [switch]$RuntimeOnly,
    [switch]$PresentationOnly,
    [switch]$ExportOnly
)
$ErrorActionPreference = 'Stop'
$modelDirectory = Split-Path -Parent $PSScriptRoot
$projectDirectory = Join-Path $modelDirectory 'CabinetViewerProject'
$sourceFile = Join-Path $modelDirectory 'source\electrical-cabinet-viewer-fit-review.blend'
$exportScript = Join-Path $PSScriptRoot 'export_cabinet_viewer.py'
$baselineScript = Join-Path $PSScriptRoot 'inspect_cabinet_baseline_intersections.py'
$verifyScript = Join-Path $PSScriptRoot 'verify_cabinet_viewer_fbx.py'
$motionScript = Join-Path $PSScriptRoot 'audit_cabinet_imported_motion.py'
$reportDirectory = Join-Path $modelDirectory 'reports'
if (@($RuntimeOnly, $PresentationOnly, $ExportOnly).Where({ $_ }).Count -gt 1) {
    throw 'RuntimeOnly, PresentationOnly, and ExportOnly are mutually exclusive.'
}
foreach ($required in @($BlenderExecutable, $UnityExecutable, $sourceFile, $exportScript,
        $baselineScript, $verifyScript, $motionScript)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing required file: $required"
    }
}
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null

if (-not $RuntimeOnly -and -not $PresentationOnly) {
    & $BlenderExecutable --background $sourceFile --python-exit-code 1 --python $exportScript
    if ($LASTEXITCODE -ne 0) { throw 'Cabinet FBX/manifest export failed.' }
    & $BlenderExecutable --background $sourceFile --python-exit-code 1 --python $baselineScript
    if ($LASTEXITCODE -ne 0) { throw 'Cabinet closed-assembly intersection verification failed.' }
    & $BlenderExecutable --background --python-exit-code 1 --python $verifyScript
    if ($LASTEXITCODE -ne 0) { throw 'Cabinet FBX round-trip verification failed.' }
}
if ($ExportOnly) {
    Write-Output 'Stage 1 export and FBX round-trip verification complete.'
    exit 0
}

$stage = if ($RuntimeOnly) { 'runtime' } elseif ($PresentationOnly) { 'studio' } else { 'windows' }
$logFile = Join-Path $reportDirectory (
    'electrical-cabinet-viewer-' + $stage + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$method = if ($RuntimeOnly) {
    'FanViewerBuild.BuildWindowsRuntime'
} elseif ($PresentationOnly) {
    'FanViewerBuild.BuildWindowsStudio'
} else {
    'FanViewerBuild.BuildWindows'
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $modelDirectory 'CabinetViewer' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$env:WORKSHOP_CABINET_BUILD_DIRECTORY = $OutputDirectory
$process = Start-Process -FilePath $UnityExecutable -WorkingDirectory $projectDirectory `
    -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode', '-force-d3d11', '-projectPath', ('"' + $projectDirectory + '"'),
    '-executeMethod', $method, '-logFile', ('"' + $logFile + '"'), '-quit'
)
while (-not $process.WaitForExit(1000)) { }
if ($process.ExitCode -ne 0) { throw "Cabinet viewer build failed. See $logFile" }
if (-not (Select-String -LiteralPath $logFile -Pattern 'Build Successful' -Quiet)) {
    throw "Unity did not report a successful Player build. See $logFile"
}
$exe = Join-Path $OutputDirectory 'CabinetViewer.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Player missing: $exe" }
if (-not $RuntimeOnly -and -not $PresentationOnly) {
    & $BlenderExecutable --background --python-exit-code 1 --python $motionScript -- --samples 2401
    if ($LASTEXITCODE -ne 0) { throw 'Cabinet imported motion verification failed.' }
}
Write-Output "Build log: $logFile"
Write-Output "Windows x64 viewer: $exe"
