param(
    [string]$UnityEditor
)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$UnityEditor = Resolve-GiftUnityEditor -ProjectPath $projectPath -EditorPath $UnityEditor
$reportPath = Join-Path $projectPath 'Docs\Validation'
New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
foreach ($testMode in @('EditMode', 'PlayMode')) {
    $resultPath = Join-Path $reportPath "$testMode.xml"
    $logPath = Join-Path $reportPath "$testMode.log"
    $unityArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectPath + '"'),
        '-runTests', '-testPlatform', $testMode, '-testResults', ('"' + $resultPath + '"'),
        '-logFile', ('"' + $logPath + '"'))
    # Do not add -quit: the test runner quits after its asynchronous test run finishes.
    $unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
    $unityProcess.WaitForExit()
    if ($unityProcess.ExitCode -ne 0) { throw "$testMode failed. See $logPath" }
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "$testMode did not produce a report." }
    [xml]$report = Get-Content -LiteralPath $resultPath -Raw
    $result = $report.'test-run'
    if ($result.result -ne 'Passed' -or [int]$result.total -eq 0) { throw "$testMode did not pass." }
    Write-Output "$testMode passed: $($result.passed)/$($result.total) tests."
}
