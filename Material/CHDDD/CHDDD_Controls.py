"""Run this embedded text to enable still poses and complete expression transitions."""
import bpy,json,pathlib
from mathutils import Matrix

def assets(scene):
    root=pathlib.Path(bpy.data.filepath).parent
    manifest=json.loads((root/'character_manifest.json').read_text(encoding='utf-8'))
    arm=next(o for o in scene.objects if o.type=='ARMATURE')
    body=next(o for o in scene.objects if o.type=='MESH' and o.data.shape_keys and o.data.shape_keys.key_blocks.get('Pet_Blink'))
    material=next(m for m in body.data.materials if m and m.node_tree and m.node_tree.nodes.get('MouthTileOffset'))
    return manifest,arm,body,material

def clear_action(data):
    if data.animation_data:
        data.animation_data.action=None
        for track in data.animation_data.nla_tracks:track.mute=True

def mouth_weights(material):
    return {int(n.name.split('_')[-1]):n.outputs[0].default_value for n in material.node_tree.nodes if n.name.startswith('MouthWeight_')}

def set_mouth(material,tile):
    weights=mouth_weights(material)
    if weights:
        for cell in weights:material.node_tree.nodes['MouthWeight_'+str(cell)].outputs[0].default_value=1 if cell==tile else 0
    else:material.node_tree.nodes['MouthTileOffset'].inputs[1].default_value=(tile%100/8,tile//100/8,0)

def apply_expression(scene,emotion):
    scene['transition_preview_stop_frame']=0
    manifest,arm,body,material=assets(scene);setting=manifest['expressions'][emotion]
    clear_action(arm);clear_action(body.data.shape_keys);clear_action(material.node_tree)
    for name,rows in json.loads(arm['neutral_body_pose']).items():arm.pose.bones[name].matrix_basis=Matrix(rows)
    arm.animation_data.action=bpy.data.actions[setting['clip']]
    scene.frame_set(1)
    # Clear the independent eye-card test when selecting a standard expression.
    for key in body.data.shape_keys.key_blocks:key.value=0
    for material_name,node_name in [('Review16_OmegaEyeLine','Review16_EyeOpacity'),('Review16_OmegaMouth','Review16_MouthOpacity'),('CHDDD_MouthAtlas','Review16_HideOriginalMouth'),('CHDDD_Face','Review9_OriginalEyeLineMask'),('CHDDD_EyeMouth','Review9_OldEyeLineOpacity'),('Review9_SqueezeEyeLine','Review9_EyeLineOpacity')]:
        test_material=bpy.data.materials.get(material_name)
        if test_material and test_material.node_tree.nodes.get(node_name):
            clear_action(test_material.node_tree)
            test_material.node_tree.nodes[node_name].outputs[0].default_value=0
    for name,value in setting['blendShapes'].items():body.data.shape_keys.key_blocks[name].value=value
    set_mouth(material,setting['mouth_tile'])
    scene['preview_emotion']=emotion;scene['preview_transition']=''
    bpy.context.view_layer.update()

def layered_action(data,name):
    clear_action(data);data.animation_data_create()
    old=bpy.data.actions.get(name)
    if old:bpy.data.actions.remove(old)
    action=bpy.data.actions.new(name);action.use_fake_user=True;data.animation_data.action=action
    return action

def transition_expression(scene,emotion,duration=.5,from_neutral=True,preset=False):
    manifest,arm,body,material=assets(scene)
    if from_neutral:apply_expression(scene,'neutral')
    bpy.context.view_layer.update()
    names=manifest['faceBones'];keys=body.data.shape_keys
    start_pose={n:arm.pose.bones[n].matrix_basis.copy() for n in names}
    start_shapes={k.name:k.value for k in keys.key_blocks if k.name!='Basis'}
    start_mouth=mouth_weights(material)
    apply_expression(scene,emotion)
    target_pose={n:arm.pose.bones[n].matrix_basis.copy() for n in names}
    target_shapes={k.name:k.value for k in keys.key_blocks if k.name!='Basis'}
    target_mouth=mouth_weights(material)
    suffix='NeutralTo'+emotion.title() if preset else 'Preview'
    actions=[layered_action(arm,'Pet_Transition_'+suffix),layered_action(keys,'Pet_TransitionShapes_'+suffix),layered_action(material.node_tree,'Pet_TransitionMouth_'+suffix)]
    end=1+max(1,round(duration*scene.render.fps/scene.render.fps_base))
    for frame in range(1,end+1):
        t=(frame-1)/(end-1);weight=t*t*(3-2*t)
        for name in names:
            a,qa,sa=start_pose[name].decompose();b,qb,sb=target_pose[name].decompose()
            bone=arm.pose.bones[name];bone.rotation_mode='QUATERNION'
            bone.location=a.lerp(b,weight);bone.rotation_quaternion=qa.slerp(qb,weight);bone.scale=sa.lerp(sb,weight)
            for path in ['location','rotation_quaternion','scale']:bone.keyframe_insert(data_path=path,frame=frame,group=name)
        for name,target in target_shapes.items():
            key=keys.key_blocks[name];key.value=start_shapes[name]+(target-start_shapes[name])*weight;key.keyframe_insert(data_path='value',frame=frame)
        for tile,target in target_mouth.items():
            node=material.node_tree.nodes['MouthWeight_'+str(tile)];node.outputs[0].default_value=start_mouth[tile]+(target-start_mouth[tile])*weight
            node.outputs[0].keyframe_insert(data_path='default_value',frame=frame)
    for action in actions:
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for point in curve.keyframe_points:point.interpolation='LINEAR'
    scene.frame_start=1;scene.frame_end=end;scene.frame_set(1)
    scene['preview_emotion']=emotion;scene['preview_transition']=actions[0].name
    return {'emotion':emotion,'durationSeconds':(end-1)/scene.render.fps*scene.render.fps_base,'frames':[1,end],
            'boneAction':actions[0].name,'shapeAction':actions[1].name,'mouthAction':actions[2].name,'loop':False}

class CHDDD_OT_Expression(bpy.types.Operator):
    bl_idname='chddd.expression';bl_label='Apply CHDDD expression';bl_options={'REGISTER','UNDO'}
    emotion:bpy.props.StringProperty(default='neutral')
    def execute(self,context):
        if context.screen and context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
        apply_expression(context.scene,self.emotion);return {'FINISHED'}

class CHDDD_OT_Transition(bpy.types.Operator):
    bl_idname='chddd.transition';bl_label='Preview expression transition';bl_options={'REGISTER','UNDO'}
    emotion:bpy.props.StringProperty(default='sleepy');play:bpy.props.BoolProperty(default=True)
    def execute(self,context):
        if context.screen and context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
        transition_expression(context.scene,self.emotion,context.scene.chddd_transition_seconds,context.scene.chddd_from_neutral)
        if self.play and context.screen:
            context.scene['transition_preview_stop_frame']=context.scene.frame_end
            bpy.ops.screen.animation_play()
        return {'FINISHED'}

class CHDDD_PT_Expressions(bpy.types.Panel):
    bl_label='CHDDD Expressions';bl_idname='CHDDD_PT_expressions';bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='CHDDD'
    def draw(self,context):
        self.layout.prop(context.scene,'chddd_transition_seconds',text='过渡时长（秒）')
        self.layout.prop(context.scene,'chddd_from_neutral',text='从 Neutral 开始')
        self.layout.label(text='表情定格 / 过渡预览')
        for emotion,label in [('neutral','Neutral'),('happy','Happy'),('shy','Shy'),('concern','Concern'),('sad','Sad'),('surprised','Surprised'),('annoyed','Annoyed'),('sleepy','Sleepy'),('confused','Confused')]:
            row=self.layout.row(align=True);row.operator('chddd.expression',text=label).emotion=emotion
            row.operator('chddd.transition',text='播放过渡',icon='PLAY').emotion=emotion

for cls in [CHDDD_OT_Expression,CHDDD_OT_Transition,CHDDD_PT_Expressions]:
    existing=getattr(bpy.types,cls.__name__,None)
    if existing:bpy.utils.unregister_class(existing)
    bpy.utils.register_class(cls)
bpy.types.Scene.chddd_transition_seconds=bpy.props.FloatProperty(default=.5,min=.05,max=3)
bpy.types.Scene.chddd_from_neutral=bpy.props.BoolProperty(default=True)

def stop_transition_preview(scene,depsgraph=None):
    end=scene.get('transition_preview_stop_frame',0)
    if not end or scene.frame_current<end:return
    scene['transition_preview_stop_frame']=0
    def finish():
        for window in bpy.context.window_manager.windows:
            if window.scene==scene and window.screen and window.screen.is_animation_playing:
                with bpy.context.temp_override(window=window,screen=window.screen):
                    bpy.ops.screen.animation_cancel(restore_frame=False)
        scene.frame_set(end)
    bpy.app.timers.register(finish,first_interval=0)
for handler in list(bpy.app.handlers.frame_change_post):
    if handler.__name__=='stop_transition_preview':bpy.app.handlers.frame_change_post.remove(handler)
bpy.app.handlers.frame_change_post.append(stop_transition_preview)
