"""Held full-body poses. Executed in the authoring rig-math namespace."""
READABLE={'Wave','Success','Confused','HeadClick','BodyClick'}
PERFORMANCE_INTENTS_ZH={
 'Confused':'停下、后缩，手靠脸颊并歪头约23度，保持疑问，再缓慢恢复。',
 'Success':'收手蓄势，踮脚握拳上提，保持喜悦，再缓冲回落并满足地放松。',
 'Wave':'注意用户、收身准备、探身招手，展示问候姿态后挥动两次，再柔和收势。',
 'HeadClick':'双肩和双手一起受惊，短暂停顿，歪头认出用户，再放松回应。',
 'BodyClick':'后缩、收肩，双手靠近胸前，脚尖内扣；视线移开后短暂回望，再恢复。'}
DEFAULT_CONTROLS={
 'hip':(0,0,0),'hips':(0,0,0),'spine':(0,0,0),'head':(0,0,0),
 'shoulder':(0,0),'left':(.18,-.14,.60),'right':(-.12,-.16,.50),
 'leftWeight':0,'rightWeight':0,'palm':0,'fist':0,'rightFist':0,
 'heel':(0,0),'toeIn':(0,0),'cheek':0,'wave':0}

def stage(t,**values):return (t,dict(DEFAULT_CONTROLS,**values))

STAGES={
 'Confused':[
  stage(0),stage(.10), # Interrupt and briefly stop before reacting.
  stage(.22,hip=(.004,.025,-.018),spine=(-6,0,0),head=(-3,-8,-5),shoulder=(4,3)),
  stage(.40,hip=(.012,.017,-.013),hips=(0,-2,0),spine=(-4,3,0),head=(2,-23,-5),shoulder=(7,2),cheek=1,leftWeight=1,palm=.65,right=(-.08,-.19,.52),rightWeight=.55),
  stage(.58,hip=(.012,.017,-.013),hips=(0,-2,0),spine=(-4,3,0),head=(2,-23,-5),shoulder=(7,2),cheek=1,leftWeight=1,palm=.65,right=(-.08,-.19,.52),rightWeight=.55),
  stage(.76,hip=(.006,.009,-.006),spine=(0,1,0),head=(0,-13,-2),cheek=.75,leftWeight=.85,palm=.50,rightWeight=.35),
  stage(.94),stage(1)],
 'Wave':[
  stage(0),stage(.12,hip=(-.012,.012,-.020),hips=(0,2,0),spine=(5,3,-5),head=(2,-3,6),left=(.11,-.18,.55),leftWeight=.5,rightWeight=.2),
  stage(.28,hip=(.016,-.015,-.005),hips=(0,-3,0),spine=(8,-5,-5),head=(-4,-7,5),shoulder=(7,2),left=(.25,-.17,.79),leftWeight=1,palm=1,right=(-.075,-.20,.53),rightWeight=.65,heel=(0,10),wave=-.65),
  stage(.37,hip=(.016,-.015,-.005),hips=(0,-3,0),spine=(8,-5,-5),head=(-4,-7,5),shoulder=(7,2),left=(.25,-.17,.79),leftWeight=1,palm=1,right=(-.075,-.20,.53),rightWeight=.65,heel=(0,10),wave=-.65),
  stage(.47,hip=(.020,-.018,-.004),hips=(0,-3,0),spine=(8,-4,-7),head=(-2,-9,3),shoulder=(7,2),left=(.29,-.16,.77),leftWeight=1,palm=1,right=(-.07,-.20,.53),rightWeight=.65,heel=(0,12),wave=.80),
  stage(.57,hip=(.014,-.014,-.005),hips=(0,-2,0),spine=(7,-4,-4),head=(-3,-7,4),shoulder=(6,2),left=(.22,-.17,.78),leftWeight=1,palm=1,right=(-.075,-.20,.53),rightWeight=.65,heel=(0,8),wave=-.55),
  stage(.67,hip=(.016,-.012,-.005),spine=(6,-3,-4),head=(-1,-6,3),shoulder=(5,1),left=(.26,-.16,.77),leftWeight=1,palm=1,rightWeight=.5,heel=(0,6),wave=.45),
  stage(.78,hip=(.008,-.006,-.003),spine=(3,-2,0),head=(1,-5,0),left=(.22,-.16,.72),leftWeight=.9,palm=.8,rightWeight=.35),
  stage(.95),stage(1)],
 'Success':[
  stage(0),stage(.15,hip=(0,.005,-.028),hips=(3,0,0),spine=(9,0,0),head=(6,0,0),shoulder=(3,3),left=(.075,-.19,.51),right=(-.075,-.19,.51),leftWeight=.8,rightWeight=.8,fist=.8,rightFist=.8),
  stage(.30,hip=(0,-.008,.012),hips=(-2,0,0),spine=(-6,-3,0),head=(-7,-5,0),shoulder=(8,8),left=(.23,-.17,.76),right=(-.23,-.17,.71),leftWeight=1,rightWeight=1,palm=.8,fist=1,rightFist=1,heel=(25,25)),
  stage(.48,hip=(0,-.008,.012),hips=(-2,0,0),spine=(-6,-3,0),head=(-7,-5,0),shoulder=(8,8),left=(.23,-.17,.76),right=(-.23,-.17,.71),leftWeight=1,rightWeight=1,palm=.8,fist=1,rightFist=1,heel=(25,25)),
  stage(.62,hip=(0,.002,-.018),spine=(5,0,0),head=(6,-6,0),shoulder=(3,3),left=(.13,-.19,.62),right=(-.09,-.21,.56),leftWeight=1,rightWeight=.9,palm=.6,fist=.8,rightFist=.7),
  stage(.76,hip=(0,-.005,-.004),spine=(0,0,0),head=(1,-8,-5),left=(.13,-.18,.59),right=(-.08,-.20,.54),leftWeight=.8,rightWeight=.7,fist=.5,rightFist=.5),
  stage(.96),stage(1)],
 'HeadClick':[
  stage(0),stage(.18,hip=(0,.025,-.027),hips=(-3,0,0),spine=(-8,0,0),head=(-11,5,0),shoulder=(9,9),left=(.25,-.18,.67),right=(-.25,-.18,.67),leftWeight=1,rightWeight=1,palm=.85),
  stage(.30,hip=(0,.025,-.027),hips=(-3,0,0),spine=(-8,0,0),head=(-11,5,0),shoulder=(9,9),left=(.25,-.18,.67),right=(-.25,-.18,.67),leftWeight=1,rightWeight=1,palm=.85),
  stage(.55,hip=(-.008,.005,-.008),spine=(2,3,0),head=(4,14,-5),left=(.14,-.19,.57),right=(-.08,-.20,.55),leftWeight=.7,rightWeight=.7,palm=.4),
  stage(.70,hip=(-.008,.005,-.008),spine=(2,3,0),head=(4,14,-5),left=(.14,-.19,.57),right=(-.08,-.20,.55),leftWeight=.7,rightWeight=.7,palm=.4),
  stage(.96),stage(1)],
 'BodyClick':[
  stage(0),stage(.13,hip=(0,.022,-.020),spine=(-7,0,-7),head=(-4,4,10),shoulder=(5,5),left=(.12,-.21,.53),right=(-.12,-.21,.53),leftWeight=.65,rightWeight=.65),
  stage(.35,hip=(0,.008,-.026),hips=(3,0,-3),spine=(9,0,-8),head=(7,-9,20),shoulder=(6,6),left=(.055,-.21,.56),right=(-.055,-.21,.56),leftWeight=1,rightWeight=1,fist=.30,rightFist=.30,toeIn=(-7,7)),
  stage(.55,hip=(0,.008,-.026),hips=(3,0,-3),spine=(9,0,-8),head=(7,-9,20),shoulder=(6,6),left=(.055,-.21,.56),right=(-.055,-.21,.56),leftWeight=1,rightWeight=1,fist=.30,rightFist=.30,toeIn=(-7,7)),
  stage(.70,hip=(0,.006,-.018),spine=(6,0,-4),head=(4,-9,8),shoulder=(4,4),left=(.065,-.21,.55),right=(-.065,-.21,.55),leftWeight=.9,rightWeight=.9,toeIn=(-4,4)),
  stage(.96),stage(1)]}

FLOW_ACTIONS={'Confused','Success','HeadClick','BodyClick'}
# Phase offsets inside each interval; values and first/second derivatives at its
# boundaries remain unchanged. The body leads and hands finish slightly later.
FLOW_OFFSETS={'hip':.045,'hips':.035,'spine':.015,'head':-.025,
              'shoulder':.015,'heel':.025,'toeIn':.025,
              'left':-.035,'right':-.035,'leftWeight':-.035,'rightWeight':-.035,
              'palm':-.045,'fist':-.045,'rightFist':-.045,'cheek':-.035}

def flow_tangents(times,values):
    """Shared, bounded velocities; stop only at holds and scalar reversals."""
    slopes=[(b-a)/(tb-ta) for ta,tb,a,b in zip(times,times[1:],values,values[1:])]
    m=[0.0]*len(values)
    for i in range(1,len(values)-1):
        a,b=slopes[i-1],slopes[i]
        if a*b<=0:continue
        h0=times[i]-times[i-1];h1=times[i+1]-times[i]
        w0=2*h1+h0;w1=h1+2*h0
        m[i]=(w0+w1)/(w0/a+w1/b)
    # Quintic Bezier derivative control points must not change sign. Limiting
    # shared tangents preserves C2 continuity without overshooting key poses.
    for i,s in enumerate(slopes):
        total=abs(m[i])+abs(m[i+1]);limit=2.4*abs(s)
        if total>limit and total:
            factor=limit/total;m[i]*=factor;m[i+1]*=factor
    return m

def flow_scalar(times,values,t,offset=0):
    tangents=flow_tangents(times,values)
    for i,(ta,tb) in enumerate(zip(times,times[1:])):
        if t<=tb:
            h=tb-ta;u=max(0,min(1,(t-ta)/h))
            u+=offset*64*u**3*(1-u)**3
            a,b=values[i],values[i+1];ma,mb=tangents[i],tangents[i+1]
            points=(a,a+h*ma/5,a+2*h*ma/5,b-2*h*mb/5,b-h*mb/5,b)
            return sum(c*p*u**j*(1-u)**(5-j) for j,(c,p) in enumerate(zip((1,5,10,10,5,1),points)))
    return values[-1]

def sample_controls(name,t):
    stages=STAGES[name]
    if name in FLOW_ACTIONS:
        times=[s[0] for s in stages];result={}
        for key,value in stages[0][1].items():
            if isinstance(value,tuple):
                result[key]=tuple(flow_scalar(times,[s[1][key][i] for s in stages],t,FLOW_OFFSETS.get(key,0)) for i in range(len(value)))
            else:result[key]=flow_scalar(times,[s[1][key] for s in stages],t,FLOW_OFFSETS.get(key,0))
        return result
    for (ta,a),(tb,b) in zip(stages,stages[1:]):
        if t<=tb:
            u=max(0,min(1,(t-ta)/(tb-ta)));w=u**3*(10-15*u+6*u*u)
            return {k:tuple(x+(y-x)*w for x,y in zip(a[k],b[k])) if isinstance(a[k],tuple) else a[k]+(b[k]-a[k])*w for k in a}
    return stages[-1][1]

def foot_pose(side,heel,turn):
    pivot=reference_world['Bip001 '+side+' Toe0'].translation
    r=Matrix.Rotation(math.radians(turn),4,'Z')@Matrix.Rotation(math.radians(heel),4,'X')
    return Matrix.Translation(pivot)@r@Matrix.Translation(-pivot)@anchors[side]['foot']

def readable_pose(name,t):
    reset();v=sample_controls(name,t)
    for bone,key in (('Bip001 Pelvis','hips'),('Bip001 Spine1','spine'),('Bip001 Head','head')):
        for axis,degrees in zip(((1,0,0),(0,1,0),(0,0,1)),v[key]):rotate(bone,axis,math.radians(degrees))
    for i,side in enumerate(('L','R')):
        rotate('Bip001 '+side+' Clavicle',(0,1,0),math.radians((-1 if side=='L' else 1)*v['shoulder'][i]))
    shift('Bip001 Pelvis',v['hip'])
    for i,side in enumerate(('L','R')):
        leg_ik(side,foot_pose(side,v['heel'][i],v['toeIn'][i]))
        # Keep the toe pad planted while the heel rolls up or the foot turns inward.
        toe=arm.pose.bones['Bip001 '+side+' Toe0'];base=reference_world[toe.name]
        toe.matrix=arm.matrix_world.inverted()@Matrix.LocRotScale(base.translation,Quaternion((0,0,1),math.radians(v['toeIn'][i]))@base.to_quaternion(),base.to_scale())
        bpy.context.view_layer.update()
    h=world_head('Bip001 Head')
    target=Vector(v['left'])
    if v['cheek']:
        # Head-relative cheek target remains beside the face during the held tilt.
        cheek=h+Vector((.13,-.025,.15));target=target.lerp(cheek,v['cheek'])
    hand_ik('L',target,(.28,-.04,.50),v['leftWeight'])
    hand_ik('R',v['right'],(-.27,-.05,.46),v['rightWeight'])
    hand=arm.pose.bones['Bip001 L Hand'];loc,_,sc=hand.matrix_basis.decompose()
    hand.matrix_basis=Matrix.LocRotScale(loc,BASE[hand.name].to_quaternion(),sc);bpy.context.view_layer.update()
    fingers_up('L',v['palm']);rotate(hand.name,(0,1,0),.48*v['wave'])
    if name in ('Success','HeadClick'):fingers_up('R',(.70 if name=='Success' else .85)*v['rightWeight'])
    curl('L',.90*v['fist']);curl('R',.75*v['rightFist'])
    return {n:arm.pose.bones[n].matrix_basis.copy() for n in body_names}

PERFORMANCE_NOTES={
 'Wave':{'tier':'special','intensity':'high','keyPoseSeconds':1.08,'holdSeconds':.32,'intent':'Notice, gather energy, lean toward the user, two clear greeting waves, soften and settle','footPolicy':'Right toe pivot; left foot planted','recommendedFrequency':'Welcome or deliberate greeting; never repeated by idle scheduler'},
 'Success':{'tier':'emotion','intensity':'medium-high','keyPoseSeconds':1.10,'holdSeconds':.61,'intent':'Gather hands and crouch, rise onto toes with a fist pump, hold delight, land softly and relax','footPolicy':'Both toes planted; heels rise, no airborne jump','recommendedFrequency':'Single response to meaningful success; avoid repeated tool-event playback'},
 'Confused':{'tier':'emotion','intensity':'medium-high','keyPoseSeconds':1.45,'holdSeconds':.61,'intent':'Stop, recoil, bring a hand beside the cheek and tilt about 23 degrees, hold the question, slowly recover','footPolicy':'Both feet planted','recommendedFrequency':'One readable reaction; not a continuous loop'},
 'HeadClick':{'tier':'special','intensity':'high-short','keyPoseSeconds':.40,'holdSeconds':.22,'intent':'Startle with shoulders and both hands, pause, recognize the user with a head tilt, settle','footPolicy':'Both feet planted','recommendedFrequency':'Sudden click only; must be throttled by the interaction host'},
 'BodyClick':{'tier':'emotion','intensity':'medium-high','keyPoseSeconds':1.05,'holdSeconds':.52,'intent':'Recoil, draw both hands to chest and turn feet inward, look away, briefly look back, release','footPolicy':'Toe pivots during inward foot turn','recommendedFrequency':'Single shy response; not constant idle behavior'}}
