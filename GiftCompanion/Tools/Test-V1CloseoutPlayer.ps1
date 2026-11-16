param([string]$Executable)
$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable=Join-Path $projectPath 'Releases\Gift-V1-Closeout\GiftCompanion.exe' }
$reports=Join-Path $projectPath 'Docs\Validation\V1Closeout\Native'
$data=Join-Path $env:LOCALAPPDATA ('GiftCompanion\Validation\v1-closeout-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $reports -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $data 'Config') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $data 'Config\settings.json') -Value '{"VoiceEnabled":false}' -Encoding utf8
$start=[Diagnostics.ProcessStartInfo]::new(); $start.FileName=[IO.Path]::GetFullPath($Executable); $start.WorkingDirectory=[IO.Path]::GetDirectoryName($start.FileName); $start.UseShellExecute=$false
$start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
$start.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY'); $start.EnvironmentVariables.Remove('GIFT_VOICE_RUNTIME_ROOT')
$start.Arguments='-force-d3d11 -v1-closeout-probe "'+$reports+'" -validation-data "'+$data+'" -logFile "'+(Join-Path $reports 'player.log')+'"'
$process=[Diagnostics.Process]::Start($start); $process.BeginOutputReadLine(); $process.BeginErrorReadLine(); $process.WaitForExit()
$result=Get-Content -LiteralPath (Join-Path $reports 'native.json') -Raw | ConvertFrom-Json
$result | ConvertTo-Json -Depth 6
if ($process.ExitCode -ne 0 -or -not $result.passed) { throw 'Closeout native checks failed.' }
