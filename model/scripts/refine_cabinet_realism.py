"""Create a separate realism-review candidate with restrained finishes and HMI glass."""
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy

HERE = Path(__file__).resolve().parent
MODEL = HERE.parent
sys.path.insert(0, str(HERE))
import refine_cabinet_materials as finish
from pump_surface_finish import render_device
from pump_uv import planar_face, unwrap

OUTPUT = MODEL / "source/electrical-cabinet-realism-review.blend"


def hmi_glass_material():
    material = bpy.data.materials.get("CAB / HMI cover glass")
    if material:
        return material
    material = bpy.data.materials.new("CAB / HMI cover glass")
    material.diffuse_color = (.80, .88, .90, .24)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    glass = nodes.new("ShaderNodeBsdfPrincipled")
    glass.inputs["Base Color"].default_value = (.82, .90, .92, 1)
    glass.inputs["Metallic"].default_value = 0
    glass.inputs["Roughness"].default_value = .075
    glass.inputs["IOR"].default_value = 1.48
    glass.inputs["Transmission Weight"].default_value = .93
    glass.inputs["Coat Weight"].default_value = .12
    glass.inputs["Coat Roughness"].default_value = .09
    material.node_tree.links.new(glass.outputs["BSDF"], output.inputs["Surface"])
    return material


def add_hmi_glass():
    material = hmi_glass_material()
    for old in [o for o in bpy.data.objects
                if o.name == "CAB_15_OperatorHMI_CoverGlass"]:
        mesh = old.data
        bpy.data.objects.remove(old, do_unlink=True)
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    created = 0
    for scene in bpy.data.scenes:
        bpy.context.window.scene = scene
        scene.frame_set(1)
        panel = scene.objects.get("CAB_15_OperatorHMI_HMIDisplay")
        group = scene.objects.get("CAB_15_OperatorHMI")
        if not panel or not group:
            continue
        verts = []
        for y in (-.3115, -.3101):
            verts.extend(((-.460, y, 1.378), (.114, y, 1.378),
                          (.114, y, 1.702), (-.460, y, 1.702)))
        faces = [(0, 1, 2, 3), (7, 6, 5, 4),
                 (4, 5, 1, 0), (5, 6, 2, 1),
                 (6, 7, 3, 2), (7, 4, 0, 3)]
        mesh = bpy.data.meshes.new("CAB_HMI_GlassPane_Mesh")
        mesh.from_pydata(verts, [], faces)
        mesh.materials.append(material)
        mesh.update()
        mesh.uv_layers.new(name="FinishUV")
        mesh.uv_layers.active = mesh.uv_layers["FinishUV"]
        for polygon in mesh.polygons:
            planar_face(mesh, polygon)
        obj = bpy.data.objects.new("CAB_15_OperatorHMI_CoverGlass", mesh)
        scene.collection.objects.link(obj)
        obj.parent = group
        obj.matrix_parent_inverse = group.matrix_world.inverted()
        obj["equipment_mesh"] = True
        obj["glass_thickness_m"] = .0014
        bevel = obj.modifiers.new("Polished glass edge", "BEVEL")
        bevel.width = .00035
        bevel.segments = 2
        bevel.limit_method = "ANGLE"
        unwrap([obj])
        created += 1
    return created


def physical_uv(mesh):
    layer = mesh.uv_layers.get("FinishUV") or mesh.uv_layers.new(name="FinishUV")
    mesh.uv_layers.active = layer
    for polygon in mesh.polygons:
        planar_face(mesh, polygon)


def smooth_wire_shading():
    changed = 0
    seen = set()
    for scene in bpy.data.scenes:
        for obj in scene.objects:
            if obj.type != "MESH" or obj.data.as_pointer() in seen:
                continue
            if not (obj.name.startswith("CAB_07_Wiring_") or
                    obj.name.startswith("CAB_15_OperatorHMI_") and
                    any(key in obj.name for key in ("Loom", "Lead"))):
                continue
            seen.add(obj.data.as_pointer())
            for polygon in obj.data.polygons:
                polygon.use_smooth = True
            changed += 1
    return changed


def repair_hmi_loom_uv():
    obj = bpy.data.scenes["01_ASSEMBLED"].objects.get(
        "CAB_15_OperatorHMI_HMIPowerLoom")
    if not obj or obj.type != "MESH":
        return False
    physical_uv(obj.data)
    return True


def repair_hmi_saddle_uv():
    scene = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.window.scene = scene
    obj = scene.objects.get("CAB_15_OperatorHMI_EthernetLeadSaddle")
    if not obj:
        return False
    unwrap([obj])
    return True


def reduce_gland_segments():
    changed = 0
    for scene in bpy.data.scenes:
        bpy.context.window.scene = scene
        for obj in scene.objects:
            if obj.type != "MESH" or "CableGland" not in obj.name:
                continue
            obj.data = obj.data.copy()
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            decimate = obj.modifiers.new("Reduce excess gland tessellation", "DECIMATE")
            decimate.ratio = .5
            bpy.ops.object.modifier_apply(modifier=decimate.name)
            obj.select_set(False)
            physical_uv(obj.data)
            changed += 1
    return changed


def render_review():
    render_device()
    previews = MODEL / "previews/electrical-cabinet"
    for scene_name, suffix in (
        ("01_ASSEMBLED", "assembled"),
        ("02_DOOR_OPEN", "door-open"),
        ("04_INTERNAL", "internal-detail"),
        ("06_FRONT", "front"),
    ):
        scene = bpy.data.scenes[scene_name]
        bpy.context.window.scene = scene
        scene.render.resolution_percentage = 50
        scene.cycles.samples = 32
        scene.render.filepath = str(previews / f"realism-review-draft-{suffix}.png")
        bpy.ops.render.render(write_still=True)
        print("CABINET_REALISM_PREVIEW", scene.render.filepath, flush=True)


def main():
    finish.apply()
    glass_count = add_hmi_glass()
    smooth_count = smooth_wire_shading()
    hmi_uv_repaired = repair_hmi_loom_uv()
    saddle_uv_repaired = repair_hmi_saddle_uv()
    gland_count = reduce_gland_segments()
    scene = bpy.data.scenes["01_ASSEMBLED"]
    scene["review_status"] = "Realism pass; waiting for visual approval; Unity deferred"
    report = {
        "source": bpy.data.filepath,
        "output": str(OUTPUT),
        "hmi_glass_objects_added": glass_count,
        "wire_meshes_smooth_shaded": smooth_count,
        "hmi_power_loom_uv_repaired": hmi_uv_repaired,
        "hmi_saddle_uv_repaired": saddle_uv_repaired,
        "cable_gland_meshes_reduced": gland_count,
        "scope": ["lower-contrast galvanized finish", "reduced micro-normal",
                  "physically layered HMI cover glass", "smooth cable shading"],
    }
    (MODEL / "reports/electrical-cabinet-realism-review.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT), check_existing=False)
    print("CABINET_REALISM_SAVED", json.dumps(report, ensure_ascii=False), flush=True)
    if "--no-render" not in sys.argv:
        render_review()


if __name__ == "__main__":
    main()
