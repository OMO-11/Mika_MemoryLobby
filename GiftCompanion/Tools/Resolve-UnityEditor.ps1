function Resolve-GiftUnityEditor {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [string]$EditorPath
    )
    $versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
    $versionLine = Get-Content -LiteralPath $versionFile |
        Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
    if (-not $versionLine) { throw "ProjectVersion.txt does not specify a Unity version." }
    $requiredVersion = ($versionLine -replace '^m_EditorVersion:\s*', '').Trim()

    if (-not $EditorPath) {
        $editorCandidates = @(
            "W:\Machine\Unity\Unity Library\Unity $requiredVersion\Editor\Unity.exe",
            "W:\Machine\Unity\Unity $requiredVersion\Editor\Unity.exe",
            "C:\Program Files\Unity\Hub\Editor\$requiredVersion\Editor\Unity.exe"
        )
        $EditorPath = $editorCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
    }
    if (-not $EditorPath -or -not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
        throw "Unity $requiredVersion was not found. Pass -UnityEditor with the full Unity.exe path."
    }
    $resolvedEditor = (Resolve-Path -LiteralPath $EditorPath).Path
    $installedVersion = (Get-Item -LiteralPath $resolvedEditor).VersionInfo.ProductVersion.Split('_')[0]
    if ($installedVersion -ne $requiredVersion) {
        throw "Editor version $installedVersion does not match project version $requiredVersion."
    }
    Write-Output $resolvedEditor
}
