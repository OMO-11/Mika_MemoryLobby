import bpy,json
from pathlib import Path
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
scene=bpy.context.scene;arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body']
mapping_path=ROOT/'Unity/body_action_map_v2.json'
mapping=json.loads(mapping_path.read_text(encoding='utf-8'))
mapping_path.write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
for name in ('CHDDD_Controls.py','CHDDD_BodyControls_v2.py'):
    source=(ROOT/name).read_text(encoding='utf-8')
    text=bpy.data.texts.get(name) or bpy.data.texts.new(name);text.clear();text.write(source)
    exec(compile(source,name,'exec'))
preview_body_v2(scene,'Idle')
body.data.shape_keys.animation_data.action=None
for key in body.data.shape_keys.key_blocks:key.value=0
mouth=bpy.data.materials['CHDDD_MouthAtlas'].node_tree
mouth.animation_data.action=None
for tile in (300,706,705,604,605,305,401,200):mouth.nodes['MouthWeight_'+str(tile)].outputs[0].default_value=1 if tile==300 else 0
for track in arm.animation_data.nla_tracks:
    track.mute=not track.name.startswith('PetV2_');track.is_solo=False
    if track.name.startswith('PetV2_'):track.strips[0].repeat=1
bpy.ops.object.select_all(action='DESELECT');visibility=[]
for obj in scene.objects:
    if obj.type not in {'ARMATURE','EMPTY'} and obj.name not in {'CH0069_Body','CHDDD_Halo_Restored'}:continue
    visibility.append((obj,obj.hide_get(),obj.hide_viewport));obj.hide_set(False);obj.hide_viewport=False;obj.select_set(True)
bpy.context.view_layer.objects.active=arm
try:
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'Unity/CHDDD_Actions_v2.fbx'),use_selection=True,
        object_types={'ARMATURE','MESH','EMPTY'},add_leaf_bones=False,bake_anim=True,
        bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,
        axis_forward='-Z',axis_up='Y',path_mode='RELATIVE')
finally:
    for obj,hidden,viewport in visibility:obj.hide_set(hidden);obj.hide_viewport=viewport
preview_body_v2(scene,'Idle');scene['body_v2_revision']=mapping['revision']
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
print(json.dumps({'bodyClips':18,'actionFaceClips':18,'optionalFaceTemplates':9,'weaponMeshesExported':False,'saved':bpy.data.filepath}))
