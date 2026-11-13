"""Audit the actual Unity-imported cabinet surfaces along the reversible path."""
import argparse
import json
import sys
from collections import defaultdict
from pathlib import Path

import numpy as np
from mathutils.bvhtree import BVHTree

MODEL = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--geometry", type=Path, default=MODEL / "reports/electrical-cabinet-unity-motion-geometry.json")
parser.add_argument("--manifest", type=Path, default=MODEL / "CabinetViewerProject/Assets/ElectricalCabinet/parts.json")
parser.add_argument("--output", type=Path, default=MODEL / "reports/electrical-cabinet-imported-motion.json")
parser.add_argument("--samples", type=int, default=601)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])

manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
geometry = json.loads(args.geometry.read_text(encoding="utf-8"))
if manifest["motionVersion"] != geometry["motionVersion"]:
    raise RuntimeError("Geometry and motion manifest are from different builds")


def vector(value):
    return [value[axis] for axis in ("x", "y", "z")]


def smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def offset_at(path, t):
    for a, b in zip(path, path[1:]):
        if t <= b["t"]:
            return a["xyz"] + (b["xyz"] - a["xyz"]) * smooth((t - a["t"]) / (b["t"] - a["t"]))
    return path[-1]["xyz"]


basis = np.array([vector(geometry["axis" + axis]) for axis in "XYZ"])
inverse = np.linalg.inv(basis)
hinge = np.array(vector(manifest["hinge"]))
rotation_start = manifest["hinge"].get("rotationStart", 0)
rotation_end = manifest["hinge"].get("rotationEnd", .25)
parts = manifest["parts"]
vertices = [[] for _ in parts]
triangles = [[] for _ in parts]
face_owners = [[] for _ in parts]
vertex_counts = [0 for _ in parts]
mesh_names = []
roof_parts = {}
for mesh in geometry["meshes"]:
    index = mesh["part"]
    points = np.array([vector(v) for v in mesh["vertices"]]) @ inverse
    faces = np.array(mesh["triangles"]).reshape(-1, 3)
    name = mesh["name"]
    if name == "CAB_00_Enclosure_TopPanel" or name.startswith(
            ("CAB_00_Enclosure_LiftingLug_", "CAB_00_Enclosure_LiftingSeat_")):
        roof_parts[name] = (points, faces)
    vertices[index].append(points)
    triangles[index].append(faces + vertex_counts[index])
    vertex_counts[index] += len(points)
    face_owners[index].extend([len(mesh_names)] * len(faces))
    mesh_names.append(mesh["name"])

roof_top = roof_parts["CAB_00_Enclosure_TopPanel"][0][:, 2].max()
lifting_lugs = []
for i, x in enumerate((-.47, .47), 1):
    points, faces = roof_parts[f"CAB_00_Enclosure_LiftingLug_{i}"]
    seat_points, _ = roof_parts[f"CAB_00_Enclosure_LiftingSeat_{i}"]
    clearance = points[:, 2].min() - roof_top
    seat_gap = points[:, 2].min() - seat_points[:, 2].max()
    seat_roof_gap = seat_points[:, 2].min() - roof_top
    tree = BVHTree.FromPolygons(points.tolist(), faces.tolist(), all_triangles=True)
    hole_open = tree.ray_cast((x, .175, 1.988), (0, 1, 0), .03)[0] is None
    wall_hit = tree.ray_cast((x + .010, .175, 1.988), (0, 1, 0), .03)[0] is not None
    lifting_lugs.append({
        "lug": i, "roofClearanceMm": round(float(clearance * 1000), 4),
        "seatGapMm": round(float(seat_gap * 1000), 4),
        "seatRoofGapMm": round(float(seat_roof_gap * 1000), 4),
        "throughHoleOpen": hole_open,
        "passed": bool(clearance > .0044 and abs(seat_gap) < 1e-5 and
                       abs(seat_roof_gap) < 1e-5 and hole_open and wall_hit),
    })

vertices = [np.concatenate(part) for part in vertices]
triangles = [np.concatenate(part).tolist() for part in triangles]
face_owners = [np.asarray(part) for part in face_owners]
for part in parts:
    for key in part["path"]:
        key["xyz"] = np.array(vector(key["offset"]))

# Include exact waypoints in addition to uniform sampling. A staged harness
# transfer is completely hidden between its seated and parked positions.
# Poses with the same rigid motion cannot acquire an overlap.
samples = sorted(set(np.linspace(0, 1, args.samples)) |
                 {key["t"] for part in parts for key in part["path"]})
initial = {}
previous = set()
new_contacts = defaultdict(list)
growing_contacts = {}
pose_cache = {}
door_parts = [part["index"] for part in parts if part["doorAttached"]]
door_clearance = None
if rotation_start > 0:
    door_rear = max(vertices[i][:, 1].max() + offset_at(parts[i]["path"], rotation_start)[1]
                    for i in door_parts)
    stationary_front = min(vertices[i][:, 1].min() for i in range(len(parts)) if i not in door_parts)
    door_clearance = float(stationary_front - door_rear)
for progress in samples:
    shapes = []
    hidden = set()
    for index, part in enumerate(parts):
        transfer = part.get("transfer")
        if transfer and transfer["hiddenAt"] <= progress <= transfer["visibleFrom"]:
            hidden.add(index)
        offset = offset_at(part["path"], progress)
        angle = np.radians(manifest["hinge"]["angleDegrees"]) * smooth(
            (progress - rotation_start) / (rotation_end - rotation_start)) if part["doorAttached"] else 0.
        pose = (angle, *offset)
        if index not in pose_cache or pose_cache[index][0] != pose:
            points = vertices[index]
            if angle:
                c, s = np.cos(angle), np.sin(angle)
                points = (points - hinge) @ np.array(((c, s, 0), (-s, c, 0), (0, 0, 1))) + hinge
            points = points + offset
            tree = BVHTree.FromPolygons(points.tolist(), triangles[index], all_triangles=True)
            pose_cache[index] = (pose, points.min(axis=0), points.max(axis=0), tree)
        shapes.append(pose_cache[index])
    contacts = {}
    for i, (pose, minimum, maximum, tree) in enumerate(shapes):
        if i in hidden:
            continue
        for j in range(i + 1, len(shapes)):
            if j in hidden:
                continue
            other_pose, other_min, other_max, other_tree = shapes[j]
            if progress and pose == other_pose:
                continue
            if np.any(maximum < other_min) or np.any(other_max < minimum):
                continue
            for a, b in tree.overlap(other_tree):
                key = (i, j, int(face_owners[i][a]), int(face_owners[j][b]))
                contacts[key] = contacts.get(key, 0) + 1
    if not progress:
        initial = contacts
    else:
        for key, count in contacts.items():
            i, j, _, _ = key
            was_stationary = shapes[i][0] == shapes[j][0] or key in previous
            if key not in initial or not was_stationary:
                new_contacts[key].append(round(float(progress), 6))
            if key in initial and count > initial[key] and count > growing_contacts.get(key, (0, 0))[0]:
                growing_contacts[key] = (count, round(float(progress), 6))
    previous = set(contacts) | {key for key in previous if shapes[key[0]][0] == shapes[key[1]][0]}


def describe(key):
    i, j, a, b = key
    return {"parts": [parts[i]["name"], parts[j]["name"]], "meshes": [mesh_names[a], mesh_names[b]]}


report = {
    "motionVersion": manifest["motionVersion"],
    "poses": len(samples),
    "meshes": len(mesh_names),
    "method": "Per-mesh BVH surface overlap, excluding the fully hidden harness transfer.",
    "liftingLugs": lifting_lugs,
    "doorFrontClearanceBeforeRotationMm": None if door_clearance is None else round(door_clearance * 1000, 2),
    "closedDoorFrameContacts": [dict(describe(key), trianglePairs=count)
                                for key, count in sorted(initial.items())
                                if key[0] == 0 and parts[key[1]]["doorAttached"]],
    "initialContacts": [dict(describe(key), trianglePairs=count) for key, count in sorted(initial.items())],
    "newContacts": [dict(describe(key), first=ts[0], last=ts[-1], poses=len(ts))
                    for key, ts in sorted(new_contacts.items())],
    "growingInitialContacts": [dict(describe(key), initial=initial[key], peak=count, progress=t)
                               for key, (count, t) in sorted(growing_contacts.items())],
    "passed": not new_contacts and not growing_contacts and
    (door_clearance is None or door_clearance > .004) and
    not any(key[0] == 0 and parts[key[1]]["doorAttached"] for key in initial) and
    all(lug["passed"] for lug in lifting_lugs),
}
args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
print("CABINET_IMPORTED_AUDIT", len(samples), len(initial), len(new_contacts), len(growing_contacts), flush=True)
if not report["passed"]:
    raise SystemExit(1)
