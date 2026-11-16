$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reports = Join-Path $project 'Docs/Delivery/Installer-2.0.2-2026-11-14'
$setup = Join-Path $project 'Installers/GiftCompanion-2.0.2/GiftCompanion-2.0.2-Setup.exe'
$destination = Join-Path $project 'Temp/InstallerValidation-2.0.2'
$appKey = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/{A3FAF55E-6D04-4F7D-AE6F-B48276E48388}_is1'
$group = Join-Path ([Environment]::GetFolderPath('Programs')) 'GiftCompanion'
$dataRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'GiftCompanion'
$isolatedData = Join-Path $dataRoot ('Validation/installer-2.0.2-' + [guid]::NewGuid().ToString('N'))
if ((Test-Path -LiteralPath $destination) -or (Test-Path -LiteralPath $appKey) -or (Test-Path -LiteralPath $group)) {
    throw 'An existing installation or validation directory must not be overwritten by this test.'
}
function Get-FormalMetadataDigest {
    $records = [Collections.Generic.List[string]]::new()
    if (Test-Path -LiteralPath $dataRoot) {
        foreach ($entry in Get-ChildItem -LiteralPath $dataRoot -Force) {
            if ($entry.Name -eq 'Validation') { continue }
            $files = if ($entry.PSIsContainer) { Get-ChildItem -LiteralPath $entry.FullName -Recurse -File -Force } else { @($entry) }
            foreach ($file in $files) { $records.Add($file.FullName + '|' + $file.Length + '|' + $file.LastWriteTimeUtc.Ticks) }
        }
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes((($records | Sort-Object) -join "`n"))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}
function Run-SetupProcess([string]$File, [string[]]$Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(900000)) { throw 'Installer validation process exceeded 15 minutes.' }
    if ($process.ExitCode -ne 0) { throw ('Installer process failed with exit code ' + $process.ExitCode) }
}
$before = Get-FormalMetadataDigest
$checks = [ordered]@{}
try {
    Run-SetupProcess $setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/TASKS=""',
        ('/DIR="' + $destination + '"'), ('/LOG="' + (Join-Path $reports 'install.log') + '"'))
    $checks.installExitCodeZero = $true
    $registry = Get-ItemProperty -LiteralPath $appKey
    if ($registry.DisplayVersion -ne '2.0.2' -or $registry.InstallLocation.TrimEnd('\') -ne $destination.TrimEnd('\')) { throw 'Incorrect uninstall registration' }
    $checks.uninstallRegistrationCorrect = $true
    foreach ($shortcut in @('GiftCompanion.lnk','卸载 GiftCompanion.lnk')) {
        if (-not (Test-Path -LiteralPath (Join-Path $group $shortcut))) { throw ('Missing shortcut: ' + $shortcut) }
    }
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut((Join-Path $group 'GiftCompanion.lnk'))
    if ($link.TargetPath -ne (Join-Path $destination 'GiftCompanion.exe') -or $link.WorkingDirectory -ne $destination) { throw 'Incorrect application shortcut' }
    $checks.startMenuShortcutsCorrect = $true
    & python (Join-Path $PSScriptRoot 'Package-Installer.py') installed --directory $destination
    if ($LASTEXITCODE -ne 0) { throw 'Installed payload verification failed' }
    $checks.installedFilesSha256Verified = $true
    & (Join-Path $project 'Tools/Test-SeasidePlayer.ps1') -Executable (Join-Path $destination 'GiftCompanion.exe') -ValidationData $isolatedData -ReportDirectory (Join-Path $reports 'Native')
    $checks.installedApplicationPassed = $true
    & (Join-Path $project 'Tools/Test-SeasidePlayer.ps1') -Executable (Join-Path $destination 'GiftCompanion.exe') -ValidationData $isolatedData -ReportDirectory (Join-Path $reports 'Resume') -Resume
    $checks.installedRestartPassed = $true
} finally {
    $uninstaller = Join-Path $destination 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        Run-SetupProcess $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $reports 'uninstall.log') + '"'))
        for ($i=0; $i -lt 10 -and (Test-Path -LiteralPath $uninstaller); $i++) { Start-Sleep -Seconds 1 }
        if ((Test-Path -LiteralPath (Join-Path $destination 'GiftCompanion.exe')) -or (Test-Path -LiteralPath $appKey) -or (Test-Path -LiteralPath $group)) { throw 'Uninstallation left registered program files or shortcuts' }
        if ((Test-Path -LiteralPath $destination) -and (Get-ChildItem -LiteralPath $destination -Force)) { throw 'Uninstallation left files in the isolated program directory' }
        $checks.uninstallRemovedProgramAndRegistration = $true
    }
    $after = Get-FormalMetadataDigest
    $checks.formalUserFilesUnchanged = ($before -eq $after)
    $checks.isolatedValidationDataPreserved = Test-Path -LiteralPath $isolatedData
    $passed = $checks.Count -eq 9 -and -not ($checks.Values -contains $false)
    [ordered]@{passed=$passed;version='2.0.2';checks=$checks;formalMetadataBefore=$before;formalMetadataAfter=$after;validationData=$isolatedData} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reports 'installation-test.json') -Encoding utf8
}
if (-not $passed) { throw 'Installation lifecycle verification did not pass completely' }
Write-Output 'Installation, exact payload, application, restart, shortcuts, uninstall and user-data preservation passed.'
