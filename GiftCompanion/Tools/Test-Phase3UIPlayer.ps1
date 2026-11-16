$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$executable = Join-Path $projectPath 'Builds\Phase3\GiftCompanion.exe'
$reports = Join-Path $projectPath 'Docs\Validation\Phase3'
$dataPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('GiftCompanion\Validation\phase3-ui-' + [Guid]::NewGuid().ToString('N'))
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build Phase 3 first.' }
Add-Type -AssemblyName System.Security
$bytes = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes((Join-Path $projectPath 'UserData\Secrets\deepseek.key.dpapi')),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
try { $probeKey = [Text.Encoding]::UTF8.GetString($bytes) } finally { [Array]::Clear($bytes,0,$bytes.Length) }
try {
    foreach ($mode in @('Write','NoKey')) {
        $info = New-Object Diagnostics.ProcessStartInfo
        $info.FileName = $executable
        $info.WorkingDirectory = $projectPath
        $info.UseShellExecute = $false
        $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        $info.Arguments = '-force-d3d11 -phase3-probe "' + $reports + '" ' + $mode + ' -validation-data "' + $dataPath + '" -logFile "' + (Join-Path $reports ('ui_' + $mode.ToLowerInvariant() + '.log')) + '"'
        $info.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY')
        if ($mode -eq 'Write') { $info.EnvironmentVariables['GIFT_DEEPSEEK_API_KEY'] = $probeKey }
        $process = [Diagnostics.Process]::Start($info)
        $info.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY')
        if (-not $process.WaitForExit(210000)) { $process.Kill(); throw "UI Player $mode timed out." }
        if ($process.ExitCode -ne 0) { throw "UI Player $mode failed; see sanitized report." }
        $result = Get-Content -LiteralPath (Join-Path $reports ('ui_' + $mode.ToLowerInvariant() + '.json')) -Raw | ConvertFrom-Json
        if (-not $result.passed) { throw "UI Player $mode report failed." }
        Write-Output "UI Player $mode passed."
    }
} finally { $probeKey = $null }
[ordered]@{dataPath=$dataPath;verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reports 'ui_run.json') -Encoding utf8
