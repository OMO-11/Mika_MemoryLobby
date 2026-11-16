param(
    [ValidateSet('Offline','Restored','Voice','Stability')][string]$Mode = 'Offline',
    [string]$Executable,
    [string]$ValidationData,
    [string]$ReportName = 'Standalone',
    [string]$ReportDirectory
)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $Executable) { $Executable = Join-Path $projectPath 'Releases\Gift-V1-Candidate\GiftCompanion.exe' }
if (-not $ValidationData) { $ValidationData = Join-Path $env:LOCALAPPDATA ('GiftCompanion\Validation\phase6-' + [guid]::NewGuid().ToString('N')) }
$ValidationData = [IO.Path]::GetFullPath($ValidationData)
$boundary = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'GiftCompanion\Validation')) + '\'
if (-not $ValidationData.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Validation data must stay under the dedicated boundary.' }
if ($ReportName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid report name.' }
# Historical multi-mode probe explicitly opts into Home/pet only within its validated data boundary.
New-Item -ItemType Directory -Path (Join-Path $ValidationData 'Config') -Force | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $ValidationData 'Config\app_entry.json'))) {
    Set-Content -LiteralPath (Join-Path $ValidationData 'Config\app_entry.json') -Value '{"Version":1,"StartupMode":"home","PetEntryEnabled":true}' -Encoding utf8
}
$reports = if ($ReportDirectory) { [IO.Path]::GetFullPath($ReportDirectory) } else { Join-Path $projectPath ('Docs\Validation\Phase6\' + $ReportName) }
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = [IO.Path]::GetFullPath($Executable)
$start.WorkingDirectory = [IO.Path]::GetDirectoryName($start.FileName)
$start.UseShellExecute = $false
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY')
# Deliberately invalid override: a sealed release must ignore development runtime paths.
$start.EnvironmentVariables['GIFT_VOICE_RUNTIME_ROOT'] = 'Z:\not-a-development-runtime'
$start.Arguments = '-force-d3d11 -phase6-probe "' + $reports + '" ' + $Mode + ' -validation-data "' + $ValidationData + '" -logFile "' + (Join-Path $reports ($Mode + '.player.log')) + '"'
$process = [Diagnostics.Process]::Start($start)
@{ pid = $process.Id; startedAtUtc = [DateTime]::UtcNow.ToString('o'); executable = $start.FileName; validationData = $ValidationData; mode = $Mode } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reports ($Mode + '.run.json')) -Encoding utf8
$process.BeginOutputReadLine()
$process.BeginErrorReadLine()
$process.WaitForExit()
$resultFile = Join-Path $reports ($Mode + '.json')
if (-not (Test-Path -LiteralPath $resultFile)) { throw 'Probe did not produce a result. See the Player log.' }
$result = Get-Content -LiteralPath $resultFile -Raw | ConvertFrom-Json
$result | ConvertTo-Json -Depth 10
if ($process.ExitCode -ne 0 -or -not $result.passed) { throw "Phase6 $Mode probe failed." }
Write-Output ('ValidationData=' + $ValidationData)
