import bpy,json
from pathlib import Path
from mathutils import Vector

OUT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
scene=bpy.context.scene; arm=bpy.data.objects['Bip001']
manifest=json.loads((OUT/'character_manifest_before_actions_v1.json').read_text(encoding='utf-8'))
for tr in list(arm.animation_data.nla_tracks):
    if tr.name.startswith('PetV1_Face_'): arm.animation_data.nla_tracks.remove(tr)
for emotion,setting in manifest['expressions'].items():
    name='PetV1_Face_'+emotion.title()
    old=bpy.data.actions.get(name)
    if old: bpy.data.actions.remove(old)
    action=bpy.data.actions[setting['clip']].copy(); action.name=name; action.use_fake_user=True
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in list(bag.fcurves):
                    if 'Bip001 Head' in curve.data_path: bag.fcurves.remove(curve)
    track=arm.animation_data.nla_tracks.new(); track.name=name
    track.strips.new(name,1,action); track.mute=True

for name in ('CHDDD_Controls.py','CHDDD_BodyControls_v1.py'):
    source=(OUT/name).read_text(encoding='utf-8')
    text=bpy.data.texts.get(name) or bpy.data.texts.new(name)
    text.clear();text.write(source)
    exec(compile(source,name,'exec'))
preview_body(scene,'Idle',play=False)
root=bpy.data.objects['HaloRoot']; bpy.context.view_layer.update()
if not root.get('body_v1_height_adjustment'):
    original=root.matrix_basis.copy()
    world=root.matrix_world.copy(); world.translation+=Vector((0,0,.065))
    root.matrix_world=world; bpy.context.view_layer.update()
    root['body_v1_height_adjustment']=.065
    root['body_v1_previous_basis']=json.dumps([list(r) for r in original])
manifest['bodyAnimationV1']={
    'status':'first-draft','blenderFile':'CHDDD_actions_v1.blend','model':'Unity/CHDDD_Actions_v1.fbx',
    'mapping':'Unity/body_action_map_v1.json','weaponVisible':False,
    'bodyClipCount':18,'loopClipCount':7,'feedbackClipCount':5,'transitionClipCount':6,
    'faceLayer':'Eyes/brows/mouth only; Head and Neck belong to body animation',
    'haloHeightAdjustmentModelUnits':.065,
    'projectIntegration':'Not yet integrated into GiftCompanion runtime'
}
(OUT/'character_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
face_map={'expressions':[]}
old_map=json.loads((OUT/'Unity/expression_map.json').read_text(encoding='utf-8'))
for entry in old_map['expressions']:
    copy=dict(entry);copy['clip']='PetV1_Face_'+entry['name'].title();face_map['expressions'].append(copy)
(OUT/'Unity/expression_map_v1.json').write_text(json.dumps(face_map,indent=2),encoding='utf-8')
scene['body_v1_batch']='';scene['preview_emotion']='neutral'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CHDDD_actions_v1.blend'))
print(json.dumps({'bodyActions':18,'facialCopies':len(face_map['expressions']),'haloRaise':.065,'saved':bpy.data.filepath}))
