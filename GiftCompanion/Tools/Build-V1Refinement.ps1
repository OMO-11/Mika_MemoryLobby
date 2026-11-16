param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$UnityEditor = Resolve-GiftUnityEditor -ProjectPath $projectPath -EditorPath $UnityEditor
$reports = Join-Path $projectPath 'Docs\Validation\V1Refinement'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$arguments = @('-batchmode','-nographics','-projectPath',('"' + $projectPath + '"'),'-executeMethod','GiftCompanion.Editor.V1RefinementBuild.Run','-logFile',('"' + (Join-Path $reports 'player_build.log') + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw 'V1 refinement build failed.' }
Get-Content -LiteralPath (Join-Path $reports 'player_build.json')
