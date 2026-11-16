param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$UnityEditor = Resolve-GiftUnityEditor -ProjectPath $projectPath -EditorPath $UnityEditor
$reports = Join-Path $projectPath 'Docs\Validation\Phase1'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$arguments = @('-batchmode','-nographics','-projectPath',('"' + $projectPath + '"'),
    '-executeMethod','GiftCompanion.Editor.Phase1CoreProbe.Run','-logFile',('"' + (Join-Path $reports 'core_probe.log') + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw 'Phase 1 Core probe failed; see core_probe.json.' }
Get-Content -LiteralPath (Join-Path $reports 'core_probe.json')
