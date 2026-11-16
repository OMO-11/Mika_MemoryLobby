"""Read-only expression-pack audit and inspection sheets for CH0069_home."""
from __future__ import annotations
import hashlib
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Material/Mika_MemoryLobby/spine_regions"
HOME = ROOT / "Material/Mika_MemoryLobby_Spine/spine/CH0069_home"
OUT = ROOT / "Docs/Audits/Spine/MemoryLobby/ExpressionRegions"
OUT.mkdir(parents=True, exist_ok=True)
FONT = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 17)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def board(size):
    result = Image.new("RGBA", size, (224, 226, 232, 255))
    draw = ImageDraw.Draw(result)
    for y in range(0, size[1], 12):
        for x in range(0, size[0], 12):
            if (x // 12 + y // 12) % 2:
                draw.rectangle((x, y, x + 11, y + 11), fill=(207, 211, 219, 255))
    return result

records = []
small = []
for path in sorted(SOURCE.glob("*.png")):
    with Image.open(path) as image:
        image.load()
        rgba = image.convert("RGBA")
        alpha = rgba.getchannel("A")
        records.append({"file": path.name, "width": image.width, "height": image.height,
                        "mode": image.mode, "alpha_bounds": alpha.getbbox(),
                        "alpha_extrema": alpha.getextrema(), "sha256": digest(path),
                        "rgba_sha256": hashlib.sha256(rgba.tobytes()).hexdigest()})
        if not path.name.startswith("CH0069") and path.name != "Halo.png":
            small.append((path.name, rgba.copy()))

for page in range(math.ceil(len(small) / 16)):
    sheet = Image.new("RGB", (960, 1000), (30, 35, 48))
    draw = ImageDraw.Draw(sheet)
    for index, (name, image) in enumerate(small[page * 16:(page + 1) * 16]):
        x = (index % 4) * 240
        y = (index // 4) * 250
        cell = board((220, 190))
        enlarged = image.copy()
        enlarged.thumbnail((210, 180), Image.Resampling.LANCZOS)
        cell.alpha_composite(enlarged, ((220 - enlarged.width) // 2, (190 - enlarged.height) // 2))
        sheet.paste(cell.convert("RGB"), (x + 10, y + 48))
        draw.text((x + 10, y + 3), name, fill="white", font=FONT)
        draw.text((x + 10, y + 26), f"{image.width}x{image.height}", fill=(180, 190, 210), font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 13))
    sheet.save(OUT / f"expression_sheet_{page + 1:02}.png")

focus = Image.open(SOURCE / "10.png").convert("RGBA")
detail = board((600, 500))
scaled = focus.resize((focus.width * 4, focus.height * 4), Image.Resampling.NEAREST)
detail.alpha_composite(scaled, ((600 - scaled.width) // 2, (500 - scaled.height) // 2))
detail.convert("RGB").save(OUT / "10_detail_nearest.png")

comparison = Image.new("RGB", (920, 440), (30, 35, 48))
draw = ImageDraw.Draw(comparison)
draw.text((18, 12), "10.png 原件放大（未贴到模型）", font=FONT, fill="white")
draw.text((478, 12), "大厅原件（既有 Unity 留证裁剪）", font=FONT, fill="white")
left = board((440, 380))
enlarged = focus.resize((384, 348), Image.Resampling.NEAREST)
left.alpha_composite(enlarged, (28, 16))
comparison.paste(left.convert("RGB"), (10, 50))
baseline = ROOT / "Docs/Audits/Spine/MemoryLobby/RuntimeBehavior/v02_hit_base.png"
if baseline.exists():
    native = Image.open(baseline).convert("RGB").crop((950, 300, 1270, 620))
    native.thumbnail((380, 380), Image.Resampling.LANCZOS)
    comparison.paste(native, (500, 50))
    comparison.save(OUT / "10_vs_home_reference.png")

# Parse the delivered text atlas; its size fields are unrotated region dimensions.
regions = []
page_name = None
current = None
for raw in (HOME / "CH0069_home.atlas").read_text(encoding="utf-8-sig").splitlines():
    line = raw.strip()
    if not line:
        continue
    if not raw.startswith(" ") and ":" not in raw:
        if line.endswith(".png"):
            page_name = line
            current = None
        else:
            current = {"name": line, "page": page_name}
            regions.append(current)
    elif current is not None and ":" in line:
        key, value = line.split(":", 1)
        current[key] = value.strip()

selected = [r for r in regions if r["name"] == "Face" or r["name"].startswith(("L_Eye", "R_Eye", "Mouse_", "Nose", "Flush"))]
decoded = []
pages = {}
for region in selected:
    if region["page"] not in pages:
        pages[region["page"]] = Image.open(HOME / region["page"]).convert("RGBA")
    x, y = map(int, region["xy"].split(","))
    w, h = map(int, region["size"].split(","))
    rotation = region.get("rotate", "false")
    degrees = 90 if rotation == "true" else 0 if rotation == "false" else int(rotation)
    crop = pages[region["page"]].crop((x, y, x + (h if degrees % 180 else w), y + (w if degrees % 180 else h)))
    if degrees:
        crop = crop.rotate(degrees, expand=True)
    decoded.append((region, crop))
    crop.save(OUT / ("home_region_" + region["name"] + ".png"))

for side in ["L_Eye", "R_Eye", "Mouse_", "Face"]:
    chosen = [(r, im) for r, im in decoded if r["name"].startswith(side)]
    sheet = Image.new("RGB", (1000, math.ceil(len(chosen) / 5) * 160), (30, 35, 48))
    draw = ImageDraw.Draw(sheet)
    for index, (region, im) in enumerate(chosen):
        x, y = (index % 5) * 200, (index // 5) * 160
        cell = board((190, 118))
        scaled = im.copy()
        scaled.thumbnail((180, 108), Image.Resampling.LANCZOS)
        cell.alpha_composite(scaled, ((190 - scaled.width) // 2, (118 - scaled.height) // 2))
        sheet.paste(cell.convert("RGB"), (x + 5, y + 36))
        draw.text((x + 4, y + 3), region["name"], fill="white", font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 12))
        draw.text((x + 4, y + 18), f"{im.width}x{im.height}", fill=(180, 190, 210), font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 12))
    sheet.save(OUT / ("home_" + side.rstrip("_") + "_sheet.png"))

groups = {}
for row in records:
    groups.setdefault(row["rgba_sha256"], []).append(row["file"])
report = {"files": records, "file_count": len(records), "expression_count": len(small),
          "identical_rgba_groups": [v for v in groups.values() if len(v) > 1],
          "home_atlas_region_count": len(regions), "home_selected_regions": selected,
          "method": "PNG decode and alpha inspection; text atlas region extraction; no character asset changes."}

def anchor(image):
    """Longest nontransparent horizontal run, used only to locate exact crop candidates."""
    pixels = image.load()
    best = (0, 0, 0)
    for y in range(image.height):
        start = None
        for x in range(image.width + 1):
            visible = x < image.width and pixels[x, y][3] > 0
            if visible and start is None:
                start = x
            if not visible and start is not None:
                if x - start > best[2]:
                    best = (start, y, x - start)
                start = None
    return best

canvases = [(p.name, Image.open(p).convert("RGBA")) for p in SOURCE.glob("CH0069*.png")]
canvases += [("home/" + p.name, Image.open(p).convert("RGBA")) for p in HOME.glob("*.png")]
canvas_bytes = [(name, image, image.tobytes()) for name, image in canvases]
matches = []
for name, image in small:
    ax, ay, length = anchor(image)
    needle = image.crop((ax, ay, ax + length, ay + 1)).tobytes()
    found = []
    for page, canvas, raw in canvas_bytes:
        position = raw.find(needle)
        while position >= 0:
            pixel = position // 4
            x, y = pixel % canvas.width - ax, pixel // canvas.width - ay
            if position % 4 == 0 and x >= 0 and y >= 0 and x + image.width <= canvas.width and y + image.height <= canvas.height:
                crop = canvas.crop((x, y, x + image.width, y + image.height))
                if crop.tobytes() == image.tobytes():
                    found.append({"page": page, "xy": [x, y], "pixel_exact": True})
            position = raw.find(needle, position + 1)
    matches.append({"file": name, "matches": found})
report["exact_unrotated_crop_matches"] = matches
report["match_limits"] = "Exact RGBA crops at delivered orientation and resolution only; no similarity or transformed-image search. No home match is not proof that no adapted variant can be made."

# Connected components identify which isolated marks in 10.png need independent placement.
alpha = focus.getchannel("A")
seen = set()
components = []
for y in range(focus.height):
    for x in range(focus.width):
        if (x, y) in seen or alpha.getpixel((x, y)) <= 8:
            continue
        pending = [(x, y)]
        seen.add((x, y))
        points = []
        while pending:
            px, py = pending.pop()
            points.append((px, py))
            for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                if 0 <= nx < focus.width and 0 <= ny < focus.height and (nx, ny) not in seen and alpha.getpixel((nx, ny)) > 8:
                    seen.add((nx, ny))
                    pending.append((nx, ny))
        if len(points) >= 5:
            components.append({"pixels_above_alpha_8": len(points), "bounds": [min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points) + 1, max(p[1] for p in points) + 1]})
report["10_components_alpha_threshold_8"] = sorted(components, key=lambda c: (c["bounds"][1], c["bounds"][0]))
(OUT / "expression_inventory.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"file_count": len(records), "expression_count": len(small),
                  "duplicates": report["identical_rgba_groups"],
                  "portrait_exact_match_count": sum(any(not m["page"].startswith("home/") for m in r["matches"]) for r in matches),
                  "home_exact_match_count": sum(any(m["page"].startswith("home/") for m in r["matches"]) for r in matches),
                  "10_matches": next(r["matches"] for r in matches if r["file"] == "10.png"),
                  "10_components": report["10_components_alpha_threshold_8"]}, ensure_ascii=False))
