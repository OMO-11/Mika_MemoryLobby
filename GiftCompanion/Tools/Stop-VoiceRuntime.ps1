param([ValidateSet('zh', 'ja')][string]$Language = 'zh')
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$recordPath = Join-Path $projectPath "UserData\Logs\voice-$Language-process.json"
if (-not (Test-Path -LiteralPath $recordPath)) { throw 'No voice process launched by this tool is recorded.' }
$record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
$voiceProcess = Get-Process -Id $record.pid -ErrorAction SilentlyContinue
if ($null -eq $voiceProcess) { Write-Output 'Recorded voice process has already exited.'; return }
$expected = Join-Path $projectPath 'Runtime\GPT-SoVITS\runtime\python.exe'
$recordedStart = [DateTime]$record.startTimeUtc
if ($voiceProcess.Path -ne $expected -or [Math]::Abs(($voiceProcess.StartTime.ToUniversalTime() - $recordedStart.ToUniversalTime()).TotalSeconds) -gt 1) { throw 'Recorded process identity does not match; nothing stopped.' }
Stop-Process -Id $voiceProcess.Id
if (-not $voiceProcess.WaitForExit(10000)) { throw 'Voice process is still exiting; retry after its GPU resources are released.' }
Write-Output "Stopped project voice process $($voiceProcess.Id)."
