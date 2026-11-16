"""Compare the lively pass to the saved revision 3, including untouched curves."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
BASE=ROOT/'Checkpoints/V2_revision3_before_lively'
CHANGED={'Wave','Success','Confused','HeadClick','BodyClick'}
mapping=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
profiles=json.loads((ROOT/'Unity/action_face_profiles_v2.json').read_text())
old_profiles=json.loads((BASE/'Unity/action_face_profiles_v2.json').read_text())
old_profiles={p['name']:p for p in old_profiles['profiles']}
untouched_faces=[p['name'] for p in profiles['profiles'] if p['name'] not in CHANGED and p==old_profiles[p['name']]]
assert len(untouched_faces)==13,untouched_faces
current={a.name:a for a in bpy.data.actions if a.name.startswith('PetV2_')}
with bpy.data.libraries.load(str(BASE/'CHDDD_actions_v2.blend'),link=False) as (source,loaded):
    # All published performance curves, not just the five edited actions.
    names=tuple(n for n in source.actions if n in current)
    loaded.actions=list(names)
baseline=dict(zip(names,loaded.actions))

def curves(action):
    result={}
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    result[(curve.data_path,curve.array_index)]=curve
    return result

max_unchanged=0;unchanged=[];changed=[]
for name,a in current.items():
    label=name
    for prefix in ('PetV2_FaceProcess_','PetV2_Shapes_','PetV2_Mouth_','PetV2_'):
        if label.startswith(prefix):label=label[len(prefix):];break
    if label in CHANGED:continue
    old=baseline[name];ca,cb=curves(a),curves(old)
    assert ca.keys()==cb.keys(),name
    diff=max((abs(x.co.y-y.co.y) for key in ca for x,y in zip(ca[key].keyframe_points,cb[key].keyframe_points)),default=0)
    assert all(len(ca[k].keyframe_points)==len(cb[k].keyframe_points) for k in ca),name
    assert diff<1e-6,(name,diff)
    max_unchanged=max(max_unchanged,diff);unchanged.append(name)

exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'controls','exec'))
arm=bpy.data.objects['Bip001'];scene=bpy.context.scene
metric_bones=('Bip001 Head','Bip001 Pelvis','Bip001 L Hand','Bip001 R Hand')
for name in sorted(CHANGED):
    entry=next(e for e in mapping['actions'] if e['name']==name)
    snapshots={}
    for version,action in [('old',baseline['PetV2_'+name]),('new',current['PetV2_'+name])]:
        preview_body_v2(scene,name)
        arm.animation_data.action=action
        if action.slots:arm.animation_data.action_slot=action.slots[0]
        frames=[]
        for frame in range(1,entry['frames'][1]+1):
            scene.frame_set(frame);bpy.context.view_layer.update()
            frames.append({n:(arm.matrix_world@arm.pose.bones[n].matrix).copy() for n in metric_bones})
        snapshots[version]=frames
    delta={n:max((a[n].translation-b[n].translation).length for a,b in zip(snapshots['old'],snapshots['new'])) for n in metric_bones}
    angular={n:max(a[n].to_quaternion().rotation_difference(b[n].to_quaternion()).angle for a,b in zip(snapshots['old'],snapshots['new'])) for n in metric_bones}
    assert max(delta.values())>.004 or max(angular.values())>.06,(name,delta,angular)
    changed.append({'name':name,'maximumWorldPositionChange':delta,'maximumAngleChangeDegrees':{n:math.degrees(a) for n,a in angular.items()}})
preview_body_v2(scene,'Idle')
result={'passed':True,'revision':4,'designDirection':'User selected game-like lively interactions','changedActions':changed,'untouchedFaceProfiles':untouched_faces,'untouchedCurveActions':len(unchanged),'unchangedCurveMaximumDifference':max_unchanged,'comparisonCheckpoint':str(BASE),'visualQualityRequiresManualReview':True}
(ROOT/'body_v2_revision4_design_validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result))
