"""Run inside interactive Blender via blender_rpc.py. Preserve the existing scene."""
import bpy
import json
import pathlib
import shutil
import struct
from mathutils import Vector

ROOT = pathlib.Path('W:/Machine/VSCode/Mika_Gift')
SOURCE = ROOT / 'Material/CH0069'
OUTPUT = ROOT / 'Material/CHDDD'
OUTPUT.mkdir(parents=True, exist_ok=True)
(OUTPUT / 'Textures').mkdir(exist_ok=True)
(OUTPUT / 'Previews').mkdir(exist_ok=True)
scene = bpy.data.scenes.new('CHDDD_Restoration')
bpy.context.window.scene = scene
scene.render.fps = 30
bpy.ops.import_scene.gltf(filepath=str(SOURCE / 'Model/CH0069.glb'))
armature = next(o for o in scene.objects if o.type == 'ARMATURE')
body = next(o for o in scene.objects if o.type == 'MESH' and o.name.startswith('CH0069_Body'))
character = next(o for o in scene.objects if o.name == 'CH0069')
for track in armature.animation_data.nla_tracks:
    track.mute = True
armature.animation_data.action = None
scene.frame_set(1)
for bone in armature.pose.bones:
    bone.matrix_basis.identity()

# Unity geometry -> glTF reflection in X -> Blender's Y-up to Z-up conversion.
record = next(m for m in json.loads((SOURCE / 'Metadata/meshes.json').read_text()) if m['name'] == 'CH0069_Halo')
vb = (SOURCE / record['vertexBuffer']['file']).read_bytes()
ib = (SOURCE / record['indexBuffer']['file']).read_bytes()
rows = [struct.unpack_from('<8f', vb, i * 32) for i in range(record['vertexCount'])]
indices = struct.unpack('<' + 'H' * (len(ib)//2), ib)
vertices = [(-r[0], -r[2], r[1]) for r in rows]
faces = [(indices[i], indices[i+2], indices[i+1]) for i in range(0, len(indices), 3)]
mesh = bpy.data.meshes.new('CHDDD_Halo_Geometry')
mesh.from_pydata(vertices, [], faces)
mesh.update()
uv = mesh.uv_layers.new(name='UVMap')
for poly in mesh.polygons:
    poly.use_smooth = True
    for loop_index in poly.loop_indices:
        r = rows[mesh.loops[loop_index].vertex_index]
        uv.data[loop_index].uv = (r[6], r[7])
halo_parent = next(o for o in scene.objects if o.name == 'CH0069_Halo')
halo = bpy.data.objects.new('CHDDD_Halo_Restored', mesh)
scene.collection.objects.link(halo)
halo.parent = halo_parent

material_sources = {}
for name in ['Body', 'Hair', 'Face', 'Eyebrow', 'EyeMouth', 'Weapon', 'Halo']:
    material_sources[name] = json.loads((SOURCE / 'Materials' / ('CH0069_' + name + '.material.json')).read_text())
    for value in material_sources[name]['textures'].values():
        if value.get('file'):
            shutil.copy2(SOURCE / value['file'], OUTPUT / 'Textures' / pathlib.Path(value['file']).name)

def image_for(file, data=False):
    image = bpy.data.images.load(str(OUTPUT / 'Textures' / pathlib.Path(file).name), check_existing=True)
    if data:
        image.colorspace_settings.name = 'Non-Color'
    return image

def build_material(part):
    source = material_sources[part]
    material = bpy.data.materials.new('CHDDD_' + part)
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    output = nodes.new('ShaderNodeOutputMaterial'); output.location = (650, 0)
    texture = nodes.new('ShaderNodeTexImage'); texture.name = 'Source_MainTexture'
    texture.image = image_for(source['textures']['_MainTex']['file']); texture.location = (-700, 150)
    emission = nodes.new('ShaderNodeEmission'); emission.location = (400, 0)
    if part == 'Halo':
        emission.inputs['Strength'].default_value = 2.2
        links.new(texture.outputs['Color'], emission.inputs['Color'])
    elif part in ['EyeMouth', 'Eyebrow']:
        links.new(texture.outputs['Color'], emission.inputs['Color'])
    else:
        diffuse = nodes.new('ShaderNodeBsdfDiffuse'); diffuse.location = (-700, -200)
        diffuse.inputs['Color'].default_value = (1, 1, 1, 1)
        rgb = nodes.new('ShaderNodeShaderToRGB'); rgb.location = (-500, -200)
        links.new(diffuse.outputs[0], rgb.inputs[0])
        ramp = nodes.new('ShaderNodeValToRGB'); ramp.name = 'Toon_Shadow_Bands'; ramp.location = (-300, -200)
        ramp.color_ramp.interpolation = 'CONSTANT'
        shadow = source['colors'].get('_ShadowTint', [0.78, 0.72, 0.82, 1])
        ramp.color_ramp.elements[0].position = 0.2
        ramp.color_ramp.elements[0].color = tuple(shadow[:3]) + (1,)
        ramp.color_ramp.elements[1].position = 0.52
        ramp.color_ramp.elements[1].color = (1,1,1,1)
        links.new(rgb.outputs[0], ramp.inputs[0])
        multiply = nodes.new('ShaderNodeMixRGB'); multiply.blend_type = 'MULTIPLY'; multiply.inputs[0].default_value = 1
        multiply.location = (100, 100)
        links.new(texture.outputs['Color'], multiply.inputs[1]); links.new(ramp.outputs['Color'], multiply.inputs[2])
        color_output = multiply.outputs[0]
        if part == 'Hair':
            spec = nodes.new('ShaderNodeTexImage'); spec.name = 'Source_HairSpec'; spec.location = (-650, 450)
            spec.image = image_for(source['textures']['_HairSpecTex']['file'], data=True)
            add = nodes.new('ShaderNodeMixRGB'); add.blend_type = 'SCREEN'; add.inputs[0].default_value = 0.12; add.location = (250, 200)
            links.new(color_output, add.inputs[1]); links.new(spec.outputs['Color'], add.inputs[2]); color_output = add.outputs[0]
        links.new(color_output, emission.inputs['Color'])
    links.new(emission.outputs[0], output.inputs['Surface'])
    material['source_shader'] = source['shader']['name']
    material['reconstruction'] = 'Eevee toon approximation; Unity uses accompanying custom shader'
    return material

materials = {part: build_material(part) for part in material_sources}
for obj in [o for o in scene.objects if o.type == 'MESH' and o.name.startswith('CH0069_')]:
    for index, mat in enumerate(obj.data.materials):
        part = mat.name.split('CH0069_')[-1].split('.')[0]
        if part in materials:
            obj.data.materials[index] = materials[part]
halo.data.materials.append(materials['Halo'])

# Mouth polygons occupy the lower-left 1/4 UV region in the source EyeMouth slot.
# The original _MouthTileTex scale is (0.5,0.5), resulting in one 1/8 atlas tile.
mouth = bpy.data.materials.new('CHDDD_MouthAtlas')
mouth.use_nodes = True
mouth.surface_render_method = 'DITHERED'
nodes, links = mouth.node_tree.nodes, mouth.node_tree.links
nodes.clear()
out = nodes.new('ShaderNodeOutputMaterial'); out.location = (750,0)
texcoord = nodes.new('ShaderNodeTexCoord'); texcoord.location = (-900,0)
scale = nodes.new('ShaderNodeVectorMath'); scale.operation = 'MULTIPLY'; scale.inputs[1].default_value = (.5,.5,1); scale.location = (-700,0)
offset = nodes.new('ShaderNodeVectorMath'); offset.operation = 'ADD'; offset.name = 'MouthTileOffset'; offset.inputs[1].default_value = (0,3/8,0); offset.location = (-500,0)
image = nodes.new('ShaderNodeTexImage'); image.image = image_for(material_sources['EyeMouth']['textures']['_MouthTileTex']['file']); image.extension = 'CLIP'; image.location = (-250,0)
em = nodes.new('ShaderNodeEmission'); em.location = (0,100)
transparent = nodes.new('ShaderNodeBsdfTransparent'); transparent.location = (0,-150)
mix = nodes.new('ShaderNodeMixShader'); mix.location = (500,0)
links.new(texcoord.outputs['UV'],scale.inputs[0]);links.new(scale.outputs[0],offset.inputs[0]);links.new(offset.outputs[0],image.inputs['Vector'])
links.new(image.outputs['Color'],em.inputs['Color']);links.new(image.outputs['Alpha'],mix.inputs[0]);links.new(transparent.outputs[0],mix.inputs[1]);links.new(em.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],out.inputs[0])
mouth['tile_id'] = 300
body.data.materials.append(mouth)
mouth_slot = len(body.data.materials)-1
eye_slot = next(i for i,m in enumerate(body.data.materials) if m == materials['EyeMouth'])
mouth_faces = []
for poly in body.data.polygons:
    if poly.material_index == eye_slot:
        coords = [body.data.uv_layers.active.data[i].uv for i in poly.loop_indices]
        if all(uv.x < .3 and uv.y < .3 for uv in coords):
            poly.material_index = mouth_slot; mouth_faces.append(poly.index)

scene.render.engine = 'BLENDER_EEVEE'
scene.world = bpy.data.worlds.new('CHDDD_PreviewWorld')
scene.world.use_nodes = True
world_background = scene.world.node_tree.nodes.new('ShaderNodeBackground')
world_output = scene.world.node_tree.nodes.new('ShaderNodeOutputWorld')
scene.world.node_tree.links.new(world_background.outputs[0],world_output.inputs[0])
world_background.inputs['Color'].default_value = (.16,.18,.24,1)
world_background.inputs['Strength'].default_value = .65
scene.view_settings.view_transform = 'Standard'
scene.render.resolution_x = 800;scene.render.resolution_y = 900;scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False

bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()
points = [obj.matrix_world @ Vector(v) for obj in [body,halo] for v in obj.bound_box]
lo = Vector(tuple(min(v[k] for v in points) for k in range(3)))
hi = Vector(tuple(max(v[k] for v in points) for k in range(3)))
center = (lo+hi)/2
camera_data = bpy.data.cameras.new('CHDDD_PreviewCamera')
camera = bpy.data.objects.new('CHDDD_PreviewCamera', camera_data);scene.collection.objects.link(camera)
camera.location = center + Vector((0,-3, .25))
camera.rotation_euler = (center-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type = 'ORTHO';camera_data.ortho_scale = max(hi.z-lo.z,hi.x-lo.x)*1.32
scene.camera = camera
for name,loc,power,size in [('Key',(-2,-3,4),450,4),('Fill',(2,-1,2),180,3)]:
    data = bpy.data.lights.new('CHDDD_'+name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    obj = bpy.data.objects.new(data.name,data);scene.collection.objects.link(obj);obj.location=center+Vector(loc);obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()

for obj in bpy.context.selected_objects:
    obj.select_set(False)
body.select_set(True);bpy.context.view_layer.objects.active = body
for window in bpy.context.window_manager.windows:
    for area in window.screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.shading.type = 'MATERIAL'
            region = next((r for r in area.regions if r.type=='WINDOW'),None)
            if region:
                with bpy.context.temp_override(window=window,area=area,region=region):
                    bpy.ops.view3d.view_selected(use_all_regions=False)
for im in bpy.data.images:
    if im.filepath and 'CHDDD' in im.filepath:
        im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT / 'CHDDD.blend'))
scene.render.filepath = str(OUTPUT / 'Previews/neutral_initial.png')
bpy.ops.render.render(write_still=True)
print(json.dumps({'file':str(OUTPUT/'CHDDD.blend'),'scene':scene.name,'mouth_faces':len(mouth_faces),'halo_vertices':len(mesh.vertices),'bounds':[list(lo),list(hi)],'armature':armature.name,'face_bones':[(b.name,list(b.head),list(b.tail)) for b in armature.data.bones if 'eye' in b.name.lower() or 'mouth' in b.name.lower()],'original_actions':len(bpy.data.actions)}))
