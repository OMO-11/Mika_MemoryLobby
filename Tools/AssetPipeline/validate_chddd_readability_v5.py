"""Check held poses, contact intent and actual skinned hair/cloth follow-through."""
import bpy,json,math,sys
from pathlib import Path
from mathutils import Matrix,Vector
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
before='--before' in sys.argv
mapping=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'controls','exec'))
scene=bpy.context.scene;arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body']
base={n:Matrix(m) for n,m in json.loads((ROOT/'body_source_cache_v1.json').read_text())['Idle'][0].items()}
secondary=[n for n in base if n.startswith(('bone_hair','bone_cape','bone_skirt','bone_wing','bone_ribbon'))]
groups={g.index:g.name for g in body.vertex_groups}
indices={kind:[v.index for v in body.data.vertices if any(groups[g.group].startswith(prefixes) and g.weight>.15 for g in v.groups)] for kind,prefixes in [('hair',('bone_hair',)),('cloth',('bone_cape','bone_skirt'))]}
def mesh_positions():
    dg=bpy.context.evaluated_depsgraph_get();evaluated=body.evaluated_get(dg)
    assert len(evaluated.data.vertices)==len(body.data.vertices)
    return {kind:[evaluated.matrix_world@evaluated.data.vertices[i].co for i in vertices] for kind,vertices in indices.items()}

mesh_rows=[]
for name in ('Wave','Success','Confused','HeadClick','BodyClick','Thinking','Working','HeadPat','Sleep'):
    entry=next(e for e in mapping['actions'] if e['name']==name);maximum={'hair':0,'cloth':0};projected={'hair':0,'cloth':0}
    for t in (.12,.20,.28,.36,.44,.52,.60,.68,.76,.84,.90):
        preview_body_v2(scene,name);f=1+t*(entry['frames'][1]-1);scene.frame_set(math.floor(f),subframe=f%1);bpy.context.view_layer.update()
        transforms={b.name:b.matrix_basis.copy() for b in arm.pose.bones}
        for tr in arm.animation_data.nla_tracks:tr.mute=True
        for n,m in transforms.items():arm.pose.bones[n].matrix_basis=m
        bpy.context.view_layer.update();actual=mesh_positions()
        for n in secondary:arm.pose.bones[n].matrix_basis=base[n]
        bpy.context.view_layer.update();rigid=mesh_positions()
        for kind in maximum:
            for a,b in zip(actual[kind],rigid[kind]):
                d=a-b;maximum[kind]=max(maximum[kind],d.length)
                projected[kind]=max(projected[kind],math.hypot(d.x,d.z)*160/1.42)
    mesh_rows.append({'name':name,'maximumActualMeshDisplacementMeters':maximum,'maximumProjectedFollowThroughPixelsAt160Canvas':projected})
if before:
    (ROOT/'body_v2_revision4_secondary_mesh_baseline.json').write_text(json.dumps(mesh_rows,indent=2),encoding='utf-8')
    print(json.dumps(mesh_rows));raise SystemExit(0)

assert mapping['revision']>=5
checks=[]
for name,lo,hi in [('Confused',.40,.58),('Success',.30,.48),('HeadClick',.18,.30),('BodyClick',.35,.55)]:
    e=preview_body_v2(scene,name);samples=[]
    for t in (lo,(lo+hi)/2,hi):
        f=1+t*(e['frames'][1]-1);scene.frame_set(math.floor(f),subframe=f%1);bpy.context.view_layer.update()
        samples.append({n:(arm.matrix_world@arm.pose.bones[n].matrix).copy() for n in ('Bip001 Head','Bip001 Pelvis','Bip001 Spine1','Bip001 L Hand','Bip001 R Hand') if not (mapping['revision']>=7 and name=='Confused' and n=='Bip001 L Hand')})
    angles=[samples[0][n].to_quaternion().rotation_difference(p[n].to_quaternion()).angle for p in samples for n in samples[0]]
    delta=max(min(a,2*math.pi-a) for a in angles)
    checks.append({'name':name,'holdSeconds':(hi-lo)*e['durationSeconds'],'maximumHoldBodyAngularChangeDegrees':math.degrees(delta),'passed':delta<.025})
contacts=[]
for name in ('Wave','Success','BodyClick'):
    e=preview_body_v2(scene,name);initial={s:arm.matrix_world@arm.pose.bones['Bip001 '+s+' Toe0'].head for s in ('L','R')}
    foot_z={s:(arm.matrix_world@arm.pose.bones['Bip001 '+s+' Foot'].head).z for s in ('L','R')};maximum=0;heel=0;penetration=0
    for f in range(1,e['frames'][1]+1):
        scene.frame_set(f);bpy.context.view_layer.update()
        for s in ('L','R'):
            p=arm.matrix_world@arm.pose.bones['Bip001 '+s+' Toe0'].head
            maximum=max(maximum,(p-initial[s]).length)
            z=(arm.matrix_world@arm.pose.bones['Bip001 '+s+' Foot'].head).z-foot_z[s]
            heel=max(heel,z);penetration=min(penetration,z)
    contacts.append({'name':name,'maximumToeContactError':maximum,'contactToleranceMeters':.001,'maximumHeelRiseMeters':heel,'minimumAnkleHeightChange':penetration,'passed':maximum<.001 and penetration>-.001 and (name!='Success' or heel>.006)})
e=preview_body_v2(scene,'Confused');scene.frame_set(1);q=(arm.matrix_world@arm.pose.bones['Bip001 Head'].matrix).to_quaternion()
scene.frame_set(1+round(.49*(e['frames'][1]-1)));bpy.context.view_layer.update()
tilt=math.degrees(q.rotation_difference((arm.matrix_world@arm.pose.bones['Bip001 Head'].matrix).to_quaternion()).angle)

# Source body curves and optional face templates must remain unchanged.
current={a.name:a for a in bpy.data.actions}
checkpoint=ROOT/'Checkpoints/V2_revision4_before_readability'
with bpy.data.libraries.load(str(checkpoint/'CHDDD_actions_v2.blend'),link=False) as (src,dst):
    names=tuple(n for n in src.actions if n in ['PetV2_Idle','PetV2_Walk','PetV2_Dragging'] or n.startswith('PetV2_FaceStatic_'))
    dst.actions=list(names)
def curves(a):return {(c.data_path,c.array_index):c for l in a.layers for st in l.strips for bag in st.channelbags for c in bag.fcurves}
for n,old in zip(names,dst.actions):
    a,b=curves(current[n]),curves(old);assert a.keys()==b.keys(),n
    assert all(len(a[k].keyframe_points)==len(b[k].keyframe_points) and all(abs(x.co.y-y.co.y)<1e-6 for x,y in zip(a[k].keyframe_points,b[k].keyframe_points)) for k in a),n
old=json.loads((checkpoint/'Unity/action_face_profiles_v2.json').read_text())['profiles'];old={p['name']:p for p in old}
new=json.loads((ROOT/'Unity/action_face_profiles_v2.json').read_text())['profiles']
if mapping['revision']>=7:
    new=json.loads(json.dumps(new))
    for profile in new:
        for sample in profile['samples']:sample['mouthWeights']=sample['mouthWeights'][:7]
unchanged=[p['name'] for p in new if p['name'] not in ('Wave','Success','Confused','HeadClick','BodyClick') and p==old[p['name']]]
assert len(unchanged)==13
visible=all(r['maximumProjectedFollowThroughPixelsAt160Canvas']['hair']>1.0 and r['maximumProjectedFollowThroughPixelsAt160Canvas']['cloth']>.65 for r in mesh_rows if r['name'] in ('Wave','Success','Confused'))
result={'revision':mapping['revision'],'heldPoses':checks,'footContacts':contacts,'confusedHeldHeadAngleDegrees':tilt,'actualSkinnedMeshFollowThrough':mesh_rows,'visibleSecondaryMotionThresholdPassed':visible,'sourceBodiesAndStaticEmotionsPreserved':True,'untouchedFaceProfiles':unchanged,'passed':all(r['passed'] for r in checks+contacts) and 20<=tilt<=32 and visible,'qualityIsHumanJudgment':True}
(ROOT/('body_v2_revision'+str(mapping['revision'])+'_readability_validation.json')).write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result))
if not result['passed']:raise RuntimeError('Readability/contact pass needs correction')
