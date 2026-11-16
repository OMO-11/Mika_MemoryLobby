"""Review-gate checks: preserve accepted data, verify requested acting and test isolation."""
import bpy,json,hashlib,math
from pathlib import Path
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V8_FirstReview')
SOURCE=ROOT.parent.parent
body=bpy.data.objects['CH0069_Body'];arm=bpy.data.objects['Bip001']
keys=body.data.shape_keys.key_blocks
def curves(a):
    return [(c.data_path,c.array_index,[(tuple(p.co),p.interpolation) for p in c.keyframe_points]) for layer in a.layers for strip in layer.strips for bag in strip.channelbags for c in bag.fcurves]
result={'revision':8,'status':'pending-user-review','checks':{},'measurements':{}}
def check(name,ok):
    result['checks'][name]=bool(ok)
    if not ok:print('CHECK_FAILED',name,flush=True)
# Load source data in a separate library namespace without modifying it.
with bpy.data.libraries.load(str(SOURCE/'CHDDD_actions_v2.blend'),link=False) as (src,dst):
    dst.actions=[a for a in src.actions if a.startswith('PetV2_')]
    dst.objects=['CH0069_Body']
loaded={a.name.rsplit('.',1)[0] if a.name[-4:-3]=='.' else a.name:a for a in dst.actions if a}
changed={'PetV2_'+s for s in ('Success','HeadClick')}|{p+s for p in ('PetV2_FaceProcess_','PetV2_Shapes_','PetV2_Mouth_') for s in ('Success','HeadClick')}
check('unrelated_actions_exact',all(curves(bpy.data.actions[n])==curves(a) for n,a in loaded.items() if n not in changed))
old=dst.objects[0].data
if old:
    check('accepted_shape_keys_exact',all(all((a.co-b.co).length==0 for a,b in zip(k.data,keys[k.name].data)) for k in old.shape_keys.key_blocks))
    check('topology_preserved',len(old.vertices)==len(body.data.vertices) and len(old.polygons)==len(body.data.polygons))
check('source_manifest_unchanged',(ROOT/'character_manifest.json').read_bytes()==(SOURCE/'character_manifest.json').read_bytes())
def group_curves(action,keep):return [(p,i,v) for p,i,v in curves(action) if any('"'+n+'"' in p for n in keep)]
torso=['Bip001 Pelvis','Bip001 Spine','Bip001 Spine1','Bip001 Head','Bip001 Neck','Bip001 L Thigh','Bip001 R Thigh','Bip001 L Calf','Bip001 R Calf','Bip001 L Foot','Bip001 R Foot','Bip001 L Toe0','Bip001 R Toe0']
check('success_torso_and_feet_exact',group_curves(bpy.data.actions['PetV2_Success'],torso)==group_curves(loaded['PetV2_Success'],torso))
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'controls','exec'))
def pos(n):return arm.matrix_world@arm.pose.bones[n].head
preview_body_v2(bpy.context.scene,'Success');bpy.context.scene.frame_set(43)
measure=[]
for side,sign in (('L',1),('R',-1)):
    d=pos('Bip001 '+side+' Hand')-pos('Bip001 '+side+' UpperArm')
    measure.append({'side':side,'outward':sign*d.x,'height':d.z,'diagonalDegrees':math.degrees(math.atan2(abs(d.x),d.z))})
result['measurements']['successArms']=measure
check('success_Y_diagonal',all(x['outward']>.07 and x['height']>.09 and 25<x['diagonalDegrees']<65 for x in measure))
check('success_closed_eyes_and_upward_brows',keys['Pet_Blink'].value>.99 and keys['Pet_BrowHappy_Review'].value>.99)
preview_body_v2(bpy.context.scene,'HeadClick');bpy.context.scene.frame_set(13)
head=pos('Bip001 Head');hands=[list(pos('Bip001 '+s+' Hand')-head) for s in ('L','R')]
result['measurements']['headCoverHandsRelative']=hands
check('headclick_hands_close_to_head',all(v[2]>.09 and abs(v[0])<.16 for v in hands))
profiles=json.loads((ROOT/'Unity/action_face_profiles_v2.json').read_text(encoding='utf-8'))
headprofile=next(x for x in profiles['profiles'] if x['name']=='HeadClick');cells=profiles['mouthTiles']
tail=headprofile['samples'][round(.55*54):round(.70*54)+1]
check('headclick_closed_mouth_annoyed',all(v['annoyed']>.5 and v['mouthWeights'][cells.index(305)]>.99 for v in tail))
iris=[v.index for v in body.data.vertices if any('Xtra_eye' in body.vertex_groups[g.group].name and 'blow' not in body.vertex_groups[g.group].name for g in v.groups)]
check('test_does_not_deform_irises',all((keys['Pet_TestSqueeze_Review'].data[i].co-keys['Basis'].data[i].co).length==0 for i in iris))
preview_review8_expression(bpy.context.scene);bpy.context.scene.frame_set(37)
check('test_full_closed_cover',keys['Pet_Blink'].value>.99 and keys['Pet_TestSqueeze_Review'].value>.99)
preview_body_v2(bpy.context.scene,'Idle');bpy.context.scene.frame_set(1)
check('test_does_not_leak_into_standard_actions',keys['Pet_TestSqueeze_Review'].value==0 and keys['Pet_BrowHappy_Review'].value==0)
for owner in (body.data.shape_keys,bpy.data.materials['CHDDD_MouthAtlas'].node_tree):
    if owner.animation_data:owner.animation_data.action=None
for k in keys:k.value=0
result['passed']=all(result['checks'].values())
(ROOT/'review_validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result),flush=True)
if not result['passed']:raise RuntimeError('Review draft verification failed; see review_validation.json')
