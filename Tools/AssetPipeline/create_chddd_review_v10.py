"""Third review: replace geometric chevrons with cropped source artwork."""
import bpy,json,math,shutil
from pathlib import Path
from mathutils import Matrix,Vector
SOURCE=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
PREVIOUS=SOURCE/'Reviews/V9_SecondReview';ROOT=SOURCE/'Reviews/V10_TextureReview'
ROOT.mkdir(parents=True,exist_ok=True);(ROOT/'Unity').mkdir(exist_ok=True);(ROOT/'Preview').mkdir(exist_ok=True)
for n in ('character_manifest_before_actions_v1.json','character_manifest.json','CHDDD_Controls.py'):
    shutil.copy2(PREVIOUS/n,ROOT/n)
for n in ('expression_map_v1.json','expression_map_v2.json','body_action_map_v2.json','action_face_profiles_v2.json'):
    shutil.copy2(PREVIOUS/'Unity'/n,ROOT/'Unity'/n)
controls=(PREVIOUS/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8')
exec(compile(controls,'prior_review_controls','exec'))
scene=bpy.context.scene;body=bpy.data.objects['CH0069_Body'];arm=bpy.data.objects['Bip001']
preview_body_v2(scene,'Idle');scene.frame_set(1)
body.data.shape_keys.animation_data.action=None
for k in body.data.shape_keys.key_blocks:k.value=0
body.data.shape_keys.key_blocks['Pet_Blink'].value=1
bpy.context.view_layer.update()

# Keep the same clear/reset clock as the prior review. Replace only the test
# eye material and mesh; every pre-existing action and shape key stays exact.
mat=bpy.data.materials['Review9_SqueezeEyeLine'];nt=mat.node_tree
animation=nt.animation_data.action if nt.animation_data else None
nt.nodes.clear();nodes=nt.nodes;links=nt.links
image=bpy.data.images.load(str(ROOT/'Textures/mika_squeeze_eyes.png'),check_existing=False);image.pack()
tex=nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Linear';tex.extension='CLIP'
uv=nodes.new('ShaderNodeTexCoord');links.new(uv.outputs['UV'],tex.inputs['Vector'])
em=nodes.new('ShaderNodeEmission')
transparent=nodes.new('ShaderNodeBsdfTransparent')
opacity=nodes.new('ShaderNodeValue');opacity.name='Review9_EyeLineOpacity';opacity.outputs[0].default_value=0
def shader_math(op,a,b):
    n=nodes.new('ShaderNodeMath');n.operation=op
    for socket,value in zip(n.inputs,(a,b)):
        if hasattr(value,'node'):links.new(value,socket)
        else:socket.default_value=value
    return n.outputs[0]
# A feathered skin underlay suppresses the old closed-lid crease in the
# transparent wedge between the two original painted branches. It has no hard
# rectangular edge and leaves the source RGBA artwork unchanged.
sep=nodes.new('ShaderNodeSeparateXYZ');links.new(uv.outputs['UV'],sep.inputs[0])
u=shader_math('DIVIDE',shader_math('SUBTRACT',shader_math('MODULO',shader_math('MULTIPLY',sep.outputs['X'],2),1),.5),.48)
v=shader_math('DIVIDE',shader_math('SUBTRACT',sep.outputs['Y'],.5),.22)
distance=shader_math('ADD',shader_math('MULTIPLY',u,u),shader_math('MULTIPLY',v,v))
skin_alpha=shader_math('MINIMUM',1,shader_math('MAXIMUM',0,shader_math('DIVIDE',shader_math('SUBTRACT',1,distance),.25)))
face_source=next(n.image for n in bpy.data.materials['CHDDD_Face'].node_tree.nodes if n.type=='TEX_IMAGE')
skin=nodes.new('ShaderNodeTexImage');skin.image=face_source
skin_uv=nodes.new('ShaderNodeCombineXYZ');skin_uv.inputs['X'].default_value=.4;skin_uv.inputs['Y'].default_value=.5;links.new(skin_uv.outputs[0],skin.inputs['Vector'])
color=nodes.new('ShaderNodeMixRGB');links.new(tex.outputs['Alpha'],color.inputs[0]);links.new(skin.outputs['Color'],color.inputs[1]);links.new(tex.outputs['Color'],color.inputs[2]);links.new(color.outputs[0],em.inputs['Color'])
coverage=shader_math('MAXIMUM',skin_alpha,tex.outputs['Alpha'])
alpha=nodes.new('ShaderNodeMath');alpha.operation='MULTIPLY';links.new(coverage,alpha.inputs[0]);links.new(opacity.outputs[0],alpha.inputs[1])
mix=nodes.new('ShaderNodeMixShader');links.new(alpha.outputs[0],mix.inputs[0]);links.new(transparent.outputs[0],mix.inputs[1]);links.new(em.outputs[0],mix.inputs[2])
out=nodes.new('ShaderNodeOutputMaterial');links.new(mix.outputs[0],out.inputs['Surface'])
if animation:nt.animation_data.action=animation

# Closed-eye white inserts must disappear together with the old eyelash strips.
# Hide only EyeMouth faces in the eye region, preserving nose/teeth/mouth parts.
old_eye_tree=bpy.data.materials['CHDDD_EyeMouth'].node_tree
eye_attribute=body.data.attributes.new(name='Review10_EyeOnlyMat3Mask',type='FLOAT',domain='FACE')
base=body.data.shape_keys.key_blocks['Basis']
for poly in body.data.polygons:
    coords=[base.data[i].co for i in poly.vertices]
    eye_attribute.data[poly.index].value=1 if poly.material_index==3 and all(-.435<co.y<-.345 and .014<abs(co.z)<.125 for co in coords) else 0
attr=old_eye_tree.nodes.new('ShaderNodeAttribute');attr.attribute_name=eye_attribute.name
amount=old_eye_tree.nodes['Review9_OldEyeLineOpacity']
factor=old_eye_tree.nodes.new('ShaderNodeMath');factor.operation='MULTIPLY'
old_eye_tree.links.new(attr.outputs['Fac'],factor.inputs[0]);old_eye_tree.links.new(amount.outputs[0],factor.inputs[1])
old_mix=next(n for n in old_eye_tree.nodes if n.type=='MIX_SHADER')
old_eye_tree.links.new(factor.outputs[0],old_mix.inputs[0])

# Project the transparent decal grid onto the actual closed skin. Inverting
# the Head deformation gives stable bind-space vertices for later head turns.
eval_body=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
head=arm.pose.bones['Bip001 Head']
deform=arm.matrix_world@head.matrix@head.bone.matrix_local.inverted()@arm.matrix_world.inverted()
inv=body.matrix_world.inverted()@deform.inverted()@body.matrix_world
def surface(y,z):
    origin=Vector((.25,y,z));direction=Vector((-1,0,0))
    for step in range(12):
        hit,point,normal,index=eval_body.ray_cast(origin,direction)
        if not hit:break
        if eval_body.data.polygons[index].material_index==1:return Vector((point.x+.008,y,z))
        origin=point+Vector((-.0001,0,0))
    # The transparent margin may miss the face near hair; keep a smooth fallback.
    return Vector((.069-.35*abs(z)+.0015,y,z))
verts=[];faces=[];uvs=[];grid=48
for eye_index,sign in enumerate((-1,1)):
    offset=len(verts);center_z=sign*.067;center_y=-.377;width=.106
    for row in range(grid+1):
        v=row/grid;y=center_y+(.5-v)*width
        for col in range(grid+1):
            u=col/grid;z=center_z+(u-.5)*width
            point=surface(y,z);verts.append(tuple(inv@point));uvs.append(((eye_index+u)/2,v))
    for row in range(grid):
        for col in range(grid):
            a=offset+row*(grid+1)+col;faces.append((a,a+1,a+grid+2,a+grid+1))
mesh=bpy.data.meshes.new('Review10_SourceEyesCurvedDecal');mesh.from_pydata(verts,[],faces);mesh.update();uvlayer=mesh.uv_layers.new(name='UVMap')
for poly in mesh.polygons:
    for li in poly.loop_indices:uvlayer.data[li].uv=uvs[mesh.loops[li].vertex_index]
obj=bpy.data.objects['CHDDD_TestSqueeze_EyeLines'];obj.data=mesh;obj.matrix_world=body.matrix_world.copy();obj.data.materials.append(mat)
for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
group=obj.vertex_groups.new(name='Bip001 Head');group.add(list(range(len(verts))),1,'REPLACE')
obj['eyeSource']='Mika_MemoryLobby/spine_regions/10.png; cropped original artwork'
obj['projection']='Closed face skin; 48x48 grid per eye; measured crease clearance; head bone deformation'

controls=controls.replace('第二版审查 · 起举闭眼与 >o<','第三版审查 · 原素材贴图挤眼').replace('>o< 第二版测试','>o< 第三版贴图测试').replace('Test >o< second draft','Test >o< source texture draft')
# Preserve function/operator IDs for compatibility while the UI labels identify
# this version. Their existing reset path already disables the experimental art.
(ROOT/'CHDDD_BodyControls_v2.py').write_text(controls,encoding='utf-8')
for n in ('CHDDD_BodyControls_v2.py','CHDDD_Controls.py'):
    text=bpy.data.texts.get(n) or bpy.data.texts.new(n);text.clear();text.write((ROOT/n).read_text(encoding='utf-8'))
mapping=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
mapping.update(revision=10,reviewIteration=3,reviewStatus='Third review: source artwork texture, awaiting user review')
(ROOT/'Unity/body_action_map_v2.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
scene['body_v2_revision']=10;scene['review_status']='Third review; source texture >o<; waiting for user review'
exec(compile(controls,'review10_controls','exec'))
# The preview reads files relative to the saved blend, so switch the file path
# before invoking it. Saving again below stores the intended held test frame.
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
preview_review9_expression(scene);scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
print('REVIEW10_TEXTURE_CREATED',flush=True)
