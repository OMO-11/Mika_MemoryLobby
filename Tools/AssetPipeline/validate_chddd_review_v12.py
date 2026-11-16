"""Run the existing recovery regression checks against V11 after the angle edit."""
from pathlib import Path
source=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/validate_chddd_review_v11.py').read_text(encoding='utf-8')
source=source.replace('V10_TextureReview','V11_FlatEyeRecovery').replace("PREV/'Unity/","PREV/'Blender/")
source=source.replace('revision=11','revision=12')
exec(compile(source,'review12_existing_regression','exec'))
