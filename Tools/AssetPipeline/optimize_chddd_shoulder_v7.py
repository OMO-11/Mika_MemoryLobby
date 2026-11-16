"""Compare local cape corrections on the same raised-arm poses; do not save scene."""
import bpy,json,math
from pathlib import Path
from mathutils import Matrix,Quaternion,Vector
from mathutils.bvhtree import BVHTree
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'controls','exec'))
scene=bpy.context.scene;arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body'];mesh=body.data
group={g.index:g.name for g in body.vertex_groups};mesh.calc_loop_triangles()
weights={kind:[sum(g.weight for g in v.groups if group[g.group].startswith(prefix)) for v in mesh.vertices] for kind,prefix in [('cape',('bone_cape_l_','bone_cape_r_')),('arm',('Bip001 L UpperArm','Bip001 R UpperArm','Bip001 L Forearm','Bip001 R Forearm'))]}
polys={k:[tuple(t.vertices) for t in mesh.loop_triangles if sum(weights[k][i] for i in t.vertices)/3>.55] for k in weights}
samples=[]
for t in (.20,.27,.34,.48,.55,.62):
    e=preview_body_v2(scene,'Success');f=1+t*(e['frames'][1]-1);scene.frame_set(math.floor(f),subframe=f%1);bpy.context.view_layer.update()
    samples.append((t,{b.name:b.matrix_basis.copy() for b in arm.pose.bones}))
for tr in arm.animation_data.nla_tracks:tr.mute=True
arm.animation_data.action=None
def rotate(n,angle):
    bone=arm.pose.bones[n];m=arm.matrix_world@bone.matrix
    bone.matrix=arm.matrix_world.inverted()@Matrix.LocRotScale(m.translation,Quaternion((0,1,0),angle)@m.to_quaternion(),m.to_scale());bpy.context.view_layer.update()
rows=[]
for angle in (-.3,-.15,0,.15,.3,.45,.6,.75):
 for shift in (0,.006,.012):
    counts=[]
    for t,pose in samples:
        for n,m in pose.items():arm.pose.bones[n].matrix_basis=m
        bpy.context.view_layer.update()
        for side,sign in (('l',1),('r',-1)):
            rotate('bone_cape_'+side+'_01',sign*angle);rotate('bone_cape_'+side+'_02',sign*angle*.5)
            bone=arm.pose.bones['bone_cape_'+side+'_01'];m=bone.matrix.copy();m.translation+=arm.matrix_world.inverted().to_3x3()@Vector((sign*shift,0,0));bone.matrix=m;bpy.context.view_layer.update()
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());vertices=[ev.matrix_world@v.co for v in ev.data.vertices]
        trees={k:BVHTree.FromPolygons(vertices,p,all_triangles=True) for k,p in polys.items()}
        counts.append(sum(not set(polys['cape'][a]).intersection(polys['arm'][b]) for a,b in trees['cape'].overlap(trees['arm'])))
    rows.append({'additionalWorldYRadians':angle,'additionalOutwardShiftMeters':shift,'intersectionPairs':counts,'total':sum(counts)})
rows.sort(key=lambda r:r['total']+abs(r['additionalWorldYRadians'])*10+r['additionalOutwardShiftMeters']*500)
(ROOT/'body_v2_revision7_shoulder_candidates.json').write_text(json.dumps(rows,indent=2),encoding='utf-8');print(json.dumps(rows[:6]))
