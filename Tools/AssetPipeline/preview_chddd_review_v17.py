"""Render the thicker omega test with the existing cameras and clock."""
from pathlib import Path
source=Path(__file__).with_name('preview_chddd_review_v16.py').read_text(encoding='utf-8')
source=source.replace('V16_OmegaFirstReview','V17_OmegaThickerReview')
exec(compile(source,'omega17_preview','exec'))
