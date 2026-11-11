"""Rebuild only the shared cast body, retaining other reviewed source meshes."""
import json
import shutil
import sys
from datetime import datetime
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_pump_detailed as builder
from pump_surface_finish import assign_machining_coordinates
from pump_uv import unwrap

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
root = bpy.data.objects["water-pump"]
groups = sorted(root.children, key=lambda o: o.name)
old = next(o for o in scene.objects if o.name.startswith("PMP_04_Volute_GrowingSpiral12mm"))
old_data = old.data
old.name = "REPLACED_CAST_BODY"
body = builder.volute_body(groups[4], bpy.data.materials["Sand cast / blue grey"])
assign_machining_coordinates(body)
uv = unwrap([body])
for obj in bpy.data.objects:
    if obj.type == "MESH" and obj.data == old_data:
        obj.data = body.data
old["outlet_revision"] = "curved cast neck, no external lower pipe rim"
bpy.data.objects.remove(body, do_unlink=True)
old.name = "PMP_04_Volute_GrowingSpiral12mm"
builder.root, builder.groups = root, groups
previous = json.loads((HERE.parent/"reports/water-pump-detailed.json").read_text(encoding="utf-8"))
builder.uv_result = dict(previous["uv"])
builder.uv_result["pass"] = uv["pass"]
data = builder.measured(scene)
builder.write_report(data)
print("PUMP_OUTLET_REBUILD", json.dumps({"triangles": data["triangles_evaluated"],
      "checks": data["checks"], "uv": uv}), flush=True)
if not all(data["checks"].values()):
    raise RuntimeError("Revised outlet failed source checks; source file was not overwritten")
source = Path(bpy.data.filepath)
shutil.copy2(source, source.with_name("water-pump-before-outlet-" +
             datetime.now().strftime("%Y%m%d-%H%M%S")+".blend"))
bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
print("PUMP_OUTLET_SAVED", flush=True)
