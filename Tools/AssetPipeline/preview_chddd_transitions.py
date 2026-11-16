import bpy,json,pathlib
from mathutils import Vector
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
exec(compile((OUT/'CHDDD_Controls.py').read_text(encoding='utf-8'),'CHDDD_Controls.py','exec'))
scene=bpy.context.scene;cam=scene.camera
saved=(cam.matrix_world.copy(),cam.data.ortho_scale,scene.render.resolution_x,scene.render.resolution_y)
target=Vector((.012,-.15,.726));cam.location=Vector((.012,-1.6,.746));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=.36;scene.render.resolution_x=640;scene.render.resolution_y=460
manifest,arm,body,material=assets(scene)
for preset in manifest['transitions']['presets']:
    for data,name in [(arm,preset['boneAction']),(body.data.shape_keys,preset['shapeAction']),(material.node_tree,preset['mouthAction'])]:
        clear_action(data);data.animation_data_create();data.animation_data.action=bpy.data.actions[name]
    for index,frame in enumerate([1,4.75,8.5,12.25,16]):
        scene.frame_set(int(frame),subframe=frame%1)
        scene.render.filepath=str(OUT/'Previews'/('transition_'+preset['emotion']+'_'+str(index)+'.png'))
        bpy.ops.render.render(write_still=True)
# Verify a new transition takes exactly the evaluated pose, shapes and mouth mixture.
transition_expression(scene,'sleepy',.5,True,False);scene.frame_set(8,subframe=.5);bpy.context.view_layer.update()
snapshot={n:arm.pose.bones[n].matrix_basis.copy() for n in manifest['faceBones']}
shapes={k.name:k.value for k in body.data.shape_keys.key_blocks};weights=mouth_weights(material)
transition_expression(scene,'sad',.5,False,False);bpy.context.view_layer.update()
pose_error=max(abs(arm.pose.bones[n].matrix_basis[i][j]-matrix[i][j]) for n,matrix in snapshot.items() for i in range(4) for j in range(4))
shape_error=max(abs(body.data.shape_keys.key_blocks[n].value-value) for n,value in shapes.items())
mouth_error=max(abs(mouth_weights(material)[cell]-value) for cell,value in weights.items())
assert max(pose_error,shape_error,mouth_error)<1e-5
report=json.loads((OUT/'transition_validation.json').read_text())
report['blenderInterruptedTransition']={'passed':True,'poseError':pose_error,'shapeError':shape_error,'mouthError':mouth_error}
(OUT/'transition_validation.json').write_text(json.dumps(report,indent=2))
cam.matrix_world,cam.data.ortho_scale,scene.render.resolution_x,scene.render.resolution_y=saved
apply_expression(scene,'neutral');scene.frame_start=1;scene.frame_end=61
print(json.dumps(report['blenderInterruptedTransition']))
