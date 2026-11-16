import bpy,json,math
from pathlib import Path
from mathutils import Matrix,Vector,Quaternion
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
OUT=ROOT/'Previews/ActionsV2/SourcePickup';OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene;arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body']
manifest=json.loads((ROOT/'character_manifest.json').read_text(encoding='utf-8'))
neutral=json.loads(arm['neutral_body_pose'])
tracks=[]
for obj in scene.objects:
    if obj.animation_data:
        obj.animation_data.action=None
        for track in obj.animation_data.nla_tracks:
            track.mute=True;track.is_solo=False
            if obj.name in ('Bip001','bone_root') and 'Formation_Pickup--' in track.name:
                track.mute=False;tracks.append((obj.name,track.name))
for owner in (body.data.shape_keys,bpy.data.materials['CHDDD_MouthAtlas'].node_tree):
    if owner.animation_data:owner.animation_data.action=None
for n in bpy.data.materials['CHDDD_MouthAtlas'].node_tree.nodes:
    if n.name.startswith('MouthWeight_'):n.outputs[0].default_value=1 if n.name=='MouthWeight_300' else 0
for k in body.data.shape_keys.key_blocks:
    if k.name!='Basis':k.value=0
for o in scene.objects:
    if o.type=='MESH' and (o.name.startswith('CH0069_Weapon') or o.name not in ('CH0069_Body','CHDDD_Halo_Restored')):o.hide_render=True
face=[n for n in manifest['faceBones'] if n!='Bip001 Head']
rows=[]
for frame in range(1,339):
    scene.frame_set(frame);bpy.context.view_layer.update()
    rows.append({'bones':{b.name:[list(r) for r in b.matrix_basis] for b in arm.pose.bones if b.name not in face},
                 'armatureWorld':[list(r) for r in arm.matrix_world],
                 'objects':{n:[list(r) for r in bpy.data.objects[n].matrix_basis] for n in ('bone_root','Bip001')},
                 'pelvisWorld':list(arm.matrix_world@arm.pose.bones['Bip001 Pelvis'].head),
                 'headWorld':list(arm.matrix_world@arm.pose.bones['Bip001 Head'].head)})
(ROOT/'body_source_pickup_v2.json').write_text(json.dumps({'clip':'CH0069_Formation_Pickup','glbOneBasedIndex':29,'fps':30,'frames':rows,'activeSourceTracks':tracks}),encoding='utf-8')
camera=scene.camera;camera.location=Vector((0,-3.5,.80));target=Vector((0,0,.65))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=1.65
scene.render.resolution_x=420;scene.render.resolution_y=520
for frame in (1,16,31,61,91,121,181,241,281,338):
    scene.frame_set(frame)
    for n in face:
        p=manifest['expressions']['neutral']['bone_pose'][n]
        arm.pose.bones[n].matrix_basis=Matrix.LocRotScale(Vector(p['location']),Quaternion(p['rotation']),Vector(p['scale']))
    bpy.context.view_layer.update()
    scene.render.filepath=str(OUT/f'pickup_{frame:03d}.png');bpy.ops.render.render(write_still=True)
print(json.dumps({'tracks':tracks,'samples':[{'frame':f,'pelvis':rows[f-1]['pelvisWorld'],'head':rows[f-1]['headWorld']} for f in (1,31,91,181,281,338)]}))
