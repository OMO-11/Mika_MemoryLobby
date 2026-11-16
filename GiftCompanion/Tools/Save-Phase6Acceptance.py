"""Collect completed Phase6 technical evidence. Player acceptance remains pending."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
reports = root / "Docs/Validation/Phase6"

def read(path): return json.loads(path.read_text(encoding="utf-8-sig"))
def digest(path):
    sha = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""): sha.update(block)
    return sha.hexdigest()

tests = {}
for mode in ["EditMode", "PlayMode"]:
    checkpoint = "ReleaseCandidate" if mode == "EditMode" else "FinalPreview"
    test = ET.parse(reports / f"{checkpoint}.{mode}.xml").getroot()
    if test.attrib["result"] != "Passed" or int(test.attrib["failed"]) != 0: raise RuntimeError("Final test results did not pass.")
    tests[mode] = int(test.attrib["passed"])
build = read(reports / "player_build.json")
if not build["passed"]: raise RuntimeError("Build failed.")
native = {}
for path in ["Standalone/Offline.json", "Standalone/Restored.json", "Relocated/Voice.json", "Stability/Stability.json"]:
    result = read(reports / path)
    if not result["passed"]: raise RuntimeError("Native check failed: " + path)
    native[path] = {"checks": len(result["flags"]), "metrics": result["metrics"]}
frozen = read(reports / "frozen_verification.json")
if not frozen["passed"]: raise RuntimeError("Frozen baseline changed.")
package = read(reports / "package_summary.json")
files = []
for directory in ["Assets/App", "Assets/Tests", "Assets/Editor", "Assets/StreamingAssets/Voice", "Tools"]:
    for p in sorted((root / directory).rglob("*")):
        if p.is_file() and p.suffix in {".cs", ".py", ".ps1", ".asmdef"}:
            files.append({"path": p.relative_to(root).as_posix(), "sha256": digest(p)})
artifacts = []
for path in ["Releases/Gift-V1-Candidate/GiftCompanion.exe", "Releases/Gift-V1-Candidate/GiftCompanion_Data/Managed/GiftCompanion.Core.dll", "Releases/Gift-V1-Candidate/GiftCompanion_Data/Managed/GiftCompanion.Runtime.dll", "Releases/Gift-V1-Candidate/Release/release_manifest.json"]:
    p = root / path
    artifacts.append({"path": path, "bytes": p.stat().st_size, "sha256": digest(p)})
result = {"passed": True, "scope": "Phase6 technical candidate only", "playerAcceptance": "pending", "schema": 10, "backupFormat": 1, "tests": tests, "native": native, "package": package, "frozenFilesUnchanged": len(frozen["files"]), "artifacts": artifacts, "sourceFiles": files, "knownUnverified": ["Phase5 player feedback postponed", "1-2 hour actual player use", "100/125/200 percent DPI and current multi-monitor topology", "lock/sleep/monitor unplug", "another Windows account/computer/GPU", "in-app real-key save-and-restart player flow"], "verifiedAtUtc": datetime.now(timezone.utc).isoformat()}
(reports / "acceptance.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Phase6 candidate evidence: {tests}, {sum(v['checks'] for v in native.values())} native checks, {len(frozen['files'])} baseline files unchanged. Player acceptance pending.")
