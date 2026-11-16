"""Commit the verified second expression experiment, held on its own peak frame."""
from pathlib import Path
source=Path(__file__).with_name('commit_chddd_eye_review.py').read_text(encoding='utf-8')
source=source.replace('preview_review9_expression(bpy.context.scene);','preview_review16_omega(bpy.context.scene);')
exec(compile(source,'commit_omega_review16','exec'))
