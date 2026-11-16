from pathlib import Path
from PIL import Image,ImageDraw
import json
p=Path('Material/CHDDD/Previews')
manifest=json.loads((p.parent/'character_manifest.json').read_text(encoding='utf-8'))
names=[s['emotion'] for s in manifest['transitions']['presets']]
sheet=Image.new('RGB',(1600,len(names)*255),'#242833');draw=ImageDraw.Draw(sheet)
for row,emotion in enumerate(names):
    for col,label in enumerate(['0 ms','125 ms','250 ms','375 ms','500 ms']):
        im=Image.open(p/('transition_'+emotion+'_'+str(col)+'.png')).convert('RGB');im.thumbnail((320,230))
        x=col*320;y=row*255;sheet.paste(im,(x,y+25));draw.text((x+8,y+6),emotion+' / '+label,fill='white')
sheet.save(p/'neutral_transition_contact_sheet.png')
sleepy=Image.new('RGB',(1600,255),'#242833');sleepy.paste(sheet.crop((0,names.index('sleepy')*255,1600,(names.index('sleepy')+1)*255)))
sleepy.save(p/'neutral_to_sleepy.png')
frames=[Image.open(p/('sleepy_motion_'+str(i).zfill(2)+'.png')).convert('RGB') for i in range(1,17)]
sequence=frames+list(reversed(frames))
duration=[33]*len(sequence);duration[0]=700;duration[15]=1000;duration[-1]=500
sequence[0].save(p/'neutral_to_sleepy.gif',save_all=True,append_images=sequence[1:],duration=duration,loop=0,optimize=False)
print('Transition sheets and motion GIF saved')
