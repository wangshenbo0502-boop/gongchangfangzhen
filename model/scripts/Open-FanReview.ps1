param([string]$BlenderExecutable = 'E:\steam\steamapps\common\Blender\blender.exe')
$ErrorActionPreference = 'Stop'
$modelDirectory = Split-Path -Parent $PSScriptRoot
$blendFile = Join-Path $modelDirectory 'source\industrial-fan.blend'
$reviewScript = Join-Path $PSScriptRoot 'fan_review_ui.py'
foreach ($requiredPath in @($BlenderExecutable, $blendFile, $reviewScript)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Missing file: $requiredPath"
    }
}
# The user requested a visible Blender modelling and review window.
Start-Process -FilePath $BlenderExecutable -ArgumentList @(
    '--no-splash', ('"' + $blendFile + '"'), '--python', ('"' + $reviewScript + '"')
) -PassThru | Select-Object Id, ProcessName
