$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reports = Join-Path $project 'Docs/Delivery/Installer-2.0.2-2026-11-14'
foreach ($name in @('source-verification.json','installed-verification.json','installation-test.json','archive-verification.json')) {
    $evidence = Get-Content -LiteralPath (Join-Path $reports $name) -Raw | ConvertFrom-Json
    if (-not $evidence.passed) { throw ('Required verification did not pass: ' + $name) }
}
$archive = Get-Content -LiteralPath (Join-Path $reports 'archive-verification.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $archive.path -Algorithm SHA256).Hash -ne $archive.sha256) { throw 'Final installer archive changed' }
$latest = Join-Path $project 'Releases/Gift-V2-Seaside'
$latestHash = (Get-FileHash -LiteralPath (Join-Path $latest 'Release/release_manifest.json') -Algorithm SHA256).Hash
$source = Get-Content -LiteralPath (Join-Path $reports 'source-verification.json') -Raw | ConvertFrom-Json
if ($latestHash -ne $source.manifestSha256) { throw 'Latest release manifest changed' }
# Exact, user-authorized obsolete outputs. Source, materials, docs, latest release/build and user data are excluded.
$relativeTargets = @('Releases/Gift-V1-Closeout','Releases/Gift-V2-Candidate',
    'Builds/V2Final','Builds/V2Maintenance','Builds/V2Reading','Builds/V2Seaside','Builds/V2Window')
$inventory = @()
foreach ($relative in $relativeTargets) {
    $target = [IO.Path]::GetFullPath((Join-Path $project $relative))
    $boundary = if ($relative.StartsWith('Releases/')) { Join-Path $project 'Releases' } else { Join-Path $project 'Builds' }
    if (-not $target.StartsWith($boundary + '\',[StringComparison]::OrdinalIgnoreCase) -or $target -eq $latest) { throw 'Unsafe cleanup boundary' }
    if (-not (Test-Path -LiteralPath $target)) { throw ('Expected obsolete output is missing: ' + $target) }
    $resolved = (Resolve-Path -LiteralPath $target).Path
    if ($resolved -ne $target -or ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unexpected cleanup target resolution' }
    $entries = @(Get-ChildItem -LiteralPath $target -Recurse -Force)
    if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Cleanup refuses reparse points' }
    $files = @($entries | Where-Object { -not $_.PSIsContainer })
    $inventory += [ordered]@{path=$target;files=$files.Count;bytes=($files | Measure-Object -Property Length -Sum).Sum;removed=$false}
}
$inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $reports 'cleanup-before.json') -Encoding utf8
$retiredManifests = Join-Path $reports 'RetiredManifests'
New-Item -ItemType Directory -Force -Path $retiredManifests | Out-Null
foreach ($name in @('Gift-V1-Closeout','Gift-V2-Candidate')) {
    Copy-Item -LiteralPath (Join-Path $project ('Releases/' + $name + '/Release/release_manifest.json')) -Destination (Join-Path $retiredManifests ($name + '.json'))
}
foreach ($entry in $inventory) {
    Remove-Item -LiteralPath $entry.path -Recurse -Force
    $entry.removed = -not (Test-Path -LiteralPath $entry.path)
    if (-not $entry.removed) { throw ('Cleanup incomplete: ' + $entry.path) }
    Write-Output ('Removed obsolete output: ' + $entry.path)
}
$bytesRemoved = 0L
foreach ($entry in $inventory) { $bytesRemoved += [long]$entry.bytes }
[ordered]@{passed=$true;targets=$inventory;bytesRemoved=$bytesRemoved;
    latestReleasePreserved=(Test-Path -LiteralPath (Join-Path $latest 'GiftCompanion.exe'));
    latestBuildPreserved=(Test-Path -LiteralPath (Join-Path $project 'Builds/V2SeasideReview/GiftCompanion.exe'))} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reports 'cleanup.json') -Encoding utf8
