"""Read-only profile of actual source triangle allocation."""
import collections
import json

import bpy

scene = bpy.data.scenes["01_ASSEMBLED"]
totals, parts = collections.Counter(), collections.Counter()
for obj in scene.objects:
    if obj.type != "MESH" or not obj.get("equipment_mesh"):
        continue
    obj.data.calc_loop_triangles()
    count = len(obj.data.loop_triangles)
    totals[obj.parent.name] += count
    parts[obj.name.split(".")[0]] += count
print("CABINET_TRIANGLE_PROFILE", json.dumps({
    "groups": totals, "parts": parts.most_common(35),
}), flush=True)
