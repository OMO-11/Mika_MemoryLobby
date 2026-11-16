"""Same transition preview plus a head-aligned frontal symmetry check."""
import bpy
from pathlib import Path
from mathutils import Vector,Matrix
exec(compile(Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/preview_chddd_review_v12.py').read_text(encoding='utf-8'),'review14_preview','exec'))
preview_review9_expression(scene);scene.frame_set(37);setup('Face')
body=bpy.data.objects['CH0069_Body'];arm=bpy.data.objects['Bip001'];head=arm.pose.bones['Bip001 Head']
deform=arm.matrix_world@head.matrix@head.bone.matrix_local.inverted()@arm.matrix_world.inverted()
bind_world=deform@body.matrix_world
forward=(bind_world.to_3x3()@Vector((1,0,0))).normalized()
right=(bind_world.to_3x3()@Vector((0,0,1))).normalized()
up=forward.cross(right).normalized();right=up.cross(forward).normalized()
obj=bpy.data.objects['CHDDD_TestSqueeze_EyeLines'];ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
center=sum((ev.matrix_world@v.co for v in ev.data.vertices),Vector())/8
target=center+up*.022
camera.location=target+forward*3.5
camera.rotation_euler=Matrix((right,up,forward)).transposed().to_quaternion().to_euler()
scene.render.filepath=str(OUT/'TestSqueeze_frontal.png');bpy.ops.render.render(write_still=True)
print('REVIEW14_FRONTAL_SYMMETRY_RENDERED',flush=True)
