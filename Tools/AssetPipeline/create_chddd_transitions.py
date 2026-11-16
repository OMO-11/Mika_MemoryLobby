import bpy,json,pathlib
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
controls=(OUT/'CHDDD_Controls.py').read_text(encoding='utf-8')
text=bpy.data.texts.get('CHDDD_Controls.py') or bpy.data.texts.new('CHDDD_Controls.py');text.clear();text.write(controls)
exec(compile(controls,'CHDDD_Controls.py','exec'))
scene=bpy.context.scene;manifest,arm,body,material=assets(scene)
presets=[];checks=[]
for emotion in manifest['expressions']:
    if emotion=='neutral':continue
    apply_expression(scene,emotion)
    expected={name:arm.pose.bones[name].matrix_basis.copy() for name in manifest['faceBones']}
    preset=transition_expression(scene,emotion,.5,True,True);presets.append(preset)
    scene.frame_set(preset['frames'][1]);bpy.context.view_layer.update()
    error=max(abs(arm.pose.bones[name].matrix_basis[i][j]-matrix[i][j]) for name,matrix in expected.items() for i in range(4) for j in range(4))
    setting=manifest['expressions'][emotion]
    shape_error=max(abs(body.data.shape_keys.key_blocks[n].value-v) for n,v in setting['blendShapes'].items())
    weights=mouth_weights(material)
    mouth_error=max(abs(v-(1 if cell==setting['mouth_tile'] else 0)) for cell,v in weights.items())
    assert max(error,shape_error,mouth_error)<1e-5,(emotion,error,shape_error,mouth_error)
    checks.append({'emotion':emotion,'poseError':error,'shapeError':shape_error,'mouthError':mouth_error})
manifest['transitions']['presets']=presets
(OUT/'character_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'transition_validation.json').write_text(json.dumps({'blenderEndpointsPassed':True,'presets':checks},indent=2),encoding='utf-8')
apply_expression(scene,'neutral');scene.frame_start=1;scene.frame_end=61
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CHDDD.blend'))
print(json.dumps({'presets':len(presets),'endpointValidation':checks}))
