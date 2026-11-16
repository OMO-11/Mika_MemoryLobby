param([string]$Compiler)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $Compiler) { $Compiler = Join-Path $project 'Tools/InstallerCompiler/InnoSetup/ISCC.exe' }
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Install the official Inno Setup compiler or pass -Compiler.' }
$reports = Join-Path $project 'Docs/Delivery/Installer-2.0.2-2026-11-14'
$verified = Get-Content -LiteralPath (Join-Path $reports 'source-verification.json') -Raw | ConvertFrom-Json
$payload = Join-Path $project 'Releases/Gift-V2-Seaside'
$manifest = (Get-FileHash -LiteralPath (Join-Path $payload 'Release/release_manifest.json') -Algorithm SHA256).Hash
if (-not $verified.passed -or $manifest -ne $verified.manifestSha256) { throw 'Run Package-Installer.py source first.' }
$output = Join-Path $project 'Installers/GiftCompanion-2.0.2'
New-Item -ItemType Directory -Force -Path $output | Out-Null
& $Compiler "/DPayloadRoot=$payload" "/DInstallerOutput=$output" (Join-Path $PSScriptRoot 'GiftCompanion.iss') *> (Join-Path $reports 'compiler.log')
if ($LASTEXITCODE -ne 0) { throw ('Installer compilation failed. See ' + (Join-Path $reports 'compiler.log')) }
Get-ChildItem -LiteralPath $output -File | Select-Object Name, Length
