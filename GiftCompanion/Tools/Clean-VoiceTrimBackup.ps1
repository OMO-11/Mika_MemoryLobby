$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$reportPath = Join-Path $projectPath 'Docs\Validation\Phase0'
$assembly = Get-Content -LiteralPath (Join-Path $reportPath 'voice_assembly.json') -Raw | ConvertFrom-Json
if (-not $assembly.passed) { throw 'Verify the trimmed voice bundle and both real inference outputs first.' }
$expected = [IO.Path]::GetFullPath((Join-Path $projectPath '.ToolsCache\VoiceTrim'))
if (-not (Test-Path -LiteralPath $expected)) { Write-Output 'No temporary voice trim backup remains.'; return }
$resolved = (Resolve-Path -LiteralPath $expected).Path
if ($resolved -ne $expected -or -not $resolved.StartsWith($projectPath + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected backup target; nothing removed.' }
if ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Backup target is a reparse point; nothing removed.' }
$links = Get-ChildItem -LiteralPath $resolved -Recurse -Force -Attributes ReparsePoint
if ($links) { throw 'Backup contains reparse points; nothing removed.' }
Remove-Item -LiteralPath $resolved -Recurse -Force
$trimPath = Join-Path $reportPath 'voice_trim.json'
$trim = Get-Content -LiteralPath $trimPath -Raw | ConvertFrom-Json
$trim | Add-Member -NotePropertyName backupDisposition -NotePropertyValue 'Temporary trim backup removed after both languages passed; original source archive remains untouched.' -Force
$trim | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $trimPath -Encoding utf8
Write-Output 'Temporary trim backup removed. Original voice archives and runtime are preserved.'
