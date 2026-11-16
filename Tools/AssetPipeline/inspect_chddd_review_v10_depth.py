import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V10_TextureReview')
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'controls','exec'))
preview_review9_expression(bpy.context.scene);bpy.context.scene.frame_set(37);bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get();body=bpy.data.objects['CH0069_Body'].evaluated_get(deps);decal=bpy.data.objects['CHDDD_TestSqueeze_EyeLines'].evaluated_get(deps)
results=[]
for sign in (-1,1):
    for z0 in (.03,.04,.05,.06,.07,.08,.09,.10):
        z=z0*sign
        for y in (-.40,-.39,-.38,-.375,-.37,-.365,-.36,-.35):
            origin=Vector((.3,y,z));direction=Vector((-1,0,0));dh,dp,dn,di=decal.ray_cast(origin,direction)
            for n in range(15):
                h,p,no,i=body.ray_cast(origin,direction)
                if not h:break
                mi=body.data.polygons[i].material_index
                if mi in (1,3):
                    if dh:results.append({'z':z,'y':y,'bodyMaterial':mi,'bodyX':p.x,'decalX':dp.x,'clearance':dp.x-p.x})
                    break
                origin=p+Vector((-.0001,0,0))
out={'minClearance':min(r['clearance'] for r in results),'samples':len(results),'intersections':[r for r in results if r['clearance']<0],'worst':sorted(results,key=lambda r:r['clearance'])[:12]}
(ROOT/'decal_depth_check.json').write_text(json.dumps(out,indent=2),encoding='utf-8');print(json.dumps(out))
