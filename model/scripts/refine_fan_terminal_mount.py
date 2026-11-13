"""Replace only the terminal supports in the reviewed source and audit their fit."""
import json
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_fan
from fan_components import GROUP_NAMES, animate, displacement
from industrial_parts import activate
from pump_uv import unwrap

MODEL = HERE.parent
PREFIXES = tuple("FAN_07_TerminalBox_"+suffix for suffix in (
    "MountRail", "TaperedStandOff", "SaddleAnchor",
    "ContouredSaddle", "RelievedSupportWeb",
))


def tree(obj, offset=(0, 0, 0)):
    offset = Vector(offset)
    vertices = [obj.matrix_world @ v.co+offset for v in obj.data.vertices]
    return BVHTree.FromPolygons(vertices, [p.vertices[:] for p in obj.data.polygons])


def main():
    scene = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.window.scene = scene
    scene.frame_set(1)
    groups = [bpy.data.objects[name] for name in GROUP_NAMES]
    root = bpy.data.objects["industrial-fan"]
    for obj in list(bpy.data.objects):
        if obj.name.startswith(PREFIXES):
            bpy.data.objects.remove(obj, do_unlink=True)
    for key, name in (
        ("grey", "Fan / industrial grey"), ("graphite", "Fan / graphite powder coat"),
        ("zinc", "Fan / zinc plated fasteners"),
    ):
        build_fan.MATS[key] = bpy.data.materials[name]
    before = set(scene.objects)
    build_fan.make_terminal_mount(groups[7])
    created = [obj for obj in scene.objects if obj not in before]
    for obj in created:
        obj["equipment_mesh"] = True
        activate(obj)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    uv = unwrap(created)
    if not uv["pass"]:
        raise RuntimeError("New support UVs failed")
    for review in bpy.data.scenes:
        if review == scene:
            continue
        parent = next((obj for obj in review.objects
                       if obj.type == "EMPTY" and obj.name.startswith(GROUP_NAMES[7])), None)
        if parent is None:
            continue
        for obj in created:
            copy = obj.copy()
            review.collection.objects.link(copy)
            copy.parent = parent
            copy.hide_render = review.name == "07_BLADE_DETAIL"
    # Keep source review animation consistent with the already approved EXE paths.
    for group in groups:
        group.animation_data_clear()
    animate(groups)
    for review in bpy.data.scenes:
        if review == scene:
            continue
        for i, name in enumerate(GROUP_NAMES):
            group = next((obj for obj in review.objects
                          if obj.type == "EMPTY" and obj.name.startswith(name)), None)
            if group:
                group.location = displacement(i, 1 if review.name == "02_EXPLODED" else 0)
    scene.frame_set(1)
    bpy.context.view_layer.update()
    supports = [obj for obj in created if "MountRail" not in obj.name]
    lugs = [obj for obj in scene.objects if obj.name.startswith((
        "FAN_01_Duct_LiftingShoe", "FAN_01_Duct_LiftingEyePlate",
        "FAN_01_Duct_LiftingFootWeld",
    ))]
    lug_trees = [(obj, tree(obj)) for obj in lugs]
    shell = next(obj for obj in scene.objects
                 if obj.name.startswith("FAN_01_Duct_Rolled8mmShell"))
    shell_tree = tree(shell)
    interference = []
    for obj in supports:
        support_tree = tree(obj)
        for lug, lug_tree in lug_trees:
            if support_tree.overlap(lug_tree):
                interference.append([obj.name, lug.name])
        if support_tree.overlap(shell_tree):
            interference.append([obj.name, shell.name])
    moving_interference = []
    for step in range(101):
        offset = displacement(7, step/100)
        for obj in supports:
            support_tree = tree(obj, offset)
            for lug, lug_tree in lug_trees:
                if support_tree.overlap(lug_tree):
                    moving_interference.append([step/100, obj.name, lug.name])
    inner_gaps = []
    for obj in supports:
        if "ContouredSaddle" not in obj.name:
            continue
        for vertex in obj.data.vertices:
            p = obj.matrix_world @ vertex.co
            radial = Vector((0, p.y, p.z-build_fan.H))
            if abs(radial.length-.7344) < .00005:
                hit, normal, _, distance = shell_tree.find_nearest(p)
                inner_gaps.append({"distance_m": distance,
                                   "signed_distance_m": (p-hit).dot(normal)})
    minimum_x = min((obj.matrix_world @ v.co).x
                    for obj in supports for v in obj.data.vertices)
    maximum_lug_x = max((obj.matrix_world @ v.co).x
                        for obj in lugs for v in obj.data.vertices)
    result = build_fan.measure(scene, root, groups)
    result["uv_unwrap_new_supports"] = uv
    result["terminal_mount"] = {
        "new_meshes": len(created),
        "lug_axial_clearance_m": minimum_x-maximum_lug_x,
        "assembled_intersections": interference,
        "lug_intersections_at_101_extraction_samples": moving_interference,
        "saddle_shell_surface_gap_m": {
            "min": min(item["distance_m"] for item in inner_gaps),
            "max": max(item["distance_m"] for item in inner_gaps),
            "minimum_signed": min(item["signed_distance_m"] for item in inner_gaps),
            "sampled_inner_vertices": len(inner_gaps),
        },
        "scope": "Terminal supports vs shell/lifting lugs; not a full assembly collision certificate.",
    }
    result["date"] = datetime.now().astimezone().isoformat()
    report = MODEL/"reports"/"industrial-fan-terminal-mount.json"
    report.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    if (not all(result["checks"].values()) or interference or moving_interference
            or minimum_x-maximum_lug_x < .010
            or min(item["signed_distance_m"] for item in inner_gaps) <= 0):
        raise RuntimeError("Support verification failed; inspect "+str(report))
    build_fan.write_report(result)
    bpy.ops.wm.save_as_mainfile(filepath=str(MODEL/"source"/"industrial-fan.blend"))
    build_fan.export_fbx(scene, root)
    print("FAN_TERMINAL_MOUNT", json.dumps(result["terminal_mount"]), flush=True)
    print("FAN_GEOMETRY", result["triangles"], result["mesh_count"], result["checks"], flush=True)


if __name__ == "__main__":
    main()
