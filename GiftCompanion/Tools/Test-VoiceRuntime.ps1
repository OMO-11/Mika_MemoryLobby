param([int]$Port = 9880, [ValidateSet('zh', 'ja')][string]$Language = 'zh')
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$runtimePath = Join-Path $projectPath 'Runtime\GPT-SoVITS'
$voiceName = if ($Language -eq 'ja') { 'voice-ja.json' } else { 'voice.json' }
$voice = Get-Content -LiteralPath (Join-Path $runtimePath $voiceName) -Raw | ConvertFrom-Json
$reportPath = Join-Path $projectPath 'Docs\Validation\Phase0'
$outputPath = Join-Path $reportPath "tts_probe_$Language.wav"
New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
$readyDeadline = [DateTime]::UtcNow.AddSeconds(90)
do {
    $ready = $false
    try { Invoke-WebRequest -Uri "http://127.0.0.1:$Port/openapi.json" -TimeoutSec 2 | Out-Null; $ready = $true } catch { }
    if (-not $ready) { Start-Sleep -Milliseconds 500 }
} until ($ready -or [DateTime]::UtcNow -gt $readyDeadline)
if (-not $ready) { throw 'Voice API did not become ready within 90 seconds. Check voice logs.' }
$text = if ($Language -eq 'ja') { '先生、今日も一緒に過ごそうね。' } else { '老师，今天也一起度过吧。' }
$request = @{text=$text; text_lang=$voice.textLanguage; ref_audio_path=(Join-Path $runtimePath $voice.referenceAudio); prompt_text=$voice.promptText; prompt_lang=$voice.promptLanguage; text_split_method='cut5'; batch_size=1; media_type='wav'; streaming_mode=$false; seed=42}
$started = [DateTime]::UtcNow
Invoke-WebRequest -Uri "http://127.0.0.1:$Port/tts" -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json))) -OutFile $outputPath -TimeoutSec 120 | Out-Null
$bytes = [IO.File]::ReadAllBytes($outputPath)
if ($bytes.Length -lt 44 -or [Text.Encoding]::ASCII.GetString($bytes,0,4) -ne 'RIFF' -or [Text.Encoding]::ASCII.GetString($bytes,8,4) -ne 'WAVE') { throw 'TTS response is not a valid WAV container.' }
@{scope='Real local /tts HTTP response; audio quality requires listening.';passed=$true;language=$Language;text=$text;bytes=$bytes.Length;seconds=([DateTime]::UtcNow-$started).TotalSeconds} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportPath "tts_probe_$Language.json") -Encoding utf8
Write-Output "TTS returned a WAV container: $($bytes.Length) bytes. Listen to the file before accepting voice quality."
