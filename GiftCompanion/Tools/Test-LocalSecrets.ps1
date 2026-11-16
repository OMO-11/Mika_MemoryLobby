$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
Add-Type -AssemblyName System.Security
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $projectPath 'Assets\App\Core\Config\LocalSecrets.cs') -Raw)
$validationPath = [IO.Path]::GetFullPath((Join-Path $projectPath 'UserData\Validation'))
$testPath = [IO.Path]::GetFullPath((Join-Path $validationPath ('dpapi-' + [Guid]::NewGuid().ToString('N'))))
if (-not $testPath.StartsWith($validationPath + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid test path.' }
$previousKey = $env:GIFT_DEEPSEEK_API_KEY
try {
    $env:GIFT_DEEPSEEK_API_KEY = $null
    $secretFolder = Join-Path $testPath 'Secrets'
    New-Item -ItemType Directory -Path $secretFolder -Force | Out-Null
    $expected = 'phase0-test-only-' + [Guid]::NewGuid().ToString('N')
    $bytes = [Text.Encoding]::UTF8.GetBytes($expected)
    try {
        $encrypted = [Security.Cryptography.ProtectedData]::Protect($bytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        [IO.File]::WriteAllBytes((Join-Path $secretFolder 'deepseek.key.dpapi'), $encrypted)
        $actual = [GiftCompanion.Core.Config.LocalSecrets]::ReadDeepSeekKey($testPath)
        if ($actual -ne $expected) { throw 'DPAPI round trip failed.' }
        @{passed=$true;scope='Synthetic value only: PowerShell DPAPI Protect to Core native Unprotect. No API request or real key used.'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectPath 'Docs\Validation\Phase0\local_secrets_probe.json') -Encoding utf8
        Write-Output 'Local secret encryption round trip passed using a synthetic test value.'
    } finally { [Array]::Clear($bytes, 0, $bytes.Length) }
} finally {
    $env:GIFT_DEEPSEEK_API_KEY = $previousKey
    if (Test-Path -LiteralPath $testPath) { Remove-Item -LiteralPath $testPath -Recurse -Force }
}
