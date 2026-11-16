"""Assemble QA previews from Unity captures; source character assets are untouched."""
from collections import Counter
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[2]
output = root / "Docs/Audits/Spine/MemoryLobby/RuntimeBehavior"
font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 24)
base = Image.open(output / "v02_hit_base.png").convert("RGBA")
overlay = Image.new("RGBA", base.size)
draw = ImageDraw.Draw(overlay)
geometry = json.loads((output / "v02_head_hit_geometry.json").read_text(encoding="utf-8"))
colors = [(30, 220, 110), (20, 205, 230), (80, 135, 255), (80, 135, 255)]
for region, color in zip(geometry["regions"], colors):
    pixels, triangles = region["pixels"], region["triangles"]
    points = list(zip(pixels[::2], pixels[1::2]))
    edges = Counter()
    for i in range(0, len(triangles), 3):
        ids = triangles[i:i+3]
        draw.polygon([points[index] for index in ids], fill=(*color, 38))
        for a, b in zip(ids, ids[1:] + ids[:1]):
            edges[tuple(sorted((a, b)))] += 1
    for (a, b), count in edges.items():
        if count == 1:
            draw.line([points[a], points[b]], fill=(*color, 230), width=2)
draw.rounded_rectangle((12, 12, 730, 102), radius=8, fill=(12, 17, 30, 215))
draw.text((26, 22), "v0.2 摸头区域：沿原作网格轮廓", font=font, fill="white")
draw.text((26, 59), "绿：脸与头顶　青：发髻　蓝：短前发", font=font, fill="white")
Image.alpha_composite(base, overlay).convert("RGB").save(output / "v02_head_hit_review.png")

frames = [Image.open(path).convert("RGB") for path in sorted((output / "V02LiveFrames").glob("frame_*.png"))]
if len(frames) == 32:
    palette = frames[0].quantize(colors=128)
    animation = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    animation[0].save(output / "v02_voice_pat_preview.gif", save_all=True, append_images=animation[1:], duration=130, loop=0, disposal=2)
else:
    raise RuntimeError(f"Expected 32 live preview frames, got {len(frames)}")

sheet = Image.new("RGB", (1000, 390), (23, 28, 44))
labels = ImageDraw.Draw(sheet)
for index, percent in enumerate((15, 45, 90)):
    frame = Image.open(output / f"v02_mouth_{percent}.png")
    crop = frame.crop((975, 430, 1135, 585)).resize((320, 310), Image.Resampling.LANCZOS)
    sheet.paste(crop, (10 + index * 330, 55))
    labels.text((14 + index * 330, 14), f"开合强度 {percent}%", font=font, fill="white")
sheet.save(output / "v02_mouth_review.png")
print(json.dumps({"live_frames": len(frames), "outputs": ["v02_head_hit_review.png", "v02_voice_pat_preview.gif", "v02_mouth_review.png"]}, ensure_ascii=False))
