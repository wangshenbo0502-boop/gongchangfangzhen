"""Reopen verification of source geometry, UVs, animation and review operators."""
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
from pump_uv import triangle_metrics
from pump_components import GROUP_NAMES, OFFSETS, manifest, displacement

MODEL = HERE.parent
scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
meshes = [o for o in scene.objects if o.get("equipment_mesh")]
groups = sorted(bpy.data.objects["water-pump"].children, key=lambda o: o.name)
home = {o.name: o.matrix_world.copy() for o in meshes}
initial = [o.location.copy() for o in groups]
motion_error, reverse_error = 0, 0
for frame in range(1, 151):
    scene.frame_set(frame)
    forward = [g.location.copy() for g in groups]
    for index, (group, base) in enumerate(zip(groups, initial)):
        motion_error = max(motion_error, (group.location-base-
            Vector(displacement(index, (frame-1)/149))).length)
    scene.frame_set(321-frame)
    reverse_error = max(reverse_error,max((g.location-p).length for g,p in zip(groups,forward)))
scene.frame_set(150)
offsets = [list(g.location-p) for g, p in zip(groups, initial)]
expected = OFFSETS
explosion_pass = all((Vector(actual)-Vector(target)).length < 1e-5
                     for actual, target in zip(offsets, expected))
scene.frame_set(320)
return_error = max((o.matrix_world.translation-home[o.name].translation).length for o in meshes)
scene.frame_set(1)

points, triangles, bad_edges, negative, uv_bad = [], 0, [], [], []
max_stretch, max_density_ratio, excluded = 0, 0, 0
for obj in meshes:
    data = obj.data
    points.extend(obj.matrix_world @ v.co for v in data.vertices)
    data.calc_loop_triangles()
    triangles += len(data.loop_triangles)
    bm = bmesh.new()
    bm.from_mesh(data)
    if any(not e.is_manifold for e in bm.edges):
        bad_edges.append(obj.name)
    if bm.calc_volume(signed=True) <= 0:
        negative.append(obj.name)
    bm.free()
    metrics, tiny = triangle_metrics(data)
    excluded += tiny
    stretch = max((s for _, s, _ in metrics), default=float("inf"))
    densities = sorted(d for _, _, d in metrics if d > 1e-20)
    median = densities[len(densities)//2] if densities else 0
    density_ratio = max((max(d/median, median/d) for _, _, d in metrics if d > 1e-20),
                        default=float("inf"))
    max_stretch = max(max_stretch, stretch)
    max_density_ratio = max(max_density_ratio, density_ratio)
    if stretch > 1.25 or density_ratio > 1.25:
        uv_bad.append(obj.name)
low = [min(p[i] for p in points) for i in range(3)]
high = [max(p[i] for p in points) for i in range(3)]
images = [i for i in bpy.data.images if i.source == "FILE"]

import pump_review_ui
operator_results = []
for index in range(3):
    result = bpy.ops.pump.review_state(index=index)
    part_results = [list(bpy.ops.pump.review_part(index=i)) for i in range(len(groups))]
    operator_results.append({"state": index, "result": list(result), "components": part_results})
bpy.ops.pump.review_state(index=0)
scene.pump_review_progress = 1
slider_full = scene.frame_current == 150
scene.pump_review_progress = 0
slider_home = scene.frame_current == 1

report = {
    "date": datetime.now().astimezone().isoformat(),
    "blender": bpy.app.version_string, "reopened_file": bpy.data.filepath,
    "scene_names": [s.name for s in bpy.data.scenes],
    "mesh_count": len(meshes), "triangles": triangles,
    "components": manifest(groups, meshes),
    "width_depth_height_m": [b-a for a, b in zip(low, high)],
    "non_manifold_objects": bad_edges, "non_positive_volume_objects": negative,
    "uv_failed_objects": uv_bad,
    "uv_max_anisotropy": max_stretch if math.isfinite(max_stretch) else None,
    "uv_max_density_area_ratio": max_density_ratio if math.isfinite(max_density_ratio) else None,
    "uv_sub_1e_14_m2_triangles_excluded": excluded,
    "packed_file_images": sum(bool(i.packed_file) for i in images),
    "total_file_images": len(images), "animation_offsets_at_150": offsets,
    "animation_exploded_pass": explosion_pass,
    "animation_max_home_error_m": return_error,
    "animation_shared_path_error_m": motion_error,
    "animation_reverse_path_error_m": reverse_error,
    "review_operators": operator_results,
    "review_slider_home_pass": slider_home, "review_slider_exploded_pass": slider_full,
}
report["source_checks_pass"] = (
    tuple(g.name for g in groups) == GROUP_NAMES and triangles <= 120000 and not bad_edges and not negative
    and not uv_bad and explosion_pass and return_error < 1e-5
    and motion_error < 1e-5 and reverse_error < 1e-5
    and slider_home and slider_full and all(i.packed_file for i in images)
    and all("FINISHED" in r["result"] and all("FINISHED" in c for c in r["components"])
            for r in operator_results)
)
(MODEL/"reports"/"water-pump-reopen-verification.json").write_text(
    json.dumps(report, indent=2, ensure_ascii=False, allow_nan=False), encoding="utf-8")
import build_pump_detailed as builder
builder.root = bpy.data.objects["water-pump"]
builder.groups = groups
builder.uv_result = {
    "method": "Reopened mesh loop-triangle measurements; no UV edits during verification",
    "objects_checked": len(meshes), "max_anisotropy": report["uv_max_anisotropy"],
    "max_area_ratio_to_object_median": report["uv_max_density_area_ratio"],
    "sub_1e_14_m2_triangles_excluded": excluded,
    "failures": uv_bad, "pass": not uv_bad,
}
builder.write_report(builder.measured(scene))
print("PUMP_REOPEN_VERIFIED", json.dumps(report, ensure_ascii=False, allow_nan=False), flush=True)
if not report["source_checks_pass"]:
    raise RuntimeError("Source verification has unresolved failures; see report.")
