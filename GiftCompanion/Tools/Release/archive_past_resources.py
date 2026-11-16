"""Archive local-only authoring history and original voice distributions.

The private archive includes existing Windows DPAPI encrypted Editor credentials
at the owner's request. It never decrypts credentials. No Unity or browser cache.
"""
import hashlib
import json
import os
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'GiftCompanion/Tools/Release/.local/past-resources'
PART_BYTES = 1500 * 1024 * 1024


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    raw = subprocess.check_output(['git', 'ls-files', '--others', '--ignored', '--exclude-standard', '-z'], cwd=ROOT)
    selected = []
    for rel in raw.decode('utf-8').split('\0'):
        if not rel:
            continue
        pieces = rel.split('/')
        if any(x.lower() in {'library', 'logs', 'usersettings', '.vscode', '__pycache__'} for x in pieces[:-1]):
            continue
        keep = rel.startswith('Material/') or rel.startswith('GiftCompanion/UserData/')
        keep |= rel.startswith('GiftCompanion/Docs/') and not any(x.startswith('.edge-') for x in pieces) and '.browser-profile' not in rel
        if keep:
            path = ROOT / rel
            if path.is_symlink() or not path.is_file():
                raise RuntimeError('Unsafe archive path: ' + rel)
            selected.append(rel)
    if not selected:
        raise RuntimeError('No local-only resources selected')
    records = []
    archive = OUT / 'Mika-PastResources-2026-11-16.zip'
    stored = {'.7z', '.zip', '.png', '.jpg', '.jpeg', '.mp4', '.webm', '.gif', '.ogg', '.mp3', '.unitypackage'}
    with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=3, allowZip64=True) as target:
        for i, rel in enumerate(sorted(selected), 1):
            path = ROOT / rel
            digest = hashlib.sha256()
            info = zipfile.ZipInfo.from_file(path, arcname=rel)
            info.compress_type = zipfile.ZIP_STORED if path.suffix.lower() in stored else zipfile.ZIP_DEFLATED
            info._compresslevel = 3
            with path.open('rb') as src, target.open(info, 'w', force_zip64=True) as dst:
                while block := src.read(1024 * 1024):
                    digest.update(block)
                    dst.write(block)
            records.append(dict(path=rel, bytes=path.stat().st_size, sha256=digest.hexdigest()))
            if i % 500 == 0:
                print(f'Archived {i}/{len(selected)} files', flush=True)
        manifest = dict(label='过往资源', date='2026-11-16', files=records,
                        credentialFormat='Original Windows DPAPI encrypted blobs only; no decryption performed')
        target.writestr('PAST-RESOURCES-MANIFEST.json', json.dumps(manifest, ensure_ascii=False, indent=2))
    # Read every member to verify ZIP CRC and its SHA-256 against original bytes.
    with zipfile.ZipFile(archive) as check:
        for record in records:
            with check.open(record['path']) as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != record['sha256']:
                    raise RuntimeError('Archive mismatch: ' + record['path'])
    (OUT / 'MANIFEST.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    whole = hashlib.sha256()
    parts = []
    with archive.open('rb') as source:
        index = 1
        while True:
            block = source.read(1024 * 1024)
            if not block:
                break
            part = OUT / (archive.name + f'.part{index:03d}')
            digest = hashlib.sha256()
            size = 0
            with part.open('wb') as stream:
                while block:
                    stream.write(block)
                    digest.update(block)
                    whole.update(block)
                    size += len(block)
                    if size >= PART_BYTES:
                        break
                    block = source.read(min(1024 * 1024, PART_BYTES - size))
            parts.append(dict(name=part.name, bytes=size, sha256=digest.hexdigest()))
            print(f'Split {part.name}: {size} bytes', flush=True)
            index += 1
    result = dict(label='过往资源', archive=archive.name, archiveBytes=archive.stat().st_size,
                  archiveSha256=whole.hexdigest(), sourceFiles=len(records),
                  sourceBytes=sum(x['bytes'] for x in records), parts=parts, verified=True)
    (OUT / 'ARCHIVE.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    (OUT / 'SHA256SUMS.txt').write_text(''.join(f"{p['sha256']}  {p['name']}\n" for p in parts) +
                                     whole.hexdigest() + '  ' + archive.name + '\n', encoding='ascii')
    (OUT / 'RESTORE.ps1').write_text('''$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ARCHIVE.json') -Raw -Encoding utf8 | ConvertFrom-Json
$output = Join-Path $PSScriptRoot $manifest.archive
if (Test-Path -LiteralPath $output) { throw 'Archive already exists; refusing to overwrite.' }
foreach ($part in $manifest.parts) {
    $path = Join-Path $PSScriptRoot $part.name
    if ((Get-Item -LiteralPath $path).Length -ne $part.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $part.sha256) { throw ('Invalid part: ' + $part.name) }
}
$destination = [IO.File]::Create($output)
try {
    foreach ($part in $manifest.parts) {
        $source = [IO.File]::OpenRead((Join-Path $PSScriptRoot $part.name))
        try { $source.CopyTo($destination) } finally { $source.Dispose() }
    }
} finally { $destination.Dispose() }
if ((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash -ne $manifest.archiveSha256) { throw 'Merged archive SHA256 mismatch.' }
Write-Output ('Verified: ' + $output)
''', encoding='utf-8-sig')
    (OUT / 'README.md').write_text('''# 过往资源归档 · 2026-11-16

这里保存主源码仓库未收录的原始 GPT-SoVITS 发行包与角色模型压缩包、Blender 制作历史与检查点、预览及审阅资料、历史数据库和 Editor 本地资料。当前项目的源码与正式素材仍以 main 为准。

下载全部 `.partNNN`、ARCHIVE.json、RESTORE.ps1 和 SHA256SUMS.txt 到同一目录，在 PowerShell 执行 `powershell -ExecutionPolicy Bypass -File .\\RESTORE.ps1`。脚本逐个校验分片、合并 ZIP 并校验完整 ZIP 的 SHA-256。再用支持 ZIP64 的工具解压。不要逐个解压分片。

MANIFEST.json 列出归档内全部文件和 SHA-256。先恢复 main 的工程，再按需取用这里的历史资料；不要直接用旧版资料覆盖现有工程。合并和解压需要额外磁盘空间。

按所有者要求保留现有 Editor 配置及 Windows DPAPI 加密文件。未解密或转换为明文；DPAPI 绑定原 Windows 用户，换机时应重新配置模型服务密钥。该归档仅保存在原私有仓库中。
''', encoding='utf-8')
    print(json.dumps(result, ensure_ascii=True), flush=True)


if __name__ == '__main__':
    main()
