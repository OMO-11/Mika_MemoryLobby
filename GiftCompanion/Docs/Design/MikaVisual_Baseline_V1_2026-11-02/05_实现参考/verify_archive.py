"""Read-only integrity verification of the frozen visual baseline. Python standard library only."""
from pathlib import Path
import hashlib,json,sys
root=Path(__file__).resolve().parent.parent
manifest=json.loads((root/'07_来源与清单/资源清单.json').read_text(encoding='utf-8'))
failures=[]
for entry in manifest['files']:
 path=(root/entry['path']).resolve()
 if not path.is_relative_to(root):failures.append(entry['path']+': invalid path');continue
 if not path.is_file():failures.append(entry['path']+': missing');continue
 if path.stat().st_size!=entry['bytes'] or hashlib.sha256(path.read_bytes()).hexdigest()!=entry['sha256']:failures.append(entry['path']+': changed')
print(json.dumps({'passed':not failures,'checked':len(manifest['files']),'failures':failures},ensure_ascii=False,indent=2))
sys.exit(bool(failures))
