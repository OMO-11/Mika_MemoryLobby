import bpy, json, pathlib
from mathutils import Vector
OUTPUT = pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
scene=bpy.context.scene
body=next(o for o in scene.objects if o.type=='MESH' and o.name.startswith('CH0069_Body'))
armature=next(o for o in scene.objects if o.type=='ARMATURE')
halo=next(o for o in scene.objects if o.name=='CHDDD_Halo_Restored')
mesh=halo.data
mouth_faces=[p.index for p in body.data.polygons if body.data.materials[p.material_index].name=='CHDDD_MouthAtlas']
scene.render.engine='BLENDER_EEVEE'

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
