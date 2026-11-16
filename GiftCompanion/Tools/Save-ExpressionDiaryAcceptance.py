"""Verify release evidence offline without reading secrets or normal player data."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import wave
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
REPORTS = ROOT / 'Docs/Validation/ExpressionDiary'
BUILD = ROOT / 'Builds/ExpressionDiary'


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
    frozen = read('frozen_assets.json')['files']
    for item in frozen:
        assert digest(ROOT / item['path']) == item['sha256'], item['path']
    references = read('reference_assets.json')
    for item in references['references']:
        assert digest(ROOT / item['output']) == item['sha256']
        assert digest(ROOT.parent / 'Material/GPT-SoVITS' / item['source']) == item['sourceSha256']
        assert digest(BUILD / 'GiftCompanion_Data/StreamingAssets/Voice/References' / Path(item['output']).name) == item['sha256']
    tests = {}
    for mode in ('EditMode', 'PlayMode'):
        report = ET.parse(REPORTS / f'Final.{mode}.xml').getroot()
        assert report.attrib['result'] == 'Passed' and int(report.attrib['failed']) == 0
        tests[mode] = dict(total=int(report.attrib['total']), passed=int(report.attrib['passed']))
    checks = {}
    built_at = (REPORTS / 'player_build.json').stat().st_mtime
    for mode in ('write', 'nokey'):
        data = read(f'player_{mode}.json')
        assert data['passed'] and all(data['flags'].values()), data
        assert datetime.datetime.fromisoformat(data['verifiedAtUtc']).timestamp() > built_at
        checks[mode] = data
    layout = read('diary_layout.json')
    assert layout['paragraphCount'] >= 2 and layout['paragraphCount'] == len(layout['paragraphLengths'])
    assert all(0 < n <= 260 for n in layout['paragraphLengths'])
    assert layout['maximumKaomoji'] == 4
    samples = read('sample_texts.json')
    assert {s['intent'] for s in samples} == {'annoyed', 'sad', 'hesitant', 'flat'}
    audio = []
    for sample in samples:
        assert sample['ReferenceId'] == 'ja-' + sample['intent']
        assert sample['chinese'] and sample['japanese'] and not sample['qualityAccepted']
        name = 'ja_' + sample['intent'] + '.wav'
        with wave.open(str(REPORTS / name), 'rb') as stream:
            assert stream.getframerate() == 48000 and stream.getnchannels() == 1
            assert stream.getsampwidth() == 2 and stream.getnframes() > 0
            audio.append(dict(file=name, seconds=stream.getnframes() / stream.getframerate()))
    source = sorted((ROOT / 'Assets/App').rglob('*.cs')) + sorted((ROOT / 'Assets/Tests').rglob('*.cs')) + sorted((ROOT / 'Assets/Editor').rglob('*.cs'))
    tested_at = datetime.datetime.fromisoformat(ET.parse(REPORTS / 'Final.EditMode.xml').getroot().attrib['start-time'].replace('Z', '+00:00')).timestamp()
    assert all(p.stat().st_mtime <= tested_at for p in source), 'Code changed after final tests'
    assert all(not re.search(r'using\s+Unity(?:Engine|Editor)', p.read_text(encoding='utf-8-sig')) for p in (ROOT / 'Assets/App/Core').rglob('*.cs'))
    assert not any('Tests' in p.name or 'nunit' in p.name.lower() for p in (BUILD / 'GiftCompanion_Data/Managed').glob('*.dll'))
    assert len(list((BUILD / 'Licenses').glob('*.txt'))) == 3
    assert not list(BUILD.rglob('*.dpapi')) and not (BUILD / 'Runtime').exists()
    process = read('process_review.json')
    assert process['projectVoiceProcesses'] == 0 and process['expressionDiaryPlayerProcesses'] == 0
    evidence = ['player_build.json', 'Final.EditMode.xml', 'Final.PlayMode.xml', 'player_write.json', 'player_nokey.json', 'player_run.json', 'sample_texts.json', 'sample_diary.md', 'diary_layout.json', 'reference_assets.json', 'process_review.json'] + [a['file'] for a in audio]
    old_bytes = json.loads((ROOT / 'Docs/Validation/VoicePolish/player_build.json').read_text(encoding='utf-8-sig'))['bytes']
    result = dict(passed=True, userListeningFeedback='pending', schemaVersion=9,
                  build=build, bytesAddedVsVoicePolish=build['bytes'] - old_bytes, tests=tests,
                  actualPlayerChecks=checks, diaryLayout=layout, audio=audio,
                  runtimeCopied=False, referenceAudioAddedBytes=0, frozenFilesVerified=len(frozen),
                  evidence=[dict(file=n, sha256=digest(REPORTS / n)) for n in evidence],
                  source=[dict(file=p.relative_to(ROOT).as_posix(), sha256=digest(p)) for p in source],
                  verifiedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (REPORTS / 'acceptance.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(passed=True, tests=tests, bytesAddedVsVoicePolish=result['bytesAddedVsVoicePolish'], frozenFilesVerified=len(frozen), sourceFiles=len(source)), ensure_ascii=False))


if __name__ == '__main__':
    main()
