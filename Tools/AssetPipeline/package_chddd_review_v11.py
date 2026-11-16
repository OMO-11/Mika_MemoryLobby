"""Publish one review version and a portable record of the main-file recovery."""
from pathlib import Path
import json, hashlib, re
from PIL import Image, ImageChops

PROJECT = Path('W:/Machine/VSCode/Mika_Gift')
MAIN = PROJECT/'Material/CHDDD'
ROOT = MAIN/'Reviews/V11_FlatEyeRecovery'
OUT = ROOT/'Preview'
source = (PROJECT/'Tools/AssetPipeline/package_chddd_review_v10.py').read_text(encoding='utf-8')
source = source.replace('V10_TextureReview', 'V11_FlatEyeRecovery').replace('V9_SecondReview', 'V10_TextureReview')
source = source.replace('第三版', '第四版').replace('原素材贴图挤眼', '眼前平面贴图挤眼')
source = source.replace('校正原图倾斜后贴合脸部', '以透明平面放在闭合眼睑前方，跟随头部但不沿脸部弯曲')
source = source.replace('本版：原素材贴图', '本版：独立平面贴图').replace('上一版：几何眼线', '上一版：曲面贴图')
source = source.replace("let frame=0,playing=true", "let frame=29,playing=false")
source = source.replace('<button id="toggle">暂停</button>', '<button id="toggle">播放</button>')
exec(compile(source, 'package_v11_from_v10', 'exec'))

html = (OUT/'index.html').read_text(encoding='utf-8')
recovered = '''<article><h2>星期五后续动作已恢复到主文件</h2><p>18 个身体动作及对应面部过程已从 V10 审查稿恢复。Y 形举手、举手时闭眼、双手护头后抿嘴生气等修改均在主文件中。以下动作预览沿用星期五已保存的同一动作数据，本轮没有重新设计身体动作。</p><div class="views"><div><h2>Succeed：Y 形欢呼与起举闭眼</h2><img style="width:360px;max-width:100%" src="../../V9_SecondReview/Preview/Success_review.gif" alt="已恢复的 Succeed 动作"></div><div><h2>HeadClick：护头后抿嘴生气</h2><img style="width:360px;max-width:100%" src="../../V9_SecondReview/Preview/HeadClick_review.gif" alt="已恢复的 HeadClick 动作"></div></div><p><a href="../../../CHDDD_actions_v2.blend">打开已恢复的主 Blender 文件</a> · <a href="../../../恢复记录_20261011.md">恢复记录</a></p></article>'''
html = html.replace('</main>', recovered+'</main>')
html = html.replace('当前为 Blender 审查预览，等待你的反馈；本版尚未重新验证 Unity。', '主 Blender 文件已恢复至本版。当前表情仍为待审查试验；Unity 资源包保留 V7，尚未同步。')
html = html.replace('左右侧面检查', '左右侧面检查（观察平面贴图的位置与外浮感）')
(OUT/'index.html').write_text(html, encoding='utf-8')
(OUT/'preview_inline.js').write_text(re.search(r'<script>(.*?)</script>', html, re.S).group(1), encoding='utf-8')

# With zero test opacity the new shader/geometry must leave neutral untouched.
a = Image.open(OUT/'TestSqueeze_Face_000.png').convert('RGBA')
b = Image.open(MAIN/'Reviews/V10_TextureReview/Preview/TestSqueeze_Face_000.png').convert('RGBA')
extrema = ImageChops.difference(a,b).getextrema()
neutral = dict(passed=all(hi == 0 for lo,hi in extrema), rgbaDifferenceExtrema=extrema)
(ROOT/'neutral_preview_validation.json').write_text(json.dumps(neutral, indent=2), encoding='utf-8')
if not neutral['passed']:
    raise RuntimeError('Neutral rendering changed')

status = dict(revision=11, reviewIteration=4, status='awaiting-user-review',
    mainWorkfileRecovered=True, mainWorkfile='Material/CHDDD/CHDDD_actions_v2.blend',
    restoredFrom='Reviews/V10_TextureReview/CHDDD_actions_v2.blend',
    method='Two flat transparent head-skinned image cards in front of closed eyelids; no curved mesh or shrinkwrap',
    source='Material/Mika_MemoryLobby/spine_regions/10.png', originalTextureUnmodified=True,
    allBodyAndFaceActionsPreserved=True, acceptedAsStandardExpression=False,
    unityDeliveryRevision=7, unityRevalidated=False)
(ROOT/'review_status.json').write_text(json.dumps(status, ensure_ascii=False, indent=2), encoding='utf-8')
notes = '''# V11：动作恢复与眼前平面贴图试验

2026-10-11。星期五 20:48 后的修改没有丢失，保存在 Reviews/V8_FirstReview、V9_SecondReview 和 V10_TextureReview。主文件原先停留在 V7。本次以最新 V10 为恢复来源，18 个身体动作、对应眼眉嘴部过程和全部形变数据保持一致。

`><` 双眼使用用户指定的 10.png 裁片，改为两个独立平面（共 8 个顶点、2 个四边形），位于闭合眼睑前，绑定 Head 骨骼。贴图不再沿脸部曲面弯曲；平面只按眼部方向摆放，没有曲面投射或 Shrinkwrap。删除旧的肤色平面衬底，只显示原素材 RGBA；原眼部残线在自身材质中处理。嘴部保留上一版的小圆口和 neutral 过渡。

主文件已同步为 `Material/CHDDD/CHDDD_actions_v2.blend`，当前测试表情保存于第 37 帧。侧栏 CHDDD 中的 Performances V2 提供 >o< 平面贴图测试，以及 18 个身体动作；切换普通动作或基础表情时，独立试验贴图自动退出。重启后若面板未出现，运行文件内 CHDDD_Controls.py 和 CHDDD_BodyControls_v2.py 两段文本；没有修改 Blender 的安全设置。

Blender 当前动作配置位于主目录 `Blender/`。`Unity/` 和 unitypackage 仍为经过验证的 V7，不代表此次恢复已完成 Unity 导出。根目录旧的 V7 验证报告是历史结果。

请审查正面形状、neutral 过渡、桌宠缩小效果和侧面贴图外浮程度。平面表达和侧面观感是否可接受，仍以人工反馈为准；本轮只提交 V11 一版，不并入已验收基础表情。
'''
(ROOT/'审查说明.md').write_text(notes, encoding='utf-8')
(MAIN/'恢复记录_20261011.md').write_text(notes+'''
恢复前的磁盘主文件、当时未保存的实时 Blender 场景、控制脚本、配置及 Unity 文件已备份到 `Checkpoints/20261011_before_main_recovery/`。原有 V8 / V9 / V10 审查稿保留。
''', encoding='utf-8')
readme_path = MAIN/'README.md'
readme = readme_path.read_text(encoding='utf-8')
marker = '<!-- recovery-v11 -->'
if marker not in readme:
    note = '''<!-- recovery-v11 -->
## 当前工作文件（2026-10-11）

`CHDDD_actions_v2.blend` 已找回星期五后续审查稿中的完整动作，当前 Blender 制作版本为 V11；18 个身体动作及面部过程与最后保存的 V10 相同。`>o<` 平面贴图为本轮待审查试验，打开主文件停在第 37 帧。最新页面：`Reviews/V11_FlatEyeRecovery/Preview/index.html`。

Blender 控制脚本读取 `Blender/` 下的动作配置。Unity 文件夹及 `CHDDD_Actions_v2.unitypackage` 保留 V7，尚未导出本轮修改。下文的 V7 Unity 验证报告属于历史交付，不是 V11 已通过 Unity 的证明。详情及备份见 `恢复记录_20261011.md`。

侧栏未出现时，运行内嵌 `CHDDD_Controls.py` 和 `CHDDD_BodyControls_v2.py`。主文件已经打包眼贴图；本次没有修改 Blender 的脚本安全选项。
<!-- /recovery-v11 -->

'''
    readme = readme.replace('# CHDDD 恢复资产\n', '# CHDDD 恢复资产\n\n'+note, 1)
    readme_path.write_text(readme, encoding='utf-8')

main_validation = json.loads((MAIN/'recovery_validation.json').read_text(encoding='utf-8'))
delivery = dict(blenderRevision=11, blenderRecovered=main_validation['passed'],
    expressionReview='pending', unityDeliveryRevision=7, unitySynchronized=False,
    latestPreview='Reviews/V11_FlatEyeRecovery/Preview/index.html',
    backup='Checkpoints/20261011_before_main_recovery', date='2026-10-11')
(MAIN/'body_v2_current_delivery_status.json').write_text(json.dumps(delivery, ensure_ascii=False, indent=2), encoding='utf-8')

# Refresh the working-file checksums; preserve the old V7 list in the checkpoint.
sha_path = MAIN/'body_v2_checksums.sha256'
paths = {line.split('  ',1)[1] for line in sha_path.read_text(encoding='utf-8').splitlines() if '  ' in line}
paths.update(['README.md','CHDDD_Controls.py','CHDDD_BodyControls_v2.py','body_v2_progress.json',
    'recovery_validation.json','body_v2_current_delivery_status.json','恢复记录_20261011.md',
    'Textures/mika_squeeze_eyes_review.png'])
paths.update(str(p.relative_to(MAIN)).replace('\\','/') for p in (MAIN/'Blender').glob('*.json'))
sha_path.write_text(''.join(hashlib.sha256((MAIN/p).read_bytes()).hexdigest()+'  '+p+'\n' for p in sorted(paths) if (MAIN/p).is_file()), encoding='utf-8')
files = [p for p in ROOT.rglob('*') if p.is_file() and p.suffix in ('.blend','.json','.html','.gif','.jpg','.md','.py','.png','.js') and p.name != 'review_checksums.json']
(ROOT/'review_checksums.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}, ensure_ascii=False, indent=2), encoding='utf-8')
print('V11_RECOVERY_REVIEW_PACKAGED', flush=True)
