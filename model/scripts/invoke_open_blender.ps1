param([int]$BlenderProcessId = 27160)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PumpBlenderWindow {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
'@
$blenderProcess = Get-Process -Id $BlenderProcessId
if ($blenderProcess.ProcessName -ne 'blender' -or $blenderProcess.MainWindowHandle -eq 0) {
    throw 'The requested Blender UI process was not found.'
}
$scriptPath = Join-Path $PSScriptRoot 'build_pump_blockout.py'
$consoleCommand = "import runpy; runpy.run_path(r'$scriptPath', run_name='__main__'); None"
$oldClipboard = [System.Windows.Forms.Clipboard]::GetText()
function Send-BlenderChord([byte]$Modifier, [byte]$Key) {
    [PumpBlenderWindow]::keybd_event($Modifier, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    [PumpBlenderWindow]::keybd_event($Key, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [PumpBlenderWindow]::keybd_event($Key, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [PumpBlenderWindow]::keybd_event($Modifier, 0, 2, [UIntPtr]::Zero)
}
try {
    [PumpBlenderWindow]::ShowWindow($blenderProcess.MainWindowHandle, 3) | Out-Null
    [PumpBlenderWindow]::SetForegroundWindow($blenderProcess.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    [PumpBlenderWindow]::SetCursorPos(600, 400) | Out-Null
    [PumpBlenderWindow]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [PumpBlenderWindow]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    Send-BlenderChord 0x10 0x73
    Start-Sleep -Milliseconds 900
    [System.Windows.Forms.Clipboard]::SetText($consoleCommand)
    Send-BlenderChord 0x11 0x56
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    Start-Sleep -Seconds 4
    Send-BlenderChord 0x10 0x74
    Start-Sleep -Milliseconds 700
    Send-BlenderChord 0x11 0x53
} finally {
    if ($oldClipboard) { [System.Windows.Forms.Clipboard]::SetText($oldClipboard) }
    else { [System.Windows.Forms.Clipboard]::Clear() }
}
Write-Output "Sent stage A build script to Blender process $BlenderProcessId."
