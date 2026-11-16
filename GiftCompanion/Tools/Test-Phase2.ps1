param([string]$UnityEditor, [ValidateSet('Both','EditMode','PlayMode')][string]$Mode = 'Both', [string]$Checkpoint = 'Current')
$ErrorActionPreference = 'Stop'
if ($Checkpoint -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid checkpoint name.' }
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$UnityEditor = Resolve-GiftUnityEditor -ProjectPath $projectPath -EditorPath $UnityEditor
$reportPath = Join-Path $projectPath 'Docs\Validation\Phase2'
New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
$modes = if ($Mode -eq 'Both') { @('EditMode','PlayMode') } else { @($Mode) }
foreach ($testMode in $modes) {
    $resultPath = Join-Path $reportPath "$Checkpoint.$testMode.xml"
    $logPath = Join-Path $reportPath "$Checkpoint.$testMode.log"
    $unityArguments = @('-batchmode','-nographics','-projectPath',('"' + $projectPath + '"'),
        '-runTests','-testPlatform',$testMode,'-testResults',('"' + $resultPath + '"'),'-logFile',('"' + $logPath + '"'))
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "$testMode failed. See $logPath" }
    [xml]$report = Get-Content -LiteralPath $resultPath -Raw
    if ($report.'test-run'.result -ne 'Passed' -or [int]$report.'test-run'.total -eq 0) { throw "$testMode did not pass." }
    Write-Output "$Checkpoint $testMode passed: $($report.'test-run'.passed)/$($report.'test-run'.total) tests."
}
