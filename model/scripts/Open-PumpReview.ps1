param([string]$BlenderExecutable = 'E:\steam\steamapps\common\Blender\blender.exe')
$ErrorActionPreference = 'Stop'
$modelDirectory = Split-Path -Parent $PSScriptRoot
$blendFile = Join-Path $modelDirectory 'source\water-pump.blend'
$reviewScript = Join-Path $PSScriptRoot 'pump_review_ui.py'
foreach ($requiredPath in @($BlenderExecutable, $blendFile, $reviewScript)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Missing file: $requiredPath"
    }
}
# A visible window is intentional: this command opens the requested Blender model.
Start-Process -FilePath $BlenderExecutable -ArgumentList @(
    '--no-splash', ('"' + $blendFile + '"'), '--python', ('"' + $reviewScript + '"')
) -PassThru | Select-Object Id, ProcessName
