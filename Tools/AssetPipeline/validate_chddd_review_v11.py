"""Regression checks for recovery, unwarped head-bound cards and clean resets."""
import bpy, json, hashlib
from pathlib import Path
from mathutils import Vector
ROOT = Path(bpy.data.filepath).parent
PREV = Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V10_TextureReview')
scene = bpy.context.scene
body = bpy.data.objects['CH0069_Body']
arm = bpy.data.objects['Bip001']
cards = bpy.data.objects['CHDDD_TestSqueeze_EyeLines']
checks, measurements = {}, {}

def curves(action):
    return [(c.data_path, c.array_index, [(tuple(p.co), p.interpolation) for p in c.keyframe_points])
        for layer in action.layers for strip in layer.strips for bag in strip.channelbags for c in bag.fcurves]

with bpy.data.libraries.load(str(PREV/'CHDDD_actions_v2.blend'), link=False) as (src, dst):
    names = tuple(n for n in src.actions if n.startswith(('PetV2_', 'Review9_TestSqueeze_')))
    dst.actions = list(names)
    dst.objects = ['CH0069_Body']
checks['friday_body_face_test_actions_recovered_exactly'] = all(curves(bpy.data.actions[n]) == curves(a) for n,a in zip(names,dst.actions))
old = dst.objects[0].data
keys = body.data.shape_keys.key_blocks
checks['all_shape_geometry_preserved'] = all(all((v.co-keys[k.name].data[i].co).length == 0 for i,v in enumerate(k.data)) for k in old.shape_keys.key_blocks)
checks['body_topology_preserved'] = len(old.vertices) == len(body.data.vertices) and len(old.polygons) == len(body.data.polygons)
mapping = json.loads((ROOT/'Blender/body_action_map_v2.json').read_text(encoding='utf-8'))
checks['all_18_actions_recovered'] = len(mapping['actions']) == 18
checks['action_entries_match_latest_friday_draft'] = mapping['actions'] == json.loads((PREV/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))['actions']
checks['face_profiles_match_friday_draft'] = (ROOT/'Blender/action_face_profiles_v2.json').read_bytes() == (PREV/'Unity/action_face_profiles_v2.json').read_bytes()
checks['two_flat_quads_only'] = len(cards.data.vertices) == 8 and len(cards.data.polygons) == 2 and all(len(p.vertices) == 4 for p in cards.data.polygons)
checks['head_bound_without_shrinkwrap'] = len(cards.vertex_groups) == 1 and cards.vertex_groups[0].name == 'Bip001 Head' and all(m.type != 'SHRINKWRAP' for m in cards.modifiers) and any(m.type == 'ARMATURE' and m.object == arm for m in cards.modifiers)
crop = json.loads((PREV/'texture_crop_manifest.json').read_text(encoding='utf-8'))
checks['user_source_texture_unchanged'] = hashlib.sha256(Path(crop['source']).read_bytes()).hexdigest() == crop['sourceSHA256']
texture = next(n.image for n in cards.data.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE' and n.image.name.startswith('mika_squeeze_eyes'))
checks['eye_crop_packed'] = bool(texture.packed_file)
exec(compile((ROOT/'CHDDD_Controls.py').read_text(encoding='utf-8'), 'static_controls', 'exec'))
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'), 'body_controls', 'exec'))
preview_review9_expression(scene)
planarity = []
for frame in (17, 37, 55):
    scene.frame_set(frame)
    mesh = cards.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    for poly in mesh.polygons:
        p = [mesh.vertices[i].co for i in poly.vertices]
        normal = (p[1]-p[0]).cross(p[2]-p[0]).normalized()
        planarity.append(abs(normal.dot(p[3]-p[0])))
    cards.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
measurements['max_nonplanarity_m'] = max(planarity)
checks['cards_stay_flat_during_motion'] = max(planarity) < .000001
checks['closed_lids_when_cards_visible'] = True
for frame in range(1,74):
    scene.frame_set(frame)
    alpha = bpy.data.materials['Review9_SqueezeEyeLine'].node_tree.nodes['Review9_EyeLineOpacity'].outputs[0].default_value
    if alpha > .001 and keys['Pet_Blink'].value < .99:
        checks['closed_lids_when_cards_visible'] = False
checks['all_body_actions_clear_experiment'] = True
for entry in mapping['actions']:
    preview_review9_expression(scene)
    scene.frame_set(37)
    preview_body_v2(scene, entry['name'])
    if bpy.data.materials['Review9_SqueezeEyeLine'].node_tree.nodes['Review9_EyeLineOpacity'].outputs[0].default_value != 0 or keys['Pet_TestRoundMouth_Review'].value != 0:
        checks['all_body_actions_clear_experiment'] = False
checks['all_standard_emotions_clear_experiment'] = True
manifest = json.loads((ROOT/'character_manifest.json').read_text(encoding='utf-8'))
for emotion in manifest['expressions']:
    preview_review9_expression(scene)
    scene.frame_set(37)
    apply_expression(scene, emotion)
    if bpy.data.materials['Review9_SqueezeEyeLine'].node_tree.nodes['Review9_EyeLineOpacity'].outputs[0].default_value != 0 or keys['Pet_TestRoundMouth_Review'].value != 0 or keys['Pet_BrowHappy_Review'].value != 0:
        checks['all_standard_emotions_clear_experiment'] = False
preview_body_v2(scene, 'Success')
scene.frame_set(21)
checks['success_eyes_closed_at_arm_raise'] = keys['Pet_Blink'].value > .99
checks['main_controls_embedded'] = all(bpy.data.texts[name].as_string() == (ROOT/name).read_text(encoding='utf-8') for name in ('CHDDD_Controls.py','CHDDD_BodyControls_v2.py'))
result = dict(revision=11, checks=checks, measurements=measurements, passed=all(checks.values()), visualApproval='Pending user review', unityRevalidated=False)
(ROOT/'recovery_validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result), flush=True)
if not result['passed']:
    raise RuntimeError('Recovery or flat-card checks failed')
