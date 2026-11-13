"""In-memory negative control for the PSU casing intersection check; never save."""
import json
import runpy
import sys
from pathlib import Path

from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import refine_cabinet_duct_entries as entry

model = HERE.parent
report = json.loads(entry.REPORT.read_text())
for n, obj in enumerate(entry.work.wire_family("24VFeed")):
    p = entry.work.old_centers(obj, 5)[0]
    x = entry.work.duct_slot(p.x, -.448)
    end = Vector((x, .177, 1.613))
    points = entry.work.cubic(p, (p.x, p.y, 1.627),
                              (x, .115, 1.619+(n % 3)*.001), end, count=6)
    points += entry.work.cubic(end, (x, .190, 1.607), (x, .191, 1.595),
                               (x, .191, 1.588), count=3)[1:]
    entry.work.wire(obj, points, .0018)
changed = {r["name"]: r for r in entry.work.REPORT["routes"]}
report["routes"] = [changed.get(r["name"], r) for r in report["routes"]]
report["scope"] = "Unsaved negative control recreating the rejected early PSU bend."
input_report = model / "reports/electrical-cabinet-psu-negative-input.json"
output = model / "reports/electrical-cabinet-psu-negative-check.json"
input_report.write_text(json.dumps(report, indent=2), encoding="utf-8")
sys.argv = ["verify_cabinet_routing.py", "--", "--report", str(input_report),
            "--devices", "--output", str(output)]
runpy.run_path(str(HERE / "verify_cabinet_routing.py"), run_name="__main__")
result = json.loads(output.read_text())
hits = [hit for hit in result["actual_wire_surface_device_intersections"]
        if hit["device"].startswith("CAB_09_PowerSupply_")]
if not hits or result["pass"]:
    raise RuntimeError("Negative control did not detect the rejected PSU bends")
print("PSU_NEGATIVE_CONTROL_DETECTED", len(hits), flush=True)
