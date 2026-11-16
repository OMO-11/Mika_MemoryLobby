"""Apply mirrored vector eyes and symmetric flat cards in a backed-up workfile."""
import bpy,json,shutil
from pathlib import Path
from mathutils import Vector

MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
ROOT=MAIN/'Reviews/V14_ChibiEyeReview'
for folder in (ROOT/'Blender',ROOT/'Preview'):
    folder.mkdir(parents=True,exist_ok=True)
for name in ('character_manifest.json','character_manifest_before_actions_v1.json','CHDDD_Controls.py'):
    shutil.copy2(MAIN/name,ROOT/name)
for path in (MAIN/'Blender').glob('*.json'):
    shutil.copy2(path,ROOT/'Blender'/path.name)
controls=(MAIN/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8').replace('平面贴图 V13 · 收小眼线夹角','平面贴图 V14 · Q 版对称眼线')
(ROOT/'CHDDD_BodyControls_v2.py').write_text(controls,encoding='utf-8')
text=bpy.data.texts['CHDDD_BodyControls_v2.py'];text.clear();text.write(controls)

obj=bpy.data.objects['CHDDD_TestSqueeze_EyeLines']
vertices=obj.data.vertices
assert len(vertices)==8 and len(obj.data.polygons)==2
# Reflect in the head's bind-space sagittal plane, not in a posed camera view.
# Average corresponding corners before mirroring so the correction is balanced.
for left_index,right_index in ((0,5),(1,4),(2,7),(3,6)):
    left=vertices[left_index].co.copy();right=vertices[right_index].co.copy()
    middle=Vector(((left.x+right.x)/2,(left.y+right.y)/2,(left.z-right.z)/2))
    vertices[left_index].co=middle
    vertices[right_index].co=Vector((middle.x,middle.y,-middle.z))
obj.data.update()
obj['review_revision']=14
obj['eyeSource']='Original mirrored chibi vector silhouette; no cutout from the lobby illustration'
obj['angle_refinement']='Same approximately 20/21 degree opening as V13; clean vector edges'
obj['geometry_symmetry']='Mirrored bind-space vertices about the head sagittal plane'
nt=bpy.data.materials['Review9_SqueezeEyeLine'].node_tree
texture=next(n for n in nt.nodes if n.type=='TEX_IMAGE' and n.image.name.startswith('mika_squeeze_eyes'))
image=bpy.data.images.load(str(ROOT/'Textures/mika_squeeze_eyes.png'),check_existing=False)
image.name='mika_squeeze_eyes_chibi_v14.png';image.alpha_mode='STRAIGHT';image.pack()
texture.image=image;texture.interpolation='Linear'

# Closed-lid normals leave bright seams beneath a clean 2D glyph. During this
# independent test only, blend the existing eye-skin mask toward even toon skin.
# The same animated mask already resets to zero for all standard expressions.
face_nt=bpy.data.materials['CHDDD_Face'].node_tree
emission=next(n for n in face_nt.nodes if n.type=='EMISSION')
previous_color=emission.inputs['Color'].links[0].from_socket
eye_skin_mix=face_nt.nodes['Review9_ReplaceEyeLinesOnly']
mask_socket=eye_skin_mix.inputs[0].links[0].from_socket
flat_skin=face_nt.nodes.new('ShaderNodeMixRGB');flat_skin.name='Review14_EvenClosedEyeSkin';flat_skin.blend_type='MULTIPLY';flat_skin.inputs[0].default_value=1
face_nt.links.new(face_nt.nodes['Review9_OriginalSkinSwatch'].outputs['Color'],flat_skin.inputs[1])
flat_skin.inputs[2].default_value=(1,.85,.85,1)
body=bpy.data.objects['CH0069_Body']
seam=body.data.attributes.new(name='Review14_ClosedLidSeamMask',type='FLOAT',domain='POINT')
closed=body.data.shape_keys.key_blocks['Pet_Blink']
for vertex in body.data.vertices:
    distance=abs(closed.data[vertex.index].co.y+.373)
    t=min(1,max(0,(distance-.009)/.013))
    seam.data[vertex.index].value=1-t*t*(3-2*t)
seam_attr=face_nt.nodes.new('ShaderNodeAttribute');seam_attr.attribute_name=seam.name
crease_mask=face_nt.nodes.new('ShaderNodeMath');crease_mask.operation='MULTIPLY'
face_nt.links.new(mask_socket,crease_mask.inputs[0]);face_nt.links.new(seam_attr.outputs['Fac'],crease_mask.inputs[1])
brightness=face_nt.nodes.new('ShaderNodeRGBToBW');face_nt.links.new(face_nt.nodes['Toon_Shadow_Bands'].outputs['Color'],brightness.inputs[0])
highlight=face_nt.nodes.new('ShaderNodeMapRange');highlight.clamp=True
highlight.inputs['From Min'].default_value=.90;highlight.inputs['From Max'].default_value=.98
face_nt.links.new(brightness.outputs[0],highlight.inputs['Value'])
highlight_mask=face_nt.nodes.new('ShaderNodeMath');highlight_mask.operation='MULTIPLY'
face_nt.links.new(crease_mask.outputs[0],highlight_mask.inputs[0]);face_nt.links.new(highlight.outputs[0],highlight_mask.inputs[1])
smooth_skin=face_nt.nodes.new('ShaderNodeMixRGB');smooth_skin.name='Review14_ClosedLidSeamCleanup'
face_nt.links.new(highlight_mask.outputs[0],smooth_skin.inputs[0]);face_nt.links.new(previous_color,smooth_skin.inputs[1]);face_nt.links.new(flat_skin.outputs[0],smooth_skin.inputs[2])
face_nt.links.new(smooth_skin.outputs[0],emission.inputs['Color'])
mapping=json.loads((ROOT/'Blender/body_action_map_v2.json').read_text(encoding='utf-8'))
mapping.update(revision=14,reviewIteration=7,reviewStatus='Symmetric chibi vector eyes; waiting for review')
(ROOT/'Blender/body_action_map_v2.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
scene=bpy.context.scene;scene['body_v2_revision']=14;scene['review_status']='V14 mirrored chibi eyes; pending user review';scene['pending_review']='V14 mirrored chibi eyes'
exec(compile(controls,'review14_controls','exec'))
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
preview_review9_expression(scene);scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
errors=[(vertices[a].co-Vector((vertices[b].co.x,vertices[b].co.y,-vertices[b].co.z))).length for a,b in ((0,5),(1,4),(2,7),(3,6))]
geometry=dict(revision=14,vertices=8,faces=2,planarCards=True,headBound=True,
    symmetryPlane='Body bind-space Z=0 (head rest sagittal plane)',maxVertexMirrorError=max(errors),
    bindVertices=[tuple(v.co) for v in vertices],projection=False,shrinkwrap=False)
(ROOT/'flat_card_geometry.json').write_text(json.dumps(geometry,indent=2),encoding='utf-8')
print('V14_CHIBI_EYE_DRAFT_CREATED',flush=True)
