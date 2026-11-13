"""Check exposed revised centerline segments with swept cable radii."""
import argparse
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

MODEL = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument("--report", type=Path, default=MODEL / "reports/electrical-cabinet-workmanship-review.json")
parser.add_argument("--output", type=Path, default=MODEL / "reports/electrical-cabinet-workmanship-routing-check.json")
parser.add_argument("--devices", action="store_true")
args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
report = json.loads(args.report.read_text())


def segment_distance(p1, q1, p2, q2):
    d1, d2, delta = q1-p1, q2-p2, p1-p2
    a, e = d1.dot(d1), d2.dot(d2)
    b, c, f = d1.dot(d2), d1.dot(delta), d2.dot(delta)
    denom = a*e-b*b
    s = max(0, min(1, (b*f-c*e)/denom)) if denom > 1e-18 else 0
    t = (b*s+f)/e if e > 1e-18 else 0
    if t < 0:
        t, s = 0, max(0, min(1, -c/a))
    elif t > 1:
        t, s = 1, max(0, min(1, (b-c)/a))
    return (p1+s*d1-p2-t*d2).length, p1+s*d1, p2+t*d2


def in_duct(p):
    if p.y < .158:
        return False
    for x, width, z, height in (
        (-.005, .93, 1.91, .040), (-.005, .93, 1.588, .040),
        (-.005, .93, .575, .040), (-.231, .47, 1.057, .040),
        (.273, .377, 1.217, .040), (.273, .377, .789, .035),
        (-.493, .039, 1.09, 1.52), (.060, .039, 1.09, 1.52),
        (.484, .039, 1.09, 1.52),
    ):
        if abs(p.x-x) <= width/2 and abs(p.z-z) <= height/2:
            return True
    if (report.get("revision") in ("field-routing", "duct-entry")
            and abs(p.x+.005) <= .450 and abs(p.z-.392) <= .0175):
        return True
    return False


routes = []
for route in report["routes"]:
    points = [Vector(p) for p in route["points"]]
    radius = route["radius_m"]
    lo = [min(p[i] for p in points)-radius for i in range(3)]
    hi = [max(p[i] for p in points)+radius for i in range(3)]
    routes.append((route, points, lo, hi))

failures = []
for i, (a, points_a, lo_a, hi_a) in enumerate(routes):
    for b, points_b, lo_b, hi_b in routes[i+1:]:
        if any(hi_a[axis] < lo_b[axis] or hi_b[axis] < lo_a[axis] for axis in range(3)):
            continue
        minimum = 1
        location = None
        for p1, q1 in zip(points_a, points_a[1:]):
            for p2, q2 in zip(points_b, points_b[1:]):
                distance, pa, pb = segment_distance(p1, q1, p2, q2)
                if in_duct(pa) and in_duct(pb):
                    continue
                # Cores intentionally enter the jacket under the breakout sleeve.
                if ("ShieldedFieldCable" in a["name"] and "FieldTermination" in b["name"]
                    or "ShieldedFieldCable" in b["name"] and "FieldTermination" in a["name"]):
                    if pa.z > .345 and pb.z > .345:
                        continue
                clearance = distance-a["radius_m"]-b["radius_m"]
                if clearance < minimum:
                    minimum, location = clearance, list((pa+pb)/2)
        if minimum < -.00015:
            failures.append({"a": a["name"], "b": b["name"],
                             "penetration_m": -minimum, "location_m": location})

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
geometry_failures = []
duct_vertices, duct_faces, face_owners = [], [], []
for obj in scene.objects:
    if obj.type != "MESH" or "CAB_07_Wiring_" not in obj.name:
        continue
    if not any(k in obj.name for k in ("CombFinger", "RemovableLid", "Duct_Base")):
        continue
    offset = len(duct_vertices)
    duct_vertices.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
    obj.data.calc_loop_triangles()
    for triangle in obj.data.loop_triangles:
        duct_faces.append(tuple(offset+i for i in triangle.vertices))
        face_owners.append(obj.name)
duct_bvh = BVHTree.FromPolygons(duct_vertices, duct_faces, all_triangles=True)
duct_intersections = []
device_intersections = []
device_trees = []
if args.devices:
    for obj in scene.objects:
        if obj.type != "MESH" or not obj.get("equipment_mesh"):
            continue
        if not obj.name.startswith(("CAB_12_VariableSpeedDrives_VFD", "CAB_11_Contactors_KM",
                                    "CAB_02_Terminals_X1_", "CAB_14_SafetyRelays_SR",
                                    "CAB_14_SafetyRelays_KA", "CAB_05_PLC_",
                                    "CAB_09_PowerSupply_")):
            continue
        verts = [obj.matrix_world @ v.co for v in obj.data.vertices]
        obj.data.calc_loop_triangles()
        faces = [tuple(t.vertices) for t in obj.data.loop_triangles]
        device_trees.append((obj.name, BVHTree.FromPolygons(
            verts, faces, all_triangles=True), verts, faces))
for route, points, _, _ in routes:
    obj = scene.objects[route["name"]]
    sides = 6 if route["radius_m"] >= .0023 else 5
    if len(obj.data.vertices) != len(points)*sides:
        geometry_failures.append({"object": obj.name, "reason": "ring count"})
        continue
    coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
    error = max((sum(coords[i*sides:(i+1)*sides], Vector())/sides-p).length
                for i, p in enumerate(points))
    if error > .00001:
        geometry_failures.append({"object": obj.name, "centerline_error_m": error})
    obj.data.calc_loop_triangles()
    wire_bvh = BVHTree.FromPolygons(
        coords, [tuple(t.vertices) for t in obj.data.loop_triangles], all_triangles=True)
    overlaps = wire_bvh.overlap(duct_bvh)
    if overlaps:
        duct_intersections.append({
            "wire": obj.name,
            "duct_parts": sorted({face_owners[b] for a, b in overlaps}),
        })
    if args.devices:
        for name, tree, device_vertices, device_faces in device_trees:
            overlaps = wire_bvh.overlap(tree)
            exposed = []
            for cable_triangle, device_triangle in overlaps:
                tri = obj.data.loop_triangles[cable_triangle]
                # Use actual triangle-edge intersections. A long triangle's centroid
                # can lie far outside a terminal although the crossing is at its end.
                for vertices, indices, target in (
                    (coords, tri.vertices, tree),
                    (device_vertices, device_faces[device_triangle], wire_bvh),
                ):
                    for k in range(3):
                        a, b = vertices[indices[k]], vertices[indices[(k+1) % 3]]
                        delta = b-a
                        if delta.length < 1e-9:
                            continue
                        hit = target.ray_cast(a, delta.normalized(), delta.length)
                        if hit[0] is None:
                            continue
                        point = hit[0]
                        allowance = .018 if name.endswith("_Boot") else .010
                        if name.startswith("CAB_09_PowerSupply_"):
                            allowance = .005 if "TerminalStrip" in name else 0
                        if min((point-points[0]).length, (point-points[-1]).length) > allowance:
                            exposed.append(list(point))
            if exposed:
                device_intersections.append({"wire": obj.name, "device": name,
                                             "surface_crossing_points_m": exposed[:3]})

result = {
    "file": bpy.data.filepath,
    "routes": len(routes),
    "exposed_swept_segment_overlap_pairs": failures,
    "centerlines_match_saved_meshes": not geometry_failures,
    "centerline_geometry_failures": geometry_failures,
    "actual_wire_surface_duct_intersections": duct_intersections,
    "actual_wire_surface_device_intersections": device_intersections,
    "device_surface_check_enabled": args.devices,
    "device_surface_mesh_count": len(device_trees),
    "psu_surface_mesh_count": sum(name.startswith("CAB_09_PowerSupply_")
                                  for name, *_ in device_trees),
    "pass": not failures and not geometry_failures and not duct_intersections and not device_intersections,
    "tolerance_m": .00015,
    "scope": "Centerlines checked against saved mesh ring centers. Pairwise swept sampled "
             "centerline segments, excluding duct interiors and jacket insertion. Actual "
             "triangle BVH surface intersections against cabinet duct combs, bases and lids. "
             "Optional device test uses actual triangle-edge surface crossings and excludes "
             "10 mm from wire ends for terminal insertion, 18 mm for Ethernet boots. "
             "PSU terminal strips allow 5 mm insertion; PSU casing parts allow none. "
             "No full containment, coplanar-contact or animated door-harness check.",
}
output = args.output
output.write_text(json.dumps(result, indent=2), encoding="utf-8")
print("CABINET_ROUTING_CHECK", json.dumps(result), flush=True)
