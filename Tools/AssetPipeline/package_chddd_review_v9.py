"""Reuse the review player, publish second iteration with current review status."""
from pathlib import Path
import json,hashlib
PIPE=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline')
source=(PIPE/'package_chddd_review_v8.py').read_text(encoding='utf-8')
source=source.replace('V8_FirstReview','V9_SecondReview')
source=source.replace('V8 第一版待审查','V9 第二版待审查').replace('V8 · 第一版审查稿','V9 · 第二版审查稿')
source=source.replace('Y形举手 · 上弧笑眉','Y形欢呼 · 起举时迅速闭眼')
source=source.replace('双手护头 → 放下手抿嘴生气','HeadClick · 保留已审查动作')
source=source.replace('>o< 第一版试验 · 眼线有缺陷，待审查','>o< 第二版 · 连续挤眼眼线与小圆嘴')
source=source.replace('未通过视觉检查：眼线断裂、闭眼弧线残留；保留失败结果，等待本版审查后再修改。','第二版：闭合眼皮遮挡、连续的 > < 眼线、小圆嘴。请审查造型与进入／退出过程。')
source=source.replace('第一版审查稿。可检查完整动作、脸部过程及缩小后的可读性。','第二版审查稿。可检查完整动作、脸部过程及缩小后的可读性。')
source=source.replace('Success 与 HeadClick 已制作；&gt;o&lt; 试验存在眼线缺陷，尚不可用。','Success 已提前闭眼，HeadClick 保留已审查动作；&gt;o&lt; 为本轮第二版测试，等待你的审查。')
source=source.replace('第一版试验','第二版试验')
source='\n'.join(line for line in source.splitlines() if not line.startswith('print(json.dumps('))
# This source writes old first-review documentation/status at the end. Replace
# those fields below before exposing this review to the user.
exec(compile(source,'package_review9_player','exec'))
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V9_SecondReview')
html=(ROOT/'Preview/index.html').read_text(encoding='utf-8')
html=html.replace('<p><a href="../CHDDD_actions_v2.blend">','<p><a href="../../V8_FirstReview/Preview/index.html">对照上一版</a> · <a href="../CHDDD_actions_v2.blend">')
(ROOT/'Preview/index.html').write_text(html,encoding='utf-8')
status={'revision':9,'reviewIteration':2,'status':'awaiting-user-review','changes':['Success closes eyes at start of arm rise','Test >o< uses independent head-skinned chevron eye lines and narrower round mouth'],'headclickPreserved':True,'allBodyCurvesPreserved':True,'experimentalExpression':{'name':'>o<','approval':'Pending user review','acceptedAsStandard':False},'previewEngine':'Blender','unityRevalidated':False,'canonicalDeliveryUnmodified':True}
(ROOT/'review_status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'审查说明.md').write_text('''# 第二版审查稿（V9）

根据第一版审查反馈继续修改。本轮完成一版后停下，等待本版人工审查。

## Succeed / Success

保留上一版 Y 形双臂及所有身体动画曲线。将原本在 1.22 秒附近完全闭合的眼睛提前到 0.65 秒附近；双臂起举阶段迅速闭眼，随后保持闭眼欢呼和上弧笑眉。身体关键姿态及恢复节奏未修改。

## >o< 第二版测试

使用已审查的 Pet_Blink 闭合皮肤遮挡，停止使用第一版失败的眼线形变。新增连续的尖括号眼线，随 Head 骨骼运动；原眼线在试验期间切换隐藏。嘴型使用原素材的张口单元，并缩窄为小圆口。

顺序为：闭眼 → 挤眼眼线与圆嘴进入 → 停留 → 撤去挤眼眼线和圆嘴 → 睁眼复原。保留独立测试，不列为已通过的标准表情。

## 保留与验证

全部 18 个身体动画曲线、HeadClick 动作和原有表情形变数据与上一版一致。测试过程中保留源瞳孔缩放；旧的失败形变保持关闭。切回普通动作后，试验眼线材质参数清零。

当前预览由 Blender 渲染，本版尚未重新进行 Unity 导入验证。上一版审查稿及正式 V2 revision 7 交付均保留。

审查入口：Preview/index.html，支持 160/240 像素全身预览、面部特写、慢放与逐帧。可对照上一版页面。Blender 的 CHDDD 面板新增 >o< 第二版测试按钮。
''',encoding='utf-8')
files=[p for p in ROOT.rglob('*') if p.is_file() and p.suffix in ('.blend','.json','.html','.gif','.jpg','.md','.py') and p.name!='review_checksums.json']
(ROOT/'review_checksums.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},ensure_ascii=False,indent=2),encoding='utf-8')
print('SECOND_REVIEW_PACKAGED',str(ROOT/'Preview/index.html'))
