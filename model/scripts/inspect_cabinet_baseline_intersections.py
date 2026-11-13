"""List exact mesh pairs intersecting in the closed cabinet assembly."""
import json
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

MODEL = Path(__file__).resolve().parents[1]
REPORT = MODEL / "reports" / "electrical-cabinet-baseline-mesh-intersections.json"
scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
targets = {
    ("CAB_00_Enclosure", "CAB_15_OperatorHMI"),
    ("CAB_03_Door", "CAB_15_OperatorHMI"),
}
meshes = [obj for obj in scene.objects
          if obj.type == "MESH" and obj.get("equipment_mesh")]
groups = {name: bpy.data.objects[name] for pair in targets for name in pair}


def owner(obj):
    parent = obj.parent
    while parent:
        if parent.name in groups:
            return parent.name
        parent = parent.parent
    return None


def tree_for(obj):
    vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    obj.data.calc_loop_triangles()
    triangles = [tuple(triangle.vertices) for triangle in obj.data.loop_triangles]
    return BVHTree.FromPolygons(vertices, triangles, all_triangles=True)


def world_bounds(obj):
    points = [obj.matrix_world @ Vector(point) for point in obj.bound_box]
    return {
        "min": [min(point[i] for point in points) for i in range(3)],
        "max": [max(point[i] for point in points) for i in range(3)],
    }


owned = {name: [] for name in groups}
for obj in meshes:
    name = owner(obj)
    if name:
        owned[name].append(obj)

intersections = []
for left_group, right_group in sorted(targets):
    for left in owned[left_group]:
        left_tree = tree_for(left)
        left_bounds = left.bound_box
        left_min = [min((left.matrix_world @ Vector(p))[i]
                        for p in left_bounds) for i in range(3)]
        left_max = [max((left.matrix_world @ Vector(p))[i]
                        for p in left_bounds) for i in range(3)]
        for right in owned[right_group]:
            right_bounds = right.bound_box
            right_min = [min((right.matrix_world @ Vector(p))[i]
                             for p in right_bounds) for i in range(3)]
            right_max = [max((right.matrix_world @ Vector(p))[i]
                             for p in right_bounds) for i in range(3)]
            if any(left_max[i] < right_min[i] - 1e-5 or
                   right_max[i] < left_min[i] - 1e-5 for i in range(3)):
                continue
            overlap = left_tree.overlap(tree_for(right))
            if overlap:
                intersections.append({
                    "groups": [left_group, right_group],
                    "objects": [left.name, right.name],
                    "trianglePairCount": len(overlap),
                    "boundsMin": [max(left_min[i], right_min[i]) for i in range(3)],
                    "boundsMax": [min(left_max[i], right_max[i]) for i in range(3)],
                })

report = {
    "scene": scene.name,
    "frame": scene.frame_current,
    "method": "World-space triangle BVH overlap for enclosure/HMI and door/HMI mesh pairs.",
    "targetObjects": [
        {
            "name": obj.name,
            "group": owner(obj),
            "bounds": world_bounds(obj),
            "centerline": obj.get("centerline_m"),
        }
        for obj in meshes
        if obj.name in {
            "CAB_00_Enclosure_FoldedUpright",
            "CAB_03_Door_InnerCableTrunk",
            "CAB_15_OperatorHMI_HMIPowerLoom",
            "CAB_15_OperatorHMI_HorizontalDuctBase",
            "CAB_15_OperatorHMI_HorizontalDuctWall",
            "CAB_15_OperatorHMI_HorizontalDuctWall.001",
            "CAB_15_OperatorHMI_HMIRearHousing",
        }
    ],
    "intersections": intersections,
}
REPORT.parent.mkdir(parents=True, exist_ok=True)
REPORT.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
print("CABINET_BASELINE_MESH_INTERSECTIONS",
      json.dumps(report, ensure_ascii=False), flush=True)
if intersections:
    raise SystemExit(1)
