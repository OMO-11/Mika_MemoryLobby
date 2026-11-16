param([string]$Executable,[ValidateSet('Before','After')][string]$Checkpoint='After')
$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable=Join-Path $projectPath 'Builds\V1Refinement\GiftCompanion.exe' }
$reports=Join-Path $projectPath ('Docs\Validation\V1Refinement\Visual'+$Checkpoint)
$data=Join-Path $env:LOCALAPPDATA ('GiftCompanion\Validation\v1-refine-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $reports -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $data 'Config') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $data 'Config\settings.json') -Value '{"VoiceEnabled":false}' -Encoding utf8
Set-Content -LiteralPath (Join-Path $data 'Config\app_entry.json') -Value '{"Version":1,"StartupMode":"home","PetEntryEnabled":true}' -Encoding utf8
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName=[IO.Path]::GetFullPath($Executable); $start.WorkingDirectory=[IO.Path]::GetDirectoryName($start.FileName); $start.UseShellExecute=$false
$start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
$start.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY'); $start.EnvironmentVariables.Remove('GIFT_VOICE_RUNTIME_ROOT')
$start.Arguments='-force-d3d11 -phase6-probe "'+$reports+'" Refinement -validation-data "'+$data+'" -logFile "'+(Join-Path $reports 'player.log')+'"'
$process=[Diagnostics.Process]::Start($start)
$process.BeginOutputReadLine(); $process.BeginErrorReadLine()
@{pid=$process.Id; data=$data; executable=$start.FileName; mode='Refinement'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reports 'run.json') -Encoding utf8
$process.WaitForExit()
$result=Get-Content -LiteralPath (Join-Path $reports 'Refinement.json') -Raw | ConvertFrom-Json
$result | ConvertTo-Json -Depth 8
if ($process.ExitCode -ne 0 -or -not $result.passed) { throw 'Refinement native check failed; see player.log.' }
