param([ValidateSet('Editor','Player')][string]$Target = 'Editor')
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$dataPath = if ($Target -eq 'Player') { Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'GiftCompanion' } else { Join-Path $projectPath 'UserData' }
$secretPath = Join-Path $dataPath 'Secrets\deepseek.key.dpapi'
Write-Output "Configuration target: $Target ($dataPath)"
$secureKey = Read-Host 'DeepSeek API Key (hidden input)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
try {
    $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($plainKey)) { throw 'API Key is empty.' }
    Add-Type -AssemblyName System.Security
    $bytes = [Text.Encoding]::UTF8.GetBytes($plainKey.Trim())
    try {
        $encrypted = [Security.Cryptography.ProtectedData]::Protect($bytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
        New-Item -ItemType Directory -Path (Split-Path -Parent $secretPath) -Force | Out-Null
        [IO.File]::WriteAllBytes($secretPath,$encrypted)
    } finally { [Array]::Clear($bytes,0,$bytes.Length); $plainKey=$null }
} finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $secureKey.Dispose() }
Write-Output 'DeepSeek Key saved with Windows user-bound encryption. The key was not printed.'
