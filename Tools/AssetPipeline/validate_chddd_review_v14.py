"""Existing performance regressions plus the two symmetry checks requested here."""
import bpy,json
from pathlib import Path
from mathutils import Vector
source=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/validate_chddd_review_v11.py').read_text(encoding='utf-8')
source=source.replace('V10_TextureReview','V13_EyeAngleReview').replace("PREV/'Unity/","PREV/'Blender/").replace('revision=11','revision=14')
exec(compile(source,'review14_regressions','exec'))
v=bpy.data.objects['CHDDD_TestSqueeze_EyeLines'].data.vertices
mirror=max((v[a].co-Vector((v[b].co.x,v[b].co.y,-v[b].co.z))).length for a,b in ((0,5),(1,4),(2,7),(3,6)))
design=json.loads((ROOT/'chibi_eye_design.json').read_text(encoding='utf-8'))
result['checks']['flat_cards_mirrored_in_head_bind_space']=mirror<1e-7
result['checks']['eye_texture_pixel_mirror_exact']=design['leftRightPixelMirrorExact']
result['checks']['vector_source_present']=(ROOT/'Textures/mika_squeeze_eyes.svg').exists()
result['checks']['high_resolution_rgba_packed']=tuple(texture.size)==(1024,512) and bool(texture.packed_file)
result['measurements']['card_bind_mirror_error_m']=mirror
result['passed']=all(result['checks'].values())
(ROOT/'recovery_validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('REVIEW14_SYMMETRY_AND_ACTIONS',result['passed'],flush=True)
if not result['passed']:raise RuntimeError('Symmetric chibi eye verification failed')
