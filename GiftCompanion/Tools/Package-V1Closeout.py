"""Keep old candidates intact; assemble a standalone companion-first closeout."""
import argparse, hashlib, json, shutil
from datetime import datetime, timezone
from pathlib import Path
P=Path(__file__).resolve().parents[1]
OLD=P/'Releases/Gift-V1-Refined-Candidate'
DEST=P/'Releases/Gift-V1-Closeout'
REPORT=P/'Docs/Validation/V1Closeout'
ROOTS=['GiftCompanion.exe','GiftCompanion_Data','MonoBleedingEdge','UnityPlayer.dll','UnityCrashHandler64.exe','D3D12','Licenses']
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(1048576),b''): h.update(b)
    return h.hexdigest()
def write(p,v):
    p.parent.mkdir(parents=True,exist_ok=True); p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def prepare():
    if DEST.exists(): raise RuntimeError('Keep existing closeout; do not overwrite it.')
    if shutil.disk_usage(P).free<12*1024**3: raise RuntimeError('Need 12GiB free.')
    m=OLD/'Release/release_manifest.json'; old=json.loads(m.read_text(encoding='utf-8')); expected={r['path']:r for r in old['files'] if r['path'].startswith('Runtime/')}
    write(REPORT/'baseline.json',{'manifest':sha(m),'artifacts':{n:sha(OLD/n) for n in ['GiftCompanion.exe','GiftCompanion_Data/Managed/GiftCompanion.Core.dll','GiftCompanion_Data/Managed/GiftCompanion.Runtime.dll']}})
    DEST.mkdir(); records=[]
    for n,r in enumerate(expected.values(),1):
        src=OLD/r['path']; dst=DEST/r['path']; dst.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(src,dst)
        digest=sha(dst)
        if digest!=r['sha256']: raise RuntimeError('Runtime copy mismatch: '+r['path'])
        records.append(dict(r,mtime_ns=dst.stat().st_mtime_ns))
        if n%3000==0: print(f'Runtime verified {n}/{len(expected)}',flush=True)
    write(REPORT/'runtime_files.json',records)
    print('Runtime copied and verified; old releases untouched.',flush=True)
def finish():
    if not (REPORT/'runtime_files.json').exists(): raise RuntimeError('Prepare first.')
    for n in ROOTS:
        src=P/'Builds/V1Closeout'/n; dst=DEST/n
        if not src.exists(): continue
        if src.is_dir(): shutil.copytree(src,dst,dirs_exist_ok=True)
        else: shutil.copy2(src,dst)
    shutil.copytree(OLD/'Licenses',DEST/'Licenses',dirs_exist_ok=True)
    for src,dst in {'Docs/v1_closeout_player_guide.md':'README.md','Docs/v1_closeout_summary.md':'V1设计与实现总结.md','Docs/v1_refinement_maintenance.md':'维护说明.md','Docs/phase6_compatibility.md':'兼容范围.md'}.items(): shutil.copy2(P/src,DEST/dst)
    write(DEST/'Release/release_manifest.json',{'version':'1.0.0-closeout','stage':'validating','schema':10,'playerAcceptance':'pending'})
    print('Closeout candidate ready for native validation.',flush=True)
def seal():
    baseline=json.loads((REPORT/'baseline.json').read_text(encoding='utf-8'))
    if sha(OLD/'Release/release_manifest.json')!=baseline['manifest'] or any(sha(OLD/n)!=v for n,v in baseline['artifacts'].items()): raise RuntimeError('Old candidate baseline changed.')
    runtime=json.loads((REPORT/'runtime_files.json').read_text(encoding='utf-8')); files=[]
    for r in runtime:
        p=DEST/r['path']; st=p.stat()
        if st.st_size!=r['bytes'] or st.st_mtime_ns!=r['mtime_ns']: raise RuntimeError('Runtime changed after verified copy.')
        files.append({k:r[k] for k in ('path','bytes','sha256')})
    for p in sorted(DEST.rglob('*')):
        rel=p.relative_to(DEST).as_posix()
        if not p.is_file() or rel.startswith('Runtime/') or rel=='Release/release_manifest.json': continue
        if p.suffix=='.dpapi' or any(s in {'UserData','Secrets','Validation'} for s in p.relative_to(DEST).parts): raise RuntimeError('Player data in package.')
        files.append({'path':rel,'bytes':p.stat().st_size,'sha256':sha(p)})
    old=json.loads((OLD/'Release/release_manifest.json').read_text(encoding='utf-8'))
    m={k:old[k] for k in ('schema','backupFormat','unity','renderer','pythonPackages','modelProfiles')}
    m.update(version='1.0.0-closeout',stage='candidate',createdAtUtc=datetime.now(timezone.utc).isoformat(),playerAcceptance='pending',files=files,bytes=sum(r['bytes'] for r in files),runtimeBytes=sum(r['bytes'] for r in files if r['path'].startswith('Runtime/')),developmentPathsRequired=False)
    write(DEST/'Release/release_manifest.json',m)
    write(REPORT/'package.json',{'passed':True,'bytes':m['bytes'],'files':len(files),'manifestSha256':sha(DEST/'Release/release_manifest.json'),'oldManifestAndProgramsUnchanged':True,'runtimeVerifiedAgainstPreviousRelease':True})
    print(f'Sealed {len(files)} files; {m["bytes"]/1024**3:.3f}GiB.',flush=True)
if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('action',choices=['prepare','finish','seal']); a=parser.parse_args(); globals()[a.action]()
