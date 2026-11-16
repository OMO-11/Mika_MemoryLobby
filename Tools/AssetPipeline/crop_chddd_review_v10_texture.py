"""Deterministically crop the user-supplied eye artwork; no generated painting."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib
SOURCE=Path('W:/Machine/VSCode/Mika_Gift/Material/Mika_MemoryLobby/spine_regions/10.png')
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V10_TextureReview')
OUT=ROOT/'Textures';OUT.mkdir(parents=True,exist_ok=True)
im=Image.open(SOURCE).convert('RGBA')
boxes={'image_left':(0,31,49,64),'image_right':(80,59,119,96)}
atlas=Image.new('RGBA',(256,128),(0,0,0,0));details={}
for index,(name,box) in enumerate(boxes.items()):
    crop=im.crop(box);crop.save(OUT/f'{name}_raw.png')
    # Both original eyes share a roughly 21 degree face tilt. Rotate the actual
    # pixels; retain the original asymmetric eyelashes and color accents.
    leveled=crop.rotate(21,resample=Image.Resampling.BICUBIC,expand=True)
    bounds=leveled.getchannel('A').getbbox();leveled=leveled.crop(bounds)
    leveled.save(OUT/f'{name}_leveled.png')
    scale=112/leveled.width
    resized=leveled.convert('RGBa').resize((112,round(leveled.height*scale)),Image.Resampling.LANCZOS).convert('RGBA')
    atlas.alpha_composite(resized,(index*128+8,round((128-resized.height)/2)))
    details[name]={'originalCrop':box,'rotationCCW':21,'leveledSize':leveled.size,'atlasCell':[index*128,0,(index+1)*128,128]}
atlas.save(OUT/'mika_squeeze_eyes.png')
proof=Image.new('RGBA',(768,288),(57,61,72,255));font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18);draw=ImageDraw.Draw(proof)
draw.text((12,10),'用户原图',font=font,fill='white');draw.text((280,10),'裁剪双眼 · 校正倾斜 · 保留原画',font=font,fill='white')
original=im.resize((256,232),Image.Resampling.NEAREST);proof.alpha_composite(original,(0,45))
proof.alpha_composite(atlas.resize((512,256),Image.Resampling.NEAREST),(256,32));proof.convert('RGB').save(ROOT/'texture_crop_review.jpg',quality=95)
meta={'source':str(SOURCE),'sourceSHA256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'sourceSize':im.size,'eyes':details,'output':'Textures/mika_squeeze_eyes.png','originalPixelsUsed':True,'mouthArtworkUsed':False,'note':'Only crop, rotate and resample; no recoloring, drawing or AI-generated replacement'}
(ROOT/'texture_crop_manifest.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(meta,ensure_ascii=False))
