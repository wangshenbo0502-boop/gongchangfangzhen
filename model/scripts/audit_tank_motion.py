"""Check per-mesh surface overlap along the tank viewer's reversible paths."""
import argparse
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
from mathutils.bvhtree import BVHTree

MODEL = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--geometry", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--samples", type=int, default=1201)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])

manifest = json.loads((MODEL / "TankViewerProject/Assets/Tank/parts.json").read_text(encoding="utf-8"))
data = json.loads(args.geometry.read_text(encoding="utf-8-sig"))
def vector(item):
    return [item[k] for k in "xyz"] if isinstance(item, dict) else item

basis = np.array([vector(data["axis"+axis]) for axis in "XYZ"]) if isinstance(data, dict) else np.eye(3)
inverse = np.linalg.inv(basis)
meshes = data["meshes"] if isinstance(data, dict) else data
parts = manifest["parts"]
vertices, faces, owners = [], [], []
for i in range(len(parts)):
    points, polygons, indices = [], [], []
    offset = 0
    for mesh in (mesh for mesh in meshes if mesh["part"] == i):
        v = np.array([vector(p) for p in mesh["vertices"]]) @ inverse
        f = np.array(mesh["triangles"]).reshape(-1, 3)
        points.append(v)
        polygons.extend((f + offset).tolist())
        indices.extend([mesh["name"]] * len(f))
        offset += len(v)
    vertices.append(np.concatenate(points))
    faces.append(polygons)
    owners.append(indices)


def position(part, progress):
    path = part["path"]
    for a, b in zip(path, path[1:]):
        if progress <= b["time"]:
            u = np.clip((progress-a["time"])/(b["time"]-a["time"]), 0, 1)
            u = u*u*u*(10+u*(-15+6*u))
            av = np.array(vector(a["offset"]))
            return av + u*(np.array(vector(b["offset"])) - av)
    return np.array(vector(path[-1]["offset"]))


def pose(index, offset):
    points = vertices[index] + offset
    return (tuple(offset), points.min(axis=0), points.max(axis=0),
            BVHTree.FromPolygons(points.tolist(), faces[index], all_triangles=True))


def contact(i, j, left, right):
    _, amin, amax, a = left
    _, bmin, bmax, b = right
    if np.any(amax < bmin) or np.any(bmax < amin):
        return Counter()
    return Counter((owners[i][p], owners[j][q]) for p, q in a.overlap(b))


if __name__ == "__main__":
    cache = [pose(i, np.zeros(3)) for i in range(len(parts))]
    pairs = [(i, j) for i in range(len(parts)) for j in range(i+1, len(parts))]
    initial = {(i, j): contact(i, j, cache[i], cache[j]) for i, j in pairs}
    active = {pair: set(items) for pair, items in initial.items()}
    new = defaultdict(list)
    larger = {}
    samples = sorted(set(np.linspace(0, 1, args.samples)) |
                     {k["time"] for part in parts for k in part["path"]})
    for progress in samples[1:]:
        for i, part in enumerate(parts):
            offset = position(part, progress)
            if tuple(offset) != cache[i][0]:
                cache[i] = pose(i, offset)
        for i, j in pairs:
            if cache[i][0] == cache[j][0]:
                continue
            key = (i, j)
            items = contact(i, j, cache[i], cache[j])
            for meshes, count in items.items():
                item = key + meshes
                if meshes not in active[key]:
                    new[item].append(progress)
                baseline = initial[key][meshes]
                if baseline and count > max(baseline, larger.get(item, (0, 0))[0]):
                    larger[item] = (count, progress)
            active[key].intersection_update(items)

    def describe(key):
        i, j, a, b = key
        return {"parts": [parts[i]["name"], parts[j]["name"]], "meshes": [a, b]}

    report = {
        "method": "Per-mesh triangle BVH overlap; uninterrupted seated contacts reported separately",
        "motionVersion": manifest["motionVersion"],
        "samples": len(samples),
        "initialContacts": sum(map(len, initial.values())),
        "newContacts": [dict(describe(k), first=round(ts[0], 6), last=round(ts[-1], 6), poses=len(ts))
                        for k, ts in sorted(new.items())],
        "growingInitialContacts": [dict(describe(k), baseline=initial[k[:2]][k[2:]],
                                       peak=v[0], progress=round(v[1], 6)) for k, v in sorted(larger.items())],
        "passed": not new,
    }
    args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("TANK_MOTION_AUDIT", len(samples), report["initialContacts"], len(new), len(larger), flush=True)
    if new:
        raise SystemExit(1)
