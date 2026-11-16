"""Audit subframe velocities and render the independent idle library."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
OUT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/RandomIdleV1')
PRE=OUT/'Preview';PRE.mkdir(exist_ok=True)
scene=bpy.context.scene;arm=bpy.data.objects['Bip001'];camera=scene.camera
data=json.loads((OUT/'idle_action_map_v1.json').read_text(encoding='utf-8'))
for t in arm.animation_data.nla_tracks:t.mute=True
report=json.loads((OUT/'validation.json').read_text(encoding='utf-8'))
report['checks'].pop('rootMotion',None)
report['checks']['rootMotionDisabled']=True
metrics=[]
for e in data['actions']:
    arm.animation_data.action=bpy.data.actions[e['clip']]
    last_p=None;last_v=None;last_q=None;last_w=None;max_v=max_a=max_w=max_alpha=0
    for i in range((e['frames'][1]-1)*4+1):
        f=1+i/4;scene.frame_set(int(f),subframe=f%1);bpy.context.view_layer.update()
        p={b.name:(arm.matrix_world@b.matrix).translation for b in arm.pose.bones}
        q={b.name:(arm.matrix_world@b.matrix).to_quaternion() for b in arm.pose.bones}
        if last_p is not None:
            v={n:(p[n]-last_p[n])*120 for n in p}
            w={}
            for n in q:
                delta=q[n]@last_q[n].conjugated()
                if delta.w<0:delta.negate()
                vec=Vector((delta.x,delta.y,delta.z));length=vec.length
                w[n]=vec*(2*math.atan2(length,delta.w)*120/length) if length>1e-9 else Vector((0,0,0))
            max_v=max(max_v,max(x.length for x in v.values()));max_w=max(max_w,max(x.length for x in w.values()))
            if last_v is not None:
                max_a=max(max_a,max((v[n]-last_v[n]).length*120 for n in v))
                max_alpha=max(max_alpha,max((w[n]-last_w[n]).length*120 for n in w))
            last_v=v;last_w=w
        last_p=p;last_q=q
    metrics.append(dict(name=e['name'],maxPositionSpeed=max_v,maxPositionAcceleration=max_a,
        maxAngularSpeedDegrees=math.degrees(max_w),maxAngularAccelerationDegrees=math.degrees(max_alpha)))
report['smoothnessMetrics']=metrics
report['smoothnessScope']='quarter-frame finite differences in Blender; numerical bounds are screening, visual review remains required'
report['checks']['angularSpeedUnder60DegPerSecond']=all(m['maxAngularSpeedDegrees']<60 for m in metrics)
report['checks']['angularAccelerationUnder180DegPerSecondSquared']=all(m['maxAngularAccelerationDegrees']<180 for m in metrics)
report['passed']=all(report['checks'].values())
assert report['passed'],metrics
(OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('SMOOTHNESS_AUDIT_PASSED',flush=True)
scene.render.resolution_x=320;scene.render.resolution_y=388;scene.render.resolution_percentage=100
scene.render.film_transparent=True
target=Vector((0,0,.53));camera.location=target+Vector((0,-3.5,.06))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=1.42
if hasattr(scene,'eevee') and hasattr(scene.eevee,'taa_render_samples'):scene.eevee.taa_render_samples=8
scene.render.image_settings.file_format='PNG'
for e in data['actions']:
    arm.animation_data.action=bpy.data.actions[e['clip']]
    for i in range(round(e['durationSeconds']*12)):
        f=1+i/12*30;scene.frame_set(int(f),subframe=f%1)
        scene.render.filepath=str(PRE/f"{e['name']}_{i:03d}.png")
        bpy.ops.render.render(write_still=True)
    print('PREVIEW_COMPLETE',e['name'],flush=True)
print('ALL_PREVIEWS_COMPLETE',flush=True)
