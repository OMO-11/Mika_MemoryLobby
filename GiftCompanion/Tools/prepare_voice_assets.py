"""Stage one Mandarin voice and calculate a conservative inference-only bundle plan."""
import hashlib
import io
import json
from pathlib import Path
import wave
import zipfile

PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT.parent / 'Material/GPT-SoVITS'
DESTINATION = PROJECT / 'Runtime/GPT-SoVITS'
REPORT = PROJECT / 'Docs/Validation/Phase0'
ROOT = 'GPT-SoVITS-v2pro-20250604/'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def decoded(info):
    return info.filename if info.flag_bits & 0x800 else info.filename.encode('cp437').decode('gbk')


def run():
    archives = sorted(SOURCE.glob('*.zip'))
    hashes = {p.name: sha(p.read_bytes()) for p in archives}
    chosen = next(p for p in archives if p.name.endswith('cn.zip'))
    outputs = []
    with zipfile.ZipFile(chosen) as archive:
        for suffix, output in [('.ckpt', 'models/mika/gpt.ckpt'), ('.pth', 'models/mika/sovits.pth'), ('.wav', 'models/mika/reference.wav')]:
            matches = [i for i in archive.infolist() if i.filename.endswith(suffix)]
            if len(matches) != 1:
                raise ValueError(f'Expected one {suffix} voice asset, got {len(matches)}')
            info = matches[0]
            data = archive.read(info)
            target = DESTINATION / output
            if target.exists() and target.read_bytes() != data:
                raise ValueError(f'Preserving changed destination: {target}')
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            outputs.append({'sourceEntry': decoded(info), 'target': output, 'bytes': len(data), 'sha256': sha(data)})
            if suffix == '.wav':
                with wave.open(io.BytesIO(data)) as audio:
                    wav = {'channels': audio.getnchannels(), 'sampleRate': audio.getframerate(),
                           'seconds': audio.getnframes() / audio.getframerate()}
                prompt = Path(decoded(info)).stem.removeprefix('【默认】').replace('_', '')

    voice = {'schemaVersion': 1, 'language': 'zh', 'modelVersionDeclaredBySource': 'v4',
             'apiBase': 'http://127.0.0.1:9880', 'referenceAudio': 'models/mika/reference.wav',
             'promptText': prompt, 'promptLanguage': 'zh', 'textLanguage': 'zh',
             'mediaType': 'wav', 'streamingMode': False, 'runtimeVerified': False}
    (DESTINATION / 'voice.json').write_text(json.dumps(voice, ensure_ascii=False, indent=2) + '\n', 'utf-8')
    (DESTINATION / 'tts_infer.yaml').write_text('''custom:
  version: v4
  device: cuda
  is_half: true
  bert_base_path: GPT_SoVITS/pretrained_models/chinese-roberta-wwm-ext-large
  cnhuhbert_base_path: GPT_SoVITS/pretrained_models/chinese-hubert-base
  t2s_weights_path: models/mika/gpt.ckpt
  vits_weights_path: models/mika/sovits.pth
''', 'utf-8')

    pretrained = ('chinese-roberta-wwm-ext-large/', 'chinese-hubert-base/', 'fast_langdetect/', 'gsv-v4-pretrained/')
    records = []
    for line in (REPORT / 'gpt_sovits_archive_files.txt').read_text('utf-8-sig').splitlines():
        if not line.startswith('-') or ROOT not in line:
            continue
        head, name = line.split(ROOT, 1)
        size = int(head.split()[4])
        keep = (name.startswith('runtime/') or
                (name.startswith('GPT_SoVITS/') and not name.startswith('GPT_SoVITS/pretrained_models/')) or
                name.startswith(tuple('GPT_SoVITS/pretrained_models/' + p for p in pretrained)) or
                name.startswith(('tools/i18n/', 'tools/audio_sr/')) or
                name in ('api_v2.py', 'LICENSE', 'ffmpeg.exe', 'ffprobe.exe'))
        # This is a conservative selection plan, not a claim that Python dependencies are already minimized.
        records.append({'path': name, 'bytes': size, 'baselineInclude': keep})
    original = sum(r['bytes'] for r in records)
    selected = sum(r['bytes'] for r in records if r['baselineInclude']) + sum(r['bytes'] for r in outputs)
    report = {'scope': 'archive-inventory-and-voice-staging-not-inference-acceptance',
              'userLanguageConfirmed': 'zh', 'modelArchivesSHA256': hashes,
              'modelArchivesAreIdentical': len(set(hashes.values())) == 1,
              'stagedAssets': outputs, 'referenceWav': wav, 'runtimeVerified': False,
              'archiveUncompressedBytes': original, 'conservativeBundleEstimateBytes': selected,
              'estimatedExcludedArchiveBytes': original - sum(r['bytes'] for r in records if r['baselineInclude']),
              'files': records}
    (REPORT / 'voice_bundle_plan.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', 'utf-8')
    print(f'Staged Mandarin voice: {sum(r["bytes"] for r in outputs) / 2**20:.1f} MiB; '
          f'conservative bundle estimate: {selected / 2**30:.2f} GiB (not yet runtime validated).')


if __name__ == '__main__':
    run()
