"""Read-only V17 export for native Unity expression review, including shader masks."""
import bpy,json,hashlib,shutil,math
from pathlib import Path
MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
ROOT=MAIN/'Reviews/UnityExpressionV17'
PROJECT=Path('W:/Machine/VSCode/Mika_Gift/Temp/UnityExpressionReviewV17')
ASSETS=PROJECT/'Assets/Review'
for path in (ROOT/'Preview',ASSETS/'Model',ASSETS/'Textures',ASSETS/'Shaders',ASSETS/'BaseMaterials',ASSETS/'Editor',PROJECT/'Packages',PROJECT/'ProjectSettings'):
    path.mkdir(parents=True,exist_ok=True)
for p in Path('W:/Machine/VSCode/Mika_Gift/GiftCompanion/ProjectSettings').glob('*'):
    if p.is_file():shutil.copy2(p,PROJECT/'ProjectSettings'/p.name)
(PROJECT/'Packages/manifest.json').write_text(json.dumps({'dependencies':{
    'com.unity.modules.animation':'1.0.0','com.unity.modules.imageconversion':'1.0.0',
    'com.unity.modules.jsonserialize':'1.0.0','com.unity.modules.imgui':'1.0.0'}},indent=2),encoding='utf-8')
for p in (MAIN/'Textures').glob('*'):
    if p.is_file() and p.suffix in ('.png','.meta'):shutil.copy2(p,ASSETS/'Textures'/p.name)
for name in ('CHDDDToon.shader','CHDDDToon.shader.meta','CHDDDMouth.shader','CHDDDMouth.shader.meta'):
    shutil.copy2(MAIN/'Unity'/name,ASSETS/'Shaders'/name)
for p in (MAIN/'Unity/Materials').glob('*'):
    if p.is_file():shutil.copy2(p,ASSETS/'BaseMaterials'/p.name)

for filename in ('CHDDD_Controls.py','CHDDD_BodyControls_v2.py'):
    exec(compile((MAIN/filename).read_text(encoding='utf-8'),filename,'exec'))
scene=bpy.context.scene;body=bpy.data.objects['CH0069_Body'];arm=bpy.data.objects['Bip001']
assert int(scene['body_v2_revision'])==17
names=[k.name for k in body.data.shape_keys.key_blocks if k.name!='Basis']
def value(material,node):return bpy.data.materials[material].node_tree.nodes[node].outputs[0].default_value
data=dict(blenderRevision=17,sourceBlend=str(MAIN/'CHDDD_actions_v2.blend'),sourceSHA256=hashlib.sha256((MAIN/'CHDDD_actions_v2.blend').read_bytes()).hexdigest(),
    fps=24,durationSeconds=2.4,shapeNames=names,mouthTiles=[300,706,705,604,605,305,401,200],expressions=[])
for label,revision,function in (('Squeeze',15,preview_review9_expression),('Omega',17,preview_review16_omega)):
    function(scene);frames=[]
    for i in range(58):
        frame=1+i/58*72;scene.frame_set(math.floor(frame),subframe=frame%1)
        frames.append(dict(time=(frame-1)/30,shapes=[body.data.shape_keys.key_blocks[n].value for n in names],
            eyeSkinMask=value('CHDDD_Face','Review9_OriginalEyeLineMask'),oldEyeMask=value('CHDDD_EyeMouth','Review9_OldEyeLineOpacity'),
            squeeze=value('Review9_SqueezeEyeLine','Review9_EyeLineOpacity'),omegaEye=value('Review16_OmegaEyeLine','Review16_EyeOpacity'),
            omegaMouth=value('Review16_OmegaMouth','Review16_MouthOpacity'),hideMouth=value('CHDDD_MouthAtlas','Review16_HideOriginalMouth'),
            mouth=[value('CHDDD_MouthAtlas','MouthWeight_'+str(t)) for t in data['mouthTiles']]))
    data['expressions'].append(dict(name=label,revision=revision,frames=frames))
(ASSETS/'expression_samples.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
shutil.copy2(ASSETS/'expression_samples.json',ROOT/'expression_samples.json')

# FBX has no Blender shader attributes; explicitly carry these as linear corner colors.
mesh=body.data
mask=mesh.color_attributes.new(name='UnityReviewMasks',type='FLOAT_COLOR',domain='CORNER')
skin=mesh.attributes['Review11_ClosedEyeSkinMask'];seam=mesh.attributes['Review14_ClosedLidSeamMask'];eyes=mesh.attributes['Review10_EyeOnlyMat3Mask']
for poly in mesh.polygons:
    for li in poly.loop_indices:
        vertex=mesh.loops[li].vertex_index
        eye_index=poly.index if eyes.domain=='FACE' else vertex
        mask.data[li].color=(skin.data[vertex].value,seam.data[vertex].value,eyes.data[eye_index].value,1)
mesh.color_attributes.active_color=mask
mesh.color_attributes.render_color_index=list(mesh.color_attributes).index(mask)

preview_body_v2(scene,'Idle');scene.frame_start=1;scene.frame_end=73;scene.frame_set(1)
body.data.shape_keys.animation_data.action=None
for key in body.data.shape_keys.key_blocks:key.value=0
for name,position in (('UnityReview_FaceCamera',(-.035,-3.5,.815)),('UnityReview_FaceTarget',(-.035,0,.755)),
                      ('UnityReview_FullCamera',(0,-3.5,.59)),('UnityReview_FullTarget',(0,0,.53))):
    obj=bpy.data.objects.new(name,None);scene.collection.objects.link(obj);obj.location=position
include={'CH0069_Body','CHDDD_Halo_Restored','CHDDD_TestSqueeze_EyeLines','CHDDD_TestOmega_Eyes','CHDDD_TestOmega_Mouth'}
for obj in bpy.context.view_layer.objects:
    obj.select_set(False)
    if obj.type in ('ARMATURE','EMPTY') or obj.name in include:
        obj.hide_set(False);obj.hide_viewport=False;obj.select_set(True)
bpy.context.view_layer.objects.active=arm
arguments=dict(filepath=str(ASSETS/'Model/CHDDD_ExpressionReview.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=True,
    bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
    axis_forward='-Z',axis_up='Y',path_mode='RELATIVE')
properties=bpy.ops.export_scene.fbx.get_rna_type().properties
if properties.get('colors_type'):arguments['colors_type']='LINEAR'
if properties.get('prioritize_active_color'):arguments['prioritize_active_color']=True
bpy.ops.export_scene.fbx(**arguments)
(ROOT/'export_manifest.json').write_text(json.dumps(dict(sourceSHA256=data['sourceSHA256'],blenderRevision=17,
    expressionRevisions={'Squeeze':15,'Omega':17},unityProject=str(PROJECT),shaderMasks='Linear corner RGBA: skin, seam, old eye insert, one',
    meshNames=sorted(include),sourceBlendModified=False),ensure_ascii=False,indent=2),encoding='utf-8')
print('UNITY_EXPRESSION_REVIEW_EXPORTED',flush=True)
