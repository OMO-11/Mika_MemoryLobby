"""Verify unchanged installer slices and remove only explicit obsolete outputs.

Run from the workspace root. --clean applies the verified cleanup plan.
"""
import argparse
import hashlib
import json
import os
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
REPORT = ROOT / 'GiftCompanion/Docs/Delivery/GitHub-2.0.2-2026-11-16'
KIT = ROOT / 'GiftCompanion/Installers/GiftCompanion-2.0.2'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--clean', action='store_true')
    args = parser.parse_args()
    prior = json.loads((ROOT / 'GiftCompanion/Docs/Delivery/Installer-2.0.2-2026-11-14/archive-verification.json').read_text(encoding='utf-8'))
    parts = []
    for item in prior['parts']:
        p = KIT / item['name']
        if p.stat().st_size != item['bytes'] or sha(p) != item['sha256']:
            raise RuntimeError('Installer mismatch: ' + p.name)
        parts.append(item)
        print('Verified ' + p.name, flush=True)
    REPORT.mkdir(parents=True, exist_ok=True)
    (KIT / 'SHA256SUMS.txt').write_text(''.join(f"{x['sha256']}  {x['name']}\n" for x in parts), encoding='utf-8')
    (REPORT / 'installer-verification.json').write_text(json.dumps(dict(passed=True, parts=parts), ensure_ascii=False, indent=2), encoding='utf-8')
    # These are generated review/test outputs, never authoring source or user data.
    targets = ['GiftCompanion/Builds/V2SeasideReview', 'Temp']
    for p in (ROOT / 'GiftCompanion/Assets').glob('InitTestScene*.unity'):
        targets += [p.relative_to(ROOT).as_posix(), p.relative_to(ROOT).as_posix() + '.meta']
    # The ZIP repeats the validated installer slices byte for byte.
    archive = ROOT / 'GiftCompanion/Installers/GiftCompanion-2.0.2-Windows-x64.zip'
    if archive.exists():
        if sha(archive) != prior['sha256']:
            raise RuntimeError('Archive changed; refusing to delete it')
        targets += [archive.relative_to(ROOT).as_posix(), archive.relative_to(ROOT).as_posix() + '.sha256']
    entries = []
    for rel in targets:
        p = (ROOT / rel).resolve()
        if not p.is_relative_to(ROOT) or p == ROOT or p.is_symlink():
            raise RuntimeError('Unsafe cleanup path: ' + rel)
        if not p.exists():
            continue
        files = []
        if p.is_dir():
            for parent, dirs, names in os.walk(p, followlinks=False):
                if any((Path(parent) / name).is_symlink() or (Path(parent) / name).is_junction() if hasattr(Path, 'is_junction') else (Path(parent) / name).is_symlink() for name in dirs):
                    raise RuntimeError('Linked directory in cleanup target')
                files += [Path(parent) / name for name in names]
        else:
            files = [p]
        entries.append(dict(path=rel, files=len(files), bytes=sum(f.stat().st_size for f in files)))
    result = dict(applied=args.clean, entries=entries, bytes=sum(x['bytes'] for x in entries))
    if args.clean:
        for entry in entries:
            p = ROOT / entry['path']
            shutil.rmtree(p) if p.is_dir() else p.unlink()
    (REPORT / 'cleanup.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=True), flush=True)


if __name__ == '__main__':
    main()
