$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sourcePath = Join-Path (Split-Path -Parent $projectPath) 'Material\Spine_Memory_BGM'
$ffmpeg = Join-Path $projectPath 'Runtime\GPT-SoVITS\runtime\ffmpeg.exe'
$outputPath = Join-Path $projectPath 'Assets\StreamingAssets\DefaultMusic'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$tracks = @(@{source='Daily Routine 247.mp4'; output='daily-routine-247.ogg'},@{source='You are my princess.mp4';output='you-are-my-princess.ogg'})
$report = foreach ($track in $tracks) {
    $source = Join-Path $sourcePath $track.source
    $output = Join-Path $outputPath $track.output
    & $ffmpeg -hide_banner -loglevel error -y -i $source -map 0:a:0 -vn -af 'loudnorm=I=-18:TP=-2:LRA=11' -ar 48000 -ac 2 -c:a libvorbis -q:a 5 $output
    if ($LASTEXITCODE -ne 0) { throw ('Audio extraction failed: ' + $track.source) }
    [ordered]@{source=$track.source;sourceSha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash;output=$track.output;outputSha256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash;bytes=(Get-Item -LiteralPath $output).Length;normalization='-18 LUFS, -2 dBTP, LRA 11';passed=$true}
}
$reports = Join-Path $projectPath 'Docs\Validation\Phase3Plus'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reports 'default_music.json') -Encoding utf8
Write-Output 'Two default music tracks extracted; original videos retained.'
