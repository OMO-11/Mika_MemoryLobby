import json,hashlib
from pathlib import Path
root=Path('Material/CHDDD')
unity=json.loads((root/'restoration_validation.json').read_text(encoding='utf-8'))
report=json.loads((root/'transition_validation.json').read_text(encoding='utf-8'))
report['blenderPlaybackOnceVerified']=True
report['unity']={key:unity[key] for key in ['passed','animationClips','transitionClips','runtimeTransitionsVerified','interruptedTransitionContinuous','mouthCrossfadeVerified','irisSizePreserved']}
report['visualReview']='Reviewed all eight neutral transitions at 0/125/250/375/500 ms and closed-eye closeups at front and +/-20 degrees. Smooth closed arcs with no exposed socket in these views.'
(root/'transition_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
checks=json.loads((root/'artifact_checksums.json').read_text(encoding='utf-8'))
for path in ['transition_validation.json','Previews/neutral_to_sleepy.gif','Previews/neutral_to_sleepy.png','Previews/neutral_transition_contact_sheet.png','CHDDD_before_curve_transition.blend','CHDDD_Controls.py','Unity/CHDDDExpressionController.cs','Unity/CHDDDMouth.shader','Unity/CHDDD.prefab','Unity/CHDDD.controller','Unity/CHDDD_Face.mask']:
    checks[path]={}
checks={name:{'bytes':(root/name).stat().st_size,'sha256':hashlib.sha256((root/name).read_bytes()).hexdigest()} for name in checks if (root/name).is_file()}
(root/'artifact_checksums.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
source=Path('Material/CH0069/Model/CH0069.glb')
assert hashlib.sha256(source.read_bytes()).hexdigest()=='57816e04521d12a9615cb5081fc7db90bb7fe0ae8f36d3cec3ed04dfad9b67a0'
assert unity['passed'] and report['blenderEndpointsPassed'] and report['blenderInterruptedTransition']['passed']
print(json.dumps({'validationPassed':True,'unityClips':unity['animationClips'],'transitionPresets':unity['transitionClips'],'checksumEntries':len(checks),'sourceUnchanged':True}))
