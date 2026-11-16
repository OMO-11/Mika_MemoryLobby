"""Assemble evidence only after the final Player probes. No API keys or user databases are read."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
reports = root / "Docs/Validation/Phase5"

def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()

frozen = json.loads((reports / "frozen_assets.json").read_text(encoding="utf-8-sig"))["files"]
verification = []
for item in frozen:
    path = (root / item["path"]).resolve()
    actual = digest(path)
    verification.append({"path": item["path"], "unchanged": actual.lower() == item["sha256"].lower()})
(reports / "frozen_verification.json").write_text(json.dumps({"passed": all(r["unchanged"] for r in verification), "files": verification}, ensure_ascii=False, indent=2), encoding="utf-8")
if not all(r["unchanged"] for r in verification):
    raise SystemExit("Frozen assets changed; see frozen_verification.json")

tests = {}
for mode in ("EditMode", "PlayMode"):
    run = ET.parse(reports / f"Final.{mode}.xml").getroot()
    assert run.attrib["failed"] == "0" and run.attrib["result"] == "Passed", mode
    tests[mode] = int(run.attrib["passed"])

build_file = reports / "player_build.json"
build = json.loads(build_file.read_text(encoding="utf-8-sig"))
assert build["passed"] and build["errors"] == 0
native = {}
for mode in ("live", "nokey"):
    data = json.loads((reports / f"player_{mode}.json").read_text(encoding="utf-8-sig"))
    composition = json.loads((reports / f"composition_{mode}.json").read_text(encoding="utf-8-sig"))
    assert data["passed"] and all(data["flags"].values()) and composition["passed"], mode
    assert datetime.fromisoformat(data["verifiedAtUtc"]).timestamp() >= build_file.stat().st_mtime, "Probe predates final build"
    native[mode] = {"checks": len(data["flags"]), "composition": composition, "verifiedAtUtc": data["verifiedAtUtc"]}

artifacts = {}
for relative in ("GiftCompanion.exe", "GiftCompanion_Data/Managed/GiftCompanion.Core.dll", "GiftCompanion_Data/Managed/GiftCompanion.Runtime.dll"):
    path = root / "Builds/Phase5" / relative
    artifacts[relative] = {"bytes": path.stat().st_size, "sha256": digest(path)}
sources = []
for directory in ("Assets/App", "Assets/Editor", "Assets/Tests"):
    for path in sorted((root / directory).rglob("*")):
        if path.is_file() and path.suffix in (".cs", ".asmdef"):
            sources.append({"path": path.relative_to(root).as_posix(), "sha256": digest(path)})
for path in sorted((root / "Tools").glob("*Phase5*")):
    if path.is_file():
        sources.append({"path": path.relative_to(root).as_posix(), "sha256": digest(path)})
shipping = root / "Builds/Phase5"
assert not any(p.is_file() and (p.suffix == ".dpapi" or p.name.startswith("GiftCompanion.Tests") or "nunit" in p.name.lower()) for p in shipping.rglob("*")), "Unexpected key or test artifact"
result = {"passed": True, "playerAcceptance": "pending", "schema": 10, "unity": build["unity"], "tests": tests, "buildBytes": build["bytes"], "native": native, "frozenFilesUnchanged": len(verification), "artifacts": artifacts, "sourceFiles": sources, "shippingKeysOrTests": False, "verifiedAtUtc": datetime.now(timezone.utc).isoformat()}
(reports / "acceptance.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Phase5 technical acceptance passed: {tests}, {len(verification)} frozen files unchanged. Player acceptance pending.")
