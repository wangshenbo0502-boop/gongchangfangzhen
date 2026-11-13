"""Render review angles from the saved source; keep the generator reproducible."""
import argparse
import json
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_fan


def refine_drive():
    processed = set()
    for scene in bpy.data.scenes:
        for obj in scene.objects:
            if obj.type != "MESH":
                continue
            if obj.name.startswith("FAN_04_Motor_OutputShaft"):
                if obj.data not in processed:
                    extent = max(v.co.x for v in obj.data.vertices)-min(v.co.x for v in obj.data.vertices)
                    for v in obj.data.vertices:
                        v.co.x *= .405/extent
                    obj.data.update()
                    processed.add(obj.data)
                obj.location.x = -.04
            if obj.name.startswith("FAN_04_Motor_ShaftKey"):
                obj.location.x = -.202


def fit_camera(scene, margin=.085):
    bpy.context.window.scene = scene
    scene.frame_set(1)
    bpy.context.view_layer.update()
    cam = scene.camera
    inverse = cam.matrix_world.inverted()
    points = [inverse @ (obj.matrix_world @ vertex.co)
              for obj in build_fan.equipment(scene) if not obj.hide_render
              for vertex in obj.data.vertices]
    low = [min(p[i] for p in points) for i in (0, 1)]
    high = [max(p[i] for p in points) for i in (0, 1)]
    aspect = scene.render.resolution_x/scene.render.resolution_y
    width, height = high[0]-low[0], high[1]-low[1]
    scale = max(width, height*aspect)/(1-2*margin)
    cam.location += cam.matrix_world.to_quaternion() @ Vector(
        ((low[0]+high[0])/2, (low[1]+high[1])/2, 0))
    cam.data.ortho_scale = scale
    bpy.context.view_layer.update()
    return {"horizontal_span_m": width, "vertical_span_m": height,
            "ortho_scale": scale, "minimum_frame_margin": margin}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--refine-drive", action="store_true")
    parser.add_argument("--extra", action="store_true")
    parser.add_argument("--export-only", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if args.export_only:
        build_fan.export_fbx(bpy.data.scenes["01_ASSEMBLED"],
                             bpy.data.objects["industrial-fan"])
        return
    if args.refine_drive:
        refine_drive()
    scene = bpy.data.scenes["01_ASSEMBLED"]
    views = [
        (scene, "assembled.png", (-3.7, -4.5, 2.95), (0, -.025, .90)),
        (scene, "inlet-front.png", (-5, -.08, 1.27), (0, 0, .90)),
        (scene, "motor-side.png", (3.8, -4.5, 2.70), (0, -.025, .90)),
        (scene, "elevated.png", (-3.6, -4.2, 4.55), (0, 0, .90)),
    ]
    if args.extra:
        views += [
            (bpy.data.scenes["02_EXPLODED"], "exploded.png",
             (-4.5, -6.2, 3.8), (0, 0, .90)),
            (bpy.data.scenes["03_INTERNAL"], "internal.png",
             (-3.6, -3.4, 2.7), (0, 0, .90)),
        ]
    result = {"date": datetime.now().astimezone().isoformat(), "views": []}
    for review, name, position, target in views:
        build_fan.camera(review, position, target, 4)
        framing = fit_camera(review)
        build_fan.render(review, name)
        result["views"].append({"name": name, "scene": review.name,
                                "position": list(position), **framing})
    build_fan.camera(scene, views[0][2], views[0][3], 4)
    fit_camera(scene)
    bpy.context.window.scene = scene
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(build_fan.SOURCE/"industrial-fan.blend"))
    build_fan.export_fbx(scene, bpy.data.objects["industrial-fan"])
    (build_fan.REPORTS/"industrial-fan-review-cameras.json").write_text(
        json.dumps(result, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
