param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$UnityEditor = Resolve-GiftUnityEditor -ProjectPath $projectPath -EditorPath $UnityEditor
$reportPath = Join-Path $projectPath 'Docs\Validation\Phase0'
New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
$unityArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'GiftCompanion.Editor.Phase0Build.Run', '-logFile', ('"' + (Join-Path $reportPath 'player_build.log') + '"'))
$unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
$unityProcess.WaitForExit()
if ($unityProcess.ExitCode -ne 0) { throw "Phase 0 build failed. See $reportPath\player_build.log" }
Write-Output 'Phase 0 Windows build succeeded. Desktop window verification is still required.'
