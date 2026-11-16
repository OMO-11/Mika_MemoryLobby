"""Read-only source audit; generated FBX probes are for verification only."""
import bpy
import json
import math
import pathlib
import sys

root = pathlib.Path(__file__).resolve().parents[2]
out = root / 'Docs' / 'Audits' / 'CH0069'
out.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps = 30
bpy.ops.import_scene.gltf(filepath=str(root / 'Material/CH0069/Model/CH0069.glb'))
report = {'blender_version': bpy.app.version_string, 'objects': [], 'actions': []}
for obj in bpy.data.objects:
    entry = {'name': obj.name, 'type': obj.type, 'parent': obj.parent.name if obj.parent else None}
    if obj.type == 'ARMATURE':
        entry['bones'] = [bone.name for bone in obj.data.bones]
        entry['nla_tracks'] = [track.name for track in obj.animation_data.nla_tracks] if obj.animation_data else []
    if obj.type == 'MESH':
        entry.update(vertices=len(obj.data.vertices), polygons=len(obj.data.polygons),
                     materials=[m.name if m else None for m in obj.data.materials],
                     vertex_groups=len(obj.vertex_groups),
                     armatures=[m.object.name for m in obj.modifiers if m.type == 'ARMATURE' and m.object],
                     shape_keys=len(obj.data.shape_keys.key_blocks) if obj.data.shape_keys else 0)
    report['objects'].append(entry)
report['actions'] = [{'name': a.name, 'frames': list(a.frame_range)} for a in bpy.data.actions]
armatures = [o for o in bpy.data.objects if o.type == 'ARMATURE']
body = next(o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('CH0069_Body'))
arm = next(o for o in armatures if o.pose.bones.get('Bip001 Head'))
# Evaluate real imported animation, rather than merely counting action datablocks.
track = next(t for t in arm.animation_data.nla_tracks if 'Cafe_Walk' in t.name)
for t in arm.animation_data.nla_tracks:
    t.mute = t != track
arm.animation_data.action = None
def evaluated_positions(frame):
    bpy.context.scene.frame_set(frame)
    evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    positions = [tuple(evaluated.matrix_world @ v.co) for v in mesh.vertices]
    evaluated.to_mesh_clear()
    return positions
strip = track.strips[0]
first = evaluated_positions(int(strip.frame_start))
second = evaluated_positions(int((strip.frame_start + strip.frame_end) / 2))
deltas = [math.dist(a, b) for a, b in zip(first, second)]
report['walk_deformation_probe'] = {'track': track.name, 'max_vertex_delta': max(deltas),
                                   'changed_vertices': sum(d > 1e-5 for d in deltas)}
for t in arm.animation_data.nla_tracks:
    t.mute = False
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.context.scene.objects:
    # glTF import creates a mesh solely to display custom bone shapes.
    if obj.type != 'MESH' or obj.name.startswith('CH0069_'):
        obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out / 'CH0069_original_probe.fbx'), use_selection=True,
                        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
                        bake_anim_use_nla_strips=True, bake_anim_simplify_factor=0,
                        path_mode='AUTO', axis_forward='-Z', axis_up='Y')
# A deliberately small head movement proves that a new clip can be authored.
# It is not a production animation or a replacement for HeadPat/Thinking.
for obj in bpy.data.objects:
    obj.animation_data_clear()
for action in list(bpy.data.actions):
    bpy.data.actions.remove(action)
for bone in arm.pose.bones:
    bone.matrix_basis.identity()
head = arm.pose.bones['Bip001 Head']
head.rotation_mode = 'XYZ'
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 31
for frame, angle in [(1, 0), (16, math.radians(8)), (31, 0)]:
    head.rotation_euler = (0, 0, angle)
    head.keyframe_insert(data_path='rotation_euler', frame=frame, group=head.name)
arm.animation_data.action.name = 'Audit_CustomHeadTurn'
bpy.ops.export_scene.fbx(filepath=str(out / 'CH0069_custom_probe.fbx'), use_selection=True,
                        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
                        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
                        path_mode='AUTO', axis_forward='-Z', axis_up='Y')
report['custom_clip'] = {'name': arm.animation_data.action.name, 'bone': head.name, 'frames': [1, 31]}
(out / 'blender_audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('CH0069_AUDIT', json.dumps({'version': report['blender_version'], 'actions': len(report['actions']),
                                'deformation': report['walk_deformation_probe'], 'output': str(out)}))
