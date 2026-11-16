"""Oblique and rear samples of the five revised gestures and their follow-through."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
OUT=ROOT/'Previews/ActionsV2/Revision4Sides';OUT.mkdir(parents=True,exist_ok=True)
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'controls','exec'))
scene=bpy.context.scene;camera=scene.camera;target=Vector((0,0,.53))
camera.data.ortho_scale=1.42;scene.render.resolution_x=480;scene.render.resolution_y=580
for obj in scene.objects:
    if obj.type=='MESH' and obj.name not in ('CH0069_Body','CHDDD_Halo_Restored'):obj.hide_render=True
for name in ('Wave','Success','Confused','HeadClick','BodyClick'):
    entry=preview_body_v2(scene,name)
    for view,angle in (('oblique',35),('rear',155)):
        camera.location=Vector((3.5*math.sin(math.radians(angle)),-3.5*math.cos(math.radians(angle)),.59))
        camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
        for i,t in enumerate((.20,.37,.54,.73,.88)):
            frame=1+t*(entry['frames'][1]-1);scene.frame_set(math.floor(frame),subframe=frame%1)
            scene.render.filepath=str(OUT/(name+'_'+view+'_'+str(i)+'.png'))
            bpy.ops.render.render(write_still=True)
print(json.dumps({'revision':4,'frames':50,'views':['oblique35','rear155']}))
