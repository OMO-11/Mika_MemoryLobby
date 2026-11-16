"""Second-pass performance: causal secondary motion and action-specific face timelines."""
import bpy,json,math,sys,re
from pathlib import Path
from mathutils import Matrix,Vector,Quaternion

ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
# Reuse rig math, without running the first-version authoring/export entry point.
legacy=(ROOT.parent.parent/'Tools/AssetPipeline/create_chddd_body_v1.py').read_text(encoding='utf-8')
exec(compile(legacy.split("batch=scene.get('body_v1_batch'")[0],'rig_math','exec'))
legacy_custom=custom_pose
SPECS={
 'Idle':(4,True,'neutral'),'Walk':(40/30,True,'neutral'),
 'Thinking':(4.8,True,'concern'),'Working':(3.6,True,'neutral'),
 'HeadPat':(4.6,True,'happy'),'Dragging':(337/30,True,'happy'),
 'Sleep':(6,True,'sleepy'),'Wave':(3.6,False,'happy'),
 'Success':(3.4,False,'happy'),'Confused':(3.4,False,'confused'),
 'HeadClick':(1.8,False,'surprised'),'BodyClick':(2.6,False,'shy'),
 'HeadPat_Enter':(.9,False,'happy'),'HeadPat_Exit':(1.1,False,'neutral'),
 'Dragging_Enter':(.7,False,'surprised'),'Dragging_Exit':(.9,False,'neutral'),
 'Sleep_Enter':(2.2,False,'sleepy'),'Sleep_Exit':(1.8,False,'neutral')}
pickup=json.loads((ROOT/'body_source_pickup_v2.json').read_text(encoding='utf-8'))['frames']
reset()
static_arm_world=arm.matrix_world.copy()
anchors={s:{'foot':arm.matrix_world@arm.pose.bones['Bip001 '+s+' Foot'].matrix,
            'knee':world_head('Bip001 '+s+' Calf')} for s in ('L','R')}
reference_world={b.name:arm.matrix_world@b.matrix for b in arm.pose.bones}
secondary=[n for n in body_names if n.startswith(('bone_hair','bone_skirt','bone_cape','bone_wing','bone_ribbon'))]
for tr in list(arm.animation_data.nla_tracks):
    if tr.name.startswith('PetV2_'):arm.animation_data.nla_tracks.remove(tr)

def leg_ik(side,foot_world=None):
    upper='Bip001 '+side+' Thigh';lower='Bip001 '+side+' Calf';foot='Bip001 '+side+' Foot'
    foot_world=anchors[side]['foot'] if foot_world is None else foot_world
    p=world_head(upper);k=world_head(lower);f=world_head(foot);target=foot_world.translation
    a=(k-p).length;b=(f-k).length;direction=target-p;d=min(direction.length,a+b-.00001)
    direction.normalize();pole=anchors[side]['knee']-p;pole-=direction*pole.dot(direction)
    if pole.length<1e-8:pole=Vector((0,-1,0))
    pole.normalize();along=(a*a-b*b+d*d)/(2*d)
    elbow=p+direction*along+pole*math.sqrt(max(0,a*a-along*along))
    align(upper,k-p,elbow-p);align(lower,world_head(foot)-world_head(lower),target-world_head(lower))
    arm.pose.bones[foot].matrix=arm.matrix_world.inverted()@foot_world
    bpy.context.view_layer.update()

def bell(t,center,width):return math.exp(-((t-center)/width)**2)

exec(compile((ROOT.parent.parent/'Tools/AssetPipeline/chddd_readable_performances.py').read_text(encoding='utf-8'),'readable_performances','exec'))
exec(compile((ROOT.parent.parent/'Tools/AssetPipeline/chddd_local_refinements_v7.py').read_text(encoding='utf-8'),'local_refinements_v7','exec'))

# Monotone cubic timing: anticipation, accents and settling have distinct speeds.
# Body and face use the same clock; game-source motions are never retimed.
BEATS={
 'Wave':[(0,0),(.14,.08),(.29,.30),(.52,.52),(.73,.73),(.87,.90),(1,1)],
 'Success':[(0,0),(.22,.14),(.35,.38),(.49,.54),(.74,.72),(1,1)],
 'Confused':[(0,0),(.20,.17),(.47,.42),(.65,.66),(.87,.85),(1,1)],
 'HeadClick':[(0,0),(.12,.20),(.34,.36),(.63,.65),(.83,.85),(1,1)],
 'BodyClick':[(0,0),(.12,.20),(.32,.40),(.64,.67),(.84,.86),(1,1)]}

def performance_time(name,t):
    if name in READABLE:return t
    points=BEATS.get(name)
    if points is None:return t
    slopes=[(b[1]-a[1])/(b[0]-a[0]) for a,b in zip(points,points[1:])]
    tangent=[slopes[0]]
    for i in range(1,len(points)-1):
        h0=points[i][0]-points[i-1][0];h1=points[i+1][0]-points[i][0]
        w0=2*h1+h0;w1=h1+2*h0
        tangent.append((w0+w1)/(w0/slopes[i-1]+w1/slopes[i]))
    tangent.append(slopes[-1])
    for i,(a,b) in enumerate(zip(points,points[1:])):
        if t<=b[0]:
            h=b[0]-a[0];u=max(0,min(1,(t-a[0])/h))
            return (2*u**3-3*u*u+1)*a[1]+(u**3-2*u*u+u)*h*tangent[i]+(-2*u**3+3*u*u)*b[1]+(u**3-u*u)*h*tangent[i+1]
    return 1
def curl(side,amount):
    for name in ('Finger01','Finger11','Finger21'):
        bone=arm.pose.bones['Bip001 '+side+' '+name]
        bone.rotation_quaternion=bone.rotation_quaternion@Quaternion((0,0,1),amount)
    bpy.context.view_layer.update()

def pickup_pose(t):
    row=pickup[round(t*337)]
    result={n:Matrix(m) for n,m in row['bones'].items()}
    source_world=Matrix(row['armatureWorld'])
    pelvis=arm.pose.bones['Bip001 Pelvis']
    result['Bip001 Pelvis']=pelvis.bone.matrix_local.inverted()@static_arm_world.inverted()@source_world@pelvis.bone.matrix_local@result['Bip001 Pelvis']
    return result

def main_pose(name,t):
    if name in ('Idle','Walk'):
        return pose(name,t)
    if name=='Dragging':return pickup_pose(t)
    if name in READABLE:return readable_pose(name,t)
    t=performance_time(name,t)
    # Rebuild these gestures from neutral to avoid accumulating legacy poses.
    if name in ('Wave','Success','HeadClick','BodyClick'):
        reset()
    else:
        legacy_custom(name,t)
    s=math.sin(2*math.pi*t);c=math.cos(2*math.pi*t)
    e=1 if SPECS[name][1] else envelope(t,.20)
    root_delta=Vector((0,0,0))
    if name=='Thinking':
        rotate('Bip001 Spine1',(0,0,1),.035*s)
        rotate('Bip001 Head',(0,0,1),.10*math.sin(2*math.pi*t+.35))
        rotate('Bip001 Head',(0,1,0),.035*s)
        h=world_head('Bip001 Head')
        hand_ik('R',h+Vector((-.037,-.15,-.056+.003*c)),h+Vector((-.19,-.12,-.23)))
        fingers_up('R',.55)
        root_delta=Vector((.002*s,0,.0015*s))
    elif name=='Working':
        rotate('Bip001 Spine1',(0,0,1),.035*s)
        rotate('Bip001 Head',(0,0,1),.055*math.sin(2*math.pi*t-.35))
        rotate('Bip001 Head',(1,0,0),.025*c)
        for side,sign in [('L',1),('R',-1)]:
            hand_ik(side,(sign*(.065+.012*c),-.14,.49+sign*.013*s),(sign*.20,-.08,.43))
            rotate('Bip001 '+side+' Hand',(0,0,1),sign*.12*s)
        root_delta=Vector((.003*s,0,-.002+.002*c))
    elif name=='HeadPat':
        rotate('Bip001 Spine1',(0,1,0),.035*s)
        rotate('Bip001 Head',(0,1,0),.075*math.sin(2*math.pi*t-.3))
        rotate('Bip001 Head',(1,0,0),.03+.03*math.sin(4*math.pi*t))
        rotate('Bip001 Neck',(0,1,0),-.022*s)
        hand_ik('R',(-.035,-.135,.46+.003*c),(-.19,-.08,.42),.35)
        root_delta=Vector((.003*s,0,-.005+.002*c))
    elif name=='Sleep':
        rotate('Bip001 Head',(1,0,0),.028*math.sin(2*math.pi*t-.5))
        rotate('Bip001 Spine1',(0,1,0),.012*s)
        root_delta=Vector((0,0,.0018*s))
    elif name=='Wave':
        # The shoulder joins the greeting; later waves soften progressively.
        wave=math.sin(6*math.pi*t)*(.98-.34*t)
        rotate('Bip001 Spine1',(0,0,1),(-.075+.022*wave)*e)
        rotate('Bip001 Head',(0,0,1),(-.080-.033*wave)*e)
        rotate('Bip001 Head',(0,1,0),-.070*e)
        hand_ik('L',(.205+.046*wave,-.105,.745+.021*math.sin(4*math.pi*t)),(.26,-.025,.55),e)
        hand=arm.pose.bones['Bip001 L Hand'];loc,_,sc=hand.matrix_basis.decompose()
        hand.matrix_basis=Matrix.LocRotScale(loc,BASE[hand.name].to_quaternion(),sc);bpy.context.view_layer.update()
        fingers_up('L',e);rotate('Bip001 L Hand',(0,1,0),.40*wave*e)
        hand_ik('R',(-.070,-.165,.475+.008*wave),(-.22,-.04,.43),.38*e)
        root_delta=Vector(((.009+.002*wave)*e,0,(-.011*bell(t,.12,.09)+.004*bell(t,.44,.22))*e))
    elif name=='Success':
        anticipation=bell(t,.15,.10);accent=bell(t,.44,.14);satisfaction=bell(t,.68,.13)
        rotate('Bip001 Spine1',(1,0,0),(.095*anticipation-.105*accent)*e)
        rotate('Bip001 Spine1',(0,0,1),-.055*accent*e)
        rotate('Bip001 Head',(0,1,0),(-.045-.050*satisfaction)*e)
        rotate('Bip001 Head',(1,0,0),(.065*anticipation-.085*accent+.065*satisfaction)*e)
        hand_ik('L',(.18,-.125,.565+.145*accent+.026*satisfaction),(.27,-.04,.48),e)
        fingers_up('L',.75*e);curl('L',.78*e)
        hand_ik('R',(-.075,-.17,.48+.034*accent),(-.23,-.045,.44),.68*e)
        curl('R',.50*e)
        root_delta=Vector((.007*accent*e,0,(-.018*anticipation+.008*accent)*e))
    elif name=='Confused':
        shrug=bell(t,.48,.19)
        rotate('Bip001 Head',(0,0,1),(.15*bell(t,.28,.13)-.11*bell(t,.57,.16))*e)
        rotate('Bip001 Spine1',(0,1,0),-.055*e)
        hand_ik('L',(.21+.018*shrug,-.145,.50+.027*shrug),(.27,-.035,.45),e)
        rotate('Bip001 L Hand',(1,0,0),-.30*e)
        hand_ik('R',(-.09,-.16,.47+.020*shrug),(-.23,-.035,.43),.35*e)
        root_delta=Vector((-.006*e,0,-.004*e))
    elif name=='HeadClick':
        surprise=bell(t,.21,.12);reply=bell(t,.58,.20)
        rotate('Bip001 Head',(1,0,0),(-.12*surprise+.05*reply)*e)
        rotate('Bip001 Neck',(1,0,0),.035*surprise*e)
        rotate('Bip001 Head',(0,1,0),(-.095*surprise+.13*reply)*e)
        rotate('Bip001 Spine1',(1,0,0),-.07*surprise*e)
        hand_ik('L',(.15,-.13,.49+.018*reply),(.24,-.02,.43),.52*reply*e)
        fingers_up('L',.35*reply*e)
        root_delta=Vector((-.005*reply*e,.009*surprise*e,-.009*surprise*e))
    elif name=='BodyClick':
        surprise=bell(t,.20,.13);reply=bell(t,.60,.22)
        rotate('Bip001 Spine1',(1,0,0),-.08*surprise*e)
        rotate('Bip001 Spine1',(0,0,1),(-.13*surprise-.055*reply)*e)
        rotate('Bip001 Head',(0,0,1),(.14*surprise+.075*reply)*e)
        rotate('Bip001 Head',(0,1,0),-.075*reply*e)
        hand_ik('R',(-.05,-.18,.525+.022*reply),(-.23,-.07,.45),.85*e)
        curl('R',.20*reply*e)
        hand_ik('L',(.16,-.11,.43+.026*surprise),(.24,-.01,.40),.32*surprise*e)
        root_delta=Vector((.006*surprise*e,.010*surprise*e,-.009*surprise*e))
    if root_delta.length>0:
        shift('Bip001 Pelvis',root_delta)
        leg_ik('L');leg_ik('R')
    return {n:arm.pose.bones[n].matrix_basis.copy() for n in body_names}

def apply_pose(p):
    for n,m in p.items():arm.pose.bones[n].matrix_basis=m
    bpy.context.view_layer.update()

def axis_vector(q):
    if q.w<0:q.negate()
    return q.axis*q.angle if q.angle>1e-6 else Vector((0,0,0))

def driver_world(poses):
    output=[]
    for p in poses:
        apply_pose(p)
        output.append({n:arm.matrix_world@arm.pose.bones[n].matrix for n in ('Bip001 Head','Bip001 Spine1','Bip001 Pelvis','Bip001 L UpperArm','Bip001 R UpperArm')})
    return output

def spring_lag(values,seconds,loop,frequency=3.0,damping=.72):
    dt=1/30;omega=2*math.pi*frequency
    work=values[:-1] if loop else values
    position=work[0].copy();velocity=Vector();out=[]
    cycles=7 if loop else 1
    for cycle in range(cycles):
        current=[]
        for target in work:
            # Four substeps keep fast sleeve/hair responses numerically stable.
            for _ in range(4):
                acceleration=(target-position)*(omega*omega)-velocity*(2*damping*omega)
                velocity+=acceleration*(dt/4);position+=velocity*(dt/4)
            current.append(position-target)
        if cycle==cycles-1:out=current
    if loop:out.append(out[0].copy())
    return out

def add_secondary(name,poses):
    if name in ('Idle','Walk','Dragging'):return poses # Retain game-source hair/clothes without double motion.
    seconds,loop,_=SPECS[name];drivers=driver_world(poses)
    lag={};inputs_by_driver={};angular_by_driver={}
    for driver in drivers[0]:
        reference=reference_world[driver]
        inputs=[];angles=[]
        for d in drivers:
            m=d[driver];q=reference.to_quaternion().rotation_difference(m.to_quaternion())
            vector=reference.to_quaternion()@axis_vector(q)
            angles.append(vector.copy())
            vector+=(m.translation-reference.translation)*7
            inputs.append(vector)
        inputs_by_driver[driver]=inputs
        angular_by_driver[driver]=angles
        for depth in range(5):
            base_frequency=2.7 if 'Arm' in driver else 2.1
            lag[(driver,depth)]=spring_lag(inputs,seconds,loop,base_frequency/(1+.20*depth),.70-.025*depth)
    depths={}
    for n in secondary:
        depth=0;parent=arm.pose.bones[n].parent
        prefix='bone_hair' if n.startswith('bone_hair') else 'bone_skirt' if n.startswith('bone_skirt') else 'bone_cape' if n.startswith('bone_cape') else 'bone_ribbon'
        while parent and parent.name.startswith(prefix):depth+=1;parent=parent.parent
        depths[n]=min(4,depth)
    for i,p in enumerate(poses):
        t=i/(len(poses)-1)
        taper=1 if loop else smooth(t/.14)*smooth((1-t)/.22)
        if name in FLOW_ACTIONS:
            def ease5(x):
                x=max(0,min(1,x));return x**3*(10-15*x+6*x*x)
            taper=ease5(t/.10)*ease5((1-t)/.16)
        for n in secondary:
            depth=depths[n]
            if n.startswith('bone_hair'):
                drv=lag[('Bip001 Head',depth)][i];frequency_scale=.25+.18*depth
                if '_f_' in n:frequency_scale=.06+.035*depth
                if 'dango' in n:frequency_scale=.10
                delta=drv*frequency_scale
                if '_f_' not in n and 'dango' not in n:
                    # At a held head tilt, long hair settles back toward gravity
                    # instead of remaining rigidly tilted with the skull.
                    head_angle=angular_by_driver['Bip001 Head'][i]
                    delta-=Vector((head_angle.x,head_angle.y,0))*(.18 if depth==0 else .12)
                limit=(.055+.030*depth) if '_f_' not in n else .025
            elif n.startswith('bone_cape') or n.startswith('bone_wing'):
                left=bool(re.search(r'_(?:l|bl|fl)(?:_|$)',n))
                right=bool(re.search(r'_(?:r|br|fr)(?:_|$)',n))
                arm_driver='Bip001 L UpperArm' if left else 'Bip001 R UpperArm'
                arm_lag=lag[(arm_driver,depth)][i] if left or right else (lag[('Bip001 L UpperArm',depth)][i]+lag[('Bip001 R UpperArm',depth)][i])*.5
                delta=lag[('Bip001 Spine1',depth)][i]*(.38+.16*depth)+arm_lag*(.20+.09*depth)
                # Shoulder fabric follows arm elevation even during the held gesture.
                # Side chain roots follow first; tips receive less displacement and more lag.
                if n.startswith('bone_cape') and (left or right):
                    arm_input=inputs_by_driver[arm_driver][i]
                    delta+=arm_input*(.12 if depth==0 else .07)
                limit=.24 if n.startswith('bone_cape') else .07
            else:
                delta=lag[('Bip001 Pelvis',depth)][i]*(.20+.16*depth)+lag[('Bip001 Spine1',depth)][i]*(.10+.055*depth)
                limit=.045+.018*depth
            if delta.length>limit:delta=delta.normalized()*limit
            delta*=taper
            if delta.length<1e-7:continue
            local=reference_world[n].to_quaternion().inverted()@delta
            loc,q,scale=p[n].decompose();p[n]=Matrix.LocRotScale(loc,q@Quaternion(local.normalized(),local.length),scale)
    return poses

def mix_matrix(a,b,t):
    pa,qa,sa=a.decompose();pb,qb,sb=b.decompose()
    return Matrix.LocRotScale(pa.lerp(pb,t),qa.slerp(qb,t),sa.lerp(sb,t))

def close_loop(poses):
    end=poses[-1];start=poses[0]
    for i,p in enumerate(poses):
        t=i/(len(poses)-1);w=smooth((t-.82)/.18)
        if w:
            for n in body_names:
                loc,q,sc=p[n].decompose();a,qa,sa=start[n].decompose();b,qb,sb=end[n].decompose()
                correction=qb.rotation_difference(qa)
                p[n]=Matrix.LocRotScale(loc+(a-b)*w,q@Quaternion().slerp(correction,w),sc+(sa-sb)*w)
    poses[-1]={n:m.copy() for n,m in poses[0].items()}
    return poses

def harmonize_loop_tangents(name,poses):
    targets=body_names if name in ('Idle','Walk') else secondary+[n for n in body_names if n.startswith('bone_foot_ribbon')]
    for n in targets:
        loc,q,scale=poses[0][n].decompose()
        l1,q1,s1=poses[1][n].decompose();le,qe,se=poses[-2][n].decompose()
        tangent=(axis_vector(q.rotation_difference(q1))+axis_vector(qe.rotation_difference(q)))*.5
        if n in secondary and tangent.length>.06:tangent=tangent.normalized()*.06
        dl=(l1-le)*.5;ds=(s1-se)*.5
        dq=Quaternion(tangent.normalized(),tangent.length) if tangent.length>1e-8 else Quaternion()
        front=Matrix.LocRotScale(loc+dl,q@dq,scale+ds)
        back=Matrix.LocRotScale(loc-dl,q@dq.inverted(),scale-ds)
        for index,desired in ((1,front),(-2,back)):
            old=poses[index][n]
            a,qa,sa=old.decompose();b,qb,sb=desired.decompose();correction=qa.rotation_difference(qb)
            for offset,weight in ((0,1),(1,.80),(2,.45),(3,.15)):
                i=index+offset if index>0 else index-offset
                li,qi,si=poses[i][n].decompose()
                poses[i][n]=Matrix.LocRotScale(li+(b-a)*weight,qi@Quaternion().slerp(correction,weight),si+(sb-sa)*weight)
    return poses

def smooth_flow_arms(name,poses):
    # Normalizing a lerp from a nearly downward finger direction to up can turn
    # the wrist abruptly around strength .5. IK poles can also accelerate the
    # elbow near a straight arm. Use the accepted WORLD orientations for the
    # whole chain, then solve local rotations parent-first without either singularity.
    if name not in FLOW_ACTIONS:return poses
    times=[t for t,_ in STAGES[name]]
    chains=['Bip001 '+side+' '+part for side in ('L','R') for part in ('UpperArm','Forearm','Hand')]
    keys={n:[] for n in chains}
    for t in times:
        apply_pose(readable_pose(name,t))
        for n in keys:keys[n].append((arm.matrix_world@arm.pose.bones[n].matrix).to_quaternion())
    vectors={};origins={}
    for side,quats in keys.items():
        origins[side]=quats[0];values=[]
        for q in quats:
            difference=q@quats[0].inverted()
            if difference.w<0:difference.negate()
            vec=axis_vector(difference)
            if values and vec.length>1e-6:
                alternatives=[vec,vec.normalized()*(vec.length-2*math.pi),vec.normalized()*(vec.length+2*math.pi)]
                vec=min(alternatives,key=lambda v:(v-values[-1]).length)
            values.append(vec)
        vectors[side]=values
    for i,p in enumerate(poses):
        t=i/(len(poses)-1);apply_pose(p)
        for side in keys:
            offset=-.020 if side.endswith('UpperArm') else -.030 if side.endswith('Forearm') else -.035
            vec=Vector(tuple(flow_scalar(times,[v[a] for v in vectors[side]],t,offset) for a in range(3)))
            q=Quaternion(vec.normalized(),vec.length) if vec.length>1e-9 else Quaternion()
            bone=arm.pose.bones[side];world=arm.matrix_world@bone.matrix
            bone.matrix=arm.matrix_world.inverted()@Matrix.LocRotScale(world.translation,q@origins[side],world.to_scale())
            bpy.context.view_layer.update();p[bone.name]=bone.matrix_basis.copy()
    return poses

all_poses={}
for name,(seconds,loop,emotion) in SPECS.items():
    if name.endswith(('_Enter','_Exit')):continue
    count=round(seconds*30)
    poses=[main_pose(name,i/count) for i in range(count+1)]
    poses=smooth_flow_arms(name,poses)
    poses=add_secondary(name,poses)
    poses=local_details_v7(name,poses)
    if loop:poses=harmonize_loop_tangents(name,close_loop(poses))
    elif name not in ('Idle','Walk','Dragging'):
        idle=all_poses['Idle'][0]
        for i,p in enumerate(poses):
            t=i/count
            if t<.08 and name not in FLOW_ACTIONS:
                poses[i]={n:mix_matrix(idle[n],p[n],smooth(t/.08)) for n in body_names}
            elif t>(.92 if name in READABLE else .78) and name not in FLOW_ACTIONS:
                start=.92 if name in READABLE else .78
                poses[i]={n:mix_matrix(p[n],idle[n],smooth((t-start)/(1-start))) for n in body_names}
        poses[0]={n:m.copy() for n,m in idle.items()}
        poses[-1]={n:m.copy() for n,m in idle.items()}
    all_poses[name]=poses
for name,(seconds,loop,emotion) in SPECS.items():
    if not name.endswith(('_Enter','_Exit')):continue
    target=name.rsplit('_',1)[0];count=round(seconds*30)
    a=all_poses['Idle'][0];b=all_poses[target][0]
    if name.endswith('_Exit'):a,b=b,a
    poses=[]
    for i in range(count+1):
        t=i/count;w=smooth(t)
        p={n:mix_matrix(a[n],b[n],w) for n in body_names}
        if target=='Sleep' and name.endswith('_Enter'):
            apply_pose(p);e=math.sin(math.pi*t)**2
            hand_ik('R',(-.04,-.16,.60),(-.20,-.08,.45),.75*e)
            p={n:arm.pose.bones[n].matrix_basis.copy() for n in body_names}
        poses.append(p)
    poses=add_secondary(name,poses)
    # Align soft accessory boundaries with the adjacent loop and idle.
    for i,p in enumerate(poses):
        t=i/count
        for n in secondary:
            if t<.15:p[n]=mix_matrix(a[n],p[n],smooth(t/.15))
            if t>.75:p[n]=mix_matrix(p[n],b[n],smooth((t-.75)/.25))
    poses[0]={n:m.copy() for n,m in a.items()};poses[-1]={n:m.copy() for n,m in b.items()}
    all_poses[name]=poses

MOUTH=[300,706,705,604,605,305,401,200]
def face_value(blink=0,sad=0,annoyed=0,sleepy=0,tile=300,gaze=0,lift=0):
    return {'blink':blink,'sad':sad,'annoyed':annoyed,'sleepy':sleepy,'gazeX':gaze,'gazeY':0,'browLift':lift,
            'mouthWeights':[1 if cell==tile else 0 for cell in MOUTH]}
def knot(t,**kwargs):return (t,face_value(**kwargs))
timelines={
 'Idle':[knot(0),knot(.38,tile=705),knot(.65),knot(1)],
 'Walk':[knot(0,blink=.04,tile=705),knot(.5,blink=.05,tile=705,gaze=.3),knot(1,blink=.04,tile=705)],
 'Thinking':[knot(0,blink=.12,sad=.35),knot(.25,blink=.10,sad=.50,tile=401,gaze=-.8),knot(.62,blink=.16,sad=.12,tile=705,gaze=.6),knot(.90,blink=.12,sad=.35),knot(1,blink=.12,sad=.35)],
 'Working':[knot(0,blink=.10,tile=305,gaze=-.2),knot(.27,blink=.14,tile=305,gaze=.5),knot(.56,blink=.05,tile=705,gaze=-.4),knot(.82,blink=.10,tile=305),knot(1,blink=.10,tile=305,gaze=-.2)],
 'HeadPat':[knot(0,blink=.25,sleepy=.08,tile=705),knot(.20,blink=.34,tile=706,gaze=.2),knot(.48,blink=.55,sleepy=.15,tile=706),knot(.73,blink=.26,sleepy=.08,tile=705),knot(1,blink=.25,sleepy=.08,tile=705)],
 'Dragging':[knot(0,blink=.05,tile=401,lift=.3),knot(.12,blink=.18,tile=705),knot(.36,blink=.32,tile=706),knot(.56,blink=.18,tile=705,gaze=-.7),knot(.76,blink=.28,tile=706,gaze=.6),knot(.94,blink=.05,tile=401,lift=.3),knot(1,blink=.05,tile=401,lift=.3)],
 'Sleep':[knot(0,blink=1,sleepy=.92,tile=305),knot(.48,blink=1,sleepy=1),knot(1,blink=1,sleepy=.92,tile=305)],
 'Wave':[knot(0),knot(.12,blink=.10,tile=705,lift=.15),knot(.28,blink=.03,tile=706,lift=.35),knot(.37,blink=.03,tile=706,lift=.35),knot(.57,blink=.22,tile=706),knot(.76,blink=.10,tile=705),knot(1)],
 'Success':[knot(0),knot(.15,blink=.18,tile=305),knot(.28,tile=605,lift=.55),knot(.36,blink=1,tile=706,lift=.90),knot(.48,blink=1,tile=706,lift=.90),knot(.56,blink=1,tile=706,lift=.80),knot(.64,blink=.70,tile=706,lift=.45),knot(.80,blink=.15,tile=705),knot(1)],
 'Confused':[knot(0),knot(.10),knot(.22,tile=605,lift=.35),knot(.40,sad=.60,tile=401,lift=.35,gaze=.10),knot(.58,sad=.60,tile=401,lift=.35,gaze=.10),knot(.76,blink=.12,sad=.24,tile=401),knot(.94),knot(1)],
 'HeadClick':[knot(0),knot(.10,blink=1,tile=200,lift=.6),knot(.18,blink=1,tile=200,lift=.6),knot(.30,blink=1,tile=200,lift=.6),knot(.55,blink=.22,annoyed=.55,tile=604,gaze=.2),knot(.70,blink=.18,annoyed=.65,tile=604),knot(.90,blink=.08,annoyed=.30,tile=305),knot(1)],
 'BodyClick':[knot(0),knot(.13,tile=605,lift=.65),knot(.35,blink=.15,sad=.20,tile=401,gaze=-.9),knot(.55,blink=.15,sad=.20,tile=401,gaze=-.9),knot(.70,blink=.10,tile=705,gaze=.65),knot(.85,blink=.12,tile=705),knot(1)]}
pulses={'Idle':[(.62,.032)],'Thinking':[(.16,.025),(.76,.025)],'Working':[(.68,.025)],'HeadPat':[(.43,.045)],'Dragging':[(.19,.015),(.52,.015),(.84,.015)],'Wave':[(.55,.022)],'Success':[(.62,.042)],'Confused':[(.72,.025)],'HeadClick':[(.40,.043)],'BodyClick':[(.24,.030),(.79,.024)]}
def evaluate_face(name,t):
    t=performance_time(name,t)
    knots=timelines[name]
    for j in range(len(knots)-1):
        ta,a=knots[j];tb,b=knots[j+1]
        if t<=tb:
            w=smooth((t-ta)/(tb-ta));result={k:a[k]+(b[k]-a[k])*w for k in a if k!='mouthWeights'}
            mouth_window=min(.14,(tb-ta)*SPECS[name][0])
            mw=smooth(min(1,max(0,((t-tb)*SPECS[name][0]+mouth_window)/mouth_window)))
            result['mouthWeights']=[x+(y-x)*mw for x,y in zip(a['mouthWeights'],b['mouthWeights'])]
            break
    else:result=dict(knots[-1][1]);result['mouthWeights']=list(knots[-1][1]['mouthWeights'])
    for center,width in pulses.get(name,[]):
        pulse=bell(t,center,width)
        result['blink']=1-(1-result['blink'])*(1-pulse)
    return result
face_samples={}
for name,poses in all_poses.items():
    if name.endswith(('_Enter','_Exit')):continue
    values=[evaluate_face(name,i/(len(poses)-1)) for i in range(len(poses))]
    if SPECS[name][1]:values[-1]=json.loads(json.dumps(values[0]))
    face_samples[name]=values
for name,poses in all_poses.items():
    if not name.endswith(('_Enter','_Exit')):continue
    target=name.rsplit('_',1)[0];a=face_value();b=face_samples[target][0]
    if name.endswith('_Exit'):a,b=b,a
    values=[]
    for i in range(len(poses)):
        t=i/(len(poses)-1);w=smooth(t)
        v={k:a[k]+(b[k]-a[k])*w for k in a if k!='mouthWeights'}
        v['mouthWeights']=[x+(y-x)*w for x,y in zip(a['mouthWeights'],b['mouthWeights'])]
        if target=='Sleep' and name.endswith('_Enter'):
            yawn=bell(t,.40,.17)
            v['blink']=max(v['blink'],.75*yawn)
            v['mouthWeights']=[x*(1-yawn)+(1 if cell==605 else 0)*yawn for x,cell in zip(v['mouthWeights'],MOUTH)]
        elif target=='Dragging' and name.endswith('_Enter'):
            surprise=bell(t,.35,.15)
            v['browLift']+=.7*surprise;v['blink']*=1-surprise
            v['mouthWeights']=[x*(1-surprise)+(1 if cell==605 else 0)*surprise for x,cell in zip(v['mouthWeights'],MOUTH)]
        elif name.endswith('_Exit'):
            v['blink']=max(v['blink'],bell(t,.62,.07)*.95)
        values.append(v)
    values[0]=json.loads(json.dumps(a));values[-1]=json.loads(json.dumps(b));face_samples[name]=values

def new_action(name,owner):
    old=bpy.data.actions.get(name)
    if old:
        if owner.animation_data and owner.animation_data.action==old:owner.animation_data.action=None
        for o in scene.objects:
            if o.animation_data:
                for tr in list(o.animation_data.nla_tracks):
                    if any(st.action==old for st in tr.strips):o.animation_data.nla_tracks.remove(tr)
        bpy.data.actions.remove(old)
    action=bpy.data.actions.new(name);action.use_fake_user=True
    id_type='OBJECT' if isinstance(owner,bpy.types.Object) else 'KEY' if isinstance(owner,bpy.types.Key) else 'NODETREE'
    slot=action.slots.new(id_type=id_type,name=owner.name)
    layer=action.layers.new('Performance');strip=layer.strips.new(type='KEYFRAME');bag=strip.channelbag(slot,ensure=True)
    return action,bag
def curve(bag,path,axis,values,group=''):
    indices=[0,len(values)-1] if max(values)-min(values)<1e-8 else range(len(values))
    c=bag.fcurves.new(path,index=axis,group_name=group)
    c.keyframe_points.add(len(indices));c.keyframe_points.foreach_set('co',[v for i in indices for v in (i+1,values[i])])
    for k in c.keyframe_points:k.interpolation='LINEAR'
    c.update()
def pose_action(name,poses):
    action,bag=new_action(name,arm)
    for n in poses[0]:
        bone=arm.pose.bones[n];bone.rotation_mode='QUATERNION'
        decomposed=[p[n].decompose() for p in poses];previous=None
        for l,q,s in decomposed:
            if previous is not None and previous.dot(q)<0:q.negate()
            previous=q.copy()
        for part,path,dims in ((0,'location',3),(1,'rotation_quaternion',4),(2,'scale',3)):
            for axis in range(dims):curve(bag,bone.path_from_id(path),axis,[d[part][axis] for d in decomposed],n)
    tr=arm.animation_data.nla_tracks.new();tr.name=name;tr.strips.new(name,1,action);tr.mute=True
    return action

entries=[];profiles=[]
for name,(seconds,loop,emotion) in SPECS.items():
    p=all_poses[name];samples=face_samples[name]
    action=pose_action('PetV2_'+name,p);action['loop']=loop;action['version']=2
    facial=[]
    for v in samples:
        pp={}
        for n in face_names:
            data=manifest['expressions']['neutral']['bone_pose'][n]
            m=Matrix.LocRotScale(Vector(data['location']),Quaternion(data['rotation']),Vector(data['scale']))
            if n in ('Bip001 Xtra_eyeL','Bip001 Xtra_eyeR'):
                delta=Vector((.0018*v['gazeX'],0,.0012*v['gazeY']))
                local=reference_world[n].to_3x3().inverted()@delta;m.translation+=local
            if 'eyeblow' in n:
                delta=Vector((0,0,.002*v['browLift']))
                m.translation+=reference_world[n].to_3x3().inverted()@delta
            pp[n]=m
        facial.append(pp)
    face_action=pose_action('PetV2_FaceProcess_'+name,facial)
    shapes,shape_bag=new_action('PetV2_Shapes_'+name,body.data.shape_keys)
    for key,field in [('Pet_Blink','blink'),('Pet_BrowSad','sad'),('Pet_BrowAnnoyed','annoyed'),('Pet_BrowSleepy','sleepy')]:
        curve(shape_bag,body.data.shape_keys.key_blocks[key].path_from_id('value'),0,[v[field] for v in samples],key)
    curve(shape_bag,body.data.shape_keys.key_blocks['Pet_EyeWide'].path_from_id('value'),0,[0]*len(samples),'Pet_EyeWide')
    mouth=bpy.data.materials['CHDDD_MouthAtlas'].node_tree;ma,mb=new_action('PetV2_Mouth_'+name,mouth)
    for i,tile in enumerate(MOUTH):curve(mb,mouth.nodes['MouthWeight_'+str(tile)].outputs[0].path_from_id('default_value'),0,[v['mouthWeights'][i] for v in samples],'Mouth')
    entry={'name':name,'clip':action.name,'faceClip':face_action.name,'shapeAction':shapes.name,'mouthAction':ma.name,
           'durationSeconds':(len(p)-1)/30,'frames':[1,len(p)],'loop':loop,'emotion':emotion,
           'source':'CH0069_Formation_Pickup; original GLB #29' if name=='Dragging' else 'Cafe source with polish' if name in ('Idle','Walk') else 'Authored V2 performance',
           'logicalAction':name.rsplit('_',1)[0] if name.endswith(('_Enter','_Exit')) else name,
           'enterClip':'PetV2_'+name+'_Enter' if name+'_Enter' in SPECS else '',
           'exitClip':'PetV2_'+(name.rsplit('_',1)[0] if name.endswith(('_Enter','_Exit')) else name)+'_Exit' if (name.rsplit('_',1)[0] if name.endswith(('_Enter','_Exit')) else name)+'_Exit' in SPECS else '',
           'nextAction':name.rsplit('_',1)[0] if name.endswith('_Enter') else 'Idle' if not loop else '',
           'transitionSeconds':.10 if name=='HeadClick' else .12 if name=='BodyClick' else .18 if name in ('Wave','Success') else .28}
    daily=name in ('Idle','Walk','Thinking','Working','Sleep','Sleep_Enter','Sleep_Exit')
    entry.update(PERFORMANCE_NOTES.get(name,{'tier':'daily' if daily else 'emotion','intensity':'low' if daily else 'medium','recommendedFrequency':'State-driven; host owns scheduling'}))
    if name in READABLE:entry['intent']=PERFORMANCE_INTENTS_ZH[name]
    entries.append(entry);profiles.append({'name':name,'durationSeconds':entry['durationSeconds'],'loop':loop,'samples':samples})

mapping={'version':2,'revision':7,'designDirection':'Approved full-body poses; continuous sub-action flow with retained readable holds','schedulingNotes':'Tier and frequency fields are authoring recommendations; runtime host must throttle special performances','fps':30,'rig':'Generic','rootMotion':False,'weaponVisible':False,'defaultAction':'Idle','defaultTransitionSeconds':.28,'actions':entries}
mapping['flowRefinement']={'actions':sorted(FLOW_ACTIONS),'preserveStageValuesAndTimes':False,'interpolation':'Bounded quintic Bezier, shared pass-through velocity, C2 joins','followThrough':'Body leads head and hands; movement-driven hair and cloth settle after the gesture','durationsPreserved':True,'faceTimelineChanges':['Success','HeadClick']}
(ROOT/'Unity/body_action_map_v2.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'Unity/action_face_profiles_v2.json').write_text(json.dumps({'fps':30,'mouthTiles':MOUTH,'profiles':profiles},separators=(',',':')),encoding='utf-8')
# Keep opt-in manual emotion controls available, with a head-free face mask.
face_map=json.loads((ROOT/'Unity/expression_map_v1.json').read_text(encoding='utf-8'))
for e in face_map['expressions']:
    src=bpy.data.actions[e['clip']];name=e['clip'].replace('PetV1_Face_','PetV2_FaceStatic_')
    old=bpy.data.actions.get(name)
    if old:bpy.data.actions.remove(old)
    copy=src.copy();copy.name=name;copy.use_fake_user=True
    tr=arm.animation_data.nla_tracks.new();tr.name=name;tr.strips.new(name,1,copy);tr.mute=True;e['clip']=name
(ROOT/'Unity/expression_map_v2.json').write_text(json.dumps(face_map,indent=2),encoding='utf-8')
disable_animation();reset();arm.animation_data.action=bpy.data.actions['PetV2_Idle']
scene.frame_start=1;scene.frame_end=121;scene.frame_set(1)
scene['body_v2_action']='Idle';scene['body_v2_revision']=7;scene['body_v2_task_status']='active-local-acting-refinement-pass'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
print(json.dumps({'bodyActions':len(entries),'faceProcesses':len(profiles),'secondaryBones':len(secondary),'dragSource':'GLB #29 Formation_Pickup','saved':bpy.data.filepath}))
