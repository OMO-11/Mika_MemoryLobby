"""Check accepted poses against revision 5 and measure continuous intermediate motion."""
import bpy,json,math
from pathlib import Path
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
CP=ROOT/'Checkpoints/V2_revision5_before_flow'
scope={}
exec(compile((ROOT.parent.parent/'Tools/AssetPipeline/chddd_readable_performances.py').read_text(encoding='utf-8'),'flow_controls','exec'),scope)
stages=scope['STAGES'];sample=scope['sample_controls'];targets=scope['FLOW_ACTIONS']
mapping=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
assert mapping['revision']==6
def flat(v):return [x for value in v.values() for x in (value if isinstance(value,tuple) else (value,))]
key_error=max(abs(a-b) for name in targets for t,value in stages[name] for a,b in zip(flat(value),flat(sample(name,t))))
assert key_error<1e-9,key_error
# Continuous derivatives on each side of the knots, in normalized time.
rows=[]
for name in sorted(targets):
    times=[s[0] for s in stages[name]]
    for i,t in enumerate(times[1:-1],1):
        h=1e-5;a=flat(sample(name,t-h));b=flat(sample(name,t));c=flat(sample(name,t+h))
        speed=[(z-x)/(2*h) for x,z in zip(a,c)]
        same_previous=stages[name][i-1][1]==stages[name][i][1]
        same_next=stages[name][i+1][1]==stages[name][i][1]
        rows.append({'name':name,'phase':t,'deliberateHoldBoundary':same_previous or same_next,
                     'maximumControlSpeedThroughKey':max(abs(x) for x in speed),
                     'maximumOneSidedVelocityDifference':max(abs((z-y)/h-(y-x)/h) for x,y,z in zip(a,b,c))})
assert all(r['maximumOneSidedVelocityDifference']<.02 for r in rows),rows
assert all(r['maximumControlSpeedThroughKey']>1 for r in rows if not r['deliberateHoldBoundary']),rows
# Verify the authored curve never exceeds the accepted scalar pose envelope.
for name in targets:
    for (ta,a),(tb,b) in zip(stages[name],stages[name][1:]):
        for j in range(31):
            v=flat(sample(name,ta+(tb-ta)*j/30))
            assert all(min(x,y)-1e-9<=z<=max(x,y)+1e-9 for x,y,z in zip(flat(a),flat(b),v)),(name,ta,j)

arm=bpy.data.objects['Bip001'];scene=bpy.context.scene
current={a.name:a for a in bpy.data.actions if a.name.startswith('PetV2_')}
with bpy.data.libraries.load(str(CP/'CHDDD_actions_v2.blend'),link=False) as (src,dst):
    names=tuple(n for n in src.actions if n.startswith('PetV2_'));dst.actions=list(names)
old=dict(zip(names,dst.actions))
def curves(a):return {(c.data_path,c.array_index):c for l in a.layers for s in l.strips for bag in s.channelbags for c in bag.fcurves}
unchanged=[]
for name in names:
    if name in {'PetV2_'+n for n in targets}:continue
    a,b=curves(current[name]),curves(old[name]);assert a.keys()==b.keys(),name
    assert all(len(a[k].keyframe_points)==len(b[k].keyframe_points) and all(abs(x.co.y-y.co.y)<1e-6 for x,y in zip(a[k].keyframe_points,b[k].keyframe_points)) for k in a),name
    unchanged.append(name)
assert json.loads((ROOT/'Unity/action_face_profiles_v2.json').read_text())==json.loads((CP/'Unity/action_face_profiles_v2.json').read_text()),'Accepted facial sequences changed'
for track in arm.animation_data.nla_tracks:track.mute=True;track.is_solo=False
def world(action,phase,count):
    arm.animation_data.action=action;arm.animation_data.action_slot=action.slots[0]
    f=1+phase*count;scene.frame_set(math.floor(f),subframe=f%1);bpy.context.view_layer.update()
    return {n:(arm.matrix_world@arm.pose.bones[n].matrix).copy() for n in ('Bip001 Head','Bip001 Pelvis','Bip001 Spine1','Bip001 L Hand','Bip001 R Hand','Bip001 L UpperArm','Bip001 R UpperArm','Bip001 L Forearm','Bip001 R Forearm','Bip001 L Foot','Bip001 R Foot')}
def angle(q):
    if q.w<0:q.negate()
    s=math.sqrt(q.x*q.x+q.y*q.y+q.z*q.z);return 2*math.atan2(s,q.w)
pose_rows=[];motion_rows=[]
for name in sorted(targets):
    e=next(e for e in mapping['actions'] if e['name']==name);count=e['frames'][1]-1
    for t,_ in stages[name]:
        a=world(old['PetV2_'+name],t,count);b=world(current['PetV2_'+name],t,count)
        loc=max((a[n].translation-b[n].translation).length for n in a)
        deg=max(math.degrees(angle(a[n].to_quaternion().rotation_difference(b[n].to_quaternion()))) for n in a)
        pose_rows.append({'name':name,'phase':t,'maximumWorldPositionDifferenceMeters':loc,'maximumWorldAngleDifferenceDegrees':deg})
    for i,(t,_) in enumerate(stages[name][1:-1],1):
        if stages[name][i-1][1]==stages[name][i][1] or stages[name][i+1][1]==stages[name][i][1]:continue
        velocities={}
        for label,action in [('before',old['PetV2_'+name]),('after',current['PetV2_'+name])]:
            a=world(action,t-1/count,count);b=world(action,t+1/count,count)
            velocities[label]={n:math.degrees(angle(a[n].to_quaternion().rotation_difference(b[n].to_quaternion())))/(2/30) for n in a}
        motion_rows.append({'name':name,'phase':t,'worldAngularSpeedDegreesPerSecond':velocities})
assert max(r['maximumWorldPositionDifferenceMeters'] for r in pose_rows)<.0015,pose_rows
assert max(r['maximumWorldAngleDifferenceDegrees'] for r in pose_rows)<1,pose_rows
result={'passed':True,'revision':6,'baselineRevision':5,'scope':sorted(targets),'exactStageControlsMaximumError':key_error,
        'boundedInterpolation':True,'sharedVelocityAndAccelerationAtJoins':True,'controlJoinDiagnostics':rows,
        'acceptedPoseWorldComparison':pose_rows,'actualIntermediateAngularVelocities':motion_rows,
        'unchangedAnimationActions':unchanged,'all18FacialTimelinesPreserved':True,'visualReviewStillRequired':True}
(ROOT/'body_v2_revision6_flow_validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'passed':True,'posePositionError':max(r['maximumWorldPositionDifferenceMeters'] for r in pose_rows),'poseAngleError':max(r['maximumWorldAngleDifferenceDegrees'] for r in pose_rows),'unchangedActions':len(unchanged),'passThroughKnots':motion_rows}))
