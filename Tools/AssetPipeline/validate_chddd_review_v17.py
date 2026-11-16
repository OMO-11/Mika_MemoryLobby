"""Reuse existing verification, comparing all previous performance curves with V16."""
from pathlib import Path
source=Path(__file__).with_name('validate_chddd_review_v16.py').read_text(encoding='utf-8')
source=source.replace("PREV=ROOT.parent/'V15_ChibiEyeReview'", "PREV=ROOT.parent/'V16_OmegaFirstReview'")
source=source.replace("('PetV2_','Review9_TestSqueeze_')", "('PetV2_','Review9_TestSqueeze_','Review16_TestOmega_')")
source=source.replace('revision=16','revision=17').replace('V16_VALIDATION','V17_VALIDATION')
exec(compile(source,'omega17_validation','exec'))
