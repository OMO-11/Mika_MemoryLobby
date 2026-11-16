param([string]$Executable,[string]$ReportDirectory)
$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if(-not $Executable){$Executable=Join-Path $projectPath 'Builds\V2Reading\GiftCompanion.exe'}
$reports=if($ReportDirectory){[IO.Path]::GetFullPath($ReportDirectory)}else{Join-Path $projectPath 'Docs\Design\V2_Visual_Implementation\02_执行记录\V2-4\Native'}
$data=Join-Path $env:LOCALAPPDATA ('GiftCompanion\Validation\v2-reading-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $reports -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $data 'Config') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $data 'Config\settings.json') -Value '{"VoiceEnabled":false}' -Encoding utf8
$start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=[IO.Path]::GetFullPath($Executable);$start.WorkingDirectory=[IO.Path]::GetDirectoryName($start.FileName)
$start.UseShellExecute=$false;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
$start.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY');$start.EnvironmentVariables.Remove('GIFT_VOICE_RUNTIME_ROOT')
$start.Arguments='-force-d3d11 -screen-width 1280 -screen-height 720 -v2-reading-probe "'+$reports+'" -validation-data "'+$data+'" -logFile "'+(Join-Path $reports 'player.log')+'"'
$process=[Diagnostics.Process]::Start($start);$process.BeginOutputReadLine();$process.BeginErrorReadLine()
if(-not $process.WaitForExit(60000)){$process.Kill();throw 'Own V2 reading probe exceeded 60 seconds.'}
$result=Get-Content -LiteralPath (Join-Path $reports 'native.json') -Raw -Encoding utf8 | ConvertFrom-Json;$result | ConvertTo-Json -Depth 6
if($process.ExitCode -ne 0 -or -not $result.passed){throw 'V2 reading validation failed; see Native/player.log.'}
