"""Verify saved repair evidence and that all six source/imported asset payloads remain intact."""
from pathlib import Path
import hashlib
import json
from PIL import Image
import numpy as np

folder = Path(__file__).resolve().parent
root = folder.parents[4]
project = root / 'Material/Mika_MemoryLobby/MemoryLobby_Seaside'
baseline = json.loads((folder.parent / 'file_audit.json').read_text(encoding='utf-8'))
source = root / 'Material/Mika_MemoryLobby_Spine/spine/CH0294_home'
files = []
for item in baseline['files']:
    original = source / item['name']
    suffix = '.bytes' if original.suffix == '.skel' else '.txt' if original.suffix == '.atlas' else ''
    imported = project / 'Assets/New Folder' / (item['name'] + suffix)
    actual = hashlib.sha256(original.read_bytes()).hexdigest()
    files.append(dict(name=original.name, sourceUnchanged=actual == item['sha256'],
                      importedIdentical=imported.read_bytes() == original.read_bytes()))
assert all(f['sourceUnchanged'] and f['importedIdentical'] for f in files)

playback = json.loads((folder / 'playback_validation.json').read_text())
assert playback['playMode'] and len(playback['results']) == 11
assert all(r['finite'] and r['baseAnimation'] == 'Idle_01' and r['overlaysCleared'] and r['lingeringAttachments'] == 0
           for r in playback['results'])
captures = json.loads((folder / 'scene_capture.json').read_text())
loop = json.loads((folder / 'loop_coverage.json').read_text())
assert len(captures) == 10 and len(loop) == 120
assert all(r['uncoveredPixels'] == 0 for r in captures + loop)
assert len(playback['results'][0]['backgroundSlotsMoved']) == 9

state = json.loads((folder / 'final_unity_state.json').read_text())
assert state['sceneSaved'] and not state['playMode']
assert state['hasCanvas'] and state['multiRenderer'] and state['canvasRenderers'] == 4
assert state['uiStraight'] and all(m['straight'] for m in state['materials'])
assert all(t['sRGB'] for t in state['textures'])

before = json.loads((folder / 'realtime_before.json').read_text())
after = json.loads((folder / 'realtime_after.json').read_text())
assert after['trackTime'] > before['trackTime'] + 1
assert after['gameTime'] > before['gameTime'] + 1

changed = []
seen = set()
for backup in sorted((folder / 'Backup').rglob('*')):
    if not backup.is_file():
        continue
    rel = backup.relative_to(folder / 'Backup')
    if rel.parts[0] == 'New Folder' or rel.as_posix() == 'New Folder.meta':
        rel = Path('Assets') / rel
    elif rel.as_posix() == 'SampleScene.disk-before.unity':
        rel = Path('Assets/Scenes/SampleScene.unity')
    if rel.parts[0] not in ('Assets', 'ProjectSettings') or rel.as_posix() in seen:
        continue
    seen.add(rel.as_posix())
    current = project / rel
    old = hashlib.sha256(backup.read_bytes()).hexdigest()
    new = hashlib.sha256(current.read_bytes()).hexdigest() if current.is_file() else None
    changed.append(dict(path=rel.as_posix(), backupPath=backup.relative_to(folder).as_posix(), backupSha256=old, currentSha256=new, changed=old != new))

with Image.open(folder / 'CH0294_修复后动态预览.gif') as gif:
    assert gif.n_frames == 120

first = np.asarray(Image.open(folder / 'repaired_idle_t0.5_reference.png').convert('RGB'), dtype=np.int16)
second = np.asarray(Image.open(folder / 'repaired_idle_t6_reference.png').convert('RGB'), dtype=np.int16)
different = np.any(first != second, axis=2)
report = dict(sourceFiles=files, changedFiles=changed, playbackScenarios=11,
              playbackSampleFrames=playback['totalFrames'], backgroundSlotsMoved=playback['results'][0]['backgroundSlotsMoved'],
              stillCaptures=len(captures), fullIdleCycleFrames=len(loop), idleCycleSeconds=12,
              fullIdleCycleUncoveredFrames=0, realtimeTrackAdvance=after['trackTime'] - before['trackTime'],
              realtimeGameAdvance=after['gameTime'] - before['gameTime'],
              idleChangedPixels=int(different.sum()), idleTotalPixels=int(different.size),
              finalSavedScene=state['scene'], validationPassed=True)
(folder / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
