"""Compare the smaller eye angle against V12 with the same review controls."""
from pathlib import Path
source=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline/package_chddd_review_v12.py').read_text(encoding='utf-8')
source=source.replace('V12','V13').replace('v12','v13').replace('V11_FlatEyeRecovery','V12_EyeAngleReview').replace('V11','V12')
source=source.replace('revision=12','revision=13').replace('blenderRevision=12','blenderRevision=13').replace('reviewIteration=5','reviewIteration=6')
source=source.replace('第五版','第六版').replace('本版：按标线调整夹角','本版：收小夹角')
source=source.replace('按橙色标线将上眼线向外上方展开、下眼线向外下方展开','将上下眼线的夹角收小约四分之一')
source=source.replace('按本轮截图中的橙色标线调整：上眼线向外上方倾斜，下眼线向外下方展开，形成更清楚的 `><` 尖角。','根据用户反馈收小上下眼线夹角：上眼线目标倾角从 26° 调至 20°，下眼线从 28° 调至 21°，合计夹角从 54° 收至 41°。')
source=source.replace('按用户橙色标线调整 `><` 上下眼线夹角','根据反馈将 `><` 夹角收小约四分之一')
exec(compile(source,'package_review13_smaller_angle','exec'))
# Keep the review's companion manifest aligned with the canonical working copy.
import shutil
shutil.copy2(MAIN/'character_manifest.json',ROOT/'character_manifest.json')
files=[p for p in ROOT.rglob('*') if p.is_file() and p.suffix in ('.blend','.json','.html','.gif','.jpg','.md','.py','.png','.js') and p.name!='review_checksums.json']
(ROOT/'review_checksums.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},ensure_ascii=False,indent=2),encoding='utf-8')
