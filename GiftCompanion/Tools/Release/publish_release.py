"""Upload the verified 2.0.2 installer to the explicitly selected GitHub repository.

Uses existing Git Credential Manager login in memory; never saves credentials.
Creates a draft first, publishes only after server size/digest checks pass.
"""
import concurrent.futures
import hashlib
import json
import os
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
KIT = ROOT / 'GiftCompanion/Installers/GiftCompanion-2.0.2'
REPO = 'OMO-11/Mika_MemoryLobby'
BASE = 'https://api.github.com/repos/' + REPO
TAG = 'v2.0.2'


def main():
    credential = subprocess.run(['git', 'credential', 'fill'], input='protocol=https\nhost=github.com\n\n',
                                text=True, capture_output=True, check=True,
                                env={**os.environ, 'GIT_TERMINAL_PROMPT': '0', 'GCM_INTERACTIVE': 'Never'})
    fields = dict(x.split('=', 1) for x in credential.stdout.splitlines() if '=' in x)
    headers = {'Authorization': 'Bearer ' + fields['password'], 'User-Agent': 'Mika-Release',
               'Accept': 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28'}

    def api(url, method='GET', body=None):
        data = json.dumps(body).encode('utf-8') if body is not None else None
        req = urllib.request.Request(url, data=data, headers={**headers, 'Content-Type': 'application/json'}, method=method)
        with urllib.request.urlopen(req, timeout=90) as response:
            return json.load(response) if response.status != 204 else None

    sha = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
    remote = api(BASE + '/commits/main')['sha']
    if remote != sha:
        raise RuntimeError('Push current commit before releasing')
    files = [KIT / ('GiftCompanion-2.0.2-Setup' + suffix) for suffix in ['.exe', '-1.bin', '-2.bin', '-3.bin', '-4.bin']]
    files += [KIT / 'INSTALL.md']
    records = []
    verified = json.loads((ROOT / 'GiftCompanion/Docs/Delivery/GitHub-2.0.2-2026-11-16/installer-verification.json').read_text(encoding='utf-8'))
    expected = {x['name']: x for x in verified['parts']}
    for p in files:
        with p.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        if p.name in expected and digest != expected[p.name]['sha256']:
            raise RuntimeError('Unverified installer: ' + p.name)
        records.append(dict(name=p.name, bytes=p.stat().st_size, sha256=digest))
    sums = KIT / 'SHA256SUMS.txt'
    sums.write_text(''.join(f"{r['sha256']}  {r['name']}\n" for r in records), encoding='utf-8')
    files.append(sums)
    records.append(dict(name=sums.name, bytes=sums.stat().st_size, sha256=hashlib.sha256(sums.read_bytes()).hexdigest()))
    body = '''## GiftCompanion 2.0.2 · Windows x64

包含窗边阁楼、海边记忆大厅、聊天与共同记忆、日记、BGM 和中日双语本地语音环境。

### 安装

下载 **Setup.exe 和全部 4 个 .bin 分卷**，保持文件名不变，放到同一文件夹，然后运行 Setup.exe。无需手动合并或解压。安装文件共约 6.18 GB，建议预留至少 20 GB。安装步骤见 INSTALL.md，完整性核对见 SHA256SUMS.txt。

默认程序目录：`%LOCALAPPDATA%/Programs/GiftCompanion`。对话及记忆目录：`%LOCALAPPDATA%/GiftCompanion`，安装与卸载均保留资料。聊天模型服务及 Key 在应用内配置。

### 版本与验证

修正大厅切换后菜单收起、阁楼原生入场及海边时钟。沿用 2026-11-14 验证的安装器：27,790 项发布文件 SHA-256、55 项大厅检查、3 项重开及卸载资料保留通过。2026-11-16 重新核对全部安装文件哈希；本轮未重跑 Unity、真实模型或 TTS 测试。

各附件上传后核对 GitHub 返回的大小和 SHA-256。第三方素材许可见程序内 Licenses 及仓库说明。GitHub 自动生成的 Source code 用于开发，安装请下载上述安装文件。
'''
    releases = api(BASE + '/releases?per_page=100')
    release = next((r for r in releases if r['tag_name'] == TAG), None)
    if release is None:
        release = api(BASE + '/releases', 'POST', dict(tag_name=TAG, target_commitish=sha, name='GiftCompanion 2.0.2 · Windows x64', body=body, draft=True, prerelease=False))
    elif not release['draft']:
        raise RuntimeError('Release is already published; refusing to overwrite it')
    upload_base = release['upload_url'].split('{')[0]
    release_url = BASE + '/releases/' + str(release['id'])

    def upload(item):
        p, record = item
        for attempt in range(1, 5):
            existing = next((a for a in api(release_url + '/assets?per_page=100') if a['name'] == p.name), None)
            if existing:
                if existing['state'] == 'uploaded' and existing['size'] == record['bytes'] and existing.get('digest') == 'sha256:' + record['sha256']:
                    print('Verified remote ' + p.name, flush=True)
                    return existing
                api(BASE + '/releases/assets/' + str(existing['id']), 'DELETE')
            try:
                print(f'Uploading {p.name} ({record["bytes"]} bytes), attempt {attempt}', flush=True)
                with p.open('rb') as stream:
                    req = urllib.request.Request(upload_base + '?name=' + urllib.parse.quote(p.name), data=stream,
                            headers={**headers, 'Content-Type': 'application/octet-stream', 'Content-Length': str(record['bytes'])}, method='POST')
                    with urllib.request.urlopen(req, timeout=3600) as response:
                        result = json.load(response)
                if result['size'] != record['bytes'] or result.get('digest') != 'sha256:' + record['sha256']:
                    raise RuntimeError('Server digest or size mismatch: ' + p.name)
                print('Uploaded and verified ' + p.name, flush=True)
                return result
            except Exception as exc:
                print(f'Upload failed for {p.name}: {type(exc).__name__}: {exc}', flush=True)
                if attempt == 4:
                    raise
                time.sleep(3)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        uploaded = list(executor.map(upload, zip(files, records)))
    final_assets = api(release_url + '/assets?per_page=100')
    by_name = {a['name']: a for a in final_assets}
    for r in records:
        a = by_name[r['name']]
        if a['state'] != 'uploaded' or a['size'] != r['bytes'] or a.get('digest') != 'sha256:' + r['sha256']:
            raise RuntimeError('Final asset verification failed')
    published = api(release_url, 'PATCH', dict(draft=False, body=body, make_latest='true'))
    result = dict(url=published['html_url'], commit=sha, tag=TAG, assets=[dict(name=a['name'], size=a['size'], digest=a.get('digest'), url=a['browser_download_url']) for a in uploaded])
    local = ROOT / 'GiftCompanion/Tools/Release/.local'
    local.mkdir(exist_ok=True)
    (local / 'published.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=True), flush=True)


if __name__ == '__main__':
    main()
