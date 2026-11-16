from pathlib import Path
import hashlib
import json
from PIL import Image

p = Path(__file__).resolve().parent
root = p.parents[4]

for folder, name, delay in [('EntranceFrames', 'Entrance_Preview.gif', 167), ('InteractionFrames', 'Interaction_Preview.gif', 100)]:
    paths = sorted((p / folder).glob('frame_*.png'))
    samples = paths[::max(1, len(paths)//8)]
    colors = Image.new('RGB', (200 * len(samples), 112))
    for index, path in enumerate(samples):
        with Image.open(path) as im:
            colors.paste(im.convert('RGB').resize((200, 112)), (index * 200, 0))
    palette = colors.quantize(colors=256)
    frames = []
    for path in paths:
        with Image.open(path) as im:
            frames.append(im.convert('RGB').quantize(palette=palette, dither=Image.Dither.NONE))
    frames[0].save(p / name, save_all=True, append_images=frames[1:], duration=delay,
                   loop=0, disposal=2, optimize=False)

read = lambda name: json.loads((p / name).read_text(encoding='utf-8'))
validation = read('validation.json')
assert validation['playMode'] and validation['finite'] and all(x['pass'] for x in validation['checks'])
native = read('native_input.json')
assert native['nativeMousePress'] and native['nativeMouseRelease'] and native['returnedIdle']
continuity = read('pat_continuity.json')
assert continuity['pressStep'] < .01 and continuity['releaseStep'] < .01 and continuity['maxStep'] < .05
assert continuity['returnedIdle'] and continuity['baseAfter'] > continuity['baseBefore'] + 3.9
framing = read('framing_validation.json')
y = [x['foreheadNormalizedY'] for x in framing]
assert max(y) - min(y) < .0001
assert all(abs(x['visibleHeight'] - 20.92) < .0001 for x in framing)
assert all(x['mappingFailures'] == 0 and x['roleVertexCount'] > 0 and x['roleVertexMaxDelta'] < .001 for x in framing)
assert max(x['headA'] for x in framing) - min(x['headA'] for x in framing) < .0001
sequences = read('sequence_captures.json')
assert all(x['uncoveredFrames'] == 0 for x in sequences)
assert all(s['started'] for x in sequences for s in x['expressionStarts'])
assert all(x['uncoveredPixels'] == 0 for x in read('interaction_captures.json'))
assert read('startup.json')['automaticTrack'] == 'Start_Idle_01'

baseline = json.loads((p.parent / 'file_audit.json').read_text(encoding='utf-8'))
source = root / 'Material/Mika_MemoryLobby_Spine/spine/CH0294_home'
integrity = []
for item in baseline['files']:
    digest = hashlib.sha256((source / item['name']).read_bytes()).hexdigest()
    integrity.append(dict(name=item['name'], unchanged=digest == item['sha256']))
assert all(x['unchanged'] for x in integrity)
report = dict(passed=True, checks=len(validation['checks']), simulationFrames=validation['frames'],
              inputSystemMouseVerified=True, automaticEntrance=True, sourceIntegrity=integrity,
              normalizedForeheadYRange=max(y)-min(y), patContinuity=continuity,
              renderedSequenceFrames=sum(x['frames'] for x in sequences),
              gifs=[dict(file=x, bytes=(p/x).stat().st_size) for x in ['Entrance_Preview.gif', 'Interaction_Preview.gif']])
(p / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
