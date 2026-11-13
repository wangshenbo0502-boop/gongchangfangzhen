"""Focused actual-surface door sweep against fixed enclosure sheets and returns."""
import json
from pathlib import Path

import bpy
from mathutils.bvhtree import BVHTree

MODEL = Path(__file__).resolve().parents[1]
scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)


def tree(obj):
    return BVHTree.FromPolygons([obj.matrix_world@v.co for v in obj.data.vertices],
                               [p.vertices[:] for p in obj.data.polygons])


moving = [o for o in scene.objects if o.type == "MESH"
          and o.parent and o.parent.name in ("CAB_03_Door", "CAB_15_OperatorHMI")
          and any(key in o.name for key in ("1p5mmFoldedPanel", "FoldedReturn",
                                            "HMIRearHousing", "ConsoleFascia",
                                            "InnerCableTrunk"))]
fixed = [o for o in scene.objects if o.type == "MESH"
         and o.parent and o.parent.name in ("CAB_00_Enclosure", "CAB_08_ControlPanel")
         and any(key in o.name for key in ("SidePanel", "TopPanel", "RearPanel",
                                           "BottomPanel", "FoldedUpright",
                                           "FrontReturn", "FixedServiceStrip"))]
fixed_trees = {o: tree(o) for o in fixed}
hits = []
for frame in range(1, 32):
    scene.frame_set(frame)
    for moving_obj in moving:
        a = tree(moving_obj)
        for fixed_obj, b in fixed_trees.items():
            count = len(a.overlap(b))
            if count:
                hits.append({"frame": frame, "moving": moving_obj.name,
                             "fixed": fixed_obj.name, "triangle_pairs": count})
scene.frame_set(1)
report = {"method": "31 actual-animation positions, triangle-surface BVH intersections; "
          "excludes hinge knuckles, fasteners and compressible seals. Not full assembly certification.",
          "moving_meshes": len(moving), "fixed_meshes": len(fixed), "samples": 31,
          "intersections": hits, "passed": not hits}
(MODEL/"reports"/"electrical-cabinet-door-clearance.json").write_text(
    json.dumps(report, indent=2), encoding="utf-8")
print("CABINET_DOOR_CLEARANCE", json.dumps({
    "passed": report["passed"], "samples": 31, "intersections": len(hits),
    "first_hits": hits[:3],
}), flush=True)
