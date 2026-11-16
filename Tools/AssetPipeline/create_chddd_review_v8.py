"""One review draft only. Never overwrite the revision-7 delivery."""
import bpy, json, shutil, math, hashlib
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

SOURCE=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
PIPE=SOURCE.parent.parent/'Tools/AssetPipeline'
DRAFT=SOURCE/'Reviews/V8_FirstReview'
DRAFT.mkdir(parents=True,exist_ok=True)
(DRAFT/'Unity').mkdir(exist_ok=True)
for name in ('character_manifest_before_actions_v1.json','character_manifest.json','body_source_cache_v1.json','body_source_pickup_v2.json','CHDDD_BodyControls_v2.py','CHDDD_Controls.py'):
    shutil.copy2(SOURCE/name,DRAFT/name)
for name in ('expression_map_v1.json','expression_map_v2.json','body_action_map_v2.json','action_face_profiles_v2.json'):
    shutil.copy2(SOURCE/'Unity'/name,DRAFT/'Unity'/name)

# Snapshot the existing delivered actions so every unrelated curve stays exact.
arm=bpy.data.objects['Bip001'];body=bpy.data.objects['CH0069_Body']
original_mapping=json.loads((SOURCE/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
original_profiles=json.loads((SOURCE/'Unity/action_face_profiles_v2.json').read_text(encoding='utf-8'))
old_actions={a.name:a for a in bpy.data.actions if a.name.startswith('PetV2_')}
for name,a in old_actions.items():a.name='Review7_'+name

legacy=(PIPE/'create_chddd_body_v1.py').read_text(encoding='utf-8').replace(SOURCE.as_posix(),DRAFT.as_posix())
source=(PIPE/'create_chddd_body_v2.py').read_text(encoding='utf-8')
source=source.replace("ROOT=Path('"+SOURCE.as_posix()+"')","ROOT=Path('"+DRAFT.as_posix()+"')")
source=source.replace("legacy=(ROOT.parent.parent/'Tools/AssetPipeline/create_chddd_body_v1.py').read_text(encoding='utf-8')","legacy="+repr(legacy))
source=source.replace("ROOT.parent.parent/'Tools/AssetPipeline/", "Path('"+PIPE.as_posix()+"')/'")
marker="exec(compile((Path('"+PIPE.as_posix()+"')/'chddd_local_refinements_v7.py').read_text(encoding='utf-8'),'local_refinements_v7','exec'))"
assert marker in source
addon=(PIPE/'chddd_review_v8_changes.py').read_text(encoding='utf-8')
source=source.replace(marker,marker+'\n'+addon)
source=source.replace("'revision':7", "'revision':8")
source=source.replace("scene['body_v2_revision']=7", "scene['body_v2_revision']=8")
source=source.replace("'active-local-acting-refinement-pass'", "'awaiting-user-review-first-draft'")
source=source.replace("def face_value(blink=0,sad=0,annoyed=0,sleepy=0,tile=300,gaze=0,lift=0):", "def face_value(blink=0,sad=0,annoyed=0,sleepy=0,tile=300,gaze=0,lift=0,happy=0):")
source=source.replace("'blink':blink,'sad':sad", "'happy':happy,'blink':blink,'sad':sad")
source=source.replace("knot(.36,blink=1,tile=706,lift=.90)","knot(.36,blink=1,tile=706,lift=.60,happy=1)")
source=source.replace("knot(.48,blink=1,tile=706,lift=.90)","knot(.48,blink=1,tile=706,lift=.60,happy=1)")
source=source.replace("knot(.56,blink=1,tile=706,lift=.80)","knot(.56,blink=1,tile=706,lift=.50,happy=1)")
source=source.replace("knot(.64,blink=.70,tile=706,lift=.45)","knot(.64,blink=.70,tile=706,lift=.35,happy=.6)")
source=source.replace("knot(.55,blink=.22,annoyed=.55,tile=604,gaze=.2)","knot(.55,blink=.22,annoyed=.55,tile=305,gaze=.2)")
source=source.replace("knot(.70,blink=.18,annoyed=.65,tile=604)","knot(.70,blink=.18,annoyed=.65,tile=305)")
source=source.replace("[('Pet_Blink','blink'),", "[('Pet_BrowHappy_Review','happy'),('Pet_Blink','blink'),")
exec(compile(source,'create_chddd_body_v2_review8','exec'))

# Restore untouched actions and their exact profiles, including the nine accepted
# opt-in facial templates. New authoring is confined to Success and HeadClick.
changed={'Success','HeadClick'}
for name,old in old_actions.items():
    keep=not any(name==prefix+action for action in changed for prefix in ('PetV2_','PetV2_FaceProcess_','PetV2_Shapes_','PetV2_Mouth_'))
    if keep:
        new=bpy.data.actions.get(name)
        if new:
            new.user_remap(old);bpy.data.actions.remove(new)
        old.name=name
    else:old.use_fake_user=True
new_profiles=json.loads((DRAFT/'Unity/action_face_profiles_v2.json').read_text(encoding='utf-8'))
for i,p in enumerate(new_profiles['profiles']):
    if p['name'] not in changed:new_profiles['profiles'][i]=next(x for x in original_profiles['profiles'] if x['name']==p['name'])
(DRAFT/'Unity/action_face_profiles_v2.json').write_text(json.dumps(new_profiles,separators=(',',':')),encoding='utf-8')
mapping=json.loads((DRAFT/'Unity/body_action_map_v2.json').read_text(encoding='utf-8'))
for i,e in enumerate(mapping['actions']):
    if e['name'] not in changed:mapping['actions'][i]=next(x for x in original_mapping['actions'] if x['name']==e['name'])
mapping['reviewStatus']='First draft, pending user review; do not promote or iterate without feedback'
mapping['experimentalExpressions']=['TestSqueeze >o< (first draft, not an accepted template)']
(DRAFT/'Unity/body_action_map_v2.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
exec(compile((PIPE/'chddd_review_v8_expression.py').read_text(encoding='utf-8'),'review8_expression','exec'))
exec(compile((DRAFT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'review_controls','exec'))
preview_body_v2(bpy.context.scene,'Success');bpy.context.scene.frame_set(43)
bpy.context.scene['review_version']='V8 first draft; waiting for user review'
for name in ('CHDDD_BodyControls_v2.py','CHDDD_Controls.py'):
    text=bpy.data.texts.get(name) or bpy.data.texts.new(name);text.clear();text.write((DRAFT/name).read_text(encoding='utf-8'))
bpy.ops.wm.save_as_mainfile(filepath=str(DRAFT/'CHDDD_actions_v2.blend'))
print('REVIEW8_READY',str(DRAFT))
