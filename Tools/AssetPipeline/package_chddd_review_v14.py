"""Publish the symmetric chibi-eye review without inheriting cutout-art claims."""
from pathlib import Path
from PIL import Image,ImageChops
import json,hashlib,re,shutil

MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
ROOT=MAIN/'Reviews/V14_ChibiEyeReview';OUT=ROOT/'Preview'
template=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/package_chddd_review_v10.py').read_text(encoding='utf-8')
template=template.split("status={'revision':10",1)[0]
template=template.replace('V10_TextureReview','V14_ChibiEyeReview').replace('V9_SecondReview','V13_EyeAngleReview')
template=template.replace('第三版','V14').replace('使用 10.png 原素材眼睛','Q 版镜像眼线')
template=template.replace('原素材贴图挤眼','Q 版对称眼线').replace('原素材贴图 >o<','Q 版对称眼线 >o<')
template=template.replace('本版：原素材贴图','本版：Q 版重绘眼线').replace('上一版：几何眼线','上一版：V13 裁片眼线')
template=template.replace('保留原画的睫毛、紫色阴影与粉色边缘','镜像轮廓 · 圆滑端点 · 与模型匹配的深紫色眼线')
template=template.replace('本版眼睛裁自你指定的 10.png，校正原图倾斜后贴合脸部。保留小圆口与 neutral 过渡。',
    '眼线重新绘制为镜像矢量轮廓，并同步校正双眼平面。保留 V13 的开合程度，圆滑端点、收细下眼线，去除原裁片的毛边和多余细线。小圆口与 neutral 过渡沿用原设定。')
template=template.replace('当前为 Blender 审查预览，等待你的反馈；本版尚未重新验证 Unity。','主 Blender 文件已同步本版，停在第 37 帧等待审查；Unity 导出包尚未同步。')
template=template.replace('原图与裁剪素材对照','镜像设计稿与高清贴图').replace('../texture_crop_review.jpg','../chibi_eye_design.jpg')
template=template.replace('用户原图及校正后的双眼裁片','重新绘制的左右镜像眼线设计稿').replace('../texture_crop_manifest.json','../chibi_eye_design.json').replace('裁剪记录','设计记录')
template=template.replace("let frame=0,playing=true","let frame=29,playing=false").replace('<button id="toggle">暂停</button>','<button id="toggle">播放</button>')
exec(compile(template,'review14_visual_template','exec'))
html=(OUT/'index.html').read_text(encoding='utf-8')
extra='''<details><summary>头部正面对称检查</summary><img src="TestSqueeze_frontal.png" alt="沿头部正面观察对称眼线"><p>双眼贴图严格镜像，平面在头部绑定空间中对称。默认视图仍保留角色原有头部朝向。</p></details>'''
html=html.replace('</article>',extra+'</article>',1)
html=html.replace('</main>','<p><a href="../../../CHDDD_actions_v2.blend">当前主 Blender 文件</a> · <a href="../Textures/mika_squeeze_eyes.svg">可编辑矢量眼线</a></p></main>')
(OUT/'index.html').write_text(html,encoding='utf-8')
(OUT/'preview_inline.js').write_text(re.search(r'<script>(.*?)</script>',html,re.S).group(1),encoding='utf-8')
diff=ImageChops.difference(Image.open(OUT/'TestSqueeze_Face_000.png').convert('RGBA'),Image.open(MAIN/'Reviews/V13_EyeAngleReview/Preview/TestSqueeze_Face_000.png').convert('RGBA'))
neutral=dict(passed=all(hi==0 for lo,hi in diff.getextrema()),rgbaDifferenceExtrema=diff.getextrema())
(ROOT/'neutral_preview_validation.json').write_text(json.dumps(neutral,indent=2),encoding='utf-8')
assert neutral['passed'],'Neutral changed during chibi eye cleanup'
validation=json.loads((ROOT/'recovery_validation.json').read_text(encoding='utf-8'))
assert validation['passed']
status=dict(revision=14,reviewIteration=7,status='awaiting-user-review',
    method='Original mirrored vector silhouette with high resolution RGBA, symmetric head-bound flat cards and closed-lid seam cleanup',
    originalCutoutUsed=False,textureResolution=[1024,512],textureMirrorExact=True,cardMirrorExact=True,
    bodyActionsChanged=False,faceTimingChanged=False,unityRevalidated=False,unityDeliveryRevision=7,
    acceptedAsStandardExpression=False)
(ROOT/'review_status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'审查说明.md').write_text('''# V14：Q 版对称眼线

按用户反馈，改为重新绘制的矢量轮廓，左右严格镜像。保留 V13 约 20° / 21° 的上下开合程度，使用与模型睫毛接近的深紫色；上笔稍厚、下笔稍细，外端与尖角平滑。没有继续使用原立绘裁片的笔触、粉边与多余细线。

透明贴图为 1024×512，由高分辨率覆盖率降采样生成，边缘颜色保持一致，避免透明边产生黑色毛边。可编辑源文件为 Textures/mika_squeeze_eyes.svg。

两个独立平面仍绑定 Head 骨骼，并在头部绑定空间内同步校正为镜像位置与朝向；未改为曲面贴合。闭眼皮肤的亮边只在本测试表情中清理，退出时恢复原材质。身体动作、眼眉形变、小圆口与表情时序保持原设定。

Preview/index.html 提供本轮与 V13 同步对照、160/240 像素桌宠效果、慢放、逐帧、左右侧面以及沿头部正面的对称检查。主文件保存于第 37 帧。

本轮只提交 V14 一版，仍等待人工审查；Unity 包保留 V7，尚未同步。V13 和修改前实时场景保存在 Checkpoints/20261011_before_chibi_eye_v14。
''',encoding='utf-8')
for name in ('body_v2_progress.json','body_v2_current_delivery_status.json'):
    path=MAIN/name;data=json.loads(path.read_text(encoding='utf-8'))
    if name=='body_v2_progress.json':data.update(revision=14,status='chibi-eye-symmetry-awaiting-user-review',previousCheckpoint='Checkpoints/20261011_before_chibi_eye_v14')
    else:data.update(blenderRevision=14,expressionReview='pending',unitySynchronized=False)
    data['latestReview' if name=='body_v2_progress.json' else 'latestPreview']='Reviews/V14_ChibiEyeReview/Preview/index.html'
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
path=MAIN/'character_manifest.json';manifest=json.loads(path.read_text(encoding='utf-8'))
manifest['blenderWorkingCopy'].update(revision=14,pendingExpression='>o< symmetric chibi vector eyes V14',latestReview='Reviews/V14_ChibiEyeReview/Preview/index.html')
path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8');shutil.copy2(path,ROOT/path.name)
path=MAIN/'README.md';text=path.read_text(encoding='utf-8');marker='<!-- chibi-eye-v14 -->'
if marker not in text:
    note='''<!-- chibi-eye-v14 -->
当前 Blender 制作版本：V14（2026-10-11）。`><` 改为 Q 版镜像矢量眼线，使用平滑透明边缘与对称平面，并清理旧闭眼亮边。主文件保存于第 37 帧；最新预览：`Reviews/V14_ChibiEyeReview/Preview/index.html`。当前等待人工审查，Unity 包仍为 V7。修改前备份：`Checkpoints/20261011_before_chibi_eye_v14/`。
<!-- /chibi-eye-v14 -->

'''
    path.write_text(text.replace('# CHDDD 恢复资产\n','# CHDDD 恢复资产\n\n'+note,1),encoding='utf-8')
sha_path=MAIN/'body_v2_checksums.sha256'
paths={line.split('  ',1)[1] for line in sha_path.read_text(encoding='utf-8').splitlines() if '  ' in line}
paths.add('Textures/mika_squeeze_eyes_review.svg')
sha_path.write_text(''.join(hashlib.sha256((MAIN/p).read_bytes()).hexdigest()+'  '+p+'\n' for p in sorted(paths) if (MAIN/p).is_file()),encoding='utf-8')
files=[p for p in ROOT.rglob('*') if p.is_file() and p.suffix in ('.blend','.json','.html','.gif','.jpg','.md','.py','.png','.js','.svg') and p.name!='review_checksums.json']
(ROOT/'review_checksums.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},ensure_ascii=False,indent=2),encoding='utf-8')
print('V14_CHIBI_EYE_REVIEW_PACKAGED',flush=True)
