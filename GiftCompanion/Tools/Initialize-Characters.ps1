param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$UnityEditor = Resolve-GiftUnityEditor -ProjectPath $projectPath -EditorPath $UnityEditor
$reportPath = Join-Path $projectPath 'Docs\Validation\Phase0'
New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
$logPath = Join-Path $reportPath 'character_setup.log'
$unityArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'GiftCompanion.Editor.CharacterAssetSetup.Run', '-logFile', ('"' + $logPath + '"'))
$unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -Wait -PassThru
if ($unityProcess.ExitCode -ne 0) { throw "Character setup failed. See $logPath" }
Write-Output 'Character bindings initialized.'
