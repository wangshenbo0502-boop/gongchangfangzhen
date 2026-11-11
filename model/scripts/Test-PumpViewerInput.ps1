param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'reports\pump-viewer-input')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PumpViewerInput {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref Point p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr order, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
}
'@
[PumpViewerInput]::SetThreadDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null
$viewer = Get-Process -Name WaterPumpViewer | Select-Object -First 1
$viewer.Refresh()
$windowHandle = $viewer.MainWindowHandle
if ($windowHandle -eq [IntPtr]::Zero) { throw 'Viewer has no visible window.' }
[PumpViewerInput]::ShowWindow($windowHandle, 9) | Out-Null
[PumpViewerInput]::SetWindowPos($windowHandle, [IntPtr]::new(-1), 0, 0, 0, 0, 0x0043) | Out-Null
[PumpViewerInput]::SetForegroundWindow($windowHandle) | Out-Null
Start-Sleep -Milliseconds 600
$bounds = New-Object PumpViewerInput+Rect
[PumpViewerInput]::GetClientRect($windowHandle, [ref]$bounds) | Out-Null
$origin = New-Object PumpViewerInput+Point
[PumpViewerInput]::ClientToScreen($windowHandle, [ref]$origin) | Out-Null
$width = $bounds.Right
$height = $bounds.Bottom
if ($width -ne 1600 -or $height -ne 900) { throw "Use a 1600 x 900 client window for this input review; found $width x $height." }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
function Assert-Foreground {
    if ([PumpViewerInput]::GetForegroundWindow() -ne $windowHandle) {
        throw 'Viewer lost foreground focus; stopping desktop input.'
    }
}
function Move-Pointer([int]$x, [int]$y) {
    Assert-Foreground
    [PumpViewerInput]::SetCursorPos($origin.X+$x, $origin.Y+$y) | Out-Null
}
function Click-Control([int]$x, [int]$y) {
    Move-Pointer $x $y
    Start-Sleep -Milliseconds 120
    [PumpViewerInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [PumpViewerInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 250
}
function Capture-Client([string]$name) {
    Assert-Foreground
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bitmap.Size)
        $bitmap.Save((Join-Path $OutputDirectory ($name+'.png')), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}
Capture-Client '01-initial'
Click-Control 82 859
Start-Sleep -Milliseconds 1600
Click-Control 303 859
Capture-Client '02-paused'
Start-Sleep -Milliseconds 1000
Capture-Client '03-still-paused'
Click-Control 303 859
Start-Sleep -Milliseconds 14600
Capture-Client '04-exploded'
Click-Control 196 859
Start-Sleep -Milliseconds 14600
Capture-Client '05-assembled'
Move-Pointer 150 802
[PumpViewerInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
foreach ($step in 1..20) {
    Move-Pointer (150+[int](673*$step/20)) 802
    Start-Sleep -Milliseconds 25
}
[PumpViewerInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1000
Capture-Client '06-scrubbed'
Move-Pointer 850 450
[PumpViewerInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
foreach ($step in 1..20) {
    Move-Pointer (850+10*$step) (450+2*$step)
    Start-Sleep -Milliseconds 30
}
[PumpViewerInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1000
Capture-Client '07-orbited'
[PumpViewerInput]::mouse_event(0x0800, 0, 0, 360, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1000
Capture-Client '08-zoomed'
Click-Control 623 859
Start-Sleep -Milliseconds 1000
Capture-Client '09-reset-camera'
Click-Control 503 859
Start-Sleep -Milliseconds 2500
Capture-Client '10-auto-orbit'
Click-Control 503 859
Click-Control 403 859
Start-Sleep -Milliseconds 14600
Capture-Client '11-loop-exploded'
Start-Sleep -Milliseconds 6500
Capture-Client '12-loop-returning'
Click-Control 196 859
Start-Sleep -Milliseconds 14600
Click-Control 623 859
Start-Sleep -Milliseconds 1000
Capture-Client '13-final-assembled'
[ordered]@{
    date = (Get-Date -Format o)
    processId = $viewer.Id
    width = $width
    height = $height
    method = 'Visible client capture and OS mouse events; screenshot review required.'
    actions = @('explode', 'pause', 'resume', 'assemble', 'scrub', 'orbit', 'zoom', 'reset camera', 'auto orbit', 'loop')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'input-actions.json') -Encoding UTF8
[PumpViewerInput]::SetWindowPos($windowHandle, [IntPtr]::new(-2), 0, 0, 0, 0, 0x0043) | Out-Null
