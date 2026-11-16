param([string[]]$Views=@('idle','idle-clean','dialogue','input','thinking','menu','diary','momotalk','music','space','character','settings','home','diary@compact','settings@compact','menu@classic'))
$ErrorActionPreference='Stop'
$edge='C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$html=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'preview_v2.html')).Path
$uri=([uri]$html).AbsoluteUri
$log=Join-Path $PSScriptRoot 'v2_render.log'
$profileName='MikaVisualV2-'+[guid]::NewGuid().ToString('N')
$profileRoot=Join-Path ([IO.Path]::GetTempPath()) $profileName
"$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') V2 render started; source=$html" | Out-File -LiteralPath $log -Encoding utf8
$allowed=@('idle','idle-clean','dialogue','input','thinking','menu','diary','momotalk','music','space','character','settings','home')
try {
    foreach($spec in $Views){
        $parts=$spec.Split('@')
        $view=$parts[0]
        $size=if($parts.Count -gt 1){$parts[1]}else{'wide'}
        if($view -notin $allowed){throw "Unknown visual view: $view"}
        if($size -notin @('wide','classic','compact')){throw "Unknown visual size: $size"}
        $suffix=if($size -eq 'wide'){''}else{'_'+$size}
        $png=Join-Path $PSScriptRoot ('v2_'+$view+$suffix+'.png')
        $url=$uri+'?view='+$view+'&size='+$size
        $arguments=@('--headless=new','--disable-gpu','--no-first-run','--disable-background-networking','--disable-component-update','--disable-sync','--hide-scrollbars','--allow-file-access-from-files','--force-device-scale-factor=1','--window-size=1440,1050','--virtual-time-budget=2500',('--user-data-dir="'+$profileRoot+'"'),('--screenshot="'+$png+'"'),('"'+$url+'"'))
        $p=Start-Process -FilePath $edge -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $p.WaitForExit()
        if(-not(Test-Path -LiteralPath $png)){throw "Visual screenshot was not created: $view@$size"}
        $item=Get-Item -LiteralPath $png
        "$($item.Name) $($item.Length) bytes" | Out-File -LiteralPath $log -Append -Encoding utf8
        $item | Select-Object Name,Length
    }
}
finally {
    if(Test-Path -LiteralPath $profileRoot){
        $resolvedProfile=(Resolve-Path -LiteralPath $profileRoot).Path
        $tempBoundary=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
        if(-not $resolvedProfile.StartsWith($tempBoundary,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $resolvedProfile) -ne $profileName -or ((Get-Item -LiteralPath $resolvedProfile -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Unexpected temporary browser profile'}
        Remove-Item -LiteralPath $resolvedProfile -Recurse -Force
    }
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') V2 render finished" | Out-File -LiteralPath $log -Append -Encoding utf8
}
