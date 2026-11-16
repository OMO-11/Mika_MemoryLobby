"""Package the native Blender renders into a local, selectable action review."""
import json, html
from pathlib import Path
from PIL import Image, ImageDraw

root=Path('Material/CHDDD')
preview=root/'Previews/ActionsV1'
mapping=json.loads((root/'Unity/body_action_map_v1.json').read_text(encoding='utf-8'))
entries=mapping['actions']
labels={'Idle':'待机','Walk':'行走','Thinking':'思考','Working':'工作','HeadPat':'摸头','Dragging':'拖动','Sleep':'打盹','Wave':'挥手','Success':'小庆祝','Confused':'困惑','HeadClick':'头部点击','BodyClick':'身体点击','HeadPat_Enter':'开始摸头','HeadPat_Exit':'结束摸头','Dragging_Enter':'拿起','Dragging_Exit':'放下','Sleep_Enter':'开始打盹','Sleep_Exit':'唤醒'}
sheet=Image.new('RGB',(1680,len(entries)*550),'#242833')
draw=ImageDraw.Draw(sheet)
for row,entry in enumerate(entries):
    files=sorted(preview.glob(entry['name']+'_motion_*.png'))
    assert len(files)==round(entry['durationSeconds']*12),(entry['name'],len(files))
    frames=[Image.open(p).convert('RGB') for p in files]
    palette=frames[0].quantize(colors=160)
    gif=[f.quantize(palette=palette) for f in frames]
    durations=[round(1000*entry['durationSeconds']/len(gif))]*len(gif)
    if not entry['loop']:durations[-1]+=700
    gif[0].save(preview/(entry['name']+'.gif'),save_all=True,append_images=gif[1:],duration=durations,loop=0,optimize=True,disposal=2)
    for col,idx in enumerate((0,len(frames)//3,2*len(frames)//3,len(frames)-1)):
        sheet.paste(frames[idx],(col*420,row*550+30))
        draw.text((col*420+8,row*550+8),entry['name']+f' / {idx/len(frames)*entry["durationSeconds"]:.2f}s',fill='white')
sheet.save(preview/'body_v1_contact_sheet.jpg',quality=90)
sheet.resize((840,len(entries)*275)).save(preview/'body_v1_contact_sheet_small.jpg',quality=88)
options=''.join(f'<option value="{html.escape(e["name"])}">{html.escape(labels[e["name"]])} · {html.escape(e["name"])}</option>' for e in entries)
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>未花 · 第一版动作审查</title>
<style>body{margin:0;background:#181e2a;color:#f3eaf0;font:16px system-ui,"Microsoft YaHei",sans-serif}main{max-width:1000px;margin:32px auto;padding:20px}h1{font-size:25px}select,button{background:#323d50;color:white;border:1px solid #677185;border-radius:8px;padding:10px;font:inherit}img{max-width:100%;border-radius:16px}.view{display:flex;gap:32px;align-items:flex-start;margin-top:24px}.note{line-height:1.9;flex:1}#animation{width:420px}a{color:#ffc6e3}@media(max-width:720px){.view{display:block}}</style>
<main><h1>未花 · 第一版动作审查</h1><p>18 个动作 · 无武器 · 光环抬高 · 已验收表情配合身体动作</p>
<label>查看动作 <select id="choice">OPTIONS</select></label> <button id="replay">重新播放</button>
<div class="view"><img id="animation" src="Idle.gif" alt="动作预览"><div class="note"><h2 id="name">待机</h2><p id="detail"></p><p>这是 Blender 渲染预览。单次动作和衔接短片为方便观察会重复播放；Unity 内它们按非循环处理。</p><p>第一版用于检查姿态、节奏和动作衔接，后续可按人工反馈继续调整。</p><p><a href="body_v1_contact_sheet.jpg">查看全部动作抽样图</a></p></div></div></main>
<script>const entries=ENTRIES;const labels=LABELS;const c=document.querySelector('#choice'),im=document.querySelector('#animation');function show(){const e=entries.find(x=>x.name===c.value);im.src=e.name+'.gif?t='+Date.now();document.querySelector('#name').textContent=labels[e.name]+' · '+e.name;document.querySelector('#detail').textContent=(e.loop?'循环动作':'单次动作')+' / '+e.durationSeconds.toFixed(2)+' 秒 / 配套表情 '+e.emotion;}c.onchange=show;document.querySelector('#replay').onclick=show;show();</script></html>'''
page=page.replace('OPTIONS',options).replace('ENTRIES',json.dumps(entries,ensure_ascii=False)).replace('LABELS',json.dumps(labels,ensure_ascii=False))
(preview/'index.html').write_text(page,encoding='utf-8')
print(json.dumps({'gifs':len(entries),'review':str(preview/'index.html')},ensure_ascii=False))
