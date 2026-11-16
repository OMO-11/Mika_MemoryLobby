param(
    [ValidateSet('Maintenance','Reading','Resize','Window','Entry','Prepare','Persist','Performance','Offline','Restored','Voice')][string]$Mode='Entry',
    [string]$Executable,
    [string]$ValidationData
)
$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if(-not $Executable){$Executable=Join-Path $projectPath 'Releases/Gift-V2-Candidate/GiftCompanion.exe'}
if(-not $ValidationData){$ValidationData=Join-Path $env:LOCALAPPDATA ('GiftCompanion/Validation/v2-final-'+[guid]::NewGuid().ToString('N'))}
$boundary=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'GiftCompanion/Validation'))+'\'
$ValidationData=[IO.Path]::GetFullPath($ValidationData)
if(-not $ValidationData.StartsWith($boundary,[StringComparison]::OrdinalIgnoreCase)){throw 'Expected isolated validation data.'}
$reports=Join-Path $projectPath ('Docs/Design/V2_Visual_Implementation/02_执行记录/V2-6/Native/'+$Mode)
New-Item -ItemType Directory -Path $reports -Force | Out-Null
if($Mode -in @('Offline','Restored','Voice')){
    & (Join-Path $PSScriptRoot 'Test-Phase6Player.ps1') -Mode $Mode -Executable $Executable -ValidationData $ValidationData -ReportName ('V2'+$Mode) -ReportDirectory $reports
    return
}
New-Item -ItemType Directory -Path (Join-Path $ValidationData 'Config') -Force | Out-Null
$settings=Join-Path $ValidationData 'Config/settings.json'
if(-not (Test-Path -LiteralPath $settings)){Set-Content -LiteralPath $settings -Value '{"VoiceEnabled":false}' -Encoding utf8}
$marker=switch($Mode){'Maintenance'{'-v2-maintenance-probe'}'Reading'{'-v2-reading-probe'}'Resize'{'-v2-resize-probe'}'Window'{'-v2-window-probe'}'Entry'{'-v1-closeout-probe'}default{'-v2-final-probe'}}
$start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=[IO.Path]::GetFullPath($Executable);$start.WorkingDirectory=[IO.Path]::GetDirectoryName($start.FileName)
$start.UseShellExecute=$false;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
$start.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY');$start.EnvironmentVariables.Remove('GIFT_VOICE_RUNTIME_ROOT')
$start.Arguments='-force-d3d11 -screen-width 1280 -screen-height 720 '+$marker+' "'+$reports+'" '+$Mode+' -validation-data "'+$ValidationData+'" -logFile "'+(Join-Path $reports 'player.log')+'"'
if($Mode -eq 'Resize'){$start.Arguments+=' -v2-final-matrix'}
$process=[Diagnostics.Process]::Start($start);$process.BeginOutputReadLine();$process.BeginErrorReadLine()
if(-not $process.WaitForExit(240000)){$process.Kill();throw 'Own final probe exceeded 240 seconds.'}
$file=Join-Path $reports $(if($Mode -in @('Prepare','Persist','Performance')){$Mode+'.json'}else{'native.json'})
$result=Get-Content -LiteralPath $file -Raw -Encoding utf8|ConvertFrom-Json
$result|ConvertTo-Json -Depth 8
if($process.ExitCode -ne 0 -or -not $result.passed){throw "V2 $Mode validation failed."}
Write-Output ('ValidationData='+$ValidationData)
