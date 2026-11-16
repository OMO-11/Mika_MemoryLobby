"""Verify the release and make a ZIP64 delivery containing Inno Setup's disk slices.

No application/user data is read. Cleanup of historical builds is a separate action.
"""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile
from concurrent.futures import ThreadPoolExecutor

PROJECT = Path(__file__).resolve().parents[2]
REPORTS = PROJECT / 'Docs/Delivery/Installer-2.0.2-2026-11-14'
PAYLOAD = PROJECT / 'Releases/Gift-V2-Seaside'
KIT = PROJECT / 'Installers/GiftCompanion-2.0.2'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def write(name, value):
    REPORTS.mkdir(parents=True, exist_ok=True)
    (REPORTS / name).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def verify(root, report_name):
    manifest_path = root / 'Release/release_manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    if manifest['version'] != '2.0.2-local-candidate':
        raise RuntimeError('Unexpected release version')
    records = manifest['files']
    def verify_record(record):
        relative = Path(record['path'])
        path = root / relative
        if relative.is_absolute() or '..' in relative.parts or not path.resolve().is_relative_to(root.resolve()):
            raise RuntimeError('Unsafe manifest path')
        if path.stat().st_size != record['bytes'] or sha(path) != record['sha256']:
            raise RuntimeError('Payload mismatch: ' + record['path'])
    with ThreadPoolExecutor(max_workers=8) as executor:
        for index, _ in enumerate(executor.map(verify_record, records), 1):
            if index % 5000 == 0:
                print(f'Verified {index}/{len(records)} files', flush=True)
    actual = {p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
    expected = {r['path'] for r in records} | {'Release/release_manifest.json'}
    # Inno Setup's own uninstall data/log are added by installation, never by the release.
    extras = actual - expected
    if root == PAYLOAD and extras:
        raise RuntimeError('Unexpected release files: ' + repr(sorted(extras)))
    if root != PAYLOAD and any(not p.startswith('unins') for p in extras):
        raise RuntimeError('Unexpected installed files: ' + repr(sorted(extras)))
    if not expected <= actual:
        raise RuntimeError('Missing installed files')
    source_manifest = sha(PAYLOAD / 'Release/release_manifest.json')
    if sha(manifest_path) != source_manifest:
        raise RuntimeError('Installed manifest differs from source')
    result = dict(passed=True, root=str(root), version=manifest['version'], files=len(records),
                  bytes=sum(r['bytes'] for r in records), manifestSha256=source_manifest,
                  sha256Verified=True, installerFiles=sorted(extras))
    write(report_name, result)
    print(json.dumps(result), flush=True)


def archive():
    source = json.loads((REPORTS / 'source-verification.json').read_text(encoding='utf-8'))
    installed = json.loads((REPORTS / 'installed-verification.json').read_text(encoding='utf-8'))
    if not source['passed'] or not installed['passed'] or source['manifestSha256'] != installed['manifestSha256']:
        raise RuntimeError('Source and installed payload must pass first')
    parts = sorted(KIT.iterdir())
    if not any(p.suffix == '.exe' for p in parts) or not any(p.suffix == '.bin' for p in parts):
        raise RuntimeError('Installer disk slices are missing')
    output = KIT.parent / 'GiftCompanion-2.0.2-Windows-x64.zip'
    with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_STORED, allowZip64=True) as archive_file:
        for path in parts:
            if not path.is_file():
                raise RuntimeError('Unexpected installer subdirectory')
            archive_file.write(path, KIT.name + '/' + path.name)
    # Read every byte back, checking ZIP CRC and each part against its original SHA256.
    with zipfile.ZipFile(output) as archive_file:
        if archive_file.testzip() is not None:
            raise RuntimeError('ZIP CRC verification failed')
        for path in parts:
            with archive_file.open(KIT.name + '/' + path.name) as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != sha(path):
                    raise RuntimeError('ZIP member SHA256 mismatch')
    digest = sha(output)
    output.with_suffix('.zip.sha256').write_text(digest + '  ' + output.name + '\n', encoding='ascii')
    result = dict(passed=True, path=str(output), bytes=output.stat().st_size, sha256=digest,
                  zipCrcVerified=True, memberSha256Verified=True,
                  parts=[dict(name=p.name, bytes=p.stat().st_size, sha256=sha(p)) for p in parts])
    write('archive-verification.json', result)
    print(json.dumps(result), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['source', 'installed', 'archive'])
    parser.add_argument('--directory', type=Path)
    args = parser.parse_args()
    if args.action == 'source':
        verify(PAYLOAD, 'source-verification.json')
    elif args.action == 'installed':
        if not args.directory:
            parser.error('--directory is required')
        verify(args.directory.resolve(), 'installed-verification.json')
    else:
        archive()
