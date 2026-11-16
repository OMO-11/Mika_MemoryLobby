import bpy,json,pathlib,math
from mathutils import Vector
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
scene=bpy.context.scene;cam=scene.camera;body=bpy.data.objects['CH0069_Body']
saved=(cam.matrix_world.copy(),cam.data.ortho_scale,scene.render.resolution_x,scene.render.resolution_y)
manifest=json.loads((OUT/'character_manifest.json').read_text(encoding='utf-8'))
keys=body.data.shape_keys.key_blocks
iris=[v.index for v in body.data.vertices if any(body.vertex_groups[g.group].name in ['Bip001 Xtra_eyeL','Bip001 Xtra_eyeR'] for g in v.groups)]
iris_unchanged=all((keys[k].data[i].co-keys['Basis'].data[i].co).length<1e-8 for k in list(keys.keys())[1:] for i in iris)
assert iris_unchanged
files=[]
for emotion in manifest['expressions']:
    bpy.ops.chddd.expression(emotion=emotion)
    scene.render.filepath=str(OUT/'Previews'/(emotion+'.png'))
    bpy.ops.render.render(write_still=True)
scene.render.resolution_x=900;scene.render.resolution_y=650
target=Vector((.012,-.15,.726));cam.data.ortho_scale=.36
for emotion in ['sad','annoyed','sleepy']:
    bpy.ops.chddd.expression(emotion=emotion)
    for degrees in [-20,0,20]:
        angle=math.radians(degrees)
        cam.location=target+Vector((math.sin(angle)*1.45,-math.cos(angle)*1.45,.02))
        cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
        filename='eye_fix_'+emotion+('_left' if degrees<0 else '_right' if degrees>0 else '')+'.png'
        scene.render.filepath=str(OUT/'Previews'/filename)
        bpy.ops.render.render(write_still=True);files.append(filename)
bpy.ops.chddd.expression(emotion='neutral')
cam.location=target+Vector((0,-1.45,.02));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
for value in [0,.25,.5,.75,1]:
    keys['Pet_Blink'].value=value
    scene.render.filepath=str(OUT/'Previews'/('blink_transition_'+str(int(value*100))+'.png'))
    bpy.ops.render.render(write_still=True)
cam.matrix_world,cam.data.ortho_scale,scene.render.resolution_x,scene.render.resolution_y=saved
bpy.ops.chddd.expression(emotion='neutral')
report={'irisVertices':len(iris),'irisUnchangedForAllShapes':iris_unchanged,
        'blendShapes':list(keys.keys())[1:],
        'sideViewCloseups':files,'blinkSamples':[0,.25,.5,.75,1],
        'eyeFix':'Existing upper/lower skin rims and lashes close over unchanged eyes',
        'visualReview':'Pending inspection of rendered closeups and transition strip'}
(OUT/'eye_fix_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CHDDD.blend'))
print(json.dumps(report))
