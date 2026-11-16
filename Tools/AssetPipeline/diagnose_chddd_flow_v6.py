"""World-space speed/acceleration review, before and after the flow pass."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
scene=bpy.context.scene;arm=bpy.data.objects['Bip001']
mapping=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text())
names=('Confused','Success','HeadClick','BodyClick')
bones=('Bip001 Head','Bip001 Spine1','Bip001 Pelvis','Bip001 L UpperArm','Bip001 R UpperArm','Bip001 L Forearm','Bip001 R Forearm','Bip001 L Hand','Bip001 R Hand')
current={n:bpy.data.actions['PetV2_'+n] for n in names}
with bpy.data.libraries.load(str(ROOT/'Checkpoints/V2_revision5_before_flow/CHDDD_actions_v2.blend'),link=False) as (src,dst):dst.actions=['PetV2_'+n for n in names]
old=dict(zip(names,dst.actions))
for track in arm.animation_data.nla_tracks:track.mute=True;track.is_solo=False
def log(q):
    if q.w<0:q.negate()
    v=Vector((q.x,q.y,q.z));return v*(2*math.atan2(v.length,q.w)/v.length) if v.length>1e-10 else Vector()
rows=[]
for name in names:
    entry=next(e for e in mapping['actions'] if e['name']==name);count=entry['frames'][1]
    result={'name':name}
    for label,action in [('before',old[name]),('after',current[name])]:
        arm.animation_data.action=action;arm.animation_data.action_slot=action.slots[0];samples=[]
        for f in range(1,count+1):
            scene.frame_set(f);bpy.context.view_layer.update();samples.append({n:(arm.matrix_world@arm.pose.bones[n].matrix).copy() for n in bones})
        values={}
        for n in bones:
            angular=[log(b[n].to_quaternion()@a[n].to_quaternion().inverted())*30 for a,b in zip(samples,samples[1:])]
            acceleration=[(b-a)*30 for a,b in zip(angular,angular[1:])]
            linear=[(b[n].translation-a[n].translation)*30 for a,b in zip(samples,samples[1:])]
            speed=[math.degrees(v.length) for v in angular];acc=[math.degrees(v.length) for v in acceleration]
            values[n]={'peakAngularSpeed':max(speed),'peakAngularStep':max(speed)/30,'peakAngularAcceleration':max(acc),
                       'peakAccelerationFrame':acc.index(max(acc))+2,'peakLinearSpeed':max(v.length for v in linear),
                       'angularSpeeds':speed,'angularAccelerations':acc}
        result[label]=values
    rows.append(result)
(ROOT/'body_v2_revision6_motion_diagnostics.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print(json.dumps([{'name':r['name'],'headPeakAcceleration':[r[k]['Bip001 Head']['peakAngularAcceleration'] for k in ('before','after')],
                  'worstStep':max(v['peakAngularStep'] for v in r['after'].values()),
                  'worstAccelerationBone':max(r['after'],key=lambda n:r['after'][n]['peakAngularAcceleration'])} for r in rows]))
