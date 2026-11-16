import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'v2_controls','exec'))
scene=bpy.context.scene;arm=bpy.data.objects['Bip001']
mapping=json.loads((ROOT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
def rotation_vector(q):
    if q.w<0:q.negate()
    # atan2 preserves tiny rotations when float32 quaternion.w rounds to 1.
    vector=Vector((q.x,q.y,q.z));length=vector.length
    return vector*(2*math.atan2(length,q.w)/length) if length>1e-10 else Vector()
rows=[]
for entry in mapping['actions']:
    if not entry['loop']:continue
    preview_body_v2(scene,entry['name']);end=entry['frames'][1];samples=[]
    for frame in (1,2,end-1,end):
        scene.frame_set(frame);bpy.context.view_layer.update()
        samples.append({b.name:b.rotation_quaternion.copy() for b in arm.pose.bones})
    errors=[]
    for name in samples[0]:
        a,b,c,d=(s[name] for s in samples)
        velocity0=rotation_vector(a.rotation_difference(b))*30
        velocity1=rotation_vector(c.rotation_difference(d))*30
        errors.append(((velocity1-velocity0).length,name,velocity0.length,velocity1.length))
    errors.sort(reverse=True)
    secondary_errors=[err for err,n,va,vb in errors if n.startswith(('bone_hair','bone_cape','bone_skirt','bone_foot_ribbon','bone_wing','bone_ribbon'))]
    rows.append({'name':entry['name'],'secondaryVelocityDifferenceMax':max(secondary_errors,default=0),'largestLoopVelocityDifferences':[{'bone':n,'differenceRadiansPerSecond':err,'firstSpeed':va,'lastSpeed':vb} for err,n,va,vb in errors[:8]]})
(ROOT/'body_v2_loop_velocity_diagnostics.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print(json.dumps(rows))
