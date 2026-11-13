$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$modelDirectory = Split-Path -Parent $PSScriptRoot
$imageDirectory = Join-Path $modelDirectory 'previews\industrial-fan'
$filenames = @('assembled.png', 'inlet-front.png', 'motor-side.png', 'elevated.png',
    'exploded.png', 'internal.png', 'blade-detail.png',
    'fault-intact.png', 'fault-bulged.png', 'fault-opened.png')
$results = foreach ($filename in $filenames) {
    $path = Join-Path $imageDirectory $filename
    $image = [System.Drawing.Bitmap]::FromFile($path)
    try {
        $samples = [System.Collections.Generic.List[double]]::new()
        for ($sampleY = 12; $sampleY -lt 44; $sampleY++) {
            for ($sampleX = 12; $sampleX -lt 44; $sampleX++) {
                $pixel = $image.GetPixel([int]($image.Width * $sampleX / 56),
                    [int]($image.Height * $sampleY / 56))
                $samples.Add((0.2126 * $pixel.R + 0.7152 * $pixel.G + 0.0722 * $pixel.B))
            }
        }
        $mean = ($samples | Measure-Object -Average).Average
        $variance = ($samples | ForEach-Object { [Math]::Pow($_ - $mean, 2) } |
            Measure-Object -Average).Average
        [pscustomobject]@{
            name = $filename
            width = $image.Width
            height = $image.Height
            central_luminance_mean = $mean
            central_luminance_stddev = [Math]::Sqrt($variance)
            passed = $image.Width -ge 1920 -and $image.Height -ge 1080 -and $variance -gt 25
        }
    } finally {
        $image.Dispose()
    }
}
$report = [pscustomobject]@{
    date = (Get-Date -Format o)
    method = 'Image dimensions and central luminance variation; visual review is separate.'
    images = @($results)
    passed = @($results | Where-Object { -not $_.passed }).Count -eq 0
}
$outputPath = Join-Path $modelDirectory 'reports\industrial-fan-preview-verification.json'
[System.IO.File]::WriteAllText($outputPath, ($report | ConvertTo-Json -Depth 6),
    [System.Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 6
if (-not $report.passed) { throw 'Fan image verification failed.' }
