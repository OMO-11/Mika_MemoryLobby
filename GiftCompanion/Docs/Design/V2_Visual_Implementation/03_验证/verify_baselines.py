"""Read-only checks; writes evidence only beside this script, outside frozen inputs."""
from pathlib import Path
import hashlib
import json
import os
import sys
from datetime import datetime, timezone

project = Path(__file__).resolve().parents[4]
release = project / 'Releases/Gift-V1-Closeout'
visual = project / 'Docs/Design/MikaVisual_Baseline_V1_2026-11-02'
acceptance = json.loads((project / 'Docs/Validation/V1Closeout/acceptance.json').read_text(encoding='utf-8-sig'))

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def verify(root, manifest_path, hashes=True):
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    failures = []
    for entry in manifest['files']:
        relative = entry['path'].replace('\\', '/')
        target = root / relative
        if relative.startswith('/') or ':' in relative or '..' in relative.split('/'):
            failures.append({'path': entry['path'], 'reason': 'outside root'})
            continue
        try:
            size = os.stat(target).st_size
            if size != entry['bytes'] or (hashes and sha(target) != entry['sha256']):
                failures.append({'path': entry['path'], 'reason': 'size or SHA256 mismatch'})
        except FileNotFoundError:
            failures.append({'path': entry['path'], 'reason': 'missing'})
    return {'checked': len(manifest['files']), 'mode': 'size-and-sha256' if hashes else 'existence-and-size', 'passed': not failures, 'failures': failures}

manifest_path = release / 'Release/release_manifest.json'
result = {
    'createdAtUtc': datetime.now(timezone.utc).isoformat(),
    'release': verify(release, manifest_path, hashes='--full-release-hashes' in sys.argv),
    'visual': verify(visual, visual / '07_来源与清单/资源清单.json'),
    'releaseManifestMatchesAcceptance': sha(manifest_path) == acceptance['package']['manifestSha256'],
    'programsMatchAcceptance': all(sha(release / x['path']) == x['sha256'] for x in acceptance['artifacts']),
    'scope': 'Release existence/sizes plus manifest and three program SHA256; visual archive full SHA256. Full release payload hashes only with --full-release-hashes. No new Unity, API, voice or player acceptance tests.'
}
result['passed'] = all([result['release']['passed'], result['visual']['passed'], result['releaseManifestMatchesAcceptance'], result['programsMatchAcceptance']])
(Path(__file__).parent / 'baseline_integrity.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))
raise SystemExit(0 if result['passed'] else 1)
