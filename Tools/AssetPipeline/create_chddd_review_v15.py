"""Update only the experimental eye image and version metadata in a V14 snapshot."""
import bpy,json,shutil
from pathlib import Path

MAIN=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
ROOT=MAIN/'Reviews/V15_ChibiEyeReview'
for folder in (ROOT/'Blender',ROOT/'Preview'):
    folder.mkdir(parents=True,exist_ok=True)
for name in ('character_manifest.json','character_manifest_before_actions_v1.json','CHDDD_Controls.py'):
    shutil.copy2(MAIN/name,ROOT/name)
for path in (MAIN/'Blender').glob('*.json'):
    shutil.copy2(path,ROOT/'Blender'/path.name)
controls=(MAIN/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8').replace('平面贴图 V14 · Q 版对称眼线','平面贴图 V15 · 稍粗稍小眼线')
assert '平面贴图 V15' in controls
(ROOT/'CHDDD_BodyControls_v2.py').write_text(controls,encoding='utf-8')
text=bpy.data.texts['CHDDD_BodyControls_v2.py'];text.clear();text.write(controls)
obj=bpy.data.objects['CHDDD_TestSqueeze_EyeLines']
assert len(obj.data.vertices)==8 and len(obj.data.polygons)==2
obj['review_revision']=15
obj['eye_refinement']='V14 mirrored vector glyph; rounded 2.4-unit outline, uniform 0.94 scale; card geometry and opening angles unchanged'
nt=bpy.data.materials['Review9_SqueezeEyeLine'].node_tree
texture=next(n for n in nt.nodes if n.type=='TEX_IMAGE' and n.image and n.image.name.startswith('mika_squeeze_eyes'))
image=bpy.data.images.load(str(ROOT/'Textures/mika_squeeze_eyes.png'),check_existing=False)
image.name='mika_squeeze_eyes_chibi_v15.png';image.alpha_mode='STRAIGHT';image.pack()
texture.image=image;texture.interpolation='Linear'
mapping_path=ROOT/'Blender/body_action_map_v2.json'
mapping=json.loads(mapping_path.read_text(encoding='utf-8'))
mapping.update(revision=15,reviewIteration=8,reviewStatus='Slightly thicker and smaller mirrored chibi eyes; waiting for review')
mapping_path.write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
scene=bpy.context.scene;scene['body_v2_revision']=15
scene['review_status']='V15 thicker and smaller mirrored chibi eyes; pending user review'
scene['pending_review']='V15 thicker and smaller mirrored chibi eyes'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
exec(compile((ROOT/'CHDDD_Controls.py').read_text(encoding='utf-8'),'review15_static_controls','exec'))
exec(compile(controls,'review15_body_controls','exec'))
preview_review9_expression(scene);scene.frame_set(37)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'CHDDD_actions_v2.blend'))
geometry=json.loads((MAIN/'Reviews/V14_ChibiEyeReview/flat_card_geometry.json').read_text(encoding='utf-8'))
geometry.update(revision=15,geometryChanged=False)
(ROOT/'flat_card_geometry.json').write_text(json.dumps(geometry,indent=2),encoding='utf-8')
print('V15_THICKER_SMALLER_EYE_DRAFT_CREATED',flush=True)
