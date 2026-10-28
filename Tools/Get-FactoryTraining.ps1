$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$downloads = Join-Path $projectRoot 'Downloads'
New-Item -ItemType Directory -Force -Path $downloads | Out-Null
$manifestPath = Join-Path $projectRoot 'Evidence/asset-service-0.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$upload = $manifest.uploads.'6000.3.0f1'
if (-not $upload.downloadS3key.StartsWith('download/')) { throw 'Unexpected official package key' }
$sourceUrl = 'https://assetstorev1-prd-cdn.unity3d.com/' + $upload.downloadS3key
$destination = Join-Path $downloads 'FactoryTraining-1.0-Unity6.3.unitypackage'
$partial = $destination + '.partial'
$head = Invoke-WebRequest -Uri $sourceUrl -Method Head -TimeoutSec 30
$expectedSize = [long]$head.Headers.'Content-Length'[0]
if ($expectedSize -ne [long]$upload.downloadSize) { throw 'Official manifest and CDN size disagree' }
Write-Output "Official CDN HTTP $($head.StatusCode), expected bytes: $expectedSize"
if (Test-Path -LiteralPath $destination) {
    if ((Get-Item -LiteralPath $destination).Length -eq $expectedSize) {
        Write-Output "Package already downloaded: $destination"
        exit 0
    }
    throw 'Existing package has unexpected size; it was preserved'
}
& curl.exe --fail --location --retry 3 --connect-timeout 30 --max-time 7200 --continue-at - --output $partial $sourceUrl --silent --show-error
if ($LASTEXITCODE -ne 0) { throw "Download failed with curl exit code $LASTEXITCODE; partial file retained for resume" }
if ((Get-Item -LiteralPath $partial).Length -ne $expectedSize) { throw 'Downloaded package size mismatch' }
Move-Item -LiteralPath $partial -Destination $destination
$hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
[PSCustomObject]@{
    asset = 'Factory Training | Industry Sample'
    assetStoreId = 344832
    version = '1.0'
    unityVersion = '6000.3.0f1'
    source = $sourceUrl
    expectedBytes = $expectedSize
    sha256 = $hash
    license = 'Unity Companion License'
    licenseUrl = 'https://unity.com/legal/licenses/unity-companion-license'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $downloads 'FactoryTraining-source.json') -Encoding utf8
Write-Output "Downloaded and size verified: $destination"
Write-Output "SHA256: $hash"
