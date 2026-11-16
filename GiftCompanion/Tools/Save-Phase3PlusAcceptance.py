"""Collect completed Phase3Plus evidence; never launch Unity/network or open user history."""
import argparse
import ast
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
REPORTS = PROJECT / "Docs/Validation/Phase3Plus"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def record(path, root):
    return dict(path=path.relative_to(root).as_posix(), sha256=sha(path), bytes=path.stat().st_size)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--checkpoint", default="Final4")
    checkpoint = parser.parse_args().checkpoint
    assert re.fullmatch(r"[A-Za-z0-9_-]+", checkpoint)
    names = ("phase3plus_delivery.md", "phase3plus_status.md", "phase3plus_checkpoint.md", "phase3_file_plan.md", "player_content_contracts.md", "architecture.md", "character_protocol.md", "memory_protocol.md", "validation.md", "v1_design.md", "development_log.md")
    doc_paths = [PROJECT.parent / "README.md", PROJECT / "README.md"] + [PROJECT / "Docs" / name for name in names]
    checked_links = []
    for path in doc_paths:
        text = path.read_text(encoding="utf-8-sig")
        assert text.count("```") % 2 == 0, path.name
        for target in re.findall(r"\]\(([^)]+)\)", text):
            target = target.strip("<>").split("#", 1)[0]
            if not target or re.match(r"[a-z]+://", target):
                continue
            resolved = (path.parent / target).resolve()
            assert resolved.exists() or resolved == (REPORTS / "acceptance.json").resolve(), (path.name, target)
            checked_links.append(resolved)
    python_tools = list((PROJECT / "Tools").glob("*.py"))
    for path in python_tools:
        ast.parse(path.read_text(encoding="utf-8-sig"), filename=path.name)
    syntax = read(REPORTS / "tool_syntax.json")
    assert syntax["passed"]
    static = dict(passed=True, verifiedAtUtc=datetime.now(timezone.utc).isoformat(), documents=len(doc_paths), localLinks=len(checked_links), pythonToolsParsed=len(python_tools), powerShellToolsParsed=syntax["count"], coreHasNoUnityReferences=True,
                  scope="UTF-8, Markdown fences/local links, Python AST and recorded PowerShell AST. acceptance.json is the output of this same collection; all links checked again after writing.")
    (REPORTS / "static_review.json").write_text(json.dumps(static, indent=2) + "\n", encoding="utf-8")
    tests, evidence = [], []
    for mode in ("EditMode", "PlayMode"):
        path = REPORTS / f"{checkpoint}.{mode}.xml"
        result = ET.parse(path).getroot().attrib
        assert result["result"] == "Passed" and int(result["passed"]) == int(result["total"]) > 0
        tests.append(dict(mode=mode, passed=int(result["passed"]), total=int(result["total"]), endedAt=result.get("end-time"), **record(path, REPORTS)))
    for name in ("player_build.json", "player_write.json", "player_restore.json", "player_nokey.json", "Window/player_probe.json", "Window/window_composition.json", "visual_review.json", "static_review.json"):
        path = REPORTS / name
        result = read(path)
        assert result["passed"] is True and all(result.get("flags", {}).values()), name
        evidence.append(dict(passed=True, **record(path, REPORTS)))
    for name in ("player_run.json", "default_music.json", "file_provider_review.json"):
        evidence.append(record(REPORTS / name, REPORTS))
    frozen = read(PROJECT / "Docs/Validation/Phase0/asset_import.json")["copiedFiles"]
    assert len(frozen) == 862
    for item in frozen:
        assert sha(PROJECT / item["target"]) == item["sha256"].lower(), item["target"]
    baseline = read(REPORTS / "checkpoint.json")["baselinePhase3Binaries"]
    for item in baseline:
        assert sha(PROJECT / item["path"]) == item["sha256"], item["path"]
    build = PROJECT / "Builds/Phase3Plus"
    assert not list(build.rglob("*.dpapi"))
    assert not list(build.rglob("GiftCompanion.Tests*.dll"))
    binaries = [record(build / name, build) for name in ("GiftCompanion.exe", "GiftCompanion_Data/Managed/GiftCompanion.Core.dll", "GiftCompanion_Data/Managed/GiftCompanion.Runtime.dll")]
    licenses = []
    for name, original in (("NotoSansCJK-OFL.txt", "Assets/App/SharedUI/Resources/Fonts/OFL.txt"), ("NotoSansCJK-Notice.txt", "Docs/Licenses/NotoSansCJK-Notice.txt"), ("Spine-Runtimes-License.txt", "Docs/Licenses/Spine-Runtimes-License.txt")):
        assert sha(build / "Licenses" / name) == sha(PROJECT / original)
        licenses.append(record(build / "Licenses" / name, build))
    music = read(REPORTS / "default_music.json")
    for item in music:
        for folder in (PROJECT / "Assets/StreamingAssets/DefaultMusic", build / "GiftCompanion_Data/StreamingAssets/DefaultMusic"):
            assert sha(folder / item["output"]) == item["outputSha256"].lower()
    sources = []
    for folder in ("Assets/App", "Assets/Editor", "Assets/Tests", "Assets/Scenes", "Assets/Characters/MainCharacter/Config", "Assets/StreamingAssets", "Tools", "Packages", "ProjectSettings"):
        sources += [record(path, PROJECT) for path in sorted((PROJECT / folder).rglob("*")) if path.is_file() and "__pycache__" not in path.parts]
    for path in (PROJECT / "Assets/App/Core").rglob("*.cs"):
        assert not re.search(r"\bUnityEngine\b|using\s+Unity", path.read_text(encoding="utf-8-sig"))
    documents = [record(path, PROJECT.parent) for path in doc_paths]
    images = [record(path, REPORTS) for path in sorted(REPORTS.rglob("*.png")) if not path.name.startswith("Attempt")]
    result = dict(passed=True, phase3PlusTechnicalComplete=True, userAcceptance="pending", verifiedAtUtc=datetime.now(timezone.utc).isoformat(), unity="6000.3.7f1", schemaVersion=8,
                  build=read(REPORTS / "player_build.json"), tests=tests, evidence=evidence, binaries=binaries, licenses=licenses, sources=sources, documents=documents, images=images,
                  frozenImportedFilesVerified=862, frozenImportedMismatches=0, baselinePhase3BinariesUnchanged=True, buildSecretFiles=0, projectTestAssembliesInBuild=0, formalUserDataMigrated=False,
                  limitations=["Native file/folder picker, OS IME candidate selection, subjective audio/appearance/persona remain manual.", "Only the existing lobby copy verified the generic space interface; no second art package provided.", "Bounded diary summaries are narrative projections of recorded dialogue, not objective psychological measurement.", "Cross-device file-provider fallback uses recoverable writes; its overwrite stage is not atomic.", "No Phase4 voice/ducking/replay, Phase5 tools or Phase6 full distribution/multi-monitor acceptance."])
    (REPORTS / "acceptance.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    assert all(path.exists() for path in checked_links)
    print(json.dumps(dict(passed=True, tests=[dict(mode=t["mode"], passed=t["passed"], total=t["total"]) for t in tests], sources=len(sources), frozenImportedFiles=862, images=len(images)), indent=2))


if __name__ == "__main__":
    main()
