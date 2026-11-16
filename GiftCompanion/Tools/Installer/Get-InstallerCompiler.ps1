$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tools = Join-Path $project 'Tools/InstallerCompiler'
$reports = Join-Path $project 'Docs/Delivery/Installer-2.0.2-2026-11-14'
$download = Join-Path $tools 'innosetup-7.1.1-x64-complete.exe'
$portable = Join-Path $tools 'InnoSetup'
$temporaryInstallation = Join-Path $tools '.compiler-install'
$url = 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe'
New-Item -ItemType Directory -Force -Path $tools,$reports | Out-Null
if (-not (Test-Path -LiteralPath $download)) {
    & curl.exe --silent --show-error -L --fail --connect-timeout 15 --max-time 1200 --output $download $url
    if ($LASTEXITCODE -ne 0) { throw 'Official compiler download failed' }
}
$signature = Get-AuthenticodeSignature -LiteralPath $download
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.(,|$)') {
    throw 'Official compiler Authenticode signature or publisher did not verify'
}
if ((Test-Path -LiteralPath $portable) -or (Test-Path -LiteralPath $temporaryInstallation)) { throw 'Refusing to overwrite an existing compiler directory' }
$process = Start-Process -FilePath $download -ArgumentList @('/SP-','/CURRENTUSER','/VERYSILENT','/SUPPRESSMSGBOXES','/NOICONS','/NORESTART','/TASKS=""',
    ('/DIR="' + $temporaryInstallation + '"'),('/LOG="' + (Join-Path $reports 'compiler-install.log') + '"')) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000) -or $process.ExitCode -ne 0) { throw 'Compiler installation failed' }
try {
    New-Item -ItemType Directory -Path $portable | Out-Null
    Get-ChildItem -LiteralPath $temporaryInstallation -Force | Where-Object { $_.Name -notlike 'unins*' } |
        Copy-Item -Destination $portable -Recurse -Force
} finally {
    $uninstaller = Join-Path $temporaryInstallation 'unins000.exe'
    $process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',
        ('/LOG="' + (Join-Path $reports 'compiler-uninstall.log') + '"')) -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(180000) -or $process.ExitCode -ne 0) { throw 'Temporary compiler uninstall failed' }
}
$compiler = Join-Path $portable 'ISCC.exe'
if ((Get-AuthenticodeSignature -LiteralPath $compiler).Status -ne 'Valid') { throw 'Portable compiler signature failed' }
[ordered]@{passed=$true;source=$url;version='7.1.1';publisher=$signature.SignerCertificate.Subject;signatureStatus=[string]$signature.Status;
    downloadSha256=(Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash;compilerSha256=(Get-FileHash -LiteralPath $compiler -Algorithm SHA256).Hash;
    portableCompiler=$compiler;temporaryInstallationUninstalled=$true} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reports 'compiler-verification.json') -Encoding utf8
Write-Output $compiler
