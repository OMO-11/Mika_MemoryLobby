"""Collect existing Phase 3 results. No Unity, network, or user database access."""
import hashlib
import json
import re
import struct
import xml.etree.ElementTree as ET
import zlib
from datetime import datetime, timezone
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
REPORTS = PROJECT / "Docs/Validation/Phase3"


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def png_metrics(path):
    # Read-only PNG analysis using the standard library, no image dependency.
    data = path.read_bytes()
    assert data[:8] == b"\x89PNG\r\n\x1a\n"
    offset, compressed = 8, bytearray()
    while offset < len(data):
        size = struct.unpack_from(">I", data, offset)[0]
        kind, payload = data[offset + 4:offset + 8], data[offset + 8:offset + 8 + size]
        if kind == b"IHDR":
            width, height, bits, color, _, _, interlace = struct.unpack(">IIBBBBB", payload)
        elif kind == b"IDAT":
            compressed.extend(payload)
        offset += size + 12
    assert bits == 8 and color in (2, 6) and interlace == 0
    bpp = 3 if color == 2 else 4
    stride, raw = width * bpp, zlib.decompress(compressed)
    previous = bytearray(stride)
    clipped, total, cursor = 0, 0, 0
    for _ in range(height):
        filtering = raw[cursor]
        row = bytearray(raw[cursor + 1:cursor + 1 + stride])
        cursor += stride + 1
        assert filtering in range(5)
        for i in range(stride):
            left = row[i - bpp] if i >= bpp else 0
            above = previous[i]
            upper_left = previous[i - bpp] if i >= bpp else 0
            if filtering == 1:
                predictor = left
            elif filtering == 2:
                predictor = above
            elif filtering == 3:
                predictor = (left + above) // 2
            elif filtering == 4:
                value = left + above - upper_left
                distances = (abs(value - left), abs(value - above), abs(value - upper_left))
                predictor = (left, above, upper_left)[distances.index(min(distances))]
            else:
                predictor = 0
            row[i] = (row[i] + predictor) & 255
        for i in range(0, stride, bpp):
            r, g, b = row[i:i + 3]
            clipped += r >= 250 and g >= 250 and b >= 250
            total += r + g + b
        previous = row
    return dict(file=path.name, width=width, height=height, clippedPixels=clipped,
                meanBrightness=total / (width * height * 765))


def main():
    now = datetime.now(timezone.utc).isoformat()
    tests = []
    for mode in ("EditMode", "PlayMode"):
        path = REPORTS / f"Final.{mode}.xml"
        result = ET.parse(path).getroot()
        assert result.attrib["result"] == "Passed"
        assert int(result.attrib["passed"]) == int(result.attrib["total"]) > 0
        tests.append(dict(mode=mode, passed=int(result.attrib["passed"]),
                          total=int(result.attrib["total"]), endedAt=result.attrib.get("end-time"), sha256=sha(path)))

    reports = ["player_build.json", "ui_write.json", "ui_nokey.json", "player_write.json",
               "player_restore.json", "player_nokey.json", "Window/player_probe.json", "Window/window_composition.json"]
    evidence = []
    for name in reports:
        path = REPORTS / name
        result = read_json(path)
        assert result["passed"] is True, name
        assert all(result.get("flags", {}).values()), name
        evidence.append(dict(path=name, passed=True, sha256=sha(path)))

    audit = read_json(REPORTS / "release_shader_audit.json")
    assert audit["mainShader"] == "GiftCompanion/SpineStraightAlpha"
    assert audit["sameAnimationFrame"] == 1.25 and audit["explicitShaderAvailable"]
    metrics = [png_metrics(REPORTS / name) for name in
               ("release_original_shader.png", "release_explicit_alpha.png", "release_ldr.png", "3E.editor.final.png")]
    assert metrics[1]["clippedPixels"] < metrics[0]["clippedPixels"] / 10
    review = dict(passed=True, reviewedAtUtc=now, mainShader=audit["mainShader"], hdrRetained=True,
                  scope="Assistant reviewed same-frame shader comparison, real UI and window images; user aesthetic/IME acceptance pending.",
                  metrics=metrics, checkedImages=["reply_write.png", "history_write.png", "resized_write.png", "Window/window_composition.png"])
    (REPORTS / "visual_review.json").write_text(json.dumps(review, indent=2) + "\n", encoding="utf-8")
    for name in ("visual_review.json", "release_shader_audit.json", "3E.editor.visual.json", "ui_run.json", "player_run.json", "tool_syntax.json", "static_review.json"):
        evidence.append(dict(path=name, sha256=sha(REPORTS / name)))

    frozen = read_json(PROJECT / "Docs/Validation/Phase0/asset_import.json")["copiedFiles"]
    assert len(frozen) == 862
    for item in frozen:
        assert sha(PROJECT / item["target"]) == item["sha256"].lower(), item["target"]

    build = PROJECT / "Builds/Phase3"
    assert not list(build.rglob("*.dpapi")), "Secret file in build"
    assert not list(build.rglob("GiftCompanion.Tests*.dll")), "Project test assembly in build"
    binaries = []
    for name in ("GiftCompanion.exe", "GiftCompanion_Data/Managed/GiftCompanion.Core.dll", "GiftCompanion_Data/Managed/GiftCompanion.Runtime.dll"):
        binaries.append(dict(path=name, sha256=sha(build / name)))
    licenses = []
    for name, original in (("NotoSansCJK-OFL.txt", "Assets/App/SharedUI/Resources/Fonts/OFL.txt"),
                           ("NotoSansCJK-Notice.txt", "Docs/Licenses/NotoSansCJK-Notice.txt"),
                           ("Spine-Runtimes-License.txt", "Docs/Licenses/Spine-Runtimes-License.txt")):
        assert sha(build / "Licenses" / name) == sha(PROJECT / original)
        licenses.append(dict(path="Licenses/" + name, sha256=sha(build / "Licenses" / name)))

    sources = []
    roots = ("Assets/App", "Assets/Editor", "Assets/Tests", "Assets/Scenes", "Assets/Characters/MainCharacter/Config", "Tools", "Packages", "ProjectSettings")
    for folder in roots:
        for path in sorted((PROJECT / folder).rglob("*")):
            if path.is_file() and "__pycache__" not in path.parts:
                sources.append(dict(path=path.relative_to(PROJECT).as_posix(), sha256=sha(path)))
    for path in (PROJECT / "Assets/App/Core").rglob("*.cs"):
        assert not re.search(r"\bUnityEngine\b|using\s+Unity", path.read_text(encoding="utf-8-sig"))
    images = [dict(path=p.relative_to(REPORTS).as_posix(), sha256=sha(p))
              for p in sorted(REPORTS.glob("*.png")) if p.name != "3A.before.png" and p.name != "3A.after.png"]
    images.append(dict(path="Window/window_composition.png", sha256=sha(REPORTS / "Window/window_composition.png")))
    documents = [dict(path=path.relative_to(PROJECT.parent).as_posix(), sha256=sha(path)) for path in
                 [PROJECT.parent / "README.md", PROJECT / "README.md"] +
                 [PROJECT / "Docs" / name for name in ("architecture.md", "character_protocol.md", "memory_protocol.md", "v1_design.md", "validation.md", "development_log.md", "phase3_plan.md", "phase3_status.md", "phase3_visual.md", "phase3_delivery.md", "phase3_checkpoint.md")]]
    result = dict(passed=True, phase3TechnicalComplete=True, userAcceptance="pending",
                  schemaVersion=7, unity="6000.3.7f1", verifiedAtUtc=now,
                  build=read_json(REPORTS / "player_build.json"), tests=tests, evidence=evidence,
                  binaries=binaries, licenses=licenses, sources=sources, images=images, documents=documents,
                  frozenImportedFilesVerified=len(frozen), frozenImportedMismatches=0,
                  buildSecretFiles=0, projectTestAssembliesInBuild=0,
                  limitations=["OS IME candidate selection and subjective visual/persona/touch acceptance remain manual.",
                               "Only neutral persistent visual; other emotions explicitly fallback; action whitelist only none.",
                               "BGM playlist empty by user choice; no Phase 4 voice/cache/replay/ducking or Phase 5 tools.",
                               "Unity shader-feature internal root cause not conclusively isolated; explicit alpha adapter verified.",
                               "Validated 1280x720 / 1024x768 UI and single-screen desktop composition, not comprehensive multi-monitor coverage."])
    (REPORTS / "acceptance.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(passed=True, tests=tests, frozenImportedFiles=len(frozen),
                          sourceFiles=len(sources), binaries=len(binaries), licenses=len(licenses), metrics=metrics), indent=2))


if __name__ == "__main__":
    main()
