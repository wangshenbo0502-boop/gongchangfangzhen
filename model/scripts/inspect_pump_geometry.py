"""Inspect the saved source without changing it."""
import json
import sys
from collections import defaultdict
from pathlib import Path

import bmesh
import bpy

totals = defaultdict(int)
rows = []
for obj in bpy.context.scene.objects:
    if obj.type != "MESH" or not obj.get("equipment_mesh"):
        continue
    obj.data.calc_loop_triangles()
    count = len(obj.data.loop_triangles)
    totals[obj.name[:6]] += count
    high = max((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    rows.append((count, obj.name, round(high, 5)))
print("TRIANGLE_GROUPS", json.dumps(dict(totals)))
print("LARGEST", json.dumps(sorted(rows, reverse=True)[:25]))
for name in ("PMP_04_Volute_GrowingSpiral12mm", "PMP_07_Impeller_BackShroud"):
    obj = bpy.data.objects[name]
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    print("BAD_EDGES", name, [(len(e.link_faces), tuple(e.verts[0].co),
                              tuple(e.verts[1].co)) for e in bm.edges if not e.is_manifold])
    bm.free()
sys.path.insert(0, str(Path(__file__).parent))
from pump_uv import triangle_metrics
obj = bpy.data.objects["PMP_04_Volute_GrowingSpiral12mm"]
metrics, tiny = triangle_metrics(obj.data)
bad = sorted([(stretch, p, density) for p, stretch, density in metrics if stretch > 1.25],
             reverse=True)[:12]
print("UV_BAD", bad)
for stretch, p, density in bad[:6]:
    face = obj.data.polygons[p]
    print("UV_FACE", p, "normal", tuple(face.normal), "area", face.area,
          "points", [tuple(obj.data.vertices[obj.data.loops[i].vertex_index].co) for i in face.loop_indices],
          "uv", [tuple(obj.data.uv_layers.active.data[i].uv) for i in face.loop_indices])
