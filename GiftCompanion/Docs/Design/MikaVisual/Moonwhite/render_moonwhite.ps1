param(
    [string[]]$Sizes = @('wide','classic','compact'),
    [string[]]$Views = @()
)

$ErrorActionPreference = 'Stop'
$Sizes = @($Sizes | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Views = @($Views | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
if (-not (Test-Path -LiteralPath $edge)) {
    $edge = 'C:\Program Files\Microsoft\Edge\Application\msedge.exe'
}
if (-not (Test-Path -LiteralPath $edge)) {
    throw 'Microsoft Edge was not found.'
}

$root = $PSScriptRoot
$html = Join-Path $root 'preview_moonwhite.html'
if (-not (Test-Path -LiteralPath $html)) {
    throw "Prototype not found: $html"
}
$uri = ([uri]$html).AbsoluteUri
$log = Join-Path $root 'moonwhite_render.log'

$sets = @{
    wide = @(
        'idle','idle-clean','dialogue','input','thinking','menu','diary',
        'momotalk','music','space','persona','settings','home'
    )
    classic = @('idle','dialogue','menu','diary','momotalk','settings','home')
    compact = @('idle','dialogue','menu','diary','momotalk','settings','home')
}
$stageSizes = @{ wide = @(1280,720); classic = @(1024,768); compact = @(900,700) }

$allowedViews = $sets.wide
$plan = @()
foreach ($size in $Sizes) {
    if (-not $stageSizes.ContainsKey($size)) { throw "Unknown size: $size" }
    $list = if ($Views.Count -gt 0) { $Views } else { $sets[$size] }
    foreach ($view in $list) {
        if ($view -notin $allowedViews) { throw "Unknown view: $view" }
        $plan += [pscustomobject]@{ Size = $size; View = $view }
    }
}

$profileName = 'MikaMoonwhite-' + [guid]::NewGuid().ToString('N')
$profileRoot = Join-Path ([IO.Path]::GetTempPath()) $profileName
"$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') Moonwhite render started; source=$html; shots=$($plan.Count)" |
    Out-File -LiteralPath $log -Encoding utf8

try {
    foreach ($shot in $plan) {
        $view = $shot.View
        $size = $shot.Size
        $suffix = if ($size -eq 'wide') { '' } else { '_' + $size }
        $png = Join-Path $root ('mw_' + $view + $suffix + '.png')
        $url = $uri + '?view=' + $view + '&size=' + $size + '&bare=1'
        [int]$w = $stageSizes[$size][0]
        [int]$h = $stageSizes[$size][1]

        $arguments = @(
            '--headless=new'
            '--disable-gpu'
            '--no-first-run'
            '--no-default-browser-check'
            '--disable-background-networking'
            '--disable-component-update'
            '--disable-sync'
            '--disable-extensions'
            '--hide-scrollbars'
            '--allow-file-access-from-files'
            '--force-device-scale-factor=1'
            '--run-all-compositor-stages-before-draw'
            '--virtual-time-budget=2500'
            ('--window-size=' + $w + ',' + $h)
            ('--user-data-dir="' + $profileRoot + '"')
            ('--screenshot="' + $png + '"')
            ('"' + $url + '"')
        )

        $p = Start-Process -FilePath $edge -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $p.WaitForExit()
        if ($p.ExitCode -ne 0) { throw "Edge exited with code $($p.ExitCode): $view@$size" }
        if (-not (Test-Path -LiteralPath $png)) { throw "Screenshot was not created: $view@$size" }

        $item = Get-Item -LiteralPath $png
        "$(Get-Date -Format 'HH:mm:ss') $($item.Name) $($item.Length) bytes $($w)x$($h)" |
            Out-File -LiteralPath $log -Append -Encoding utf8
        $item | Select-Object Name, Length
    }
}
finally {
    if (Test-Path -LiteralPath $profileRoot) {
        try {
            $resolvedProfile = (Resolve-Path -LiteralPath $profileRoot).Path
            $tempBoundary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
            $isTemp = $resolvedProfile.StartsWith($tempBoundary, [StringComparison]::OrdinalIgnoreCase)
            $isOwn = (Split-Path -Leaf $resolvedProfile) -eq $profileName
            $isLink = ((Get-Item -LiteralPath $resolvedProfile -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
            if (-not $isTemp -or -not $isOwn -or $isLink) { throw 'Unexpected temporary browser profile; skipped cleanup.' }
            Remove-Item -LiteralPath $resolvedProfile -Recurse -Force
            "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') Temporary profile removed: $profileName" |
                Out-File -LiteralPath $log -Append -Encoding utf8
        }
        catch {
            "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') Profile cleanup skipped: $($_.Exception.Message)" |
                Out-File -LiteralPath $log -Append -Encoding utf8
        }
    }
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') Moonwhite render finished" |
        Out-File -LiteralPath $log -Append -Encoding utf8
}
