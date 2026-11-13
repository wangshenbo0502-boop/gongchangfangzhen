param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'reports\industrial-fan-player-20261112'),
    [int]$Width = 1280,
    [int]$Height = 720
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FanViewerInput {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref Point p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint attach, uint attachTo, bool value);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr processId);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr order,
        int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
}
'@
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'IndustrialFanViewer\IndustrialFanViewer.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Viewer not found: $exe" }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$old = Get-Process IndustrialFanViewer -ErrorAction SilentlyContinue
foreach ($process in $old) { $process.CloseMainWindow() | Out-Null }
$statePath = Join-Path $OutputDirectory 'state.json'
$logPath = Join-Path $OutputDirectory 'Player.log'
Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue
$viewer = Start-Process -FilePath $exe -ArgumentList @(
    '-screen-width', "$Width", '-screen-height', "$Height", '-screen-fullscreen', '0',
    '-fanReviewOutput', ('"' + $OutputDirectory + '"'), '-logFile', ('"' + $logPath + '"')
) -PassThru
try {
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        $viewer.Refresh()
        if ($viewer.HasExited) { throw 'Viewer exited during startup.' }
        if ($viewer.MainWindowHandle -ne [IntPtr]::Zero -and (Test-Path $statePath)) { break }
    }
    $hwnd = $viewer.MainWindowHandle
    if ($hwnd -eq [IntPtr]::Zero -or -not (Test-Path $statePath)) { throw 'No viewer window or live state.' }
    [FanViewerInput]::ShowWindow($hwnd, 9) | Out-Null
    [FanViewerInput]::SetWindowPos($hwnd, [IntPtr]::new(-1), 0, 0, 0, 0, 0x0043) | Out-Null
    [FanViewerInput]::AllowSetForegroundWindow($viewer.Id) | Out-Null
    $foreground = [FanViewerInput]::GetForegroundWindow()
    $foregroundThread = [FanViewerInput]::GetWindowThreadProcessId($foreground, [IntPtr]::Zero)
    $currentThread = [FanViewerInput]::GetCurrentThreadId()
    [FanViewerInput]::AttachThreadInput($currentThread, $foregroundThread, $true) | Out-Null
    [FanViewerInput]::SetForegroundWindow($hwnd) | Out-Null
    [FanViewerInput]::AttachThreadInput($currentThread, $foregroundThread, $false) | Out-Null
    Start-Sleep -Milliseconds 500
    $client = New-Object FanViewerInput+Rect
    [FanViewerInput]::GetClientRect($hwnd, [ref]$client) | Out-Null
    $origin = New-Object FanViewerInput+Point
    [FanViewerInput]::ClientToScreen($hwnd, [ref]$origin) | Out-Null
    $actualWidth, $actualHeight = $client.Right, $client.Bottom
    function Assert-Foreground {
        $viewer.Refresh()
        if ($viewer.HasExited) { throw 'Viewer exited during interaction test.' }
    }
    function Read-State {
        Assert-Foreground
        Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    }
    function Capture([string]$Name) {
        Assert-Foreground
        $bitmap = New-Object System.Drawing.Bitmap $actualWidth, $actualHeight
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bitmap.Size)
            $bitmap.Save((Join-Path $OutputDirectory ($Name + '.png')),
                [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
    function Click-At([int]$X, [int]$Y, [int]$Delay = 250) {
        Assert-Foreground
        [FanViewerInput]::SetCursorPos($origin.X + $X, $origin.Y + $Y) | Out-Null
        Start-Sleep -Milliseconds 100
        [FanViewerInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 90
        [FanViewerInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds $Delay
    }
    function Wait-State([scriptblock]$Condition, [int]$TimeoutSeconds = 20) {
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        do {
            $current = Read-State
            if (& $Condition $current) { return $current }
            Start-Sleep -Milliseconds 150
        } while ((Get-Date) -lt $deadline)
        throw "Timed out waiting for viewer state. Last state: $($current | ConvertTo-Json -Compress)"
    }
    $initial = Wait-State { param($s) -not $s.animating -and $s.progress -lt .001 }
    Capture '01-assembled'
    $buttonY = $actualHeight - 38
    Click-At ([int]($actualWidth * .054)) $buttonY
    $moving = Wait-State { param($s) $s.progress -gt .08 }
    $startPosition = $moving.parts[2].position
    Start-Sleep -Seconds 2
    $mid = Read-State
    $distance = [Math]::Sqrt(
        [Math]::Pow($mid.parts[2].position.x - $startPosition.x, 2) +
        [Math]::Pow($mid.parts[2].position.y - $startPosition.y, 2) +
        [Math]::Pow($mid.parts[2].position.z - $startPosition.z, 2))
    if ($distance -lt .02) { throw 'Mid-motion position did not change.' }
    Click-At ([int]($actualWidth * .115)) $buttonY 100
    $returned = Wait-State { param($s) -not $s.animating -and $s.progress -lt .001 }
    if ($returned.maxHomeError -gt .002) { throw "Reverse assembly did not return home: $($returned.maxHomeError)" }
    Capture '02-reassembled-after-reversal'
    Click-At ([int]($actualWidth * .054)) $buttonY 100
    $exploded = Wait-State { param($s) -not $s.animating -and $s.progress -gt .999 } 24
    Capture '03-exploded'
    $target = $exploded.parts | Where-Object { $_.pickable } | Select-Object -First 1
    if (-not $target) { throw 'No exploded assembly supplied a verified pick point.' }
    Click-At ([int]$target.pickPoint.x) ([int]($actualHeight - $target.pickPoint.y)) 140
    Click-At ([int]$target.pickPoint.x) ([int]($actualHeight - $target.pickPoint.y)) 400
    $selected = Wait-State { param($s) $s.selectedPart -ge 0 -and $s.descriptionVisible -and $s.outlineVisible }
    Capture '04-component-selected'
    if ($selected.visibleLabels -lt 1) { throw 'Exploded-state component labels are not visible.' }
    if ($selected.overlappingLabels -ne 0) { throw "Label overlap count: $($selected.overlappingLabels)" }
    if ($selected.labelsOutsideViewport -ne 0) { throw "Labels outside model viewport: $($selected.labelsOutsideViewport)" }
    $errors = Select-String -LiteralPath $logPath -Pattern 'Exception:|NullReferenceException|error CS[0-9]+' `
        -ErrorAction SilentlyContinue
    if ($errors) { throw "Runtime errors found in player log: $($errors | Select-Object -First 3 | ForEach-Object Line)" }
    [ordered]@{
        date = (Get-Date -Format o)
        executable = $exe
        width = $actualWidth
        height = $actualHeight
        tested = @('startup', 'explode motion', 'reverse assembly mid-motion', 'home reset',
            'complete disassembly', 'double-click assembly pick', 'outline', 'introduction',
            'label overlap and viewport bounds', 'runtime log')
        midMotionDisplacement = $distance
        reverseAssemblyHomeError = $returned.maxHomeError
        explodedProgress = $exploded.progress
        selectedPart = $selected.selectedPart
        visibleLabels = $selected.visibleLabels
        overlappingLabels = $selected.overlappingLabels
        labelsOutsideViewport = $selected.labelsOutsideViewport
        averageFps = $selected.averageFps
        allocatedMemoryBytes = $selected.allocatedMemoryBytes
        graphicsDevice = $selected.graphicsDevice
        runtimeErrors = 0
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'interaction-verification.json') -Encoding UTF8
} finally {
    if ($viewer -and -not $viewer.HasExited) { $viewer.CloseMainWindow() | Out-Null }
}
