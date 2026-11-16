import bpy,json,pathlib
bpy.ops.chddd.expression(emotion='neutral')
body=bpy.data.objects['CH0069_Body']
bpy.context.view_layer.update()
ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
world=[list(ev.matrix_world@v.co) for v in mesh.vertices];ev.to_mesh_clear()
data={'world':world,'local':[list(v.co) for v in body.data.vertices],
      'materials':[m.name for m in body.data.materials],
      'faces':[{'v':list(p.vertices),'mat':p.material_index} for p in body.data.polygons],
      'weights':[[[body.vertex_groups[g.group].name,g.weight] for g in v.groups] for v in body.data.vertices],
      'uv':[[list(body.data.uv_layers.active.data[i].uv) for i in p.loop_indices] for p in body.data.polygons]}
path=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Previews/face_geometry.json')
path.write_text(json.dumps(data))
print(data['materials'])
