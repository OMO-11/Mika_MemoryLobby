"""Publish exactly one local review draft, with honest visual-test status."""
from pathlib import Path
import json,hashlib
from PIL import Image,ImageDraw,ImageFont
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V8_FirstReview')
OUT=ROOT/'Preview';OUT.mkdir(exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',14)
configs=[('Success','Y形举手 · 上弧笑眉',82,3.4),('HeadClick','双手护头 → 放下手抿嘴生气',43,1.8),('TestSqueeze','>o< 第一版试验 · 眼线有缺陷，待审查',58,2.4)]
def composite(path):
    im=Image.open(path).convert('RGBA');bg=Image.new('RGBA',im.size,(56,60,72,255));bg.alpha_composite(im);return bg.convert('RGB')
for name,label,count,duration in configs:
    view='Face' if name=='TestSqueeze' else 'Full'
    frames=[]
    for i in range(count):
        p=OUT/f'{name}_{view}_{i:03d}.png'
        if not p.exists():raise RuntimeError(f'Missing review frame: {p}')
        im=composite(p);canvas=Image.new('RGB',(im.width,im.height+42),(32,35,43));canvas.paste(im,(0,42));ImageDraw.Draw(canvas).text((12,10),label,font=font,fill='#f6eaf1');frames.append(canvas)
    frames[0].save(OUT/f'{name}_review.gif',save_all=True,append_images=frames[1:],duration=[40 if i%6!=5 else 50 for i in range(count)],loop=0,disposal=2)
    face_paths=[OUT/f'{name}_Face_{i:03d}.png' for i in range(count)]
    for p in face_paths:
        if not p.exists():raise RuntimeError(f'Missing face frame: {p}')
    indices=[0,round(count*.2),round(count*.45),round(count*.7),count-1]
    sheet=Image.new('RGB',(5*240,238),(32,35,43));draw=ImageDraw.Draw(sheet)
    for column,i in enumerate(indices):
        im=composite(face_paths[i]);im.thumbnail((240,200));sheet.paste(im,(column*240,34));draw.text((column*240+8,8),f'{i/count*duration:.2f}s',font=small,fill='white')
    sheet.save(OUT/f'{name}_face_process.jpg',quality=94)
cards=[]
for name,label,count,duration in configs:
    cards.append(f'''<article data-name="{name}" data-count="{count}" data-duration="{duration}">
<h2>{label}</h2>
<p>{'未通过视觉检查：眼线断裂、闭眼弧线残留；保留失败结果，等待本版审查后再修改。' if name=='TestSqueeze' else '第一版审查稿。可检查完整动作、脸部过程及缩小后的可读性。'}</p>
<div class="views">{'' if name=='TestSqueeze' else '<canvas class="full" width="480" height="580"></canvas>'}<canvas class="face" width="480" height="400"></canvas></div>
<div class="controls"><button class="toggle">暂停</button><button class="restart">从头</button><select class="speed"><option value="1">正常速度</option><option value="0.5">半速</option><option value="0.25">四分之一速</option></select><select class="size"><option value="240">全身 240px</option><option value="160">全身 160px</option><option value="480">全身放大</option></select><button class="prev">前一帧</button><button class="next">后一帧</button></div>
<input class="scrub" type="range" min="0" max="{count-1}" step="1" value="0"><output>0.00s</output>
<details><summary>面部过程关键帧</summary><img src="{name}_face_process.jpg" alt="{name}面部过程"></details></article>''')
html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>V8 第一版待审查</title>
<style>body{margin:0;background:#191d26;color:#f3edf3;font:16px/1.7 system-ui,"Microsoft YaHei";padding:28px}main{max-width:1080px;margin:auto}h1{font-size:28px;margin:0}h2{font-size:20px}p{color:#c9c2cf}.notice{background:#37313b;padding:14px 20px;border-left:4px solid #f0a5c9;border-radius:10px}article{background:#252a35;border:1px solid #444353;padding:22px;margin:22px 0;border-radius:16px}.views{display:flex;align-items:center;gap:22px;flex-wrap:wrap}canvas{background:#383c48;border-radius:10px;object-fit:contain}.full{width:240px;height:290px}.face{width:420px;height:350px;max-width:100%;height:auto}.controls{display:flex;gap:8px;flex-wrap:wrap;margin-top:18px}button,select{background:#363b49;border:1px solid #777083;border-radius:7px;color:white;padding:8px 10px;cursor:pointer}input{width:80%;margin-top:15px}output{margin-left:12px;font-variant-numeric:tabular-nums}details{margin-top:12px}details img{max-width:100%;height:auto}a{color:#efb2d0}</style>
<main><h1>V8 · 第一版审查稿</h1><p>Success / HeadClick / &gt;o&lt; 表情试验</p>
<div class="notice">当前停在这一版，等待你的审查。Success 与 HeadClick 已制作；&gt;o&lt; 试验存在眼线缺陷，尚不可用。<br>本页为 Blender 审查预览；本版尚未重新进行 Unity 导入验证。上一版正式交付文件保留。</div>
'''+''.join(cards)+'''<p><a href="../CHDDD_actions_v2.blend">打开本版 Blender 文件</a> · <a href="../审查说明.md">审查说明与已知问题</a></p></main>
<script>
for(const a of document.querySelectorAll('article')){
 const name=a.dataset.name,n=+a.dataset.count,duration=+a.dataset.duration;
 let frame=0,playing=true,speed=1,last=performance.now(),elapsed=0;
 const scrub=a.querySelector('.scrub'),out=a.querySelector('output'),toggle=a.querySelector('.toggle');
 const full=a.querySelector('.full'),face=a.querySelector('.face');const sources={};
 for(const view of full?['Full','Face']:['Face'])sources[view]=Array.from({length:n},(_,i)=>{const im=new Image();im.src=`${name}_${view}_${String(i).padStart(3,'0')}.png`;im.onload=()=>{if(i===frame)draw()};return im});
 function draw(){scrub.value=frame;out.value=`${(frame/n*duration).toFixed(2)}s`;for(const [view,c] of [['Full',full],['Face',face]]){if(!c)continue;const im=sources[view]?.[frame];const ctx=c.getContext('2d');ctx.clearRect(0,0,c.width,c.height);if(im?.complete&&im.naturalWidth)ctx.drawImage(im,0,0,c.width,c.height)}}
 function stop(){playing=false;toggle.textContent='播放'}
 toggle.onclick=()=>{playing=!playing;toggle.textContent=playing?'暂停':'播放';elapsed=0};
 a.querySelector('.restart').onclick=()=>{frame=0;elapsed=0;draw()};
 scrub.oninput=()=>{stop();frame=+scrub.value;draw()};
 a.querySelector('.speed').onchange=e=>{speed=+e.target.value};
 a.querySelector('.size').onchange=e=>{if(full){full.style.width=e.target.value+'px';full.style.height=(+e.target.value*580/480)+'px'}};
 a.querySelector('.prev').onclick=()=>{stop();frame=Math.max(0,frame-1);draw()};
 a.querySelector('.next').onclick=()=>{stop();frame=Math.min(n-1,frame+1);draw()};
 function tick(now){const dt=Math.min(.1,(now-last)/1000);last=now;if(playing){elapsed+=dt*speed;const step=Math.floor(elapsed/(duration/n));if(step){elapsed-=step*duration/n;frame=(frame+step)%n;draw()}}requestAnimationFrame(tick)}draw();requestAnimationFrame(tick);
}
</script></html>'''
(OUT/'index.html').write_text(html,encoding='utf-8')
status={'revision':8,'reviewIteration':1,'status':'awaiting-user-review','bodyDrafts':['Success','HeadClick'],'experimentalExpression':{'name':'>o<','visualPassed':False,'issues':['Broken eyeliner','Residual normal closed-eye arc'],'acceptedAsStandard':False},'previewEngine':'Blender','unityRevalidated':False,'canonicalDeliveryUnmodified':True}
(ROOT/'review_status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'审查说明.md').write_text('''# V8 第一版审查稿

本版停在人工审查环节。收到本版反馈之前，不制作第二版，不并入正式交付。

## 本版动作

- Success：双臂向斜上方展开成 Y 形，约 37～39 度；保留上一版躯干、挺胸和踮脚曲线。闭眼喜悦阶段使用独立的上弧眉毛形变。
- HeadClick：闭眼受惊时双手护住头顶两侧，随后放下手，抿嘴、皱眉佯装生气。

## >o< 第一版测试结果

尚未通过视觉检查。测试保留既有闭眼皮肤遮挡，未缩放或变形瞳孔，但眼线变形后出现断裂，普通闭眼弧线仍有残留，因此当前效果还不能称为可用的 >o< 表情。本页完整展示这次结果，不列为已验收表情。

定位过程中修正了将眼内贴片误当作眼线的网格选择错误；未继续制作第二种美术方案。下一版需在本版审查后处理眼线连续性和完整的尖括号轮廓。

## 审查入口

打开 Preview/index.html，可查看 240/160 像素全身动作、面部过程、慢放与逐帧；或打开此目录下 CHDDD_actions_v2.blend，在 CHDDD 面板选择 Success、HeadClick 或 >o< 第一版测试。

本页为 Blender 渲染；本版没有重新执行 Unity 导入验证。正式 V2 revision 7 的 blend、FBX、unitypackage 和已审查表情模板未替换。完整基线和当时 Blender 场景已备份至 Checkpoints/V2_revision7_before_y_headcover_test。

数据验证通过原有动作/表情保持、Success 躯干与脚部曲线保持、瞳孔不变形和测试形变不泄漏等检查。数据验证不代表试验表情已通过视觉验收。
''',encoding='utf-8')
files=[p for p in ROOT.rglob('*') if p.is_file() and p.suffix in ('.blend','.json','.html','.gif','.jpg','.md','.py') and p.name!='review_checksums.json']
(ROOT/'review_checksums.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'review':str(OUT/'index.html'),'motionFrames':sum(c[2]*(1 if c[0]=='TestSqueeze' else 2) for c in configs),'expressionVisualPassed':False,'status':'awaiting-user-review'}))
