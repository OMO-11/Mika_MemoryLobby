$ErrorActionPreference = "Stop"

$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
if (-not (Test-Path $edge)) {
    $edge = "C:\Program Files\Microsoft\Edge\Application\msedge.exe"
}
if (-not (Test-Path $edge)) {
    throw "Microsoft Edge was not found."
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$html = Join-Path $root "preview_v3.html"
$uri = [System.Uri]::new($html).AbsoluteUri
$profile = Join-Path $root ".edge-v3-profile-58b3b771-6c13-4bb0-955d-c2a653ef50b2"
$renderLog = Join-Path $root "v3_render.log"
New-Item -ItemType Directory -Force -Path $profile | Out-Null

$shots = @(
    @{ Name = "v3_idle.png"; Hash = "idle"; Size = "1280,720" }
    @{ Name = "v3_input.png"; Hash = "input"; Size = "1280,720" }
    @{ Name = "v3_dialogue.png"; Hash = "dialogue"; Size = "1280,720" }
    @{ Name = "v3_menu.png"; Hash = "menu"; Size = "1280,720" }
    @{ Name = "v3_diary.png"; Hash = "diary"; Size = "1280,720" }
    @{ Name = "v3_momotalk.png"; Hash = "momotalk"; Size = "1280,720" }
    @{ Name = "v3_music.png"; Hash = "music"; Size = "1280,720" }
    @{ Name = "v3_settings.png"; Hash = "settings"; Size = "1280,720" }
    @{ Name = "v3_compact.png"; Hash = "idle"; Size = "900,700" }
    @{ Name = "v3_diary_compact.png"; Hash = "diary"; Size = "900,700" }
)

Add-Content -Path $renderLog -Encoding UTF8 -Value "`n[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] Start rendering preview_v3.html"

foreach ($shot in $shots) {
    $output = Join-Path $root $shot.Name
    $target = "$uri#$($shot.Hash)"
    Add-Content -Path $renderLog -Encoding UTF8 -Value "Start: $($shot.Name), $($shot.Size), $target"

    $edgeArgs = @(
        "--headless=new",
        "--allow-file-access-from-files",
        "--disable-background-networking",
        "--disable-component-update",
        "--disable-extensions",
        "--no-first-run",
        "--no-default-browser-check",
        "--force-device-scale-factor=1",
        "--hide-scrollbars",
        "--run-all-compositor-stages-before-draw",
        "--virtual-time-budget=1400",
        "--window-size=$($shot.Size)",
        "--user-data-dir=$profile",
        "--screenshot=$output",
        $target
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & $edge @edgeArgs 2>&1 | Out-File -FilePath $renderLog -Append -Encoding utf8
    $edgeExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    if ($edgeExitCode -ne 0) {
        throw "Edge rendering failed: $($shot.Name), exit code $edgeExitCode"
    }
    if (-not (Test-Path $output)) {
        throw "Screenshot missing: $output"
    }

    $file = Get-Item $output
    Add-Content -Path $renderLog -Encoding UTF8 -Value "Done: $($file.FullName), $($file.Length) bytes"
    Write-Host "$($shot.Name)  $($file.Length) bytes"
}

Add-Content -Path $renderLog -Encoding UTF8 -Value "All screenshots completed. No files or profile directories were deleted."
