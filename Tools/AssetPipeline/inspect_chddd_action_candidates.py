"""Preview existing source clips for planning; restore the live scene, never save it."""
import bpy
import json
import math
from pathlib import Path
from mathutils import Matrix, Vector

scene = bpy.context.scene
arm = next(o for o in scene.objects if o.type == 'ARMATURE')
body = bpy.data.objects['CH0069_Body']
out = Path('W:/Machine/VSCode/Mika_Gift/Docs/Production/CHDDD/ReferencePreviews')
out.mkdir(parents=True, exist_ok=True)
names = scene.get('inspection_candidates', 'Cafe_Idle,Cafe_Walk,Cafe_Reaction,Public01,Victory_Start,Victory_End').split(',')
neutral = json.loads(arm['neutral_body_pose'])
frame = (scene.frame_current, scene.frame_subframe)
bases = {b.name: b.matrix_basis.copy() for b in arm.pose.bones}
animations = []
ids = list(scene.objects) + [body.data.shape_keys]
ids += [m.node_tree for m in bpy.data.materials if m.use_nodes]
for owner in ids:
    ad = owner.animation_data
    if ad:
        animations.append((ad, ad.action, ad.action_slot, ad.use_nla,
                           [(t, t.mute, t.is_solo) for t in ad.nla_tracks]))
shapes = [(k, k.value) for k in body.data.shape_keys.key_blocks]
mouth = bpy.data.materials['CHDDD_MouthAtlas'].node_tree
weights = [(n, n.outputs[0].default_value) for n in mouth.nodes if n.name.startswith('MouthWeight_')]
camera = scene.camera
camera_matrix = camera.matrix_world.copy()
camera_scale = camera.data.ortho_scale
camera_type = camera.data.type
settings = {k: getattr(scene.render, k) for k in ('resolution_x', 'resolution_y', 'resolution_percentage', 'filepath')}
visibility = [(o, o.hide_render) for o in scene.objects]
report = []
try:
    for ad, action, slot, use_nla, tracks in animations:
        ad.action = None
        ad.use_nla = True
        for t, mute, solo in tracks:
            t.mute = True
            t.is_solo = False
    for k, value in shapes:
        if k.name != 'Basis':
            k.value = 0
    for n, value in weights:
        n.outputs[0].default_value = 1.0 if n.name == 'MouthWeight_300' else 0.0
    for o, hidden in visibility:
        if o.type == 'MESH' and not o.name.startswith(('CH0069_', 'CHDDD_Halo')):
            o.hide_render = True
    scene.render.resolution_x = 300
    scene.render.resolution_y = 420
    scene.render.resolution_percentage = 100
    camera.data.type = 'ORTHO'
    fitted = False
    for short in names:
        track = next(t for t in arm.animation_data.nla_tracks if t.name.startswith('CH0069_' + short + '--'))
        strip = track.strips[0]
        track.mute = False
        poses = []
        for proportion in (0.0, 0.1, 0.5, 0.9, 1.0):
            for name, rows in neutral.items():
                arm.pose.bones[name].matrix_basis = Matrix(rows)
            f = strip.frame_start + (strip.frame_end - strip.frame_start) * proportion
            scene.frame_set(math.floor(f), subframe=f - math.floor(f))
            # Preview with neutral shapes; source animation may reevaluate face bones at render.
            for name, rows in neutral.items():
                if 'eye' in name.lower() or name == 'bone_Mouth':
                    arm.pose.bones[name].matrix_basis = Matrix(rows)
            bpy.context.view_layer.update()
            poses.append({b.name: b.matrix.copy() for b in arm.pose.bones})
            if not fitted:
                evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
                points = [evaluated.matrix_world @ v.co for v in evaluated.data.vertices]
                lo = Vector([min(p[i] for p in points) for i in range(3)])
                hi = Vector([max(p[i] for p in points) for i in range(3)])
                center = (lo + hi) / 2
                camera.location = center + Vector((0, -4, 0.10))
                camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
                camera.data.ortho_scale = max((hi.z-lo.z)*1.25, (hi.x-lo.x)*1.4*420/300)
                fitted = True
            if proportion not in (0.0, 1.0):
                scene.render.filepath = str(out / f'{short}_{int(proportion*100):02d}.png')
                bpy.ops.render.render(write_still=True)
        track.mute = True
        body_names = [n for n in poses[0] if n.startswith('Bip001 ') and 'eye' not in n.lower() and 'Xtra' not in n]
        ends = [((poses[0][n].translation-poses[-1][n].translation).length,
                 math.degrees(poses[0][n].to_quaternion().rotation_difference(poses[-1][n].to_quaternion()).angle))
                for n in body_names]
        report.append({'clip': short, 'seconds': (strip.frame_end-strip.frame_start)/30,
                       'max_body_endpoint_distance': max(x[0] for x in ends),
                       'max_body_endpoint_angle_deg': max(min(x[1], 360-x[1]) for x in ends),
                       'samples_percent': [10,50,90], 'face': 'source animation channels; neutral shape values for preview only'})
finally:
    for ad, action, slot, use_nla, tracks in animations:
        ad.action = action
        if action and slot:
            ad.action_slot = slot
        ad.use_nla = use_nla
        for t, mute, solo in tracks:
            t.mute = mute
            t.is_solo = solo
    scene.frame_set(frame[0], subframe=frame[1])
    for name, matrix in bases.items():
        arm.pose.bones[name].matrix_basis = matrix
    for k, value in shapes:
        k.value = value
    for n, value in weights:
        n.outputs[0].default_value = value
    for o, hidden in visibility:
        o.hide_render = hidden
    camera.matrix_world = camera_matrix
    camera.data.type = camera_type
    camera.data.ortho_scale = camera_scale
    for k, value in settings.items():
        setattr(scene.render, k, value)
    bpy.context.view_layer.update()
(out / ('inspection_' + names[0] + '.json')).write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({'inspected':report,'restored':True,'saved_blend':False}))
