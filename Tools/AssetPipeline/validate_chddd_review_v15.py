"""Reuse the existing recovery checks against V14 and verify the new eye mirror."""
from pathlib import Path
source=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/validate_chddd_review_v14.py').read_text(encoding='utf-8')
source=source.replace(".replace('V10_TextureReview','V13_EyeAngleReview')", ".replace('V10_TextureReview','V14_ChibiEyeReview')")
# V14 no longer uses a crop; keep the immutable source check from V13.
source=source.replace("exec(compile(source,'review14_regressions','exec'))", "source=source.replace(\"PREV/'texture_crop_manifest.json'\", \"PREV.parent/'V13_EyeAngleReview/texture_crop_manifest.json'\")\nexec(compile(source,'review15_regressions','exec'))")
source=source.replace('revision=14','revision=15').replace('REVIEW14_','REVIEW15_')
exec(compile(source,'review15_validation','exec'))
