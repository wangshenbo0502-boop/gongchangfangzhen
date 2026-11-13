"""Apply an editable physical surface library to the routing review candidate."""
import argparse
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from pump_uv import planar_face, unwrap
from pump_surface_finish import render_device

MODEL = HERE.parent
TEX = MODEL / "textures/electrical-cabinet/finish-review"
MANIFEST = json.loads((TEX / "manifest.json").read_text(encoding="utf-8"))
CHANGES = {}


def srgb(hex_color):
    values = [int(hex_color[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055)**2.4
                 for v in values)


def node(tree, kind, label, x, y):
    item = tree.nodes.new(kind)
    item.label = label
    item.location = (x, y)
    return item


def surface(name, family, color, rough_shift=0, anisotropy=0, coat=0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    links = tree.links
    out = node(tree, "ShaderNodeOutputMaterial", "Surface", 920, 80)
    bsdf = node(tree, "ShaderNodeBsdfPrincipled", "Physical finish", 620, 80)
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    linear = srgb(color)
    mat.diffuse_color = (*linear, 1)
    bsdf.inputs["Coat Weight"].default_value = coat
    bsdf.inputs["Coat Roughness"].default_value = .3
    bsdf.inputs["Anisotropic"].default_value = anisotropy
    uv = node(tree, "ShaderNodeUVMap", "Separate metre-scale finish UV", -1100, 100)
    uv.uv_map = "FinishUV"
    scale = node(tree, "ShaderNodeVectorMath", "Physical repeat in metres", -920, 100)
    scale.operation = "SCALE"
    scale.inputs["Scale"].default_value = 1 / MANIFEST[family]["tile_m"]
    links.new(uv.outputs["UV"], scale.inputs[0])
    textures = {}
    for index, channel in enumerate(("BaseColor", "Roughness", "Metallic", "Normal", "Height")):
        texture = node(tree, "ShaderNodeTexImage", channel, -690, 360 - index * 250)
        texture.image = bpy.data.images.load(str(TEX / f"{family}_{channel}.png"),
                                              check_existing=True)
        if channel != "BaseColor":
            texture.image.colorspace_settings.name = "Non-Color"
        texture.image.pack()
        texture.extension = "REPEAT"
        links.new(scale.outputs["Vector"], texture.inputs["Vector"])
        textures[channel] = texture
    tint = node(tree, "ShaderNodeMixRGB", "Linear substrate reflectance", -360, 350)
    tint.blend_type = "MULTIPLY"
    tint.inputs[0].default_value = 1
    tint.inputs[2].default_value = (*linear, 1)
    links.new(textures["BaseColor"].outputs["Color"], tint.inputs[1])
    links.new(tint.outputs[0], bsdf.inputs["Base Color"])
    rough = node(tree, "ShaderNodeMath", "Keep roughness map variation", -330, 130)
    rough.operation = "ADD"
    rough.inputs[1].default_value = rough_shift
    links.new(textures["Roughness"].outputs["Color"], rough.inputs[0])
    links.new(rough.outputs[0], bsdf.inputs["Roughness"])
    links.new(textures["Metallic"].outputs["Color"], bsdf.inputs["Metallic"])
    normal = node(tree, "ShaderNodeNormalMap", "Tangent micro-normal", -330, -140)
    normal.uv_map = "FinishUV"
    normal.inputs["Strength"].default_value = .38
    links.new(textures["Normal"].outputs["Color"], normal.inputs["Color"])
    bump = node(tree, "ShaderNodeBump", "Supplementary fine relief", 90, -230)
    bump.inputs["Distance"].default_value = MANIFEST[family]["height_range_m"] * .20
    bump.inputs["Strength"].default_value = .08
    links.new(textures["Height"].outputs["Color"], bump.inputs["Height"])
    links.new(normal.outputs["Normal"], bump.inputs["Normal"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    mat["finish_revision"] = "2026-11-12 / physical PBR review"
    mat["finish_family"] = family
    mat["finish_repeat_m"] = MANIFEST[family]["tile_m"]
    mat["pbr_metallic"] = MANIFEST[family]["metallic"]
    mat["pbr_roughness"] = .32 if family == "zinc" else .43 + rough_shift
    CHANGES[name] = {"family": family, "color_srgb": color, "tile_m": mat["finish_repeat_m"]}
    return mat


def physical_uv(data):
    old_active = data.uv_layers.active
    finish = data.uv_layers.get("FinishUV") or data.uv_layers.new(name="FinishUV")
    data.uv_layers.active = finish
    for face in data.polygons:
        planar_face(data, face)
    data.uv_layers.active = old_active


def optimize_wires(scene):
    before = 0
    after = 0
    seen = set()
    for obj in scene.objects:
        if obj.type != "MESH" or not obj.get("equipment_mesh"):
            continue
        data = obj.data
        if data.as_pointer() in seen:
            continue
        seen.add(data.as_pointer())
        if not (obj.name.startswith("CAB_07_Wiring") and
                any(key in obj.name for key in ("Wire", "Loom", "Lead", "Cable", "Branch"))):
            continue
        data.calc_loop_triangles()
        before += len(data.loop_triangles)
        bm = bmesh.new()
        bm.from_mesh(data)
        # Only erase coplanar subdivisions; preserve tube silhouette and bends.
        bmesh.ops.dissolve_limit(bm, angle_limit=.0001, use_dissolve_boundaries=False,
                                verts=list(bm.verts), edges=list(bm.edges),
                                delimit={"NORMAL"})
        bm.to_mesh(data)
        bm.free()
        data.update()
        data.calc_loop_triangles()
        after += len(data.loop_triangles)
    return {"wire_triangles_before": before, "wire_triangles_after": after}


def apply():
    scene = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.window.scene = scene
    scene.frame_set(1)
    optimization = optimize_wires(scene)
    # The previous attachment edit changed vertices after the original unwrap.
    repair = [o for o in scene.objects if o.name == "CAB_15_OperatorHMI_HMIPowerLoom"]
    uv_report = unwrap(repair)
    specs = (
        ("steel", "powder", "A3AAA7", 0, 0, .06),
        ("paint", "powder", "A3AAA7", 0, 0, .06),
        ("blue", "polymer", "252C2D", -.04, 0, 0),
        ("black", "polymer", "181C1D", -.04, 0, 0),
        ("white", "polymer", "C1C2B9", .02, 0, 0),
        ("grey", "polymer", "555E5E", -.02, 0, 0),
        ("teal", "polymer", "266B59", -.01, 0, 0),
        ("yellow", "polymer", "E9BD22", -.04, 0, 0),
        ("red", "polymer", "AD2118", -.07, 0, 0),
        ("drive", "polymer", "24475C", -.06, 0, 0),
        ("wire_blue", "pvc", "215E9A", 0, 0, 0),
        ("wire_brown", "pvc", "62402B", 0, 0, 0),
        ("wire_pe", "pvc", "287B3D", 0, 0, 0),
        ("zinc", "zinc", "CACFD2", 0, .12, 0),
        ("aluminium", "brushed", "D4D7D9", 0, .36, 0),
        ("rubber", "rubber", "181B1B", 0, 0, 0),
    )
    for key, family, color, rough, aniso, coat in specs:
        surface("CAB / " + key, family, color, rough, aniso, coat)
    duct = surface("CAB / PVC cable duct", "polymer", "AFB2AC", .04)
    plinth = surface("CAB / graphite powder base", "powder", "414A4D", .03, coat=.04)
    screw = surface("CAB / nickel fasteners", "brushed", "CED1D2", -.04, .14)
    glass = bpy.data.materials["CAB / inspection glass"].node_tree.nodes.get("Principled BSDF")
    glass.inputs["Base Color"].default_value = (.94, .97, .96, 1)
    glass.inputs["Roughness"].default_value = .035
    glass.inputs["Transmission Weight"].default_value = 1
    glass.inputs["IOR"].default_value = 1.5
    copper = bpy.data.materials["CAB / copper"].node_tree.nodes.get("Principled BSDF")
    copper.inputs["Base Color"].default_value = (.72, .32, .12, 1)
    copper.inputs["Roughness"].default_value = .29
    # Technical printing follows its substrate rather than behaving as paper.
    print_backgrounds = {
        "cpu": "30393B", "io": "30393B", "contactor": "404646",
        "supply": "BFC5C2", "rating": "B0B5B5", "drive": "172F44",
        "network": "1B272C", "safety": "373D3B", "operator": "1B2427",
        "zones": "1B2427", "mcb": "C4C7BE",
    }
    for mat in bpy.data.materials:
        if not mat.name.startswith("CAB / equipment print / "):
            continue
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        key = mat.name.rsplit(" / ", 1)[-1]
        bsdf.inputs["Roughness"].default_value = .35 if key in ("supply", "rating") else .48
        bsdf.inputs["Metallic"].default_value = .75 if key in ("supply", "rating") else 0
    seen_data = set()
    assignments = {}
    for state in bpy.data.scenes:
        bpy.context.window.scene = state
        for obj in state.objects:
            if obj.type != "MESH" or not obj.name.startswith(("CAB_", "cabinet_lamp")):
                continue
            obj["equipment_mesh"] = True
            data = obj.data
            if data.as_pointer() in seen_data:
                continue
            seen_data.add(data.as_pointer())
            physical_uv(data)
            if not data.materials:
                continue
            name = obj.name
            if any(s in name for s in ("Duct", "Trunk", "Wireway")) and not any(
                    s in name for s in ("Screw", "Clamp", "Bracket", "Fastener", "Clip")):
                data.materials[0] = duct
            elif "Plinth" in name and not any(s in name for s in (
                    "Screw", "Fastener", "Anchor", "Rail", "Gland", "Clamp", "Bolt")):
                data.materials[0] = plinth
            elif data.materials[0].name in ("CAB / zinc", "CAB / steel") and any(
                    s in name for s in ("Screw", "Fastener", "HingePin", "Head", "Washer")):
                data.materials[0] = screw
            obj.color = data.materials[0].diffuse_color
            assignments[data.materials[0].name] = assignments.get(data.materials[0].name, 0) + 1
        state["finish_revision"] = "physical-pbr-review-20261112"
        state["review_status"] = "Material revision; awaiting user visual review; Unity deferred"
        state.view_settings.view_transform = "AgX"
        state.view_settings.look = "AgX - Medium High Contrast"
        state.view_settings.exposure = -.25
        state.cycles.samples = 96
        state.cycles.adaptive_threshold = .009
        state.cycles.use_denoising = True
        state.cycles.max_bounces = 10
        state.render.resolution_x, state.render.resolution_y = 2400, 1600
        state.render.resolution_percentage = 100
        for obj in state.objects:
            if obj.type != "LIGHT":
                continue
            if "WindowKey" in obj.name:
                obj.data.energy = 650
                obj.data.size, obj.data.size_y = 1.05, 3.0
            elif "SoftFill" in obj.name:
                obj.data.energy = 130
            elif "FrontReflector" in obj.name:
                obj.data.energy = 55
                obj.data.size, obj.data.size_y = .75, 2.5
            elif "CeilingStrip" in obj.name:
                obj.data.energy = 220
            elif "MetalEdge" in obj.name:
                obj.data.energy = 230
        bg = state.world.node_tree.nodes.get("Background")
        if bg:
            bg.inputs["Strength"].default_value = .18
    floor = bpy.data.materials.get("CAB / floor")
    if floor:
        bsdf = floor.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (.065, .071, .075, 1)
        bsdf.inputs["Roughness"].default_value = .83
    bpy.context.window.scene = scene
    scene.frame_set(1)
    return {"materials": CHANGES, "assignments": assignments,
            "wire_optimization": optimization, "repaired_uv": uv_report}


def render(states, draft=False):
    render_device()
    names = {"01_ASSEMBLED": "assembled", "02_DOOR_OPEN": "door-open",
             "04_INTERNAL": "internal-detail", "06_FRONT": "front"}
    for name in states:
        scene = bpy.data.scenes[name]
        bpy.context.window.scene = scene
        scene.render.resolution_percentage = 50 if draft else 100
        scene.cycles.samples = 32 if draft else 96
        scene.render.filepath = str(MODEL / "previews/electrical-cabinet" /
                                   ("finish-review-" + ("draft-" if draft else "") +
                                    names[name] + ".png"))
        bpy.ops.render.render(write_still=True)
        print("CABINET_FINISH_RENDERED", scene.render.filepath, flush=True)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--render-only", action="store_true")
    parser.add_argument("--draft", action="store_true")
    parser.add_argument("--states", nargs="*", default=[])
    options = parser.parse_args(args)
    target = MODEL / "source/electrical-cabinet-material-review.blend"
    if not options.render_only:
        report = apply()
        report["source"] = str(target)
        report["visual_status"] = "Awaiting user approval"
        (MODEL / "reports/electrical-cabinet-materials.json").write_text(
            json.dumps(report, indent=2), encoding="utf-8")
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(target), check_existing=False)
        print("CABINET_FINISH_SAVED", target, flush=True)
    if options.states:
        render(options.states, options.draft)
