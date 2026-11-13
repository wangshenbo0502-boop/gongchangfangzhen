"""Exercise the optional review controls against the reopened source."""
import json
import runpy
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent
module = runpy.run_path(str(HERE/"cabinet_review_ui.py"))
results = []
for state, name in enumerate(module["SCENES"]):
    status = bpy.ops.cabinet.review_state(index=state)
    parts = [sorted(bpy.ops.cabinet.review_part(index=i)) for i in range(len(module["GROUP_NAMES"]))]
    results.append({"scene": name, "operator": sorted(status), "parts": parts})
bpy.ops.cabinet.review_state(index=0)
scene = bpy.context.scene
frames = []
for progress in (0, .25, 1, 0):
    scene.cabinet_review_progress = progress
    frames.append(scene.frame_current)
report = {"states": results, "slider_frames": frames,
          "passed": all(r["operator"] == ["FINISHED"]
                        and all(p == ["FINISHED"] for p in r["parts"]) for r in results)
                    and frames == [1, 31, 121, 1],
          "scope": "Operator execution and slider mapping; no mouse/UI screenshot assertion."}
(HERE.parent/"reports"/"electrical-cabinet-review-ui.json").write_text(
    json.dumps(report, indent=2), encoding="utf-8")
print("CABINET_REVIEW_UI", report["passed"], frames, flush=True)
