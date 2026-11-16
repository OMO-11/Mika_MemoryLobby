"""Source-art texture review, synchronized against the preceding geometry test."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V10_TextureReview');OUT=ROOT/'Preview'
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
def composed(path):
    im=Image.open(path).convert('RGBA');bg=Image.new('RGBA',im.size,(56,60,72,255));bg.alpha_composite(im);return bg.convert('RGB')
for view in ('Face','Full'):
    frames=[]
    for i in range(58):
        path=OUT/f'TestSqueeze_{view}_{i:03d}.png'
        if not path.exists():raise RuntimeError(f'Missing {path}')
        im=composed(path);canvas=Image.new('RGB',(im.width,im.height+42),(32,35,43));canvas.paste(im,(0,42));ImageDraw.Draw(canvas).text((12,10),'第三版 · 使用 10.png 原素材眼睛',font=font,fill='#f6eaf1');frames.append(canvas)
    frames[0].save(OUT/f'TestSqueeze_{view}_review.gif',save_all=True,append_images=frames[1:],duration=[40 if i%6!=5 else 50 for i in range(58)],loop=0,disposal=2)
sheet=Image.new('RGB',(1200,238),(32,35,43));draw=ImageDraw.Draw(sheet)
for column,i in enumerate((0,12,29,43,57)):
    im=composed(OUT/f'TestSqueeze_Face_{i:03d}.png');im.thumbnail((240,200));sheet.paste(im,(column*240,34));draw.text((column*240+8,6),f'{i/58*2.4:.2f}s',font=font,fill='white')
sheet.save(OUT/'face_process.jpg',quality=95)
html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>第三版 · 原素材贴图 >o< 审查</title>
<style>body{margin:0;background:#191d26;color:#f3edf3;font:16px/1.65 system-ui,"Microsoft YaHei";padding:28px}main{max-width:1200px;margin:auto}h1{font-size:28px;margin:0}h2{font-size:18px}p{color:#c9c2cf}.notice{background:#37313b;padding:14px 20px;border-left:4px solid #f0a5c9;border-radius:10px}article{background:#252a35;border:1px solid #444353;padding:22px;margin:22px 0;border-radius:16px}.views{display:flex;align-items:flex-start;gap:20px;flex-wrap:wrap}canvas{background:#383c48;border-radius:10px}.face{width:420px;height:350px;max-width:100%;height:auto}.old{width:300px;height:250px}.full{width:160px;height:193px}.controls{display:flex;gap:8px;flex-wrap:wrap;margin-top:18px}button,select{background:#363b49;border:1px solid #777083;border-radius:7px;color:white;padding:8px 10px;cursor:pointer}input{width:80%;margin-top:15px}output{margin-left:12px;font-variant-numeric:tabular-nums}details{margin-top:18px}details img{max-width:100%;height:auto}a{color:#efb2d0}.side{max-width:45%;height:auto;border-radius:8px}</style>
<main><h1>第三版 · 原素材贴图挤眼</h1><p>&gt;o&lt;：保留原画的睫毛、紫色阴影与粉色边缘</p>
<div class="notice">本版眼睛裁自你指定的 10.png，校正原图倾斜后贴合脸部。保留小圆口与 neutral 过渡。<br>当前为 Blender 审查预览，等待你的反馈；本版尚未重新验证 Unity。</div>
<article><div class="views"><div><h2>本版：原素材贴图</h2><canvas class="face" width="480" height="400"></canvas></div><div><h2>上一版：几何眼线</h2><canvas class="old" width="480" height="400"></canvas></div><div><h2>全身 160px</h2><canvas class="full" width="480" height="580"></canvas></div></div>
<div class="controls"><button id="toggle">暂停</button><button id="restart">从头</button><select id="speed"><option value="1">正常速度</option><option value="0.5">半速</option><option value="0.25">四分之一速</option></select><button id="prev">前一帧</button><button id="next">后一帧</button><select id="size"><option value="160">全身 160px</option><option value="240">全身 240px</option><option value="480">全身放大</option></select></div>
<input id="scrub" type="range" min="0" max="57" step="1" value="0"><output id="time">0.00s</output>
<details><summary>完整面部过程</summary><img src="face_process.jpg" alt="neutral 到挤眼圆嘴再恢复"></details>
<details><summary>原图与裁剪素材对照</summary><img src="../texture_crop_review.jpg" alt="用户原图及校正后的双眼裁片"><p><a href="../Textures/mika_squeeze_eyes.png">透明双眼贴图</a> · <a href="../texture_crop_manifest.json">裁剪记录</a></p></details>
<details><summary>左右侧面检查</summary><img class="side" src="TestSqueeze_side_-25.png" alt="左侧面"><img class="side" src="TestSqueeze_side_25.png" alt="右侧面"></details></article>
<p><a href="../CHDDD_actions_v2.blend">Blender 审查稿</a> · <a href="../审查说明.md">审查说明</a> · <a href="../../V9_SecondReview/Preview/index.html">上一版完整页面</a></p></main>
<script>
const n=58,duration=2.4;let frame=0,playing=true,speed=1,last=performance.now(),elapsed=0;
const canvases=[document.querySelector('.face'),document.querySelector('.old'),document.querySelector('.full')];
const roots=['TestSqueeze_Face_','../../V9_SecondReview/Preview/TestSqueeze_Face_','TestSqueeze_Full_'];
const sources=roots.map(root=>Array.from({length:n},(_,i)=>{const im=new Image();im.src=root+String(i).padStart(3,'0')+'.png';im.onload=()=>{if(i===frame)draw()};return im}));
const scrub=document.querySelector('#scrub'),out=document.querySelector('#time'),toggle=document.querySelector('#toggle');
function draw(){scrub.value=frame;out.value=(frame/n*duration).toFixed(2)+'s';canvases.forEach((c,j)=>{const im=sources[j][frame],ctx=c.getContext('2d');ctx.clearRect(0,0,c.width,c.height);if(im.complete&&im.naturalWidth)ctx.drawImage(im,0,0,c.width,c.height)})}
function stop(){playing=false;toggle.textContent='播放'}
toggle.onclick=()=>{playing=!playing;toggle.textContent=playing?'暂停':'播放';elapsed=0};document.querySelector('#restart').onclick=()=>{frame=0;elapsed=0;draw()};
scrub.oninput=()=>{stop();frame=+scrub.value;draw()};document.querySelector('#speed').onchange=e=>speed=+e.target.value;
document.querySelector('#prev').onclick=()=>{stop();frame=Math.max(0,frame-1);draw()};document.querySelector('#next').onclick=()=>{stop();frame=Math.min(n-1,frame+1);draw()};
document.querySelector('#size').onchange=e=>{canvases[2].style.width=e.target.value+'px';canvases[2].style.height=(+e.target.value*580/480)+'px'};
function tick(now){const dt=Math.min(.1,(now-last)/1000);last=now;if(playing){elapsed+=dt*speed;const step=Math.floor(elapsed/(duration/n));if(step){elapsed-=step*duration/n;frame=(frame+step)%n;draw()}}requestAnimationFrame(tick)}draw();requestAnimationFrame(tick);
</script></html>'''
(OUT/'index.html').write_text(html,encoding='utf-8')
status={'revision':10,'reviewIteration':3,'status':'awaiting-user-review','expression':'>o<','method':'Original user-supplied 10.png eye crops projected onto head-skinned face decals','source':'Material/Mika_MemoryLobby/spine_regions/10.png','mouth':'Existing narrow round mouth retained; original sprite smile not used','allBodyAndFaceActionsPreserved':True,'originalTextureUnmodified':True,'previewEngine':'Blender','unityRevalidated':False,'acceptedAsStandardExpression':False}
(ROOT/'review_status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'审查说明.md').write_text('''# 第三版 >o< 贴图审查稿（V10）

按用户指定素材 `Material/Mika_MemoryLobby/spine_regions/10.png` 裁剪左右眼。仅进行裁剪、21°倾斜校正与透明边缘重采样，保留素材的睫毛、紫色阴影和粉色边缘，未重画或改变原素材颜色。

原素材嘴型是笑嘴，本版继续保留 >o< 的小圆口。双眼贴图投射到随 Head 骨骼运动的弧面上。原眼皮继续负责闭合，试验时隐藏原眼线和白色眼内贴片，退出时恢复。

所有身体动画、Succeed 提前闭眼设定、HeadClick 以及原表情形变数据与上一版一致。贴图为独立试验，不并入已验收标准表情。

Preview/index.html 提供本版／上一版同步对照、全身 160/240 像素查看、慢放、逐帧、正侧面与裁剪来源。当前为 Blender 审查结果，本版尚未复测 Unity。

本轮停在第三版，等待人工审查后再继续修改。前两版审查稿、原始 10.png 和正式 revision 7 交付均保留。
''',encoding='utf-8')
files=[p for p in ROOT.rglob('*') if p.is_file() and p.suffix in ('.blend','.json','.html','.gif','.jpg','.md','.py','.png') and p.name!='review_checksums.json']
(ROOT/'review_checksums.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},ensure_ascii=False,indent=2),encoding='utf-8')
print('TEXTURE_REVIEW_PACKAGED',str(OUT/'index.html'))
