$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..\..')).Path
$workspaceRoot = Split-Path -Parent $projectRoot
if ($projectRoot -ne 'W:\Machine\VSCode\Mika_Gift\GiftCompanion') { throw 'Unexpected project root' }
$releaseRoot = Join-Path $projectRoot 'Releases'
$buildRoot = Join-Path $projectRoot 'Builds'
$keep = Join-Path $releaseRoot 'Gift-V1-Closeout'
$acceptancePath = Join-Path $projectRoot 'Docs\Validation\V1Closeout\acceptance.json'
$acceptance = Get-Content -LiteralPath $acceptancePath -Raw -Encoding utf8 | ConvertFrom-Json
if (-not (Test-Path -LiteralPath (Join-Path $keep 'GiftCompanion.exe'))) { throw 'Retained executable missing' }
$programs = @('GiftCompanion.exe','GiftCompanion_Data\Managed\GiftCompanion.Core.dll','GiftCompanion_Data\Managed\GiftCompanion.Runtime.dll','Release\release_manifest.json')
$beforeHashes = @{}
foreach ($relative in $programs) { $beforeHashes[$relative] = (Get-FileHash -LiteralPath (Join-Path $keep $relative) -Algorithm SHA256).Hash }
foreach ($artifact in $acceptance.artifacts) {
    $relative = $artifact.path.Replace('/', '\')
    if ($beforeHashes[$relative] -ne $artifact.sha256) { throw "Validated program mismatch: $relative" }
}
if ($beforeHashes['Release\release_manifest.json'] -ne $acceptance.package.manifestSha256) { throw 'Release manifest mismatch' }
$targets = @(
    (Join-Path $releaseRoot 'Gift-V1-Candidate'),
    (Join-Path $releaseRoot 'Gift-V1-Refined-Candidate')
)
$buildNames = @('Bilingual','ExpressionDiary','Phase0','Phase1','Phase2','Phase3','Phase3Plus','Phase4','Phase5','Phase6','V1Closeout','V1Refinement','VoicePolish')
foreach ($name in $buildNames) { $targets += Join-Path $buildRoot $name }
$records = @()
# Validate the entire explicit allowlist before deleting anything.
foreach ($target in $targets) {
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $resolved = (Resolve-Path -LiteralPath $target).Path
    $parent = Split-Path -Parent $resolved
    if ($parent -notin @($releaseRoot,$buildRoot) -or $resolved -eq $keep -or -not $resolved.StartsWith($workspaceRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe target: $resolved" }
    foreach ($ancestor in @($projectRoot,$releaseRoot,$buildRoot,$resolved)) {
        if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse ancestor: $ancestor" }
    }
    if (@(Get-ChildItem -LiteralPath $resolved -Recurse -Force -Attributes ReparsePoint).Count -gt 0) { throw "Reparse child: $resolved" }
    if (Test-Path -LiteralPath (Join-Path $resolved 'UserData')) { throw "Unexpected user data: $resolved" }
    $files = @(Get-ChildItem -LiteralPath $resolved -File -Recurse -Force)
    $records += [pscustomobject]@{ path=$resolved; files=$files.Count; bytes=($files | Measure-Object Length -Sum).Sum; deleted=$false }
}
$report = [ordered]@{
    date='2026-11-03'; timezone='Asia/Shanghai'; kept=$keep
    scope='Two obsolete release candidates and all 13 duplicate build directories, including Builds/V1Closeout; source, runtime, assets, user data and historical validation/design records retained.'
    entries=$records; retainedProgramHashesBefore=$beforeHashes; retainedProgramHashesAfter=@{}
    allTargetsDeleted=$false; keptHashesUnchanged=$false; deletedBytes=0; deletedFiles=0; error=$null
}
try {
    foreach ($record in $records) {
        Remove-Item -LiteralPath $record.path -Recurse -Force
        $record.deleted = -not (Test-Path -LiteralPath $record.path)
        if (-not $record.deleted) { throw "Deletion incomplete: $($record.path)" }
    }
    foreach ($relative in $programs) { $report.retainedProgramHashesAfter[$relative] = (Get-FileHash -LiteralPath (Join-Path $keep $relative) -Algorithm SHA256).Hash }
    $report.keptHashesUnchanged = @($programs | Where-Object { $beforeHashes[$_] -ne $report.retainedProgramHashesAfter[$_] }).Count -eq 0
    $report.allTargetsDeleted = @($records | Where-Object { -not $_.deleted }).Count -eq 0
    $report.deletedBytes = ($records | Where-Object deleted | Measure-Object bytes -Sum).Sum
    $report.deletedFiles = ($records | Where-Object deleted | Measure-Object files -Sum).Sum
    if (-not $report.keptHashesUnchanged) { throw 'Retained fingerprints changed' }
} catch { $report.error = $_.Exception.Message; throw }
finally {
    $report | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $PSScriptRoot '封装清理.json') -Encoding utf8
}
[pscustomobject]$report | Select-Object allTargetsDeleted,keptHashesUnchanged,deletedFiles,deletedBytes | ConvertTo-Json
