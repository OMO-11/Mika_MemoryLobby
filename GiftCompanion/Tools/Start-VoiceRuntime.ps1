param([int]$Port = 9880, [ValidateSet('zh', 'ja')][string]$Language = 'zh')
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$runtimePath = Join-Path $projectPath 'Runtime\GPT-SoVITS'
$pythonPath = Join-Path $runtimePath 'runtime\python.exe'
$apiPath = Join-Path $runtimePath 'api_v2.py'
$configName = if ($Language -eq 'ja') { 'tts_infer-ja.yaml' } else { 'tts_infer.yaml' }
$configPath = Join-Path $runtimePath $configName
foreach ($required in @($pythonPath, $apiPath, $configPath)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Voice runtime is not prepared: $required. See Docs/voice_runtime.md." }
}
if ($Port -lt 1024 -or $Port -gt 65535) { throw 'Invalid voice port.' }
$portCheck = [Net.Sockets.TcpClient]::new()
try {
    try { $portCheck.Connect('127.0.0.1', $Port) } catch [Net.Sockets.SocketException] { }
    if ($portCheck.Connected) { throw "Voice port $Port already has a listener; stop that service before starting another." }
} finally { $portCheck.Dispose() }
$logPath = Join-Path $projectPath 'UserData\Logs'
New-Item -ItemType Directory -Path $logPath -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $projectPath 'Docs\Validation\Phase0') -Force | Out-Null
$runner = Join-Path $PSScriptRoot 'voice_api_runner.py'
$voiceArguments = @('-u', '-s', ('"' + $runner + '"'), '-c', $configName, '-a', '127.0.0.1', '-p', $Port)
$previousPath = $env:PATH
$previousHub = $env:HF_HUB_OFFLINE
$previousTransformers = $env:TRANSFORMERS_OFFLINE
$previousTrace = $env:GIFT_VOICE_DEPENDENCY_REPORT
$previousEncoding = $env:PYTHONIOENCODING
try {
    $env:PATH = (Join-Path $runtimePath 'runtime') + ';' + $env:PATH
    $env:HF_HUB_OFFLINE = '1'
    $env:TRANSFORMERS_OFFLINE = '1'
    $env:PYTHONIOENCODING = 'utf-8'
    $env:GIFT_VOICE_DEPENDENCY_REPORT = Join-Path $projectPath "Docs\Validation\Phase0\voice_dependencies_$Language.json"
    $voiceProcess = Start-Process -FilePath $pythonPath -ArgumentList $voiceArguments -WorkingDirectory $runtimePath -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logPath "voice-$Language-stdout.log") -RedirectStandardError (Join-Path $logPath "voice-$Language-stderr.log")
} finally {
    $env:PATH = $previousPath
    $env:HF_HUB_OFFLINE = $previousHub
    $env:TRANSFORMERS_OFFLINE = $previousTransformers
    $env:GIFT_VOICE_DEPENDENCY_REPORT = $previousTrace
    $env:PYTHONIOENCODING = $previousEncoding
}
@{pid=$voiceProcess.Id;startTimeUtc=$voiceProcess.StartTime.ToUniversalTime().ToString('O');pythonPath=$pythonPath;language=$Language;port=$Port} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logPath "voice-$Language-process.json") -Encoding utf8
Write-Output "Voice process started: pid=$($voiceProcess.Id). API readiness still needs verification."
