"""Author a mirrored vector eye silhouette and rasterize coverage at high resolution."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageChops
import json

ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V14_ChibiEyeReview')
OUT=ROOT/'Textures';OUT.mkdir(parents=True,exist_ok=True)
COLOR=(86,66,96)
START=(20,25)
COMMANDS=[
    ('C',(13.3,22.3),(9.3,32.3),(16,35)),
    ('C',(46,45),(78,55),(104,64)),
    ('C',(75,75),(42,86),(18,94)),
    ('C',(12.67,96),(15.67,104),(21,102)),
    ('L',(114,69)),
    ('C',(120,67),(120,61),(114,59)),
    ('L',(20,25)),
]
points=[START];p=START
for command in COMMANDS:
    if command[0]=='L':
        p=command[1];points.append(p)
    else:
        a,b,c=command[1:]
        for step in range(1,65):
            t=step/64;s=1-t
            points.append(tuple(s*s*s*p[k]+3*s*s*t*a[k]+3*s*t*t*b[k]+t*t*t*c[k] for k in (0,1)))
        p=c
mask=Image.new('L',(2048,2048))
ImageDraw.Draw(mask).polygon([(round(x*16),round(y*16)) for x,y in points],fill=255)
mask=mask.resize((512,512),Image.Resampling.LANCZOS)
left=Image.new('RGBA',mask.size,COLOR+(0,));left.putalpha(mask)
right=left.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
atlas=Image.new('RGBA',(1024,512),COLOR+(0,));atlas.paste(left,(0,0));atlas.paste(right,(512,0))
atlas.save(OUT/'mika_squeeze_eyes.png')
path='M '+' '.join(map(str,START))+' '+' '.join(c[0]+' '+' '.join(str(v) for pair in c[1:] for v in pair) for c in COMMANDS)+' Z'
svg=f'''<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="512" viewBox="0 0 256 128">
<title>Symmetric chibi squeeze eyes</title>
<defs><path id="eye" d="{path}"/></defs>
<g fill="#564260"><use href="#eye"/><use href="#eye" transform="translate(256 0) scale(-1 1)"/></g>
</svg>'''
(OUT/'mika_squeeze_eyes.svg').write_text(svg,encoding='utf-8')
mirror_error=ImageChops.difference(left,right.transpose(Image.Transpose.FLIP_LEFT_RIGHT)).getextrema()
assert all(hi==0 for lo,hi in mirror_error)
meta=dict(revision=14,method='Original vector silhouette, mirrored exactly; not a crop or upscale of the lobby art',
    source='Textures/mika_squeeze_eyes.svg',output='Textures/mika_squeeze_eyes.png',
    textureResolution=[1024,512],rasterCoverageResolution=[2048,2048],
    leftRightPixelMirrorExact=True,rgbaMirrorDifferenceExtrema=mirror_error,
    color='#564260',upperAngleApproxDegrees=20,lowerAngleApproxDegrees=21,
    style='Rounded outer ends, tapered lower stroke, rounded shared corner; no extra painted crease',
    alpha='Straight RGBA with constant edge RGB and supersampled coverage')
(ROOT/'chibi_eye_design.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
proof=Image.new('RGBA',(1024,512),(248,220,217,255));proof.alpha_composite(atlas)
proof.convert('RGB').save(ROOT/'chibi_eye_design.jpg',quality=96)
print('V14_MIRRORED_VECTOR_EYES_CREATED',flush=True)
