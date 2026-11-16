"""Extract the conservative inference selection next to Unity, keeping source archives untouched."""
import json
from pathlib import Path, PurePosixPath
import subprocess
import time

PROJECT = Path(__file__).resolve().parents[1]
REPORT = PROJECT / 'Docs/Validation/Phase0'
ROOT = 'GPT-SoVITS-v2pro-20250604/'
DESTINATION = (PROJECT / 'Runtime/GPT-SoVITS').resolve()
ARCHIVE = PROJECT.parent / 'Material/GPT-SoVITS/GPT-SoVITS-v2pro-20250604.7z'


def run():
    if not DESTINATION.is_relative_to(PROJECT.resolve()):
        raise ValueError('Runtime destination must stay inside the project.')
    plan = json.loads((REPORT / 'voice_bundle_plan.json').read_text('utf-8'))
    paths = []
    for entry in plan['files']:
        name = entry['path']
        additional = name in ('config.py', 'tools/audio_sr.py', 'tools/my_utils.py') or (
            name.startswith('tools/AP_BWE_main/') and not name.endswith('g_24kto48k.zip'))
        if not entry['baselineInclude'] and not additional:
            continue
        # Training UI frontend source is not needed by the API, and brackets are tar patterns.
        if name.startswith('runtime/Lib/site-packages/gradio/_frontend_code/'):
            continue
        path = PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts:
            raise ValueError('Invalid archive path.')
        paths.append(ROOT + path.as_posix())
    list_path = REPORT / 'voice_extract_files.txt'
    list_path.write_text('\n'.join(paths) + '\n', 'utf-8')
    DESTINATION.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    print(f'Extracting {len(paths)} selected files into embedded runtime...', flush=True)
    subprocess.run([r'C:\Windows\System32\tar.exe', '-xf', str(ARCHIVE), '--strip-components', '1',
                    '-C', str(DESTINATION), '-T', str(list_path)], check=True)
    sizes = sum(p.stat().st_size for p in DESTINATION.rglob('*') if p.is_file())
    result = {'scope': 'runtime-assembly-not-inference-acceptance', 'filesRequested': len(paths),
              'runtimeBytes': sizes, 'seconds': round(time.monotonic() - started, 2), 'passed': True}
    (REPORT / 'voice_assembly.json').write_text(json.dumps(result, indent=2) + '\n', 'utf-8')
    print(f'Runtime assembled: {sizes / 2**30:.2f} GiB. Inference still needs verification.', flush=True)


if __name__ == '__main__':
    run()
