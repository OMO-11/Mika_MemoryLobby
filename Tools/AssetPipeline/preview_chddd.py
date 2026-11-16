import bpy,json,pathlib,sys
from mathutils import Matrix
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
manifest=json.loads((OUT/'character_manifest.json').read_text())
scene=bpy.context.scene
arm=next(o for o in scene.objects if o.type=='ARMATURE')
emotion=scene.get('preview_emotion','neutral')
bpy.ops.chddd.expression(emotion=emotion)
setting=manifest['expressions'][emotion]
for track in arm.animation_data.nla_tracks:track.mute=True
if arm.get('neutral_body_pose'):
    for name,rows in json.loads(arm['neutral_body_pose']).items():
        arm.pose.bones[name].matrix_basis=Matrix(rows)
arm.animation_data.action=bpy.data.actions[setting['clip']]
scene.frame_set(1)
tile=setting['mouth_tile']
body=next(o for o in scene.objects if o.type=='MESH' and o.name.startswith('CH0069_Body'))
for name,value in setting.get('blendShapes',{}).items():
    body.data.shape_keys.key_blocks[name].value=value
scene.render.filepath=str(OUT/'Previews'/(emotion+'.png'))
bpy.ops.render.render(write_still=True)
print(json.dumps({'emotion':emotion,'file':scene.render.filepath,'tile':tile}))
