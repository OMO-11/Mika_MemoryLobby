"""Read browser screenshots; verify actual changed clock pixels stay inside the Unity window."""
from pathlib import Path
import json
import math
from PIL import Image, ImageChops

root=Path(__file__).resolve().parent
screen=json.loads((root/'window_clock_ink_screen.json').read_text(encoding='utf-8'))
off=Image.open(root/'r5-window-ink-off.png').convert('RGB')
on=Image.open(root/'r5-window-ink-on.png').convert('RGB')
diff=ImageChops.difference(off,on)
box=diff.getbbox()
assert box, 'Clock was not rendered; an empty image is not a pass.'
rect=screen['scene'];scale=rect['width']/1280
pane=screen['pane'];safe=screen['safe']
def distance(p,polygon):
    return min(((b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0]))/math.dist(a,b)
               for a,b in zip(polygon,polygon[1:]+polygon[:1]))
changed=outside=outside_safe=0
minimum=float('inf')
for y in range(box[1],box[3]):
    for x in range(box[0],box[2]):
        if max(diff.getpixel((x,y)))<3:continue
        p=((x+.5-rect['x'])/scale,(y+.5-rect['y'])/scale)
        changed+=1;d=distance(p,pane);minimum=min(minimum,d)
        outside+=d<-.75
        outside_safe+=distance(p,safe)<-.75
result=dict(passed=changed>0 and outside==0 and outside_safe==0,
            method='Read-only pixel difference of the actual fitted SVG on/off at 1280x720, isolated from decorative guide and review-frame compositing; no image editing.',
            changedClockPixels=changed,outsideMeasuredWindowPixels=outside,
            outsideInsetSafeRegionPixels=outside_safe,minimumInkWindowClearance=minimum,
            logicalSize=[1280,720],screenshotSize=list(on.size),threshold=3)
(root/'clock_pixel_audit.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
assert result['passed'], 'Rendered clock pixels leaked outside the window.'
