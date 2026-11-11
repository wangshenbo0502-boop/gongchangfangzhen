param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'reports\pump-clearance-default-window')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PumpDefaultWindow {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref Point p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
'@
[PumpDefaultWindow]::SetThreadDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$executable = Join-Path (Split-Path -Parent $PSScriptRoot) 'WaterPumpViewer\WaterPumpViewer.exe'
$logPath = Join-Path $outputPath 'Player.log'
$viewer = Start-Process -FilePath $executable -ArgumentList @('-logFile', ('"'+$logPath+'"')) -PassThru
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    Start-Sleep -Milliseconds 500
    $viewer.Refresh()
    if ($viewer.HasExited) { throw 'Viewer exited during normal startup.' }
    if ($viewer.MainWindowHandle -ne [IntPtr]::Zero) { break }
}
$windowHandle = $viewer.MainWindowHandle
if ($windowHandle -eq [IntPtr]::Zero) { throw 'Viewer did not create a visible window.' }
Start-Sleep -Milliseconds 2500
[PumpDefaultWindow]::SetForegroundWindow($windowHandle) | Out-Null
Start-Sleep -Milliseconds 500
$bounds = New-Object PumpDefaultWindow+Rect
if (-not [PumpDefaultWindow]::GetClientRect($windowHandle, [ref]$bounds)) { throw 'GetClientRect failed.' }
$origin = New-Object PumpDefaultWindow+Point
if (-not [PumpDefaultWindow]::ClientToScreen($windowHandle, [ref]$origin)) { throw 'ClientToScreen failed.' }
$desktop = [System.Windows.Forms.Screen]::FromHandle($windowHandle).Bounds
$width = $bounds.Right - $bounds.Left
$height = $bounds.Bottom - $bounds.Top
$expectedHeight = [int][Math]::Floor([Math]::Min(1260, [Math]::Min($desktop.Height - 180, ($desktop.Width - 100) * 9.0 / 16.0)))
$expectedWidth = [int][Math]::Round($expectedHeight * 16.0 / 9.0)
$foreground = [PumpDefaultWindow]::GetForegroundWindow() -eq $windowHandle
if ($foreground) {
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bitmap.Size)
        $bitmap.Save((Join-Path $outputPath 'default-window.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}
$result = [ordered]@{
    date = (Get-Date -Format o)
    processId = $viewer.Id
    arguments = @('-logFile', $logPath)
    desktopWidth = $desktop.Width
    desktopHeight = $desktop.Height
    clientWidth = $width
    clientHeight = $height
    expectedWidth = $expectedWidth
    expectedHeight = $expectedHeight
    foregroundCapture = $foreground
    passed = (($width -eq $expectedWidth) -and ($height -eq $expectedHeight))
    leftRunningForReview = $true
}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'window-verification.json') -Encoding UTF8
$result | ConvertTo-Json
if (-not $result.passed) { throw 'Default client dimensions did not match desktop sizing.' }
