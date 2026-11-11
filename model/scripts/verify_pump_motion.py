"""Sample cross-component surface intersections along the shared motion path."""
import json
import sys
from collections import Counter, defaultdict
from datetime import datetime
from itertools import combinations
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from pump_components import GROUP_NAMES, displacement

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
bpy.context.view_layer.update()
groups = [bpy.data.objects[n] for n in GROUP_NAMES]
geometry = []
mesh_bounds, mesh_group = {}, {}
for group in groups:
    vertices, faces, owners = [], [], []
    for obj in group.children_recursive:
        if obj.type != "MESH":
            continue
        points = [obj.matrix_world @ v.co for v in obj.data.vertices]
        mesh_bounds[obj.name] = (
            Vector([min(p[k] for p in points) for k in range(3)]),
            Vector([max(p[k] for p in points) for k in range(3)]))
        mesh_group[obj.name] = len(geometry)
        first = len(vertices)
        vertices.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
        faces.extend(tuple(first+i for i in p.vertices) for p in obj.data.polygons)
        owners.extend([obj.name]*len(obj.data.polygons))
    low = Vector([min(p[i] for p in vertices) for i in range(3)])
    high = Vector([max(p[i] for p in vertices) for i in range(3)])
    geometry.append((vertices, faces, owners, low, high))


def intersections(progress):
    offsets = [Vector(displacement(i, progress)) for i in range(len(groups))]
    trees = {}
    pairs = Counter()
    for a, b in combinations(range(len(groups)), 2):
        va, fa, oa, la, ha = geometry[a]
        vb, fb, ob, lb, hb = geometry[b]
        da, db = offsets[a], offsets[b]
        if any(ha[k]+da[k] < lb[k]+db[k]-1e-7 or
               hb[k]+db[k] < la[k]+da[k]-1e-7 for k in range(3)):
            continue
        for index in (a, b):
            if index not in trees:
                verts, faces, _, _, _ = geometry[index]
                trees[index] = BVHTree.FromPolygons(
                    [v+offsets[index] for v in verts], faces, epsilon=1e-7)
        for pa, pb in trees[a].overlap(trees[b]):
            pairs[(oa[pa], ob[pb])] += 1
    return pairs


def seating_contact(pair, progress):
    # AABB overlap <=10 micrometres on one axis bounds any actual penetration.
    a, b = pair
    la, ha = mesh_bounds[a]
    lb, hb = mesh_bounds[b]
    da = Vector(displacement(mesh_group[a], progress))
    db = Vector(displacement(mesh_group[b], progress))
    return min(min(ha[k]+da[k], hb[k]+db[k])-
               max(la[k]+da[k], lb[k]+db[k]) for k in range(3)) <= 1e-5


baseline = intersections(0)
unresolved_baseline = {pair: count for pair, count in baseline.items()
                       if not seating_contact(pair, 0)}
new = defaultdict(lambda: {"samples": 0, "first": 1, "last": 0, "peak_face_pairs": 0})
existing = defaultdict(lambda: {"samples": 0, "last": 0, "peak_face_pairs": 0})
contacts = Counter()
for step in range(1, 301):
    progress = step/300
    hits = intersections(progress)
    for pair, count in hits.items():
        if seating_contact(pair, progress):
            contacts[pair] += 1
            continue
        row = existing[pair] if pair in unresolved_baseline else new[pair]
        row["samples"] += 1
        row["last"] = progress
        row["peak_face_pairs"] = max(row["peak_face_pairs"], count)
        if "first" in row:
            row["first"] = min(row["first"], progress)
    if step % 30 == 0:
        print("MOTION_CHECK", step, "new_pairs", len(new), flush=True)

report = {
    "date": datetime.now().astimezone().isoformat(),
    "samples": 300, "mesh_objects": sum(len(set(row[2])) for row in geometry),
    "method": "BVH polygon surface intersections, 300 forward poses; reverse uses identical poses. "
              "Baseline penetrations are not excluded. Only contacts with AABB overlap "
              "<=10 micrometres on at least one axis are classified as seating contact. "
              "This does not certify containment, tolerances or manufacturing feasibility.",
    "baseline": [{"meshes": pair, "face_pairs": count} for pair, count in baseline.items()],
    "unresolved_baseline": [{"meshes": pair, "face_pairs": count}
                            for pair, count in unresolved_baseline.items()],
    "seating_contacts": [{"meshes": pair, "samples": count} for pair, count in contacts.items()],
    "new_intersections": [{"meshes": pair, **row} for pair, row in new.items()],
    "existing_intersections_during_motion": [
        {"meshes": pair, **row} for pair, row in existing.items()],
    "no_new_surface_intersections": not new,
    "surface_checks_pass": not new and not unresolved_baseline and not existing,
}
path = HERE.parent/"reports/water-pump-motion-intersections.json"
path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
print("MOTION_CHECK_RESULT", json.dumps(
    {"baseline_pairs": len(baseline), "new_pairs": len(new), "report": str(path)}), flush=True)
