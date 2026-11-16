import bpy,json,pathlib
from mathutils import Matrix
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
scene=bpy.context.scene
arm=next(o for o in scene.objects if o.type=='ARMATURE')
body=next(o for o in scene.objects if o.type=='MESH' and o.name.startswith('CH0069_Body'))
manifest=json.loads((OUT/'character_manifest.json').read_text())
halo_root=next(o for o in scene.objects if o.name=='HaloRoot')
halo_local=halo_root.matrix_basis.copy()
arm.animation_data.action=None
for track in list(arm.animation_data.nla_tracks):
    if track.name.startswith(('Pet_Emotion_','Pet_Transition_')):
        arm.animation_data.nla_tracks.remove(track)
for setting in manifest['expressions'].values():
    action=bpy.data.actions[setting['clip']]
    track=arm.animation_data.nla_tracks.new();track.name=action.name
    track.strips.new(action.name,1,action)
for preset in manifest.get('transitions',{}).get('presets',[]):
    action=bpy.data.actions[preset['boneAction']]
    track=arm.animation_data.nla_tracks.new();track.name=action.name
    track.strips.new(action.name,1,action)
for track in arm.animation_data.nla_tracks:track.mute=False
for key in body.data.shape_keys.key_blocks:
    key.value=0
if body.data.shape_keys.animation_data:body.data.shape_keys.animation_data.action=None
mouth=bpy.data.materials['CHDDD_MouthAtlas']
if mouth.node_tree.animation_data:mouth.node_tree.animation_data.action=None
for node in mouth.node_tree.nodes:
    if node.name.startswith('MouthWeight_'):node.outputs[0].default_value=1 if node.name=='MouthWeight_300' else 0
bpy.ops.object.select_all(action='DESELECT')
export_visibility={}
for obj in scene.objects:
    if obj.type in ['ARMATURE','EMPTY'] or (obj.type=='MESH' and (obj.name.startswith('CH0069_') or obj.name=='CHDDD_Halo_Restored')):
        export_visibility[obj.name]=(obj.hide_get(),obj.hide_viewport)
        obj.hide_set(False);obj.hide_viewport=False
        obj.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(OUT/'Unity/CHDDD.fbx'),use_selection=True,
                        object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,
                        bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,
                        bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='RELATIVE')
for name,(hidden,viewport_hidden) in export_visibility.items():
    obj=scene.objects[name];obj.hide_set(hidden);obj.hide_viewport=viewport_hidden
for track in arm.animation_data.nla_tracks:track.mute=True
if arm.get('neutral_body_pose'):
    for name,rows in json.loads(arm['neutral_body_pose']).items():
        arm.pose.bones[name].matrix_basis=Matrix(rows)
halo_root.matrix_basis=halo_local
arm.animation_data.action=bpy.data.actions['Pet_Emotion_Neutral']
scene.frame_set(1)
bpy.data.materials['CHDDD_MouthAtlas'].node_tree.nodes['MouthTileOffset'].inputs[1].default_value=(0,3/8,0)
scene['preview_emotion']='neutral'
scene.render.filepath=str(OUT/'Previews/neutral.png')
scene.render.resolution_x=800;scene.render.resolution_y=900
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CHDDD.blend'))
entries=[{'name':n,'clip':s['clip'],'mouthTile':s['mouth_tile'],
          'blink':s['blendShapes']['Pet_Blink'],'eyeWide':s['blendShapes']['Pet_EyeWide'],
          'browSad':s['blendShapes'].get('Pet_BrowSad',0),
          'browAnnoyed':s['blendShapes'].get('Pet_BrowAnnoyed',0),
          'browSleepy':s['blendShapes'].get('Pet_BrowSleepy',0)} for n,s in manifest['expressions'].items()]
(OUT/'Unity/expression_map.json').write_text(json.dumps({'expressions':entries},ensure_ascii=False,indent=2))
print(json.dumps({'fbx':str(OUT/'Unity/CHDDD.fbx'),'animation_tracks':len(arm.animation_data.nla_tracks),'shapes':list(body.data.shape_keys.key_blocks.keys()),'blend':str(OUT/'CHDDD.blend')}))
