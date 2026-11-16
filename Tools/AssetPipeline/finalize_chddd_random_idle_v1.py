"""Add a deterministic continuous sequence and validate the exported clip names."""
import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
OUT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/RandomIdleV1')
data=json.loads((OUT/'idle_action_map_v1.json').read_text(encoding='utf-8'))
arm=bpy.data.objects['Bip001'];scene=bpy.context.scene
for t in arm.animation_data.nla_tracks:t.mute=True
arm.animation_data.action=None
order=['IdleLookAround','IdleArmSettle','IdleLeanLeft','IdleNod','IdleShoulderRelax','IdleLeanRight','IdleBreath']
track=arm.animation_data.nla_tracks.new();track.name='RandomIdle_ContinuousDemo'
cursor=1;timeline=[]
for name in order:
    entry=next(e for e in data['actions'] if e['name']==name)
    strip=track.strips.new(entry['clip'],cursor,bpy.data.actions[entry['clip']]);strip.blend_type='REPLACE'
    timeline.append(dict(name=name,start=cursor,end=cursor+entry['frames'][1]-1))
    cursor+=entry['frames'][1]-1
scene.frame_start=1;scene.frame_end=cursor;scene.frame_set(1)
target=Vector((0,0,.53));camera=scene.camera
camera.location=target+Vector((0,-3.5,.06))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=1.42
scene.render.resolution_x=480;scene.render.resolution_y=580
data['continuousDemo']=timeline
data['compatibility']='Pairwise seam guarantee is for this common-anchor library. Existing Idle enters via 0.28s blend; arbitrary mid-clip interruption requires crossfade.'
(OUT/'idle_action_map_v1.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CHDDD_random_idle_v1.blend'))
# Verify the actual FBX rather than inferring export success from the file size.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(OUT/'CHDDD_random_idle_v1.fbx'))
names=[a.name for a in bpy.data.actions]
found={e['clip']:any(e['clip'] in n for n in names) for e in data['actions']}
assert all(found.values()),names
report=json.loads((OUT/'validation.json').read_text(encoding='utf-8'))
report['fbxRoundTrip']={'passed':True,'actionNames':names,'expectedClipsFound':found,'unityValidation':False}
(OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('FINALIZED_SEQUENCE_AND_FBX',flush=True)
