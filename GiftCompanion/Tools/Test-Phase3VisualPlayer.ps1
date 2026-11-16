$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$executable = Join-Path $projectPath 'Builds\Phase3\GiftCompanion.exe'
$reports = Join-Path $projectPath 'Docs\Validation\Phase3'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build Phase 3 first.' }
$dataPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('GiftCompanion\Validation\phase3-visual-' + [Guid]::NewGuid().ToString('N'))
$info = New-Object Diagnostics.ProcessStartInfo
$info.FileName = $executable
$info.WorkingDirectory = $projectPath
$info.UseShellExecute = $false
$info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$info.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY')
$info.Arguments = '-force-d3d11 -phase3-probe "' + $reports + '" Visual -validation-data "' + $dataPath + '" -logFile "' + (Join-Path $reports 'release_visual.log') + '"'
$process = [Diagnostics.Process]::Start($info)
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Visual probe timed out.' }
if ($process.ExitCode -ne 0) { throw 'Visual probe failed; see release_visual.log.' }
$audit = Get-Content -LiteralPath (Join-Path $reports 'release_shader_audit.json') -Raw | ConvertFrom-Json
if ($audit.mainShader -ne 'GiftCompanion/SpineStraightAlpha' -or -not $audit.explicitShaderAvailable) { throw 'Scene alpha shader not applied.' }
Write-Output 'Visual comparison captured; inspect images before accepting appearance.'
