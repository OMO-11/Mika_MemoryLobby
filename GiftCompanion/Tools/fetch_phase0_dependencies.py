"""Fetch pinned official SQLite and UniWindowController distributions into the workspace."""
import csv
import hashlib
import io
import json
from pathlib import Path
import re
import urllib.request
import zipfile
import argparse

PROJECT = Path(__file__).resolve().parents[1]
CACHE = PROJECT / '.ToolsCache'
REPORT = PROJECT / 'Docs/Validation/Phase0'


def fetch(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'GiftCompanion-Phase0'})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def run(from_cache=False):
    CACHE.mkdir(exist_ok=True)
    html = (CACHE / 'sqlite-download.html').read_text('utf-8-sig') if from_cache else fetch('https://www.sqlite.org/download.html').decode('utf-8')
    row = next(csv.reader([line]).__next__() for line in html.splitlines()
               if line.startswith('PRODUCT,') and re.search(r'/sqlite-dll-win-x64-\d+\.zip', line))
    url = 'https://www.sqlite.org/' + row[2]
    data = (CACHE / 'sqlite.zip').read_bytes() if from_cache else fetch(url)
    if len(data) != int(row[3]) or hashlib.sha3_256(data).hexdigest() != row[4]:
        raise ValueError('Official SQLite SHA3-256 mismatch.')
    (CACHE / 'sqlite.zip').write_bytes(data)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        native = archive.read(next(n for n in archive.namelist() if n.endswith('sqlite3.dll')))
    target = PROJECT / 'Assets/Plugins/x86_64/gift_sqlite3.dll'
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(native)

    commit = json.loads((CACHE / 'uniwindow-commit.json').read_text('utf-8-sig') if from_cache else fetch('https://api.github.com/repos/kirurobo/UniWindowController/commits/upm'))['sha']
    github = 'https://codeload.github.com/kirurobo/UniWindowController/zip/' + commit
    package = (CACHE / 'uniwindowcontroller.zip').read_bytes() if from_cache else fetch(github)
    (CACHE / 'uniwindowcontroller.zip').write_bytes(package)
    destination = PROJECT / 'Packages/com.kirurobo.uniwindowcontroller'
    with zipfile.ZipFile(io.BytesIO(package)) as archive:
        for item in archive.infolist():
            if item.is_dir():
                continue
            parts = item.filename.split('/')[1:]
            if not parts or '..' in parts:
                raise ValueError('Unexpected upstream archive path.')
            # Windows V1 does not ship macOS plugins or sample projects.
            if any(p.endswith('.bundle') for p in parts) or parts[0] == 'Samples~':
                continue
            path = destination.joinpath(*parts).resolve()
            if not path.is_relative_to(destination.resolve()):
                raise ValueError('Package path escapes destination.')
            data = archive.read(item)
            if path.exists() and path.read_bytes() != data:
                raise ValueError(f'Preserving modified dependency: {path}')
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    receipt = {'sqlite': {'version': row[1], 'url': url, 'sha3_256': row[4],
                          'dllSHA256': hashlib.sha256(native).hexdigest()},
               'uniWindowController': {'commit': commit, 'url': github,
                                       'archiveSHA256': hashlib.sha256(package).hexdigest()}}
    (REPORT / 'native_dependencies.json').write_text(json.dumps(receipt, indent=2) + '\n', 'utf-8')
    print(f'SQLite {row[1]} and UniWindowController {commit[:12]} imported.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--from-cache', action='store_true')
    run(parser.parse_args().from_cache)
