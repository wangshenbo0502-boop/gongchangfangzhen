param(
    [string]$Player = 'D:\UnityProjects\WorkshopInspectionDemo\Build\WorkshopCombined\WorkshopSystem.exe',
    [string]$Evidence = ('D:\UnityProjects\WorkshopInspectionDemo\Evidence\NativeShortcuts-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WorkshopNativeInput
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public UIntPtr dwExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public UIntPtr dwExtraInfo;
    }
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION {
        [FieldOffset(0)] public MOUSEINPUT mouse;
        [FieldOffset(0)] public KEYBDINPUT keyboard;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION data; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window, ref POINT point);
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint key, uint mapType);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint count, INPUT[] inputs, int size);
    public static void Key(ushort key, bool up) {
        var input = new INPUT { type = 1 };
        input.data.keyboard.wScan = (ushort)MapVirtualKey(key, 0);
        input.data.keyboard.dwFlags = 8u | (up ? 2u : 0u);
        if (SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT))) != 1)
            throw new InvalidOperationException("Windows SendInput failed.");
    }
}
'@

[WorkshopNativeInput]::SetThreadDpiAwarenessContext([IntPtr](-4)) | Out-Null
[IO.Directory]::CreateDirectory($Evidence) | Out-Null
$playerProcess = $null
try {
    $playerProcess = Start-Process -FilePath $Player -ArgumentList @(
        '-windowed', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11',
        '-logFile', (Join-Path $Evidence 'player.log')
    ) -WindowStyle Normal -PassThru
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 250
        $playerProcess.Refresh()
        if ($playerProcess.HasExited) { throw 'Player exited before showing its window.' }
    } while ($playerProcess.MainWindowHandle -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline)
    if ($playerProcess.MainWindowHandle -eq [IntPtr]::Zero) { throw 'No player window.' }
    Start-Sleep -Seconds 12
    $window = $playerProcess.MainWindowHandle

    function Press-PlayerKey([ushort]$Key) {
        [WorkshopNativeInput]::ShowWindow($window, 9) | Out-Null
        [WorkshopNativeInput]::SetForegroundWindow($window) | Out-Null
        Start-Sleep -Milliseconds 300
        [WorkshopNativeInput]::Key($Key, $false)
        Start-Sleep -Milliseconds 150
        [WorkshopNativeInput]::Key($Key, $true)
        Start-Sleep -Milliseconds 700
    }

    function Capture-Player([string]$Name) {
        $rect = New-Object WorkshopNativeInput+RECT
        $origin = New-Object WorkshopNativeInput+POINT
        [WorkshopNativeInput]::GetClientRect($window, [ref]$rect) | Out-Null
        [WorkshopNativeInput]::ClientToScreen($window, [ref]$origin) | Out-Null
        $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bitmap.Size)
            $bitmap.Save((Join-Path $Evidence ($Name + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
        } finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }

    [WorkshopNativeInput]::SetForegroundWindow($window) | Out-Null
    Start-Sleep -Milliseconds 500
    Capture-Player 'assembly-visible'
    Press-PlayerKey 0x48
    Capture-Player 'assembly-hidden'
    Press-PlayerKey 0x48
    Capture-Player 'assembly-restored'
    Press-PlayerKey 0x32
    Start-Sleep -Seconds 3
    Capture-Player 'inspection-visible'
    Press-PlayerKey 0x48
    Capture-Player 'inspection-hidden'
    Press-PlayerKey 0x48
    Capture-Player 'inspection-restored'
    Press-PlayerKey 0x31
    Start-Sleep -Seconds 3
    Capture-Player 'assembly-returned'
    "Native input captures: $Evidence"
} finally {
    if ($playerProcess -and !$playerProcess.HasExited) {
        $playerProcess.CloseMainWindow() | Out-Null
        if (!$playerProcess.WaitForExit(10000)) { Stop-Process -Id $playerProcess.Id }
    }
}
