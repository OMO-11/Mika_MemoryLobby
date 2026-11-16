param(
    [string]$UnityEditor
)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$UnityEditor = Resolve-GiftUnityEditor -ProjectPath $projectPath -EditorPath $UnityEditor
$reportPath = Join-Path $projectPath 'Docs\Validation'
New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
$unityArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'GiftCompanion.Editor.SkeletonProjectSetup.Run',
    '-logFile', ('"' + (Join-Path $reportPath 'setup.log') + '"'))
$unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -Wait -PassThru
if ($unityProcess.ExitCode -ne 0) { throw "Unity setup failed. See Docs\Validation\setup.log" }
Write-Output 'Skeleton scenes and build settings are ready.'
