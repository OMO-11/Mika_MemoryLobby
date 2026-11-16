"""One flat-card review: same cameras, lighting, clock and artwork as V10."""
from pathlib import Path
source = Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/preview_chddd_review_v9.py').read_text(encoding='utf-8')
source = source.replace('V9_SecondReview', 'V11_FlatEyeRecovery').replace('REVIEW9', 'REVIEW11')
source = source.replace("jobs=[('TestSqueeze','Face')] if '--test-only' in sys.argv else [('Success','Full'),('TestSqueeze','Face'),('Success','Face')]", "jobs=[] if '--key-only' in sys.argv else [('TestSqueeze','Face'),('TestSqueeze','Full')]")
exec(compile(source, 'preview_review11_flat', 'exec'))
