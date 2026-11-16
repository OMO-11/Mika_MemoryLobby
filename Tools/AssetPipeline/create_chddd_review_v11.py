"""Recover the saved Friday performances and replace warped decals by flat cards."""
import bpy, json, shutil, numpy as np
from pathlib import Path
from mathutils import Vector

SOURCE = Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
PREVIOUS = SOURCE/'Reviews/V10_TextureReview'
ROOT = SOURCE/'Reviews/V11_FlatEyeRecovery'
for folder in (ROOT, ROOT/'Blender', ROOT/'Preview', ROOT/'Textures'):
    folder.mkdir(parents=True, exist_ok=True)
for name in ('character_manifest_before_actions_v1.json', 'character_manifest.json',
             'texture_crop_manifest.json', 'texture_crop_review.jpg'):
    shutil.copy2(PREVIOUS/name, ROOT/name)
for path in (PREVIOUS/'Textures').glob('*.png'):
    shutil.copy2(path, ROOT/'Textures'/path.name)
for name in ('expression_map_v1.json', 'expression_map_v2.json',
             'body_action_map_v2.json', 'action_face_profiles_v2.json'):
    shutil.copy2(PREVIOUS/'Unity'/name, ROOT/'Blender'/name)

controls = (PREVIOUS/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8')
exec(compile(controls, 'review10_controls', 'exec'))
scene = bpy.context.scene
body = bpy.data.objects['CH0069_Body']
arm = bpy.data.objects['Bip001']
preview_body_v2(scene, 'Idle')
scene.frame_set(1)
keys = body.data.shape_keys
keys.animation_data.action = None
for key in keys.key_blocks:
    key.value = 0
keys.key_blocks['Pet_Blink'].value = 1
bpy.context.view_layer.update()
evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())

def face_surface(y, z):
    origin = Vector((.25, y, z))
    direction = Vector((-1, 0, 0))
    for _ in range(16):
        hit, point, normal, index = evaluated.ray_cast(origin, direction)
        if not hit:
            return None
        if evaluated.data.polygons[index].material_index == 1:
            return point
        origin = point + Vector((-.0001, 0, 0))
    return None

# Measure clearance only where the source contains ink. Each card is a rigid
# rectangle; no vertex follows the face. Transparent margins need no clearance.
mat = bpy.data.materials['Review9_SqueezeEyeLine']
nt = mat.node_tree
texture = next(n for n in nt.nodes if n.type == 'TEX_IMAGE' and n.image.name.startswith('mika_squeeze_eyes'))
pixels = list(texture.image.pixels)
width_px, height_px = texture.image.size
depths, sampled_maxima, planes = [], [], []
card_width, eye_center = .096, .064
for eye_index, sign in enumerate((-1, 1)):
    samples = []
    for row in range(0, height_px, 2):
        for col in range(eye_index*width_px//2, (eye_index+1)*width_px//2, 2):
            if pixels[(row*width_px+col)*4+3] < .01:
                continue
            u = (col+.5)/(width_px/2)-eye_index
            v = (row+.5)/height_px
            point = face_surface(-.377 + (.5-v)*card_width, sign*eye_center + (u-.5)*card_width)
            if point is not None:
                samples.append(tuple(point))
    design = np.array([[1, p[1]+.377, p[2]-sign*eye_center] for p in samples])
    actual_depths = np.array([p[0] for p in samples])
    plane = np.linalg.lstsq(design, actual_depths, rcond=None)[0]
    plane[0] += max(actual_depths-design@plane)+.003
    planes.append(plane.tolist())
    sampled_maxima.append(float(max(actual_depths)))
    depths.append(float(plane[0]))

# Use only the RGBA source artwork on the cards: no skin-colored plane.
emission = next(n for n in nt.nodes if n.type == 'EMISSION')
nt.links.new(texture.outputs['Color'], emission.inputs['Color'])
mix_shader = next(n for n in nt.nodes if n.type == 'MIX_SHADER')
opacity = nt.nodes['Review9_EyeLineOpacity']
alpha = nt.nodes.new('ShaderNodeMath')
alpha.operation = 'MULTIPLY'
nt.links.new(texture.outputs['Alpha'], alpha.inputs[0])
nt.links.new(opacity.outputs[0], alpha.inputs[1])
nt.links.new(alpha.outputs[0], mix_shader.inputs[0])

# Erase the old closed-eye artwork on the actual skin, retaining face shading.
# Feather the region in mesh coordinates, below the independent brow geometry.
face_nt = bpy.data.materials['CHDDD_Face'].node_tree
attr = body.data.attributes.new(name='Review11_ClosedEyeSkinMask', type='FLOAT', domain='POINT')
closed = keys.key_blocks['Pet_Blink']
def smoothstep(a, b, value):
    t = min(1, max(0, (value-a)/(b-a)))
    return t*t*(3-2*t)
for vertex in body.data.vertices:
    co = closed.data[vertex.index].co
    attr.data[vertex.index].value = (smoothstep(-.415, -.405, co.y)
        * (1-smoothstep(-.350, -.340, co.y)) * smoothstep(.008, .018, abs(co.z))
        * (1-smoothstep(.112, .125, abs(co.z))))
attribute = face_nt.nodes.new('ShaderNodeAttribute')
attribute.attribute_name = attr.name
amount = face_nt.nodes.new('ShaderNodeMath')
amount.operation = 'MULTIPLY'
face_nt.links.new(attribute.outputs['Fac'], amount.inputs[0])
face_nt.links.new(face_nt.nodes['Review9_OriginalEyeLineMask'].outputs[0], amount.inputs[1])
face_nt.links.new(amount.outputs[0], face_nt.nodes['Review9_ReplaceEyeLinesOnly'].inputs[0])

# Some white eye inserts straddle the old all-vertices bounds. Select whole
# eye-region polygons by their center, leaving the central nose and mouth alone.
eye_only = body.data.attributes['Review10_EyeOnlyMat3Mask']
base = keys.key_blocks['Basis']
for poly in body.data.polygons:
    center = sum((base.data[i].co for i in poly.vertices), Vector())/len(poly.vertices)
    eye_only.data[poly.index].value = int(poly.material_index == 3
        and -.44 < center.y < -.34 and .008 < abs(center.z) < .17)
head = arm.pose.bones['Bip001 Head']
deform = arm.matrix_world @ head.matrix @ head.bone.matrix_local.inverted() @ arm.matrix_world.inverted()
inv = body.matrix_world.inverted() @ deform.inverted() @ body.matrix_world
verts, faces, uvs = [], [], []
for eye_index, sign in enumerate((-1, 1)):
    start = len(verts)
    for u, v in ((0, 0), (1, 0), (1, 1), (0, 1)):
        dy, dz = (.5-v)*card_width, (u-.5)*card_width
        # All four corners satisfy one plane equation. Fitting its orientation
        # keeps a floating flat card near the socket without distorting the art.
        x = planes[eye_index][0]+planes[eye_index][1]*dy+planes[eye_index][2]*dz
        point = Vector((x, -.377+dy, sign*eye_center+dz))
        verts.append(tuple(inv @ point))
        uvs.append(((eye_index+u)/2, v))
    faces.append(tuple(start+i for i in range(4)))
mesh = bpy.data.meshes.new('Review11_FlatEyeCards')
mesh.from_pydata(verts, [], faces)
mesh.update()
uv_layer = mesh.uv_layers.new(name='UVMap')
for poly in mesh.polygons:
    for loop in poly.loop_indices:
        uv_layer.data[loop].uv = uvs[mesh.loops[loop].vertex_index]
obj = bpy.data.objects['CHDDD_TestSqueeze_EyeLines']
obj.data = mesh
obj.matrix_world = body.matrix_world.copy()
obj.data.materials.append(mat)
for group in list(obj.vertex_groups):
    obj.vertex_groups.remove(group)
group = obj.vertex_groups.new(name='Bip001 Head')
group.add(list(range(8)), 1, 'REPLACE')
obj['projection'] = 'Two rigid flat image cards in front of closed eyelids; no surface projection or shrinkwrap'
obj['review_revision'] = 11
obj['measured_skin_clearance_m'] = .003
obj['eyeSource'] = 'Mika_MemoryLobby/spine_regions/10.png; unchanged V10 crops'

# Repoint the packed image as well as storing a portable disk copy.
for node in obj.data.materials[0].node_tree.nodes:
    if node.type == 'TEX_IMAGE' and node.image.name.startswith('mika_squeeze_eyes'):
        node.image.filepath = str(ROOT/'Textures/mika_squeeze_eyes.png')

# Blender authoring metadata is separate from the older Unity delivery.
controls = controls.replace("root/'Unity/body_action_map_v2.json'", "root/'Blender/body_action_map_v2.json'")
controls = controls.replace("Path(bpy.data.filepath).parent/'Unity/body_action_map_v2.json'", "Path(bpy.data.filepath).parent/'Blender/body_action_map_v2.json'")
controls = controls.replace('第三版审查 · 原素材贴图挤眼', '已恢复星期五动作 · 平面贴图试验 V11')
controls = controls.replace('>o< 第三版贴图测试', '>o< 平面贴图测试')
controls = controls.replace('Test >o< source texture draft', 'Test >o< flat image cards')
(ROOT/'CHDDD_BodyControls_v2.py').write_text(controls, encoding='utf-8')

static_controls = (PREVIOUS/'CHDDD_Controls.py').read_text(encoding='utf-8')
needle = "    for name,value in setting['blendShapes'].items():body.data.shape_keys.key_blocks[name].value=value"
replacement = """    # Clear the independent eye-card test when selecting a standard expression.
    for key in body.data.shape_keys.key_blocks:key.value=0
    for material_name,node_name in [('CHDDD_Face','Review9_OriginalEyeLineMask'),('CHDDD_EyeMouth','Review9_OldEyeLineOpacity'),('Review9_SqueezeEyeLine','Review9_EyeLineOpacity')]:
        test_material=bpy.data.materials.get(material_name)
        if test_material and test_material.node_tree.nodes.get(node_name):
            clear_action(test_material.node_tree)
            test_material.node_tree.nodes[node_name].outputs[0].default_value=0
    for name,value in setting['blendShapes'].items():body.data.shape_keys.key_blocks[name].value=value"""
assert needle in static_controls
static_controls = static_controls.replace(needle, replacement)
(ROOT/'CHDDD_Controls.py').write_text(static_controls, encoding='utf-8')
for name, source in (('CHDDD_BodyControls_v2.py', controls), ('CHDDD_Controls.py', static_controls)):
    text = bpy.data.texts.get(name) or bpy.data.texts.new(name)
    text.clear()
    text.write(source)
    text.use_module = True  # Respects Blender's existing trusted-script policy.

mapping = json.loads((ROOT/'Blender/body_action_map_v2.json').read_text(encoding='utf-8'))
mapping.update(revision=11, reviewIteration=4, reviewStatus='Flat image cards; awaiting user review')
(ROOT/'Blender/body_action_map_v2.json').write_text(json.dumps(mapping, ensure_ascii=False, indent=2), encoding='utf-8')
scene['body_v2_revision'] = 11
scene['review_status'] = 'Friday V10 actions recovered; flat eye-card test V11 pending approval'
scene['unity_delivery_revision'] = 7
scene['blender_mapping_directory'] = 'Blender'
(ROOT/'flat_card_geometry.json').write_text(json.dumps({
    'vertices':8, 'faces':2, 'depthLocalX':depths, 'sampledFaceDepthMax':sampled_maxima,
    'clearanceAtCreationMeters':.003, 'widthMeters':card_width, 'headWeighted':True,
    'flatPlaneEquations':planes,
    'projectedVertices':False, 'shrinkwrap':False,
}, indent=2), encoding='utf-8')
exec(compile(static_controls, 'review11_static_controls', 'exec'))
exec(compile(controls, 'review11_body_controls', 'exec'))
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
preview_review9_expression(scene)
scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
print('REVIEW11_FLAT_CARDS_CREATED', depths, flush=True)
