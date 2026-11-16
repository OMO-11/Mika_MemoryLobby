param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$UnityEditor = Resolve-GiftUnityEditor -ProjectPath $projectPath -EditorPath $UnityEditor
$reports = Join-Path $projectPath 'Docs\Validation\Phase2'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$arguments = @('-batchmode','-nographics','-projectPath',('"' + $projectPath + '"'),
    '-executeMethod','GiftCompanion.Editor.Phase2CoreProbe.Run','-logFile',('"' + (Join-Path $reports 'core_probe.log') + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw 'Phase 2 Core probe failed; see core_acceptance.json.' }
Get-Content -LiteralPath (Join-Path $reports 'core_acceptance.json')
