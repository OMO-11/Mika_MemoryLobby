import json,math
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD');OUT=ROOT/'Previews/ActionsV2'
entries=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))['actions']
revision=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))['revision']
def rgb_frame(path):
    im=Image.open(path).convert('RGBA')
    background=Image.new('RGBA',im.size,(73,86,106,255));background.alpha_composite(im)
    return background.convert('RGB')
labels={'Idle':'待机','Walk':'行走','Thinking':'思考','Working':'工作','HeadPat':'摸头','Dragging':'举起','Sleep':'打盹','Wave':'挥手','Success':'庆祝','Confused':'疑惑','HeadClick':'头部点击','BodyClick':'身体点击','HeadPat_Enter':'开始摸头','HeadPat_Exit':'结束摸头','Dragging_Enter':'拿起','Dragging_Exit':'放下','Sleep_Enter':'开始打盹','Sleep_Exit':'唤醒'}
details={
 'Idle':'呼吸与轻微重心变化，偶尔微笑、自然眨眼。',
 'Walk':'保留原 Café 行走节奏，窗口移动由桌宠程序控制。',
 'Thinking':'托腮、视线左右寻找、眉头轻抬，疑惑嘴型逐渐转为会意的微笑。',
 'Working':'双手交替小幅操作，头部跟随关注，专注间隙短暂微笑、眨眼。',
 'HeadPat':'低头和轻轻侧倾，头发晚于转头回落；从放松微笑到享受的闭眼，再柔和睁开。',
 'Dragging':'使用原 GLB 第 29 条 Formation_Pickup：保留游戏内举起、腿部摆动、转头与发衣动画；搭配惊讶、微笑、侧看和眨眼的独立过程。',
 'Sleep':'闭眼打盹，缓慢呼吸、轻点头与低幅度发衣运动。',
 'Wave':'修订6 · 特殊演出：注意、收身准备、探身招手；展示清楚的问候姿态后挥动两次，最后柔和放松。适合低频欢迎。',
 'Success':'修订6 · 情绪响应：收手蓄势、踮脚握拳、保持开心约0.6秒、缓冲回落。双肩、重心、双手、足部共同参与。',
 'Confused':'修订6 · 情绪响应：停下、后缩、手靠脸颊、歪头约23度，保持约0.6秒，再慢慢恢复；身体和轮廓承担疑惑信息。',
 'HeadClick':'修订6 · 特殊演出：双肩与双手一起受惊、短暂停顿，再歪头认出用户、放松回应。短暂反馈需由桌宠端限制频率。',
 'BodyClick':'修订6 · 情绪响应：后缩、收肩、双手近胸、脚尖内扣，视线移开后短暂回望，再恢复站姿。',
 'HeadPat_Enter':'身体柔和进入摸头姿态，同时逐渐放松眼睑和嘴角。',
 'HeadPat_Exit':'头部回正、手部收回，眨眼后回到平静待机。',
 'Dragging_Enter':'被拿起时先惊讶，身体进入原版举起姿态，表情接上举起循环。',
 'Dragging_Exit':'由悬空举起姿态平滑放下，表情和头发逐渐恢复。',
 'Sleep_Enter':'先困倦、抬手掩嘴打呵欠，再低头闭眼。',
 'Sleep_Exit':'渐渐抬头、睁眼，短暂眨眼后恢复待机。'}
contact=Image.new('RGB',(7*240,len(entries)*312),(27,33,45));draw=ImageDraw.Draw(contact)
review=[]
for row,e in enumerate(entries):
    name=e['name'];paths=sorted(OUT.glob(name+'_motion_*.png'));expected=round(e['durationSeconds']*24)
    assert len(paths)==expected,(name,len(paths),expected)
    palette=rgb_frame(paths[len(paths)//2]).quantize(colors=256)
    frames=[rgb_frame(p).quantize(palette=palette,dither=Image.Dither.NONE) for p in paths]
    durations=[round((i+1)*e['durationSeconds']*100/len(paths))*10-round(i*e['durationSeconds']*100/len(paths))*10 for i in range(len(paths))]
    frames[0].save(OUT/(name+'.gif'),save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False,disposal=2)
    for i in range(7):
        index=round(i*(len(paths)-1)/6);im=rgb_frame(paths[index]).resize((240,290))
        contact.paste(im,(i*240,row*312+22));draw.text((i*240+7,row*312+4),name+' '+str(round(index/(len(paths)-1)*100))+'%',fill='white')
    review.append(dict(e,label=labels[name],detail=e['intent'] if name in ('Confused','Success','Wave','HeadClick','BodyClick') else details[name],previewFrames=len(paths)))
    print(name,len(paths),flush=True)
contact.save(OUT/'body_v2_contact_sheet.jpg',quality=92)
options=''.join('<option value="'+e['name']+'">'+e['label']+' · '+e['name']+'</option>' for e in review)
html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>未花 · 动作优化 V2</title>
<style>body{margin:0;background:#181e2a;color:#f3eaf0;font:16px system-ui,"Microsoft YaHei",sans-serif}main{max-width:1120px;margin:24px auto;padding:20px}h1{font-size:25px}select,button{background:#323d50;color:white;border:1px solid #677185;border-radius:8px;padding:10px;font:inherit}canvas,img{max-width:100%;border-radius:14px}.view{display:flex;gap:24px;align-items:flex-start;margin-top:22px}.note{line-height:1.8;flex:1}canvas{width:480px}#previous{width:300px}a{color:#ffc6e3}.controls{display:flex;gap:12px;align-items:center;flex-wrap:wrap}input{width:260px}@media(max-width:760px){.view{display:block}}</style>
<main><h1>未花 · 动作优化 V2</h1><p>原版举起动画 · 发衣跟随 · 动作专属表情过程</p>
<div class="controls"><label>查看动作 <select id="choice">OPTIONS</select></label><button id="play">暂停</button><button id="replay">从头播放</button><label>速度 <select id="speed"><option value="1">正常</option><option value=".5">半速</option><option value=".25">四分之一</option></select></label></div>
<div class="view"><div><canvas id="animation" width="480" height="580"></canvas><p><input id="scrub" type="range" min="0" max="1000" value="0"> <span id="time"></span></p></div><div class="note"><h2 id="name"></h2><p id="detail"></p><p id="status"></p><p>这是连续渲染预览。短反馈与进入、退出动作在本页重复播放，方便审查；Unity 中按单次动作处理。</p><p>可重点观察头发是否滞后回落、衣服是否跟随肢体，以及表情是否贴合动作节奏。</p><p><label><input id="compare" style="width:auto" type="checkbox"> 显示第一版对比</label></p><img id="previous" hidden alt="第一版动作对比"><p><a href="body_v2_contact_sheet.jpg">全部动作抽样图</a> · <a id="gif" href="Idle.gif">下载当前 GIF</a></p></div></div></main>
<script>const entries=DATA;let entry,frames=[],token=0,playing=true,phase=0,last=performance.now();const canvas=document.getElementById('animation'),ctx=canvas.getContext('2d');async function show(){let own=++token;entry=entries.find(e=>e.name===choice.value);phase=0;frames=[];document.getElementById('name').textContent=entry.label;detail.textContent=entry.detail;status.textContent=entry.durationSeconds.toFixed(2)+' 秒 · '+(entry.loop?'循环':'单次');gif.href=entry.name+'.gif';previous.src='../ActionsV1/'+entry.name+'.gif';ctx.clearRect(0,0,480,580);let promises=Array.from({length:entry.previewFrames},(_,i)=>new Promise(resolve=>{let im=new Image;im.onload=()=>resolve(im);im.onerror=()=>resolve(null);im.src=entry.name+'_motion_'+String(i).padStart(3,'0')+'.png';}));let loaded=await Promise.all(promises);if(own===token)frames=loaded;}
function tick(now){let dt=Math.min(.10,(now-last)/1000);last=now;if(entry&&frames.length){if(playing)phase=(phase+dt*Number(speed.value)/entry.durationSeconds)%1;let i=Math.min(frames.length-1,Math.floor(phase*frames.length));if(frames[i])ctx.drawImage(frames[i],0,0);scrub.value=phase*1000;document.getElementById('time').textContent=(phase*entry.durationSeconds).toFixed(2)+'s';}requestAnimationFrame(tick);}choice.onchange=show;document.getElementById('play').onclick=()=>{playing=!playing;document.getElementById('play').textContent=playing?'暂停':'播放';};replay.onclick=()=>{phase=0;playing=true;document.getElementById('play').textContent='暂停';};scrub.oninput=()=>{phase=Number(scrub.value)/1000;playing=false;document.getElementById('play').textContent='播放';};compare.onchange=()=>previous.hidden=!compare.checked;choice.value='Dragging';show();requestAnimationFrame(tick);</script></html>'''
html=html.replace('<script>const entries=', '<script>const choice=document.getElementById("choice"),detail=document.getElementById("detail"),status=document.getElementById("status"),gif=document.getElementById("gif"),previous=document.getElementById("previous"),speed=document.getElementById("speed"),scrub=document.getElementById("scrub"),replay=document.getElementById("replay"),compare=document.getElementById("compare");const entries=')
html=html.replace('显示第一版对比','显示对比版本 <select id="compareVersion"><option value="Revision6/">上一轮 · 修订6</option><option value="Revision5/">修订5</option><option value="Revision4/">修订4</option><option value="Revision3/">修订3</option><option value="BeforeResume/">暂停前 V2</option><option value="../ActionsV1/">第一版 V1</option></select>')
html=html.replace('previous.src=\'../ActionsV1/\'+entry.name+\'.gif\';', 'previous.src=document.getElementById("compareVersion").value+entry.name+".gif";')
html=html.replace("choice.value='Dragging';show();", 'document.getElementById("compareVersion").onchange=()=>previous.src=document.getElementById("compareVersion").value+entry.name+".gif";choice.value="Wave";show();')
html=html.replace('未花 · 动作优化 V2','未花 · 动作优化 V2 修订'+str(revision)).replace('原版举起动画 · 发衣跟随 · 动作专属表情过程','全身身体语言 · 关键姿态停留 · 分层发衣跟随 · 动作专属表情')
html=html.replace('if(frames[i])ctx.drawImage(frames[i],0,0);','if(frames[i]){ctx.clearRect(0,0,480,580);ctx.drawImage(frames[i],0,0);}')
html=html.replace('<main><h1>','<main><p><a href="readability.html">小尺寸与轮廓同步审查</a></p><h1>')
html=html.replace('OPTIONS',options).replace('DATA',json.dumps(review,ensure_ascii=False))
(OUT/'index.html').write_text(html,encoding='utf-8')
(ROOT/'body_v2_preview_validation.json').write_text(json.dumps({'passed':True,'revision':revision,'fps':24,'actions':len(review),'nativeFrames':sum(e['previewFrames'] for e in review),'preview':'Previews/ActionsV2/index.html'},indent=2),encoding='utf-8')
