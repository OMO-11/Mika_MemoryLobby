param([switch]$UsePlayerKey)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$executable = Join-Path $projectPath 'Builds\Phase2\GiftCompanion.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build Phase 2 first.' }
$info = New-Object Diagnostics.ProcessStartInfo
$info.FileName = $executable
$info.WorkingDirectory = $projectPath
$info.UseShellExecute = $false
# This launches the interactive app the user is about to operate, not a background helper.
$info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Normal
$info.Arguments = '-force-d3d11 -phase2-dev'
$editorSecret = Join-Path $projectPath 'UserData\Secrets\deepseek.key.dpapi'
$launchKey = $null
try {
    $info.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY')
    if (-not $UsePlayerKey -and (Test-Path -LiteralPath $editorSecret)) {
        Add-Type -AssemblyName System.Security
        $bytes = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($editorSecret),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
        try { $launchKey = [Text.Encoding]::UTF8.GetString($bytes) } finally { [Array]::Clear($bytes,0,$bytes.Length) }
        $info.EnvironmentVariables['GIFT_DEEPSEEK_API_KEY'] = $launchKey
    }
    $process = [Diagnostics.Process]::Start($info)
    Write-Output "Phase 2 development app started. PID: $($process.Id)"
} finally {
    $info.EnvironmentVariables.Remove('GIFT_DEEPSEEK_API_KEY')
    $launchKey = $null
}
