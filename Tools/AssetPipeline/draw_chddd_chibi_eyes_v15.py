"""Re-author V14 vector coverage with a modest rounded outline and size reduction."""
from pathlib import Path
from PIL import Image, ImageChops
import json

TOOLS = Path(__file__).parent
source = (TOOLS/'draw_chddd_chibi_eyes_v14.py').read_text(encoding='utf-8')
source = source.replace('V14_ChibiEyeReview', 'V15_ChibiEyeReview').replace('revision=14', 'revision=15')
source = source.replace("mask=Image.new('L',(2048,2048))", """SHAPE_SCALE=.94
OUTLINE_WIDTH=2.4
points=[(64+(x-64)*SHAPE_SCALE,64+(y-64)*SHAPE_SCALE) for x,y in points]
mask=Image.new('L',(2048,2048))""")
source = source.replace("mask=mask.resize((512,512),Image.Resampling.LANCZOS)", """# Match an SVG round-joined outline, applied before the uniform scale.
draw=ImageDraw.Draw(mask)
pixels=[(x*16,y*16) for x,y in points]
width=OUTLINE_WIDTH*SHAPE_SCALE*16
draw.line(pixels+[pixels[0]],fill=255,width=round(width),joint='curve')
for x,y in pixels:
    r=width/2
    draw.ellipse((x-r,y-r,x+r,y+r),fill=255)
mask=mask.resize((512,512),Image.Resampling.LANCZOS)""")
source = source.replace('<defs><path id="eye" d="{path}"/></defs>',
    '<defs><g id="eye" transform="translate(64 64) scale(.94) translate(-64 -64)"><path d="{path}" stroke="#564260" stroke-width="2.4" stroke-linejoin="round" stroke-linecap="round"/></g></defs>')
source = source.replace('V14_MIRRORED_VECTOR_EYES_CREATED', 'V15_THICKER_SMALLER_EYES_CREATED')
exec(compile(source,str(TOOLS/'draw_chddd_chibi_eyes_v14.py'),'exec'))

meta_path=ROOT/'chibi_eye_design.json'
meta=json.loads(meta_path.read_text(encoding='utf-8'))
previous=Image.open(ROOT.parent/'V14_ChibiEyeReview/Textures/mika_squeeze_eyes.png').convert('RGBA')
current=Image.open(OUT/'mika_squeeze_eyes.png').convert('RGBA')
meta.update(previousRevision=14,uniformGlyphScale=.94,roundedOutlineWidthIn128UnitTile=2.4,
    geometryChanged=False,anglesChanged=False,faceTimingChanged=False,
    previousAlphaBoundingBox=previous.getchannel('A').getbbox(),
    alphaBoundingBox=current.getchannel('A').getbbox(),
    previousAlphaCoverage=sum(previous.getchannel('A').getdata())/255,
    alphaCoverage=sum(current.getchannel('A').getdata())/255)
meta_path.write_text(json.dumps(meta,indent=2),encoding='utf-8')
print(json.dumps({k:meta[k] for k in ('uniformGlyphScale','previousAlphaBoundingBox','alphaBoundingBox','previousAlphaCoverage','alphaCoverage')}))
