"""Verify both handoffs and import the frozen CHDDD Unity V7 package without GUID changes."""
import argparse
import hashlib
import json
from datetime import datetime
from pathlib import Path, PurePosixPath
import re
import tarfile

PROJECT = Path(__file__).resolve().parents[1]
WORKSPACE = PROJECT.parent
SPINE = WORKSPACE / 'Material/Mika_MemoryLobby'
REPORT = PROJECT / 'Docs/Validation/Phase0'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def contained(root, path):
    result = (root / path).resolve()
    if not result.is_relative_to(root.resolve()):
        raise ValueError(f'Path outside declared root: {path}')
    return result


def verify(root, entry, records):
    path = contained(root, entry['path'])
    data = path.read_bytes()
    actual = digest(data)
    ok = actual == entry['sha256'].lower() and len(data) == entry.get('bytes', len(data))
    records.append({'path': str(path.relative_to(WORKSPACE)), 'sha256': actual, 'passed': ok})
    if not ok:
        raise ValueError(f'Handoff checksum differs: {path}')


def run(import_pet, import_spine):
    records = []
    pet = json.loads((WORKSPACE / 'Material/CHDDD/Integration/resource_manifest_v1.json').read_text('utf-8-sig'))
    spine = json.loads((SPINE / 'CH0069_runtime_manifest.json').read_text('utf-8-sig'))
    for entry in pet['assets']:
        verify(WORKSPACE, entry, records)
    for entry in spine['files']:
        verify(SPINE, entry, records)
        if entry.get('meta'):
            verify(SPINE, entry['meta'], records)

    writes = {}
    imported = []
    if import_pet:
        with tarfile.open(WORKSPACE / 'Material/CHDDD/CHDDD_Actions_v2.unitypackage', 'r:gz') as archive:
            members = {m.name: m for m in archive.getmembers()}
            for member in archive.getmembers():
                if not member.name.endswith('/pathname'):
                    continue
                original = archive.extractfile(member).read().decode('utf-8').strip()
                relative = PurePosixPath(original)
                if relative.parts[:2] != ('Assets', 'CHDDD') or '..' in relative.parts:
                    raise ValueError(f'Unexpected package path: {original}')
                destination = contained(PROJECT, 'Assets/Characters/MainCharacter/Pet/CHDDD/' + '/'.join(relative.parts[2:]))
                source = contained(WORKSPACE / 'Material/CHDDD', '/'.join(relative.parts[2:]))
                base = member.name.rsplit('/', 1)[0]
                for suffix, archived in [('', 'asset'), ('.meta', 'asset.meta')]:
                    name = base + '/' + archived
                    if name not in members:
                        raise ValueError(f'Missing packaged {archived}: {original}')
                    data = archive.extractfile(members[name]).read()
                    # The package is a frozen delivery; detect differences against loose files.
                    loose = Path(str(source) + suffix)
                    if not loose.is_file() or digest(loose.read_bytes()) != digest(data):
                        raise ValueError(f'Package and loose source differ: {loose}')
                    target = Path(str(destination) + suffix)
                    if target.exists() and target.read_bytes() != data:
                        raise ValueError(f'Preserving modified destination: {target}')
                    writes[target] = data
                imported.append({'source': str(source.relative_to(WORKSPACE)),
                                 'target': str(destination.relative_to(PROJECT)),
                                 'sha256': digest(writes[destination])})

    if import_spine:
        for entry in spine['files']:
            if not entry['required']:
                continue
            for item, destination in [(entry, entry['targetProjectPath']),
                                      (entry.get('meta'), entry['targetProjectPath'] + '.meta')]:
                if item:
                    writes[contained(PROJECT, destination)] = contained(SPINE, item['path']).read_bytes()
        runtime_root = SPINE / 'MemoryLobby/Assets/Spine'
        for source in runtime_root.rglob('*'):
            if source.is_file():
                target = contained(PROJECT, 'Assets/Spine/' + source.relative_to(runtime_root).as_posix())
                writes[target] = source.read_bytes()

    if writes:
        # Validate all incoming GUIDs and preserve edited destinations before any writes.
        existing = {}
        for meta in (PROJECT / 'Assets').rglob('*.meta'):
            match = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text('utf-8-sig'), re.M)
            if match:
                existing.setdefault(match[1], []).append(meta.resolve())
        incoming = {}
        for target, data in writes.items():
            if target.exists() and target.read_bytes() != data:
                raise ValueError(f'Preserving modified destination: {target}')
            if target.suffix != '.meta':
                continue
            match = re.search(rb'^guid: ([a-f0-9]{32})\r?$', data, re.M)
            if not match:
                raise ValueError(f'Missing GUID: {target}')
            guid = match[1].decode()
            if guid in incoming or any(p != target.resolve() for p in existing.get(guid, [])):
                raise ValueError(f'GUID collision for {target}: {guid}')
            incoming[guid] = target
        for target, data in writes.items():
            if not target.exists():
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)

    REPORT.mkdir(parents=True, exist_ok=True)
    receipt = {'generatedAt': datetime.now().astimezone().isoformat(),
               'scope': 'handoff-checksums-and-frozen-package-import-not-runtime-acceptance',
               'passed': True, 'sourceFiles': records, 'importedPetAssets': imported,
               'spineRuntimeImported': import_spine,
               'spineLicense': 'user-confirmed-2026-10-11' if import_spine else 'awaiting-user-confirmation',
               'copiedFiles': [{'target': str(p.relative_to(PROJECT)), 'sha256': digest(data)} for p, data in writes.items()],
               'petDeliveryRevision': 7, 'latestBlenderFullySynchronized': False}
    (REPORT / 'asset_import.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf-8')
    print(f'Verified {len(records)} handoff files; imported {len(imported)} CHDDD assets; Spine imported={import_spine}.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--import-pet', action='store_true')
    parser.add_argument('--import-spine-authorized', action='store_true',
                        help='Only use after the user confirms Spine Runtime authorization.')
    args = parser.parse_args()
    run(args.import_pet, args.import_spine_authorized)
