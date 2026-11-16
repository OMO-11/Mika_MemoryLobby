"""Second user-review iteration: early cheer blink and independent >o< eye strokes."""
import bpy,json,math,shutil,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
SOURCE=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
PREVIOUS=SOURCE/'Reviews/V8_FirstReview'
ROOT=DRAFT=SOURCE/'Reviews/V9_SecondReview'
PIPE=SOURCE.parent.parent/'Tools/AssetPipeline'
ROOT.mkdir(parents=True,exist_ok=True);(ROOT/'Unity').mkdir(exist_ok=True);(ROOT/'Preview').mkdir(exist_ok=True)
for n in ('character_manifest_before_actions_v1.json','character_manifest.json','CHDDD_BodyControls_v2.py','CHDDD_Controls.py'):
    shutil.copy2(PREVIOUS/n,ROOT/n)
for n in ('expression_map_v1.json','expression_map_v2.json','body_action_map_v2.json','action_face_profiles_v2.json'):
    shutil.copy2(PREVIOUS/'Unity'/n,ROOT/'Unity'/n)
scene=bpy.context.scene;body=bpy.data.objects['CH0069_Body'];arm=bpy.data.objects['Bip001']
helpers=(PIPE/'create_chddd_body_v2.py').read_text(encoding='utf-8')
exec(compile(helpers[helpers.index('def new_action('):helpers.index('entries=[];profiles=[]')],'review9_action_helpers','exec'))
def ease(x):
    x=max(0,min(1,x));return x**3*(10-15*x+6*x*x)

# Change facial timing only. Every body curve, including approved HeadClick,
# remains the exact prior reviewed action.
data=json.loads((ROOT/'Unity/action_face_profiles_v2.json').read_text(encoding='utf-8'))
p=next(p for p in data['profiles'] if p['name']=='Success')
for i,v in enumerate(p['samples']):
    t=i/(len(p['samples'])-1)
    if .145<=t<.36:
        v['blink']=max(v['blink'],ease((t-.145)/.045))
        v['happy']=max(v.get('happy',0),.65*ease((t-.15)/.08))
    if .36<=t<=.56:v['blink']=1
a,bag=new_action('PetV2_Shapes_Success',body.data.shape_keys)
fields={'Pet_Blink':'blink','Pet_EyeWide':None,'Pet_BrowSad':'sad','Pet_BrowAnnoyed':'annoyed','Pet_BrowSleepy':'sleepy','Pet_BrowHappy_Review':'happy','Pet_TestSqueeze_Review':None}
for key,field in fields.items():
    curve(bag,body.data.shape_keys.key_blocks[key].path_from_id('value'),0,[v.get(field,0) if field else 0 for v in p['samples']],key)
(ROOT/'Unity/action_face_profiles_v2.json').write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
mapping=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
mapping.update(revision=9,reviewStatus='Second iteration; awaiting user review',reviewIteration=2)
(ROOT/'Unity/body_action_map_v2.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')

# Start from the accepted Blink skin. The failed previous eyelash deformation
# is retained as an inactive historical key; its value is always zero here.
controls=(SOURCE/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8')
controls=controls.replace("keys=body.data.shape_keys;keys.animation_data_create();", "keys=body.data.shape_keys\n    for key in keys.key_blocks:key.value=0\n    keys.animation_data_create();")
exec(compile(controls,'base_review_controls','exec'))
preview_body_v2(scene,'Idle');scene.frame_set(1)
for k in body.data.shape_keys.key_blocks:k.value=0
body.data.shape_keys.animation_data.action=None
body.data.shape_keys.key_blocks['Pet_Blink'].value=1
bpy.context.view_layer.update()

# Match the original toon shading: replace only the eye-line atlas islands
# with their original skin swatch, before the existing shadow multiplication.
mat=bpy.data.materials['CHDDD_Face'];tree=mat.node_tree;nodes=tree.nodes;links=tree.links
tex=next(n for n in nodes if n.type=='TEX_IMAGE')
targets=[l.to_socket for l in list(links) if l.from_node==tex and l.from_socket==tex.outputs['Color']]
uv=nodes.new('ShaderNodeTexCoord');sep=nodes.new('ShaderNodeSeparateXYZ');links.new(uv.outputs['UV'],sep.inputs[0])
def math_node(op,a,b=None):
    n=nodes.new('ShaderNodeMath');n.operation=op
    if hasattr(a,'node'):links.new(a,n.inputs[0])
    else:n.inputs[0].default_value=a
    if b is not None:
        if hasattr(b,'node'):links.new(b,n.inputs[1])
        else:n.inputs[1].default_value=b
    return n.outputs[0]
top=math_node('GREATER_THAN',sep.outputs['Y'],.70)
bottom=math_node('MULTIPLY',math_node('GREATER_THAN',sep.outputs['Y'],.10),math_node('LESS_THAN',sep.outputs['Y'],.20))
region=math_node('MAXIMUM',top,bottom)
amount=nodes.new('ShaderNodeValue');amount.name='Review9_OriginalEyeLineMask';amount.outputs[0].default_value=0
# Match the actual eyelid polygons, including the pink lower-line UV islands.
# Skin shading remains handled by the existing toon shader after this mix.
eye_mask=body.data.attributes.new(name='Review9_EyeSkinMask',type='FLOAT',domain='FACE')
eye_vertices={v.index for v in body.data.vertices if any(body.vertex_groups[g.group].name.startswith(('Bip001 eye_','Bip001 bone_eye_D_')) and g.weight>.18 for g in v.groups)}
for poly in body.data.polygons:eye_mask.data[poly.index].value=1 if poly.material_index==1 and any(i in eye_vertices for i in poly.vertices) else 0
attribute=nodes.new('ShaderNodeAttribute');attribute.attribute_name=eye_mask.name
brightness=nodes.new('ShaderNodeRGBToBW');links.new(tex.outputs['Color'],brightness.inputs[0])
darkness=math_node('MINIMUM',1,math_node('MAXIMUM',0,math_node('DIVIDE',math_node('SUBTRACT',.55,brightness.outputs[0]),.18)))
factor=math_node('MULTIPLY',math_node('MULTIPLY',attribute.outputs['Fac'],darkness),amount.outputs[0])
skin=nodes.new('ShaderNodeTexImage');skin.image=tex.image;skin.name='Review9_OriginalSkinSwatch';skin.inputs['Vector'].default_value=(.967,.039,0)
swatch_uv=nodes.new('ShaderNodeCombineXYZ');swatch_uv.inputs['X'].default_value=.967;swatch_uv.inputs['Y'].default_value=.039
links.new(swatch_uv.outputs[0],skin.inputs['Vector'])
mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.name='Review9_ReplaceEyeLinesOnly'
links.new(factor,mix.inputs[0]);links.new(tex.outputs['Color'],mix.inputs[1]);links.new(skin.outputs['Color'],mix.inputs[2])
for target in targets:links.new(mix.outputs[0],target)

# The remaining dark eyelash strips live in a tiny solid-color EyeMouth UV
# island. Hide just that island during the test; iris UVs stay unaffected.
oldlash=bpy.data.materials['CHDDD_EyeMouth'].node_tree;on=oldlash.nodes;ol=oldlash.links
old_output=next(n for n in on if n.type=='OUTPUT_MATERIAL');old_surface=old_output.inputs['Surface'].links[0].from_socket
ouv=on.new('ShaderNodeTexCoord');osep=on.new('ShaderNodeSeparateXYZ');ol.new(ouv.outputs['UV'],osep.inputs[0])
omask=on.new('ShaderNodeValue');omask.name='Review9_OldEyeLineOpacity';omask.outputs[0].default_value=0
def oldmath(op,a,b):
    n=on.new('ShaderNodeMath');n.operation=op
    for socket,value in zip(n.inputs,(a,b)):
        if hasattr(value,'node'):ol.new(value,socket)
        else:socket.default_value=value
    return n.outputs[0]
rect=oldmath('MULTIPLY',oldmath('MULTIPLY',oldmath('GREATER_THAN',osep.outputs['X'],.025),oldmath('LESS_THAN',osep.outputs['X'],.04)),oldmath('MULTIPLY',oldmath('GREATER_THAN',osep.outputs['Y'],.84),oldmath('LESS_THAN',osep.outputs['Y'],.85)))
ofactor=oldmath('MULTIPLY',rect,omask.outputs[0]);otrans=on.new('ShaderNodeBsdfTransparent');omix=on.new('ShaderNodeMixShader')
ol.new(ofactor,omix.inputs[0]);ol.new(old_surface,omix.inputs[1]);ol.new(otrans.outputs[0],omix.inputs[2]);ol.new(omix.outputs[0],old_output.inputs['Surface'])

# Independent, continuous eye strokes sit on the closed face surface and use
# the same head bone deformation. Both ends taper; the central corner is joined.
lashmat=bpy.data.materials.new('Review9_SqueezeEyeLine');lashmat.use_nodes=True
ln=lashmat.node_tree.nodes;ll=lashmat.node_tree.links;ln.clear()
em=ln.new('ShaderNodeEmission');em.inputs['Color'].default_value=(.087,.057,.115,1)
transparent=ln.new('ShaderNodeBsdfTransparent');opacity=ln.new('ShaderNodeValue');opacity.name='Review9_EyeLineOpacity';opacity.outputs[0].default_value=0
sh=ln.new('ShaderNodeMixShader');ll.new(opacity.outputs[0],sh.inputs[0]);ll.new(transparent.outputs[0],sh.inputs[1]);ll.new(em.outputs[0],sh.inputs[2]);out=ln.new('ShaderNodeOutputMaterial');ll.new(sh.outputs[0],out.inputs[0])
evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get());closed=evaluated.to_mesh()
base=body.data.shape_keys.key_blocks['Basis'];skin_vertices=set()
for poly in body.data.polygons:
    if poly.material_index!=1:continue
    for li in poly.loop_indices:
        uvv=body.data.uv_layers.active.data[li].uv
        if uvv.y<.07:skin_vertices.add(body.data.loops[li].vertex_index)
# Evaluated mesh coordinates are the actual posed closed skin; convert back to
# original bind coordinates through the head's current deformation.
head=arm.pose.bones['Bip001 Head'];deform=arm.matrix_world@head.matrix@head.bone.matrix_local.inverted()@arm.matrix_world.inverted()
skin_points=[closed.vertices[i].co.copy() for i in skin_vertices]
def depth(s,y):
    points=[co.x for co in skin_points if abs(abs(co.z)-s)<.012 and abs(co.y-y)<.04]
    return (max(points) if points else .065)+.0025
verts=[];faces=[]
for sign in (1,-1):
    path=[]
    for j in range(33):
        u=j/32
        # outer-upper -> inward-pointing corner -> outer-lower
        if u<.5:
            f=u*2;s=.095+(.039-.095)*f;y=-.397+(.377-.397)*f
        else:
            f=(u-.5)*2;s=.039+(.095-.039)*f;y=-.377+(.358-.377)*f
        # y above is a signed head-local vertical coordinate.
        if u<.5:y=-.397+.020*(u*2)
        else:y=-.377+.019*((u-.5)*2)
        path.append(Vector((depth(s,y),y,sign*s)))
    start=len(verts)
    for j,point in enumerate(path):
        tangent=path[min(j+1,len(path)-1)]-path[max(j-1,0)]
        normal=Vector((0,-tangent.z,tangent.y)).normalized()
        width=.0016*(.25+.75*min(1,j/3,(32-j)/3))
        for q in (point-normal*width,point+normal*width):
            world=body.matrix_world@q
            bind=deform.inverted()@world
            verts.append(tuple(body.matrix_world.inverted()@bind))
    for j in range(32):faces.append((start+j*2,start+j*2+1,start+j*2+3,start+j*2+2))
evaluated.to_mesh_clear()
mesh=bpy.data.meshes.new('Review9_ChevronEyeLinesMesh');mesh.from_pydata(verts,[],faces);mesh.update()
obj=bpy.data.objects.new('CHDDD_TestSqueeze_EyeLines',mesh);scene.collection.objects.link(obj);obj.matrix_world=body.matrix_world.copy();obj.data.materials.append(lashmat)
vg=obj.vertex_groups.new(name='Bip001 Head');vg.add(list(range(len(verts))),1,'REPLACE')
mod=obj.modifiers.new('HeadDeform','ARMATURE');mod.object=arm
obj['experimental_only']=True

count=73
roundmouth=body.shape_key_add(name='Pet_TestRoundMouth_Review',from_mix=False)
mouth_vertices=set(i for p in body.data.polygons if p.material_index==5 for i in p.vertices)
center=sum((base.data[i].co for i in mouth_vertices),Vector())/len(mouth_vertices)
for i in mouth_vertices:
    co=base.data[i].co.copy();co.z=center.z+(co.z-center.z)*.62;co.y=center.y+(co.y-center.y)*1.10;co.x+=.006;roundmouth.data[i].co=co
def test_values(t):
    blink=ease((t-.08)/.14)*ease((.97-t)/.14)
    squeeze=ease((t-.22)/.12)*ease((.81-t)/.12)
    mouth=ease((t-.17)/.16)*ease((.87-t)/.15)
    return blink,squeeze,mouth
a,bag=new_action('Review9_TestSqueeze_Shapes',body.data.shape_keys)
for key in body.data.shape_keys.key_blocks:
    if key.name=='Basis':continue
    values=[]
    for i in range(count):
        b,s,m=test_values(i/72);values.append(b if key.name=='Pet_Blink' else s*.35 if key.name=='Pet_BrowHappy_Review' else m if key.name=='Pet_TestRoundMouth_Review' else 0)
    curve(bag,key.path_from_id('value'),0,values,key.name)
mouth=bpy.data.materials['CHDDD_MouthAtlas'].node_tree;MOUTH=[300,706,705,604,605,305,401,200]
a,bag=new_action('Review9_TestSqueeze_Mouth',mouth)
for tile in MOUTH:curve(bag,mouth.nodes['MouthWeight_'+str(tile)].outputs[0].path_from_id('default_value'),0,[test_values(i/72)[2] if tile==605 else 1-test_values(i/72)[2] if tile==300 else 0 for i in range(count)],'Mouth')
a,bag=new_action('Review9_TestSqueeze_FaceMaterial',tree)
curve(bag,amount.outputs[0].path_from_id('default_value'),0,[test_values(i/72)[1] for i in range(count)],'EyeLineMask')
a,bag=new_action('Review9_TestSqueeze_OldLashMaterial',oldlash)
curve(bag,omask.outputs[0].path_from_id('default_value'),0,[test_values(i/72)[1] for i in range(count)],'OldEyeLines')
a,bag=new_action('Review9_TestSqueeze_EyeLineMaterial',lashmat.node_tree)
curve(bag,opacity.outputs[0].path_from_id('default_value'),0,[test_values(i/72)[1] for i in range(count)],'EyeLines')

controls=controls.replace("scene['transition_preview_stop_frame']=0", "scene['transition_preview_stop_frame']=0\n    for material_name,node_name in [('CHDDD_Face','Review9_OriginalEyeLineMask'),('CHDDD_EyeMouth','Review9_OldEyeLineOpacity'),('Review9_SqueezeEyeLine','Review9_EyeLineOpacity')]:\n        material=bpy.data.materials.get(material_name)\n        if material and material.node_tree.nodes.get(node_name):\n            nt=material.node_tree\n            if nt.animation_data:nt.animation_data.action=None\n            nt.nodes[node_name].outputs[0].default_value=0")
controls=controls.replace("self.layout.label(text='身体 · 发衣跟随 · 独立表情过程')", "self.layout.label(text='第二版审查 · 起举闭眼与 >o<')\n        self.layout.operator('chddd.review9_expression',text='>o< 第二版测试',icon='PLAY')")
controls+='''
def preview_review9_expression(scene,play=False):
    preview_body_v2(scene,'Idle')
    body=bpy.data.objects['CH0069_Body']
    body.data.shape_keys.animation_data.action=bpy.data.actions['Review9_TestSqueeze_Shapes']
    for material_name,action_name in [('CHDDD_MouthAtlas','Review9_TestSqueeze_Mouth'),('CHDDD_Face','Review9_TestSqueeze_FaceMaterial'),('CHDDD_EyeMouth','Review9_TestSqueeze_OldLashMaterial'),('Review9_SqueezeEyeLine','Review9_TestSqueeze_EyeLineMaterial')]:
        nt=bpy.data.materials[material_name].node_tree;nt.animation_data_create();nt.animation_data.action=bpy.data.actions[action_name]
    scene.frame_start=1;scene.frame_end=73;scene.frame_set(1);scene['body_v2_action']='TestSqueeze'
    if play and bpy.context.screen:
        scene['transition_preview_stop_frame']=73;bpy.ops.screen.animation_play()
    return {'name':'TestSqueeze','durationSeconds':2.4,'frames':[1,73],'loop':False}
class CHDDD_OT_Review9Expression(bpy.types.Operator):
    bl_idname='chddd.review9_expression';bl_label='Test >o< second draft'
    def execute(self,context):
        if context.screen and context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
        preview_review9_expression(context.scene,True);return {'FINISHED'}
old=getattr(bpy.types,CHDDD_OT_Review9Expression.__name__,None)
if old:bpy.utils.unregister_class(old)
bpy.utils.register_class(CHDDD_OT_Review9Expression)
'''
(ROOT/'CHDDD_BodyControls_v2.py').write_text(controls,encoding='utf-8')
exec(compile(controls,'review9_controls','exec'))
for n in ('CHDDD_BodyControls_v2.py','CHDDD_Controls.py'):
    text=bpy.data.texts.get(n) or bpy.data.texts.new(n);text.clear();text.write((ROOT/n).read_text(encoding='utf-8'))
scene['body_v2_revision']=9;scene['review_status']='Second review iteration; awaiting user review'
preview_review9_expression(scene);scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
(ROOT/'expression_test_v2.json').write_text(json.dumps({'label':'>o<','reviewIteration':2,'method':'Accepted Blink skin + original skin swatch masks + independent head-skinned chevron eye lines','irisScaleChanged':False,'failedPreviousMorphActive':False,'neutralRestore':True},indent=2),encoding='utf-8')
print('REVIEW9_CREATED',flush=True)
