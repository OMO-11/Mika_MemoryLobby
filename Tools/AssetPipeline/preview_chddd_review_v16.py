"""Use the existing cameras and transition sampling for the second expression test."""
from pathlib import Path
from mathutils import Matrix
source=Path(__file__).with_name('preview_chddd_review_v14.py').read_text(encoding='utf-8')
# Expand the nested generic preview into one source, then substitute only the new test.
base=Path(__file__).with_name('preview_chddd_review_v9.py').read_text(encoding='utf-8')
base=base.replace('V9_SecondReview','V16_OmegaFirstReview')
base=base.replace("'CHDDD_TestSqueeze_EyeLines'", "'CHDDD_TestSqueeze_EyeLines','CHDDD_TestOmega_Eyes','CHDDD_TestOmega_Mouth'")
base=base.replace('preview_review9_expression','preview_review16_omega').replace('TestSqueeze','TestOmega')
base=base.replace("jobs=[('TestOmega','Face')] if '--test-only' in sys.argv else [('Success','Full'),('TestOmega','Face'),('Success','Face')]", "jobs=[] if '--key-only' in sys.argv else [('TestOmega','Face'),('TestOmega','Full')]")
exec(compile(base,'review16_preview','exec'))
# The two original flat eye cards remain a convenient head-front reference.
frontal=source.split("preview_review9_expression(scene);scene.frame_set(37);setup('Face')",1)[1]
exec(compile("preview_review16_omega(scene);scene.frame_set(37);setup('Face')"+frontal.replace('TestSqueeze_frontal','TestOmega_frontal'),'review16_frontal','exec'))
