param(
    [Parameter(Mandatory)][string]$Candidate,
    [Parameter(Mandatory)][string]$Output
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$candidatePath = (Resolve-Path -LiteralPath $Candidate).Path
$candidateRoot = Split-Path -Parent $candidatePath
$outputPath = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $outputPath) { throw "Evidence already exists: $outputPath" }
$baselinePath = Join-Path $projectRoot 'Evidence/fault-module-20261115/protected-before.json'
$baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
$checks = foreach ($root in @(
    (Join-Path $projectRoot 'Build/WorkshopPlatform/Viewers'),
    (Join-Path $candidateRoot 'Viewers')
)) {
    $issues = @()
    foreach ($file in $baseline) {
        $path = Join-Path $root $file.path
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            $issues += "Missing: $($file.path)"
            continue
        }
        $item = Get-Item -LiteralPath $path
        if ($item.Length -ne $file.length -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
            $issues += "Changed: $($file.path)"
        }
    }
    $actual = @(Get-ChildItem -LiteralPath $root -File -Recurse).Count
    [ordered]@{
        root = $root; expected = $baseline.Count; actual = $actual; issues = $issues
        pass = $issues.Count -eq 0 -and $actual -eq $baseline.Count
    }
}
$fingerprints = foreach ($path in @($candidatePath,
    (Join-Path $candidateRoot 'WorkshopPlatform_Data/Managed/Assembly-CSharp.dll'))) {
    [ordered]@{
        path = $path; length = (Get-Item -LiteralPath $path).Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
}
$parts = foreach ($name in @('cabinet','fan','pump','tank')) {
    $relative = "Assets/WorkshopDemo/Resources/ReviewedEquipment/$name-parts.json"
    $working = Get-Content -LiteralPath (Join-Path $projectRoot $relative) -Raw | ConvertFrom-Json
    $committed = (& git -C $projectRoot show "HEAD:$relative") -join "`n" | ConvertFrom-Json
    [ordered]@{
        path = $relative
        pass = ($working | ConvertTo-Json -Depth 30 -Compress) -ceq
            ($committed | ConvertTo-Json -Depth 30 -Compress)
    }
}
$missingMeta = @(
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/WorkshopDemo/Scripts'),
        (Join-Path $projectRoot 'Assets/WorkshopDemo/Editor'),
        (Join-Path $projectRoot 'Assets/WorkshopDemo/Resources/WorkshopFaults') -Recurse |
        Where-Object { $_.Extension -ne '.meta' -and
            !(Test-Path -LiteralPath ($_.FullName + '.meta')) } |
        ForEach-Object { $_.FullName }
)
$result = [ordered]@{
    time = (Get-Date).ToString('o'); candidate = $candidatePath
    baseline = $baselinePath; viewers = @($checks); fingerprints = @($fingerprints)
    assemblyContracts = @($parts); missingMeta = $missingMeta
    pass = @($checks | Where-Object { !$_.pass }).Count -eq 0 -and
        @($parts | Where-Object { !$_.pass }).Count -eq 0 -and $missingMeta.Count -eq 0
}
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outputPath -Encoding utf8
Write-Output ("Delivery check pass={0}; viewer files={1} per copy; missing meta={2}" -f
    $result.pass, $baseline.Count, $missingMeta.Count)
if (!$result.pass) { throw "Delivery protection check failed: $outputPath" }
