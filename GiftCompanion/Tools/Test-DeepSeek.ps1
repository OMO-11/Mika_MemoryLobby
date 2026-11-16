param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$UnityEditor = Resolve-GiftUnityEditor -ProjectPath $projectPath -EditorPath $UnityEditor
$reportPath = Join-Path $projectPath 'Docs\Validation\Phase0'
New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
$unityArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'GiftCompanion.Editor.DeepSeekPhase0Probe.Run', '-logFile', ('"' + (Join-Path $reportPath 'deepseek_probe.log') + '"'))
$unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
$unityProcess.WaitForExit()
if ($unityProcess.ExitCode -eq 2) { Write-Output 'DeepSeek was not tested: local key is missing or unreadable.'; return }
if ($unityProcess.ExitCode -ne 0) { throw 'DeepSeek real request failed. See Phase0/deepseek_probe.txt.' }
Get-Content -LiteralPath (Join-Path $reportPath 'deepseek_probe.txt')
