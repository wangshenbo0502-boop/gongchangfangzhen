"""Independently reopen source or FBX; record measurements from real vertices."""
import argparse
import json
import math
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_fan
from fan_components import GROUP_NAMES, ancestors, displacement

MODEL = HERE.parent


def source():
    scene = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.window.scene = scene
    scene.frame_set(1)
    root = bpy.data.objects["industrial-fan"]
    groups = [bpy.data.objects[name] for name in GROUP_NAMES]
    result = build_fan.measure(scene, root, groups)
    result["reopened_file"] = bpy.data.filepath
    blades = sorted((o for o in build_fan.equipment(scene)
                     if "TwistedAirfoil" in o.name), key=lambda o: o.name)
    sections = []
    for blade in blades:
        theta = math.tau*(blade["blade_index"]-1)/8+math.radians(11)
        tangent = Vector((0, -math.sin(theta), math.cos(theta)))
        data = blade.data
        key = data.shape_keys.key_blocks["Basis"] if data.shape_keys else None
        def co(index):
            return blade.matrix_world @ (key.data[index].co if key else data.vertices[index].co)
        measured = []
        for j in (0, 22):
            offset = j*37
            chord = (co(offset+18)+co(offset+19))*.5-co(offset)
            angle = math.degrees(math.atan2(chord.x, chord.dot(tangent)))
            thickness = max((co(offset+i)-co(offset+37-i)).length for i in range(1, 19))
            measured.append((angle, thickness))
        sections.append({"object": blade.name, "root_pitch_deg": measured[0][0],
                         "tip_pitch_deg": measured[1][0],
                         "twist_deg": abs(measured[0][0]-measured[1][0]),
                         "root_thickness_m": measured[0][1], "tip_thickness_m": measured[1][1]})
    result["airfoil_measurements"] = sections
    result["airfoils_pass"] = len(blades) == 8 and all(
        15 <= s["twist_deg"] <= 25 and s["root_thickness_m"] >= .012-1e-5
        and s["tip_thickness_m"] <= .004+1e-5 for s in sections)
    radial_max = max(math.hypot(p.y, p.z-1.04) for blade in blades
                     for p in (blade.matrix_world @ v.co for v in blade.data.vertices))
    result["blade_swept_cylinder_radius_m"] = radial_max
    result["nominal_blade_to_duct_radial_clearance_m"] = .726-radial_max
    shaft = bpy.data.objects["FAN_04_Motor_OutputShaft"]
    hub = bpy.data.objects["FAN_02_Impeller_Hub"]
    def axial_bounds(obj):
        points = [obj.matrix_world @ v.co for v in obj.data.vertices]
        return min(p.x for p in points), max(p.x for p in points)
    s0, s1 = axial_bounds(shaft)
    h0, h1 = axial_bounds(hub)
    result["motor_shaft_length_m"] = s1-s0
    result["shaft_axial_engagement_with_hub_m"] = min(s1, h1)-max(s0, h0)
    animation_error, reverse_error = 0, 0
    for frame in range(1, 151):
        scene.frame_set(frame)
        current = [g.location.copy() for g in groups]
        for i, group in enumerate(groups):
            animation_error = max(animation_error,
                                  (group.location-Vector(displacement(i, (frame-1)/149))).length)
        scene.frame_set(321-frame)
        reverse_error = max(reverse_error, max((g.location-v).length for g, v in zip(groups, current)))
    scene.frame_set(320)
    home_error = max(g.location.length for g in groups)
    scene.frame_set(1)
    result["animation_shared_path_error_m"] = animation_error
    result["animation_reverse_error_m"] = reverse_error
    result["animation_home_error_m"] = home_error
    images = [image for image in bpy.data.images if image.source == "FILE"]
    result["file_images"] = len(images)
    result["packed_images"] = sum(bool(image.packed_file) for image in images)
    blade = next(o for o in blades if o.get("fault_region"))
    result["fault_geometry"] = build_fan.fault_export(blade)
    import fan_review_ui
    operator_results = []
    for i in range(7):
        operator_results.append({"view": i, "result": list(bpy.ops.fan.review_view(index=i))})
    bpy.ops.fan.review_view(index=0)
    part_results = [list(bpy.ops.fan.review_component(index=i)) for i in range(len(GROUP_NAMES))]
    scene.fan_review_progress = 1
    slider_end = scene.frame_current == 150
    scene.fan_review_progress = 0
    slider_start = scene.frame_current == 1
    result["review_operators"] = operator_results
    result["component_operators"] = part_results
    result["progress_slider_pass"] = slider_end and slider_start
    result["source_checks_pass"] = (
        all(result["checks"].values()) and result["airfoils_pass"]
        and result["nominal_blade_to_duct_radial_clearance_m"] > .010
        and result["shaft_axial_engagement_with_hub_m"] > .060
        and animation_error < 1e-5 and reverse_error < 1e-5 and home_error < 1e-5
        and all(image.packed_file for image in images)
        and all("FINISHED" in entry["result"] for entry in operator_results)
        and all("FINISHED" in entry for entry in part_results)
        and slider_end and slider_start)
    return result


def fbx():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = MODEL/"fbx"/"industrial-fan.fbx"
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    root = bpy.data.objects.get("industrial-fan")
    rotor = bpy.data.objects.get("industrial-fan_rotor")
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.context.view_layer.update()
    points = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    low = [min(v[i] for v in points) for i in range(3)]
    high = [max(v[i] for v in points) for i in range(3)]
    size = [b-a for a, b in zip(low, high)]
    axis = rotor.matrix_world.to_3x3() @ Vector((0, 0, 1)) if rotor else Vector((0, 0, 0))
    shape_objects = [o.name for o in meshes if o.data.shape_keys]
    result = {
        "date": datetime.now().astimezone().isoformat(),
        "file": str(path), "mesh_count": len(meshes), "width_depth_height_m": size,
        "top_level_components": sorted(g.name for g in root.children) if root else [],
        "local_Z_axis_in_roundtrip_blender": list(axis),
        "rotor_axis_pass": abs(axis.x) > .999 and abs(axis.y) < 1e-4 and abs(axis.z) < 1e-4,
        "rotor_center": list(rotor.matrix_world.translation) if rotor else None,
        "shape_key_objects": shape_objects,
        "cameras": sum(o.type == "CAMERA" for o in bpy.context.scene.objects),
        "lights": sum(o.type == "LIGHT" for o in bpy.context.scene.objects),
        "animation_actions": len(bpy.data.actions),
    }
    result["fbx_checks_pass"] = (
        result["top_level_components"] == list(GROUP_NAMES)
        and result["rotor_axis_pass"] and len(shape_objects) == 1
        and result["cameras"] == 0 and result["lights"] == 0
        and result["animation_actions"] == 0
        and all(abs(s/t-1) < .03 for s, t in zip(size, (1.6, 1.6, 1.8))))
    return result


parser = argparse.ArgumentParser()
parser.add_argument("--fbx", action="store_true")
args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
result = fbx() if args.fbx else source()
filename = "industrial-fan-fbx-roundtrip.json" if args.fbx else "industrial-fan-reopen-verification.json"
(MODEL/"reports"/filename).write_text(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False),
                                    encoding="utf-8")
print("FAN_VERIFIED", json.dumps(result, ensure_ascii=False, allow_nan=False), flush=True)
if not result["fbx_checks_pass" if args.fbx else "source_checks_pass"]:
    raise RuntimeError("Fan verification failed; see "+filename)
