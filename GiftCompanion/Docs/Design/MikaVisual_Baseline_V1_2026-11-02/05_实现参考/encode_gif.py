"""Encode browser-rendered frames as a GIF; no painting or scene compositing in Python."""
from pathlib import Path
import json,hashlib
from PIL import Image,ImageChops
root=Path(__file__).resolve().parent.parent/'01_确认效果'
paths=sorted((root/'Frames').glob('*.png'))
assert len(paths)==174
# One palette for the entire sequence keeps the night sky from changing color between frames.
samples=Image.new('RGB',(960,540*3))
for i,index in enumerate([0,32,82]):
 with Image.open(paths[index]) as im:samples.paste(im.convert('RGB'),(0,540*i))
palette=samples.quantize(colors=192)
frames=[]
for path in paths:
 with Image.open(path) as im:
  assert im.size==(960,540)
  frames.append(im.convert('RGB').quantize(palette=palette,dither=Image.Dither.NONE))
out=root/'窗边运镜_第一版确定稿.gif'
frames[0].save(out,save_all=True,append_images=frames[1:],duration=[70 if i%3==0 else 60 if i%3==1 else 70 for i in range(len(frames))],loop=0,optimize=True,disposal=1)
with Image.open(out) as gif:
 duration=0
 for i in range(gif.n_frames):gif.seek(i);duration+=gif.info.get('duration',0)
 assert duration==11600
 report={'passed':True,'framesRendered':len(paths),'encodedFrames':gif.n_frames,'size':list(gif.size),'durationMs':duration,'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest()}
(root/'gif-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
