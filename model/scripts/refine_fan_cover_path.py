"""Synchronize the cover clearance path without rebuilding approved meshes."""
import json
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from fan_components import GROUP_NAMES, ancestors, animate, displacement, manifest
from build_fan import export_fbx


def main():
    scene = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.window.scene = scene
    scene.frame_set(1)
    groups = [bpy.data.objects[name] for name in GROUP_NAMES]
    for group in groups:
        group.animation_data_clear()
    animate(groups)
    scene.frame_set(1)
    bpy.context.view_layer.update()
    bounds = []
    for group in (groups[1], groups[8], groups[7], groups[10]):
        bounds.append([
            obj.matrix_world @ Vector(corner)
            for obj in scene.objects
            if obj.type == "MESH" and group in ancestors(obj)
            for corner in obj.bound_box
        ])
    duct_top = max(v.z for v in bounds[0])
    cover_bottom = min(v.z for v in bounds[1])
    lifted_gap = cover_bottom + displacement(8, .40)[2] - duct_top
    if lifted_gap < .12:
        raise RuntimeError("Cover must fully clear the duct before axial motion.")
    box_gap = min(v.x for v in bounds[2]) - (
        max(v.x for v in bounds[1]) + displacement(8, .22)[0])
    lid_gap = min(v.x for v in bounds[3]) + displacement(10, .28)[0] - max(
        v.x for v in bounds[2])
    if min(box_gap, lid_gap) < .12:
        raise RuntimeError("Box, cover and lid need independent extraction lanes.")
    # Keep source review scenes consistent with the runtime manifest.
    for review in bpy.data.scenes:
        if review == scene:
            continue
        for i, name in enumerate(GROUP_NAMES):
            group = next((o for o in review.objects
                          if o.type == "EMPTY" and o.name.startswith(name)), None)
            if group:
                group.location = displacement(i, 1 if review.name == "02_EXPLODED" else 0)
    report = {
        "date": datetime.now().astimezone().isoformat(),
        "motion_version": manifest(groups, [])["motionVersion"],
        "duct_top_m": duct_top,
        "cover_seated_bottom_m": cover_bottom,
        "cover_vertical_gap_before_axial_motion_m": lifted_gap,
        "cover_to_box_axial_gap_before_lift_m": box_gap,
        "lid_to_box_axial_gap_before_box_lift_m": lid_gap,
        "steps": "Unseat cover, shift away from box, lift above duct, then slide. "
                 "Extract lid and park axially before moving box body.",
        "geometry_changed": False,
    }
    (HERE.parent/"reports"/"industrial-fan-cover-lift.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE.parent/"source"/"industrial-fan.blend"))
    export_fbx(scene, bpy.data.objects["industrial-fan"])
    print("FAN_COVER_VERTICAL_CLEARANCE", lifted_gap, flush=True)


if __name__ == "__main__":
    main()
