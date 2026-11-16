param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$UnityEditor = Resolve-GiftUnityEditor -ProjectPath $projectPath -EditorPath $UnityEditor
$reportPath = Join-Path $projectPath 'Docs\Validation\Phase0'
$resultPath = Join-Path $reportPath 'PlayMode.xml'
$logPath = Join-Path $reportPath 'PlayMode.log'
$unityArguments = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $projectPath + '"'),
    '-runTests', '-testPlatform', 'PlayMode', '-testResults', ('"' + $resultPath + '"'), '-logFile', ('"' + $logPath + '"'))
$unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -Wait -PassThru
if ($unityProcess.ExitCode -ne 0) { throw "Character tests failed. See $logPath" }
[xml]$report = Get-Content -LiteralPath $resultPath -Raw
if ($report.'test-run'.result -ne 'Passed') { throw 'Character tests did not pass.' }
Write-Output "Character PlayMode: $($report.'test-run'.passed)/$($report.'test-run'.total) passed."
