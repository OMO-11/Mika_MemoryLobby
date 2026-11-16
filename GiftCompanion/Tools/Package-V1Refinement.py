"""Create a separate V1 refinement candidate; old releases and source Runtime stay read-only."""
import argparse
import hashlib
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
OLD = PROJECT / 'Releases/Gift-V1-Candidate'
DEST = PROJECT / 'Releases/Gift-V1-Refined-Candidate'
REPORT = PROJECT / 'Docs/Validation/V1Refinement'
BUILD = PROJECT / 'Builds/V1Refinement'
ROOTS = {'GiftCompanion.exe', 'GiftCompanion_Data', 'MonoBleedingEdge', 'UnityPlayer.dll', 'UnityCrashHandler64.exe', 'D3D12', 'Licenses'}
DOCS = {'Docs/v1_refinement_player_guide.md': 'README.md', 'Docs/phase6_compatibility.md': '兼容范围.md', 'Docs/v1_refinement_delivery.md': 'Release/交付记录.md',
        'Docs/v1_refinement_maintenance.md': '维护说明.md', 'Assets/StreamingAssets/Defaults/BackupGuide.md': '资料备份与迁移.md'}

def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''): h.update(block)
    return h.hexdigest()

def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')

def docs():
    for source, target in DOCS.items():
        p = DEST / target; p.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(PROJECT / source, p)

def assemble():
    if DEST.exists(): raise RuntimeError('New candidate already exists; keep the reviewed copy.')
    if shutil.disk_usage(PROJECT).free < 14 * 1024**3: raise RuntimeError('Need 14GiB available for a separate full candidate.')
    if not (BUILD / 'GiftCompanion.exe').is_file(): raise RuntimeError('Build the refinement candidate first.')
    old_manifest = OLD / 'Release/release_manifest.json'
    baseline = json.loads(old_manifest.read_text(encoding='utf-8'))
    write(REPORT / 'previous_release_baseline.json', {'manifestSha256': digest(old_manifest), 'runtimeFiles': sum(f['path'].startswith('Runtime/') for f in baseline['files'])})
    DEST.mkdir(parents=True)
    for name in sorted(ROOTS):
        src, target = BUILD / name, DEST / name
        if not src.exists():
            if name in {'D3D12', 'UnityCrashHandler64.exe'}: continue
            raise RuntimeError(f'Missing build root: {name}')
        if src.is_dir(): shutil.copytree(src, target)
        else: shutil.copy2(src, target)
    shutil.copytree(OLD / 'Licenses', DEST / 'Licenses', dirs_exist_ok=True)
    # Use the already validated portable runtime, without changing or pruning either original.
    shutil.copytree(OLD / 'Runtime', DEST / 'Runtime')
    docs()
    write(DEST / 'Release/release_manifest.json', {'version': '1.0.0-refined-candidate', 'stage': 'validating', 'schema': 10, 'backupFormat': 1, 'playerAcceptance': 'pending'})
    print('Separate candidate assembled; old release and runtime preserved.', flush=True)

def refresh_build():
    marker = DEST / 'Release/release_manifest.json'
    if not marker.is_file() or json.loads(marker.read_text(encoding='utf-8'))['version'] != '1.0.0-refined-candidate': raise RuntimeError('Only the refinement candidate may be refreshed.')
    for name in sorted(ROOTS):
        src, target = BUILD / name, DEST / name
        if not target.resolve().is_relative_to(DEST.resolve()): raise RuntimeError('Build destination escapes staging.')
        if not src.exists(): continue
        if src.is_dir(): shutil.copytree(src, target, dirs_exist_ok=True)
        else: shutil.copy2(src, target)
    print('Refinement build refreshed; runtime and previous release untouched.', flush=True)

def seal():
    if not (DEST / 'GiftCompanion.exe').is_file(): raise RuntimeError('Assemble first.')
    docs()
    old_manifest = OLD / 'Release/release_manifest.json'
    base = json.loads((REPORT / 'previous_release_baseline.json').read_text(encoding='utf-8'))
    if digest(old_manifest) != base['manifestSha256']: raise RuntimeError('Previous release manifest changed.')
    old = json.loads(old_manifest.read_text(encoding='utf-8'))
    # Verify all old release files, not only a handful of source assets.
    unchanged = all((OLD / f['path']).is_file() and digest(OLD / f['path']) == f['sha256'] for f in old['files'])
    write(REPORT / 'previous_release_verification.json', {'passed': unchanged, 'files': len(old['files']), 'manifestSha256': digest(old_manifest)})
    if not unchanged: raise RuntimeError('Previous candidate changed.')
    frozen = json.loads((PROJECT / 'Docs/Validation/Phase6/frozen_assets.json').read_text(encoding='utf-8'))['files']
    checks = [{'path': f['path'], 'unchanged': digest(PROJECT / f['path']) == f['sha256']} for f in frozen]
    write(REPORT / 'frozen_verification.json', {'passed': all(f['unchanged'] for f in checks), 'files': checks})
    if not all(f['unchanged'] for f in checks): raise RuntimeError('Frozen source asset or old acceptance changed.')
    files = []
    for p in sorted(DEST.rglob('*')):
        if not p.is_file() or p == DEST / 'Release/release_manifest.json': continue
        rel = p.relative_to(DEST)
        if p.is_symlink() or any(part in {'UserData', 'Secrets', 'Validation', 'Library', 'Material', 'Temp'} for part in rel.parts): raise RuntimeError(f'Unexpected release data: {rel}')
        if p.suffix == '.dpapi' or p.name in {'memory.db', 'GiftCompanion.EditModeTests.dll', 'GiftCompanion.PlayModeTests.dll'}: raise RuntimeError('Private or test data in release.')
        files.append({'path': rel.as_posix(), 'bytes': p.stat().st_size, 'sha256': digest(p)})
    old_runtime = {f['path']: f['sha256'] for f in old['files'] if f['path'].startswith('Runtime/')}
    if old_runtime != {f['path']: f['sha256'] for f in files if f['path'].startswith('Runtime/')}: raise RuntimeError('Runtime differs from the approved portable runtime.')
    result = {k: old[k] for k in ('pythonPackages', 'modelProfiles', 'renderer', 'unity', 'schema', 'backupFormat')}
    result.update(version='1.0.0-refined-candidate', stage='candidate', createdAtUtc=datetime.now(timezone.utc).isoformat(), playerAcceptance='pending', files=files,
                  bytes=sum(f['bytes'] for f in files), runtimeBytes=sum(f['bytes'] for f in files if f['path'].startswith('Runtime/')), developmentPathsRequired=False)
    write(DEST / 'Release/release_manifest.json', result)
    write(REPORT / 'package_summary.json', {k: v for k, v in result.items() if k != 'files'} | {'files': len(files), 'manifestSha256': digest(DEST / 'Release/release_manifest.json')})
    print(f"Sealed {len(files)} files; {result['bytes']/1024**3:.3f}GiB; old release verified unchanged.", flush=True)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('action', choices=['assemble', 'refresh-build', 'seal']); args = parser.parse_args()
    {'assemble': assemble, 'refresh-build': refresh_build, 'seal': seal}[args.action]()
