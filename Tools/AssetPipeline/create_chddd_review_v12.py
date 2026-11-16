"""One local angle refinement; preserve every performance and the flat card geometry."""
import bpy,json,shutil
from pathlib import Path
MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
PREV=MAIN/'Reviews/V11_FlatEyeRecovery'
ROOT=MAIN/'Reviews/V12_EyeAngleReview'
for folder in (ROOT,ROOT/'Blender',ROOT/'Preview'):
    folder.mkdir(parents=True,exist_ok=True)
for name in ('character_manifest.json','character_manifest_before_actions_v1.json','CHDDD_Controls.py'):
    shutil.copy2(MAIN/name,ROOT/name)
for name in ('texture_crop_manifest.json','texture_crop_review.jpg','flat_card_geometry.json'):
    shutil.copy2(PREV/name,ROOT/name)
for path in (MAIN/'Blender').glob('*.json'):
    shutil.copy2(path,ROOT/'Blender'/path.name)
controls=(MAIN/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8')
controls=controls.replace('已恢复星期五动作 · 平面贴图试验 V11','平面贴图 V12 · 眼线夹角微调')
(ROOT/'CHDDD_BodyControls_v2.py').write_text(controls,encoding='utf-8')
text=bpy.data.texts['CHDDD_BodyControls_v2.py'];text.clear();text.write(controls)
nt=bpy.data.materials['Review9_SqueezeEyeLine'].node_tree
texture=next(n for n in nt.nodes if n.type=='TEX_IMAGE' and n.image.name.startswith('mika_squeeze_eyes'))
image=bpy.data.images.load(str(ROOT/'Textures/mika_squeeze_eyes.png'),check_existing=False)
image.name='mika_squeeze_eyes_angle_v12.png';image.pack();texture.image=image
obj=bpy.data.objects['CHDDD_TestSqueeze_EyeLines']
obj['review_revision']=12
obj['angle_refinement']='User orange guide: upper branches upward, lower branches downward; original stroke thickness retained'
mapping=json.loads((ROOT/'Blender/body_action_map_v2.json').read_text(encoding='utf-8'))
mapping.update(revision=12,reviewIteration=5,reviewStatus='Eye angle refinement; awaiting user review')
(ROOT/'Blender/body_action_map_v2.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
scene=bpy.context.scene;scene['body_v2_revision']=12
scene['review_status']='V12: >o< stroke angles follow the user orange guide; pending approval'
scene['pending_review']='V12 eye stroke angle refinement'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
exec(compile(controls,'review12_controls','exec'))
preview_review9_expression(scene);scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
print('V12_EYE_ANGLE_DRAFT_CREATED',flush=True)
