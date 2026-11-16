import bpy,pathlib,json
from mathutils import Vector
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Previews')
scene=bpy.context.scene;cam=scene.camera
saved=(cam.matrix_world.copy(),cam.data.ortho_scale,scene.render.resolution_x,scene.render.resolution_y)
scene.render.resolution_x=900;scene.render.resolution_y=650;scene.render.resolution_percentage=100
for emotion in ['neutral','sad','annoyed','sleepy']:
    bpy.ops.chddd.expression(emotion=emotion)
    target=Vector((.012,-.15,.726))
    cam.location=Vector((.012,-1.6,.746))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=.36
    scene.render.filepath=str(OUT/('eye_fix_'+emotion+'.png'))
    bpy.ops.render.render(write_still=True)
cam.matrix_world,cam.data.ortho_scale,scene.render.resolution_x,scene.render.resolution_y=saved
bpy.ops.chddd.expression(emotion='neutral')
print('Eye closeups complete')
