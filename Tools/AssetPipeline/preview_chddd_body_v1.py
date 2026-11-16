import bpy,json,math
from pathlib import Path
from mathutils import Vector
OUT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
PRE=OUT/'Previews/ActionsV1';PRE.mkdir(parents=True,exist_ok=True)
exec(compile((OUT/'CHDDD_BodyControls_v1.py').read_text(encoding='utf-8'),'body_controls','exec'))
scene=bpy.context.scene;cam=scene.camera
snapshot=(cam.matrix_world.copy(),cam.data.ortho_scale,scene.render.resolution_x,scene.render.resolution_y,scene.render.filepath)
cam.location=Vector((0,-3,.60)); target=Vector((0,0,.54))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=1.37
scene.render.resolution_x=420;scene.render.resolution_y=520
names=scene.get('body_v1_preview_batch','Idle,Walk,Thinking,Working,HeadPat,Dragging,Sleep,Wave,Success,Confused,HeadClick,BodyClick').split(',')
motion=bool(scene.get('body_v1_preview_motion',False))
try:
    for name in names:
        entry=preview_body(scene,name,play=False)
        count=round(entry['durationSeconds']*12) if motion else 4
        for i in range(count):
            proportion=i/count if motion else i/(count-1)
            f=1+proportion*(entry['frames'][1]-1)
            scene.frame_set(math.floor(f),subframe=f%1)
            scene.render.filepath=str(PRE/f'{name}_{"motion" if motion else "pose"}_{i:03d}.png')
            bpy.ops.render.render(write_still=True)
finally:
    cam.matrix_world,cam.data.ortho_scale,scene.render.resolution_x,scene.render.resolution_y,scene.render.filepath=snapshot
    preview_body(scene,'Idle',play=False)
print(json.dumps({'previewed':names,'motion':motion,'folder':str(PRE)}))
