"""Refine only eyelid/brow topology; fit a continuous closed-eye arc."""
import bpy,bmesh,json,pathlib,numpy as np
from mathutils import Vector
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
bpy.ops.chddd.expression(emotion='neutral')
body=bpy.data.objects['CH0069_Body'];mesh=body.data
before=len(mesh.vertices)
if not mesh.get('closed_curve_refined'):
    bm=bmesh.new();bm.from_mesh(mesh)
    deform=bm.verts.layers.deform.active
    def region(v):
        names=[body.vertex_groups[i].name for i,w in v[deform].items() if w>.05]
        if any('Xtra_eye' in n and 'blow' not in n for n in names):return 0
        if any('eyeblow' in n for n in names):return 2
        if any(n.startswith(('Bip001 eye_','Bip001 bone_eye_D_')) for n in names):return 1
        return 0
    edges=[e for e in bm.edges if region(e.verts[0]) and region(e.verts[0])==region(e.verts[1])]
    bmesh.ops.subdivide_edges(bm,edges=edges,cuts=3,use_grid_fill=True)
    bm.to_mesh(mesh);bm.free();mesh.update();mesh['closed_curve_refined']=True
keys=mesh.shape_keys.key_blocks;base=keys['Basis'];blink=keys['Pet_Blink']
upper=[(.0256,-.3901),(.0352,-.3891),(.0366,-.3962),(.0450,-.3995),(.0588,-.4009),(.0739,-.3988),(.0839,-.3952),(.0939,-.3885),(.0974,-.3843),(.1108,-.3967)]
lower=[(.0256,-.3901),(.0352,-.3774),(.0376,-.3662),(.0410,-.3615),(.0450,-.3594),(.0582,-.3576),(.0715,-.3560),(.0788,-.3580),(.0851,-.3621),(.0900,-.3672),(.0944,-.3744),(.0974,-.3843),(.1108,-.3967)]
def interp(samples,x):return float(np.interp(x,[p[0] for p in samples],[p[1] for p in samples]))
def arc(s):return -.3678-.012*((s-.065)/.043)**2
changed=[];iris=[];brows=[]
for v in mesh.vertices:
    groups=[(body.vertex_groups[g.group].name,g.weight) for g in v.groups]
    if any('Xtra_eye' in n and 'blow' not in n for n,w in groups):iris.append(v.index);continue
    if any('eyeblow' in n for n,w in groups):brows.append(v.index);continue
    up=sum(w for n,w in groups if n.startswith('Bip001 eye_'))
    down=sum(w for n,w in groups if n.startswith('Bip001 bone_eye_D_'))
    co=base.data[v.index].co;s=abs(co.z)
    if max(up,down)<.18 or co.x<.015 or not .023<s<.119:continue
    top=interp(upper,s);bottom=interp(lower,s)
    if up>=.18 and (down<.18 or co.y<(top+bottom)/2):
        offset=co.y-top
        y=arc(s)+offset*.30-.00035
    elif down>=.18:
        offset=co.y-bottom
        y=arc(s)+offset*.60+.00035
    else:continue
    blink.data[v.index].co=Vector((co.x,y,co.z));changed.append(v.index)
# Preserve the narrow brow strip around a smooth fitted center curve.
sx=np.array([abs(base.data[i].co.z) for i in brows])
sy=np.array([base.data[i].co.y for i in brows]);fit=np.polyfit(sx,sy,3)
for i in brows:
    co=base.data[i].co;s=abs(co.z);t=(s-.0248)/.0747
    center=-.442+.006*(2*t-1)**2
    residual=co.y-float(np.polyval(fit,s))
    keys['Pet_BrowSleepy'].data[i].co=Vector((co.x+.001,center+residual,co.z))
manifest=json.loads((OUT/'character_manifest.json').read_text(encoding='utf-8'))
manifest['closedEyeCurve']={'curve':'quadratic arc with refined eyelid/lash topology',
                          'beforeVertices':before,'afterVertices':len(mesh.vertices),
                          'eyelidVertices':len(changed),'browVertices':len(brows),
                          'irisVerticesUnchanged':len(iris)}
(OUT/'character_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.chddd.expression(emotion='sleepy')
print(json.dumps(manifest['closedEyeCurve']))
