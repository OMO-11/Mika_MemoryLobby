param([string]$Executable, [string]$ReportDirectory, [string]$ValidationData)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$executable = if ($Executable) { $Executable } else { Join-Path $projectPath 'Builds\Phase0\GiftCompanion.exe' }
if (-not (Test-Path -LiteralPath $executable)) { throw 'Run Build-Phase0.ps1 first.' }
$reportPath = if ($ReportDirectory) { $ReportDirectory } else { Join-Path $projectPath 'Docs\Validation\Phase0' }
New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
foreach ($name in @('window_pet_ready.json', 'window_capture_done.flag', 'player_probe.json', 'window_composition.json')) {
    $old = Join-Path $reportPath $name
    if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old }
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class GiftProbeWindow {
    private delegate bool EnumCallback(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
    public static IntPtr FindPlayerWindow(uint playerId) {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hwnd, ignored) => {
            uint id; GetWindowThreadProcessId(hwnd, out id);
            if (id != playerId) return true;
            var name = new StringBuilder(128); GetClassName(hwnd, name, 128);
            if (name.ToString() != "UnityWndClass") return true;
            result = hwnd; return false;
        }, IntPtr.Zero);
        return result;
    }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
}
'@
[GiftProbeWindow]::SetProcessDPIAware() | Out-Null
$previousDpiContext = [GiftProbeWindow]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
$backdrop = New-Object Windows.Forms.Form
$backdrop.FormBorderStyle = 'None'
$backdrop.StartPosition = 'Manual'
$backdrop.Location = New-Object Drawing.Point(64, 64)
$backdrop.Size = New-Object Drawing.Size(640, 540)
$backdrop.BackColor = [Drawing.Color]::FromArgb(17, 105, 179)
$backdrop.ShowInTaskbar = $false
$backdrop.TopMost = $true
$player = $null
try {
    $backdrop.Show()
    [Windows.Forms.Application]::DoEvents()
    $arguments = @('-force-d3d11', '-screen-fullscreen', '0', '-phase0-window-probe', ('"' + $reportPath + '"'),
        '-logFile', ('"' + (Join-Path $reportPath 'player_probe.log') + '"'))
    if ($ValidationData) { $arguments += @('-validation-data', ('"' + $ValidationData + '"')) }
    $player = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(80)
    $readyPath = Join-Path $reportPath 'window_pet_ready.json'
    while (-not (Test-Path -LiteralPath $readyPath)) {
        if ($player.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw 'Player did not reach the pet probe.' }
        [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 100
    }
    $player.Refresh()
    $handle = [GiftProbeWindow]::FindPlayerWindow($player.Id)
    if ($handle -eq [IntPtr]::Zero) { throw 'No player window handle.' }
    if (-not [GiftProbeWindow]::SetWindowPos($handle, [IntPtr](-1), 84, 84, 600, 500, 0x50)) { throw 'Cannot position the probe window.' }
    [GiftProbeWindow]::SetWindowPos($backdrop.Handle, $handle, 64, 64, 640, 540, 0x50) | Out-Null
    # Pump the helper's messages while the desktop compositor draws several frames.
    $settle = [DateTime]::UtcNow.AddSeconds(2)
    while ([DateTime]::UtcNow -lt $settle) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 40 }
    $rect = New-Object GiftProbeWindow+Rect
    $point = New-Object GiftProbeWindow+Point
    if (-not [GiftProbeWindow]::GetClientRect($handle, [ref]$rect) -or -not [GiftProbeWindow]::ClientToScreen($handle, [ref]$point)) { throw 'Cannot measure player client area.' }
    if ($rect.Right -ne 600 -or $rect.Bottom -ne 500) { throw "Unexpected player client size: $($rect.Right)x$($rect.Bottom)." }
    $bitmap = New-Object Drawing.Bitmap(600, 500)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($point.X, $point.Y, 0, 0, $bitmap.Size)
        $bitmap.Save((Join-Path $reportPath 'window_composition.png'), [Drawing.Imaging.ImageFormat]::Png)
        $matches = 0
        $samples = @()
        foreach ($coordinate in @(@(8,8), @(591,8), @(8,491), @(591,491))) {
            $color = $bitmap.GetPixel($coordinate[0], $coordinate[1])
            $match = [Math]::Abs([int]$color.R-17) -le 2 -and [Math]::Abs([int]$color.G-105) -le 2 -and [Math]::Abs([int]$color.B-179) -le 2
            if ($match) { $matches++ }
            $samples += @{x=$coordinate[0];y=$coordinate[1];r=$color.R;g=$color.G;b=$color.B;backdropVisible=$match}
        }
        $composition = @{passed=($matches -ge 3);clientWidth=$rect.Right;clientHeight=$rect.Bottom;physicalPixelCoordinates=$true;backgroundSamplesMatched=$matches;samples=$samples;scope='Actual desktop composition, captured only inside the probe player over a temporary solid backdrop.'}
        $composition | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reportPath 'window_composition.json') -Encoding utf8
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
    [IO.File]::WriteAllText((Join-Path $reportPath 'window_capture_done.flag'), 'captured')
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while (-not $player.HasExited -and [DateTime]::UtcNow -lt $deadline) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 100 }
    if (-not $player.HasExited) { throw 'Player did not finish the probe.' }
    $result = Get-Content -LiteralPath (Join-Path $reportPath 'player_probe.json') -Raw | ConvertFrom-Json
    if (-not $composition.passed -or -not $result.passed) { throw 'Window probe failed. See Phase0 evidence.' }
    Write-Output 'Windows Player passed: desktop alpha composition, borderless/topmost pet, Home restoration, SQLite reopen and FTS5.'
} finally {
    $backdrop.Close()
    $backdrop.Dispose()
    if ($null -ne $player -and -not $player.HasExited) { $player.Kill() }
    if ($previousDpiContext -ne [IntPtr]::Zero) { [GiftProbeWindow]::SetThreadDpiAwarenessContext($previousDpiContext) | Out-Null }
}
