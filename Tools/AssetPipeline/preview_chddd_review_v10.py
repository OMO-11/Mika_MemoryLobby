"""Render source-texture test using the same review camera and clock."""
from pathlib import Path
source=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/preview_chddd_review_v9.py').read_text(encoding='utf-8')
source=source.replace('V9_SecondReview','V10_TextureReview').replace("jobs=[('TestSqueeze','Face')] if '--test-only' in sys.argv else [('Success','Full'),('TestSqueeze','Face'),('Success','Face')]", "jobs=[('TestSqueeze','Face'),('TestSqueeze','Full')]")
source=source.replace('REVIEW9','REVIEW10')
exec(compile(source,'preview_review10_texture','exec'))
