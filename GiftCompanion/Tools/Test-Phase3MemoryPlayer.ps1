$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$executable = Join-Path $projectPath 'Builds\Phase3\GiftCompanion.exe'
$reports = Join-Path $projectPath 'Docs\Validation\Phase3'
$validationRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'GiftCompanion\Validation'
$dataPath = Join-Path $validationRoot ('phase3-memory-' + [Guid]::NewGuid().ToString('N'))
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build Phase 3 first.' }
Add-Type -AssemblyName System.Security
$bytes = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes((Join-Path $projectPath 'UserData\Secrets\deepseek.key.dpapi')),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
try { $probeKey = [Text.Encoding]::UTF8.GetString($bytes) } finally { [Array]::Clear($bytes,0,$bytes.Length) }
try {
    foreach ($mode in @('Write','Restore','NoKey')) {
        $info = New-Object Diagnostics.ProcessStartInfo
        $info.FileName = $executable
        $info.WorkingDirectory = $projectPath
        $info.UseShellExecute = $false
        $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        $info.Arguments = '-force-d3d11 -phase2-probe "' + $reports + '" ' + $mode + ' -validation-data "' + $dataPath + '" -logFile "' + (Join-Path $reports ('player_' + $mode.ToLowerInvariant() + '.log')) + '"'
        if ($mode -ne 'NoKey') { $info.EnvironmentVariables['GIFT_DEEPSEEK_API_KEY'] = $probeKey }
        else { $info.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY') }
        $process = [Diagnostics.Process]::Start($info)
        $info.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY')
        if (-not $process.WaitForExit(330000)) { $process.Kill(); throw "Player $mode timed out." }
        if ($process.ExitCode -ne 0) { throw "Player $mode failed; see its sanitized report." }
        $result = Get-Content -LiteralPath (Join-Path $reports ('player_' + $mode.ToLowerInvariant() + '.json')) -Raw | ConvertFrom-Json
        if (-not $result.passed) { throw "Player $mode report failed." }
        Write-Output "Player $mode passed."
    }
} finally { $probeKey = $null }
[ordered]@{dataPath=$dataPath;verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reports 'player_run.json') -Encoding utf8
Write-Output "Isolated Player data retained: $dataPath"

