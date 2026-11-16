param([ValidateSet('Both','EditMode','PlayMode')][string]$Mode='Both',[string]$Checkpoint='Current')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Test-Phase3.ps1') -Mode $Mode -Checkpoint $Checkpoint -ReportDirectory (Join-Path $PSScriptRoot '..\Docs\Validation\Phase3Plus')
