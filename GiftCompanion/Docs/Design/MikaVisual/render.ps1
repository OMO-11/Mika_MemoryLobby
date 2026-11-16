param(
    [string[]]$Views=@('dialogue','menu','diary','settings'),
    [ValidateSet('preview.html','revision2.html')][string]$HtmlFile='preview.html',
    [ValidateSet('','r2-')][string]$OutputPrefix=''
)
$ErrorActionPreference='Stop'
$edge='C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$html=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot $HtmlFile)).Path
$uri=([uri]$html).AbsoluteUri
$profileName='MikaVisual-'+[guid]::NewGuid().ToString('N')
$profileRoot=Join-Path ([IO.Path]::GetTempPath()) $profileName
foreach($view in $Views){
    if($view -notin @('idle','controls','dialogue','menu','diary','settings','music','home')){throw 'Unknown visual view.'}
    $png=Join-Path $PSScriptRoot ($OutputPrefix+$view+'.png')
    $renderStarted=Get-Date
    $arguments=@('--headless=new','--disable-gpu','--no-first-run','--disable-background-networking','--disable-component-update','--disable-sync','--hide-scrollbars','--allow-file-access-from-files','--force-device-scale-factor=1','--window-size=1440,1050','--virtual-time-budget=3000',('--user-data-dir="'+$profileRoot+'"'),('--screenshot="'+$png+'"'),('"'+$uri+'?view='+$view+'"'))
    $p=Start-Process -FilePath $edge -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    if($p.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $png) -or (Get-Item -LiteralPath $png).LastWriteTime -lt $renderStarted){throw 'A fresh visual screenshot was not created.'}
    Get-Item -LiteralPath $png | Select-Object Name,Length
}
$resolvedProfile=(Resolve-Path -LiteralPath $profileRoot).Path
$tempBoundary=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
if(-not $resolvedProfile.StartsWith($tempBoundary,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $resolvedProfile) -ne $profileName -or ((Get-Item -LiteralPath $resolvedProfile -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Unexpected temporary browser profile'}
Remove-Item -LiteralPath $resolvedProfile -Recurse -Force
