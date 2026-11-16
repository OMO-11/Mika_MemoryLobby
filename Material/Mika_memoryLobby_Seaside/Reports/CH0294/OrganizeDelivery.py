import hashlib
import json
import re
import shutil
from pathlib import Path

workspace = Path('W:/Machine/VSCode/Mika_Gift')
project = workspace / 'Material/Mika_MemoryLobby/MemoryLobby_Seaside'
reports = workspace / 'Material/Mika_memoryLobby_Seaside/Reports/CH0294'
raw = workspace / 'Material/Mika_MemoryLobby_Spine/spine/CH0294_home'
delivery = workspace / 'Material/CH0294_Seaside_交付整理_20261106'
if delivery.exists():
    raise RuntimeError('Delivery already exists; do not overwrite an existing delivery')
delivery.mkdir()
sources = {}

def copy_file(src, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    sources[dst.relative_to(delivery).as_posix()] = str(src)

unity = delivery / '01_UnityProject'
for folder in ('Assets', 'Packages', 'ProjectSettings'):
    for src in sorted((project / folder).rglob('*')):
        if not src.is_file():
            continue
        rel = src.relative_to(project)
        if rel.parts[:3] == ('Assets', 'Seaside', 'Backups') or rel.as_posix() == 'Assets/Seaside/Backups.meta':
            continue
        parts = list(rel.parts)
        if parts[:2] == ['Assets', 'New Folder']:
            parts[1] = 'CH0294'
        elif parts[:2] == ['Assets', 'New Folder.meta']:
            parts[1] = 'CH0294.meta'
        copy_file(src, unity.joinpath(*parts))

# The delivery runs independently of the editor-control plugin; gameplay does not use MCP.
modified = []
for filename in ('manifest.json', 'packages-lock.json'):
    path = unity / 'Packages' / filename
    data = json.loads(path.read_text(encoding='utf-8-sig'))
    data['dependencies'].pop('com.coplaydev.unity-mcp', None)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    modified.append(path.relative_to(delivery).as_posix())

for src in raw.iterdir():
    if src.is_file():
        copy_file(src, delivery / '02_SpineOriginal' / src.name)

previews = {
    'EntranceTransition/Entrance_1.25x_WhiteFade.mp4': '01_完整入场与白光转场.mp4',
    'EntranceTransition/WhiteFade_Preview.gif': '02_白光转场.gif',
    'Interaction/Interaction_Preview.gif': '03_抚摸与待机表情.gif',
    'Interaction/aligned_idle_16x9.png': '04_待机构图.png',
    'Interaction/idle_closed_eye_smile.png': '05_闭眼微笑.png',
    'Interaction/idle_thoughtful.png': '06_思考表情.png',
}
for src, name in previews.items():
    copy_file(reports / src, delivery / '03_Previews' / name)

for src in reports.glob('*.md'):
    copy_file(src, delivery / '04_Reports' / src.name)
for folder, names in {
    'Repair': ['CH0294_Spine动态背景修复报告.md', 'verification.json', 'playback_validation.json', 'loop_coverage.json'],
    'Interaction': ['视角与互动更新报告.md', 'framing_validation.json', 'validation.json', 'pat_continuity.json', 'native_input.json', 'verification.json'],
    'EntranceTransition': ['入场加速与白光转场修复报告.md', 'final_validation.json', 'playmode_validation.json', 'artifact_validation.json', 'validation.json'],
}.items():
    for name in names:
        copy_file(reports / folder / name, delivery / '04_Reports' / folder / name)

readme = '''# CH0294 海边记忆大厅：可复用资源交付

整理日期：2026-11-06。此目录是当前工作区已修复内容的独立副本，源工程与原始素材保持原位。

## 从这里开始

推荐通过 Unity Hub 添加本目录下的 `01_UnityProject`，使用 **Unity 6000.3.7f1** 打开。
打开 `Assets/Seaside/Scenes/CH0294_Seaside_Repaired.unity`，按 Play。
首次打开需要 Unity 解析 Packages 并重新生成 Library，可能需要访问 Unity 包仓库。

## 目录说明

| 目录 | 内容与用途 |
| --- | --- |
| `01_UnityProject` | 完整的 Assets、Packages、ProjectSettings，作为可打开的 Unity 工程使用 |
| `01_UnityProject/Assets/CH0294` | 修复后的导入资源：骨骼、图集、四张贴图、材质、Atlas 与 SkeletonData 资产及其 .meta |
| `01_UnityProject/Assets/Seaside` | 当前修复场景、UI 材质及三个播放／互动控制脚本 |
| `01_UnityProject/Assets/Spine` | 原工程附带的 Spine 4.2 运行时与编辑器支持，保留原许可声明 |
| `01_UnityProject/Assets/Settings` | 原工程渲染配置；移动场景到其他工程时也要检查这些依赖 |
| `02_SpineOriginal` | 原始 .skel、.atlas 与四张 PNG，共六个文件，适合交给其他支持 Spine 4.2 的环境继续适配 |
| `03_Previews` | 最新入场、白光、抚摸与表情预览；构图和表情截图 |
| `04_Reports` | 分阶段调查／修复报告及当时的验证数据 |

## 已具备的效果

- 正确的贴图颜色与透明混合、动态海面及星空背景。
- 修复后的纵向视野与宽屏适配。
- 自动入场特写；最终方案为 1.25 倍速，并在原时间轴 16.6667 秒侧脸转正面的位置，以白光接入待机，取消末尾约五秒正面停留。历史验收总时长约 14.07 秒，10 FPS 验收约 14.20 秒。
- 额头至头顶区域抚摸判定、反馈保持、松手收尾。
- 轻微看向玩家、闭眼微笑和思考表情。

## 复用到已有 Unity 工程

建议先直接打开交付工程确认效果，再迁移。原工程资源目录 `Assets/New Folder` 在交付副本中整理为 `Assets/CH0294`，文件内容及所有 .meta GUID 保留。
迁移时保留 `.meta`，并一并带入 `Assets/CH0294`、`Assets/Seaside`、Spine 4.2 支持与相关 URP Settings；也可以在打开交付工程后通过 Unity 的 Export Package（Include dependencies）导出修复场景。
已有工程若安装 Spine，先核对版本与 GUID，避免重复导入两套运行时。目标工程还需支持 URP 17.3.0、Unity UI 与 Input System 1.18.0，并维持原工程的 Linear 色彩空间和输入设置。只复制原始六个 Spine 文件不会自动包含修复材质与互动逻辑。

## 整理范围与验证边界

本次只整理复制，不修改已完成的动画和互动行为。交付副本移除了不参与播放的 Unity MCP 包依赖，保留其余包及版本锁定；没有复制 Library、Temp、Logs、旧修复场景备份和数百张过程帧图。
历史报告中的绝对路径及截图引用保留为原始记录，使用本目录的工程和预览为准。抚摸／表情 GIF 是前一阶段的互动展示，最新入场以 MP4 和白光 GIF 为准。

本次完成了复制文件 SHA-256 对照、GUID 引用静态核对及交付文件清单生成；本次会话没有可用 Unity MCP，未重新启动整理后的工程进行播放测试。历史 Play 模式验收数据随报告交付，代表先前验证结果。

语音未补齐；原游戏独立漂浮光点所需 Effect/Prefab/Timeline 仍未提供。本目录保留现有 Spine 动态背景，不包含额外补绘或音频资源。Spine 运行时沿用其原有许可要求；原游戏素材的使用范围由使用者自行确认。

`DELIVERY_VALIDATION.json` 记录本次整理核对结果，`FILE_MANIFEST.json` 记录各文件大小、SHA-256 与来源。
'''
(delivery / 'README_使用说明.md').write_text(readme, encoding='utf-8')

guid_pattern = re.compile(r'guid:\s*([0-9a-f]{32})')
def guid_map(folder):
    result = {}
    for meta in folder.rglob('*.meta'):
        match = guid_pattern.search(meta.read_text(encoding='utf-8-sig', errors='ignore'))
        if match:
            result[match.group(1)] = str(meta)
    return result

source_guids = guid_map(project / 'Assets')
copied_guids = guid_map(unity / 'Assets')
package_guids = guid_map(project / 'Library/PackageCache')
missing_project_refs = []
unknown_refs = set()
for path in (unity / 'Assets').rglob('*'):
    if path.suffix not in ('.unity', '.asset', '.mat', '.prefab', '.meta', '.controller'):
        continue
    for guid in guid_pattern.findall(path.read_text(encoding='utf-8-sig', errors='ignore')):
        if guid in copied_guids or guid.startswith('0000000000000000'):
            continue
        if guid in source_guids:
            missing_project_refs.append({'file': path.relative_to(unity).as_posix(), 'guid': guid, 'source': source_guids[guid]})
        elif guid not in package_guids:
            unknown_refs.add(guid)

hash_failures = []
for relative, source in sources.items():
    if relative in modified:
        continue
    if hashlib.sha256((delivery / relative).read_bytes()).digest() != hashlib.sha256(Path(source).read_bytes()).digest():
        hash_failures.append(relative)
validation = {
    'date': '2026-11-06', 'sourceProject': str(project),
    'copiedFilesComparedToSource': len(sources) - len(modified),
    'sourceHashMismatches': hash_failures,
    'missingProjectGuidReferences': missing_project_refs,
    'referencesUnresolvedByAssetsOrCachedPackages': sorted(unknown_refs),
    'modifiedFiles': modified, 'assetFolderRename': 'Assets/New Folder -> Assets/CH0294',
    'unityPlaybackRetestedThisSession': False,
}
(delivery / 'DELIVERY_VALIDATION.json').write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding='utf-8')
manifest = []
for path in sorted(delivery.rglob('*')):
    if path.is_file():
        data = path.read_bytes()
        rel = path.relative_to(delivery).as_posix()
        manifest.append({'path': rel, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(), 'source': sources.get(rel, 'generated during organization')})
(delivery / 'FILE_MANIFEST.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'path': str(delivery), 'files': len(manifest)+1, 'sizeMB': round(sum(p.stat().st_size for p in delivery.rglob('*') if p.is_file())/1024/1024, 2), 'validation': validation}, ensure_ascii=False, indent=2))
if hash_failures or missing_project_refs:
    raise RuntimeError('Delivery verification failed')
