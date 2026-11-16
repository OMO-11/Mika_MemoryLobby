"""Publish the owner's historical resources to the existing private repository."""
import concurrent.futures
import hashlib
import json
import os
import subprocess
import time
import urllib.request
import urllib.parse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'GiftCompanion/Tools/Release/.local/past-resources'
BASE = 'https://api.github.com/repos/OMO-11/Mika_MemoryLobby'
TAG = 'past-resources-2026-11-16'


def main():
    credential = subprocess.run(['git', 'credential', 'fill'], input='protocol=https\nhost=github.com\n\n',
                                text=True, capture_output=True, check=True,
                                env={**os.environ, 'GIT_TERMINAL_PROMPT': '0', 'GCM_INTERACTIVE': 'Never'})
    fields = dict(x.split('=', 1) for x in credential.stdout.splitlines() if '=' in x)
    headers = {'Authorization': 'Bearer ' + fields['password'], 'Accept': 'application/vnd.github+json',
               'User-Agent': 'Mika-PastResources', 'X-GitHub-Api-Version': '2022-11-28'}

    def api(url, method='GET', body=None):
        req = urllib.request.Request(url, data=json.dumps(body).encode() if body is not None else None,
                                     headers={**headers, 'Content-Type': 'application/json'}, method=method)
        with urllib.request.urlopen(req, timeout=90) as response:
            return json.load(response) if response.status != 204 else None

    if not api(BASE)['private']:
        raise RuntimeError('Historical private Editor data must stay in the existing private repository')
    info = json.loads((OUT / 'ARCHIVE.json').read_text(encoding='utf-8'))
    assert info['verified']
    records = list(info['parts'])
    for name in ['ARCHIVE.json', 'MANIFEST.json', 'SHA256SUMS.txt', 'README.md', 'RESTORE.ps1']:
        p = OUT / name
        records.append(dict(name=name, bytes=p.stat().st_size, sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    sha = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    assert api(BASE + '/commits/main')['sha'] == sha, 'Push current source before archiving'
    body = f'''# 过往资源 · 2026-11-16

按项目所有者要求归档本地独有资料，共 {info['sourceFiles']} 个文件，原始大小 {info['sourceBytes']:,} bytes。

包括原始语音发行包与角色模型、Blender 历史制作稿和检查点、审阅与预览资料、历史数据库及 Editor 本地配置。保留现有 Windows DPAPI 加密资料，未进行解密或明文转换；该资料绑定原 Windows 用户，换机需重新配置密钥。

下载全部 `.partNNN` 分片及 ARCHIVE.json、RESTORE.ps1、SHA256SUMS.txt，在同一目录运行 RESTORE.ps1，校验各分片并合并 ZIP，然后解压。完整 ZIP 的 SHA-256：`{info['archiveSha256']}`。

全部内容及逐文件 SHA-256 见 MANIFEST.json。归档前逐成员读回核验，上传后核对 GitHub 返回的大小和 SHA-256。当前可继续开发的工程在 main；正式安装包见 v2.0.2。该历史资源保持独立预发布标记，不取代当前安装版本。
'''
    releases = api(BASE + '/releases?per_page=100')
    release = next((r for r in releases if r['tag_name'] == TAG), None)
    if release is None:
        release = api(BASE + '/releases', 'POST', dict(tag_name=TAG, target_commitish=sha,
                      name='过往资源归档 · 2026-11-16', body=body, draft=True, prerelease=True))
    elif not release['draft']:
        raise RuntimeError('Historical archive already published; refusing overwrite')
    url = BASE + '/releases/' + str(release['id'])
    upload_url = release['upload_url'].split('{')[0]

    class Progress:
        def __init__(self, stream, name):
            self.stream, self.name, self.sent, self.last = stream, name, 0, 0

        def read(self, size=-1):
            block = self.stream.read(1024 * 1024 if size > 0 else size)
            self.sent += len(block)
            if self.sent - self.last >= 256 * 1024 * 1024:
                self.last = self.sent
                print(f'{self.name}: sent {self.sent // (1024 * 1024)} MiB', flush=True)
            return block

    def upload(record):
        p = OUT / record['name']
        with p.open('rb') as stream:
            assert hashlib.file_digest(stream, 'sha256').hexdigest() == record['sha256'], 'Local archive part changed'
        for attempt in range(1, 5):
            existing = next((a for a in api(url + '/assets?per_page=100') if a['name'] == p.name), None)
            if existing:
                if existing['state'] == 'uploaded' and existing['size'] == record['bytes'] and existing.get('digest') == 'sha256:' + record['sha256']:
                    print('Verified remote ' + p.name, flush=True)
                    return
                api(BASE + '/releases/assets/' + str(existing['id']), 'DELETE')
            try:
                print(f'Uploading {p.name}, attempt {attempt}', flush=True)
                with p.open('rb') as stream:
                    req = urllib.request.Request(upload_url + '?name=' + urllib.parse.quote(p.name), data=Progress(stream, p.name),
                          headers={**headers, 'Content-Type': 'application/octet-stream', 'Content-Length': str(record['bytes'])}, method='POST')
                    with urllib.request.urlopen(req, timeout=3600) as response:
                        result = json.load(response)
                if result['size'] != record['bytes'] or result.get('digest') != 'sha256:' + record['sha256']:
                    raise RuntimeError('Remote size/digest mismatch')
                print('Uploaded and verified ' + p.name, flush=True)
                return
            except Exception as exc:
                print(f'{p.name}: {type(exc).__name__}: {exc}', flush=True)
                if attempt == 4:
                    raise
                time.sleep(3)

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as executor:
        list(executor.map(upload, records))
    assets = {a['name']: a for a in api(url + '/assets?per_page=100')}
    for r in records:
        a = assets[r['name']]
        assert a['state'] == 'uploaded' and a['size'] == r['bytes'] and a.get('digest') == 'sha256:' + r['sha256']
    published = api(url, 'PATCH', dict(draft=False, prerelease=True, make_latest='false', body=body))
    result = dict(published=True, url=published['html_url'], sourceCommit=sha,
                  assets=[dict(name=a['name'], bytes=a['size'], sha256=a['digest'], url=a['browser_download_url']) for a in published['assets']])
    (OUT / 'PUBLISHED.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=True), flush=True)


if __name__ == '__main__':
    main()
