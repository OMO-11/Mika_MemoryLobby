"""Commit an already verified local eye review to the canonical Blender workfile."""
import bpy,json,shutil
from pathlib import Path
MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
DRAFT=Path(bpy.data.filepath).parent
revision=int(bpy.context.scene['body_v2_revision'])
assert json.loads((DRAFT/'recovery_validation.json').read_text(encoding='utf-8'))['passed']
for path in (DRAFT/'Blender').glob('*.json'):
    shutil.copy2(path,MAIN/'Blender'/path.name)
for name in ('CHDDD_Controls.py','CHDDD_BodyControls_v2.py','recovery_validation.json'):
    shutil.copy2(DRAFT/name,MAIN/name)
for suffix in ('png','svg'):
    path=DRAFT/f'Textures/mika_squeeze_eyes.{suffix}'
    if path.exists():shutil.copy2(path,MAIN/f'Textures/mika_squeeze_eyes_review.{suffix}')
nt=bpy.data.materials['Review9_SqueezeEyeLine'].node_tree
texture=next(n for n in nt.nodes if n.type=='TEX_IMAGE' and n.image.name.startswith('mika_squeeze_eyes'))
texture.image.filepath=str(MAIN/'Textures/mika_squeeze_eyes_review.png')
bpy.ops.wm.save_as_mainfile(filepath=str(MAIN/'CHDDD_actions_v2.blend'))
exec(compile((MAIN/'CHDDD_Controls.py').read_text(encoding='utf-8'),'canonical_static_controls','exec'))
exec(compile((MAIN/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'canonical_body_controls','exec'))
preview_review9_expression(bpy.context.scene);bpy.context.scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(MAIN/'CHDDD_actions_v2.blend'))
print('CANONICAL_EYE_REVIEW_COMMITTED',revision,flush=True)
