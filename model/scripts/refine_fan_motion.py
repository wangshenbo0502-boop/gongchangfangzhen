"""Update only the duct, service cover, removable cradle and rear hood."""
import json
import sys
from datetime import datetime
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_fan
from fan_components import GROUP_NAMES, ancestors, animate, displacement
from industrial_parts import activate
from pump_uv import unwrap


def main():
    scene = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.window.scene = scene
    scene.frame_set(1)
    groups = [bpy.data.objects[name] for name in GROUP_NAMES]
    root = bpy.data.objects["industrial-fan"]
    names = {
        "blue": "Fan / blue grey enamel", "grey": "Fan / industrial grey",
        "lining": "Fan / dark duct interior", "graphite": "Fan / graphite powder coat",
        "yellow": "Fan / safety yellow", "steel": "Fan / machined steel",
        "zinc": "Fan / zinc plated fasteners", "guard": "Fan / dull galvanized wire",
        "rubber": "Fan / EPDM vibration pads", "black": "Fan / label black",
    }
    for key, name in names.items():
        build_fan.MATS[key] = bpy.data.materials[name]
    changed = (1, 3, 5, 6, 8, 9)
    for review in bpy.data.scenes:
        for i in changed:
            group = next((o for o in review.objects
                          if o.type == "EMPTY" and o.name.startswith(GROUP_NAMES[i])), None)
            if group:
                for obj in list(group.children_recursive):
                    if obj.type == "MESH":
                        bpy.data.objects.remove(obj, do_unlink=True)
    before = set(scene.objects)
    build_fan.make_duct(groups[1], groups[8])
    build_fan.make_guard(groups[3], True)
    build_fan.make_guard(groups[5], False)
    build_fan.make_struts(groups[6])
    # Reuse the generator's hood, preserving the approved motor meshes.
    before_motor = set(scene.objects)
    build_fan.make_motor(groups[4], groups[9])
    for obj in list(set(scene.objects)-before_motor):
        if groups[4] in ancestors(obj):
            bpy.data.objects.remove(obj, do_unlink=True)
    created = [o for o in scene.objects if o not in before]
    for obj in created:
        obj["equipment_mesh"] = True
        activate(obj)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    uv = unwrap(created)
    if not uv["pass"]:
        raise RuntimeError("Revised mesh UVs failed")
    for review in bpy.data.scenes:
        if review == scene:
            continue
        for obj in created:
            index = next(i for i in changed if groups[i] in ancestors(obj))
            parent = next((o for o in review.objects
                           if o.type == "EMPTY" and o.name.startswith(GROUP_NAMES[index])), None)
            if parent is None:
                continue
            copy = obj.copy()
            review.collection.objects.link(copy)
            copy.parent = parent
            copy.hide_render = review.name == "07_BLADE_DETAIL"
    for group in groups:
        group.animation_data_clear()
    animate(groups)
    for review in bpy.data.scenes:
        if review == scene:
            continue
        for i, name in enumerate(GROUP_NAMES):
            group = next((o for o in review.objects
                          if o.type == "EMPTY" and o.name.startswith(name)), None)
            if group:
                group.location = displacement(i, 1 if review.name == "02_EXPLODED" else 0)
    scene.frame_set(1)
    bpy.context.view_layer.update()
    result = build_fan.measure(scene, root, groups)
    result["date"] = datetime.now().astimezone().isoformat()
    result["revised_groups"] = [GROUP_NAMES[i] for i in changed]
    result["revised_uv"] = uv
    report = HERE.parent/"reports"/"industrial-fan-motion-geometry.json"
    report.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    if not all(result["checks"].values()):
        raise RuntimeError("Revised geometry check failed: "+str(result["checks"]))
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE.parent/"source"/"industrial-fan.blend"))
    build_fan.export_fbx(scene, root)
    print("FAN_MOTION_REFINED", result["triangles"], result["mesh_count"], flush=True)


if __name__ == "__main__":
    main()
