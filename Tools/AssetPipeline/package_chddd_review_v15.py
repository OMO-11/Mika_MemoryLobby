"""Publish one V15 eye review with synchronized V14 comparison and size checks."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

task_root=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews')
task_sheet=Image.new('RGB',(520,710),(32,35,43))
task_draw=ImageDraw.Draw(task_sheet)
task_font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
for task_row,task_version in enumerate(('V14','V15')):
    task_top=task_row*350
    task_draw.text((16,task_top+8),task_version+' · 全身 160px / 240px',font=task_font,fill='white')
    task_image=Image.open(task_root/f'{task_version}_ChibiEyeReview/Preview/TestSqueeze_Full_029.png').convert('RGBA')
    for task_x,task_width in ((16,160),(232,240)):
        task_scaled=task_image.resize((task_width,round(task_width*580/480)),Image.Resampling.LANCZOS)
        task_bg=Image.new('RGBA',task_scaled.size,(56,60,72,255));task_bg.alpha_composite(task_scaled)
        task_sheet.paste(task_bg.convert('RGB'),(task_x,task_top+42))
task_sheet.save(task_root/'V15_ChibiEyeReview/Preview/small_size_comparison.png')
source=Path(__file__).with_name('package_chddd_review_v14.py').read_text(encoding='utf-8')
source=source.replace('V14','V15').replace('v14','v15').replace('V13_EyeAngleReview','V14_ChibiEyeReview').replace('V13','V14')
source=source.replace('revision=14','revision=15').replace('blenderRevision=14','blenderRevision=15').replace('reviewIteration=7','reviewIteration=8')
source=source.replace('Q 版对称眼线','稍粗稍小眼线').replace('Q 版镜像眼线','加粗缩小眼线')
source=source.replace('本版：Q 版重绘眼线','本版：稍粗稍小眼线').replace('上一版：V14 裁片眼线','上一版：V14 对称眼线')
source=source.replace('眼线重新绘制为镜像矢量轮廓，并同步校正双眼平面。保留 V14 的开合程度，圆滑端点、收细下眼线，去除原裁片的毛边和多余细线。小圆口与 neutral 过渡沿用原设定。',
    '在 V14 矢量眼线上增加少量圆角描边，再以原中心缩至 94%。保留左右镜像、现有开合角度、小圆口与 neutral 过渡。可用全身 160／240 像素检查缩小后的辨识度。')
source=source.replace('Original mirrored vector silhouette with high resolution RGBA, symmetric head-bound flat cards and closed-lid seam cleanup',
    'V14 vector glyph with a 2.4-unit rounded outline and 0.94 uniform scale; existing head-bound flat cards and skin material retained')
source=source.replace('originalCutoutUsed=False,textureResolution=', 'originalCutoutUsed=False,uniformGlyphScale=.94,roundedOutlineWidth=2.4,textureResolution=')
source=source.replace('按用户反馈，改为重新绘制的矢量轮廓，左右严格镜像。保留 V14 约 20° / 21° 的上下开合程度，使用与模型睫毛接近的深紫色；上笔稍厚、下笔稍细，外端与尖角平滑。没有继续使用原立绘裁片的笔触、粉边与多余细线。',
    '按用户反馈，在 V14 的矢量轮廓上增加 2.4 单位圆角描边（单眼坐标范围 128），随后围绕各自中心统一缩至 94%。因此笔画略厚、整体轮廓略小。左右仍严格镜像，开合角度、颜色和圆滑端点沿用 V14。')
source=source.replace('两个独立平面仍绑定 Head 骨骼，并在头部绑定空间内同步校正为镜像位置与朝向；未改为曲面贴合。闭眼皮肤的亮边只在本测试表情中清理，退出时恢复原材质。身体动作、眼眉形变、小圆口与表情时序保持原设定。',
    '本轮只更换独立测试表情的眼线图像。两个 Head 绑定平面的几何、位置、朝向，原有闭眼皮肤处理、身体动作、眼眉形变、小圆口与表情时序均沿用 V14。')
source=source.replace('`><` 改为 Q 版镜像矢量眼线，使用平滑透明边缘与对称平面，并清理旧闭眼亮边。',
    '`><` 眼线略增粗并以原中心缩至 94%，保留左右对称与原有角度。')
source=source.replace("status='chibi-eye-symmetry-awaiting-user-review'", "status='thicker-smaller-chibi-eyes-awaiting-user-review'")
source=source.replace("extra='''<details>", "extra='''<details open><summary>桌宠尺寸前后对照</summary><img src=\"small_size_comparison.png\" alt=\"V14 与 V15 在 160 和 240 像素下的比较\"></details><details>")
exec(compile(source,'review15_package','exec'))
