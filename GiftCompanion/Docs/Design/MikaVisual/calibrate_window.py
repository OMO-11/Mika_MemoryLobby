"""Fit a complete clock rectangle inside the measured Unity window, with clearance."""
from pathlib import Path
import json
import math

root = Path(__file__).resolve().parent
geometry = json.loads((root / 'WindowProbe/geometry.json').read_text(encoding='utf-8'))
slots = {s['path']: s for s in geometry['slots'] if s['path']}
lower = slots['BG2/Window_01']['vertices']
upper = slots['BG2/Window_02']['vertices']
point = lambda v: (v['x'], v['y'])
cross = lambda a, b: a[0]*b[1]-a[1]*b[0]
sub = lambda a, b: (a[0]-b[0], a[1]-b[1])
def intersect(a,b,c,d):
    u,v = sub(b,a),sub(d,c)
    t=cross(sub(c,a),v)/cross(u,v)
    return (a[0]+t*u[0],a[1]+t*u[1])

# Inner edges of the lower sill and left sash meet behind the character.
bottom_left = intersect(point(lower[0]),point(lower[1]),point(upper[6]),point(upper[7]))
pane = [point(upper[6]),point(upper[5]),point(lower[1]),bottom_left]
clearance = 20.0
safe = [(clearance,clearance),(1280-clearance,clearance),
        (1280-clearance,720-clearance),(clearance,720-clearance)]
for a,b in zip(pane,pane[1:]+pane[:1]):
    edge=sub(b,a);length=math.hypot(*edge)
    signed=lambda p:cross(edge,sub(p,a))-clearance*length
    result=[]
    for p,q in zip(safe,safe[1:]+safe[:1]):
        dp,dq=signed(p),signed(q)
        if dp>=0:result.append(p)
        if (dp>=0)!=(dq>=0):
            t=dp/(dp-dq);result.append((p[0]+t*(q[0]-p[0]),p[1]+t*(q[1]-p[1])))
    safe=result

edge=sub(point(lower[1]),point(lower[0]));angle=math.atan2(edge[1],edge[0])
u=(math.cos(angle),math.sin(angle));v=(-u[1],u[0])
uv=[(p[0]*u[0]+p[1]*u[1],p[0]*v[0]+p[1]*v[1]) for p in safe]
lo=(min(p[0] for p in uv),min(p[1] for p in uv));hi=(max(p[0] for p in uv),max(p[1] for p in uv))
ratio=1.7
best=(0,None)
for ix in range(201):
    for iy in range(201):
        center=(lo[0]+(hi[0]-lo[0])*ix/200,lo[1]+(hi[1]-lo[1])*iy/200)
        height=float('inf')
        for a,b in zip(uv,uv[1:]+uv[:1]):
            e=sub(b,a)
            distance=cross(e,sub(center,a))
            height=min(height,2*distance/(abs(e[1])*ratio+abs(e[0])))
        if height>best[0]:best=(height,center)
h,center=best;h*=.96;w=h*ratio
corners_uv=[(center[0]-w/2,center[1]-h/2),(center[0]+w/2,center[1]-h/2),
            (center[0]+w/2,center[1]+h/2),(center[0]-w/2,center[1]+h/2)]
clock=[(p[0]*u[0]+p[1]*v[0],p[0]*u[1]+p[1]*v[1]) for p in corners_uv]
minimum=min(cross(sub(b,a),sub(p,a))/math.dist(a,b)
            for p in clock for a,b in zip(pane,pane[1:]+pane[:1]))
assert minimum>=clearance
assert all(clearance<=p[0]<=1280-clearance and clearance<=p[1]<=720-clearance for p in clock)
layout=dict(version=1,source='WindowProbe/geometry.json',width=1280,height=720,
            sourceSlots=['BG2/Window_01','BG2/Window_02'],pane=pane,safePolygon=safe,
            clockQuad=clock,clearancePixels=clearance,minimumClockClearance=minimum,
            angleDegrees=math.degrees(angle),fit='complete glyph bounding box; no oversized clipped text')
(root/'window_clock_layout.json').write_text(json.dumps(layout,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(layout,ensure_ascii=False,indent=2))
