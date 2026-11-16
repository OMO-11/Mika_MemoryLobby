"""Save the reviewed Friday actions and pending V11 eye test to the main workfile."""
import bpy, json, shutil
from pathlib import Path
ROOT = Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
DRAFT = ROOT/'Reviews/V11_FlatEyeRecovery'
assert json.loads((DRAFT/'recovery_validation.json').read_text(encoding='utf-8'))['passed']
(ROOT/'Blender').mkdir(exist_ok=True)
for path in (DRAFT/'Blender').glob('*.json'):
    shutil.copy2(path, ROOT/'Blender'/path.name)
for name in ('CHDDD_Controls.py', 'CHDDD_BodyControls_v2.py'):
    shutil.copy2(DRAFT/name, ROOT/name)
shutil.copy2(DRAFT/'Textures/mika_squeeze_eyes.png', ROOT/'Textures/mika_squeeze_eyes_review.png')
for image in bpy.data.images:
    if image.name.startswith('mika_squeeze_eyes'):
        image.filepath = str(ROOT/'Textures/mika_squeeze_eyes_review.png')
scene = bpy.context.scene
scene['main_file_recovery_date'] = '2026-10-11'
scene['restored_from'] = 'Reviews/V10_TextureReview/CHDDD_actions_v2.blend'
scene['pending_review'] = 'V11 flat eye cards >o<'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
exec(compile((ROOT/'CHDDD_Controls.py').read_text(encoding='utf-8'), 'static_controls', 'exec'))
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'), 'body_controls', 'exec'))
preview_review9_expression(scene)
scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
progress = json.loads((ROOT/'body_v2_progress.json').read_text(encoding='utf-8'))
progress.update(date='2026-10-11', revision=11,
    status='friday-actions-recovered-flat-eye-expression-awaiting-review',
    previousCheckpoint='Checkpoints/20261011_before_main_recovery',
    restoredFrom='Reviews/V10_TextureReview/CHDDD_actions_v2.blend',
    restoredBodyClips=18, restoredFacialProcesses=18,
    blenderAuthoringMap='Blender/body_action_map_v2.json',
    latestReview='Reviews/V11_FlatEyeRecovery/Preview/index.html',
    unityDeliveryRevision=7, unityRevalidated=False)
(ROOT/'body_v2_progress.json').write_text(json.dumps(progress, ensure_ascii=False, indent=2), encoding='utf-8')
manifest = json.loads((ROOT/'character_manifest.json').read_text(encoding='utf-8'))
manifest['blenderWorkingCopy'] = dict(revision=11, file='CHDDD_actions_v2.blend',
    actionMap='Blender/body_action_map_v2.json', recoveryDate='2026-10-11',
    sourceDraft='Reviews/V10_TextureReview/CHDDD_actions_v2.blend',
    pendingExpression='>o< flat eye cards V11', unityDeliveryRevision=7)
(ROOT/'character_manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print('CANONICAL_BLENDER_WORKFILE_RECOVERED', flush=True)
