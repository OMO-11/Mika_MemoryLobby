"""Read-only source/Unity import comparison, PNG and atlas checks."""
from pathlib import Path
import hashlib
import json
from datetime import datetime
from PIL import Image
import numpy as np

root = Path(__file__).resolve().parents[4]
pack = root / 'Material/Mika_MemoryLobby_Spine'
source = pack / 'spine/CH0294_home'
imported = root / 'Material/Mika_MemoryLobby/MemoryLobby_Seaside/Assets/New Folder'
output = Path(__file__).parent
manifest = json.loads((pack / 'manifest.json').read_text(encoding='utf-8'))
expected = {x['path']: x for group in manifest['files'].values() for x in group}
checks = {}
for line in (pack / 'Diagnostics/checksums.sha256').read_text(encoding='utf-8').splitlines():
    digest, filename = line.split(None, 1)
    checks[filename.strip().replace('\\', '/')] = digest

files = []
for file in sorted(source.iterdir()):
    content = file.read_bytes()
    digest = hashlib.sha256(content).hexdigest()
    rel = file.relative_to(pack).as_posix()
    name = file.name + ('.bytes' if file.suffix == '.skel' else '.txt' if file.suffix == '.atlas' else '')
    copy = imported / name
    declared = expected.get(rel)
    files.append(dict(name=file.name, bytes=len(content), sha256=digest,
                      manifestMatches=bool(declared and declared['bytes'] == len(content) and declared['sha256'] == digest),
                      checksumMatches=checks.get(rel) == digest,
                      importedPath=str(copy), importedIdentical=copy.exists() and copy.read_bytes() == content))

pages, regions = [], []
for raw in (source / 'CH0294_home.atlas').read_text(encoding='utf-8').splitlines():
    line = raw.strip()
    if not line:
        continue
    if ':' not in line:
        if line.endswith('.png'):
            page = dict(name=line, properties={})
            pages.append(page)
            region = None
        else:
            region = dict(name=line, page=page['name'], properties={})
            regions.append(region)
    else:
        key, value = line.split(':', 1)
        (region if region is not None else page)['properties'][key] = value

page_sizes = {}
for page in pages:
    with Image.open(source / page['name']) as image:
        image.load()
        rgba = np.asarray(image.convert('RGBA'), dtype=np.int16)
        rgb, alpha = rgba[:, :, :3], rgba[:, :, 3]
        semi = (alpha > 0) & (alpha < 255)
        exceeds = np.any(rgb > alpha[:, :, None] + 2, axis=2)
        page.update(width=image.width, height=image.height, mode=image.mode, pngDecode=True,
                    atlasDimensionsMatch=list(image.size) == [int(x) for x in page['properties']['size'].split(',')],
                    semitransparentPixels=int(semi.sum()),
                    semitransparentWithRGBGreaterThanAlpha=int((semi & exceeds).sum()),
                    transparentPixelsWithColor=int(((alpha == 0) & np.any(rgb > 0, axis=2)).sum()),
                    regionCount=sum(r['page'] == page['name'] for r in regions))
        page_sizes[page['name']] = image.size

invalid = []
for region in regions:
    x, y, w, h = [int(v) for v in region['properties']['bounds'].split(',')]
    degrees = int(region['properties'].get('rotate', 0))
    if degrees % 180:
        w, h = h, w
    pw, ph = page_sizes[region['page']]
    if x < 0 or y < 0 or x + w > pw or y + h > ph:
        invalid.append(region)

comparisons = []
for first, second in [('unity_idle_t0.5_full.png', 'unity_idle_t6_full.png'),
                      ('unity_idle_t2_viewport.png', 'unity_talk01_layered_t2_viewport.png'),
                      ('unity_straight_idle_t0.5_full.png', 'unity_straight_idle_t6_full.png'),
                      ('unity_straight_idle_t2_viewport.png', 'unity_straight_talk01_layered_t2_viewport.png'),
                      ('unity_idle_t2_viewport.png', 'unity_straight_idle_t2_viewport.png')]:
    if not (output / first).exists() or not (output / second).exists():
        continue
    with Image.open(output / first) as a, Image.open(output / second) as b:
        arr_a, arr_b = np.asarray(a).astype(np.int16), np.asarray(b).astype(np.int16)
        diff = np.abs(arr_a - arr_b)
        comparisons.append(dict(first=first, second=second, changedPixels=int(np.any(diff > 0, axis=2).sum()),
                                totalPixels=a.width*a.height, meanAbsoluteChannelDifference=float(diff.mean())))

report = dict(testedAt=datetime.now().astimezone().isoformat(), source=str(source), imported=str(imported),
              files=files, totalBytes=sum(f['bytes'] for f in files), pages=pages,
              atlasRegionCount=len(regions), outOfBoundsRegions=invalid,
              rgba32Bytes=sum(p['width']*p['height']*4 for p in pages), imageComparisons=comparisons,
              legacyVoiceDirectoryExists=(root / 'BA_Data/deliverables/Mika_MemorialLobby').exists())
(output / 'file_audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
