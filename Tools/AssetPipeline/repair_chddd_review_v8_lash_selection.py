"""Correct a UV-island selection bug in the first expression test."""
import bpy,json,math,numpy as np,shutil
from pathlib import Path
from mathutils import Vector
SOURCE=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
DRAFT=SOURCE/'Reviews/V8_FirstReview';ROOT=DRAFT
PIPE=SOURCE.parent.parent/'Tools/AssetPipeline'
body=bpy.data.objects['CH0069_Body'];arm=bpy.data.objects['Bip001'];scene=bpy.context.scene
MOUTH=[300,706,705,604,605,305,401,200]
def ease5_v7(t):
    t=max(0,min(1,t));return t**3*(10-15*t+6*t*t)
source=(PIPE/'create_chddd_body_v2.py').read_text(encoding='utf-8')
exec(compile(source[source.index('def new_action('):source.index('entries=[];profiles=[]')],'review_action_helpers','exec'))
shutil.copy2(SOURCE/'CHDDD_BodyControls_v2.py',DRAFT/'CHDDD_BodyControls_v2.py')
exec(compile((PIPE/'chddd_review_v8_expression.py').read_text(encoding='utf-8'),'review8_uv_selection_fix','exec'))
exec(compile((DRAFT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'review_controls','exec'))
preview_review8_expression(scene);scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(DRAFT/'CHDDD_actions_v2.blend'))
scene.render.resolution_x=480;scene.render.resolution_y=400;scene.render.resolution_percentage=100;scene.render.film_transparent=True
camera=scene.camera;target=Vector((-.035,0,.755));camera.location=Vector((target.x,-3.5,target.z+.06))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=.43
for obj in scene.objects:
    if obj.type=='MESH' and obj.name not in ('CH0069_Body','CHDDD_Halo_Restored'):obj.hide_render=True
scene.render.filepath=str(DRAFT/'Preview/TestSqueeze_corrected_selection.png');bpy.ops.render.render(write_still=True)
for i in range(58):
    frame=1+i/58*72;scene.frame_set(math.floor(frame),subframe=frame%1)
    scene.render.filepath=str(DRAFT/'Preview'/f'TestSqueeze_Face_{i:03d}.png');bpy.ops.render.render(write_still=True)
print('FIRST_REVIEW_UV_SELECTION_FIXED',flush=True)
