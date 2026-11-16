"""Update the previously delivered local candidate, retaining its verified Runtime."""
import json,runpy,shutil
from pathlib import Path
P=Path(__file__).resolve().parents[1]
REPORT=P/'Docs/Design/CH0294_Seaside_Integration_2026-11-06/Review1'
DEST=P/'Releases/Gift-V2-Seaside'
def run():
    build=P/'Builds/V2SeasideReview'
    result=json.loads((P/'Docs/Validation/V2SeasideReview/player_build.json').read_text())
    if not result['passed']:raise RuntimeError('Build must pass first.')
    baseline=P/'Docs/Design/CH0294_Seaside_Integration_2026-11-06/Validation'
    (REPORT/'Validation').mkdir(exist_ok=True)
    for name in ['runtime_files.json','release_baseline.json']:shutil.copy2(baseline/name,REPORT/'Validation'/name)
    previous=REPORT/'previous_release_manifest.json'
    if not previous.exists():shutil.copy2(DEST/'Release/release_manifest.json',previous)
    for name in ['GiftCompanion.exe','GiftCompanion_Data','MonoBleedingEdge','UnityPlayer.dll','UnityCrashHandler64.exe','D3D12','Licenses']:
        src=build/name;dst=DEST/name
        if not src.exists():
            if name in ['UnityCrashHandler64.exe','D3D12']:continue
            raise RuntimeError('Missing build '+name)
        if src.is_dir():shutil.copytree(src,dst,dirs_exist_ok=True)
        else:shutil.copy2(src,dst)
    readme=DEST/'README.md';text=readme.read_text(encoding='utf-8-sig')
    note='\n\n## 2026-11-06 审阅修正 2.0.2\n\n切换大厅后自动收起菜单，入场结束直接留在人物画面。阁楼恢复约 20 秒原生入场；海边右上角为透明底萝莉体时钟。Enter 可跳过入场开始输入，Esc 仍可主动打开菜单。\n'
    if '审阅修正 2.0.2' not in text:readme.write_text(text+note,encoding='utf-8')
    ns=runpy.run_path(str(P/'Tools/Package-Seaside.py'))
    g=ns['seal'].__globals__;g['REPORT']=REPORT;g['VERSION']='2.0.2-local-candidate'
    ns['seal']()
if __name__=='__main__':run()
