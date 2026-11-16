"""Record current refinement evidence without rewriting earlier phase results."""
import hashlib
import json
from pathlib import Path
from datetime import datetime, timezone
import xml.etree.ElementTree as ET

project = Path(__file__).resolve().parents[1]
reports = project / 'Docs/Validation/V1Refinement'
release = project / 'Releases/Gift-V1-Refined-Candidate'

def read(name): return json.loads((reports / name).read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

tests = {}
for mode in ('EditMode', 'PlayMode'):
    path = reports / f'Final2.{mode}.xml'
    doc = ET.parse(path).getroot()
    tests[mode] = {'passed': doc.attrib['result'] == 'Passed', 'count': int(doc.attrib['passed']), 'sha256': sha(path)}
native = {name: read(path) for name, path in {'visual': 'VisualAfter/Refinement.json', 'offline': 'Standalone/Offline.json', 'restored': 'Standalone/Restored.json', 'voice': 'Voice/Voice.json'}.items()}
summary = read('package_summary.json')
manifest = release / 'Release/release_manifest.json'
checks = {
    'build': read('player_build.json')['passed'],
    'previousReleaseUnchanged': read('previous_release_verification.json')['passed'],
    'frozenAssetsUnchanged': read('frozen_verification.json')['passed'],
    'installationNotWrittenDuringUse': read('install_write_verification.json')['passed'],
    'manifestMatchesSummary': sha(manifest) == summary['manifestSha256'],
}
files = [release / 'GiftCompanion.exe', release / 'GiftCompanion_Data/Managed/GiftCompanion.Core.dll', release / 'GiftCompanion_Data/Managed/GiftCompanion.Runtime.dll', manifest]
sources = sorted(set(p for folder in ('Assets/App', 'Assets/Tests', 'Assets/Editor') for p in (project / folder).rglob('*') if p.is_file() and p.suffix in {'.cs', '.asmdef', '.shader'}))
result = {'passed': all(c['passed'] for c in tests.values()) and all(c['passed'] for c in native.values()) and all(checks.values()),
          'createdAtUtc': datetime.now(timezone.utc).isoformat(), 'playerAcceptance': 'pending', 'schema': 10, 'backupFormat': 1,
          'tests': tests, 'native': native, 'checks': checks, 'package': summary,
          'artifacts': [{'path': str(p.relative_to(project)), 'sha256': sha(p)} for p in files],
          'sources': [{'path': str(p.relative_to(project)), 'sha256': sha(p)} for p in sources],
          'validationReuse': 'Final2 suites passed before the last label-height-only change; final native display-label visibility and screenshots validate that change. No business logic changed afterward.',
          'limits': ['No new real DeepSeek conversation or full Key-save-restart player acceptance this round.',
                     'No claim of faster cold model loading, all-day stability or broad hardware support.',
                     'Phase5 player testing remains deferred; Phase8/9 reserved features not implemented.']}
(reports / 'acceptance.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'passed': result['passed'], 'tests': tests, 'nativeChecks': sum(len(v['flags']) for v in native.values()), 'checks': checks}, ensure_ascii=False))
if not result['passed']: raise RuntimeError('Refinement evidence is incomplete or failed.')
