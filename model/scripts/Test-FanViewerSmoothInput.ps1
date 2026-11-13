param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Executable = (Join-Path (Split-Path -Parent $PSScriptRoot) 'IndustrialFanViewer\IndustrialFanViewer.exe'),
    [int]$Width = 1997,
    [int]$Height = 1217,
    [int]$StartupDelayMilliseconds = 500
)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FanSmoothInput {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr order,
        int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr process);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
'@
[FanSmoothInput]::SetThreadDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -ErrorAction Stop | Out-Null
$statePath = Join-Path $OutputDirectory 'state.json'
$logPath = Join-Path $OutputDirectory 'Player.log'
$viewer = Start-Process -FilePath $Executable -PassThru -ArgumentList @(
    '-screen-width', "$Width", '-screen-height', "$Height", '-screen-fullscreen', '0',
    '-fanReviewOutput', ('"' + $OutputDirectory + '"'), '-logFile', ('"' + $logPath + '"'))
try {
    function Wait-FanState([scriptblock]$Condition, [int]$Timeout = 25) {
        $deadline = (Get-Date).AddSeconds($Timeout)
        do {
            $viewer.Refresh()
            if ($viewer.HasExited) { throw 'Viewer exited before input verification completed.' }
            if (Test-Path -LiteralPath $statePath) {
                $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
                if (& $Condition $state) { return $state }
            }
            Start-Sleep -Milliseconds 150
        } while ((Get-Date) -lt $deadline)
        throw 'Timed out waiting for mouse-driven fan state.'
    }
    $initial = Wait-FanState {
        param($state) $state.progress -lt .001 -and $state.clarityRenderWidth -gt 0
    }
    $viewer.Refresh()
    $window = $viewer.MainWindowHandle
    if ($window -eq [IntPtr]::Zero) { throw 'Viewer has no visible window.' }
    [FanSmoothInput]::ShowWindow($window, 9) | Out-Null
    [FanSmoothInput]::SetWindowPos($window, [IntPtr]::new(-1), 0, 0, 0, 0, 0x0043) | Out-Null
    $foregroundThread = [FanSmoothInput]::GetWindowThreadProcessId(
        [FanSmoothInput]::GetForegroundWindow(), [IntPtr]::Zero)
    $currentThread = [FanSmoothInput]::GetCurrentThreadId()
    $attached = [FanSmoothInput]::AttachThreadInput($currentThread, $foregroundThread, $true)
    try { [FanSmoothInput]::SetForegroundWindow($window) | Out-Null }
    finally {
        if ($attached) {
            [FanSmoothInput]::AttachThreadInput($currentThread, $foregroundThread, $false) | Out-Null
        }
    }
    Start-Sleep -Milliseconds ([Math]::Max(100, $StartupDelayMilliseconds))
    if ([FanSmoothInput]::GetForegroundWindow() -ne $window) { throw 'Viewer did not receive foreground focus.' }
    $scale = [Math]::Sqrt(($initial.width / 1600.0) * ($initial.height / 900.0))
    function Click-FanButton([double]$ReferenceX) {
        if ([FanSmoothInput]::GetForegroundWindow() -ne $window) { throw 'Viewer lost foreground focus.' }
        $point = New-Object FanSmoothInput+Point
        $point.X = [int]($ReferenceX * $scale)
        $point.Y = [int]($initial.height - 41 * $scale)
        if (-not [FanSmoothInput]::ClientToScreen($window, [ref]$point)) { throw 'Client coordinate conversion failed.' }
        [FanSmoothInput]::SetCursorPos($point.X, $point.Y) | Out-Null
        Start-Sleep -Milliseconds 120
        [FanSmoothInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 80
        [FanSmoothInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    }
    Click-FanButton 82
    $exploded = Wait-FanState {
        param($state) $state.progress -gt .999 -and -not $state.animating -and $state.motionPerformance.Count -eq 1
    }
    Click-FanButton 196
    $assembled = Wait-FanState {
        param($state) $state.progress -lt .001 -and -not $state.animating -and $state.motionPerformance.Count -eq 2
    }
    if ($assembled.maxHomeError -gt .00001) { throw 'Mouse-driven assembly did not return home.' }
    $errors = @(Select-String -LiteralPath $logPath -Pattern 'Exception:|NullReferenceException|error CS[0-9]+')
    if ($errors.Count) { throw 'Player log contains runtime errors.' }
    $result = [ordered]@{
        date = Get-Date -Format o
        executable = [IO.Path]::GetFullPath($Executable)
        resolution = "$($assembled.width) x $($assembled.height)"
        method = 'Foreground OS mouse down/up at real UI buttons; normal EventSystem and input enabled; no screenshots during motion.'
        passed = $true
        homeError = $assembled.maxHomeError
        targetFrameRate = $assembled.targetFrameRate
        startupDelayMilliseconds = $StartupDelayMilliseconds
        renderReadySeconds = $assembled.renderReadySeconds
        displayRefreshHz = $assembled.displayRefreshHz
        motionPerformance = $assembled.motionPerformance
        runtimeErrors = 0
    }
    $result | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $OutputDirectory 'input-verification.json') -Encoding utf8
    $result | ConvertTo-Json -Depth 6
} finally {
    if ($viewer -and -not $viewer.HasExited) {
        [FanSmoothInput]::SetWindowPos($viewer.MainWindowHandle, [IntPtr]::new(-2),
            0, 0, 0, 0, 0x0013) | Out-Null
        $viewer.CloseMainWindow() | Out-Null
        if (-not $viewer.WaitForExit(5000)) { throw 'Test viewer did not close gracefully.' }
    }
}
