"""One second review version: key check and motion frames, no artistic iteration."""
import bpy,json,math,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V9_SecondReview')
OUT=ROOT/'Preview';OUT.mkdir(exist_ok=True)
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'review9_controls','exec'))
scene=bpy.context.scene;camera=scene.camera
scene.render.resolution_percentage=100;scene.render.film_transparent=True
for obj in scene.objects:
    if obj.type=='MESH' and obj.name not in ('CH0069_Body','CHDDD_Halo_Restored','CHDDD_TestSqueeze_EyeLines'):obj.hide_render=True
def setup(view):
    scene.render.resolution_x=480;scene.render.resolution_y=580 if view=='Full' else 400
    target=Vector((0,0,.53)) if view=='Full' else Vector((-.035,0,.755))
    camera.location=Vector((target.x,-3.5,target.z+.06))
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=1.42 if view=='Full' else .43
preview_review9_expression(scene);scene.frame_set(37);setup('Face')
scene.render.filepath=str(OUT/'TestSqueeze_check.png');bpy.ops.render.render(write_still=True)
print('REVIEW9_TEST_KEYFRAME',flush=True)
jobs=[('TestSqueeze','Face')] if '--test-only' in sys.argv else [('Success','Full'),('TestSqueeze','Face'),('Success','Face')]
for name,view in jobs:
    entry=preview_review9_expression(scene) if name=='TestSqueeze' else preview_body_v2(scene,name)
    setup(view);count=round(entry['durationSeconds']*24)
    for i in range(count):
        frame=1+i/count*(entry['frames'][1]-1);scene.frame_set(math.floor(frame),subframe=frame%1)
        scene.render.filepath=str(OUT/f'{name}_{view}_{i:03d}.png');bpy.ops.render.render(write_still=True)
    print('REVIEW9_RENDER_DONE',name,view,flush=True)
# Check oblique views for skin coverage and head-bound eye-line placement.
preview_review9_expression(scene);scene.frame_set(37);setup('Face')
for degrees in (-25,25):
    theta=math.radians(degrees);target=Vector((-.035,0,.755))
    camera.location=target+Vector((3.5*math.sin(theta),-3.5*math.cos(theta),.06))
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/f'TestSqueeze_side_{degrees}.png');bpy.ops.render.render(write_still=True)
print('REVIEW9_PREVIEW_COMPLETE',flush=True)
