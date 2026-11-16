param([string]$Executable,[string]$ValidationData,[switch]$Resume,[string]$ReportDirectory)
$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if(-not $Executable){$Executable=Join-Path $projectPath 'Builds/V2Seaside/GiftCompanion.exe'}
if(-not $ValidationData){$ValidationData=Join-Path $env:LOCALAPPDATA ('GiftCompanion/Validation/seaside-'+[guid]::NewGuid().ToString('N'))}
$ValidationData=[IO.Path]::GetFullPath($ValidationData)
$boundary=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'GiftCompanion/Validation'))+'\'
if(-not $ValidationData.StartsWith($boundary,[StringComparison]::OrdinalIgnoreCase)){throw 'Expected isolated validation data.'}
$reports=if($ReportDirectory){[IO.Path]::GetFullPath($ReportDirectory)}else{Join-Path $projectPath ('Docs/Design/CH0294_Seaside_Integration_2026-11-06/Validation/'+$(if($Resume){'Resume'}else{'Native'}))}
New-Item -ItemType Directory -Path $reports -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ValidationData 'Config') -Force | Out-Null
$settings=Join-Path $ValidationData 'Config/settings.json'
if(-not (Test-Path -LiteralPath $settings)){Set-Content -LiteralPath $settings -Value '{"VoiceEnabled":false}' -Encoding utf8}
$start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=[IO.Path]::GetFullPath($Executable);$start.WorkingDirectory=[IO.Path]::GetDirectoryName($start.FileName)
$start.UseShellExecute=$false;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
$start.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY');$start.EnvironmentVariables.Remove('GIFT_VOICE_RUNTIME_ROOT')
$start.Arguments='-force-d3d11 -screen-width 1280 -screen-height 720 -seaside-probe "'+$reports+'" -validation-data "'+$ValidationData+'" -logFile "'+(Join-Path $reports 'player.log')+'"'
if($Resume){$start.Arguments+=' -seaside-resume'}
$process=[Diagnostics.Process]::Start($start)
if(-not $process.WaitForExit(180000)){$process.Kill();throw 'Own seaside probe exceeded 180 seconds.'}
$result=Get-Content -LiteralPath (Join-Path $reports 'native.json') -Raw -Encoding utf8|ConvertFrom-Json
$result|ConvertTo-Json -Depth 8
if($process.ExitCode -ne 0 -or -not $result.passed){throw 'Seaside validation failed.'}
Write-Output ('ValidationData='+$ValidationData)
