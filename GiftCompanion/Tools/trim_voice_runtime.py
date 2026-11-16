"""Move observed-unused UI, training and ASR components out of the embedded inference runtime."""
import argparse
import ctypes
from ctypes import wintypes
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil

PROJECT = Path(__file__).resolve().parents[1]
RUNTIME = PROJECT / 'Runtime/GPT-SoVITS'
REPORT = PROJECT / 'Docs/Validation/Phase0'
STASH = PROJECT / '.ToolsCache/VoiceTrim'
CANDIDATES = {
    'cmake': 'Native build tooling; delivered v4 inference uses prebuilt wheels.',
    'gradio': 'Training/browser UI is outside the embedded /tts API.',
    'gradio_client': 'Training/browser UI client.',
    'ctranslate2': 'ASR backend; not GPT-SoVITS v4 synthesis.',
    'faster_whisper': 'ASR transcription is outside the supplied trained-voice inference.',
    'funasr': 'ASR training/transcription.',
    'modelscope': 'ASR/model download tooling; all required inference weights are local.',
    'mecab_ko_dic': 'Korean-only dictionary; this bundle targets Chinese and Japanese.',
    'eunjeon': 'Korean-only tokenizer.',
    'pyarrow': 'Training dataset interchange.',
    'av': 'ASR video/media decoder; inference audio uses ffmpeg and soundfile.',
    'av.libs': 'ASR decoder native libraries.',
    'wordfreq': 'Unused general word-frequency dataset.',
    'datasets': 'Training dataset tooling.',
    'onnx': 'ONNX model authoring; retain onnxruntime for G2PW inference.',
    'ipadic': 'Unused MeCab dictionary; Japanese uses bundled pyopenjtalk dictionary.',
    'torch/include': 'C++ extension headers; v4 inference does not compile extensions.',
    'torch/test': 'Native PyTorch test executables.',
}


def size(path):
    return sum(p.stat().st_size for p in path.rglob('*') if p.is_file())


def run(apply):
    if not RUNTIME.resolve().is_relative_to(PROJECT.resolve()) or not STASH.resolve().is_relative_to(PROJECT.resolve()):
        raise RuntimeError('Runtime and trim backup must stay inside this project.')
    if apply:
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        kernel.OpenProcess.restype = wintypes.HANDLE
        kernel.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
        kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        for record in (PROJECT / 'UserData/Logs').glob('voice-*-process.json'):
            process = json.loads(record.read_text('utf-8-sig'))
            handle = kernel.OpenProcess(0x1000, False, process['pid'])
            if not handle:
                continue
            try:
                buffer = ctypes.create_unicode_buffer(32768)
                length = wintypes.DWORD(len(buffer))
                if kernel.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(length)) and Path(buffer.value).resolve() == (RUNTIME / 'runtime/python.exe').resolve():
                    raise RuntimeError('Stop the recorded voice process before trimming its runtime.')
            finally:
                kernel.CloseHandle(handle)
    imports = {}
    for language in ('zh', 'ja'):
        trace = REPORT / f'voice_dependencies_{language}.json'
        if trace.exists():
            imports.update(json.loads(trace.read_text('utf-8-sig'))['modules'])
    if not imports:
        raise RuntimeError('Start the API through Start-VoiceRuntime.ps1 and infer before considering trimming.')
    loaded_files = {str(p).replace('\\', '/') for p in imports.values()}
    records = []
    moved_now = 0
    prior_path = REPORT / 'voice_trim.json'
    prior = json.loads(prior_path.read_text('utf-8-sig')) if prior_path.exists() else {}
    previous_records = {r['path']: r for r in prior.get('records', [])}
    before = size(RUNTIME)
    for name, reason in CANDIDATES.items():
        relative = 'runtime/Lib/site-packages/' + name
        source = (RUNTIME / relative).resolve()
        destination = (STASH / relative).resolve()
        if not source.is_relative_to(RUNTIME.resolve()) or not destination.is_relative_to(STASH.resolve()):
            raise RuntimeError('Trim paths must stay inside the owned runtime and backup directories.')
        if not source.exists():
            previous = previous_records.get(relative)
            if previous and previous['moved']:
                records.append(previous)
            continue
        loaded = [p for p in loaded_files if p == relative or p.startswith(relative + '/')]
        if name == 'av.libs':
            loaded += [p for p in loaded_files if p.startswith('runtime/Lib/site-packages/av/')]
        record = {'path': relative, 'reason': reason, 'bytes': size(source), 'observedLoaded': bool(loaded), 'moved': False}
        if apply and not loaded:
            if destination.exists():
                raise RuntimeError(f'Existing backup preserved: {relative}')
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.move(str(source), str(destination))
            record['moved'] = True
            moved_now += 1
        records.append(record)
    result = {'scope': 'Conservative inference trim; both languages must be re-tested after application.',
              'applied': apply, 'beforeBytes': before if moved_now else prior.get('beforeBytes', before), 'afterBytes': size(RUNTIME), 'records': records,
              'appliedUtc': datetime.now(timezone.utc).isoformat() if moved_now else prior.get('appliedUtc', datetime.fromtimestamp(prior_path.stat().st_mtime, timezone.utc).isoformat() if prior else datetime.now(timezone.utc).isoformat()),
              'originalArchivesModified': False, 'backup': '.ToolsCache/VoiceTrim'}
    output = REPORT / ('voice_trim.json' if apply else 'voice_trim_plan.json')
    output.write_text(json.dumps(result, indent=2) + '\n', 'utf-8')
    print(json.dumps({**{k: v for k, v in result.items() if k != 'records'}, 'componentsMovedThisRun': moved_now}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    run(parser.parse_args().apply)
