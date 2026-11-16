"""Render native 30 FPS previews without re-running the completed curve audit."""
import bpy,json
from pathlib import Path
from mathutils import Vector
OUT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/RandomIdleV1')
PRE=OUT/'Preview30';PRE.mkdir(exist_ok=True)
scene=bpy.context.scene;arm=bpy.data.objects['Bip001'];camera=scene.camera
data=json.loads((OUT/'idle_action_map_v1.json').read_text(encoding='utf-8'))
for t in arm.animation_data.nla_tracks:t.mute=True
source=Path(__file__).with_name('preview_chddd_random_idle_v1.py').read_text(encoding='utf-8')
source=source[source.index('scene.render.resolution_x='):].replace("*12)","*30)").replace('i/12*30','i/30*30')
exec(compile(source,'native_30fps_preview','exec'))
