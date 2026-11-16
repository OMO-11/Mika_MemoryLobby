"""Collect existing Phase4 evidence and hashes; never starts Unity, TTS, or a cloud call."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
REPORTS = ROOT / "Docs/Validation/Phase4"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    with path.open("rb") as stream:
        value = hashlib.sha256()
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def record(path, base=ROOT):
    return {"path": path.relative_to(base).as_posix(), "sha256": digest(path), "bytes": path.stat().st_size}


def tests(name):
    path = REPORTS / (name + ".xml")
    data = ET.parse(path).getroot().attrib
    return {"passed": data["result"] == "Passed" and int(data["failed"]) == 0 and int(data["total"]) > 0,
            "total": int(data["total"]), "successful": int(data["passed"]), "evidence": record(path)}


def main():
    build = read(REPORTS / "player_build.json")
    checks = {"editMode": tests("Final2.EditMode"), "playMode": tests("Final3.PlayMode")}
    evidence = {}
    for name in ("player_write", "player_restore", "player_nokey", "player_faults", "visual_review", "static_review", "process_review"):
        path = REPORTS / (name + ".json")
        evidence[name] = {"passed": read(path)["passed"], "evidence": record(path)}
    frozen = read(ROOT / "Docs/Validation/Phase0/asset_import.json")["copiedFiles"]
    mismatches = [item["target"] for item in frozen if not (ROOT / item["target"]).is_file() or digest(ROOT / item["target"]) != item["sha256"]]
    old = {}
    for phase in ("Phase3", "Phase3Plus"):
        prior = read(ROOT / "Docs/Validation" / phase / "acceptance.json")
        old[phase] = all(digest(ROOT / "Builds" / phase / item["path"]) == item["sha256"] for item in prior["binaries"])
    build_root = ROOT / "Builds/Phase4"
    files = [file for file in build_root.rglob("*") if file.is_file()]
    secrets = [file for file in files if file.suffix.lower() == ".dpapi"]
    test_assemblies = [file for file in files if file.name in ("GiftCompanion.EditModeTests.dll", "GiftCompanion.PlayModeTests.dll")]
    source_files = set()
    for name in ("Assets/App", "Assets/Editor", "Assets/Tests", "Assets/CompanionValidation/Runtime", "Assets/Characters/MainCharacter/Config",
                 "Assets/StreamingAssets/Defaults", "Assets/StreamingAssets/Voice", "Tools", "ProjectSettings", "Packages"):
        source_files.update(file for file in (ROOT / name).rglob("*") if file.is_file() and "__pycache__" not in file.parts and file.suffix != ".pyc")
    source_files.update((ROOT / "Docs").glob("*.md"))
    source_files.update((ROOT / "README.md",))
    passed = bool(build["passed"] and build["errors"] == 0 and all(check["passed"] for check in checks.values()) and
                  all(check["passed"] for check in evidence.values()) and not mismatches and all(old.values()) and not secrets and not test_assemblies)
    result = {"passed": passed, "phase4TechnicalComplete": passed, "userAcceptance": "pending",
              "verifiedAtUtc": datetime.now(timezone.utc).isoformat(), "unity": build["unity"], "schemaVersion": 9,
              "defaultLanguage": "zh", "japaneseEndToEndAcceptance": "not_part_of_this_run", "build": build, "tests": checks, "evidence": evidence,
              "binaries": [record(file, build_root) for file in files if file.name in ("GiftCompanion.exe", "GiftCompanion.Core.dll", "GiftCompanion.Runtime.dll")],
              "sources": [record(file) for file in sorted(source_files)], "images": [record(file) for file in sorted(REPORTS.glob("*.png"))],
              "licenses": [record(file, build_root) for file in sorted((build_root / "Licenses").glob("*.txt"))],
              "frozenImportedFilesVerified": len(frozen), "frozenImportedMismatches": mismatches, "previousBinariesUnchanged": old,
              "buildSecretFiles": len(secrets), "projectTestAssembliesInBuild": len(test_assemblies), "formalConversationDatabaseUsedForTests": False,
              "limitations": ["Player listening, voice similarity, timing preference and detailed UI polish remain for player feedback.",
                              "Observed short real reply may finish text before audio; controlled streaming test verifies no full-reply barrier.",
                              "Development launcher shares the existing Runtime; portable installation and further runtime trimming remain Phase6.",
                              "Unadapted Spine spaces use audio/subtitles with safe basic visuals; no missing mouth mapping is invented.",
                              "Diary remains pending player review; Phase5 tools are not enabled."]}
    (REPORTS / "acceptance.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"passed": passed, "sources": len(source_files), "frozen": len(frozen), "evidence": len(evidence),
                      "acceptanceSha256": digest(REPORTS / "acceptance.json")}, ensure_ascii=False))
    if not passed:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
