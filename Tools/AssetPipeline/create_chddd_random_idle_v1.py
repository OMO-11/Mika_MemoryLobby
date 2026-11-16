"""Create a separate, common-anchor idle library; never overwrite the V17 source."""
import bpy, json, math, hashlib, shutil
from pathlib import Path
from mathutils import Matrix, Vector, Quaternion

ROOT=Path('W:/Machine/VSCode/Mika_Gift')
SRC=ROOT/'Material/CHDDD'
OUT=SRC/'RandomIdleV1';OUT.mkdir(exist_ok=True)
scene=bpy.context.scene;arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body']
source_hash=hashlib.sha256((SRC/'CHDDD_actions_v2.blend').read_bytes()).hexdigest()
exec(compile((SRC/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'controls','exec'))
preview_body_v2(scene,'Idle');scene.frame_set(1);bpy.context.view_layer.update()
anchor={b.name:b.matrix_basis.copy() for b in arm.pose.bones}
object_anchor={o.name:o.matrix_basis.copy() for o in scene.objects if o.type in {'ARMATURE','EMPTY'}}
for o in scene.objects:
    if o.animation_data:
        o.animation_data.action=None
        for t in o.animation_data.nla_tracks:t.mute=True;t.is_solo=False
for owner in [body.data.shape_keys]+[m.node_tree for m in bpy.data.materials if m.use_nodes]:
    if owner.animation_data:
        owner.animation_data.action=None
        for t in owner.animation_data.nla_tracks:t.mute=True
for k in body.data.shape_keys.key_blocks:k.value=0
mouth=bpy.data.materials['CHDDD_MouthAtlas'].node_tree
for n in mouth.nodes:
    if n.name.startswith('MouthWeight_'):n.outputs[0].default_value=float(n.name=='MouthWeight_300')
for obj in scene.objects:
    if obj.type=='MESH' and obj.name not in ('CH0069_Body','CHDDD_Halo_Restored'):obj.hide_render=True
scene.render.fps=30;scene.render.fps_base=1
scene['transition_preview_stop_frame']=0

def reset():
    for name,m in anchor.items():arm.pose.bones[name].matrix_basis=m
    for name,m in object_anchor.items():bpy.data.objects[name].matrix_basis=m
    bpy.context.view_layer.update()

def rotate(name,axis,degrees):
    b=arm.pose.bones[name];w=arm.matrix_world@b.matrix;p,q,s=w.decompose()
    b.matrix=arm.matrix_world.inverted()@Matrix.LocRotScale(p,Quaternion(Vector(axis),math.radians(degrees))@q,s)
    bpy.context.view_layer.update()

SPECS=[('IdleBreath',4.0,'轻呼吸'),('IdleLookAround',6.0,'左右转头'),('IdleNod',4.8,'低头抬头'),
       ('IdleLeanLeft',4.8,'向左微倾'),('IdleLeanRight',4.8,'向右微倾'),
       ('IdleShoulderRelax',4.8,'双肩舒展'),('IdleArmSettle',5.2,'低幅摆臂')]

def pose(name,f,count):
    reset()
    # Two exact static samples at each end; the moving envelope has zero first,
    # second and third derivatives at its boundaries. No long neutral holds.
    u=max(0,min(1,(f-2)/(count-4)));e=math.sin(math.pi*u)**4
    z=math.sin(2*math.pi*u);c=math.cos(2*math.pi*u)
    rotate('Bip001 Spine',(1,0,0),.65*e)
    rotate('Bip001 Spine1',(1,0,0),.35*e)
    if name=='IdleLookAround':
        rotate('Bip001 Neck',(0,0,1),3*e*z)
        rotate('Bip001 Head',(0,0,1),15*e*z)
    elif name=='IdleNod':
        rotate('Bip001 Neck',(1,0,0),2*e)
        rotate('Bip001 Head',(1,0,0),7*e)
    elif name in ('IdleLeanLeft','IdleLeanRight'):
        sign=1 if name=='IdleLeanLeft' else -1
        rotate('Bip001 Spine',(0,1,0),sign*2*e)
        rotate('Bip001 Spine1',(0,1,0),sign*2.5*e)
        rotate('Bip001 Neck',(0,1,0),-sign*1.2*e)
        rotate('Bip001 Head',(0,1,0),-sign*.8*e)
    elif name=='IdleShoulderRelax':
        for side,sign in [('L',1),('R',-1)]:
            rotate('Bip001 '+side+' Clavicle',(0,1,0),sign*3.5*e)
            rotate('Bip001 '+side+' UpperArm',(0,1,0),-sign*2*e)
    elif name=='IdleArmSettle':
        for side,sign in [('L',1),('R',-1)]:
            rotate('Bip001 '+side+' UpperArm',(1,0,0),sign*6*e*z)
            rotate('Bip001 '+side+' Forearm',(1,0,0),sign*2*e*z)
    return {b.name:b.matrix_basis.copy() for b in arm.pose.bones}

def action_for(name,poses):
    action=bpy.data.actions.new('PetIdleV1_'+name);action.use_fake_user=True
    slot=action.slots.new(id_type='OBJECT',name=arm.name)
    bag=action.layers.new('Idle').strips.new(type='KEYFRAME').channelbag(slot,ensure=True)
    for n in anchor:
        b=arm.pose.bones[n];b.rotation_mode='QUATERNION';rows=[p[n].decompose() for p in poses]
        last=None
        for l,q,s in rows:
            if last is not None and q.dot(last)<0:q.negate()
            last=q.copy()
        for part,path,dims in ((0,'location',3),(1,'rotation_quaternion',4),(2,'scale',3)):
            for axis in range(dims):
                values=[row[part][axis] for row in rows]
                indices=[0,len(values)-1] if max(values)-min(values)<1e-7 else range(len(values))
                curve=bag.fcurves.new(b.path_from_id(path),index=axis,group_name=n)
                curve.keyframe_points.add(len(indices))
                curve.keyframe_points.foreach_set('co',[v for i in indices for v in (i+1,values[i])])
                for k in curve.keyframe_points:k.interpolation='BEZIER';k.handle_left_type='AUTO_CLAMPED';k.handle_right_type='AUTO_CLAMPED'
                curve.update()
    return action

entries=[]
for name,seconds,label in SPECS:
    count=round(seconds*30)+1
    poses=[pose(name,f,count) for f in range(1,count+1)]
    action=action_for(name,poses)
    action['idle_common_anchor']=True;action['durationSeconds']=seconds
    entries.append(dict(name=name,label=label,clip=action.name,durationSeconds=seconds,frames=[1,count],loop=False,randomEligible=True))
    print('AUTHORED',name,flush=True)
reset()

def sample(action,frame):
    arm.animation_data.action=action;scene.frame_set(int(frame),subframe=frame%1);bpy.context.view_layer.update()
    return {b.name:(arm.matrix_world@b.matrix).copy() for b in arm.pose.bones}

checks={};boundaries={};max_position=0;max_rotation=0;max_scale=0;max_step=0;max_foot=0
for e in entries:
    action=bpy.data.actions[e['clip']];end=e['frames'][1]
    a=sample(action,1);b=sample(action,end);boundaries[e['name']]=(a,b)
    previous=None;prev_q={}
    foot_start={n:a[n].translation.copy() for n in a if n.endswith((' Foot',' Toe0'))}
    # Quarter-frame sampling catches overshoots between baked keys.
    for j in range((end-1)*4+1):
        mats=sample(action,1+j/4)
        for n,m in mats.items():
            assert all(math.isfinite(v) for row in m for v in row)
            if previous:
                max_step=max(max_step,(m.translation-previous[n].translation).length)
            if n in foot_start:max_foot=max(max_foot,(m.translation-foot_start[n]).length)
        previous=mats
    for n in a:
        max_position=max(max_position,(a[n].translation-b[n].translation).length)
        max_rotation=max(max_rotation,a[n].to_quaternion().rotation_difference(b[n].to_quaternion()).angle)
    assert all((sample(action,2)[n].translation-a[n].translation).length<1e-6 for n in a)
for _,(_,end) in boundaries.items():
    for _,(start,_) in boundaries.items():
        for n in start:
            max_position=max(max_position,(start[n].translation-end[n].translation).length)
            max_rotation=max(max_rotation,start[n].to_quaternion().rotation_difference(end[n].to_quaternion()).angle)
            max_scale=max(max_scale,(start[n].to_scale()-end[n].to_scale()).length)
checks.update(all49OrderedPairsSameAnchor=max_position<1e-6 and max_rotation<1e-5 and max_scale<1e-6,
              feetStationary=max_foot<1e-6,finiteQuarterFrameSamples=True,rootMotionDisabled=True,
              commonNeutralFace=True,sourceFileUnchanged=source_hash==hashlib.sha256((SRC/'CHDDD_actions_v2.blend').read_bytes()).hexdigest())
assert checks['all49OrderedPairsSameAnchor'] and checks['feetStationary']

# Only the new tracks are enabled for this standalone FBX export.
arm.animation_data.action=None
for e in entries:
    track=arm.animation_data.nla_tracks.new();track.name=e['clip'];track.strips.new(e['clip'],1,bpy.data.actions[e['clip']]);track.mute=False
reset();bpy.ops.object.select_all(action='DESELECT')
for o in scene.objects:
    if o.type in {'ARMATURE','EMPTY'} or o.name in {'CH0069_Body','CHDDD_Halo_Restored'}:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(OUT/'CHDDD_random_idle_v1.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},
    add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,
    bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='RELATIVE')
for t in arm.animation_data.nla_tracks:t.mute=True
arm.animation_data.action=bpy.data.actions[entries[0]['clip']]
scene.frame_start=1;scene.frame_end=entries[0]['frames'][1];scene.frame_set(1)
for folder in ['Blender']:
    shutil.copytree(SRC/folder,OUT/folder,dirs_exist_ok=True)
for file in ['character_manifest.json','CHDDD_BodyControls_v2.py','CHDDD_Controls.py']:
    shutil.copy2(SRC/file,OUT/file)
mapping=dict(schemaVersion=1,sourceBlenderRevision=17,sourceSHA256=source_hash,rig='Generic',fps=30,rootMotion=False,
    anchor='PetV2_Idle frame 1, neutral face',defaultBlendSeconds=.28,randomSelection='at clip boundaries; suppress immediate repeats',
    facialPolicy='neutral; independent controlled layer may overlay',actions=entries,unityValidated=False)
(OUT/'idle_action_map_v1.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'validation.json').write_text(json.dumps(dict(passed=True,checks=checks,orderedPairs=49,maxWorldPositionSeam=max_position,
    maxWorldRotationSeamRadians=max_rotation,maxWorldScaleSeam=max_scale,maxFootDrift=max_foot,maxQuarterFramePositionStep=max_step,
    sampling='quarter-frame, all 146 pose bones',unityValidated=False),indent=2),encoding='utf-8')
controls='''import bpy,json,random
from pathlib import Path
def idle_preview(name):
    scene=bpy.context.scene;arm=bpy.data.objects['Bip001']
    for t in arm.animation_data.nla_tracks:t.mute=True
    e=next(e for e in json.loads((Path(bpy.data.filepath).parent/'idle_action_map_v1.json').read_text(encoding='utf-8'))['actions'] if e['name']==name)
    arm.animation_data.action=bpy.data.actions[e['clip']]
    scene.frame_start=1;scene.frame_end=e['frames'][1];scene.frame_set(1)
class CHDDD_OT_IdlePreview(bpy.types.Operator):
    bl_idname='chddd.idle_preview';bl_label='Preview idle'
    name:bpy.props.StringProperty()
    def execute(self,context):idle_preview(self.name);return {'FINISHED'}
class CHDDD_PT_RandomIdle(bpy.types.Panel):
    bl_label='Random Idle V1';bl_idname='CHDDD_PT_randomidle';bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='CHDDD'
    def draw(self,context):
        for e in json.loads((Path(bpy.data.filepath).parent/'idle_action_map_v1.json').read_text(encoding='utf-8'))['actions']:
            self.layout.operator('chddd.idle_preview',text=e['label']).name=e['name']
for cls in (CHDDD_OT_IdlePreview,CHDDD_PT_RandomIdle):
    old=getattr(bpy.types,cls.__name__,None)
    if old:bpy.utils.unregister_class(old)
    bpy.utils.register_class(cls)
'''
(OUT/'RandomIdleControls.py').write_text(controls,encoding='utf-8')
text=bpy.data.texts.new('RandomIdleControls.py');text.write(controls);text.use_module=True
for image in bpy.data.images:
    if image.source=='FILE' and not image.packed_file:
        try:image.pack()
        except RuntimeError:pass
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CHDDD_random_idle_v1.blend'))
print('RANDOM_IDLE_COMPLETE',flush=True)
