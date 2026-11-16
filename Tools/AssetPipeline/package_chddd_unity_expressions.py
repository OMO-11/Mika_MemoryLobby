"""Package actual Unity GPU frames, native desktop sizes and Blender comparisons."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib,re
MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
ROOT=MAIN/'Reviews/UnityExpressionV17';OUT=ROOT/'Preview'
validation=json.loads((ROOT/'unity_validation.json').read_text(encoding='utf-8'))
assert validation['passed'] and validation['frames']==348
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
title=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
def composed(path):
    image=Image.open(path).convert('RGBA');background=Image.new('RGBA',image.size,(56,60,72,255));background.alpha_composite(image);return background.convert('RGB')
for name in ('Squeeze','Omega'):
    for view in ('Face','Full160','Full240'):
        expected={'Face':(480,400),'Full160':(160,193),'Full240':(240,290)}[view]
        frames=[]
        for index in range(58):
            path=OUT/f'{name}_{view}_{index:03d}.png'
            assert Image.open(path).size==expected
            frames.append(composed(path))
        frames[0].save(OUT/f'{name}_{view}.gif',save_all=True,append_images=frames[1:],duration=[40 if i%6!=5 else 50 for i in range(58)],loop=0,disposal=2)
    sheet=Image.new('RGB',(720,130),(32,35,43));draw=ImageDraw.Draw(sheet)
    for column,index in enumerate((0,10,20,29,43,57)):
        image=composed(OUT/f'{name}_Face_{index:03d}.png');image.thumbnail((120,100));sheet.paste(image,(column*120,28))
        draw.text((column*120+8,2),f'{index/58*2.4:.2f}s',font=font,fill='white')
    sheet.save(OUT/f'{name}_process.jpg',quality=96)

# Native images are pasted at their exact output resolution, without resizing.
sheet=Image.new('RGB',(944,770),(32,35,43));draw=ImageDraw.Draw(sheet)
for row,(name,label) in enumerate((('Squeeze','>< · V15'),('Omega','-ω- · V17'))):
    top=row*380
    draw.text((16,top+10),'Unity 实渲 · '+label,font=title,fill='white')
    for x,view,label in ((16,'Face','面部近景'),(512,'Full160','原生 160px'),(688,'Full240','原生 240px')):
        draw.text((x,top+48),label,font=font,fill='#d9cadb')
        image=composed(OUT/f'{name}_{view}_029.png')
        if view=='Face':image=image.resize((480,400))
        # Face is a cropped upper section to keep the contact sheet compact;
        # full face frames remain available untouched in the review page.
        if view=='Face':image=image.crop((0,60,480,350))
        sheet.paste(image,(x,top+80))
sheet.save(OUT/'unity_expression_comparison.png')

html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Unity 实渲 · >< 与 -ω-</title><style>
*{box-sizing:border-box}body{margin:0;padding:24px;background:#191d26;color:#f3edf3;font:16px/1.65 system-ui,"Microsoft YaHei"}main{max-width:1100px;margin:auto}h1{font-size:27px;margin:0}h2{font-size:18px;margin:0 0 10px}p{color:#cfc4d4}.notice,article{background:#252a35;border:1px solid #494250;border-radius:14px;padding:20px;margin:18px 0}.notice{border-left:4px solid #eaa2c9}.controls{display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin:16px 0}button,select{background:#363b49;border:1px solid #81718a;border-radius:7px;color:white;padding:9px 12px;cursor:pointer}button.active{background:#70475f;border-color:#eca6cc}.views{display:flex;align-items:flex-start;gap:22px;flex-wrap:wrap}canvas{display:block;background:#383c48;border-radius:8px}.face{max-width:100%;height:auto}.native{flex-shrink:0}.controls input{min-width:200px;flex:1}output{font-variant-numeric:tabular-nums}details{margin-top:16px}details img{max-width:100%;height:auto}a{color:#efb2d0}.subtle{font-size:14px;color:#b7acbf}</style>
<main><h1>Unity 实际渲染：&gt;&lt; 与 -ω-</h1>
<div class="notice">Unity 6000.3.7f1 · Direct3D 11 · 8× MSAA。两种表情均使用最新模型的真实骨骼、形变与独立眼嘴几何；包含 neutral → 表情 → neutral 完整过程。<br>160／240 像素画面由 Unity 直接按该分辨率渲染，按原尺寸显示。</div>
<div class="controls"><button class="active" id="squeeze">&gt;&lt; · V15</button><button id="omega">-ω- · V17</button><button id="toggle">播放</button><button id="restart">从头</button><select id="speed"><option value="1">正常速度</option><option value=".5">半速</option><option value=".25">四分之一速</option></select><button id="prev">前一帧</button><button id="next">后一帧</button></div>
<div class="controls"><input id="scrub" type="range" min="0" max="57" step="1" value="29"><output id="time">1.20s</output></div>
<article><div class="views"><div><h2>Unity · 面部近景</h2><canvas class="face" id="unityFace" width="480" height="400"></canvas></div><div><h2>Blender · 对照</h2><canvas class="face" id="blenderFace" width="480" height="400"></canvas></div></div><p class="subtle">Unity 使用工程现有 Toon 基础材质与光照；实验表情使用 Unity 着色器处理眼线、闭眼蒙版和嘴型。整体光照、颜色与描边可与 Blender 有差别。</p></article>
<article><h2>桌宠尺寸 · Unity 原生渲染</h2><div class="views"><div class="native"><p>160 × 193</p><canvas id="full160" width="160" height="193"></canvas></div><div class="native"><p>240 × 290</p><canvas id="full240" width="240" height="290"></canvas></div></div></article>
<details><summary>两种表情定格与桌宠尺寸对照</summary><img src="unity_expression_comparison.png" alt="Unity 实渲两种表情"></details>
<p><a href="../unity_validation.json">Unity 渲染记录</a> · <a href="../审查说明.md">审查说明</a> · <a href="../../V17_OmegaThickerReview/Preview/index.html">上一轮 Blender 审查</a></p>
<p class="subtle">本轮为独立 Unity 渲染验证；正式桌宠的运行时表情流程与 V7 交付包尚未整体更新。原 Blender 主文件保持原样。</p></main>
<script>
const n=58,duration=2.4;let expression='Squeeze',frame=29,playing=false,speed=1,last=performance.now(),elapsed=0;
const canvases=['unityFace','blenderFace','full160','full240'].map(id=>document.getElementById(id));
const sources={};
for(const name of ['Squeeze','Omega']){
const blender=name==='Squeeze'?'../../V15_ChibiEyeReview/Preview/TestSqueeze_Face_':'../../V17_OmegaThickerReview/Preview/TestOmega_Face_';
sources[name]=[name+'_Face_',blender,name+'_Full160_',name+'_Full240_'].map(root=>Array.from({length:n},(_,i)=>{const im=new Image();im.src=root+String(i).padStart(3,'0')+'.png';im.onload=()=>{if(i===frame&&name===expression)draw()};return im}));}
const toggle=document.getElementById('toggle'),scrub=document.getElementById('scrub'),out=document.getElementById('time');
function draw(){scrub.value=frame;out.value=(frame/n*duration).toFixed(2)+'s';canvases.forEach((c,j)=>{const im=sources[expression][j][frame],ctx=c.getContext('2d');ctx.clearRect(0,0,c.width,c.height);if(im.complete&&im.naturalWidth)ctx.drawImage(im,0,0,c.width,c.height)})}
function stop(){playing=false;toggle.textContent='播放'}
function select(name){stop();expression=name;frame=29;elapsed=0;document.getElementById('squeeze').classList.toggle('active',name==='Squeeze');document.getElementById('omega').classList.toggle('active',name==='Omega');draw()}
document.getElementById('squeeze').onclick=()=>select('Squeeze');document.getElementById('omega').onclick=()=>select('Omega');
toggle.onclick=()=>{playing=!playing;toggle.textContent=playing?'暂停':'播放';elapsed=0};document.getElementById('restart').onclick=()=>{frame=0;elapsed=0;draw()};
scrub.oninput=()=>{stop();frame=+scrub.value;draw()};document.getElementById('speed').onchange=e=>speed=+e.target.value;
document.getElementById('prev').onclick=()=>{stop();frame=Math.max(0,frame-1);draw()};document.getElementById('next').onclick=()=>{stop();frame=Math.min(n-1,frame+1);draw()};
function tick(now){const dt=Math.min(.1,(now-last)/1000);last=now;if(playing){elapsed+=dt*speed;const step=Math.floor(elapsed/(duration/n));if(step){elapsed-=step*duration/n;frame=(frame+step)%n;draw()}}requestAnimationFrame(tick)}draw();requestAnimationFrame(tick);
</script></html>'''
(OUT/'index.html').write_text(html,encoding='utf-8')
(OUT/'preview_inline.js').write_text(re.search(r'<script>(.*?)</script>',html,re.S).group(1),encoding='utf-8')
differences={}
for name in ('Squeeze','Omega'):
    images=[hashlib.sha256((OUT/f'{name}_Face_{i:03d}.png').read_bytes()).hexdigest() for i in range(58)]
    differences[name]={'distinctFaceFrames':len(set(images)),'transitionVisible':len(set(images))>20}
assert all(item['transitionVisible'] for item in differences.values())
(ROOT/'frame_validation.json').write_text(json.dumps(differences,indent=2),encoding='utf-8')
assert hashlib.sha256((MAIN/'CHDDD_actions_v2.blend').read_bytes()).hexdigest()==validation['sourceSHA256']
(ROOT/'审查说明.md').write_text('''# >< 与 -ω-：Unity 原生渲染审查

使用项目规定的 Unity 6000.3.7f1、Direct3D 11 与现有 Built-in Toon 基础材质。>< 为暂时保留的 V15，-ω- 为 V17 加粗版。模型导出自当前 V17 主文件；主 Blender 文件没有改动。

FBX 导入真实骨骼、8 个形变与三组 Head 绑定实验几何。Blender 的眼部隐藏蒙版显式导出为顶点颜色，再由 Unity 着色器处理原眼线隐藏、闭眼区域与原嘴部淡出。闭眼区域的重复描边及折面亮线已在测试着色器中修正。

两个表情各 2.4 秒、24 FPS，直接使用 Blender 原逐帧参数驱动 Unity 形变和材质。Unity 分别渲染 480×400 近景、160×193 与 240×290 全身，合计 348 张原生分辨率截图；小图没有从大图缩小生成。每个姿态隔多个编辑器帧后再读取 GPU 画面，避免连续 Camera.Render 复用上一帧骨骼缓存。

Preview/index.html 提供两种表情切换、Unity 与 Blender 同步对照、慢放、逐帧与原生桌宠尺寸。默认停在表情充分展现的第 29 张截图。

独立测试工程：W:/Machine/VSCode/Mika_Gift/Temp/UnityExpressionReviewV17/。可使用 Unity 6000.3.7f1 打开 Assets/Review/UnityExpressions.unity；CHDDD Review/Render expressions 菜单可重新渲染。正式 GiftCompanion 工程及 V7 交付包尚未整体更新，本轮只验证两个实验表情的 Unity 显示。
''',encoding='utf-8')
record=dict(passed=True,sourceBlenderRevision=17,expressions={'squeeze':15,'omega':17},unityVersion=validation['unityVersion'],
    preview='Reviews/UnityExpressionV17/Preview/index.html',scope='independent-expression-render-review',fullRuntimeSynchronized=False)
path=MAIN/'body_v2_current_delivery_status.json';status=json.loads(path.read_text(encoding='utf-8'));status['unityExpressionReview']=record
path.write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf-8')
sha_path=MAIN/'body_v2_checksums.sha256';paths={line.split('  ',1)[1] for line in sha_path.read_text(encoding='utf-8').splitlines() if '  ' in line}
sha_path.write_text(''.join(hashlib.sha256((MAIN/p).read_bytes()).hexdigest()+'  '+p+'\n' for p in sorted(paths) if (MAIN/p).is_file()),encoding='utf-8')
files=[p for p in ROOT.rglob('*') if p.is_file() and p.suffix in ('.json','.html','.gif','.jpg','.md','.png','.js') and p.name!='review_checksums.json']
(ROOT/'review_checksums.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},ensure_ascii=False,indent=2),encoding='utf-8')
print('UNITY_EXPRESSION_REVIEW_PACKAGED',json.dumps(differences),flush=True)
