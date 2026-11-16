import bpy, json, pathlib, math
from mathutils import Vector, Quaternion, Matrix

OUT = pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
scene = bpy.context.scene
arm = next(o for o in scene.objects if o.type == 'ARMATURE')
body = next(o for o in scene.objects if o.type == 'MESH' and o.name.startswith('CH0069_Body'))
mouth = bpy.data.materials['CHDDD_MouthAtlas']
arm.animation_data.action = None
for action in list(bpy.data.actions):
    if action.name.startswith('Pet_Emotion_'):
        bpy.data.actions.remove(action)
idle = next(t for t in arm.animation_data.nla_tracks if 'Cafe_Idle' in t.name)
for obj in scene.objects:
    if obj.animation_data:
        obj.animation_data.action = None
        for t in obj.animation_data.nla_tracks:
            t.mute = True
idle.mute = False
scene.frame_set(0)
base = {b.name: b.matrix_basis.copy() for b in arm.pose.bones}
arm['neutral_body_pose'] = json.dumps({name:[list(row) for row in matrix] for name,matrix in base.items()})
face_names = [b.name for b in arm.pose.bones if any(s in b.name.lower() for s in ['eye', 'mouth'])]
eye_names = [n for n in face_names if 'eyeblow' not in n and 'mouth' not in n]
scene.frame_set(0)
blink = {name: arm.pose.bones[name].matrix_basis.copy() for name in eye_names}
idle.mute = True

# Add controlled eyelid/iris compression instead of reusing the distorted source blink.
if body.data.shape_keys is None:
    body.shape_key_add(name='Basis')
keys = body.data.shape_keys.key_blocks
blink_key = keys.get('Pet_Blink') or body.shape_key_add(name='Pet_Blink')
wide_key = keys.get('Pet_EyeWide') or body.shape_key_add(name='Pet_EyeWide')
blink_key.value = 0; wide_key.value = 0
bpy.context.view_layer.update()
evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
evaluated_mesh = evaluated.to_mesh()
world_vertices = [evaluated.matrix_world @ v.co for v in evaluated_mesh.vertices]
evaluated.to_mesh_clear()
color_layer = body.data.color_attributes.active_color
eye_vertices = set()
for poly in body.data.polygons:
    if body.data.materials[poly.material_index].name == 'CHDDD_EyeMouth':
        for loop_index in poly.loop_indices:
            loop = body.data.loops[loop_index]
            color = color_layer.data[loop_index if color_layer.domain == 'CORNER' else loop.vertex_index].color
            if color[3] < .05:
                eye_vertices.add(loop.vertex_index)
eye_groups = [[i for i in eye_vertices if world_vertices[i].x < 0],
              [i for i in eye_vertices if world_vertices[i].x >= 0]]
for ids in eye_groups:
    center_z = (min(world_vertices[i].z for i in ids)+max(world_vertices[i].z for i in ids))/2
    for i in ids:
        vertex = body.data.vertices[i]
        deform = Matrix(((0,0,0),(0,0,0),(0,0,0)))
        for group in vertex.groups:
            bone = arm.pose.bones.get(body.vertex_groups[group.group].name)
            if bone:
                transform = arm.matrix_world @ bone.matrix @ bone.bone.matrix_local.inverted() @ arm.matrix_world.inverted() @ body.matrix_world
                deform += transform.to_3x3() * group.weight
        inverse = deform.inverted_safe()
        delta = world_vertices[i].z-center_z
        blink_key.data[i].co = keys['Basis'].data[i].co + inverse @ Vector((0,0,-delta*.97))
        wide_key.data[i].co = keys['Basis'].data[i].co + inverse @ Vector((0,0,delta*.22))

def reset():
    for bone in arm.pose.bones:
        bone.matrix_basis = base[bone.name]

def eye_blend(factor):
    for name in eye_names:
        arm.pose.bones[name].matrix_basis = base[name].lerp(blink[name], factor)

def move_world(bone, delta):
    basis = arm.matrix_world.to_3x3() @ bone.bone.matrix_local.to_3x3()
    bone.location += basis.inverted() @ Vector(delta)

settings = {
    'neutral': {'tile':300,'blink':0,'brow':0,'tilt':0},
    'happy': {'tile':706,'blink':.4,'brow':.004,'tilt':-.035},
    'shy': {'tile':705,'blink':.25,'brow':.002,'tilt':.045},
    'concern': {'tile':604,'blink':.12,'brow':.006,'tilt':-.02},
    'sad': {'tile':604,'blink':.35,'brow':.005,'tilt':-.055},
    'surprised': {'tile':605,'blink':0,'brow':.010,'tilt':.02},
    'annoyed': {'tile':305,'blink':.2,'brow':-.006,'tilt':.02},
    'sleepy': {'tile':305,'blink':1,'brow':-.001,'tilt':-.08},
    'confused': {'tile':401,'blink':.08,'brow':.004,'tilt':.065},
}
blink_values={'neutral':0,'happy':.1,'shy':.15,'concern':.1,'sad':.25,'surprised':0,'annoyed':.38,'sleepy':.92,'confused':.08}

for emotion, values in settings.items():
    reset()
    for side in ['L','R']:
        for number in ['1','2']:
            bone = arm.pose.bones['Bip001 Xtra_eyeblow'+side+number]
            amount = values['brow'] * (1 if number=='1' else .45)
            if emotion == 'confused' and side == 'R': amount *= -.7
            move_world(bone,(0,0,amount))
    head = arm.pose.bones['Bip001 Head']
    head.rotation_mode = 'QUATERNION'
    head.rotation_quaternion = head.rotation_quaternion @ Quaternion((0,0,1),values['tilt'])
    action = bpy.data.actions.new('Pet_Emotion_'+emotion.title())
    action.use_fake_user = True
    arm.animation_data.action = action
    keyed_names = face_names + ['Bip001 Head']
    for frame in [1,61]:
        for name in keyed_names:
            bone = arm.pose.bones[name]
            bone.rotation_mode = 'QUATERNION'
            for path in ['location','rotation_quaternion','scale']:
                bone.keyframe_insert(data_path=path,frame=frame,group=name)
    values['clip'] = action.name
    values['frame_range'] = [1,61]
    values['duration_seconds'] = 2
    values['loop'] = True
    values['layer'] = 'Emotion; mask to facial bones and head'
    values['bone_pose'] = {name:{'location':list(arm.pose.bones[name].location),
                                  'rotation':list(arm.pose.bones[name].rotation_quaternion),
                                  'scale':list(arm.pose.bones[name].scale)} for name in keyed_names}
    values['mouth_tile'] = values.pop('tile')
    values['blendShapes'] = {'Pet_Blink':blink_values[emotion], 'Pet_EyeWide':1 if emotion=='surprised' else 0}

manifest = {
    'schemaVersion':1,'character':'CHDDD','sourceCharacter':'CH0069',
    'rig':'Generic','fps':30,'blenderFile':'CHDDD.blend',
    'model':'Unity/CHDDD.fbx','expressions':settings,
    'originalAnimationCount':33,'emotionAnimationCount':len(settings),
    'mouthAtlas':{'file':'Textures/Character_Mouth.png','rows':8,'columns':8,
                  'sourceUvScale':[.5,.5],'tileIdEncoding':'row * 100 + column; rows start at bottom',
                  'shaderProperty':'_MouthTile'},
    'faceBones':keyed_names,
    'blendShapes':['Pet_Blink','Pet_EyeWide'],
    'petActions':{'idle':'CH0069_Cafe_Idle','walk':'CH0069_Cafe_Walk'},
    'pendingBodyActions':['HeadPat','Wave','Thinking','Working','Success','Confused','Dragging','Sleep'],
    'rendering':'Reconstructed toon approximation; original compiled shader source unavailable',
}
(OUT/'character_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'Unity').mkdir(exist_ok=True)
arm.animation_data.action = bpy.data.actions[settings['neutral']['clip']]
reset()
blink_key.value=0;wide_key.value=0
mouth.node_tree.nodes['MouthTileOffset'].inputs[1].default_value=(0,3/8,0)
scene.frame_start = 1;scene.frame_end = 61;scene.frame_set(1)
# Clear localized default world nodes; keep one explicit background/output pair.
nodes=scene.world.node_tree.nodes;nodes.clear()
background=nodes.new('ShaderNodeBackground');background.inputs['Color'].default_value=(.11,.14,.21,1);background.inputs['Strength'].default_value=.7
output=nodes.new('ShaderNodeOutputWorld');scene.world.node_tree.links.new(background.outputs[0],output.inputs[0])
scene.camera.location=Vector((0,-3.0,.90))
target=Vector((0,0,.55))
scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera.data.ortho_scale=1.48
scene.render.resolution_x=800;scene.render.resolution_y=900
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CHDDD.blend'))
print(json.dumps({'emotion_clips':len(settings),'face_bones':len(keyed_names),'original_tracks':len(arm.animation_data.nla_tracks),'manifest':str(OUT/'character_manifest.json')}))
# Apply the corrected skin/lash blink and brow morphs after legacy preset setup.
exec(compile((OUT/'CHDDD_Controls.py').read_text(encoding='utf-8'),'CHDDD_Controls.py','exec'))
fix_path=OUT.parent.parent/'Tools/AssetPipeline/fix_chddd_eyelids.py'
exec(compile(fix_path.read_text(encoding='utf-8'),str(fix_path),'exec'))
pipeline_root=OUT.parent.parent/'Tools/AssetPipeline'
for stage in ['smooth_chddd_closed_curves.py','thicken_chddd_brows.py','setup_chddd_mouth_blending.py','create_chddd_transitions.py']:
    stage_path=pipeline_root/stage
    exec(compile(stage_path.read_text(encoding='utf-8'),str(stage_path),'exec'))
bpy.ops.chddd.expression(emotion='neutral')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CHDDD.blend'))
