from PIL import Image,ImageDraw
from pathlib import Path
p=Path('Material/CHDDD/Previews')
sheet=Image.new('RGB',(1350,1050),'#242833');draw=ImageDraw.Draw(sheet)
for row,e in enumerate(['sad','annoyed','sleepy']):
    for col,s in enumerate(['_left','','_right']):
        im=Image.open(p/('eye_fix_'+e+s+'.png')).convert('RGB');im.thumbnail((450,325))
        sheet.paste(im,(col*450,row*350+25));draw.text((col*450+12,row*350+7),e+s,fill='white')
sheet.save(p/'eye_fix_contact_sheet.png')
strip=Image.new('RGB',(1800,285),'#242833');draw=ImageDraw.Draw(strip)
for j,n in enumerate([0,25,50,75,100]):
    im=Image.open(p/('blink_transition_'+str(n)+'.png')).convert('RGB');im.thumbnail((360,260))
    strip.paste(im,(j*360,25));draw.text((j*360+12,7),str(n)+'%',fill='white')
strip.save(p/'blink_transition_strip.png')
full=Image.new('RGB',(1050,1260),'#242833');draw=ImageDraw.Draw(full)
for j,e in enumerate(['neutral','happy','shy','concern','sad','surprised','annoyed','sleepy','confused']):
    im=Image.open(p/(e+'.png')).convert('RGB');im.thumbnail((350,394));x=(j%3)*350;y=(j//3)*420
    full.paste(im,(x,y+25));draw.text((x+12,y+7),e,fill='white')
full.save(p/'expressions_contact_sheet.png')
