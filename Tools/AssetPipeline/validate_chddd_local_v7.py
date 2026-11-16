"""Regression and requested acting constraints against the user-reviewed revision 6."""
import bpy,json,math
from pathlib import Path
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD');CP=ROOT/'Checkpoints/V2_revision6_before_local'
scene=bpy.context.scene;arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body']
mapping=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
profiles=json.loads((ROOT/'Unity/action_face_profiles_v2.json').read_text(encoding='utf-8'))
assert mapping['revision']==7 and profiles['mouthTiles']==[300,706,705,604,605,305,401,200]
old_map=json.loads((CP/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
assert {e['name']:e['durationSeconds'] for e in mapping['actions']}=={e['name']:e['durationSeconds'] for e in old_map['actions']}
current={a.name:a for a in bpy.data.actions if a.name.startswith('PetV2_')}
with bpy.data.libraries.load(str(CP/'CHDDD_actions_v2.blend'),link=False) as (src,dst):
    names=tuple(n for n in src.actions if n.startswith('PetV2_') and not n.startswith(('PetV2_Shapes_','PetV2_Mouth_')));dst.actions=list(names)
old=dict(zip(names,dst.actions))
def curves(a):return {(c.data_path,c.array_index):c for l in a.layers for s in l.strips for bag in s.channelbags for c in bag.fcurves}
def equal_curves(a,b,filter=lambda k:True):
    a={k:v for k,v in curves(a).items() if filter(k)};b={k:v for k,v in curves(b).items() if filter(k)}
    return a.keys()==b.keys() and all(len(a[k].keyframe_points)==len(b[k].keyframe_points) and all(abs(x.co.y-y.co.y)<1e-6 for x,y in zip(a[k].keyframe_points,b[k].keyframe_points)) for k in a)
focus={'Confused','Success','Wave','HeadClick','BodyClick'}
unchanged=[]
for e in mapping['actions']:
    if e['name'] not in focus:
        n=e['clip'];assert equal_curves(current[n],old[n]),n;unchanged.append(e['name'])
assert len(unchanged)==13
for n in names:
    if n.startswith('PetV2_FaceStatic_'):assert equal_curves(current[n],old[n]),n
assert equal_curves(current['PetV2_HeadClick'],old['PetV2_HeadClick'])
assert equal_curves(current['PetV2_BodyClick'],old['PetV2_BodyClick'],lambda k:'bone_wing' not in k[0])
torso=('Bip001 Pelvis','Bip001 Spine','Bip001 Spine1','Bip001 Head','Bip001 L Foot','Bip001 R Foot','Bip001 L Toe0','Bip001 R Toe0')
assert equal_curves(current['PetV2_Success'],old['PetV2_Success'],lambda k:any(('"'+n+'"') in k[0] for n in torso))
old_profiles=json.loads((CP/'Unity/action_face_profiles_v2.json').read_text(encoding='utf-8'))['profiles'];old_profiles={p['name']:p for p in old_profiles}
unchanged_face=[]
for profile in profiles['profiles']:
    if profile['name'] in ('HeadClick','Success'):continue
    normalized=json.loads(json.dumps(profile))
    for v in normalized['samples']:assert v['mouthWeights'][7]==0;v['mouthWeights']=v['mouthWeights'][:7]
    assert normalized==old_profiles[profile['name']],profile['name'];unchanged_face.append(profile['name'])
assert len(unchanged_face)==16
for profile in profiles['profiles']:
    assert all(len(s['mouthWeights'])==8 and abs(sum(s['mouthWeights'])-1)<1e-6 for s in profile['samples'])
    if profile['name']=='Success':assert all(s['blink']>.999 and s['browLift']>.79 for i,s in enumerate(profile['samples']) if .36<=i/(len(profile['samples'])-1)<=.56)
    if profile['name']=='HeadClick':
        assert all(s['blink']>.999 and s['mouthWeights'][7]>.999 for i,s in enumerate(profile['samples']) if .10<=i/(len(profile['samples'])-1)<=.30)
        assert all(s['mouthWeights'][1]==0 and s['mouthWeights'][2]==0 for s in profile['samples'])

for tr in arm.animation_data.nla_tracks:tr.mute=True;tr.is_solo=False
def sample(action,t,count):
    arm.animation_data.action=action;arm.animation_data.action_slot=action.slots[0]
    f=1+t*count;scene.frame_set(math.floor(f),subframe=f%1);bpy.context.view_layer.update()
def pos(n):return arm.matrix_world@arm.pose.bones[n].head
rise=[]
for label,action in [('before',old['PetV2_Success']),('after',current['PetV2_Success'])]:
    sample(action,.40,102);rise.append({s:pos('Bip001 '+s+' Hand').z for s in ('L','R')})
assert all(rise[1][s]-rise[0][s]>.05 for s in ('L','R')),rise
scratch=[];normals=[]
for t in (.42,.445,.47,.495,.52,.545,.57):
    sample(current['PetV2_Confused'],t,102);w=pos('Bip001 L Hand');f=pos('Bip001 L Finger1')-w
    across=pos('Bip001 L Finger2')-pos('Bip001 L Finger0');normal=across.cross(f).normalized()
    target=pos('Bip001 Head')-w;target-=f.normalized()*target.dot(f.normalized());target.normalize()
    normals.append(normal.dot(target));scratch.append(pos('Bip001 L Finger1'))
assert min(normals)>.70,normals
scratch_range=max((a-b).length for a in scratch for b in scratch);assert scratch_range>.002,scratch_range
# Last wave flows through the old stop point into the return.
sample(current['PetV2_Wave'],.78-1/108,108);a=pos('Bip001 L Hand')
sample(current['PetV2_Wave'],.78+1/108,108);b=pos('Bip001 L Hand');return_speed=(b-a).length*15
assert return_speed>.015,return_speed
wing_rows=[]
for name,phase,count in [('Wave',.43,108),('BodyClick',.80,78)]:
    qs=[]
    for action in (old['PetV2_'+name],current['PetV2_'+name]):
        sample(action,phase,count);qs.append([arm.pose.bones['bone_wing_'+s+'_01'].matrix_basis.to_quaternion() for s in ('l','r')])
    angles=[min(q.rotation_difference(r).angle,2*math.pi-q.rotation_difference(r).angle) for q,r in zip(qs[0],qs[1])]
    assert min(angles)>.01,(name,angles)
    wing_rows.append({'name':name,'phase':phase,'leftRightRootDifferenceRadians':angles})

# Same-body counterfactual, shoulder side panels only. Intersections elsewhere
# and sewn contacts are outside this diagnostic; visual review is also required.
mesh=body.data;mesh.calc_loop_triangles();groups={g.index:g.name for g in body.vertex_groups}
weight={k:[sum(g.weight for g in v.groups if groups[g.group].startswith(prefix)) for v in mesh.vertices] for k,prefix in [('cape',('bone_cape_l_','bone_cape_r_')),('arm',('Bip001 L UpperArm','Bip001 R UpperArm','Bip001 L Forearm','Bip001 R Forearm'))]}
polys={k:[tuple(t.vertices) for t in mesh.loop_triangles if sum(weight[k][i] for i in t.vertices)/3>.55] for k in weight}
baseline=json.loads((ROOT/'body_v2_revision7_shoulder_uncorrected.json').read_text(encoding='utf-8'))
def intersections():
    ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());verts=[ev.matrix_world@v.co for v in ev.data.vertices]
    trees={k:BVHTree.FromPolygons(verts,p,all_triangles=True) for k,p in polys.items()}
    return sum(not set(polys['cape'][a]).intersection(polys['arm'][b]) for a,b in trees['cape'].overlap(trees['arm']))
contacts=[]
for phase in (.20,.27,.34,.48,.55,.62):
    frame=round(phase*102);sample(current['PetV2_Success'],frame/102,102)
    transforms={b.name:b.matrix_basis.copy() for b in arm.pose.bones};arm.animation_data.action=None
    for n,m in transforms.items():arm.pose.bones[n].matrix_basis=m
    bpy.context.view_layer.update();corrected=intersections()
    for n,m in baseline[frame].items():arm.pose.bones[n].matrix_basis=Matrix(m)
    bpy.context.view_layer.update();uncorrected=intersections()
    contacts.append({'phase':frame/102,'uncorrectedPairs':uncorrected,'correctedPairs':corrected})
assert sum(r['correctedPairs'] for r in contacts)<sum(r['uncorrectedPairs'] for r in contacts),contacts
result={'passed':True,'revision':7,'baselineRevision':6,'untouchedBodyActions':unchanged,'untouchedFaceProfiles':unchanged_face,
 'successTorsoAndFeetCurvesPreserved':True,'headClickBodyPreserved':True,'bodyClickOnlyWingBonesChanged':True,
 'successWristHeightBeforeAfter':rise,'confusedPalmInwardDots':normals,'confusedScratchFingerRangeMeters':scratch_range,
 'waveReturnWristSpeedMetersPerSecond':return_speed,'wingLocalPoseDifferences':wing_rows,'shoulderSamePoseIntersectionComparison':contacts,
 'headClickTriangleMouthAndClosedEyesVerified':True,'headClickNoSmileVerified':True,'successClosedEyesAndRaisedBrowsVerified':True,
 'allDurationsPreserved':True,'allMouthWeightsNormalized':True,'shoulderComparisonIsNotCompleteCollisionCertification':True}
(ROOT/'body_v2_revision7_local_validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
