"""Record whether standalone use changed the installation tree; never inspect player data."""
import argparse
import json
from pathlib import Path

project = Path(__file__).resolve().parents[1]
reports = project / "Docs/Validation/Phase6"
parser = argparse.ArgumentParser()
parser.add_argument("action", choices=["capture", "verify"])
parser.add_argument("directory")
parser.add_argument("--reports")
args = parser.parse_args()
if args.reports:
    reports = Path(args.reports).resolve()
reports.mkdir(parents=True, exist_ok=True)
directory = Path(args.directory).resolve()
if not directory.is_relative_to(project / "Releases"):
    raise RuntimeError("Only this project's release staging directories may be audited.")
files = {p.relative_to(directory).as_posix(): {"bytes": p.stat().st_size, "mtime_ns": p.stat().st_mtime_ns} for p in directory.rglob("*") if p.is_file()}
snapshot = reports / "install_before.json"
if args.action == "capture":
    snapshot.write_text(json.dumps(files, indent=2), encoding="utf-8")
    print(f"Captured {len(files)} installation file metadata records.")
else:
    before = json.loads(snapshot.read_text(encoding="utf-8"))
    changed = [p for p in files if p in before and files[p] != before[p]]
    added = sorted(set(files) - set(before))
    removed = sorted(set(before) - set(files))
    result = {"passed": not changed and not added and not removed, "changed": changed, "added": added, "removed": removed, "scope": "Installation tree names, sizes and modification timestamps; final release manifest separately records full SHA256."}
    (reports / "install_write_verification.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False))
    if not result["passed"]: raise RuntimeError("Standalone use modified the installation tree.")
