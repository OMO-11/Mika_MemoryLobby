"""Widen the soft brow strip by 20 percent and add a shallow back surface."""
import bpy,bmesh,numpy as np,json,pathlib
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
bpy.ops.chddd.expression(emotion='neutral');body=bpy.data.objects['CH0069_Body'];mesh=body.data
if not mesh.get('soft_brow_thickness'):
    ids=[v.index for v in mesh.vertices if any('eyeblow' in body.vertex_groups[g.group].name for g in v.groups)]
    if not mesh.get('brow_width_scaled'):
        for key in mesh.shape_keys.key_blocks:
            x=np.array([abs(key.data[i].co.z) for i in ids]);y=np.array([key.data[i].co.y for i in ids]);fit=np.polyfit(x,y,3)
            for i in ids:
                co=key.data[i].co;center=float(np.polyval(fit,abs(co.z)))
                co.y=center+(co.y-center)*1.20
        mesh['brow_width_scaled']=True
    # Merge UV duplicates only within the narrow brow strip, then extrude its back.
    bm=bmesh.new();bm.from_mesh(mesh);deform=bm.verts.layers.deform.active
    brow_verts=[v for v in bm.verts if any('eyeblow' in body.vertex_groups[g].name for g in v[deform].keys())]
    bmesh.ops.remove_doubles(bm,verts=brow_verts,dist=1e-6)
    faces=[f for f in bm.faces if body.data.materials[f.material_index].name=='CHDDD_Eyebrow']
    front=set(v for f in faces for v in f.verts)
    extrusion=bmesh.ops.extrude_face_region(bm,geom=faces,use_keep_orig=True)
    new=[v for v in extrusion['geom'] if isinstance(v,bmesh.types.BMVert) and v not in front]
    for v in new:
        v.co.x-=.0004
        for layer in bm.verts.layers.shape.values():
            co=v[layer].copy();co.x-=.0004;v[layer]=co
    bmesh.ops.recalc_face_normals(bm,faces=[f for f in bm.faces if f.material_index==2])
    bm.to_mesh(mesh);bm.free();mesh.update();mesh['soft_brow_thickness']=True
material=bpy.data.materials['CHDDD_Eyebrow'];nodes=material.node_tree.nodes;links=material.node_tree.links
image=next(n.image for n in nodes if n.bl_idname=='ShaderNodeTexImage')
nodes.clear();tex=nodes.new('ShaderNodeTexImage');tex.image=image
diffuse=nodes.new('ShaderNodeBsdfDiffuse');diffuse.inputs['Color'].default_value=(1,1,1,1)
rgb=nodes.new('ShaderNodeShaderToRGB');links.new(diffuse.outputs[0],rgb.inputs[0])
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.1;ramp.color_ramp.elements[0].color=(.70,.66,.63,1)
ramp.color_ramp.elements[1].position=.65;ramp.color_ramp.elements[1].color=(1,1,1,1);links.new(rgb.outputs[0],ramp.inputs[0])
color=nodes.new('ShaderNodeMixRGB');color.blend_type='MULTIPLY';color.inputs[0].default_value=1
links.new(tex.outputs['Color'],color.inputs[1]);links.new(ramp.outputs[0],color.inputs[2])
emission=nodes.new('ShaderNodeEmission');links.new(color.outputs[0],emission.inputs[0])
output=nodes.new('ShaderNodeOutputMaterial');links.new(emission.outputs[0],output.inputs[0])
manifest=json.loads((OUT/'character_manifest.json').read_text(encoding='utf-8'))
manifest['softBrows']={'widthMultiplier':1.2,'depthMeters':.0004,'shading':'subtle toon light/shadow on a shallow brow strip'}
(OUT/'character_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(manifest['softBrows']))
