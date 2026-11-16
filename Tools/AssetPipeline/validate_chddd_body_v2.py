import bpy,json,math,hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'v2_controls','exec'))
scene=bpy.context.scene;arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body']
mapping=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
profiles={p['name']:p for p in json.loads((ROOT/'Unity/action_face_profiles_v2.json').read_text())['profiles']}
face=json.loads((ROOT/'character_manifest.json').read_text(encoding='utf-8'))['faceBones']
face=[n for n in face if n!='Bip001 Head']
names=[b.name for b in arm.pose.bones if b.name not in face]
ends={};results=[];bad=[]
for entry in mapping['actions']:
    name=entry['name'];preview_body_v2(scene,name)
    frames=entry['frames'];samples=[];feet=[];secondary_motion={};finite=True
    for f in sorted(set([1,2,round(frames[1]*.20),round(frames[1]*.40),round(frames[1]*.65),frames[1]-1,frames[1]])):
        scene.frame_set(f);bpy.context.view_layer.update()
        p={n:arm.pose.bones[n].matrix_basis.copy() for n in names};samples.append((f,p))
        feet.append([arm.matrix_world@arm.pose.bones['Bip001 '+side+' Foot'].head for side in ('L','R')])
        for n in names:
            if n.startswith(('bone_hair','bone_cape','bone_skirt')):
                secondary_motion.setdefault(n,[]).append(p[n].to_quaternion())
        finite &= all(math.isfinite(v) for m in p.values() for row in m for v in row)
    first,last=samples[0][1],samples[-1][1];ends[name]=(first,last)
    endpoint=max(abs(first[n][i][j]-last[n][i][j]) for n in names for i in range(4) for j in range(4))
    foot_range=max(max(p[side][axis] for p in feet)-min(p[side][axis] for p in feet) for side in range(2) for axis in range(3))
    moving_accessories=sum(max(q[0].rotation_difference(x).angle for x in q)>.002 for q in secondary_motion.values())
    shape_data=profiles[name]['samples'];face_range=max(max(s[k] for s in shape_data)-min(s[k] for s in shape_data) for k in ('blink','sad','annoyed','sleepy','browLift','gazeX'))
    mouth_changes=sum(max(s['mouthWeights'][i] for s in shape_data)-min(s['mouthWeights'][i] for s in shape_data)>.2 for i in range(len(shape_data[0]['mouthWeights'])))
    face_valid=all(all(math.isfinite(v) and -1e-6<=v<=1.00001 for k,v in s.items() if k in ('blink','sad','annoyed','sleepy')) and abs(sum(s['mouthWeights'])-1)<1e-5 for s in shape_data)
    facial_endpoint=max(abs(shape_data[0][k]-shape_data[-1][k]) for k in ('blink','sad','annoyed','sleepy','gazeX','browLift'))
    facial_endpoint=max(facial_endpoint,max(abs(a-b) for a,b in zip(shape_data[0]['mouthWeights'],shape_data[-1]['mouthWeights'])))
    okay=finite and face_valid and (not entry['loop'] or (endpoint<3e-5 and facial_endpoint<1e-5))
    if name in ('Thinking','Working','HeadPat','Sleep','Confused','HeadClick'):okay &= foot_range<.0001
    if name in ('Wave','Success','BodyClick') and mapping.get('revision',0)<5:okay &= foot_range<.0001
    if name not in ('Idle','Walk','Dragging') and not name.endswith(('_Enter','_Exit')):okay &= moving_accessories>=10 and (face_range>.02 or mouth_changes>0)
    if not okay:bad.append(name)
    results.append({'name':name,'loop':entry['loop'],'endpointMatrixError':endpoint,'faceEndpointError':facial_endpoint,'footRange':foot_range,
                    'movingHairCapeSkirtBones':moving_accessories,'facialRange':face_range,'changingMouthCells':mouth_changes,'passed':okay})
connections=[]
for name in ('HeadPat','Dragging','Sleep'):
    for a,ai,b,bi in [('Idle',0,name+'_Enter',0),(name+'_Enter',1,name,0),(name,1,name+'_Exit',0),(name+'_Exit',1,'Idle',0)]:
        pa,pb=ends[a][ai],ends[b][bi];diff=max(abs(pa[n][i][j]-pb[n][i][j]) for n in names for i in range(4) for j in range(4))
        connections.append({'from':a,'to':b,'error':diff,'passed':diff<3e-5})
# Compare recovered pickup head/pelvis world trajectories against the actual GLB animation.
native=json.loads((ROOT/'body_source_pickup_v2.json').read_text())['frames'];preview_body_v2(scene,'Dragging')
source_error=0
for frame in (1,31,91,181,241,300):
    scene.frame_set(frame);bpy.context.view_layer.update()
    for name,key in [('Bip001 Head','headWorld'),('Bip001 Pelvis','pelvisWorld')]:
        source_error=max(source_error,((arm.matrix_world@arm.pose.bones[name].head)-Vector(native[frame-1][key])).length)
source_digest=hashlib.sha256((ROOT.parent/'CH0069/Model/CH0069.glb').read_bytes()).hexdigest()
report={'revision':mapping['revision'],'bodyActions':results,'connections':connections,'pickupOneBasedIndex':29,'pickupClip':'CH0069_Formation_Pickup',
        'pickupWorldTrajectoryMaxError':source_error,'sourceUnchanged':source_digest=='57816e04521d12a9615cb5081fc7db90bb7fe0ae8f36d3cec3ed04dfad9b67a0',
        'passed':not bad and all(c['passed'] for c in connections) and source_error<1e-4}
(ROOT/'body_v2_blender_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
preview_body_v2(scene,'Idle')
print(json.dumps({'passed':report['passed'],'badActions':bad,'badConnections':[c for c in connections if not c['passed']],'sourceError':source_error,'actions':results}))
if not report['passed']:raise RuntimeError('V2 draft needs another refinement pass')
