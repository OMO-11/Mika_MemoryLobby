"""Make a small, classified V1 documentation snapshot without moving live project files."""
import hashlib, html, json, os, re, shutil
from datetime import datetime, timezone
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
PROJECT=ROOT/'GiftCompanion'; DOCS=PROJECT/'Docs'; OUT=ROOT/'V1归档'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def run():
    if OUT.exists(): raise RuntimeError('V1归档 already exists; keep the existing snapshot.')
    release=PROJECT/'Releases/Gift-V1-Closeout'
    baseline=json.loads((DOCS/'Validation/V1Closeout/acceptance.json').read_text(encoding='utf-8'))
    protected=[release/'Release/release_manifest.json']+[release/r['path'] for r in baseline['artifacts']]
    before={str(p.relative_to(ROOT)):sha(p) for p in protected}
    if any(sha(release/r['path'])!=r['sha256'] for r in baseline['artifacts']): raise RuntimeError('Current release differs from closeout evidence.')
    copies={}
    def add(src,dst):
        if src.is_file(): copies[src.resolve()]=OUT/dst
    for name in ['v1_closeout_player_guide.md','phase6_compatibility.md','v1_refinement_maintenance.md','phase6_licenses.md']:
        add(DOCS/name,Path('01_当前交付')/name)
    add(ROOT/'项目总宪法.md',Path('02_设计与实现/项目总宪法.md'))
    for name in ['v1_closeout_summary.md','v1_design.md','architecture.md','character_protocol.md','memory_protocol.md','player_content_contracts.md','asset_manifest.md','native_dependencies.md','voice_runtime.md','validation.md']:
        add(DOCS/name,Path('02_设计与实现')/name)
    for src in sorted(DOCS.glob('*.md')):
        n=src.name
        if re.match(r'^phase[0-6]',n): group=re.match(r'^(phase\d+(?:plus)?)',n).group(1)
        elif n.startswith(('bilingual_','voice_polish_','expression_diary_')): group='声音与日记扩展'
        elif n.startswith('v1_refinement_'): group='V1完善'
        elif n.startswith('v1_closeout_'): group='V1收尾'
        elif n=='development_log.md': group='开发日志'
        else: continue
        if src.resolve() not in copies: add(src,Path('03_阶段记录')/group/n)
    stages=['6000.6.4f1','Phase0','Phase1','Phase2','Phase3','Phase3Plus','Phase4','Bilingual','VoicePolish','ExpressionDiary','Phase5','Phase6','V1Refinement','V1Closeout']
    receipts=['acceptance.json','player_build.json','package.json','package_summary.json','frozen_verification.json','install_write_verification.json','previous_release_verification.json']
    for stage in stages:
        folder=DOCS/'Validation'/stage
        for name in receipts: add(folder/name,Path('04_验收记录')/stage/name)
    for stage,patterns in {'V1Closeout':['Entry.*.xml','Native/*.json','Native/*.png'], 'V1Refinement':['Final2.*.xml','VisualAfter/*.json','VisualAfter/settings.png','VisualAfter/display.png','Standalone/Offline.json','Standalone/Restored.json','Voice/Voice.json']}.items():
        for pattern in patterns:
            for src in (DOCS/'Validation'/stage).glob(pattern): add(src,Path('04_验收记录')/stage/src.relative_to(DOCS/'Validation'/stage))
    add(release/'Release/release_manifest.json',Path('01_当前交付/发布文件清单.json'))
    OUT.mkdir(); records=[]
    link=re.compile(r'(!?\[[^\]\n]*\]\()([^\)\n]+)(\))')
    for src,dst in copies.items():
        dst.parent.mkdir(parents=True,exist_ok=True); source_hash=sha(src); changes=[0]
        if src.suffix.lower()=='.md':
            text=src.read_text(encoding='utf-8-sig')
            def fix(m):
                target=m[2].strip().strip('<>')
                if re.match(r'^[a-zA-Z][a-zA-Z0-9+.-]*:',target) or target.startswith('#'): return m[0]
                path,sep,anchor=target.partition('#'); original=(src.parent/path).resolve()
                if not original.exists(): return m[0] # Keep historical deleted paths as history.
                actual=copies.get(original,original)
                href=os.path.relpath(actual,dst.parent).replace('\\','/')+(sep+anchor if sep else '')
                changes[0]+=1; return m[1]+'<'+href+'>'+m[3]
            dst.write_text(link.sub(fix,text),encoding='utf-8')
        else: shutil.copy2(src,dst)
        records.append({'source':src.relative_to(ROOT).as_posix(),'archive':dst.relative_to(OUT).as_posix(),'sourceSha256':source_hash,'archiveSha256':sha(dst),'bytes':dst.stat().st_size,'linkAdjustments':changes[0]})
    launcher=OUT/'01_当前交付/启动V1.cmd'
    launcher.write_text('@echo off\r\nstart "" "%~dp0..\\..\\GiftCompanion\\Releases\\Gift-V1-Closeout\\GiftCompanion.exe"\r\n',encoding='ascii')
    rows=[]
    for r in records:
        rows.append(f"| [{r['archive']}]({r['archive']}) | [{r['source']}](../{r['source']}) |")
    index='# V1归档\n\n2026-11-03（Asia/Shanghai）。这是V1研发文档与验收记录的分类快照，原工程与唯一收尾包均保留原位。\n\n'
    index+='- **启动程序**：双击 `01_当前交付/启动V1.cmd`。完整程序仍在 `../GiftCompanion/Releases/Gift-V1-Closeout/`，本归档无需再复制约8.9GiB模型环境。\n'
    index+='- **了解V1**：[设计与实现总结](02_设计与实现/v1_closeout_summary.md)。\n- **查看进度**：[收尾checkpoint](03_阶段记录/V1收尾/v1_closeout_checkpoint.md)。\n'
    index+='- **核对结果**：[收尾验收](04_验收记录/V1Closeout/acceptance.json)。历史165/39/10属于V1，不代表V2已验证。\n\n'
    index+='## 分类\n\n| 文件夹 | 内容 |\n|---|---|\n| 01_当前交付 | 启动入口、使用/维护/兼容/许可说明、发布清单 |\n| 02_设计与实现 | 总宪法、架构、接口、记忆/角色协议与V1总结 |\n| 03_阶段记录 | Phase0—6、声音/日记扩展、完善、收尾与开发日志 |\n| 04_验收记录 | 精简验收JSON、最终测试XML与关键截图 |\n\n'
    index+='## 使用约定\n\n这是归档快照，不是第二套开发源。继续维护请使用下表“原文件”；不自动同步归档。归档中的有效相对链接已调整，旧构建已被其他任务清理的历史路径仍保留为记录，不能再启动。源码、Material、Runtime、Unity缓存、UserData、Secrets、数据库、音频与原始大日志未复制；没有删除或搬移原文件，也不启动Unity/API/TTS。\n\n'
    index+='V2视觉稿与实施计划维持原分类，见[V2工作区](../GiftCompanion/Docs/Design/V2_Visual_Implementation/README.md)，不混入V1程序归档。若单独搬走本归档，启动器与回源链接需要原项目目录配合；归档文档本身仍可读。\n\n## 文件索引\n\n| 归档文件 | 原文件（后续维护用） |\n|---|---|\n'+'\n'.join(rows)+'\n'
    (OUT/'README.md').write_text(index,encoding='utf-8')
    body=''.join(f'<li><a href="{html.escape(r["archive"],quote=True)}">{html.escape(r["archive"])}</a></li>' for r in records)
    (OUT/'开始阅读.html').write_text('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>V1归档</title><style>body{max-width:1000px;margin:48px auto;padding:0 24px;font:16px/1.8 system-ui;background:#f5f7fb;color:#223}a{color:#246}h1{font-size:28px}</style><h1>V1分类归档</h1><p>运行：打开01_当前交付文件夹，双击启动V1.cmd。文档为2026-11-03快照，后续维护以原工程为准。</p><p><a href="README.md">分类说明与原文件索引</a> · <a href="02_设计与实现/v1_closeout_summary.md">V1设计与实现总结</a></p><ul>'+body+'</ul></html>',encoding='utf-8')
    if any(sha(ROOT/p)!=v for p,v in before.items()): raise RuntimeError('Live release changed during archive.')
    result={'createdAtUtc':datetime.now(timezone.utc).isoformat(),'scope':'V1 documentation snapshot; live project, V2 design and player data remain in place','files':records,'copiedFiles':len(records),'bytes':sum(r['bytes'] for r in records),'releaseFingerprintsUnchanged':True,'originalsUnchanged':all(sha(ROOT/r['source'])==r['sourceSha256'] for r in records),'archiveVerified':all(sha(OUT/r['archive'])==r['archiveSha256'] for r in records)}
    (OUT/'归档清单.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='files'},ensure_ascii=False))
    if not result['originalsUnchanged'] or not result['archiveVerified']: raise RuntimeError('Archive verification failed.')
if __name__=='__main__': run()
