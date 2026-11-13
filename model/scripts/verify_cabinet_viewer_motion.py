"""Sample actual cabinet assembly meshes along the exported reversible motion path."""
import json
import math
from collections import defaultdict
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

MODEL = Path(__file__).resolve().parents[1]
ASSETS = MODEL / "CabinetViewerProject" / "Assets" / "ElectricalCabinet"
REPORT = MODEL / "reports" / "electrical-cabinet-viewer-motion-intersections-241.json"
manifest = json.loads((ASSETS / "parts.json").read_text(encoding="utf-8"))
scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
groups = {part["name"]: bpy.data.objects[part["name"]] for part in manifest["parts"]}
meshes = [obj for obj in scene.objects if obj.type == "MESH" and obj.get("equipment_mesh")]


def ancestors(obj):
    while obj:
        yield obj
        obj = obj.parent


def smooth(value):
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def offset_at(path, progress):
    for start, end in zip(path, path[1:]):
        if progress <= end["t"]:
            factor = (progress - start["t"]) / (end["t"] - start["t"])
            factor = smooth(factor)
            a, b = start["offset"], end["offset"]
            return Vector(tuple(a[k] + (b[k] - a[k]) * factor for k in ("x", "y", "z")))
    return Vector(tuple(path[-1]["offset"][k] for k in ("x", "y", "z")))


owned = {}
for part in manifest["parts"]:
    group = groups[part["name"]]
    owned[part["name"]] = [
        obj for obj in meshes if group in ancestors(obj)
    ]
    if not owned[part["name"]]:
        raise RuntimeError("Motion audit found an empty assembly: " + part["name"])

hinge = Vector(tuple(manifest["hinge"][key] for key in ("x", "y", "z")))
hinge_angle = math.radians(manifest["hinge"]["angleDegrees"])
parts = manifest["parts"]
baseline_pairs = set()
pair_samples = defaultdict(list)
peak_pair_counts = defaultdict(int)
sample_count = 241
forbidden_baseline_pairs = {
    ("CAB_00_Enclosure", "CAB_15_OperatorHMI"),
    ("CAB_03_Door", "CAB_15_OperatorHMI"),
}

for sample in range(sample_count):
    progress = sample / (sample_count - 1)
    trees = {}
    bounds = {}
    for part in parts:
        name = part["name"]
        rotation = Matrix.Identity(4)
        if part["doorAttached"]:
            angle = hinge_angle * smooth(progress / 0.25)
            rotation = Matrix.Rotation(angle, 4, "Z")
        translation = offset_at(part["path"], progress)
        vertices, polygons = [], []
        for obj in owned[name]:
            base = len(vertices)
            for vertex in obj.data.vertices:
                point = obj.matrix_world @ vertex.co
                if part["doorAttached"]:
                    point = hinge + rotation.to_3x3() @ (point - hinge)
                vertices.append(point + translation)
            polygons.extend(tuple(base + index for index in polygon.vertices)
                            for polygon in obj.data.polygons if len(polygon.vertices) >= 3)
        mins = tuple(min(vertex[axis] for vertex in vertices) for axis in range(3))
        maxs = tuple(max(vertex[axis] for vertex in vertices) for axis in range(3))
        bounds[name] = (mins, maxs)
        trees[name] = BVHTree.FromPolygons(vertices, polygons, all_triangles=False)

    for left_index, left in enumerate(parts):
        left_name = left["name"]
        left_min, left_max = bounds[left_name]
        for right in parts[left_index + 1:]:
            right_name = right["name"]
            right_min, right_max = bounds[right_name]
            if any(left_max[axis] < right_min[axis] - 1e-5 or
                   right_max[axis] < left_min[axis] - 1e-5 for axis in range(3)):
                continue
            pair = (left_name, right_name)
            overlap = trees[left_name].overlap(trees[right_name])
            if not overlap:
                continue
            if sample == 0:
                baseline_pairs.add(pair)
            else:
                pair_samples[pair].append(round(progress, 4))
                peak_pair_counts[pair] = max(peak_pair_counts[pair], len(overlap))

new_intersections = [
    {
        "parts": list(pair),
        "samples": values,
        "peakTrianglePairs": peak_pair_counts[pair],
    }
    for pair, values in sorted(pair_samples.items())
    if pair not in baseline_pairs
]
forbidden_found = sorted(baseline_pairs & forbidden_baseline_pairs)
report = {
    "method": (
        "241 sampled poses using actual Blender mesh surfaces, the exported SmoothStep "
        "motion keys, and BVH triangle overlap. Pairs already intersecting at the "
        "assembled baseline are reported separately and excluded from new intersections."
    ),
    "samples": sample_count,
    "assemblyCount": len(parts),
    "baselineIntersectingPairs": [list(pair) for pair in sorted(baseline_pairs)],
    "newIntersections": new_intersections,
    "forbiddenBaselinePairs": [list(pair) for pair in forbidden_found],
    "passed": not new_intersections and not forbidden_found,
}
REPORT.parent.mkdir(parents=True, exist_ok=True)
REPORT.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
print("CABINET_MOTION_INTERSECTIONS", json.dumps({
    "passed": report["passed"],
    "samples": sample_count,
    "baselinePairs": len(baseline_pairs),
    "newPairs": len(new_intersections),
    "forbiddenBaselinePairs": [list(pair) for pair in forbidden_found],
    "firstNewPairs": new_intersections[:5],
}, ensure_ascii=False), flush=True)
if new_intersections or forbidden_found:
    raise SystemExit(1)
