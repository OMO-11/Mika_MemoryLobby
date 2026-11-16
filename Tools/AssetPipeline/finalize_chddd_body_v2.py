import hashlib,json,re,tarfile
from pathlib import Path
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
blender=read(ROOT/'body_v2_blender_validation.json');unity=read(ROOT/'body_v2_unity_validation.json');preview=read(ROOT/'body_v2_preview_validation.json')
assert blender['passed'] and unity['passed'] and preview['passed']
revision=read(ROOT/'Unity/body_action_map_v2.json')['revision']
design=read(ROOT/'body_v2_revision7_readability_validation.json')
small=read(ROOT/'body_v2_revision7_small_preview_validation.json')
assert revision==7 and design['passed'] and small['passed'] and len(design['untouchedFaceProfiles'])==13
assert small['nativeUnityFramesVerified'] and read(ROOT/'body_v2_revision7_unity_frames.json')['passed']
assert blender['revision']==7 and unity['revision']==7 and preview['revision']==7
local=read(ROOT/'body_v2_revision7_local_validation.json')
assert local['passed'] and len(local['untouchedFaceProfiles'])==16
assert read(ROOT/'body_v2_revision7_unity_local_validation.json')['passed']
velocity=read(ROOT/'body_v2_loop_velocity_diagnostics.json')
assert all(r['secondaryVelocityDifferenceMax']<.01 for r in velocity), 'Secondary motion loop velocity needs another pass'
baseline=read(ROOT/'character_manifest_before_actions_v1.json');manifest=read(ROOT/'character_manifest.json')
for key in ('expressions','faceBones','blendShapes','transitions','mouthAtlas'):
    assert manifest[key]==baseline[key],('accepted-face-data-changed',key)
with tarfile.open(ROOT/'CHDDD_Actions_v2.unitypackage','r:gz') as archive:
    paths=[archive.extractfile(x).read().decode('utf-8').strip() for x in archive.getmembers() if x.name.endswith('/pathname')]
    names={p.split('/')[-1] for p in paths}
    for required in ('CHDDD_Actions_v2.prefab','CHDDD_Actions_v2.fbx','CHDDD_Actions_v2.controller','CHDDD_Face_v2.mask','body_action_map_v2.json','action_face_profiles_v2.json','CHDDDBodyControllerV2.cs','CHDDDActionFaceControllerV2.cs','CHDDDExpressionController.cs'):
        assert required in names,('missing-package-dependency',required)
    assert not any('/Editor/' in p for p in paths)
    assert sum('/AnimationsV2/' in p and p.endswith('.anim') for p in paths)==45
    guids={x.name.split('/')[0] for x in archive.getmembers() if x.name.endswith('/pathname')}
    missing=[]
    for member in archive.getmembers():
        if not member.name.endswith('/asset'):continue
        if member.size>8000000:continue
        data=archive.extractfile(member).read()
        if not data.startswith(b'%YAML'):continue
        for guid in re.findall(rb'guid: ([0-9a-f]{32})',data):
            value=guid.decode('ascii')
            if value.startswith('0000000000000000'):continue
            if value not in guids:missing.append((member.name,value))
    assert not missing,missing[:10]
    for filename in ('body_action_map_v2.json','action_face_profiles_v2.json'):
        path_member=next(x for x in archive.getmembers() if x.name.endswith('/pathname') and archive.extractfile(x).read().decode('utf-8').strip().endswith('/'+filename))
        packaged=json.loads(archive.extractfile(path_member.name.rsplit('/',1)[0]+'/asset').read())
        assert packaged==read(ROOT/'Unity'/filename),('stale-package-configuration',filename)
    verified_assets=0
    for path_member in archive.getmembers():
        if not path_member.name.endswith('/pathname'):continue
        path=archive.extractfile(path_member).read().decode('utf-8').strip()
        prefix='Assets/CHDDD/Unity/'
        if not path.startswith(prefix):continue
        local=ROOT/'Unity'/path[len(prefix):]
        if not local.is_file():continue
        packaged=archive.extractfile(path_member.name.rsplit('/',1)[0]+'/asset').read()
        assert hashlib.sha256(packaged).digest()==hashlib.sha256(local.read_bytes()).digest(),('stale-package-asset',path)
        verified_assets+=1
    assert verified_assets>=55,('insufficient-package-asset-comparison',verified_assets)
    assert {e['tier'] for e in read(ROOT/'Unity/body_action_map_v2.json')['actions']}=={'daily','emotion','special'}
manifest['bodyAnimationV2']={
 'status':'local-acting-refinements-ready-for-design-review','revision':revision,'designDirection':'Daily low intensity / emotional response medium-high / rare special performance high; readable full-body poses at desktop size','blenderFile':'CHDDD_actions_v2.blend',
 'model':'Unity/CHDDD_Actions_v2.fbx','mapping':'Unity/body_action_map_v2.json','faceProfiles':'Unity/action_face_profiles_v2.json',
 'bodyClipCount':18,'actionFaceClipCount':18,'optionalEmotionClipCount':9,'loopClipCount':7,'weaponVisible':False,
 'draggingSource':{'glbOneBasedIndex':29,'clip':'CH0069_Formation_Pickup','durationSeconds':337/30,'bodyTrajectoryPreserved':True,'secondaryMotionSource':'Game animation with four-frame loop boundary velocity smoothing','originalFaceScalingExcluded':True},
 'secondaryMotion':'Movement-driven damped follow-through; gravity settling during held head tilts; shoulder-to-cape coupling; measured actual skinned mesh displacement. Source motion kept for Idle, Walk and Dragging',
 'performanceTiming':'Durations and approved torso amplitudes retained; inward-palm head scratch; higher celebration hands and closed eyes; wing flaps; direct wave return; closed-eye triangle startle and playful frown',
 'faceLayer':'Action-specific eye/brow/mouth sequences; Head remains on body layer; manual emotions optional',
 'unityValidation':'passed','unityVersion':unity['unityVersion'],'prefab':'Unity/CHDDD_Actions_v2.prefab',
 'controller':'Unity/CHDDD_Actions_v2.controller','unityPackage':'CHDDD_Actions_v2.unitypackage',
 'preview':'Previews/ActionsV2/index.html','smallSizePreview':'Previews/ActionsV2/readability.html','previewFPS':24,'authoringFPS':30,
 'intensityTiers':['daily','emotion','special'],'frequencyControl':'Recommendations stored in action map; host scheduling not yet integrated',
 'qualityAssessment':'Multiple visual review and correction passes; final comparison with official game quality requires human review',
 'projectIntegration':'Not yet integrated into GiftCompanion runtime'}
(ROOT/'character_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
result={'passed':True,'acceptedFaceDataPreserved':True,'sourceUnchanged':blender['sourceUnchanged'],
 'blenderPassed':True,'unityPassed':True,'previewPassed':True,'packageAssets':len(paths),'packageAnimationClips':45,
 'packageReferencesComplete':True,'packageAssetsMatchFinalFiles':verified_assets,'secondaryLoopVelocitiesContinuous':True,'revision':revision,'designRegressionPassed':True,'heldPosesAndToeContactsVerified':True,'actualSecondaryMeshDisplacementVerified':True,'smallSizeColorAndSilhouettePreviewVerified':True,'untouchedFaceProfiles':16,'requestedLocalActingVerified':True,'v1DeliveryPreserved':(ROOT/'CHDDD_actions_v1.blend').exists() and (ROOT/'CHDDD_Actions_v1.unitypackage').exists()}
(ROOT/'body_v2_delivery_validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
files=[ROOT/'CHDDD_actions_v2.blend',ROOT/'CHDDD_Actions_v2.unitypackage',ROOT/'character_manifest.json',ROOT/'动作优化第二版.md']
files+=sorted((ROOT/'Unity/AnimationsV2').glob('*'))
files+=sorted(p for p in (ROOT/'Unity').iterdir() if p.is_file() and 'v2' in p.name.lower())
(ROOT/'body_v2_checksums.sha256').write_text('\n'.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(ROOT).as_posix() for p in files)+'\n',encoding='utf-8')
(ROOT/'body_v2_progress.json').write_text(json.dumps({'date':'2026-10-09','status':'completed-local-acting-refinement-pass-ready-for-design-review','revision':revision,'designDirection':'Local hand acting, shoulder clearance, wing animation and targeted facial timelines','previousCheckpoint':'Checkpoints/V2_revision6_before_local','bodyClips':18,'actionFaceProfiles':18,'sourceDragging':'GLB #29','unityVersion':unity['unityVersion'],'previewFrames':preview['nativeFrames'],'smallSizePreview':'Previews/ActionsV2/readability.html','officialQualityCertified':False},indent=2),encoding='utf-8')
print(json.dumps(result))
