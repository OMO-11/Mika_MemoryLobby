import bpy,json,pathlib
from mathutils import Vector
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
scene=bpy.context.scene;cam=scene.camera
saved=(cam.matrix_world.copy(),cam.data.ortho_scale,scene.render.resolution_x,scene.render.resolution_y)
target=Vector((.012,-.15,.726));cam.location=Vector((.012,-1.6,.746));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=.36;scene.render.resolution_x=640;scene.render.resolution_y=460
manifest=json.loads((OUT/'character_manifest.json').read_text(encoding='utf-8'))
preset=next(p for p in manifest['transitions']['presets'] if p['emotion']=='sleepy')
arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body'];mouth=bpy.data.materials['CHDDD_MouthAtlas']
for data,name in [(arm,preset['boneAction']),(body.data.shape_keys,preset['shapeAction']),(mouth.node_tree,preset['mouthAction'])]:
    data.animation_data_create();data.animation_data.action=bpy.data.actions[name]
for frame in range(1,17):
    scene.frame_set(frame);scene.render.filepath=str(OUT/'Previews'/('sleepy_motion_'+str(frame).zfill(2)+'.png'))
    bpy.ops.render.render(write_still=True)
cam.matrix_world,cam.data.ortho_scale,scene.render.resolution_x,scene.render.resolution_y=saved
bpy.ops.chddd.expression(emotion='neutral');scene.frame_end=61
print('Sleepy motion frames rendered')
