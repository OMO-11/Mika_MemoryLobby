"""Rotate the two painted strokes about their shared tip to follow the user's guide."""
from pathlib import Path
from PIL import Image, ImageChops
import math, json, hashlib, argparse

parser=argparse.ArgumentParser()
parser.add_argument('--revision',type=int,default=12)
parser.add_argument('--upper-angle',type=float,default=26)
parser.add_argument('--lower-angle',type=float,default=28)
args=parser.parse_args()

MAIN = Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
ROOT = MAIN/f'Reviews/V{args.revision}_EyeAngleReview'
OUT = ROOT/'Textures'
OUT.mkdir(parents=True, exist_ok=True)
src = MAIN/'Reviews/V11_FlatEyeRecovery/Textures/mika_squeeze_eyes.png'
atlas = Image.open(src).convert('RGBA')
result = Image.new('RGBA', atlas.size)
details = []
for i, (pivot, upper, lower) in enumerate([
    ((111,61), (20,57), (22,88)),
    ((113,65), (20,46), (20,92)),
]):
    eye = atlas.crop((128*i,0,128*(i+1),128))
    if i: eye = eye.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    px,py = pivot
    upper_slope = (upper[1]-py)/(upper[0]-px)
    lower_slope = (lower[1]-py)/(lower[0]-px)
    boundary_slope = (upper_slope+lower_slope)/2
    rotations = [-(args.upper_angle-math.degrees(math.atan(upper_slope))),
                  args.lower_angle+math.degrees(math.atan(lower_slope))]
    masks = [Image.new('L', eye.size), Image.new('L', eye.size)]
    for y in range(128):
        for x in range(128):
            boundary = py+boundary_slope*(x-px)
            # Shared opaque corner is included in both branches to prevent a gap.
            tip = math.hypot(x-px,y-py) < 12
            w = max(0, min(1, (boundary-y+2)/4))
            masks[0].putpixel((x,y), 255 if tip else round(w*255))
            masks[1].putpixel((x,y), 255 if tip else round((1-w)*255))
    rotated = []
    for mask, angle in zip(masks,rotations):
        part = eye.copy()
        part.putalpha(ImageChops.multiply(eye.getchannel('A'),mask))
        part = part.convert('RGBa').resize((512,512),Image.Resampling.LANCZOS)
        part = part.rotate(angle,resample=Image.Resampling.BICUBIC,center=(px*4,py*4))
        rotated.append(part.resize((128,128),Image.Resampling.LANCZOS).convert('RGBA'))
    combined = Image.alpha_composite(rotated[1],rotated[0])
    if i: combined = combined.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    result.alpha_composite(combined,(128*i,0))
    details.append(dict(eye=i,upperStrokeRotationCCW=rotations[0],lowerStrokeRotationCCW=rotations[1],
        targetUpperAngleDegrees=args.upper_angle,targetLowerAngleDegrees=args.lower_angle,pivot=pivot))
result.save(OUT/'mika_squeeze_eyes.png')
(ROOT/'angle_adjustment.json').write_text(json.dumps(dict(
    sourceAtlas=str(src),sourceSHA256=hashlib.sha256(src.read_bytes()).hexdigest(),
    method='Rotate original upper and lower painted branches about a shared tip; keep flat cards',
    eyes=details,originalSourceUnmodified=True),indent=2),encoding='utf-8')
print(f'V{args.revision}_EYE_ANGLES_ADJUSTED',flush=True)
