"""Oblique/rear inspection of held full-body poses and accessory settling."""
import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
revision=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))['revision']
OUT=ROOT/f'Previews/ActionsV2/Revision{revision}Sides';OUT.mkdir(parents=True,exist_ok=True)
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'controls','exec'))
scene=bpy.context.scene;camera=scene.camera;target=Vector((0,0,.53))
camera.data.ortho_scale=1.42;scene.render.resolution_x=480;scene.render.resolution_y=580
for obj in scene.objects:
    if obj.type=='MESH' and obj.name not in ('CH0069_Body','CHDDD_Halo_Restored'):obj.hide_render=True
selected=sys.argv[sys.argv.index('--names')+1].split(',') if '--names' in sys.argv else ['Wave','Success','Confused','HeadClick','BodyClick']
for name in selected:
    entry=preview_body_v2(scene,name)
    for view,angle in (('oblique',35),('rear',155)):
        camera.location=Vector((3.5*math.sin(math.radians(angle)),-3.5*math.cos(math.radians(angle)),.59))
        camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
        for i,t in enumerate((.20,.40,.50,.70,.88)):
            frame=1+t*(entry['frames'][1]-1);scene.frame_set(math.floor(frame),subframe=frame%1)
            scene.render.filepath=str(OUT/(name+'_'+view+'_'+str(i)+'.png'))
            bpy.ops.render.render(write_still=True)
print(json.dumps({'revision':revision,'frames':len(selected)*10,'views':['oblique35','rear155']}))
