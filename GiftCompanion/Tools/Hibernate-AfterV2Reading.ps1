$ErrorActionPreference = 'Stop'
$evidence = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Docs/Design/V2_Visual_Implementation/02_执行记录/V2-4'))
$statePath = Join-Path $evidence 'hibernate.json'
$state = [ordered]@{
    userRequested = $true
    action = 'hibernate'
    command = 'shutdown.exe /h'
    forcedClose = $false
    status = 'checking-completion'
    updatedAt = [DateTimeOffset]::Now.ToString('o')
}
function Save-State {
    $state.updatedAt = [DateTimeOffset]::Now.ToString('o')
    $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
}
try {
    $acceptance = Get-Content -LiteralPath (Join-Path $evidence 'acceptance.json') -Raw | ConvertFrom-Json
    if ($acceptance.passed -ne $true) { throw 'V2-3/4 completion evidence did not pass.' }
    $protection = Get-Content -LiteralPath (Join-Path $evidence 'protection.json') -Raw | ConvertFrom-Json
    if ($protection.passed -ne $true) { throw 'Baseline protection evidence did not pass.' }
    $state.status = 'scheduled'
    $state.delaySeconds = 12
    Save-State
    Start-Sleep -Seconds 12
    $state.status = 'requesting'
    Save-State
    $commandOutput = & "$env:SystemRoot\System32\shutdown.exe" /h 2>&1
    $commandExitCode = $LASTEXITCODE
    $state.exitCode = $commandExitCode
    $state.commandOutput = ($commandOutput | Out-String).Trim()
    $state.status = if ($commandExitCode -eq 0) { 'command-accepted' } else { 'command-failed' }
    Save-State
} catch {
    $state.status = 'failed'
    $state.error = $_.Exception.Message
    Save-State
    exit 1
}
