param(
    [Parameter(Mandatory)][int]$PlayerId,
    [Parameter(Mandatory)][string]$Output,
    [string[]]$Actions = @('capture:screen'),
    [ValidatePattern('^[A-Za-z0-9_-]*$')][string]$Sequence = '',
    [ValidateRange(1,60)][double]$DurationSeconds = 20
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FaultMouse {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Mouse { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct Input { public uint Type; public Mouse Mouse; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct KeyInput {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort VirtualKey;
        [FieldOffset(10)] public ushort Scan;
        [FieldOffset(12)] public uint Flags;
    }
    [DllImport("user32.dll")] static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("user32.dll", EntryPoint = "SendInput")] static extern uint SendKeysInput(uint count, KeyInput[] input, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr process);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint source, uint target, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    public static bool Button(uint flags) {
        return SendInput(1, new [] { new Input { Mouse = new Mouse { Flags = flags } } }, Marshal.SizeOf<Input>()) == 1;
    }
    static bool Key(ushort key, uint flags) {
        return SendKeysInput(1, new [] { new KeyInput { Type = 1,
            Scan = (ushort)MapVirtualKey(key, 0), Flags = flags | 8 } },
            Marshal.SizeOf<KeyInput>()) == 1;
    }
    public static bool Paste() {
        bool ok = Key(0x11, 0) && Key(0x56, 0);
        System.Threading.Thread.Sleep(120);
        return Key(0x56, 2) && Key(0x11, 2) && ok;
    }
}
'@
[FaultMouse]::SetProcessDPIAware() | Out-Null
[FaultMouse]::SetThreadDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null
$player = Get-Process -Id $PlayerId
$window = $player.MainWindowHandle
if (!$window) { throw 'Player window unavailable' }
[FaultMouse]::ShowWindow($window, 9) | Out-Null
$current = [FaultMouse]::GetCurrentThreadId()
$front = [FaultMouse]::GetWindowThreadProcessId([FaultMouse]::GetForegroundWindow(), [IntPtr]::Zero)
if ($front) { [FaultMouse]::AttachThreadInput($current, $front, $true) | Out-Null }
try { [FaultMouse]::SetForegroundWindow($window) | Out-Null }
finally { if ($front) { [FaultMouse]::AttachThreadInput($current, $front, $false) | Out-Null } }
[FaultMouse]::SetWindowPos($window,[IntPtr]::new(-1),20,35,0,0,0x41) | Out-Null
New-Item -ItemType Directory -Path $Output -Force | Out-Null
Start-Sleep -Milliseconds 400
foreach ($action in $Actions) {
    $rect = New-Object FaultMouse+Rect
    $origin = New-Object FaultMouse+Point
    if (![FaultMouse]::GetClientRect($window,[ref]$rect) -or
        ![FaultMouse]::ClientToScreen($window,[ref]$origin)) { throw 'Client bounds unavailable' }
    if ($rect.Right -le 0 -or $rect.Bottom -le 0) { throw 'Player client is minimized or empty' }
    if ($action -match '^click:(\d+),(\d+)$') {
        [FaultMouse]::SetCursorPos($origin.X + [int]([int]$Matches[1] * $rect.Right / 1280),
            $origin.Y + [int]([int]$Matches[2] * $rect.Bottom / 720)) | Out-Null
        Start-Sleep -Milliseconds 150
        if (![FaultMouse]::Button(2)) { throw 'Mouse press rejected' }
        Start-Sleep -Milliseconds 120
        if (![FaultMouse]::Button(4)) { throw 'Mouse release rejected' }
        Start-Sleep -Milliseconds 650
    } elseif ($action -match '^text:([A-Za-z0-9 .,_-]+)$') {
        [Windows.Forms.SendKeys]::SendWait($Matches[1])
        Start-Sleep -Milliseconds 300
    } elseif ($action -match '^paste:([A-Za-z0-9 .,_-]+)$') {
        $inputText = $Matches[1]
        $previousClipboard = Get-Clipboard -Raw
        try {
            Set-Clipboard -Value $inputText
            if (![FaultMouse]::Paste()) { throw 'Paste keyboard input rejected' }
            Start-Sleep -Milliseconds 600
        } finally { Set-Clipboard -Value ([string]$previousClipboard) }
    } elseif ($action -match '^capture:([A-Za-z0-9_-]+)$') {
        $bitmap = [Drawing.Bitmap]::new($rect.Right,$rect.Bottom)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($origin.X,$origin.Y,0,0,$bitmap.Size)
            $bitmap.Save((Join-Path $Output ($Matches[1]+'.png')),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    } else { throw "Unknown action: $action" }
    "$(Get-Date -Format o) PID=$PlayerId $action" | Add-Content -LiteralPath (Join-Path $Output 'actions.txt')
}
if ($Sequence) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $frames = [Collections.Generic.List[object]]::new()
    while ($timer.Elapsed.TotalSeconds -lt $DurationSeconds) {
        $rect = New-Object FaultMouse+Rect
        $origin = New-Object FaultMouse+Point
        if (![FaultMouse]::GetClientRect($window,[ref]$rect) -or
            ![FaultMouse]::ClientToScreen($window,[ref]$origin) -or
            $rect.Right -le 0 -or $rect.Bottom -le 0) { throw 'Sequence client bounds unavailable' }
        $name = '{0}-motion-{1:D2}.png' -f $Sequence,$frames.Count
        $framePath = Join-Path $Output $name
        if (Test-Path -LiteralPath $framePath) { throw "Sequence frame already exists: $framePath" }
        $bitmap = [Drawing.Bitmap]::new($rect.Right,$rect.Bottom)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($origin.X,$origin.Y,0,0,$bitmap.Size)
            $bitmap.Save($framePath,[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
        $frames.Add([ordered]@{file=$name; elapsed=$timer.Elapsed.TotalSeconds})
        Start-Sleep -Milliseconds 480
    }
    $frames | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output ($Sequence+'-sequence.json')) -Encoding utf8
    Write-Output "$Sequence captured $($frames.Count) frames over $($timer.Elapsed.TotalSeconds.ToString('F2')) seconds"
}
