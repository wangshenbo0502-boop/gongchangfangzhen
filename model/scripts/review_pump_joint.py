"""Inspect the outlet's saved topology and render both sides of its root."""
import json
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from pump_surface_finish import render_device

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
body = bpy.data.objects["PMP_04_Volute_GrowingSpiral12mm"]
bm = bmesh.new()
bm.from_mesh(body.data)
unseen = set(bm.verts)
shell_sizes = []
while unseen:
    queue = [unseen.pop()]
    size = 0
    while queue:
        vertex = queue.pop()
        size += 1
        for edge in vertex.link_edges:
            other = edge.other_vert(vertex)
            if other in unseen:
                unseen.remove(other)
                queue.append(other)
    shell_sizes.append(size)
report = {
    "source": bpy.data.filepath,
    "object": body.name,
    "connected_shell_vertex_counts": sorted(shell_sizes, reverse=True),
    "boundary_edges": sum(e.is_boundary for e in bm.edges),
    "non_manifold_edges": sum(not e.is_manifold for e in bm.edges),
    "scope": "Joined volute and outlet mesh topology, plus front/back visual review; not a pressure test.",
}
report["topology_pass"] = len(shell_sizes) == 1 and report["non_manifold_edges"] == 0
bm.free()
(HERE.parent/"reports/water-pump-outlet-joint.json").write_text(
    json.dumps(report, indent=2), encoding="utf-8")
print("PUMP_JOINT_TOPOLOGY", json.dumps(report), flush=True)
if not report["topology_pass"]:
    raise RuntimeError("Outlet joint contains disconnected or open geometry")

scene.render.resolution_x, scene.render.resolution_y = 1280, 960
scene.render.resolution_percentage = 100
scene.cycles.samples = 48
render_device()
camera = scene.camera
camera.animation_data_clear()
camera.data.animation_data_clear()
for name, position, target, scale in (
    ("source-12-assembled", (-3.5, -4.4, 2.6), (0, 0, .64), 2.75),
    ("outlet-joint-front", (-2, -2.8, 1.9), (-.518, .20, .86), .70),
    ("outlet-joint-back", (-2, 2.8, 1.7), (-.518, .27, .82), .65),
):
    camera.location = position
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = scale
    scene.render.filepath = str(HERE.parent/"previews/water-pump"/(name+".png"))
    bpy.ops.render.render(write_still=True)
print("PUMP_JOINT_REVIEW_RENDERED", flush=True)
