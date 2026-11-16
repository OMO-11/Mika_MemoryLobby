"""Independent -omega- test: original brow geometry and a smooth native mouth ribbon."""
import bpy,json,shutil,math
import numpy as np
from pathlib import Path
from mathutils import Vector

MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
ROOT=MAIN/'Reviews/V16_OmegaFirstReview'
for folder in ('Blender','Preview','Textures'):(ROOT/folder).mkdir(parents=True,exist_ok=True)
for name in ('character_manifest.json','character_manifest_before_actions_v1.json','CHDDD_Controls.py','CHDDD_BodyControls_v2.py'):
    shutil.copy2(MAIN/name,ROOT/name)
for path in (MAIN/'Blender').glob('*.json'):shutil.copy2(path,ROOT/'Blender'/path.name)
for suffix in ('png','svg'):shutil.copy2(MAIN/f'Textures/mika_squeeze_eyes_review.{suffix}',ROOT/f'Textures/mika_squeeze_eyes.{suffix}')
body=bpy.data.objects['CH0069_Body'];arm=bpy.data.objects['Bip001'];scene=bpy.context.scene
mesh=body.data;base=mesh.shape_keys.key_blocks['Basis']

def opacity_wrapper(material,name,hide=False):
    tree=material.node_tree;nodes=tree.nodes;links=tree.links
    output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
    surface=output.inputs['Surface'].links[0].from_socket
    transparent=nodes.new('ShaderNodeBsdfTransparent')
    opacity=nodes.new('ShaderNodeValue');opacity.name=name;opacity.outputs[0].default_value=0
    mix=nodes.new('ShaderNodeMixShader');links.new(opacity.outputs[0],mix.inputs[0])
    links.new(surface,mix.inputs[1 if hide else 2]);links.new(transparent.outputs[0],mix.inputs[2 if hide else 1])
    links.new(mix.outputs[0],output.inputs['Surface'])
    return opacity

def head_mesh(name,vertices,faces,material):
    data=bpy.data.meshes.new(name+'Mesh');data.from_pydata(vertices,[],faces);data.update()
    obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj)
    obj.matrix_world=body.matrix_world.copy();data.materials.append(material)
    group=obj.vertex_groups.new(name='Bip001 Head');group.add(list(range(len(vertices))),1,'REPLACE')
    modifier=obj.modifiers.new('HeadDeform','ARMATURE');modifier.object=arm
    obj['experimental_only']=True;obj['review_revision']=16
    return obj

# Duplicate one original eyebrow, retaining its topology, UVs and shallow depth.
brow_index=next(i for i,m in enumerate(mesh.materials) if m.name=='CHDDD_Eyebrow')
polys=[p for p in mesh.polygons if p.material_index==brow_index and all(base.data[i].co.z>0 for i in p.vertices)]
ids=sorted(set(i for p in polys for i in p.vertices));indices={i:j for j,i in enumerate(ids)}
points=[base.data[i].co.copy() for i in ids]
fit=np.polyfit([p.z for p in points],[p.y for p in points],3)
depth_fit=np.polyfit([p.z for p in points],[p.x for p in points],3)
center_z=(min(p.z for p in points)+max(p.z for p in points))/2
center_y=float(np.polyval(fit,center_z))
cards=bpy.data.objects['CHDDD_TestSqueeze_EyeLines'].data.vertices
p0,p1,p2=[cards[i].co for i in (4,5,6)]
normal=(p1-p0).cross(p2-p0);constant=normal.dot(p0)
vertices=[]
for sign in (1,-1):
    for co in points:
        z=.065+(co.z-center_z)*.78
        line_y=-.378+(float(np.polyval(fit,co.z))-center_y)*.12
        y=line_y+(co.y-float(np.polyval(fit,co.z)))*1.25
        x=(constant-normal.y*y-normal.z*z)/normal.x+.0012+co.x-float(np.polyval(depth_fit,co.z))
        vertices.append((x,y,sign*z))
faces=[]
for sign_index in (0,1):
    for p in polys:
        face=tuple(indices[i]+sign_index*len(ids) for i in p.vertices)
        faces.append(face if sign_index==0 else tuple(reversed(face)))
eye_material=bpy.data.materials['CHDDD_Eyebrow'].copy();eye_material.name='Review16_OmegaEyeLine'
eye_tree=eye_material.node_tree
eye_emission=next(n for n in eye_tree.nodes if n.type=='EMISSION')
original_color=eye_emission.inputs['Color'].links[0].from_socket
tint=eye_tree.nodes.new('ShaderNodeMixRGB');tint.name='Review16_EyeLineTint';tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1
tint.inputs[2].default_value=(.40,.32,.34,1)
eye_tree.links.new(original_color,tint.inputs[1]);eye_tree.links.new(tint.outputs[0],eye_emission.inputs['Color'])
opacity_wrapper(eye_material,'Review16_EyeOpacity')
eyes=head_mesh('CHDDD_TestOmega_Eyes',vertices,faces,eye_material)
uv=eyes.data.uv_layers.new(name='UVMap')
for poly_index,p in enumerate(eyes.data.polygons):
    source_poly=polys[poly_index%len(polys)]
    source_uv=[mesh.uv_layers.active.data[i].uv.copy() for i in source_poly.loop_indices]
    if poly_index>=len(polys):source_uv.reverse()
    for li,value in zip(p.loop_indices,source_uv):uv.data[li].uv=value
for p in eyes.data.polygons:p.use_smooth=True
eyes['source']='Exact CHDDD_Eyebrow positive-side mesh and UVs, mirrored; original brows preserved'
eyes['style']='78 percent brow width, 12 percent centerline curvature, 125 percent strip thickness'

# Symmetric rounded omega path; a single filled ribbon avoids font and alpha artifacts.
start=(18,22)
segments=[((10,32),(18,48),(31,48)),((43,48),(50,39),(50,30)),
          ((50,39),(57,48),(69,48)),((82,48),(90,32),(82,22))]
path=[Vector(start)];previous=Vector(start)
for a,b,c in segments:
    a,b,c=Vector(a),Vector(b),Vector(c)
    for step in range(1,33):
        t=step/32;s=1-t;path.append(previous*s**3+a*3*s*s*t+b*3*s*t*t+c*t**3)
    previous=c
mouth_center_y=-.326;scale=.00047;halfwidth=.00085;mouth_x=.082
vertices=[];faces=[]
for index,point in enumerate(path):
    tangent=(path[min(index+1,len(path)-1)]-path[max(index-1,0)]).normalized()
    perpendicular=Vector((-tangent.y,tangent.x))
    for sign in (-1,1):
        q=(point-Vector((50,35)))*scale+perpendicular*halfwidth*sign
        vertices.append((mouth_x,mouth_center_y+q.y,q.x))
for index in range(len(path)-1):faces.append((index*2,index*2+1,index*2+3,index*2+2))
for index in (0,len(path)-1):
    point=path[index];tangent=(path[1]-path[0] if index==0 else path[-1]-path[-2]).normalized()
    if index==0:tangent=-tangent
    angle=math.atan2(tangent.y,tangent.x)
    center=len(vertices);vertices.append((mouth_x,mouth_center_y+(point.y-35)*scale,(point.x-50)*scale))
    ring=[]
    for j in range(17):
        theta=angle-math.pi/2+j*math.pi/16
        ring.append(len(vertices));vertices.append((mouth_x,mouth_center_y+(point.y-35)*scale+math.sin(theta)*halfwidth,(point.x-50)*scale+math.cos(theta)*halfwidth))
    for a,b in zip(ring,ring[1:]):faces.append((center,a,b))
mouth_material=bpy.data.materials.new('Review16_OmegaMouth');mouth_material.use_nodes=True
nodes=mouth_material.node_tree.nodes;links=mouth_material.node_tree.links;nodes.clear()
emission=nodes.new('ShaderNodeEmission');emission.inputs['Color'].default_value=(.39,.17,.14,1)
output=nodes.new('ShaderNodeOutputMaterial');links.new(emission.outputs[0],output.inputs['Surface'])
opacity_wrapper(mouth_material,'Review16_MouthOpacity')
mouth_obj=head_mesh('CHDDD_TestOmega_Mouth',vertices,faces,mouth_material)
mouth_obj['source']='Native symmetric cubic omega ribbon with round endpoints'
hide_mouth=opacity_wrapper(bpy.data.materials['CHDDD_MouthAtlas'],'Review16_HideOriginalMouth',hide=True)

def ease(t):
    t=min(1,max(0,t));return t*t*(3-2*t)
def weights(t):
    blink=ease((t-.06)/.16)*ease((.97-t)/.16)
    glyph=ease((t-.23)/.13)*ease((.79-t)/.13)
    mouth=ease((t-.14)/.18)*ease((.88-t)/.18)
    return blink,glyph,mouth
def animate(owner,name,channels):
    action=bpy.data.actions.new(name);action.use_fake_user=True
    owner.animation_data_create();owner.animation_data.action=action
    for path,values in channels:
        for frame,value in enumerate(values,1):
            owner.path_resolve(path.rsplit('.',1)[0]).default_value=value if path.endswith('.default_value') else value
            # Use standard ID keyframe insertion for Blender's layered action slots.
            owner.keyframe_insert(data_path=path,frame=frame,group='OmegaTest')
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for point in curve.keyframe_points:point.interpolation='LINEAR'
    return action
samples=[weights(i/72) for i in range(73)]
# Shape values need direct assignment before insertion (the socket helper below is separate).
keys=mesh.shape_keys;action=bpy.data.actions.new('Review16_TestOmega_Shapes');action.use_fake_user=True
keys.animation_data_create();keys.animation_data.action=action
for key in keys.key_blocks:
    if key.name=='Basis':continue
    for frame,(blink,glyph,mouth) in enumerate(samples,1):
        key.value=blink if key.name=='Pet_Blink' else 0
        key.keyframe_insert(data_path='value',frame=frame,group=key.name)
for material_name,node_name,action_name,column in (
    ('Review16_OmegaEyeLine','Review16_EyeOpacity','Review16_TestOmega_Eyes',1),
    ('Review16_OmegaMouth','Review16_MouthOpacity','Review16_TestOmega_MouthLine',2),
    ('CHDDD_Face','Review9_OriginalEyeLineMask','Review16_TestOmega_Face',1),
    ('CHDDD_EyeMouth','Review9_OldEyeLineOpacity','Review16_TestOmega_OldLashes',1)):
    tree=bpy.data.materials[material_name].node_tree
    animate(tree,action_name,[(tree.nodes[node_name].outputs[0].path_from_id('default_value'),[s[column] for s in samples])])
tree=bpy.data.materials['CHDDD_MouthAtlas'].node_tree
channels=[(hide_mouth.outputs[0].path_from_id('default_value'),[s[2] for s in samples])]
for node in tree.nodes:
    if node.name.startswith('MouthWeight_'):channels.append((node.outputs[0].path_from_id('default_value'),[1 if node.name=='MouthWeight_300' else 0]*73))
animate(tree,'Review16_TestOmega_MouthAtlas',channels)

extra_reset="('Review16_OmegaEyeLine','Review16_EyeOpacity'),('Review16_OmegaMouth','Review16_MouthOpacity'),('CHDDD_MouthAtlas','Review16_HideOriginalMouth'),"
for filename in ('CHDDD_Controls.py','CHDDD_BodyControls_v2.py'):
    contents=(ROOT/filename).read_text(encoding='utf-8').replace("[('CHDDD_Face','Review9_OriginalEyeLineMask')",'['+extra_reset+"('CHDDD_Face','Review9_OriginalEyeLineMask')")
    if filename=='CHDDD_BodyControls_v2.py':
        contents=contents.replace('平面贴图 V15 · 稍粗稍小眼线','表情实验 V16 · -ω- 与已保留 >o<')
        contents=contents.replace("self.layout.operator('chddd.review9_expression',text='>o< 平面贴图测试',icon='PLAY')", "self.layout.operator('chddd.review9_expression',text='>o< 已保留 V15',icon='PLAY')\n        self.layout.operator('chddd.review16_omega',text='-ω- 第一版测试',icon='PLAY')")
        contents+='''
def preview_review16_omega(scene,play=False):
    preview_body_v2(scene,'Idle')
    bpy.data.objects['CH0069_Body'].data.shape_keys.animation_data.action=bpy.data.actions['Review16_TestOmega_Shapes']
    for material_name,action_name in [('Review16_OmegaEyeLine','Review16_TestOmega_Eyes'),('Review16_OmegaMouth','Review16_TestOmega_MouthLine'),('CHDDD_Face','Review16_TestOmega_Face'),('CHDDD_EyeMouth','Review16_TestOmega_OldLashes'),('CHDDD_MouthAtlas','Review16_TestOmega_MouthAtlas')]:
        tree=bpy.data.materials[material_name].node_tree;tree.animation_data_create();tree.animation_data.action=bpy.data.actions[action_name]
    scene.frame_start=1;scene.frame_end=73;scene.frame_set(1);scene['body_v2_action']='TestOmega'
    if play and bpy.context.screen:
        scene['transition_preview_stop_frame']=73;bpy.ops.screen.animation_play()
    return {'name':'TestOmega','durationSeconds':2.4,'frames':[1,73],'loop':False}
class CHDDD_OT_Review16Omega(bpy.types.Operator):
    bl_idname='chddd.review16_omega';bl_label='Test -omega- expression'
    def execute(self,context):
        if context.screen and context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
        preview_review16_omega(context.scene,True);return {'FINISHED'}
old=getattr(bpy.types,CHDDD_OT_Review16Omega.__name__,None)
if old:bpy.utils.unregister_class(old)
bpy.utils.register_class(CHDDD_OT_Review16Omega)
'''
    (ROOT/filename).write_text(contents,encoding='utf-8')
    text=bpy.data.texts[filename];text.clear();text.write(contents)
    exec(compile(contents,filename,'exec'))
mapping_path=ROOT/'Blender/body_action_map_v2.json';mapping=json.loads(mapping_path.read_text(encoding='utf-8'))
mapping.update(revision=16,reviewStatus='V15 squeeze eyes provisionally retained; first independent omega expression pending review')
mapping_path.write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
scene['body_v2_revision']=16;scene['pending_review']='-omega- first experiment'
scene['review_status']='V16 omega test pending review; squeeze V15 provisionally retained'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
preview_review16_omega(scene);scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
(ROOT/'omega_design.json').write_text(json.dumps(dict(revision=16,expression='-ω-',eyeSource='CHDDD_Eyebrow mesh and UVs',sourceBrowVertexCount=len(ids),sourceBrowFaceCount=len(polys),eyesMirrored=True,bodyTopologyChanged=False,existingShapeGeometryChanged=False,squeezeV15Preserved=True,durationSeconds=2.4),ensure_ascii=False,indent=2),encoding='utf-8')
print('V16_OMEGA_FIRST_DRAFT_CREATED',flush=True)
