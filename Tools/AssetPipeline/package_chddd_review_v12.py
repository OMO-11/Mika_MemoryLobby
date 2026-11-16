"""One angle-review page comparing V12 against V11 without changing earlier reviews."""
from pathlib import Path
from PIL import Image,ImageChops
import json,re,hashlib
MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
ROOT=MAIN/'Reviews/V12_EyeAngleReview';OUT=ROOT/'Preview'
source=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/package_chddd_review_v10.py').read_text(encoding='utf-8')
source=source.replace('V10_TextureReview','V12_EyeAngleReview').replace('V9_SecondReview','V11_FlatEyeRecovery')
source=source.replace('第三版','第五版').replace('原素材贴图挤眼','眼线角度微调').replace('原素材贴图 >o<','眼线角度 >o<')
source=source.replace('本版：原素材贴图','本版：按标线调整夹角').replace('上一版：几何眼线','上一版：V11 平面贴图')
source=source.replace('本版眼睛裁自你指定的 10.png，校正原图倾斜后贴合脸部。保留小圆口与 neutral 过渡。',
    '按橙色标线将上眼线向外上方展开、下眼线向外下方展开，保留素材线条厚度与颜色。继续使用眼前平面贴图、小圆口与 neutral 过渡。')
source=source.replace("let frame=0,playing=true","let frame=29,playing=false").replace('<button id="toggle">暂停</button>','<button id="toggle">播放</button>')
exec(compile(source,'package_v12_review','exec'))
html=(OUT/'index.html').read_text(encoding='utf-8')
html=html.replace('当前为 Blender 审查预览，等待你的反馈；本版尚未重新验证 Unity。','主 Blender 文件已保存本版，停在第 37 帧等待审查；Unity 导出包尚未同步。')
html=html.replace('</main>','<p><a href="../../../CHDDD_actions_v2.blend">当前主 Blender 文件</a> · <a href="../angle_adjustment.json">本次角度记录</a></p></main>')
(OUT/'index.html').write_text(html,encoding='utf-8')
(OUT/'preview_inline.js').write_text(re.search(r'<script>(.*?)</script>',html,re.S).group(1),encoding='utf-8')
diff=ImageChops.difference(Image.open(OUT/'TestSqueeze_Face_000.png').convert('RGBA'),Image.open(MAIN/'Reviews/V11_FlatEyeRecovery/Preview/TestSqueeze_Face_000.png').convert('RGBA'))
neutral=dict(passed=all(hi==0 for lo,hi in diff.getextrema()),rgbaDifferenceExtrema=diff.getextrema())
(ROOT/'neutral_preview_validation.json').write_text(json.dumps(neutral,indent=2),encoding='utf-8')
assert neutral['passed'],'Neutral changed during the eye-angle refinement'
status=dict(revision=12,reviewIteration=5,status='awaiting-user-review',method='Original upper/lower strokes rotated to follow user guide; same flat head-skinned cards',
    bodyActionsChanged=False,faceTimingChanged=False,originalSourceTextureUnmodified=True,unityRevalidated=False,unityDeliveryRevision=7)
(ROOT/'review_status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'审查说明.md').write_text('''# V12：眼线夹角微调

按本轮截图中的橙色标线调整：上眼线向外上方倾斜，下眼线向外下方展开，形成更清楚的 `><` 尖角。使用原素材分支绕共同尖角旋转，保留线条厚度与颜色；左右图案分别校正，以避免原图倾斜造成的差异。

继续使用 V11 的两个独立平面，绑定 Head 骨骼，不沿脸部弯曲。身体动作、表情时序、小圆口与 neutral 过渡保持原设定。原始 10.png 和 V11 审查稿保留。

主文件 CHDDD_actions_v2.blend 已保存本版，并停在第 37 帧。Preview/index.html 提供本轮与 V11 同步对照、慢放、逐帧和侧面检查。本轮仅提交 V12 一版，等待用户审查；Unity 导出包尚未同步。
''',encoding='utf-8')
readme=MAIN/'README.md';content=readme.read_text(encoding='utf-8')
marker='<!-- eye-angle-v12 -->'
if marker not in content:
    note='''<!-- eye-angle-v12 -->
当前 Blender 制作版本：V12（2026-10-11）。按用户橙色标线调整 `><` 上下眼线夹角，继续使用平面贴图。主文件已保存，动作与时序保持 V11，最新预览：`Reviews/V12_EyeAngleReview/Preview/index.html`。Unity 包仍为 V7，待同步。本轮前备份：`Checkpoints/20261011_before_eye_angle_v12/`。
<!-- /eye-angle-v12 -->

'''
    readme.write_text(content.replace('# CHDDD 恢复资产\n','# CHDDD 恢复资产\n\n'+note,1),encoding='utf-8')
for name in ('body_v2_progress.json','body_v2_current_delivery_status.json'):
    data=json.loads((MAIN/name).read_text(encoding='utf-8'))
    if name=='body_v2_progress.json':
        data.update(revision=12,status='eye-angle-refinement-awaiting-user-review',previousCheckpoint='Checkpoints/20261011_before_eye_angle_v12')
    else:data.update(blenderRevision=12)
    data['latestReview' if name=='body_v2_progress.json' else 'latestPreview']='Reviews/V12_EyeAngleReview/Preview/index.html'
    (MAIN/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
manifest=json.loads((MAIN/'character_manifest.json').read_text(encoding='utf-8'))
manifest['blenderWorkingCopy'].update(revision=12,pendingExpression='>o< eye stroke angles V12',latestReview='Reviews/V12_EyeAngleReview/Preview/index.html')
(MAIN/'character_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
sha_path=MAIN/'body_v2_checksums.sha256'
paths={line.split('  ',1)[1] for line in sha_path.read_text(encoding='utf-8').splitlines() if '  ' in line}
sha_path.write_text(''.join(hashlib.sha256((MAIN/p).read_bytes()).hexdigest()+'  '+p+'\n' for p in sorted(paths) if (MAIN/p).is_file()),encoding='utf-8')
files=[p for p in ROOT.rglob('*') if p.is_file() and p.suffix in ('.blend','.json','.html','.gif','.jpg','.md','.py','.png','.js') and p.name!='review_checksums.json']
(ROOT/'review_checksums.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},ensure_ascii=False,indent=2),encoding='utf-8')
print('V12_ANGLE_REVIEW_PACKAGED',flush=True)
