import bpy,json,math,sys,argparse
from pathlib import Path
from mathutils import Vector
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
OUT=ROOT/'Previews/ActionsV2';OUT.mkdir(parents=True,exist_ok=True)
parser=argparse.ArgumentParser();parser.add_argument('--names',default='Dragging,Wave,Thinking,HeadPat,Success,Sleep_Enter');parser.add_argument('--motion',action='store_true');parser.add_argument('--fps',type=float,default=12)
parser.add_argument('--indices',default='',help='Optional motion-frame indices to update after a local curve correction')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'body_controls_v2','exec'))
scene=bpy.context.scene;camera=scene.camera
target=Vector((0,0,.53));camera.location=Vector((0,-3.5,.59))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=1.42
scene.render.resolution_x=480;scene.render.resolution_y=580
if args.motion:scene.render.film_transparent=True
for obj in scene.objects:
    if obj.type=='MESH' and obj.name not in ('CH0069_Body','CHDDD_Halo_Restored'):obj.hide_render=True
for name in args.names.split(','):
    entry=preview_body_v2(scene,name)
    count=round(entry['durationSeconds']*args.fps) if args.motion else 7
    selected=[int(v) for v in args.indices.split(',')] if args.indices else range(count)
    for i in selected:
        t=i/count if args.motion else i/(count-1)
        frame=1+t*(entry['frames'][1]-1)
        scene.frame_set(math.floor(frame),subframe=frame%1)
        scene.render.filepath=str(OUT/f'{name}_{"motion" if args.motion else "pose"}_{i:03d}.png')
        bpy.ops.render.render(write_still=True)
preview_body_v2(scene,'Idle')
print(json.dumps({'names':args.names,'motion':args.motion,'fps':args.fps}))
