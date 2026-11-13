"""Sample actual fan surfaces along the shared translation paths."""
import argparse
import json
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from fan_components import GROUP_NAMES, ancestors, displacement


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--samples", type=int, default=151)
    parser.add_argument("--part", type=int)
    parser.add_argument("--report", default="industrial-fan-motion-audit.json")
    parser.add_argument("--unity-geometry", type=Path)
    parser.add_argument("--legacy-negative-y", action="store_true",
                        help="Reproduce the removed hardcoded runtime axis conversion")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    scene = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.window.scene = scene
    scene.frame_set(1)
    groups = [bpy.data.objects[name] for name in GROUP_NAMES]
    imported = (json.loads(args.unity_geometry.read_text(encoding="utf-8"))
                if args.unity_geometry else None)
    basis = ([Vector(tuple(imported[key][axis] for axis in ("x", "y", "z")))
              for key in ("axisX", "axisY", "axisZ")] if imported else None)
    if args.legacy_negative_y:
        if not imported:
            raise ValueError("Legacy mapping reproduction requires imported Unity geometry")
        basis = [Vector((1, 0, 0)), Vector((0, 0, -1)), Vector((0, 1, 0))]
    geometry = []
    for part, group in enumerate(groups):
        vertices, faces, owners = [], [], []
        if imported:
            for mesh in imported["meshes"]:
                if mesh["part"] != part:
                    continue
                start = len(vertices)
                vertices.extend(Vector(tuple(v[k] for k in ("x", "y", "z")))
                                for v in mesh["vertices"])
                triangles = mesh["triangles"]
                faces.extend(tuple(start + v for v in triangles[i:i+3])
                             for i in range(0, len(triangles), 3))
                owners.extend([mesh["name"]]*(len(triangles)//3))
        else:
            for obj in scene.objects:
                if obj.type != "MESH" or group not in ancestors(obj):
                    continue
                start = len(vertices)
                vertices.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
                faces.extend(tuple(start+i for i in p.vertices) for p in obj.data.polygons)
                owners.extend([obj.name]*len(obj.data.polygons))
        geometry.append((vertices, faces, owners,
                         Vector(tuple(min(v[k] for v in vertices) for k in range(3))),
                         Vector(tuple(max(v[k] for v in vertices) for k in range(3)))))
    cached = [None]*len(groups)
    last = [None]*len(groups)
    hits = {}
    for sample in range(args.samples):
        t = sample/(args.samples-1)
        offsets = [Vector(displacement(i, t)) for i in range(len(groups))]
        if basis:
            offsets = [sum((basis[k]*offset[k] for k in range(3)), Vector())
                       for offset in offsets]
        for i, (vertices, faces, _, _, _) in enumerate(geometry):
            if last[i] != offsets[i]:
                cached[i] = BVHTree.FromPolygons([v+offsets[i] for v in vertices], faces)
                last[i] = offsets[i].copy()
        for i in range(len(groups)):
            for j in range(i+1, len(groups)):
                if args.part is not None and args.part not in (i, j):
                    continue
                amin, amax = geometry[i][3]+offsets[i], geometry[i][4]+offsets[i]
                bmin, bmax = geometry[j][3]+offsets[j], geometry[j][4]+offsets[j]
                if any(amax[k] < bmin[k] or bmax[k] < amin[k] for k in range(3)):
                    continue
                pairs = {(geometry[i][2][a], geometry[j][2][b])
                         for a, b in cached[i].overlap(cached[j])}
                for pair in pairs:
                    key = "|".join(pair)
                    entry = hits.setdefault(key, {"parts": [i+1, j+1], "meshes": pair,
                                                 "samples": [], "assembled": False})
                    entry["samples"].append(round(t, 6))
                    if sample == 0:
                        entry["assembled"] = True
        if sample % 25 == 0:
            print("FAN_MOTION_SAMPLE", sample, "pairs", len(hits), flush=True)
    result = {
        "date": datetime.now().astimezone().isoformat(),
        "samples": args.samples,
        "part_filter": None if args.part is None else args.part+1,
        "geometry": str(args.unity_geometry) if imported else "Blender source",
        "motion_basis": [list(axis) for axis in basis] if basis else None,
        "legacy_mapping_reproduction": args.legacy_negative_y,
        "method": "World-space polygon BVH surface intersections; identical reverse path. "
                  "Includes seated mating/fastener contacts; no swept-volume certificate.",
        "intersections": list(hits.values()),
    }
    report = HERE.parent/"reports"/args.report
    report.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    print("FAN_MOTION_AUDIT", report, "pairs", len(hits), flush=True)


if __name__ == "__main__":
    main()
