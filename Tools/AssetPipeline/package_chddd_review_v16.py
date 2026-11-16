"""One reviewable -omega- experiment, with neutral and desktop-size comparison."""
from pathlib import Path
from PIL import Image,ImageChops
import json,hashlib,re,shutil
MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
ROOT=MAIN/'Reviews/V16_OmegaFirstReview';OUT=ROOT/'Preview'
source=Path(__file__).with_name('package_chddd_review_v10.py').read_text(encoding='utf-8').split("status={'revision':10",1)[0]
source=source.replace('V10_TextureReview','V16_OmegaFirstReview').replace('V9_SecondReview','V15_ChibiEyeReview')
source=source.replace('TestSqueeze','TestOmega')
source=source.replace('第三版','V16').replace('使用 10.png 原素材眼睛','-ω- 第一版')
source=source.replace('原素材贴图 >o<','-ω- 表情实验').replace('原素材贴图挤眼','-ω- 表情实验')
source=source.replace('&gt;o&lt;：保留原画的睫毛、紫色阴影与粉色边缘','-ω-：复用模型原眉毛几何 · 柔和短眼线 · 对称圆弧嘴型')
source=source.replace('本版眼睛裁自你指定的 10.png，校正原图倾斜后贴合脸部。保留小圆口与 neutral 过渡。','眼睛复制原模型眉毛的几何与 UV，压平主要弧度并加深复制件的颜色。ω 嘴型使用圆滑曲线，包含 neutral → -ω- → neutral 的完整过程。')
source=source.replace('当前为 Blender 审查预览，等待你的反馈；本版尚未重新验证 Unity。','已保留 V15 的 &gt;o&lt;。本页只提交 -ω- 第一版，等待人工审查。主 Blender 文件停在第 37 帧。')
source=source.replace('本版：原素材贴图','第一版：-ω-').replace('上一版：几何眼线','Neutral 对照')
source=source.replace("'../../V15_ChibiEyeReview/Preview/TestOmega_Face_'", "'TestOmega_Face_'")
source=source.replace('roots.map(root=>','roots.map((root,rootIndex)=>').replace("root+String(i).padStart", "root+String(rootIndex===1?0:i).padStart")
source=source.replace('原图与裁剪素材对照','头部正面定格').replace('../texture_crop_review.jpg','TestOmega_frontal.png')
source=source.replace('用户原图及校正后的双眼裁片','原眉毛复用的短闭眼与 ω 嘴型')
source=source.replace('<a href="../Textures/mika_squeeze_eyes.png">透明双眼贴图</a> · ','')
source=source.replace('../texture_crop_manifest.json','../omega_design.json').replace('裁剪记录','设计记录')
source=source.replace('上一版完整页面','已保留的 V15 &gt;o&lt;')
source=source.replace('let frame=0,playing=true','let frame=29,playing=false').replace('<button id="toggle">暂停</button>','<button id="toggle">播放</button>')
exec(compile(source,'omega_review_template','exec'))
html=(OUT/'index.html').read_text(encoding='utf-8')
html=html.replace('</main>','<p><a href="../../../CHDDD_actions_v2.blend">当前主 Blender 文件</a>。Blender 的 CHDDD 面板提供两个独立实验按钮：已保留 &gt;o&lt; 与 -ω- 第一版。</p></main>')
(OUT/'index.html').write_text(html,encoding='utf-8')
(OUT/'preview_inline.js').write_text(re.search(r'<script>(.*?)</script>',html,re.S).group(1),encoding='utf-8')
neutral_diff=ImageChops.difference(Image.open(OUT/'TestOmega_Face_000.png').convert('RGBA'),Image.open(MAIN/'Reviews/V15_ChibiEyeReview/Preview/TestSqueeze_Face_000.png').convert('RGBA'))
neutral=dict(passed=all(hi==0 for lo,hi in neutral_diff.getextrema()),rgbaDifferenceExtrema=neutral_diff.getextrema())
(ROOT/'neutral_preview_validation.json').write_text(json.dumps(neutral,indent=2),encoding='utf-8')
assert neutral['passed'],'Neutral image changed'
assert json.loads((ROOT/'recovery_validation.json').read_text(encoding='utf-8'))['passed']
status=dict(revision=16,expression='-ω-',status='awaiting-user-review',experimentNumber=2,
    eyeMethod='Duplicate native eyebrow geometry and UVs; flatten curvature, darken duplicated material, mirror',
    mouthMethod='Native symmetric cubic ribbon with round caps',
    previousSqueezeRevision=15,previousSqueezeStatus='provisionally-retained-per-user',
    bodyActionsChanged=False,existingShapeGeometryChanged=False,unityRevalidated=False,unityDeliveryRevision=7)
(ROOT/'review_status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'审查说明.md').write_text('''# V16：第二个表情实验 -ω-

按用户指示，V15 的 >o< 暂时保留，不继续调整。本轮只提交 -ω- 第一版。

双眼直接复制模型 CHDDD_Eyebrow 的一侧眉毛几何与 UV，镜像生成另一侧，压平主要弧度、保留端点和细微厚度，并加深复制件颜色。原模型眉毛保持原样。眼皮仍真实闭合，原眼线仅在本测试阶段隐藏。

ω 嘴型由对称的圆滑曲线构成，带圆润端点。原嘴部仅在本实验阶段淡出；完整过程为 neutral → 闭眼与 ω 嘴型 → 短暂停留 → neutral，全长 2.4 秒。

新增眼线与嘴型均单独绑定 Head。18 个身体动作、原表情形变及 V15 >o< 的图像、几何和时序保持一致。CHDDD 面板保留 >o< 按钮，并新增 -ω- 测试按钮；切换动作、标准表情或两个实验时会复位。

Preview/index.html 提供 neutral 对照、160／240 像素全身查看、慢放、逐帧与侧面检查。主文件保存在第 37 帧。本轮等待人工审查，Unity 包尚未同步。

修改前主文件与实时场景：Checkpoints/20261011_before_omega_v16/。
''',encoding='utf-8')
for name in ('body_v2_progress.json','body_v2_current_delivery_status.json'):
    path=MAIN/name;data=json.loads(path.read_text(encoding='utf-8'))
    if name=='body_v2_progress.json':data.update(revision=16,status='omega-first-experiment-awaiting-user-review',previousCheckpoint='Checkpoints/20261011_before_omega_v16')
    else:data.update(blenderRevision=16,expressionReview='omega-pending',unitySynchronized=False)
    data['latestReview' if name=='body_v2_progress.json' else 'latestPreview']='Reviews/V16_OmegaFirstReview/Preview/index.html'
    data['squeezeExpressionReview']={'revision':15,'status':'provisionally-retained-per-user'}
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
path=MAIN/'character_manifest.json';manifest=json.loads(path.read_text(encoding='utf-8'))
manifest['blenderWorkingCopy'].update(revision=16,pendingExpression='-ω- first experiment',latestReview='Reviews/V16_OmegaFirstReview/Preview/index.html')
manifest['blenderWorkingCopy']['squeezeExpressionReview']={'revision':15,'status':'provisionally-retained-per-user'}
path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8');shutil.copy2(path,ROOT/path.name)
path=MAIN/'README.md';text=path.read_text(encoding='utf-8');marker='<!-- omega-v16 -->'
if marker not in text:
    note='''<!-- omega-v16 -->
当前 Blender 制作版本：V16（2026-10-11）。V15 的 `>o<` 按用户指示暂时保留；新增第二个表情实验 `-ω-`，双眼复用原眉毛几何，嘴型为对称圆弧。主文件停在第 37 帧。最新预览：`Reviews/V16_OmegaFirstReview/Preview/index.html`，等待人工审查。Unity 包仍为 V7。备份：`Checkpoints/20261011_before_omega_v16/`。
<!-- /omega-v16 -->

'''
    path.write_text(text.replace('# CHDDD 恢复资产\n','# CHDDD 恢复资产\n\n'+note,1),encoding='utf-8')
sha_path=MAIN/'body_v2_checksums.sha256';paths={line.split('  ',1)[1] for line in sha_path.read_text(encoding='utf-8').splitlines() if '  ' in line}
sha_path.write_text(''.join(hashlib.sha256((MAIN/p).read_bytes()).hexdigest()+'  '+p+'\n' for p in sorted(paths) if (MAIN/p).is_file()),encoding='utf-8')
files=[p for p in ROOT.rglob('*') if p.is_file() and p.suffix in ('.blend','.json','.html','.gif','.jpg','.md','.py','.png','.js','.svg') and p.name!='review_checksums.json']
(ROOT/'review_checksums.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},ensure_ascii=False,indent=2),encoding='utf-8')
print('V16_OMEGA_FIRST_REVIEW_PACKAGED',flush=True)
