import json,sys
from pathlib import Path
import numpy as np
from PIL import Image
r=Path(__file__).resolve().parent
assets=Path('W:/Machine/VSCode/Mika_Gift/Material/Mika_MemoryLobby/MemoryLobby_Seaside/Assets/New Folder')
rows=json.loads((r/(sys.argv[1] if len(sys.argv)>1 else 'before.json')).read_text())
width,height=(int(sys.argv[3]) if len(sys.argv)>3 else 1280),720
scale=height/20.92
xmin=.5-20.92*width/height/2;ymax=19.65
canvas=np.zeros((height,width,4),np.float32)
textures={p.name:np.asarray(Image.open(p).convert('RGBA'),dtype=np.float32)/255 for p in assets.glob('*.png')}
for a in rows:
 v=np.array(a['vertices']).reshape(-1,2);uv=np.array(a['uvs']).reshape(-1,2)
 v[:,0]=(v[:,0]-xmin)*scale;v[:,1]=(ymax-v[:,1])*scale
 tex=textures[a['page']];th,tw=tex.shape[:2]
 for tri in np.array(a['triangles']).reshape(-1,3):
  points=v[tri];x0,y0=np.maximum(np.floor(points.min(axis=0)),0).astype(int);x1,y1=np.minimum(np.ceil(points.max(axis=0)),[width-1,height-1]).astype(int)
  if x1<x0 or y1<y0:continue
  x,y=np.meshgrid(np.arange(x0,x1+1)+.5,np.arange(y0,y1+1)+.5)
  (ax,ay),(bx,by),(cx,cy)=points
  det=(by-cy)*(ax-cx)+(cx-bx)*(ay-cy)
  if abs(det)<1e-10:continue
  b0=((by-cy)*(x-cx)+(cx-bx)*(y-cy))/det;b1=((cy-ay)*(x-cx)+(ax-cx)*(y-cy))/det;b2=1-b0-b1
  mask=(b0>=0)&(b1>=0)&(b2>=0)
  tuv=b0[...,None]*uv[tri[0]]+b1[...,None]*uv[tri[1]]+b2[...,None]*uv[tri[2]]
  tx=np.clip((tuv[...,0]*tw).astype(int),0,tw-1);ty=np.clip((tuv[...,1]*th).astype(int),0,th-1)
  color=tex[ty,tx]*a['color'];alpha=color[...,3:4]*mask[...,None]
  dest=canvas[y0:y1+1,x0:x1+1]
  dest[...,:3]=(dest[...,:3]*color[...,:3]*alpha+dest[...,:3]*(1-alpha)) if a['blend']=='Multiply' else (dest[...,:3]+color[...,:3]*alpha if a['blend']=='Additive' else color[...,:3]*alpha+dest[...,:3]*(1-alpha))
  dest[...,3:4]=alpha+dest[...,3:4]*(1-alpha)
Image.fromarray(np.clip(canvas*255,0,255).astype(np.uint8)).save(r/(sys.argv[2] if len(sys.argv)>2 else 'before.png'))
