$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$externalRoot = Join-Path $projectRoot 'Assets/WorkshopDemo/External'
New-Item -ItemType Directory -Force -Path $externalRoot | Out-Null

function Get-VerifiedFile($entry, $destination) {
    if (Test-Path -LiteralPath $destination) {
        if ((Get-FileHash -LiteralPath $destination -Algorithm MD5).Hash.ToLowerInvariant() -eq $entry.md5) { return }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Invoke-WebRequest -Uri $entry.url -OutFile $destination -TimeoutSec 180
    if ((Get-FileHash -LiteralPath $destination -Algorithm MD5).Hash.ToLowerInvariant() -ne $entry.md5) {
        throw "Checksum mismatch: $destination"
    }
}

$models = @('modular_industrial_pipes_01', 'metal_tool_chest', 'Barrel_02', 'industrial_storage_cart')
foreach ($assetId in $models) {
    Write-Output "Downloading CC0 model: $assetId"
    $manifest = Invoke-RestMethod "https://api.polyhaven.com/files/$assetId"
    $entry = $manifest.fbx.'1k'.fbx
    $folder = Join-Path $externalRoot $assetId
    Get-VerifiedFile $entry (Join-Path $folder "$assetId.fbx")
    foreach ($texture in $entry.include.PSObject.Properties) {
        Get-VerifiedFile $texture.Value (Join-Path $folder $texture.Name)
    }
}

foreach ($assetId in @('concrete_floor_painted', 'corrugated_iron_02')) {
    Write-Output "Downloading CC0 PBR material: $assetId"
    $manifest = Invoke-RestMethod "https://api.polyhaven.com/files/$assetId"
    foreach ($map in @('diff', 'nor_gl', 'rough')) {
        $entry = $manifest.$map.'2k'.jpg
        if (-not $entry) { $entry = $manifest.$map.'2k'.png }
        if ($entry) {
            $extension = [IO.Path]::GetExtension(([Uri]$entry.url).AbsolutePath)
            Get-VerifiedFile $entry (Join-Path $externalRoot "$assetId/$map$extension")
        }
    }
}

Write-Output 'Downloading open licensed Chinese font'
$fontFolder = Join-Path $externalRoot 'SourceHanSans'
New-Item -ItemType Directory -Force -Path $fontFolder | Out-Null
Invoke-WebRequest 'https://raw.githubusercontent.com/adobe-fonts/source-han-sans/release/OTF/SimplifiedChinese/SourceHanSansSC-Regular.otf' -OutFile (Join-Path $fontFolder 'SourceHanSansSC-Regular.otf') -TimeoutSec 180
Invoke-WebRequest 'https://raw.githubusercontent.com/adobe-fonts/source-han-sans/release/LICENSE.txt' -OutFile (Join-Path $fontFolder 'LICENSE.txt') -TimeoutSec 30
Write-Output 'All external assets downloaded.'
