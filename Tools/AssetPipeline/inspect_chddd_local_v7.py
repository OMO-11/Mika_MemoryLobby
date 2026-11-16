import bpy,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'controls','exec'))
scene=bpy.context.scene;arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body']
group={g.index:g.name for g in body.vertex_groups}
mesh=body.data;mesh.calc_loop_triangles()
weight={kind:[sum(g.weight for g in v.groups if group[g.group].startswith(prefix)) for v in mesh.vertices] for kind,prefix in [('cape',('bone_cape',)),('arm',('Bip001 L UpperArm','Bip001 R UpperArm','Bip001 L Forearm','Bip001 R Forearm'))]}
polys={kind:[tuple(t.vertices) for t in mesh.loop_triangles if sum(weight[kind][i] for i in t.vertices)/3>.55] for kind in weight}
rows=[]
for name in ('Confused','Success','HeadClick'):
    e=preview_body_v2(scene,name)
    for t in (.20,.30,.40,.48,.55,.62,.76):
        f=1+t*(e['frames'][1]-1);scene.frame_set(math.floor(f),subframe=f%1);bpy.context.view_layer.update()
        def pos(n):return arm.matrix_world@arm.pose.bones[n].head
        r={'name':name,'phase':t,'head':list(pos('Bip001 Head')),'leftHand':list(pos('Bip001 L Hand')),'rightHand':list(pos('Bip001 R Hand'))}
        if name=='Success':
            ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());vertices=[ev.matrix_world@v.co for v in ev.data.vertices]
            trees={k:BVHTree.FromPolygons(vertices,p,all_triangles=True) for k,p in polys.items()}
            intersections=[(a,b) for a,b in trees['cape'].overlap(trees['arm']) if not set(polys['cape'][a]).intersection(polys['arm'][b])]
            r['shoulderCapeArmIntersectionPairs']=len(intersections)
        rows.append(r)
(ROOT/'body_v2_revision7_local_inspection.json').write_text(json.dumps(rows,indent=2),encoding='utf-8');print(json.dumps(rows))
