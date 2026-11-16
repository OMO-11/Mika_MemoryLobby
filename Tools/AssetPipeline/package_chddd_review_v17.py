"""Package one thicker omega revision with V16 and desktop-size comparisons."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
task_root=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews')
task_sheet=Image.new('RGB',(520,710),(32,35,43));task_draw=ImageDraw.Draw(task_sheet)
task_font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
for task_row,task_folder in enumerate(('V16_OmegaFirstReview','V17_OmegaThickerReview')):
    task_top=task_row*350
    task_draw.text((16,task_top+8),task_folder[:3]+' · 全身 160px / 240px',font=task_font,fill='white')
    task_image=Image.open(task_root/task_folder/'Preview/TestOmega_Full_029.png').convert('RGBA')
    for task_x,task_width in ((16,160),(232,240)):
        task_scaled=task_image.resize((task_width,round(task_width*580/480)),Image.Resampling.LANCZOS)
        task_bg=Image.new('RGBA',task_scaled.size,(56,60,72,255));task_bg.alpha_composite(task_scaled)
        task_sheet.paste(task_bg.convert('RGB'),(task_x,task_top+42))
task_sheet.save(task_root/'V17_OmegaThickerReview/Preview/small_size_comparison.png')

source=Path(__file__).with_name('package_chddd_review_v16.py').read_text(encoding='utf-8')
source=source.replace('V16_OmegaFirstReview','V17_OmegaThickerReview').replace('V16','V17').replace('v16','v17')
source=source.replace('revision=16','revision=17').replace('blenderRevision=16','blenderRevision=17')
source=source.replace('第一版','加粗版').replace('omega-first-experiment-awaiting-user-review','omega-thicker-strokes-awaiting-user-review')
source=source.replace('-ω- first experiment','-ω- thicker strokes').replace('V17_OMEGA_FIRST_REVIEW_PACKAGED','V17_OMEGA_THICKER_REVIEW_PACKAGED')
source=source.replace('眼睛复制原模型眉毛的几何与 UV，压平主要弧度并加深复制件的颜色。ω 嘴型使用圆滑曲线，包含 neutral → -ω- → neutral 的完整过程。',
    '在 V16 的基础上将眼线增厚至 1.55 倍、嘴线增厚至 1.65 倍。保留原来的线条轮廓、颜色、圆润端点与 neutral 过渡。右侧同步显示 V16，便于比较。')
source=source.replace('本轮只提交 -ω- 加粗版。','本轮只调整 -ω- 的线条粗细，眼线为 V16 的 1.55 倍，嘴线为 1.65 倍。中心线、颜色、表情时序和已有动作保持原设定。')
source=source.replace("eyeMethod='Duplicate native eyebrow geometry and UVs; flatten curvature, darken duplicated material, mirror'", "eyeMethod='V16 original eyebrow geometry; 1.55x thickness around unchanged centerline'")
source=source.replace("mouthMethod='Native symmetric cubic ribbon with round caps'", "mouthMethod='V16 native cubic omega ribbon and round caps; 1.65x stroke thickness'")
source=source.replace("experimentNumber=2,", "experimentNumber=2,reviewIteration=2,eyeStrokeWidthMultiplier=1.55,mouthStrokeWidthMultiplier=1.65,")
source=source.replace('提供 neutral 对照、160／240 像素全身查看','提供 V16 同步对照、160／240 像素全身查看')
source=source.replace('新增第二个表情实验 `-ω-`，双眼复用原眉毛几何，嘴型为对称圆弧。','第二个表情实验 `-ω-` 的眼线增厚至 1.55 倍，嘴线增厚至 1.65 倍，保留原来的轮廓与过渡。')
# Inject the comparison after the base HTML has been generated, before its hashes.
injection='''html=html.replace('Neutral 对照','V16：加粗前')
html=html.replace("const roots=['TestOmega_Face_','TestOmega_Face_','TestOmega_Full_'];", "const roots=['TestOmega_Face_','../../V16_OmegaFirstReview/Preview/TestOmega_Face_','TestOmega_Full_'];")
html=html.replace('String(rootIndex===1?0:i)','String(i)')
html=html.replace('</article>','<details open><summary>桌宠尺寸前后对照</summary><img src="small_size_comparison.png" alt="V16 与 V17 在 160 和 240 像素下的比较"></details></article>',1)
'''
source=source.replace("(OUT/'index.html').write_text(html,encoding='utf-8')",injection+"(OUT/'index.html').write_text(html,encoding='utf-8')")
exec(compile(source,'omega17_package','exec'))
