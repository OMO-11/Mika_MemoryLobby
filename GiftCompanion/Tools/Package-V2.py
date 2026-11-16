"""Separate local V2 candidate; V1 and the frozen design remain read-only."""
import argparse, hashlib, json, shutil
from pathlib import Path
from datetime import datetime, timezone
P=Path(__file__).resolve().parents[1]
OLD=P/'Releases/Gift-V1-Closeout'
DEST=P/'Releases/Gift-V2-Candidate'
REPORT=P/'Docs/Design/V2_Visual_Implementation/02_执行记录/V2-6'
ROOTS=['GiftCompanion.exe','GiftCompanion_Data','MonoBleedingEdge','UnityPlayer.dll','UnityCrashHandler64.exe','D3D12','Licenses']
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(p,v):
    p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def prepare():
    if DEST.exists():raise RuntimeError('Preserve existing V2 candidate; do not overwrite.')
    old=json.loads((OLD/'Release/release_manifest.json').read_text(encoding='utf-8-sig'))
    runtime=[r for r in old['files'] if r['path'].startswith('Runtime/')]
    if shutil.disk_usage(P).free<sum(r['bytes'] for r in runtime)+2*1024**3:raise RuntimeError('Not enough free space.')
    write(REPORT/'v1_baseline.json',{'manifest':sha(OLD/'Release/release_manifest.json'),'programs':{n:sha(OLD/n) for n in ['GiftCompanion.exe','GiftCompanion_Data/Managed/GiftCompanion.Core.dll','GiftCompanion_Data/Managed/GiftCompanion.Runtime.dll']}})
    DEST.mkdir();records=[]
    for i,r in enumerate(runtime,1):
        rel=Path(r['path']);src=OLD/rel;dst=DEST/rel
        if not dst.resolve().is_relative_to(DEST.resolve()) or src.is_symlink():raise RuntimeError('Invalid runtime path.')
        dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
        if dst.stat().st_size!=r['bytes'] or sha(dst)!=r['sha256']:raise RuntimeError('Runtime mismatch: '+str(rel))
        records.append(dict(r,mtime_ns=dst.stat().st_mtime_ns))
        if i%2500==0:print(f'Runtime copied and SHA256 verified: {i}/{len(runtime)}',flush=True)
    write(REPORT/'runtime_files.json',records)
    print('V2 Runtime copied and verified; V1 untouched.',flush=True)
def finish():
    if not (REPORT/'runtime_files.json').exists():raise RuntimeError('Prepare first.')
    for n in ROOTS:
        src=P/'Builds/V2Final'/n;dst=DEST/n
        if not src.exists():
            if n in ['D3D12','UnityCrashHandler64.exe']:continue
            raise RuntimeError('Missing build root: '+n)
        if src.is_dir():shutil.copytree(src,dst,dirs_exist_ok=True)
        else:shutil.copy2(src,dst)
    shutil.copytree(OLD/'Licenses',DEST/'Licenses',dirs_exist_ok=True)
    for n in ['玩家说明.md','兼容与验收范围.md','第三方资源说明.md']:
        shutil.copy2(REPORT/n,DEST/('README.md' if n=='玩家说明.md' else n))
    shutil.copy2(REPORT/'第三方资源说明.md',DEST/'Licenses/V2-Resources.md')
    shutil.copy2(P/'Assets/StreamingAssets/Defaults/BackupGuide.md',DEST/'资料备份与迁移.md')
    write(DEST/'Release/release_manifest.json',{'version':'2.0.0-candidate','stage':'validating','schema':10,'backupFormat':1,'playerAcceptance':'pending'})
    print('Standalone V2 candidate ready.',flush=True)
def seal():
    base=json.loads((REPORT/'v1_baseline.json').read_text(encoding='utf-8'))
    if sha(OLD/'Release/release_manifest.json')!=base['manifest'] or any(sha(OLD/n)!=v for n,v in base['programs'].items()):raise RuntimeError('V1 baseline changed.')
    runtime=json.loads((REPORT/'runtime_files.json').read_text(encoding='utf-8'));files=[]
    for r in runtime:
        f=DEST/r['path'];st=f.stat()
        if st.st_size!=r['bytes'] or st.st_mtime_ns!=r['mtime_ns']:raise RuntimeError('Runtime changed since verified copy: '+r['path'])
        files.append({k:r[k] for k in ['path','bytes','sha256']})
    actual={f.relative_to(DEST).as_posix() for f in (DEST/'Runtime').rglob('*') if f.is_file()}
    if actual!={r['path'] for r in runtime}:raise RuntimeError('Unexpected Runtime file additions/removals.')
    for f in sorted(DEST.rglob('*')):
        if not f.is_file():continue
        rel=f.relative_to(DEST)
        if rel.as_posix().startswith('Runtime/') or rel.as_posix()=='Release/release_manifest.json':continue
        if f.is_symlink() or f.suffix=='.dpapi' or any(x in ['UserData','Secrets','Validation','Library','Temp'] for x in rel.parts) or f.name in ['memory.db','GiftCompanion.EditModeTests.dll','GiftCompanion.PlayModeTests.dll']:raise RuntimeError('Private/test data in candidate.')
        files.append({'path':rel.as_posix(),'bytes':f.stat().st_size,'sha256':sha(f)})
    old=json.loads((OLD/'Release/release_manifest.json').read_text(encoding='utf-8-sig'))
    m={k:old[k] for k in ['unity','schema','backupFormat','renderer','pythonPackages','modelProfiles']}
    m.update(version='2.0.0-candidate',stage='local-candidate',createdAtUtc=datetime.now(timezone.utc).isoformat(),playerAcceptance='pending',developmentPathsRequired=False,redistributionRights='User-supplied art/audio/font rights not independently established; local candidate only',files=files,bytes=sum(r['bytes'] for r in files),runtimeBytes=sum(r['bytes'] for r in files if r['path'].startswith('Runtime/')))
    write(DEST/'Release/release_manifest.json',m)
    write(REPORT/'package.json',{'passed':True,'files':len(files),'bytes':m['bytes'],'runtimeFiles':len(runtime),'manifestSha256':sha(DEST/'Release/release_manifest.json'),'v1Unchanged':True})
    print(f'Sealed {len(files)} files; {m["bytes"]/1024**3:.3f} GiB.',flush=True)
if __name__=='__main__':
    a=argparse.ArgumentParser();a.add_argument('action',choices=['prepare','finish','seal']);globals()[a.parse_args().action]()
