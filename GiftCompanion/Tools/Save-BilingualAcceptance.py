"""Offline verification of Bilingual release evidence; never reads keys or normal player data."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
REPORTS = ROOT / 'Docs/Validation/Bilingual'


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def read(name):
    return json.loads((REPORTS / name).read_text(encoding='utf-8-sig'))


def main():
    build = read('player_build.json')
    assert build['passed'] and build['errors'] == 0
    frozen = read('baseline_frozen.json')['files']
    for item in frozen:
        assert digest(ROOT / item['path']) == item['sha256'], item['path']
    tests = {}
    for mode in ('EditMode', 'PlayMode'):
        report = ET.parse(REPORTS / f'Final2.{mode}.xml').getroot()
        assert report.attrib['result'] == 'Passed' and int(report.attrib['failed']) == 0
        tests[mode] = dict(total=int(report.attrib['total']), passed=int(report.attrib['passed']))
    checks = {}
    built_at = (REPORTS / 'player_build.json').stat().st_mtime
    for mode in ('write', 'nokey'):
        data = read(f'player_{mode}.json')
        assert data['passed'] and all(data['flags'].values())
        at = datetime.datetime.fromisoformat(data['verifiedAtUtc']).timestamp()
        assert at > built_at, 'Player evidence must follow final build'
        checks[mode] = data
    visuals = read('visual_review.json')
    assert visuals['reviewed'] and len(visuals['images']) == 2
    for item in visuals['images']:
        assert digest(REPORTS / item['file']) == item['sha256']
    source = sorted((ROOT / 'Assets/App').rglob('*.cs')) + sorted((ROOT / 'Assets/Tests').rglob('*.cs')) + sorted((ROOT / 'Assets/Editor').rglob('*.cs'))
    tested_at = datetime.datetime.fromisoformat(ET.parse(REPORTS / 'Final2.EditMode.xml').getroot().attrib['start-time'].replace('Z', '+00:00')).timestamp()
    assert all(p.stat().st_mtime <= tested_at for p in source), 'Code changed after final tests'
    core = list((ROOT / 'Assets/App/Core').rglob('*.cs'))
    assert all(not re.search(r'using\s+Unity(?:Engine|Editor)', p.read_text(encoding='utf-8-sig')) for p in core)
    managed = ROOT / 'Builds/Bilingual/GiftCompanion_Data/Managed'
    assert not any('Tests' in p.name or 'nunit' in p.name.lower() for p in managed.glob('*.dll'))
    assert len(list((ROOT / 'Builds/Bilingual/Licenses').glob('*.txt'))) == 3
    assert not list((ROOT / 'Builds/Bilingual').rglob('*.dpapi'))
    assert not (ROOT / 'Builds/Bilingual/Runtime').exists()
    evidence = ['player_build.json', 'Final2.EditMode.xml', 'Final2.PlayMode.xml', 'player_write.json', 'player_nokey.json', 'player_run.json', 'comparison_text.json', 'comparison_zh.wav', 'comparison_ja.wav', 'visual_review.json', 'process_review.json']
    process = read('process_review.json')
    assert process['projectVoiceProcesses'] == 0 and process['bilingualPlayerProcesses'] == 0
    old_bytes = json.loads((ROOT / 'Docs/Validation/Phase4/player_build.json').read_text())['bytes']
    result = dict(passed=True, userListeningFeedback='pending', schemaVersion=9,
                  build=build, bytesAddedVsPhase4=build['bytes'] - old_bytes, tests=tests,
                  actualPlayerChecks=checks, runtimeCopied=False, frozenFilesVerified=len(frozen),
                  evidence=[dict(file=n, sha256=digest(REPORTS / n)) for n in evidence],
                  source=[dict(file=p.relative_to(ROOT).as_posix(), sha256=digest(p)) for p in source],
                  verifiedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (REPORTS / 'acceptance.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(passed=True, tests=tests, bytesAddedVsPhase4=result['bytesAddedVsPhase4'], frozenFilesVerified=len(frozen), sourceFiles=len(source)), ensure_ascii=False))


if __name__ == '__main__':
    main()
