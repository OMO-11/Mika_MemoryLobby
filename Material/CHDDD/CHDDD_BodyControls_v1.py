"""Run this embedded text to preview body clips with the accepted face presets."""
import bpy, json
from pathlib import Path
from mathutils import Matrix

ROOT = Path(bpy.data.filepath).parent

def preview_body(scene, name, emotion='', play=False):
    root=Path(bpy.data.filepath).parent
    mapping=json.loads((root/'Unity/body_action_map_v1.json').read_text(encoding='utf-8'))
    entry=next(e for e in mapping['actions'] if e['name']==name)
    manifest=json.loads((root/'character_manifest.json').read_text(encoding='utf-8'))
    emotion=emotion or entry['emotion']; setting=manifest['expressions'][emotion]
    arm=bpy.data.objects['Bip001']; body=bpy.data.objects['CH0069_Body']
    scene['transition_preview_stop_frame']=0
    for o in scene.objects:
        if o.animation_data:
            o.animation_data.action=None
            for t in o.animation_data.nla_tracks: t.mute=True; t.is_solo=False
    for n,rows in json.loads(arm['neutral_body_pose']).items(): arm.pose.bones[n].matrix_basis=Matrix(rows)
    face_track=next(t for t in arm.animation_data.nla_tracks if t.name=='PetV1_Face_'+emotion.title())
    body_track=next(t for t in arm.animation_data.nla_tracks if t.name==entry['clip'])
    body_track.mute=False; face_track.mute=False
    # Facial NLA strip repeats to cover long body previews, while body clip remains one cycle.
    face_strip=face_track.strips[0]; face_strip.repeat=3
    keys=body.data.shape_keys
    if keys.animation_data: keys.animation_data.action=None
    for key in keys.key_blocks:
        if key.name!='Basis': key.value=setting['blendShapes'].get(key.name,0)
    mouth=bpy.data.materials['CHDDD_MouthAtlas'].node_tree
    if mouth.animation_data: mouth.animation_data.action=None
    for n in mouth.nodes:
        if n.name.startswith('MouthWeight_'):
            n.outputs[0].default_value=1 if int(n.name.split('_')[-1])==setting['mouth_tile'] else 0
    for o in scene.objects:
        if o.type=='MESH' and o.name.startswith('CH0069_Weapon'): o.hide_render=True; o.hide_set(True)
    scene.frame_start=entry['frames'][0]; scene.frame_end=entry['frames'][1]; scene.frame_set(scene.frame_start)
    scene['preview_emotion']=emotion; scene['body_v1_action']=name
    if play and bpy.context.screen:
        if not entry['loop']: scene['transition_preview_stop_frame']=scene.frame_end
        bpy.ops.screen.animation_play()
    bpy.context.view_layer.update()
    return entry

class CHDDD_OT_BodyV1(bpy.types.Operator):
    bl_idname='chddd.body_v1'; bl_label='Preview body animation'; bl_options={'REGISTER','UNDO'}
    action_name:bpy.props.StringProperty(default='Idle')
    play:bpy.props.BoolProperty(default=True)
    def execute(self, context):
        if context.screen and context.screen.is_animation_playing: bpy.ops.screen.animation_cancel(restore_frame=False)
        preview_body(context.scene,self.action_name,context.scene.chddd_body_emotion,self.play)
        return {'FINISHED'}

class CHDDD_PT_BodyV1(bpy.types.Panel):
    bl_label='CHDDD Body Actions V1'; bl_idname='CHDDD_PT_body_v1'
    bl_space_type='VIEW_3D'; bl_region_type='UI'; bl_category='CHDDD'
    def draw(self,context):
        layout=self.layout; layout.prop(context.scene,'chddd_body_emotion',text='配套表情')
        layout.label(text='无武器 · 第一版身体动作')
        mapping=json.loads((Path(bpy.data.filepath).parent/'Unity/body_action_map_v1.json').read_text(encoding='utf-8'))
        for entry in mapping['actions']:
            row=layout.row(align=True)
            button=row.operator('chddd.body_v1',text=entry['name'],icon='FILE_REFRESH' if entry['loop'] else 'PLAY')
            button.action_name=entry['name']; button.play=True
            button=row.operator('chddd.body_v1',text='定格'); button.action_name=entry['name']; button.play=False

for cls in (CHDDD_OT_BodyV1,CHDDD_PT_BodyV1):
    old=getattr(bpy.types,cls.__name__,None)
    if old: bpy.utils.unregister_class(old)
    bpy.utils.register_class(cls)
bpy.types.Scene.chddd_body_emotion=bpy.props.EnumProperty(items=[('','跟随动作','')]+[(n,n,'') for n in ('neutral','happy','shy','concern','sad','surprised','annoyed','sleepy','confused')],default='')
