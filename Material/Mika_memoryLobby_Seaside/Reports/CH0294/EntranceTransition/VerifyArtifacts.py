import json
from pathlib import Path
import numpy as np
from PIL import Image

root = Path(__file__).resolve().parent
live = json.loads((root / "playmode_validation.json").read_text(encoding="utf-8"))
assert live["passed"] and live["speed"] == 1.25 and 13 < live["gameSeconds"] < 15
checks = json.loads((root / "final_validation.json").read_text(encoding="utf-8"))
assert all(row["passed"] for row in checks if "fps" in row)
assert checks[-1]["resumedWithoutSkipping"]
minimum_alpha = 255
white_frames = []
frames = sorted((root / "Frames").glob("frame_*.png"))
for index, path in enumerate(frames):
    pixels = np.asarray(Image.open(path).convert("RGBA"))
    minimum_alpha = min(minimum_alpha, int(pixels[:, :, 3].min()))
    if np.all(pixels[:, :, :3] == 255):
        white_frames.append(index)
assert white_frames, "No fully white frame covering the pose change"
gif = Image.open(root / "WhiteFade_Preview.gif")
result = dict(frames=len(frames), minimumAlpha=minimum_alpha, fullyWhiteFrames=white_frames,
              gifFrames=gif.n_frames, livePassed=live["passed"])
(root / "artifact_validation.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result))
