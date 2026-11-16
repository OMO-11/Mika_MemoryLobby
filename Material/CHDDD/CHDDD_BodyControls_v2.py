"""Action performances: body, eye/brow geometry and mouth timing share one clock."""
import bpy,json
from pathlib import Path
from mathutils import Matrix

def preview_body_v2(scene,name,play=False):
    root=Path(bpy.data.filepath).parent
    mapping=json.loads((root/'Blender/body_action_map_v2.json').read_text(encoding='utf-8'))
    entry=next(e for e in mapping['actions'] if e['name']==name)
    arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body']
    scene['transition_preview_stop_frame']=0
    for material_name,node_name in [('Review16_OmegaEyeLine','Review16_EyeOpacity'),('Review16_OmegaMouth','Review16_MouthOpacity'),('CHDDD_MouthAtlas','Review16_HideOriginalMouth'),('CHDDD_Face','Review9_OriginalEyeLineMask'),('CHDDD_EyeMouth','Review9_OldEyeLineOpacity'),('Review9_SqueezeEyeLine','Review9_EyeLineOpacity')]:
        material=bpy.data.materials.get(material_name)
        if material and material.node_tree.nodes.get(node_name):
            nt=material.node_tree
            if nt.animation_data:nt.animation_data.action=None
            nt.nodes[node_name].outputs[0].default_value=0
    for o in scene.objects:
        if o.animation_data:
            o.animation_data.action=None
            for track in o.animation_data.nla_tracks:track.mute=True;track.is_solo=False
    for n,rows in json.loads(arm['neutral_body_pose']).items():arm.pose.bones[n].matrix_basis=Matrix(rows)
    for clip in (entry['clip'],entry['faceClip']):
        track=next(t for t in arm.animation_data.nla_tracks if t.name==clip)
        track.mute=False;track.strips[0].repeat=1
    keys=body.data.shape_keys
    for key in keys.key_blocks:key.value=0
    keys.animation_data_create();keys.animation_data.action=bpy.data.actions[entry['shapeAction']]
    mouth=bpy.data.materials['CHDDD_MouthAtlas'].node_tree;mouth.animation_data_create();mouth.animation_data.action=bpy.data.actions[entry['mouthAction']]
    for o in scene.objects:
        if o.type=='MESH' and o.name.startswith('CH0069_Weapon'):o.hide_render=True;o.hide_set(True)
    scene.frame_start=1;scene.frame_end=entry['frames'][1];scene.frame_set(1)
    scene['body_v2_action']=name
    if play and bpy.context.screen:
        if not entry['loop']:scene['transition_preview_stop_frame']=scene.frame_end
        bpy.ops.screen.animation_play()
    bpy.context.view_layer.update()
    return entry

class CHDDD_OT_BodyV2(bpy.types.Operator):
    bl_idname='chddd.body_v2';bl_label='Preview V2 performance';bl_options={'REGISTER','UNDO'}
    action_name:bpy.props.StringProperty(default='Idle')
    play:bpy.props.BoolProperty(default=True)
    def execute(self,context):
        if context.screen and context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
        preview_body_v2(context.scene,self.action_name,self.play);return {'FINISHED'}

class CHDDD_PT_BodyV2(bpy.types.Panel):
    bl_label='CHDDD Performances V2';bl_idname='CHDDD_PT_body_v2'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='CHDDD'
    def draw(self,context):
        self.layout.label(text='表情实验 V17 · -ω- 与已保留 >o<')
        self.layout.operator('chddd.review9_expression',text='>o< 已保留 V15',icon='PLAY')
        self.layout.operator('chddd.review16_omega',text='-ω- 加粗版测试',icon='PLAY')
        mapping=json.loads((Path(bpy.data.filepath).parent/'Blender/body_action_map_v2.json').read_text(encoding='utf-8'))
        for e in mapping['actions']:
            row=self.layout.row(align=True)
            b=row.operator('chddd.body_v2',text=e['name'],icon='PLAY');b.action_name=e['name'];b.play=True
            b=row.operator('chddd.body_v2',text='定格');b.action_name=e['name'];b.play=False

for cls in (CHDDD_OT_BodyV2,CHDDD_PT_BodyV2):
    old=getattr(bpy.types,cls.__name__,None)
    if old:bpy.utils.unregister_class(old)
    bpy.utils.register_class(cls)

def preview_review9_expression(scene,play=False):
    preview_body_v2(scene,'Idle')
    body=bpy.data.objects['CH0069_Body']
    body.data.shape_keys.animation_data.action=bpy.data.actions['Review9_TestSqueeze_Shapes']
    for material_name,action_name in [('CHDDD_MouthAtlas','Review9_TestSqueeze_Mouth'),('CHDDD_Face','Review9_TestSqueeze_FaceMaterial'),('CHDDD_EyeMouth','Review9_TestSqueeze_OldLashMaterial'),('Review9_SqueezeEyeLine','Review9_TestSqueeze_EyeLineMaterial')]:
        nt=bpy.data.materials[material_name].node_tree;nt.animation_data_create();nt.animation_data.action=bpy.data.actions[action_name]
    scene.frame_start=1;scene.frame_end=73;scene.frame_set(1);scene['body_v2_action']='TestSqueeze'
    if play and bpy.context.screen:
        scene['transition_preview_stop_frame']=73;bpy.ops.screen.animation_play()
    return {'name':'TestSqueeze','durationSeconds':2.4,'frames':[1,73],'loop':False}
class CHDDD_OT_Review9Expression(bpy.types.Operator):
    bl_idname='chddd.review9_expression';bl_label='Test >o< flat image cards'
    def execute(self,context):
        if context.screen and context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
        preview_review9_expression(context.scene,True);return {'FINISHED'}
old=getattr(bpy.types,CHDDD_OT_Review9Expression.__name__,None)
if old:bpy.utils.unregister_class(old)
bpy.utils.register_class(CHDDD_OT_Review9Expression)

def preview_review16_omega(scene,play=False):
    preview_body_v2(scene,'Idle')
    bpy.data.objects['CH0069_Body'].data.shape_keys.animation_data.action=bpy.data.actions['Review16_TestOmega_Shapes']
    for material_name,action_name in [('Review16_OmegaEyeLine','Review16_TestOmega_Eyes'),('Review16_OmegaMouth','Review16_TestOmega_MouthLine'),('CHDDD_Face','Review16_TestOmega_Face'),('CHDDD_EyeMouth','Review16_TestOmega_OldLashes'),('CHDDD_MouthAtlas','Review16_TestOmega_MouthAtlas')]:
        tree=bpy.data.materials[material_name].node_tree;tree.animation_data_create();tree.animation_data.action=bpy.data.actions[action_name]
    scene.frame_start=1;scene.frame_end=73;scene.frame_set(1);scene['body_v2_action']='TestOmega'
    if play and bpy.context.screen:
        scene['transition_preview_stop_frame']=73;bpy.ops.screen.animation_play()
    return {'name':'TestOmega','durationSeconds':2.4,'frames':[1,73],'loop':False}
class CHDDD_OT_Review16Omega(bpy.types.Operator):
    bl_idname='chddd.review16_omega';bl_label='Test -omega- expression'
    def execute(self,context):
        if context.screen and context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
        preview_review16_omega(context.scene,True);return {'FINISHED'}
old=getattr(bpy.types,CHDDD_OT_Review16Omega.__name__,None)
if old:bpy.utils.unregister_class(old)
bpy.utils.register_class(CHDDD_OT_Review16Omega)
