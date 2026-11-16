$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class GiftPhase6Displays {
    [StructLayout(LayoutKind.Sequential)] public struct Box { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct Info { public int Size; public Box Monitor, Work; public int Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string Device; }
    public sealed class Display { public string Device; public int Left,Top,Width,Height,WorkLeft,WorkTop,WorkWidth,WorkHeight; public uint Dpi; public double Scale; public bool Primary; }
    private delegate bool Callback(IntPtr monitor, IntPtr dc, ref Box box, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, Callback callback, IntPtr data);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern bool GetMonitorInfoW(IntPtr monitor, ref Info info);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr CreateWindowExW(uint ex, string cls, string name, uint style, int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    public static Display[] Inspect() {
        var result = new List<Display>(); var old = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try { EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr monitor, IntPtr dc, ref Box box, IntPtr data) => {
            var info = new Info { Size=Marshal.SizeOf(typeof(Info)) }; if(!GetMonitorInfoW(monitor, ref info)) return true;
            var window=CreateWindowExW(0,"STATIC","",0x80000000,info.Monitor.Left+10,info.Monitor.Top+10,1,1,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
            uint dpi=window==IntPtr.Zero?0:GetDpiForWindow(window); if(window!=IntPtr.Zero)DestroyWindow(window);
            result.Add(new Display { Device=info.Device, Left=info.Monitor.Left,Top=info.Monitor.Top,Width=info.Monitor.Right-info.Monitor.Left,Height=info.Monitor.Bottom-info.Monitor.Top,WorkLeft=info.Work.Left,WorkTop=info.Work.Top,WorkWidth=info.Work.Right-info.Work.Left,WorkHeight=info.Work.Bottom-info.Work.Top,Dpi=dpi,Scale=dpi/96d,Primary=(info.Flags&1)!=0 }); return true;
        },IntPtr.Zero); } finally { SetThreadDpiAwarenessContext(old); }
        return result.ToArray();
    }
}
'@
$result = [ordered]@{
    date = (Get-Date -Format o)
    os = (Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber,TotalVisibleMemorySize)
    cpu = (Get-CimInstance Win32_Processor | Select-Object Name,NumberOfLogicalProcessors)
    gpu = @(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion)
    displays = @([GiftPhase6Displays]::Inspect())
    note = 'Read-only enumeration. Hidden 1px probe windows never shown; no system DPI/resolution/monitor settings changed.'
}
$path = Join-Path $projectPath 'Docs\Validation\Phase6\hardware.json'
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding utf8
$result | ConvertTo-Json -Depth 8
