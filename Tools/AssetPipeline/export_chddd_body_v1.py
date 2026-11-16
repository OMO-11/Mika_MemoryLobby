import bpy,json
from pathlib import Path
OUT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
exec(compile((OUT/'CHDDD_BodyControls_v1.py').read_text(encoding='utf-8'),'body_controls','exec'))
scene=bpy.context.scene;arm=bpy.data.objects['Bip001']
preview_body(scene,'Idle',emotion='neutral',play=False)
for track in arm.animation_data.nla_tracks:
    track.mute=not track.name.startswith('PetV1_')
    if track.name.startswith('PetV1_Face_'): track.strips[0].repeat=1
visibility=[]
bpy.ops.object.select_all(action='DESELECT')
for obj in scene.objects:
    allowed=obj.type in {'ARMATURE','EMPTY'} or obj.name in {'CH0069_Body','CHDDD_Halo_Restored'}
    if not allowed: continue
    visibility.append((obj,obj.hide_get(),obj.hide_viewport))
    obj.hide_set(False);obj.hide_viewport=False;obj.select_set(True)
bpy.context.view_layer.objects.active=arm
try:
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Unity/CHDDD_Actions_v1.fbx'),use_selection=True,
        object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=True,
        bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,
        axis_forward='-Z',axis_up='Y',path_mode='RELATIVE')
finally:
    for obj,hidden,viewport in visibility: obj.hide_set(hidden);obj.hide_viewport=viewport
    preview_body(scene,'Idle',emotion='neutral',play=False)
scene['body_v1_preview_motion']=False;scene['body_v1_preview_batch']=''
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CHDDD_actions_v1.blend'))
print(json.dumps({'model':str(OUT/'Unity/CHDDD_Actions_v1.fbx'),'bodyClips':18,'faceClips':9,'weaponMeshesExported':False}))
