"""Verify actual extracted files, unchanged role weights and real audio probe artifacts."""
import hashlib
from datetime import datetime
import json
import math
from pathlib import Path
import struct
import wave

PROJECT = Path(__file__).resolve().parents[1]
RUNTIME = PROJECT / 'Runtime/GPT-SoVITS'
REPORT = PROJECT / 'Docs/Validation/Phase0'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def run():
    plan = read(REPORT / 'voice_bundle_plan.json')
    trimmed = read(REPORT / 'voice_trim.json')
    trimTime = datetime.fromisoformat(trimmed['appliedUtc']).timestamp() if 'appliedUtc' in trimmed else (REPORT / 'voice_trim.json').stat().st_mtime
    exclusions = [r['path'] + '/' for r in trimmed['records'] if r['moved']]
    missing = [r['path'] for r in plan['files'] if r['baselineInclude'] and not (RUNTIME / r['path']).is_file()
               and not any(r['path'].startswith(prefix) for prefix in exclusions)
               and not r['path'].startswith('runtime/Lib/site-packages/gradio/_frontend_code/')]
    required = ['config.py', 'api_v2.py', 'tools/audio_sr.py', 'tools/my_utils.py',
                'tools/AP_BWE_main/datasets1/dataset.py', 'tools/AP_BWE_main/models/model.py',
                'runtime/python.exe', 'GPT_SoVITS/pretrained_models/gsv-v4-pretrained/s2Gv4.pth',
                'GPT_SoVITS/pretrained_models/gsv-v4-pretrained/vocoder.pth']
    missing += [p for p in required if not (RUNTIME / p).is_file()]
    if missing:
        raise RuntimeError(f'Missing inference files: {missing[:20]}')
    assets = plan['stagedAssets'] + read(REPORT / 'japanese_voice_import.json')['assets']
    for asset in assets:
        if hashlib.sha256((RUNTIME / asset['target']).read_bytes()).hexdigest() != asset['sha256']:
            raise RuntimeError('Staged role asset changed: ' + asset['target'])
    audio = {}
    for language in ('zh', 'ja'):
        probe = read(REPORT / f'tts_probe_{language}.json')
        if not probe['passed']:
            raise RuntimeError(f'{language} real inference did not pass.')
        path = REPORT / f'tts_probe_{language}.wav'
        if path.stat().st_mtime < trimTime:
            raise RuntimeError(f'{language} WAV predates trimming; run the real inference again.')
        with wave.open(str(path)) as wav:
            if wav.getsampwidth() != 2 or wav.getnchannels() != 1:
                raise RuntimeError('Expected mono PCM16 inference audio.')
            samples = struct.unpack('<%dh' % wav.getnframes(), wav.readframes(wav.getnframes()))
            info = {'duration': wav.getnframes() / wav.getframerate(), 'sampleRate': wav.getframerate(),
                    'channels': wav.getnchannels(), 'peak': max(map(abs, samples)),
                    'rms': math.sqrt(sum(s*s for s in samples) / len(samples)),
                    'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
        if info['duration'] <= 0 or info['rms'] < 1:
            raise RuntimeError('Inference audio is empty or silent.')
        probe['audio'] = info
        (REPORT / f'tts_probe_{language}.json').write_text(json.dumps(probe, indent=2, ensure_ascii=False) + '\n', 'utf-8')
        audio[language] = info
        configPath = RUNTIME / ('voice.json' if language == 'zh' else 'voice-ja.json')
        config = read(configPath)
        config['runtimeVerified'] = True
        config.setdefault('qualityAccepted', False)
        configPath.write_text(json.dumps(config, indent=2, ensure_ascii=False) + '\n', 'utf-8')
    files = [p for p in RUNTIME.rglob('*') if p.is_file()]
    result = {'scope': 'Actual embedded inference bundle and real /tts artifacts; voice quality not accepted.',
              'passed': True, 'files': len(files), 'runtimeBytes': sum(p.stat().st_size for p in files),
              'originalArchiveUncompressedBytes': plan['archiveUncompressedBytes'],
              'trimmedBytes': sum(r['bytes'] for r in trimmed['records'] if r['moved']),
              'roleAssetsSHA256Verified': len(assets), 'defaultLanguage': 'zh', 'audio': audio}
    (REPORT / 'voice_assembly.json').write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n', 'utf-8')
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == '__main__':
    run()
