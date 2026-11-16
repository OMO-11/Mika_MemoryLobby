import bpy,bmesh,json
body=bpy.data.objects['CH0069_Body'];copy=body.data.copy()
bm=bmesh.new();bm.from_mesh(copy)
print('shapes',list(bm.verts.layers.shape.keys()))
deform=bm.verts.layers.deform.active
def lid(v):
    return any(body.vertex_groups[i].name.startswith(('Bip001 eye_','Bip001 bone_eye_D_')) and w>.05 for i,w in v[deform].items())
edges=[e for e in bm.edges if all(lid(v) for v in e.verts)]
count=len(edges)
bmesh.ops.subdivide_edges(bm,edges=edges,cuts=3,use_grid_fill=True)
bm.to_mesh(copy);bm.free()
print(json.dumps({'edges':count,'vertices':len(copy.vertices),'shape_lengths':[len(k.data) for k in copy.shape_keys.key_blocks]}))
bpy.data.meshes.remove(copy)
