"""Compose inspection artifacts from actual Unity Pat10 captures; no character pixels are authored."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parents[2]
out=root/'Docs/Audits/Spine/MemoryLobby/Pat10Trial'
report=json.loads((out/'pat10_review_report.json').read_text(encoding='utf-8-sig'))
eye_scale=report['eyeSize']
hand_scale=eye_scale*report['handSideEyeScale']
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
sheet=Image.new('RGB',(900,430),(25,30,44))
draw=ImageDraw.Draw(sheet)
for index,(filename,label) in enumerate([('reference_size_before.png','上一版：上侧 1.90× / 靠手侧 2.375×'),('reference_size_after.png',f'当前：上侧 {eye_scale:.2f}× / 靠手侧 {hand_scale:.3f}×')]):
    original=Image.open(out/filename).convert('RGB')
    crop=original.crop((950,330,1270,650))
    crop.save(out/('eyes_before.png' if index==0 else 'eyes_after.png'))
    crop=crop.resize((370,370),Image.Resampling.LANCZOS)
    sheet.paste(crop,(20+index*450,48))
    draw.text((20+index*450,12),label,font=font,fill='white')
sheet.save(out/'eye_size_comparison.png')

frames=[Image.open(p).convert('RGB') for p in sorted((out/'LiveFrames').glob('frame_*.png'))]
if len(frames)!=64:raise RuntimeError(f'Expected 64 actual Unity frames; got {len(frames)}')
atlas=Image.new('RGB',(8*86,8*82))
for index,frame in enumerate(frames):
    thumb=frame.resize((86,82),Image.Resampling.BILINEAR)
    atlas.paste(thumb,((index%8)*86,(index//8)*82))
palette=atlas.quantize(colors=256)
animation=[frame.quantize(palette=palette,dither=Image.Dither.NONE) for frame in frames]
animation[0].save(out/'pat10_live_preview.gif',save_all=True,append_images=animation[1:],duration=130,loop=0,disposal=2)
print(json.dumps({'actual_runtime_frames':len(frames),'eye_scale':eye_scale,'hand_side_eye_scale':hand_scale,'artifacts':['eye_size_comparison.png','eyes_after.png','pat10_live_preview.gif']}))
