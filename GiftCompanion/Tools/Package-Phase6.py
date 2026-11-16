"""Assemble a standalone candidate from explicit roots. Never edits the original Runtime or old releases."""
import argparse
import hashlib
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
REPORT = PROJECT / "Docs/Validation/Phase6"
DEST = PROJECT / "Releases/Gift-V1-Candidate"
ALLOWED_BUILD = {"GiftCompanion.exe", "GiftCompanion_Data", "MonoBleedingEdge", "UnityPlayer.dll", "UnityCrashHandler64.exe", "D3D12", "Licenses"}

def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()

def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")

def record(path, base):
    return {"path": path.relative_to(base).as_posix(), "bytes": path.stat().st_size, "sha256": digest(path)}

def regenerable_bytecode(path):
    if path.suffix.lower() not in {".pyc", ".pyo"}: return False
    source = path.parent.parent / (path.name.split(".")[0] + ".py") if path.parent.name == "__pycache__" else path.with_suffix(".py")
    return source.is_file()

def freeze():
    out = REPORT / "frozen_assets.json"
    if out.exists():
        return verify_frozen()
    old = json.loads((PROJECT / "Docs/Validation/Phase5/frozen_assets.json").read_text(encoding="utf-8-sig"))["files"]
    extra = ["Docs/Validation/Phase5/acceptance.json", "Builds/Phase5/GiftCompanion.exe", "Builds/Phase5/GiftCompanion_Data/Managed/GiftCompanion.Core.dll", "Builds/Phase5/GiftCompanion_Data/Managed/GiftCompanion.Runtime.dll"]
    paths = sorted({f["path"] for f in old} | set(extra))
    files = [record(PROJECT / p, PROJECT) for p in paths]
    write(out, {"files": files, "createdAtUtc": datetime.now(timezone.utc).isoformat()})
    print(f"Frozen {len(files)} baseline files; old Phase5 acceptance unchanged.", flush=True)

def verify_frozen():
    values = json.loads((REPORT / "frozen_assets.json").read_text(encoding="utf-8"))["files"]
    results = [{"path": v["path"], "unchanged": (PROJECT / v["path"]).is_file() and digest(PROJECT / v["path"]) == v["sha256"]} for v in values]
    write(REPORT / "frozen_verification.json", {"passed": all(r["unchanged"] for r in results), "files": results})
    if not all(r["unchanged"] for r in results):
        raise RuntimeError("Frozen baseline changed. Inspect frozen_verification.json")
    print(f"Unchanged frozen files: {len(results)}", flush=True)

def assemble():
    if DEST.exists():
        raise RuntimeError("Candidate already exists. Keep it; use --refresh to refresh only build roots.")
    source = PROJECT / "Runtime/GPT-SoVITS"
    build = PROJECT / "Builds/Phase6"
    if not (build / "GiftCompanion.exe").exists():
        raise RuntimeError("Build Phase6 first.")
    if shutil.disk_usage(PROJECT).free < 16 * 1024 ** 3:
        raise RuntimeError("Need at least 16GiB free to stage the candidate safely.")
    DEST.mkdir(parents=True)
    refresh()
    omitted = []
    for p in sorted(source.rglob("*")):
        if not p.is_file():
            continue
        if p.is_symlink():
            raise RuntimeError("Runtime source cannot contain a symlink.")
        rel = p.relative_to(source)
        if regenerable_bytecode(p) or p.suffix.lower() == ".log" or p.name == ".gitkeep":
            omitted.append({"path": rel.as_posix(), "bytes": p.stat().st_size, "reason": "regenerable Python bytecode/log/placeholder"})
            continue
        target = DEST / "Runtime/GPT-SoVITS" / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(p, target)
    write(REPORT / "runtime_trim.json", {"sourceUnchanged": True, "removedFromStagingOnly": omitted, "savedBytes": sum(r["bytes"] for r in omitted), "modelsOrNativeLibrariesRemoved": False})
    print(f"Runtime staged. Removed only bytecode/logs: {sum(r['bytes'] for r in omitted)} bytes.", flush=True)

def repair_bytecode():
    # Conservative fixup for an earlier staging run; no second multi-GB runtime copy.
    path = REPORT / "runtime_trim.json"
    report = json.loads(path.read_text(encoding="utf-8"))
    remaining, preserved = [], []
    for item in report["removedFromStagingOnly"]:
        source = PROJECT / "Runtime/GPT-SoVITS" / item["path"]
        if source.suffix.lower() in {".pyc", ".pyo"} and not regenerable_bytecode(source):
            target = DEST / "Runtime/GPT-SoVITS" / item["path"]
            target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, target); preserved.append(item)
        else: remaining.append(item)
    report.update(removedFromStagingOnly=remaining, savedBytes=sum(i["bytes"] for i in remaining), sourcelessBytecodePreserved=preserved)
    write(path, report)
    print(f"Preserved {len(preserved)} bytecode files without a corresponding source; no source runtime changed.", flush=True)

def refresh():
    build = PROJECT / "Builds/Phase6"
    DEST.mkdir(parents=True, exist_ok=True)
    for item in sorted(ALLOWED_BUILD):
        path = build / item
        if not path.exists():
            if item in {"D3D12", "UnityCrashHandler64.exe"}: continue
            raise RuntimeError(f"Missing required build root: {item}")
        target = DEST / item
        if path.is_dir(): shutil.copytree(path, target, dirs_exist_ok=True)
        else: shutil.copy2(path, target)
    docs = {"Assets/StreamingAssets/Defaults/BackupGuide.md": "资料备份与迁移.md", "Docs/phase6_player_guide.md": "README.md", "Docs/phase6_compatibility.md": "兼容范围.md", "Docs/phase6_maintenance.md": "维护说明.md"}
    for source, name in docs.items():
        if (PROJECT / source).exists(): shutil.copy2(PROJECT / source, DEST / name)
    if (PROJECT / "Runtime/GPT-SoVITS/LICENSE").exists():
        (DEST / "Licenses").mkdir(exist_ok=True)
        shutil.copy2(PROJECT / "Runtime/GPT-SoVITS/LICENSE", DEST / "Licenses/GPT-SoVITS-LICENSE.txt")
    if (PROJECT / "Docs/phase6_licenses.md").exists(): shutil.copy2(PROJECT / "Docs/phase6_licenses.md", DEST / "Licenses/THIRD_PARTY_NOTICES.md")
    shutil.copy2(PROJECT / "Packages/com.kirurobo.uniwindowcontroller/LICENSE.md", DEST / "Licenses/UniWindowController-LICENSE.md")
    # A release marker disables development runtime overrides before the final inventory is sealed.
    write(DEST / "Release/release_manifest.json", {"version": "1.0.0-candidate", "stage": "validating", "schema": 10, "unity": "6000.3.7f1", "playerAcceptance": "pending"})
    print("Phase6 build roots refreshed; runtime not copied again.", flush=True)

def seal():
    # Final player-facing records are written after the native audit; do not recopy the runtime/build.
    for source, name in {"Docs/phase6_player_guide.md": "README.md", "Docs/phase6_compatibility.md": "兼容范围.md", "Docs/phase6_maintenance.md": "维护说明.md", "Assets/StreamingAssets/Defaults/BackupGuide.md": "资料备份与迁移.md"}.items():
        shutil.copy2(PROJECT / source, DEST / name)
    allowed = ALLOWED_BUILD | {"Runtime", "Release", "README.md", "兼容范围.md", "维护说明.md", "资料备份与迁移.md"}
    if any(p.name not in allowed for p in DEST.iterdir()): raise RuntimeError("Unexpected candidate root.")
    files = []
    for p in sorted(DEST.rglob("*")):
        if not p.is_file() or p == DEST / "Release/release_manifest.json": continue
        rel = p.relative_to(DEST)
        if any(part in {"UserData", "Secrets", "Validation", "Library", "Material", "Temp"} for part in rel.parts): raise RuntimeError(f"Forbidden release data root: {rel}")
        if p.suffix == ".dpapi" or p.name in {"memory.db", "GiftCompanion.EditModeTests.dll", "GiftCompanion.PlayModeTests.dll"}: raise RuntimeError("Private or test data in release.")
        files.append(record(p, DEST))
    packages = []
    for p in sorted((DEST / "Runtime/GPT-SoVITS/runtime/Lib/site-packages").glob("*.dist-info")):
        metadata = p / "METADATA"
        if not metadata.is_file(): continue
        fields = {}
        for line in metadata.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.startswith("Name: "): fields["name"] = line[6:]
            if line.startswith("Version: "): fields["version"] = line[9:]
            if len(fields) == 2: break
        if len(fields) == 2: packages.append(fields)
    result = {"version": "1.0.0-candidate", "stage": "candidate", "schema": 10, "backupFormat": 1, "unity": "6000.3.7f1", "renderer": "D3D11/Built-in/Gamma", "createdAtUtc": datetime.now(timezone.utc).isoformat(), "playerAcceptance": "pending", "files": files, "bytes": sum(v["bytes"] for v in files), "runtimeBytes": sum(v["bytes"] for v in files if v["path"].startswith("Runtime/")), "developmentPathsRequired": False, "pythonPackages": packages, "modelProfiles": ["Runtime/GPT-SoVITS/voice.json", "Runtime/GPT-SoVITS/voice-ja.json"]}
    write(DEST / "Release/release_manifest.json", result)
    write(REPORT / "package_summary.json", {k: v for k, v in result.items() if k != "files"} | {"files": len(files), "directory": str(DEST)})
    print(f"Sealed {len(files)} files, {result['bytes']/1024**3:.3f}GiB; player acceptance pending.", flush=True)

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=["freeze", "verify-frozen", "assemble", "refresh", "seal", "repair-bytecode"])
    args = parser.parse_args()
    {"freeze": freeze, "verify-frozen": verify_frozen, "assemble": assemble, "refresh": refresh, "seal": seal, "repair-bytecode": repair_bytecode}[args.action]()
