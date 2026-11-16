$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$cachePath=Join-Path $projectPath '.ToolsCache'
New-Item -ItemType Directory -Path $cachePath -Force | Out-Null
Invoke-WebRequest -Uri 'https://www.sqlite.org/download.html' -OutFile (Join-Path $cachePath 'sqlite-download.html') -TimeoutSec 60
$row=Get-Content -LiteralPath (Join-Path $cachePath 'sqlite-download.html') | Where-Object {$_ -match '^PRODUCT,.*?/sqlite-dll-win-x64-\d+\.zip,'} | Select-Object -First 1
if (-not $row) {throw 'Official SQLite download entry not found.'}
$fields=$row.Split(',')
Invoke-WebRequest -Uri ('https://www.sqlite.org/'+$fields[2]) -OutFile (Join-Path $cachePath 'sqlite.zip') -TimeoutSec 60
Invoke-WebRequest -Uri 'https://api.github.com/repos/kirurobo/UniWindowController/commits/upm' -OutFile (Join-Path $cachePath 'uniwindow-commit.json') -TimeoutSec 60
$commit=(Get-Content -LiteralPath (Join-Path $cachePath 'uniwindow-commit.json') -Raw | ConvertFrom-Json).sha
Invoke-WebRequest -Uri "https://codeload.github.com/kirurobo/UniWindowController/zip/$commit" -OutFile (Join-Path $cachePath 'uniwindowcontroller.zip') -TimeoutSec 60
& python (Join-Path $PSScriptRoot 'fetch_phase0_dependencies.py') --from-cache
if ($LASTEXITCODE -ne 0) {throw 'Dependency verification/import failed.'}
