param([ValidateSet('Both','EditMode','PlayMode')][string]$Mode='Both',[string]$ReportDirectory)
$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Resolve-UnityEditor.ps1')
$editorPath=Resolve-GiftUnityEditor -ProjectPath $projectPath
$reports=if($ReportDirectory){[IO.Path]::GetFullPath($ReportDirectory)}else{Join-Path $projectPath 'Docs/Design/V2_Visual_Implementation/02_执行记录/V2-5'}
New-Item -ItemType Directory -Force -Path $reports | Out-Null
$modes=if($Mode -eq 'Both'){@('EditMode','PlayMode')}else{@($Mode)}
foreach($testMode in $modes){
    $result=Join-Path $reports "all.$testMode.xml"
    $log=Join-Path $reports "all.$testMode.log"
    $arguments=@('-batchmode','-force-d3d11','-projectPath',('"'+$projectPath+'"'),'-runTests','-testPlatform',$testMode,'-testResults',('"'+$result+'"'),'-logFile',('"'+$log+'"'))
    $process=Start-Process -FilePath $editorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw "$testMode failed. See $log"}
    [xml]$report=Get-Content -LiteralPath $result -Raw
    if($report.'test-run'.result -ne 'Passed' -or [int]$report.'test-run'.total -eq 0){throw "$testMode did not pass. See $result"}
    Write-Output "$testMode passed: $($report.'test-run'.passed)/$($report.'test-run'.total)"
}
