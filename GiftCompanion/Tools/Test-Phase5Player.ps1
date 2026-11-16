param([switch]$WindowOnly)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$executable = Join-Path $projectPath 'Builds\Phase5\GiftCompanion.exe'
$reports = Join-Path $projectPath 'Docs\Validation\Phase5'
$dataPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('GiftCompanion\Validation\phase5-' + [Guid]::NewGuid().ToString('N'))
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build Phase5 first.' }
New-Item -ItemType Directory -Path (Join-Path $dataPath 'Config'),(Join-Path $dataPath 'ToolFixture') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $dataPath 'Config\settings.json'),'{"VoiceEnabled":false,"BgmVolume":0}',[Text.UTF8Encoding]::new($false))
$targetSource = Join-Path $reports 'ControlledTarget.cs'
[IO.File]::WriteAllText($targetSource,@'
using System;
using System.Windows.Forms;
class ControlledTarget {
    [STAThread] static void Main() {
        var form = new Form { Text = "GiftCompanion Phase5 controlled target", Width = 340, Height = 130 };
        form.Controls.Add(new Label { Text = "Owned validation window. Closes automatically.", Dock = DockStyle.Fill });
        var timer = new Timer { Interval = 45000 }; timer.Tick += (s,e) => form.Close(); timer.Start();
        Application.Run(form);
    }
}
'@,[Text.UTF8Encoding]::new($false))
$compiler = Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ('/out:' + (Join-Path $dataPath 'ControlledTarget.exe')) $targetSource
if ($LASTEXITCODE -ne 0) { throw 'Controlled target compilation failed.' }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Security
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class GiftPhase5Capture {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
    private delegate bool Callback(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback,IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window,StringBuilder name,int length);
    public static IntPtr Find(uint process) { IntPtr result=IntPtr.Zero; EnumWindows((w,p)=> {uint id;GetWindowThreadProcessId(w,out id); if(id!=process)return true; var name=new StringBuilder(128);GetClassName(w,name,128);if(name.ToString()!="UnityWndClass")return true;result=w;return false;},IntPtr.Zero); return result; }
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr window,ref Point point);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window,uint attribute,out Rect rect,int size);
    public static Rect PhysicalBounds(IntPtr window) { Rect rect; int code=DwmGetWindowAttribute(window,9,out rect,Marshal.SizeOf(typeof(Rect))); if(code!=0)Marshal.ThrowExceptionForHR(code);return rect; }
    public static void Place(IntPtr window,IntPtr backdrop) {
        IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
        try { SetWindowPos(window,new IntPtr(-1),84,84,600,500,0x50);SetWindowPos(backdrop,window,64,64,640,540,0x50); }
        finally { if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous); }
    }
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
    public static bool MoveOwned(IntPtr window,int x,int y) {
        IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
        try {SetCursorPos(x,y);System.Threading.Thread.Sleep(80);Point point=new Point {X=x,Y=y};return WindowFromPoint(point)==window;}
        finally {if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
    }
    public static void MouseButton(bool down) {mouse_event(down?2u:4u,0,0,0,UIntPtr.Zero);}
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    public static bool CornerHits(IntPtr backdrop,int x,int y) {
        IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));Point cursor;GetCursorPos(out cursor);
        try {SetCursorPos(x,y);System.Threading.Thread.Sleep(700);return WindowFromPoint(new Point {X=x,Y=y})==backdrop;}
        finally {SetCursorPos(cursor.X,cursor.Y);if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
    }
}
'@
$probeKey=$null
if(-not $WindowOnly){
    $bytes = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes((Join-Path $projectPath 'UserData\Secrets\deepseek.key.dpapi')),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
    try { $probeKey = [Text.Encoding]::UTF8.GetString($bytes) } finally { [Array]::Clear($bytes,0,$bytes.Length) }
}
[GiftPhase5Capture]::SetProcessDPIAware()|Out-Null
$previousDpi = [GiftPhase5Capture]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
$backdrop = New-Object Windows.Forms.Form
$backdrop.FormBorderStyle='None';$backdrop.StartPosition='Manual';$backdrop.Location=New-Object Drawing.Point(64,64);$backdrop.Size=New-Object Drawing.Size(640,540)
$backdrop.BackColor=[Drawing.Color]::FromArgb(17,105,179);$backdrop.TopMost=$true;$backdrop.ShowInTaskbar=$false
$process=$null
try {
    $modes=if($WindowOnly){@('Window')}else{@('Live','NoKey')}
    foreach ($mode in $modes) {
        foreach ($name in @('capture_ready.flag','capture_done.flag','gesture_prepare.flag','gesture_ready.json','gesture_done.flag','gesture_observed.flag')) { $flag=Join-Path $reports $name; if(Test-Path -LiteralPath $flag){Remove-Item -LiteralPath $flag} }
        $priorReport=Join-Path $reports ('player_'+$mode.ToLowerInvariant()+'.json');if(Test-Path -LiteralPath $priorReport){Remove-Item -LiteralPath $priorReport}
        $info=New-Object Diagnostics.ProcessStartInfo
        $info.FileName=$executable;$info.WorkingDirectory=$projectPath;$info.UseShellExecute=$false;$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
        $info.EnvironmentVariables['GIFT_VOICE_RUNTIME_ROOT']=Join-Path $projectPath 'Runtime\GPT-SoVITS'
        $info.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY')
        if($mode -eq 'Live'){$info.EnvironmentVariables['GIFT_DEEPSEEK_API_KEY']=$probeKey}
        $info.Arguments='-force-d3d11 -phase5-probe "'+$reports+'" '+$mode+' -validation-data "'+$dataPath+'" -logFile "'+(Join-Path $reports ('player_'+$mode.ToLowerInvariant()+'.log'))+'"'
        Write-Output "Phase5 $mode starting."
        $process=[Diagnostics.Process]::Start($info);$info.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY')
        $deadline=[DateTime]::UtcNow.AddMinutes(5)
        while(-not(Test-Path -LiteralPath (Join-Path $reports 'capture_ready.flag'))){
            if($process.HasExited -or [DateTime]::UtcNow -gt $deadline){throw "Phase5 $mode did not reach capture; see sanitized report."}
            [Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 100
        }
        $backdrop.Show();[Windows.Forms.Application]::DoEvents()
        $handle=[GiftPhase5Capture]::Find($process.Id);if($handle -eq [IntPtr]::Zero){throw 'No probe window.'}
        [GiftPhase5Capture]::Place($handle,$backdrop.Handle)
        $until=[DateTime]::UtcNow.AddSeconds(2);while([DateTime]::UtcNow -lt $until){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 50}
        # Moving between monitors can deliver WM_DPICHANGED and resize after the first call.
        [GiftPhase5Capture]::Place($handle,$backdrop.Handle)
        $until=[DateTime]::UtcNow.AddSeconds(1);while([DateTime]::UtcNow -lt $until){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 50}
        $rect=New-Object GiftPhase5Capture+Rect;$point=New-Object GiftPhase5Capture+Point
        [GiftPhase5Capture]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))|Out-Null
        $physical=[GiftPhase5Capture]::PhysicalBounds($handle);$point.X=$physical.Left;$point.Y=$physical.Top;$rect.Right=$physical.Right-$physical.Left;$rect.Bottom=$physical.Bottom-$physical.Top
        [ordered]@{left=$point.X;top=$point.Y;physicalWidth=$rect.Right;physicalHeight=$rect.Bottom}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $reports ('capture_bounds_'+$mode.ToLowerInvariant()+'.json')) -Encoding utf8
        if($rect.Right-ne 600 -or $rect.Bottom-ne 500){throw 'Probe physical client size differs from 600x500.'}
        [IO.File]::WriteAllText((Join-Path $reports 'gesture_prepare.flag'),'ready')
        $until=[DateTime]::UtcNow.AddSeconds(10)
        while(-not(Test-Path -LiteralPath (Join-Path $reports 'gesture_ready.json'))){if($process.HasExited -or [DateTime]::UtcNow-gt $until){throw 'Gesture preparation timed out.'};[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 40}
        $gesture=Get-Content -LiteralPath (Join-Path $reports 'gesture_ready.json') -Raw|ConvertFrom-Json
        $preview=New-Object Drawing.Bitmap(600,500);$previewGraphics=[Drawing.Graphics]::FromImage($preview)
        try{$previewGraphics.CopyFromScreen($point.X,$point.Y,0,0,$preview.Size);$preview.Save((Join-Path $reports 'pet_hit_preview.png'),[Drawing.Imaging.ImageFormat]::Png)}finally{$previewGraphics.Dispose();$preview.Dispose()}
        function Select-OwnedPetPoint($candidates){
            $bounds=[GiftPhase5Capture]::PhysicalBounds($handle)
            foreach($candidate in $candidates){$x=$bounds.Left+[int]$candidate.x;$y=$bounds.Top+[int]$candidate.y;if([GiftPhase5Capture]::MoveOwned($handle,$x,$y)){return [pscustomobject]@{X=$x;Y=$y}}}
            throw ('No opaque pet candidate pixel was found; candidates='+$candidates.Count)
        }
        $savedCursor=[Windows.Forms.Cursor]::Position
        try {
            $head=Select-OwnedPetPoint $gesture.heads
            [GiftPhase5Capture]::MouseButton($true);Start-Sleep -Milliseconds 130;[GiftPhase5Capture]::MouseButton($false);Start-Sleep -Milliseconds 600
            $body=Select-OwnedPetPoint $gesture.bodies
            [GiftPhase5Capture]::MouseButton($true);Start-Sleep -Milliseconds 130;[GiftPhase5Capture]::MouseButton($false);Start-Sleep -Milliseconds 600
            $head=Select-OwnedPetPoint $gesture.heads
            [GiftPhase5Capture]::MouseButton($true);Start-Sleep -Milliseconds 850;[GiftPhase5Capture]::MouseButton($false);Start-Sleep -Milliseconds 600
            $body=Select-OwnedPetPoint $gesture.bodies
            [GiftPhase5Capture]::MouseButton($true);Start-Sleep -Milliseconds 160
            for($step=1;$step-le 5;$step++){[GiftPhase5Capture]::MoveOwned($handle,($body.X+$step*12),($body.Y+$step*6))|Out-Null;Start-Sleep -Milliseconds 90}
            [GiftPhase5Capture]::MouseButton($false);Start-Sleep -Milliseconds 600
        } finally {[GiftPhase5Capture]::MouseButton($false);[Windows.Forms.Cursor]::Position=$savedCursor}
        [IO.File]::WriteAllText((Join-Path $reports 'gesture_done.flag'),'ready')
        $until=[DateTime]::UtcNow.AddSeconds(10)
        while(-not(Test-Path -LiteralPath (Join-Path $reports 'gesture_observed.flag'))){if($process.HasExited -or [DateTime]::UtcNow-gt $until){throw 'Gesture observation timed out.'};[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 40}
        [GiftPhase5Capture]::Place($handle,$backdrop.Handle)
        $until=[DateTime]::UtcNow.AddSeconds(1);while([DateTime]::UtcNow-lt $until){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 50}
        $physical=[GiftPhase5Capture]::PhysicalBounds($handle);$point.X=$physical.Left;$point.Y=$physical.Top
        $bitmap=New-Object Drawing.Bitmap(600,500);$graphics=[Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($point.X,$point.Y,0,0,$bitmap.Size);$bitmap.Save((Join-Path $reports ('pet_'+$mode.ToLowerInvariant()+'.png')),[Drawing.Imaging.ImageFormat]::Png)
            $matches=0
            foreach($position in @(@(8,8),@(591,8),@(8,491),@(591,491))){$color=$bitmap.GetPixel($position[0],$position[1]);if([Math]::Abs([int]$color.R-17)-le 2 -and [Math]::Abs([int]$color.G-105)-le 2 -and [Math]::Abs([int]$color.B-179)-le 2){$matches++}}
            $clickThrough=[GiftPhase5Capture]::CornerHits($backdrop.Handle,($point.X+8),($point.Y+8))
            [ordered]@{passed=($matches-ge 3 -and $clickThrough);backdropCorners=$matches;transparentCornerPassesToOwnedBackdrop=$clickThrough;clientWidth=$rect.Right;clientHeight=$rect.Bottom;scope='Only owned probe client over temporary solid backdrop; no external desktop capture.'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $reports ('composition_'+$mode.ToLowerInvariant()+'.json')) -Encoding utf8
        } finally {$graphics.Dispose();$bitmap.Dispose();$backdrop.Hide()}
        [IO.File]::WriteAllText((Join-Path $reports 'capture_done.flag'),'captured')
        # GPU counters are sampled only for the owned probe PID. No process commands or window titles are read.
        try {
            $gpu=Get-Counter '\GPU Engine(*)\Utilization Percentage' -SampleInterval 1 -MaxSamples 3 -ErrorAction Stop
            $samples=@($gpu|ForEach-Object {@($_.CounterSamples|Where-Object {$_.InstanceName -match ('^pid_'+$process.Id+'_')}|ForEach-Object {[ordered]@{engine=$_.InstanceName;percent=$_.CookedValue}})})
            $samples|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $reports ('gpu_'+$mode.ToLowerInvariant()+'.json')) -Encoding utf8
        } catch {[ordered]@{available=$false;reason='GPU performance counter unavailable on this host.'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $reports ('gpu_'+$mode.ToLowerInvariant()+'.json')) -Encoding utf8}
        $deadline=[DateTime]::UtcNow.AddSeconds(180)
        while(-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 100}
        if(-not $process.HasExited){throw 'Probe shutdown timed out.'}
        $result=Get-Content -LiteralPath (Join-Path $reports ('player_'+$mode.ToLowerInvariant()+'.json')) -Raw|ConvertFrom-Json
        $composition=Get-Content -LiteralPath (Join-Path $reports ('composition_'+$mode.ToLowerInvariant()+'.json')) -Raw|ConvertFrom-Json
        if($process.ExitCode-ne 0 -or -not $result.passed -or -not $composition.passed){throw "Phase5 $mode failed; see sanitized reports."}
        Write-Output "Phase5 $mode passed."
    }
    [ordered]@{dataPath=$dataPath;verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $reports 'player_run.json') -Encoding utf8
} finally {
    $probeKey=$null;$backdrop.Close();$backdrop.Dispose()
    if($null-ne $process -and -not $process.HasExited){$process.Kill()}
    if($previousDpi-ne [IntPtr]::Zero){[GiftPhase5Capture]::SetThreadDpiAwarenessContext($previousDpi)|Out-Null}
}
