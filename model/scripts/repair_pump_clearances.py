"""Selective extraction-clearance repairs shared with the source generator."""
import json
import math
import shutil
import sys
from datetime import datetime
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from industrial_parts import boolean, box, cylinder
from pump_components import GROUP_NAMES, OFFSETS, animate_groups, manifest
from pump_surface_finish import assign_machining_coordinates
from pump_uv import unwrap


def guard_support_layout():
    return [
        ("PMP_03_Guard_MountingSill", (.08, y, .123), (.18, .032, .018))
        for y in (-.17, .17)
    ] + [
        ("PMP_03_Guard_SupportPost", (x, y, .392), (.025, .018, .520))
        for x in (.015, .145) for y in (-.17, .17)
    ]


def repair_guard_supports(scene, root, groups):
    if root.get("guard_support_revision") == 1:
        return []
    legacy = sorted(
        [o for o in scene.objects if o.type == "MESH" and
         o.name.startswith(("PMP_02_Coupling_GuardBracket", "PMP_02_Coupling_GuardStand"))],
        key=lambda o: o.name)
    changed = []
    if legacy and len(legacy) != 6:
        raise RuntimeError("Expected two guard brackets and four support posts.")
    for obj, (name, center, size) in zip(legacy, guard_support_layout()):
        previous = obj.data
        users = [o for o in bpy.data.objects if o.type == "MESH" and o.data == previous]
        replacement = box("guard_support_repair", center, size, previous.materials[0], bevel=.001)
        replacement.data.transform(obj.matrix_world.inverted() @ replacement.matrix_world)
        data = replacement.data
        bpy.data.objects.remove(replacement, do_unlink=True)
        world = obj.matrix_world.copy()
        obj.parent = groups[3]
        obj.matrix_parent_inverse.identity()
        obj.matrix_basis = groups[3].matrix_world.inverted() @ world
        for user in users:
            user.data = data
            if user != obj:
                owner_scene = next(s for s in user.users_scene if user in s.objects.values())
                user.parent = next(o for o in owner_scene.objects if
                                   o.type == "EMPTY" and o.name.startswith(GROUP_NAMES[3]))
                user.matrix_parent_inverse = obj.matrix_parent_inverse.copy()
                user.matrix_basis = obj.matrix_basis.copy()
            user.name = name
        changed.append(obj)
    root["guard_support_revision"] = 1
    bpy.context.view_layer.update()
    return changed


def apply_clearances(scene, root, groups, generated=False):
    changed = []

    def cut(obj, center, radius, length, axis=(1, 0, 0), segments=48):
        previous = obj.data
        users = [o for o in bpy.data.objects if o.type == "MESH" and o.data == previous]
        obj.data = previous.copy()
        cutter = cylinder("clearance_tool", center, radius, length, None,
                          axis=axis, segments=segments, bevel=0)
        boolean(obj, cutter)
        for other in users:
            other.data = obj.data
        if obj not in changed:
            changed.append(obj)

    if not root.get("clearance_revision"):
        body = bpy.data.objects["PMP_04_Volute_GrowingSpiral12mm"]
        cut(body, (-.75, 0, .66), .252, .612, segments=112)
        register = bpy.data.objects["PMP_04_Volute_FrontRegister"]
        cut(register, (-.643, 0, .66), .252, .020, segments=112)
        for i in range(12):
            angle = math.tau*(i+.5)/12
            center = (-.64, .261*math.cos(angle), .66+.261*math.sin(angle))
            for obj in (body, register):
                cut(obj, center, .0062, .13, segments=12)
        for obj in groups[6].children_recursive:
            if "PedestalWeb" in obj.name:
                cut(obj, (-.168, 0, .66), .1145, .24, segments=64)
        for x in (.32, .70):
            for y in (-.19, .19):
                for obj in list(scene.objects):
                    if obj.type != "MESH" or not (
                        obj.name.startswith("PMP_01_Motor_Foot") and "Bolt" not in obj.name or
                        "PMP_01_Motor_AlignmentShim" in obj.name or
                        obj.name.startswith("PMP_00_Base_Stiffener")
                    ):
                        continue
                    points = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
                    if (min(p.x for p in points)-.01 <= x <= max(p.x for p in points)+.01 and
                        min(p.y for p in points)-.01 <= y <= max(p.y for p in points)+.01):
                        cut(obj, (x, y, .23), .008, .14, axis=(0, 0, 1), segments=16)
        if not generated:
            shaft = bpy.data.objects["PMP_02_Coupling_PumpShaft"]
            for vertex in shaft.data.vertices:
                world = shaft.matrix_world @ vertex.co
                world.x = -.409+(world.x+.52)*(.489/.60)
                vertex.co = shaft.matrix_world.inverted() @ world
            changed.append(shaft)
            for obj in groups[3].children_recursive:
                if obj.type != "MESH":
                    continue
                for vertex in obj.data.vertices:
                    world = obj.matrix_world @ vertex.co
                    world.x = -.06+(world.x+.06)*(.25/.29)
                    vertex.co = obj.matrix_world.inverted() @ world
                changed.append(obj)
        root["clearance_revision"] = 1

    if root.get("clearance_revision") != 2:
        if not generated:
            from build_pump_detailed import guard_sheet
            sheet = bpy.data.objects["PMP_03_Guard_Perforated1mmSheet"]
            previous = sheet.data
            users = [o for o in bpy.data.objects if o.type == "MESH" and o.data == previous]
            replacement = guard_sheet(groups[3], sheet.data.materials[0])
            replacement.data.transform(sheet.matrix_world.inverted() @ replacement.matrix_world)
            for obj in users:
                obj.data = replacement.data
            bpy.data.objects.remove(replacement, do_unlink=True)
            changed.append(sheet)
        # Keep the seating top fixed while removing overlap below the rail top.
        for obj in scene.objects:
            if obj.type != "MESH":
                continue
            if ("AlignmentShim" in obj.name or "SealBearing_BaseShim" in obj.name or
                obj.name == "PMP_04_Volute_CastFoot"):
                points = [obj.matrix_world @ v.co for v in obj.data.vertices]
                bottom, top = min(p.z for p in points), max(p.z for p in points)
                for vertex in obj.data.vertices:
                    p = obj.matrix_world @ vertex.co
                    p.z = top-(top-p.z)*(top-.200)/(top-bottom)
                    vertex.co = obj.matrix_world.inverted() @ p
                changed.append(obj)
            if not generated and "HexNut" in obj.name and (
                "Motor_FootBolt" in obj.name or "InletCover_Bolt" in obj.name):
                points = [obj.matrix_world @ v.co for v in obj.data.vertices]
                center = (Vector([min(p[k] for p in points) for k in range(3)])+
                          Vector([max(p[k] for p in points) for k in range(3)]))*.5
                axis = Vector((0, 0, 1) if "Motor" in obj.name else (1, 0, 0))
                radius = .012 if "Motor" in obj.name else .010
                for vertex in obj.data.vertices:
                    p = obj.matrix_world @ vertex.co
                    radial = p-center-axis*(p-center).dot(axis)
                    if radial.length < radius*.80:
                        p += radial*.25
                    vertex.co = obj.matrix_world.inverted() @ p
                changed.append(obj)
        root["clearance_revision"] = 2

    changed.extend(repair_guard_supports(scene, root, groups))
    for obj in set(changed):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=list(bm.edges))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
        assign_machining_coordinates(obj)
    if changed:
        uv = unwrap(list(set(changed)))
        if not uv["pass"]:
            raise RuntimeError(json.dumps(uv))
    return changed


if __name__ == "__main__":
    scene = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.window.scene = scene
    scene.frame_set(1)
    source = Path(bpy.data.filepath)
    shutil.copy2(source, source.with_name("water-pump-before-clearance-" +
                 datetime.now().strftime("%Y%m%d-%H%M%S")+".blend"))
    root = bpy.data.objects["water-pump"]
    groups = [bpy.data.objects[n] for n in GROUP_NAMES]
    changed = apply_clearances(scene, root, groups)
    initial = [g.location.copy() for g in groups]
    animate_groups(scene, groups)
    for other in (bpy.data.scenes["02_EXPLODED"], bpy.data.scenes["03_INTERNAL"]):
        for index, name in enumerate(GROUP_NAMES):
            group = next(o for o in other.objects if o.type == "EMPTY" and o.name.startswith(name))
            group.animation_data_clear()
            group.location = initial[index]+Vector(OFFSETS[index])
    data = manifest(groups, [o for o in scene.objects if o.get("equipment_mesh")])
    root["component_contract"] = json.dumps(data, ensure_ascii=False)
    (HERE.parent/"reports/water-pump-components.json").write_text(
        json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    print("PUMP_CLEARANCE_REPAIRED", len(changed), flush=True)
