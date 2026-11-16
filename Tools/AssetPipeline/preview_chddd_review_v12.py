"""Use the existing review camera and exact transition clock for the angle change."""
from pathlib import Path
import bpy
source=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/preview_chddd_review_v11.py').read_text(encoding='utf-8')
source=source.replace("'V11_FlatEyeRecovery'",repr(Path(bpy.data.filepath).parent.name)).replace('REVIEW11',f"REVIEW{bpy.context.scene.get('body_v2_revision',12)}")
exec(compile(source,'review12_preview','exec'))
