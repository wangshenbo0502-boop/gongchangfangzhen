param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) `
        ('previews\electrical-cabinet\unity-review\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [int]$Width = 1920,
    [int]$Height = 1080,
    [string]$Executable = (Join-Path (Split-Path -Parent $PSScriptRoot) `
        'CabinetViewer\CabinetViewer.exe')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CabinetViewerInput {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref Point p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(
        IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr p);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
'@
[CabinetViewerInput]::SetProcessDPIAware() | Out-Null
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw "Viewer not found: $Executable" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$statePath = Join-Path $OutputDirectory 'state.json'
$logPath = Join-Path $OutputDirectory 'Player.log'
$viewer = Start-Process -FilePath $Executable -PassThru -ArgumentList @(
    '-screen-width', "$Width", '-screen-height', "$Height", '-screen-fullscreen', '0',
    '-fanReviewOutput', ('"' + $OutputDirectory + '"'),
    '-logFile', ('"' + $logPath + '"'))
try {
    $windowDeadline = (Get-Date).AddSeconds(10)
    do {
        $viewer.Refresh()
        if ($viewer.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $windowDeadline)
    if ($viewer.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Player window was not created.' }
    [CabinetViewerInput]::SetWindowPos($viewer.MainWindowHandle, [IntPtr](-1),
        0, 0, 0, 0, 0x0001) | Out-Null
    [CabinetViewerInput]::BringWindowToTop($viewer.MainWindowHandle) | Out-Null
    [CabinetViewerInput]::SetForegroundWindow($viewer.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    function Read-State {
        $viewer.Refresh()
        if ($viewer.HasExited) { throw 'Viewer exited during input verification.' }
        if (-not (Test-Path -LiteralPath $statePath)) { return $null }
        Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    }
    function Wait-State([scriptblock]$Condition, [int]$Timeout = 25) {
        $deadline = (Get-Date).AddSeconds($Timeout)
        do {
            $current = Read-State
            if ($current -and (& $Condition $current)) { return $current }
            Start-Sleep -Milliseconds 150
        } while ((Get-Date) -lt $deadline)
        throw "Timed out waiting for Player state in $statePath"
    }
    function Capture([string]$Name) {
        $client = New-Object CabinetViewerInput+Rect
        [CabinetViewerInput]::GetClientRect($viewer.MainWindowHandle, [ref]$client) | Out-Null
        $origin = New-Object CabinetViewerInput+Point
        [CabinetViewerInput]::ClientToScreen($viewer.MainWindowHandle, [ref]$origin) | Out-Null
        $bitmap = New-Object System.Drawing.Bitmap $client.Right, $client.Bottom
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bitmap.Size)
            $bitmap.Save((Join-Path $OutputDirectory ($Name + '.png')),
                [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
    function Click([int]$X, [int]$Y, [int]$Delay = 250) {
        $viewer.Refresh()
        $current = Read-State
        if (-not $current) { throw 'Player state is unavailable before mouse input.' }
        $foregroundThread = [CabinetViewerInput]::GetWindowThreadProcessId(
            [CabinetViewerInput]::GetForegroundWindow(), [IntPtr]::Zero)
        $currentThread = [CabinetViewerInput]::GetCurrentThreadId()
        $attached = [CabinetViewerInput]::AttachThreadInput($currentThread, $foregroundThread, $true)
        try { [CabinetViewerInput]::SetForegroundWindow($viewer.MainWindowHandle) | Out-Null }
        finally {
            if ($attached) {
                [CabinetViewerInput]::AttachThreadInput($currentThread, $foregroundThread, $false) | Out-Null
            }
        }
        $origin = New-Object CabinetViewerInput+Point
        [CabinetViewerInput]::ClientToScreen($viewer.MainWindowHandle, [ref]$origin) | Out-Null
        $client = New-Object CabinetViewerInput+Rect
        [CabinetViewerInput]::GetClientRect($viewer.MainWindowHandle, [ref]$client) | Out-Null
        $clientX = [int]($X * $client.Right / $current.width)
        $clientY = [int]($Y * $client.Bottom / $current.height)
        [CabinetViewerInput]::SetCursorPos($origin.X + $clientX, $origin.Y + $clientY) | Out-Null
        Start-Sleep -Milliseconds 100
        [CabinetViewerInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 90
        [CabinetViewerInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds $Delay
    }
    $initial = Wait-State { param($s) -not $s.animating -and $s.progress -lt .001 }
    Start-Sleep -Seconds 1
    Capture '01-assembled'
    $scale = [Math]::Sqrt(($initial.width / 1600.0) * ($initial.height / 900.0))
    $buttonY = [int]($initial.height - 41 * $scale)
    Click ([int](82 * $scale)) $buttonY
    $midMotion = Wait-State {
        param($s) $s.animating -and $s.progress -ge .52 -and $s.progress -le .62
    }
    Capture '02-disassembly-midpoint'
    $moving = Wait-State { param($s) -not $s.animating -and $s.progress -ge .999 }
    Capture '03-exploded'
    $pickable = @($moving.parts | Where-Object { $_.pickable })
    if ($pickable.Count -lt 3) { throw "Too few assemblies are mouse-pickable: $($pickable.Count)" }
    $target = $pickable | Select-Object -First 1
    $clickY = [int]($moving.height - $target.pickPoint.y)
    Click ([int]$target.pickPoint.x) $clickY 140
    Click ([int]$target.pickPoint.x) $clickY 350
    $selected = Wait-State { param($s) $s.selectedPart -ge 0 -and $s.descriptionVisible -and $s.outlineVisible }
    Capture '04-component-selected'
    Click ([int](196 * $scale)) $buttonY
    $assembled = Wait-State { param($s) -not $s.animating -and $s.progress -lt .001 }
    if ($assembled.maxHomeError -gt .002) { throw "Assembly home error: $($assembled.maxHomeError)" }
    Capture '05-reassembled'
    Click ([int](82 * $scale)) $buttonY 100
    $reversalDelaySeconds = 4
    Start-Sleep -Seconds $reversalDelaySeconds
    Click ([int](196 * $scale)) $buttonY 100
    $reversed = Wait-State { param($s) -not $s.animating -and $s.progress -lt .001 }
    if ($reversed.maxHomeError -gt .002) { throw "Reversal home error: $($reversed.maxHomeError)" }
    Capture '06-reversed-to-assembled'
    $errors = @(Select-String -LiteralPath $logPath `
        -Pattern 'NullReferenceException|error CS[0-9]+|ArgumentException|InvalidOperationException' `
        -ErrorAction SilentlyContinue)
    if ($errors.Count) { throw "Player runtime errors: $($errors | Select-Object -First 3 | ForEach-Object Line)" }
    $result = [ordered]@{
        date = Get-Date -Format o
        executable = [IO.Path]::GetFullPath($Executable)
        resolution = "$($initial.width) x $($initial.height)"
        focusedAtCompletion = $moving.focused
        method = 'OS foreground mouse down/up against ordinary Player UI; physical Unity input and EventSystem enabled.'
        passed = $true
        tested = @('startup', 'mid-motion capture', 'full disassembly', 'double-click assembly selection',
            'outline and introduction', 'full reassembly', 'mid-motion reversal', 'home pose')
        reversalRequestedAfterSeconds = $reversalDelaySeconds
        configuredOneWaySeconds = 14
        pickableAssemblies = $pickable.Count
        selectedPart = $selected.selectedPart
        homeError = $reversed.maxHomeError
        renderReadySeconds = $initial.renderReadySeconds
        targetFrameRate = $initial.targetFrameRate
        displayRefreshHz = $initial.displayRefreshHz
        graphicsDevice = $initial.graphicsDevice
        motionPerformance = $reversed.motionPerformance
        runtimeErrors = 0
    }
    $result | ConvertTo-Json -Depth 7 |
        Set-Content -LiteralPath (Join-Path $OutputDirectory 'input-verification.json') -Encoding UTF8
    $result | ConvertTo-Json -Depth 7
} finally {
    if ($viewer -and -not $viewer.HasExited) {
        $viewer.CloseMainWindow() | Out-Null
        if (-not $viewer.WaitForExit(5000)) { Stop-Process -Id $viewer.Id }
    }
}
