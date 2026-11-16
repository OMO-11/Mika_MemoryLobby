"""Package native-frame review, scheduling contract, and final file hashes."""
from pathlib import Path
import json,hashlib
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Material/CHDDD/RandomIdleV1'
data=json.loads((OUT/'idle_action_map_v1.json').read_text(encoding='utf-8'))
report=json.loads((OUT/'validation.json').read_text(encoding='utf-8'))
expected=0;images=[]
for e in data['actions']:
    count=round(e['durationSeconds']*30);expected+=count
    files=list((OUT/'Preview30').glob(e['name']+'_*.png'))
    assert len(files)==count,(e['name'],len(files),count)
    e['previewFrames']=count
    sample=OUT/'Preview30'/f"{e['name']}_{count//2:03d}.png"
    if e['name'] in ('IdleLookAround','IdleArmSettle'):sample=OUT/'Preview30'/f"{e['name']}_{int(count*.35):03d}.png"
    with Image.open(sample) as image:
        bg=Image.new('RGBA',image.size,(48,51,61,255));bg.alpha_composite(image.convert('RGBA'));images.append(bg.convert('RGB'))
sheet=Image.new('RGB',(4*320,2*422),(48,51,61));draw=ImageDraw.Draw(sheet)
for i,(e,image) in enumerate(zip(data['actions'],images)):
    x=i%4*320;y=i//4*422;sheet.paste(image,(x,y));draw.text((x+12,y+394),e['name'],fill='white')
sheet.save(OUT/'Preview30/contact_sheet.jpg',quality=92)
html='''<!doctype html><html lang="zh"><meta charset="utf-8"><title>CHDDD 常态随机待机 V1</title>
<style>body{background:#1f222a;color:#e7e8ee;font:16px system-ui;margin:30px auto;max-width:1080px}button,select,input{font:inherit;margin:6px;padding:8px;background:#333947;color:inherit;border:1px solid #636c80;border-radius:6px}canvas{background:#30333d;max-width:100%;height:auto}p{line-height:1.7}.row{display:flex;gap:30px;align-items:center;flex-wrap:wrap}#log{color:#c6cfe6;font:14px monospace}a{color:#adc9ff}</style>
<h1>常态随机待机 · 第一版</h1><p>7 条独立动作，统一 Neutral 基准，无根位移、无情绪切换。原生 30 FPS Blender 帧；支持随机连续、顺序连续和单条循环。流畅度与小幅动作请优先正常速度审查。</p>
<div><select id="mode"><option value="random">随机连续（不连续重复同条）</option><option value="sequence">顺序连续</option><option value="single">单条循环</option></select><select id="clip"></select><select id="speed"><option value="1">1× 正常</option><option value="0.5">0.5×</option><option value="0.25">0.25×</option></select><button id="play">暂停</button><button id="restart">从头播放</button></div>
<div class="row"><canvas id="view" width="320" height="388"></canvas><div><p id="state">加载中</p><input id="seek" type="range" min="0" max="1" step="0.001" value="0"><p id="log"></p><p>脚部固定；首尾位置误差小于 0.000001 模型单位。<br>已检查全部 49 种有序拼接。<br>独立 FBX 已回导检查；尚未进行 Unity 播放验证。</p><a href="../README.md">制作与接入说明</a> · <a href="../validation.json">检查记录</a></div></div>
<p>本组片段在边界连续衔接。中途切换和与旧 Idle 的连接使用约 0.28 秒交叉混合；预览未伪造未知动作之间的兼容性。小图为完整帧直接显示，放大不能增加原生细节。</p><img src="contact_sheet.jpg" style="width:100%">
<script>const clips=DATA; const $=id=>document.getElementById(id);let index=0,nextIndex=1,frame=0,elapsed=0,paused=false,last=0,ready=false,generation=0;const cache=new Map();const ctx=$('view').getContext('2d');
clips.forEach((c,i)=>{$('clip').add(new Option(c.label+' · '+c.durationSeconds+' 秒',i))});
function pick(){if($('mode').value==='single')return index;if($('mode').value==='sequence')return(index+1)%clips.length;return(index+1+Math.floor(Math.random()*(clips.length-1)))%clips.length}
async function load(i){if(cache.has(i))return cache.get(i);let promise=Promise.all(Array.from({length:clips[i].previewFrames},(_,f)=>new Promise((resolve,reject)=>{let im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=clips[i].name+'_'+String(f).padStart(3,'0')+'.png'})));cache.set(i,promise);return promise}
let images=[];async function start(i){let token=++generation;ready=false;index=i;elapsed=0;images=await load(index);if(token!==generation)return;nextIndex=pick();load(nextIndex);ready=true;$('clip').value=index;last=performance.now();paint()}
function paint(){let c=clips[index];frame=Math.min(images.length-1,Math.floor(elapsed*30));ctx.clearRect(0,0,320,388);ctx.drawImage(images[frame],0,0);$('state').textContent=c.label+' / '+c.name+' · '+elapsed.toFixed(2)+' / '+c.durationSeconds.toFixed(2)+'s';$('seek').value=elapsed/c.durationSeconds}
async function tick(now){let delta=Math.min(.1,(now-last)/1000);last=now;if(ready&&!paused){elapsed+=delta*Number($('speed').value);if(elapsed>=clips[index].durationSeconds){ready=false;let old=index;index=nextIndex;images=await load(index);nextIndex=pick();load(nextIndex);for(let k of cache.keys())if(k!==index&&k!==nextIndex)cache.delete(k);elapsed=0;ready=true;last=performance.now();$('clip').value=index;$('log').textContent=clips[old].name+' → '+clips[index].name}paint()}requestAnimationFrame(tick)}
$('play').onclick=()=>{paused=!paused;$('play').textContent=paused?'播放':'暂停'};$('restart').onclick=()=>start(index);$('clip').onchange=()=>start(Number($('clip').value));$('mode').onchange=()=>{nextIndex=pick();load(nextIndex)};$('seek').oninput=()=>{elapsed=Number($('seek').value)*clips[index].durationSeconds;paint()};start(0);requestAnimationFrame(tick);
</script></html>'''.replace('DATA',json.dumps(data['actions'],ensure_ascii=False))
(OUT/'Preview30/index.html').write_text(html,encoding='utf-8')
table='\n'.join(f"| `{e['name']}` | {e['label']} | {e['durationSeconds']:.1f} 秒 | `{e['clip']}` |" for e in data['actions'])
(OUT/'README.md').write_text('''# CHDDD 常态随机待机 V1

制作日期：2026-10-23。按本轮用户要求，仅从模型的动作连续性出发设计，不加入角色性格、情绪演出或物理跟随。源为 V17 主文件，保留原文件及既有动作不变；本组保存为独立制作副本。

## 方案与成品

| ID | 内容 | 时长 | Blender / FBX 动作 |
| --- | --- | --- | --- |
'''+table+'''

使用低强度、固定根节点、固定脚部和统一 Neutral 面部。左右微倾只改变上身姿态，不移动骨盆或根节点。头发、衣服和光环随其现有蒙皮与父骨骼变换，不额外制作物理摆动。

曲线使用连续的正弦包络（端点一至三阶导数为零）、统一边界姿态、四元数同半球及逐帧平滑关键帧。端部仅两个采样精确归零，不加入较长的中立停顿；动作在过程内渐进加速和减速。

## 使用

- `CHDDD_random_idle_v1.blend`：新的独立主文件。打开后时间轴已排列约 34.4 秒连续演示，直接播放即可；NLA 轨道为 `RandomIdle_ContinuousDemo`。
- `RandomIdleControls.py`：在 Blender 文本编辑器运行后，侧栏 CHDDD / Random Idle V1 可单独选择 7 个片段。脚本不会自动改变原 V17 文件。
- `CHDDD_random_idle_v1.fbx`：只导出本组 7 个骨骼动作，Generic，不含武器与实验表情几何；已实际回导 Blender 检查名称。Unity 导入与最终画面仍需验证。
- `idle_action_map_v1.json`：名字、时长、范围、默认混合、随机资格与演示序列；路径属于本目录。
- [30 FPS 连续随机预览](Preview30/index.html)：正常 / 慢放、拖动进度、单条 / 随机 / 顺序连续。使用真实 Blender 连续帧，不用静帧插值冒充动画。

## 衔接约定

本组全部片段从同一个 `PetV2_Idle` 第 1 帧基准出发并返回，首尾所有骨骼位置、旋转和缩放一致，速度归零。可任意排序、重复或随机组合；随机选择在片段完成时进行，默认不连续重复同一条。不依赖根位移，也不累计旋转。

本组使用绝对姿态，未配置为 Additive Clip。进入本组时从现有 Idle 交叉混合约 0.28 秒；常态调度器在每条结束后选择下一条。**中途打断须从当前姿态交叉混合，不能将未知动画任意帧硬切接入。** 若外部时间轴要严格硬拼，外部 Clip 也必须满足相同基准及零边界速度约定；本次验证范围是本组 7×7 组合，不包含所有旧动作。

若新时间轴使用本 FBX，禁止再同时让旧 BodyController 或其他 Animator 写同一身体层；Emotion 可作为独立受控层，但不能覆盖 Head 的本组运动。首版固定 Neutral，没有自动眨眼或嘴型变化，便于单独审查身体连续性。Root Motion 保持关闭，保留 Generic 骨骼和特殊骨骼。

## 检查结果

检查全部 49 种有序拼接的骨骼端点；按四分之一帧采样所有 146 骨骼，检查非有限值、脚部漂移、速度与加速度。检查通过，数值详见 [validation.json](validation.json)。角速度 / 加速度阈值是本组低强度动作的筛查条件，不代表自动证明观感完美，连续预览仍供人工审查。

Blender / FBX 独立交付，不改变原 Unity V7 动作包，也不宣称已接入正式桌宠。原 V17 制作源哈希记录在映射文件中。此前“表情实验结束”的冻结状态继续保留，本轮是新的常态身体动作任务。
''',encoding='utf-8')
report['preview']={'fps':30,'frames':expected,'size':[320,388],'path':'Preview30/index.html'}
(OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'idle_action_map_v1.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
files=[OUT/n for n in ['CHDDD_random_idle_v1.blend','CHDDD_random_idle_v1.fbx','idle_action_map_v1.json','RandomIdleControls.py','README.md','validation.json','Preview30/index.html']]
(OUT/'checksums.sha256').write_text('\n'.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(OUT).as_posix() for p in files)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'clips':len(data['actions']),'nativeFrames':expected,'preview':'Preview30/index.html'}))

