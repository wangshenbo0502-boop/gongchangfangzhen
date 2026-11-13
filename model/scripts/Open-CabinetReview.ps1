param([string]$BlenderExecutable = 'E:\steam\steamapps\common\Blender\blender.exe')
$ErrorActionPreference = 'Stop'
$modelDirectory = Split-Path -Parent $PSScriptRoot
$blendFile = Join-Path $modelDirectory 'source\electrical-cabinet-duct-entry-review.blend'
$reviewScript = Join-Path $PSScriptRoot 'cabinet_review_ui.py'
foreach ($requiredPath in @($BlenderExecutable, $blendFile, $reviewScript)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Missing file: $requiredPath"
    }
}
# The requested model opens in a new visible window, preserving existing sessions.
Start-Process -FilePath $BlenderExecutable -ArgumentList @(
    '--no-splash', ('"' + $blendFile + '"'), '--python', ('"' + $reviewScript + '"')
) -PassThru | Select-Object Id, ProcessName
