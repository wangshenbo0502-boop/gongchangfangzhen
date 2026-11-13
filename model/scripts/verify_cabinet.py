"""Read-only reopened Blender measurements; fails on unresolved source defects."""
import argparse
import json
import math
import sys
from datetime import datetime
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from cabinet_components import GROUP_NAMES, HINGE, DOOR_ANGLE, DOOR_ATTACHED, pose
from pump_uv import triangle_metrics

MODEL = HERE.parent
scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
root = scene.objects["electrical-cabinet"]
groups = sorted(root.children, key=lambda o: o.name)
meshes = [o for o in scene.objects if o.get("equipment_mesh")]
initial = {o.name: o.matrix_world.copy() for o in meshes}
homes = [o.location.copy() for o in groups]


def owner(obj):
    while obj.parent:
        if obj.parent in groups:
            return obj.parent.name
        obj = obj.parent
    return None


points, bad_topology, non_positive, uv_failures = [], [], [], []
triangles = 0
uv_max, density_max, tiny_count = 0, 0, 0
for obj in meshes:
    data = obj.data
    points.extend(obj.matrix_world @ v.co for v in data.vertices)
    data.calc_loop_triangles()
    triangles += len(data.loop_triangles)
    bm = bmesh.new()
    bm.from_mesh(data)
    bad = sum(not e.is_manifold for e in bm.edges)
    if bad:
        bad_topology.append({"object": obj.name, "edges": bad})
    volume = bm.calc_volume(signed=True)
    if volume <= 0:
        non_positive.append({"object": obj.name, "volume_m3": volume})
    bm.free()
    metrics, tiny = triangle_metrics(data)
    tiny_count += tiny
    stretch = max((s for _, s, _ in metrics), default=float("inf"))
    values = sorted(d for _, _, d in metrics if d > 1e-20)
    median = values[len(values)//2] if values else 0
    density = max((max(d/median, median/d) for _, _, d in metrics
                   if median > 0 and d > 1e-20), default=float("inf"))
    uv_max = max(uv_max, stretch)
    density_max = max(density_max, density)
    if stretch > 1.25 or density > 1.25:
        uv_failures.append({"object": obj.name, "anisotropy": stretch,
                            "area_ratio_to_object_median": density})

lo = [min(p[i] for p in points) for i in range(3)]
hi = [max(p[i] for p in points) for i in range(3)]
dimensions = [b-a for a, b in zip(lo, hi)]
max_pose_error, max_reverse_error, door_front = 0, 0, 0
door_sweep = []
moving = [o for o in meshes if owner(o) in tuple(GROUP_NAMES[i] for i in DOOR_ATTACHED)]
for step in range(121):
    frame = 1+step
    scene.frame_set(frame)
    forward = [(g.location.copy(), g.rotation_euler.z) for g in groups]
    for i, g in enumerate(groups):
        delta, angle = pose(i, step/120)
        max_pose_error = max(max_pose_error, (g.location-homes[i]-delta).length,
                             abs(g.rotation_euler.z-angle))
    if frame <= 31:
        furthest = max(-(o.matrix_world @ v.co).y for o in moving for v in o.data.vertices)
        door_front = max(door_front, furthest)
        door_sweep.append({"frame": frame, "angle_deg": math.degrees(groups[3].rotation_euler.z),
                           "front_extent_m": furthest})
    scene.frame_set(282-frame)
    for g, (loc, angle) in zip(groups, forward):
        max_reverse_error = max(max_reverse_error, (g.location-loc).length,
                                abs(g.rotation_euler.z-angle))
scene.frame_set(281)
home_error = max(max(abs(o.matrix_world[i][j]-initial[o.name][i][j])
                     for i in range(4) for j in range(4)) for o in meshes)
scene.frame_set(1)

hinge_error = (groups[3].matrix_world.translation-Vector(HINGE)).length
images = [i for i in bpy.data.images if i.source == "FILE"]
checks = {
    "G1": abs(lo[2]) < .005 and abs((lo[0]+hi[0])/2) < .005 and abs((lo[1]+hi[1])/2) < .005,
    "G2": all((o.scale-Vector((1, 1, 1))).length < 1e-5 for o in [root]+groups+meshes),
    "G3": all(abs(actual/target-1) <= .03 for actual, target in zip(dimensions, (1.2, .65, 2.0))),
    "G4": tuple(g.name for g in groups) == GROUP_NAMES,
    "G5": not bad_topology,
    "G6": not non_positive,
    "G7": triangles <= 110000,
    "G8": not uv_failures,
    "G9": hinge_error <= .002 and groups[3].get("hinge_axis") == "Blender Z / Unity Y",
    "G10": any("lamp" in o.name and "lens" in o.name for o in meshes),
    "G11": all(owner(o) for o in meshes),
    "G12": max(-lo[1], hi[1]) <= 1.2 and door_front <= 1.2,
}
report = {
    "date": datetime.now().astimezone().isoformat(), "file": bpy.data.filepath,
    "blender": bpy.app.version_string, "dimensions_m": dimensions,
    "bounds_min_m": lo, "bounds_max_m": hi,
    "mesh_count": len(meshes), "triangles": triangles,
    "groups": [{"name": g.name, "meshes": sum(owner(o) == g.name for o in meshes)} for g in groups],
    "checks": checks, "non_manifold": bad_topology, "non_positive": non_positive,
    "uv_failures": uv_failures,
    "uv_max_anisotropy": uv_max, "uv_max_area_ratio_to_object_median": density_max,
    "tiny_uv_triangles_excluded": tiny_count,
    "hinge_position_error_m": hinge_error, "door_front_extent_max_m": door_front,
    "door_sweep": door_sweep, "motion_max_pose_error": max_pose_error,
    "motion_max_reverse_error": max_reverse_error, "motion_home_matrix_error": home_error,
    "sheet_thickness_m": {o.name: o["sheet_thickness_m"] for o in meshes if "sheet_thickness_m" in o},
    "packed_images": sum(bool(i.packed_file) for i in images), "file_images": len(images),
    "scenes": [s.name for s in bpy.data.scenes],
    "scope": "Source geometry, units, UV, hinge sweep and native animation; no Unity business acceptance",
}
report["source_checks_pass"] = (all(checks.values()) and max_pose_error < 1e-5
                                and max_reverse_error < 1e-5 and home_error < 1e-5
                                and all(i.packed_file for i in images))
args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument("--output", type=Path)
options = parser.parse_args(args)
path = options.output.resolve() if options.output else (
    MODEL / "reports" / "electrical-cabinet-verification.json")
path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("CABINET_VERIFIED", json.dumps({
    "checks": checks, "triangles": triangles, "meshes": len(meshes),
    "uv_max": uv_max, "non_manifold": len(bad_topology), "non_positive": len(non_positive),
    "uv_failures": len(uv_failures), "door_front_extent_max_m": door_front,
    "motion_max_pose_error": max_pose_error, "source_checks_pass": report["source_checks_pass"],
}), flush=True)
if not report["source_checks_pass"]:
    raise RuntimeError(f"Cabinet source checks failed; inspect {path}")
