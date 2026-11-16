"""Create a separate local candidate; preserve both existing releases and user data."""
import hashlib,json,shutil,sys
from pathlib import Path
P=Path(__file__).resolve().parents[1]
OLD=P/'Releases/Gift-V2-Candidate'
DEST=P/'Releases/Gift-V2-Seaside'
REPORT=P/'Docs/Design/CH0294_Seaside_Integration_2026-11-06'
VERSION='2.0.1-local-candidate'
def sha(p):
    with p.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def write(p,value):
    p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def prepare():
    if DEST.exists():raise RuntimeError('Existing candidate must be preserved.')
    manifest=json.loads((OLD/'Release/release_manifest.json').read_text(encoding='utf-8-sig'))
    records=[r for r in manifest['files'] if r['path'].startswith('Runtime/')]
    if shutil.disk_usage(P).free<sum(r['bytes'] for r in records)+2*1024**3:raise RuntimeError('Insufficient disk space.')
    baseline={'manifest':sha(OLD/'Release/release_manifest.json'),'exe':sha(OLD/'GiftCompanion.exe')}
    DEST.mkdir();copied=[]
    for i,r in enumerate(records,1):
        src=OLD/r['path'];dst=DEST/r['path']
        if src.is_symlink() or not dst.resolve().is_relative_to(DEST.resolve()):raise RuntimeError('Invalid package path.')
        dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
        if sha(dst)!=r['sha256']:raise RuntimeError('Runtime copy mismatch: '+r['path'])
        copied.append(dict(r,mtime_ns=dst.stat().st_mtime_ns))
        if i%5000==0:print('Verified Runtime',i,'/',len(records),flush=True)
    write(REPORT/'Validation/runtime_files.json',copied);write(REPORT/'Validation/release_baseline.json',baseline)
def finish():
    for name in ['GiftCompanion.exe','GiftCompanion_Data','MonoBleedingEdge','UnityPlayer.dll','UnityCrashHandler64.exe','D3D12','Licenses']:
        src=P/'Builds/V2Seaside'/name;dst=DEST/name
        if not src.exists():
            if name in ['UnityCrashHandler64.exe','D3D12']:continue
            raise RuntimeError('Missing build '+name)
        if src.is_dir():shutil.copytree(src,dst,dirs_exist_ok=True)
        else:shutil.copy2(src,dst)
    shutil.copytree(OLD/'Licenses',DEST/'Licenses',dirs_exist_ok=True)
    for name in ['兼容与验收范围.md','第三方资源说明.md','资料备份与迁移.md']:shutil.copy2(OLD/name,DEST/name)
    (DEST/'README.md').write_text('# Mika V2 海边资源补充\n\n保留整个文件夹，启动 GiftCompanion.exe。\n\nEsc → 陪伴空间 → 海边记忆大厅 → 进入。Esc 逐层返回人物画面。海边无新增菜单运镜，Esc 直接打开简易毛玻璃菜单，背景动画继续。原记忆大厅和共享资料保留，最后选择会在重开时恢复。\n\n完整本地语音 Runtime 沿用 V2 收尾包。不要同时运行旧版与新版；请先退出旧程序。资料位置和备份方式见《资料备份与迁移》。\n\n本包为本机体验候选；原有授权与设备验证边界仍见随包说明。\n',encoding='utf-8')
    write(DEST/'Release/release_manifest.json',{'version':'2.0.1-local-candidate','stage':'validating','schema':10,'backupFormat':1})
def seal():
    baseline=json.loads((REPORT/'Validation/release_baseline.json').read_text(encoding='utf-8'))
    if sha(OLD/'Release/release_manifest.json')!=baseline['manifest'] or sha(OLD/'GiftCompanion.exe')!=baseline['exe']:raise RuntimeError('Old release changed.')
    records=json.loads((REPORT/'Validation/runtime_files.json').read_text(encoding='utf-8'));files=[]
    for r in records:
        st=(DEST/r['path']).stat()
        if st.st_size!=r['bytes'] or st.st_mtime_ns!=r['mtime_ns']:raise RuntimeError('Runtime changed: '+r['path'])
        files.append({k:r[k] for k in ['path','bytes','sha256']})
    actual={f.relative_to(DEST).as_posix() for f in (DEST/'Runtime').rglob('*') if f.is_file()}
    if actual!={r['path'] for r in records}:raise RuntimeError('Unexpected Runtime files.')
    for f in sorted(DEST.rglob('*')):
        if not f.is_file():continue
        rel=f.relative_to(DEST).as_posix()
        if rel.startswith('Runtime/') or rel=='Release/release_manifest.json':continue
        if f.suffix=='.dpapi' or f.name=='memory.db' or 'Tests.dll' in f.name or f.is_symlink():raise RuntimeError('Unexpected private/test file.')
        files.append(dict(path=rel,bytes=f.stat().st_size,sha256=sha(f)))
    old=json.loads((OLD/'Release/release_manifest.json').read_text(encoding='utf-8-sig'))
    old.update(version=VERSION,stage='local-candidate',playerAcceptance='pending',files=files)
    write(DEST/'Release/release_manifest.json',old)
    write(REPORT/'Validation/package.json',dict(path=str(DEST),files=len(files),bytes=sum(r['bytes'] for r in files),manifestSha256=sha(DEST/'Release/release_manifest.json')))
    print('Seaside package sealed.',flush=True)
if __name__=='__main__':globals()[sys.argv[1]]()
