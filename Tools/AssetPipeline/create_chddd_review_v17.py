"""Thicken only the native omega eye and mouth strokes in the backed-up V16 scene."""
import bpy,json,shutil
import numpy as np
from pathlib import Path
from mathutils import Vector
MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
ROOT=MAIN/'Reviews/V17_OmegaThickerReview'
for folder in ('Blender','Preview','Textures'):(ROOT/folder).mkdir(parents=True,exist_ok=True)
for name in ('character_manifest.json','character_manifest_before_actions_v1.json','CHDDD_Controls.py'):
    shutil.copy2(MAIN/name,ROOT/name)
for p in (MAIN/'Blender').glob('*.json'):shutil.copy2(p,ROOT/'Blender'/p.name)
for suffix in ('png','svg'):shutil.copy2(MAIN/f'Textures/mika_squeeze_eyes_review.{suffix}',ROOT/f'Textures/mika_squeeze_eyes.{suffix}')
controls=(MAIN/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8').replace('表情实验 V16','表情实验 V17').replace('-ω- 第一版测试','-ω- 加粗版测试')
(ROOT/'CHDDD_BodyControls_v2.py').write_text(controls,encoding='utf-8')
embedded=bpy.data.texts['CHDDD_BodyControls_v2.py'];embedded.clear();embedded.write(controls)

eyes=bpy.data.objects['CHDDD_TestOmega_Eyes'];vertices=eyes.data.vertices;half=len(vertices)//2
old=[v.co.copy() for v in vertices[:half]]
fit=np.polyfit([p.z for p in old],[p.y for p in old],3)
card=bpy.data.objects['CHDDD_TestSqueeze_EyeLines'].data.vertices
normal=(card[5].co-card[4].co).cross(card[6].co-card[4].co)
eye_factor=1.55
for index,co in enumerate(old):
    center=float(np.polyval(fit,co.z));new_y=center+(co.y-center)*eye_factor
    new_x=co.x-normal.y/normal.x*(new_y-co.y)
    vertices[index].co=(new_x,new_y,co.z)
    vertices[index+half].co=(new_x,new_y,-co.z)
eyes.data.update();eyes['review_revision']=17;eyes['stroke_width_multiplier_v16']=eye_factor

mouth=bpy.data.objects['CHDDD_TestOmega_Mouth'];v=mouth.data.vertices
assert len(v)==294,'Unexpected native omega topology'
mouth_factor=1.65
for index in range(129):
    a,b=v[index*2],v[index*2+1];center=(a.co+b.co)/2
    a.co=center+(a.co-center)*mouth_factor;b.co=center+(b.co-center)*mouth_factor
for center_index in (258,276):
    center=v[center_index].co.copy()
    for index in range(center_index+1,center_index+18):v[index].co=center+(v[index].co-center)*mouth_factor
mouth.data.update();mouth['review_revision']=17;mouth['stroke_width_multiplier_v16']=mouth_factor

mapping_path=ROOT/'Blender/body_action_map_v2.json';mapping=json.loads(mapping_path.read_text(encoding='utf-8'))
mapping.update(revision=17,reviewStatus='Second omega experiment review: thicker eye and mouth strokes')
mapping_path.write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
scene=bpy.context.scene;scene['body_v2_revision']=17;scene['pending_review']='-omega- thicker strokes'
scene['review_status']='V17 thicker omega eye and mouth lines; pending user review'
exec(compile((ROOT/'CHDDD_Controls.py').read_text(encoding='utf-8'),'static_controls','exec'))
exec(compile(controls,'omega_thicker_controls','exec'))
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
preview_review16_omega(scene);scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
design=json.loads((MAIN/'Reviews/V16_OmegaFirstReview/omega_design.json').read_text(encoding='utf-8'))
design.update(revision=17,previousRevision=16,eyeStrokeWidthMultiplier=eye_factor,mouthStrokeWidthMultiplier=mouth_factor,
    mouthCenterlineUnchanged=True,eyeCenterlineUnchanged=True,faceTimingChanged=False,colorsChanged=False)
(ROOT/'omega_design.json').write_text(json.dumps(design,ensure_ascii=False,indent=2),encoding='utf-8')
print('V17_OMEGA_STROKES_THICKENED',flush=True)
