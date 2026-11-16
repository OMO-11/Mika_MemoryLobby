"""Author the approved first body-animation draft, preserving facial assets."""
import bpy, json, math, hashlib
from pathlib import Path
from mathutils import Matrix, Vector, Quaternion

OUT = Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
scene = bpy.context.scene
arm = bpy.data.objects['Bip001']
body = bpy.data.objects['CH0069_Body']
manifest = json.loads((OUT/'character_manifest_before_actions_v1.json').read_text(encoding='utf-8'))
face_names = [n for n in manifest['faceBones'] if n != 'Bip001 Head']
body_names = [b.name for b in arm.pose.bones if b.name not in face_names]
neutral = {n: Matrix(rows) for n, rows in json.loads(arm['neutral_body_pose']).items()}
scene.render.fps = 30
scene.render.fps_base = 1
scene['transition_preview_stop_frame'] = 0
if bpy.context.screen and bpy.context.screen.is_animation_playing:
    bpy.ops.screen.animation_cancel(restore_frame=False)

SPECS = {
    'Idle': (4, True, 'neutral'), 'Walk': (40/30, True, 'neutral'),
    'Thinking': (3, True, 'concern'), 'Working': (2.4, True, 'neutral'),
    'HeadPat': (3, True, 'happy'), 'Dragging': (1.6, True, 'surprised'),
    'Sleep': (5, True, 'sleepy'), 'Wave': (2, False, 'happy'),
    'Success': (1.8, False, 'happy'), 'Confused': (2, False, 'confused'),
    'HeadClick': (.8, False, 'surprised'), 'BodyClick': (.8, False, 'shy'),
    'HeadPat_Enter': (.6, False, 'happy'), 'HeadPat_Exit': (.6, False, 'neutral'),
    'Dragging_Enter': (.4, False, 'surprised'), 'Dragging_Exit': (.6, False, 'neutral'),
    'Sleep_Enter': (1.2, False, 'sleepy'), 'Sleep_Exit': (1.2, False, 'neutral'),
}

def disable_animation():
    for o in scene.objects:
        if o.animation_data:
            o.animation_data.action = None
            for t in o.animation_data.nla_tracks: t.mute = True; t.is_solo = False
    for owner in (body.data.shape_keys, bpy.data.materials['CHDDD_MouthAtlas'].node_tree):
        if owner.animation_data:
            owner.animation_data.action = None
            for t in owner.animation_data.nla_tracks: t.mute = True

disable_animation()
cache_path = OUT/'body_source_cache_v1.json'
if not cache_path.exists():
    cached = {}
    object_basis = {o.name: [list(row) for row in o.matrix_basis] for o in scene.objects if o.type in {'ARMATURE','EMPTY'}}
    for key, frames in [('Idle',121),('Walk',41)]:
        track = next(t for t in arm.animation_data.nla_tracks if t.name.startswith('CH0069_Cafe_'+key+'--'))
        track.mute = False
        rows = []
        for f in range(1, frames+1):
            for n, m in neutral.items(): arm.pose.bones[n].matrix_basis = m
            scene.frame_set(f); bpy.context.view_layer.update()
            rows.append({n: [list(row) for row in arm.pose.bones[n].matrix_basis] for n in body_names})
        track.mute = True
        cached[key] = rows
    for name, rows in object_basis.items(): scene.objects[name].matrix_basis = Matrix(rows)
    cached['objects'] = object_basis
    cache_path.write_text(json.dumps(cached), encoding='utf-8')
cache = json.loads(cache_path.read_text(encoding='utf-8'))
for name, rows in cache['objects'].items():
    if scene.objects.get(name) and not (name=='HaloRoot' and scene.objects[name].get('body_v1_height_adjustment')):
        scene.objects[name].matrix_basis = Matrix(rows)
BASE = {n: Matrix(rows) for n,rows in cache['Idle'][0].items()}
for o in scene.objects:
    if o.type == 'MESH' and o.name.startswith('CH0069_Weapon'):
        o.hide_render = True; o.hide_set(True)

def reset():
    for n, m in neutral.items(): arm.pose.bones[n].matrix_basis = m
    for n, m in BASE.items(): arm.pose.bones[n].matrix_basis = m
    bpy.context.view_layer.update()

def world_head(name): return arm.matrix_world @ arm.pose.bones[name].head

def rotate(name, axis, angle):
    if abs(angle) < 1e-9: return
    b = arm.pose.bones[name]
    w = arm.matrix_world @ b.matrix
    p, q, scale = w.decompose()
    b.matrix = arm.matrix_world.inverted() @ Matrix.LocRotScale(p, Quaternion(Vector(axis),angle) @ q, scale)
    bpy.context.view_layer.update()

def shift(name, delta):
    b=arm.pose.bones[name]; m=b.matrix.copy()
    m.translation += arm.matrix_world.inverted().to_3x3() @ Vector(delta)
    b.matrix=m; bpy.context.view_layer.update()

def align(name, original, desired):
    if original.length < 1e-8 or desired.length < 1e-8: return
    q=original.normalized().rotation_difference(desired.normalized())
    if q.angle > 1e-8: rotate(name, q.axis, q.angle)

def hand_ik(side, target, pole, strength=1):
    if strength < 1e-8: return
    upper='Bip001 '+side+' UpperArm'; lower='Bip001 '+side+' Forearm'; hand='Bip001 '+side+' Hand'
    original={n:arm.pose.bones[n].matrix_basis.copy() for n in (upper,lower)}
    shoulder=world_head(upper); elbow=world_head(lower); wrist=world_head(hand)
    target=Vector(target)
    a=(elbow-shoulder).length; b=(wrist-elbow).length
    direction=target-shoulder; d=max(abs(a-b)+.001,min(direction.length,a+b-.001))
    direction.normalize(); target=shoulder+direction*d
    plane=Vector(pole)-shoulder
    plane -= direction*plane.dot(direction)
    if plane.length < 1e-8: plane=Vector((0,-1,0)).cross(direction)
    plane.normalize(); along=(a*a-b*b+d*d)/(2*d)
    desired_elbow=shoulder+direction*along+plane*math.sqrt(max(0,a*a-along*along))
    align(upper,elbow-shoulder,desired_elbow-shoulder)
    align(lower,world_head(hand)-world_head(lower),target-world_head(lower))
    if strength < 1:
        solved={n:arm.pose.bones[n].matrix_basis.copy() for n in (upper,lower)}
        for n in (upper,lower):
            a,qa,sa=original[n].decompose();b,qb,sb=solved[n].decompose()
            arm.pose.bones[n].matrix_basis=Matrix.LocRotScale(a.lerp(b,strength),qa.slerp(qb,strength),sa.lerp(sb,strength))
        bpy.context.view_layer.update()

def fingers_up(side, strength=1):
    hand='Bip001 '+side+' Hand'; finger='Bip001 '+side+' Finger1'
    current=world_head(finger)-world_head(hand)
    desired=current.normalized().lerp(Vector((0,0,1)),strength).normalized()*current.length
    align(hand,current,desired)

def smooth(t): t=max(0,min(1,t)); return t*t*(3-2*t)
def envelope(t, width=.25): return smooth(t/width)*smooth((1-t)/width)

def custom_pose(name, t):
    reset()
    s=math.sin(2*math.pi*t); c=math.cos(2*math.pi*t)
    looping=SPECS[name][1]
    e=1 if looping else envelope(t)
    # Feet stay fixed during standing loops: respiration is above the pelvis.
    breathe=.008*s if looping else .006*math.sin(2*math.pi*t)*e
    rotate('Bip001 Spine',(1,0,0),breathe)
    rotate('Bip001 Spine1',(1,0,0),breathe*.6)
    if name=='Thinking':
        rotate('Bip001 Head',(0,1,0),.07+.018*s)
        rotate('Bip001 Head',(1,0,0),.045+.012*c)
        head=world_head('Bip001 Head')
        hand_ik('R',head+Vector((-.035,-.155,-.055)),head+Vector((-.18,-.12,-.23)))
        fingers_up('R',.6)
    elif name=='Working':
        rotate('Bip001 Spine1',(1,0,0),.055)
        rotate('Bip001 Head',(1,0,0),.07+.01*c)
        for side,sign in [('L',1),('R',-1)]:
            hand_ik(side,(sign*.075,-.13,.475+sign*.009*s),(sign*.20,-.10,.43))
    elif name=='HeadPat':
        rotate('Bip001 Head',(1,0,0),.12+.018*c)
        rotate('Bip001 Head',(0,1,0),.025*s)
        rotate('Bip001 Spine1',(0,1,0),.016*s)
        for side,sign in [('L',1),('R',-1)]: rotate('Bip001 '+side+' UpperArm',(0,1,0),sign*.03)
    elif name=='Sleep':
        rotate('Bip001 Spine1',(1,0,0),.065+.006*s)
        rotate('Bip001 Head',(1,0,0),.20+.024*c)
        rotate('Bip001 Head',(0,1,0),.025+.008*s)
    elif name=='Dragging':
        shift('Bip001 Pelvis',(0,0,.045+.004*c))
        rotate('Bip001 Spine1',(0,1,0),.065*s)
        rotate('Bip001 Head',(0,1,0),-.045*s)
        for side,sign in [('L',1),('R',-1)]:
            rotate('Bip001 '+side+' Thigh',(1,0,0),sign*.06*s)
            rotate('Bip001 '+side+' Calf',(1,0,0),.12+.025*c)
            hand_ik(side,(sign*(.19+.01*c),-.015,.455+sign*.013*s),(sign*.23,.045,.42))
    elif name=='Wave':
        rotate('Bip001 Head',(0,1,0),-.05*e)
        hand_ik('L',(.18,-.105,.71),(.23,-.02,.55),e)
        fingers_up('L',e)
        rotate('Bip001 L Hand',(0,1,0),.18*math.sin(6*math.pi*t)*e)
    elif name=='Success':
        rotate('Bip001 Spine1',(1,0,0),-.025*e)
        rotate('Bip001 Head',(1,0,0),.10*math.sin(4*math.pi*t)*e)
        hand_ik('L',(.16,-.105,.57),(.23,-.02,.48),e)
        fingers_up('L',.6*e)
    elif name=='Confused':
        rotate('Bip001 Head',(0,1,0),.12*e)
        rotate('Bip001 Head',(0,0,1),-.04*e)
        hand_ik('L',(.18,-.13,.49),(.24,.02,.44),e)
        fingers_up('L',.35*e)
        rotate('Bip001 L Hand',(0,0,1),.25*e)
    elif name=='HeadClick':
        rotate('Bip001 Head',(1,0,0),-.075*e)
        rotate('Bip001 Neck',(1,0,0),.025*e)
    elif name=='BodyClick':
        rotate('Bip001 Spine1',(1,0,0),-.06*e)
        rotate('Bip001 Head',(0,1,0),-.05*e)
        rotate('Bip001 L UpperArm',(0,1,0),.08*e)
    return {n: arm.pose.bones[n].matrix_basis.copy() for n in body_names}

def pose(name, t):
    if name in ('Idle','Walk'):
        rows=cache[name][round(t*(len(cache[name])-1))]
        result={n: Matrix(m) for n,m in rows.items()}
        # Close secondary hair/cape source channels without a last-frame snap.
        w=smooth((t-.75)/.25)
        if w:
            for n in body_names:
                start=Matrix(cache[name][0][n]); end=Matrix(cache[name][-1][n])
                if max(abs(start[i][j]-end[i][j]) for i in range(4) for j in range(4))<1e-6: continue
                a,qa,sa=result[n].decompose();first,qfirst,sfirst=start.decompose();last,qlast,slast=end.decompose()
                correction=qlast.rotation_difference(qfirst)
                result[n]=Matrix.LocRotScale(a+(first-last)*w,qa @ Quaternion().slerp(correction,w),sa+(sfirst-slast)*w)
        return result
    if name.endswith(('_Enter','_Exit')):
        target=name.rsplit('_',1)[0]
        start=BASE; end=custom_pose(target,0)
        w=smooth(t)
        if name.endswith('_Exit'): start,end=end,start
        result={}
        for n in body_names:
            a,qa,sa=start[n].decompose(); b,qb,sb=end[n].decompose()
            result[n]=Matrix.LocRotScale(a.lerp(b,w),qa.slerp(qb,w),sa.lerp(sb,w))
        return result
    return custom_pose(name,t)

def action_curves(action):
    return [c for layer in action.layers for strip in layer.strips for bag in strip.channelbags for c in bag.fcurves]

def build(name):
    seconds,loop,emotion=SPECS[name]; count=round(seconds*30)
    poses=[pose(name,i/count) for i in range(count+1)]
    action_name='PetV1_'+name
    old=bpy.data.actions.get(action_name)
    if old:
        for tr in list(arm.animation_data.nla_tracks):
            if any(st.action==old for st in tr.strips): arm.animation_data.nla_tracks.remove(tr)
        bpy.data.actions.remove(old)
    action=bpy.data.actions.new(action_name); action.use_fake_user=True
    slot=action.slots.new(id_type='OBJECT',name=arm.name)
    layer=action.layers.new('Body'); strip=layer.strips.new(type='KEYFRAME'); bag=strip.channelbag(slot,ensure=True)
    for n in body_names:
        b=arm.pose.bones[n]; b.rotation_mode='QUATERNION'
        decomposed=[p[n].decompose() for p in poses]
        previous=None
        for loc,quat,scale in decomposed:
            if previous is not None and previous.dot(quat)<0: quat.negate()
            previous=quat.copy()
        for part,path,dimensions in [(0,'location',3),(1,'rotation_quaternion',4),(2,'scale',3)]:
            data_path=b.path_from_id(path)
            for axis in range(dimensions):
                values=[d[part][axis] for d in decomposed]
                if name in ('Idle','Walk') and max(values)-min(values)>1e-7:
                    values[-1]=values[0]
                    tangent=(values[1]-values[-2])*.5
                    first_delta=values[0]+tangent-values[1]
                    last_delta=values[0]-tangent-values[-2]
                    for offset,weight in [(1,1),(2,.5),(3,.15)]:
                        values[offset]+=first_delta*weight
                        values[-1-offset]+=last_delta*weight
                indices=[0,count] if max(values)-min(values)<1e-7 else list(range(count+1))
                curve=bag.fcurves.new(data_path,index=axis,group_name=n)
                curve.keyframe_points.add(len(indices))
                curve.keyframe_points.foreach_set('co',[v for i in indices for v in (i+1,values[i])])
                for point in curve.keyframe_points: point.interpolation='LINEAR'
                curve.update()
    action['bodyVersion']=1; action['loop']=loop; action['suggestedEmotion']=emotion
    action['source']='CH0069_Cafe_'+name if name in ('Idle','Walk') else 'First authored draft'
    track=arm.animation_data.nla_tracks.new(); track.name=action_name
    track.strips.new(action_name,1,action); track.mute=True
    return {'name':name,'clip':action_name,'durationSeconds':count/30,'frames':[1,count+1],
            'loop':loop,'emotion':emotion,'source':action['source']}

batch=scene.get('body_v1_batch',','.join(SPECS)).split(',')
results=[build(name) for name in batch]
map_path=OUT/'Unity/body_action_map_v1.json'
existing=json.loads(map_path.read_text(encoding='utf-8')) if map_path.exists() else {'version':1,'fps':30,'rig':'Generic','rootMotion':False,'weaponVisible':False,'actions':[]}
by_name={e['name']:e for e in existing['actions']}; by_name.update({e['name']:e for e in results})
existing['actions']=[by_name[n] for n in SPECS if n in by_name]
existing['defaultAction']='Idle'; existing['defaultTransitionSeconds']=.25
existing['facePolicy']='Body owns Head/Neck. Emotion mask contains only eyes, brows and mouth; accepted source expression data unchanged.'
map_path.write_text(json.dumps(existing,ensure_ascii=False,indent=2),encoding='utf-8')
disable_animation(); reset()
setting=manifest['expressions']['neutral']
for n in face_names:
    p=setting['bone_pose'][n]
    arm.pose.bones[n].matrix_basis=Matrix.LocRotScale(Vector(p['location']),Quaternion(p['rotation']),Vector(p['scale']))
for k in body.data.shape_keys.key_blocks:
    if k.name!='Basis': k.value=setting['blendShapes'].get(k.name,0)
for node in bpy.data.materials['CHDDD_MouthAtlas'].node_tree.nodes:
    if node.name.startswith('MouthWeight_'): node.outputs[0].default_value=1 if node.name=='MouthWeight_300' else 0
arm.animation_data.action=bpy.data.actions['PetV1_Idle']
scene.frame_start=1; scene.frame_end=121; scene.frame_set(1)
scene['preview_emotion']='neutral'; scene['body_v1_action']='Idle'
scene['body_v1_batch']=''
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CHDDD_actions_v1.blend'))
print(json.dumps({'created':results,'totalBodyActions':len(existing['actions']),'file':bpy.data.filepath}))
