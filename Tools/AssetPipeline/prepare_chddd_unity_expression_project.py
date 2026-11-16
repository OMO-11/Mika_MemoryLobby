"""Install review-only Unity scripts; retain the existing delivery shaders unchanged."""
from pathlib import Path
import shutil
ROOT=Path('W:/Machine/VSCode/Mika_Gift')
TOOLS=ROOT/'Tools/AssetPipeline'
ASSETS=ROOT/'Temp/UnityExpressionReviewV17/Assets/Review'
for name in ('CHDDDReviewFace.shader','CHDDDReviewOverlay.shader','CHDDDReviewEyeMouth.shader'):
    shutil.copy2(TOOLS/name,ASSETS/'Shaders'/name)
shutil.copy2(TOOLS/'CHDDDUnityExpressionReview.cs',ASSETS/'Editor/CHDDDUnityExpressionReview.cs')
source=(ROOT/'Material/CHDDD/Unity/CHDDDMouth.shader').read_text(encoding='utf-8')
source=source.replace('CHDDD/MouthAtlas','CHDDD/ReviewMouthAtlas')
source=source.replace('Properties\n    {','Properties\n    {\n        _ReviewHide("Hide original mouth", Range(0,1)) = 0')
source=source.replace('float _MouthTile, _MouthBlendEnabled;', 'float _MouthTile, _MouthBlendEnabled, _ReviewHide;')
source=source.replace('return cell(i.uv,_MouthTile);','return cell(i.uv,_MouthTile)*(1-_ReviewHide);')
source=source.replace('return result;','return result*(1-_ReviewHide);')
(ASSETS/'Shaders/CHDDDReviewMouth.shader').write_text(source,encoding='utf-8')
print('UNITY_REVIEW_SCRIPTS_PREPARED',flush=True)
