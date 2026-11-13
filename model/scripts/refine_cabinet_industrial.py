"""Refine the review source with curved, terminated wiring and formed controls."""
import argparse
import json
import math
import re
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from industrial_parts import box, cylinder, ring, lathe, boolean, activate
from pump_uv import planar_face
from pump_surface_finish import render_device
from cabinet_components import GROUP_NAMES

MODEL = HERE.parent
OUTPUT = MODEL / "source/electrical-cabinet-industrial-review.blend"
SCENE = bpy.data.scenes["01_ASSEMBLED"]
STATS = {}


def base_name(name):
    return re.sub(r"\.\d+$", "", name)


def physical_uv(data):
    data.uv_layers.active = data.uv_layers.get("UVMap") or data.uv_layers.new(name="UVMap")
    for polygon in data.polygons:
        planar_face(data, polygon)
    data.uv_layers.active = data.uv_layers.get("FinishUV") or data.uv_layers.new(name="FinishUV")
    for polygon in data.polygons:
        planar_face(data, polygon)
    data.uv_layers.active = data.uv_layers["UVMap"]


def prepare(obj):
    obj["equipment_mesh"] = True
    physical_uv(obj.data)
    return obj


def group(scene, index):
    return next(o for o in scene.objects if o.type == "EMPTY"
                and base_name(o.name) == GROUP_NAMES[index])


def share_new(obj, index):
    prepare(obj)
    for scene in bpy.data.scenes:
        if scene == SCENE:
            continue
        copy = obj.copy()
        scene.collection.objects.link(copy)
        copy.parent = group(scene, index)
        copy.matrix_parent_inverse = obj.matrix_parent_inverse.copy()
        copy.matrix_basis = obj.matrix_basis.copy()
    return obj


def replace_data(old, new):
    for obj in bpy.data.objects:
        if obj.type == "MESH" and obj.data == old:
            obj.data = new
    if old.users == 0:
        bpy.data.meshes.remove(old)


def tube_data(name, centers, radius, sides=6):
    centers = [Vector(p) for p in centers]
    verts, faces = [], []
    previous = None
    for i, center in enumerate(centers):
        tangent = (centers[min(i+1, len(centers)-1)] -
                   centers[max(i-1, 0)]).normalized()
        if previous is None:
            ref = Vector((1, 0, 0)) if abs(tangent.x) < .9 else Vector((0, 1, 0))
            u = tangent.cross(ref).normalized()
        else:
            u = (previous-tangent*previous.dot(tangent)).normalized()
        v = tangent.cross(u).normalized()
        previous = u
        for j in range(sides):
            a = math.tau*j/sides
            verts.append(center+radius*(u*math.cos(a)+v*math.sin(a)))
    faces.append(tuple(reversed(range(sides))))
    for i in range(len(centers)-1):
        for j in range(sides):
            faces.append((i*sides+j, i*sides+(j+1) % sides,
                          (i+1)*sides+(j+1) % sides, (i+1)*sides+j))
    faces.append(tuple(range((len(centers)-1)*sides, len(centers)*sides)))
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    bm = bmesh.new()
    bm.from_mesh(data)
    # Individual planar UV islands retain measured scale across bent tubes.
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    for p in data.polygons:
        p.use_smooth = True
    physical_uv(data)
    return data


def rounded_path(points, radius):
    points = [Vector(p) for p in points]
    result = [points[0]]
    for i in range(1, len(points)-1):
        p, prev, after = points[i], points[i-1], points[i+1]
        reach = min((p-prev).length*.40, (after-p).length*.40,
                    max(.009, radius*5))
        a = p+(prev-p).normalized()*reach
        b = p+(after-p).normalized()*reach
        result.append(a)
        for step in (1, 2):
            t = step/2
            result.append((1-t)**2*a+2*(1-t)*t*p+t*t*b)
    result.append(points[-1])
    return result


def new_wire(name, points, radius, material, index=7):
    data = tube_data(name, rounded_path(points, radius), radius, 8)
    data.materials.append(material)
    obj = bpy.data.objects.new(name, data)
    SCENE.collection.objects.link(obj)
    obj.parent = group(SCENE, index)
    obj.matrix_parent_inverse = obj.parent.matrix_world.inverted()
    return share_new(obj, index)


def revise_wires():
    keys = ("MCBFeed", "MCBLoad", "DriveOutput", "DriveInput", "ContactorPower",
            "PLCChannel", "24VFeed", "IndustrialEthernet", "CPUProfinet",
            "FieldNetworkLead", "SafetyLoop", "SafetyReturn", "TerminalUpper",
            "FieldTermination", "ShieldedFieldCable", "ProtectiveEarth", "BackplateBond")
    count, old_tri, new_tri = 0, 0, 0
    seen = set()
    for obj in list(SCENE.objects):
        if obj.type != "MESH" or obj.data.as_pointer() in seen:
            continue
        if not obj.name.startswith("CAB_07_Wiring_") or not any(k in obj.name for k in keys):
            continue
        old = obj.data
        seen.add(old.as_pointer())
        if len(old.vertices) % 4:
            raise RuntimeError("Unexpected source tube topology: " + obj.name)
        centers = [sum((v.co for v in old.vertices[i:i+4]), Vector())/4
                   for i in range(0, len(old.vertices), 4)]
        radius = sum((v.co-centers[0]).length for v in old.vertices[:4])/4
        # Plugs on the module faces need a visible vertical lead before the bend.
        if "PLCChannel" in obj.name:
            centers[0].y = .042
            centers[1].y = .042
        if "ContactorPower" in obj.name:
            centers[0].y = .081
            centers[1].y = .084
        if "24VFeed" in obj.name:
            centers[0].y = .051
            centers[1].y = .051
        if "IndustrialEthernet" in obj.name:
            # Fan out in front of the plugs, then curve to the right-hand edge.
            col = round((centers[0].x-.181)/.040)
            row = round((1.027-centers[0].z)/.038)
            x, z = centers[0].x, centers[0].z
            lane = .408+col*.012
            y = -.011-row*.014-col*.009
            centers = [Vector(p) for p in (
                (x, .027, z), (x, y, z), (x+.010, y-.006, z-.019),
                (lane, y-.006, z-.035-row*.006), (lane, .077, z-.070),
                (lane, .181, .789))]
        if "CPUProfinet" in obj.name:
            centers = [Vector(p) for p in (
                (.171, .010, 1.338), (.171, -.013, 1.311),
                (.198, -.015, 1.294), (.300, .010, 1.278),
                (.326, .112, 1.250), (.326, .184, 1.217))]
        old.calc_loop_triangles()
        old_tri += len(old.loop_triangles)
        data = tube_data(old.name+"_Formed", rounded_path(centers, radius), radius,
                         6 if radius >= .004 else 5)
        for mat in old.materials:
            data.materials.append(mat)
        data.calc_loop_triangles()
        new_tri += len(data.loop_triangles)
        replace_data(old, data)
        count += 1
    STATS["curved_wire_meshes"] = count
    STATS["wire_triangles_before_after"] = [old_tri, new_tri]


def optimize_hidden_small_parts():
    seen, saved = set(), 0
    for obj in list(SCENE.objects):
        if obj.type != "MESH" or not obj.get("equipment_mesh"):
            continue
        data = obj.data
        if data.as_pointer() in seen:
            continue
        seen.add(data.as_pointer())
        if not any(k in obj.name for k in (
                "Screw", "Washer", "HexHead", "HexNut", "Anchor",
                "EndClamp", "ClampBracket", "StandOff", "TerminalWell",
                "ClampRecess", "CoolingFin", "MountingEar", "CaptiveHead")):
            continue
        if max(obj.dimensions) > .14:
            continue
        data.calc_loop_triangles()
        before = len(data.loop_triangles)
        if before < 40:
            continue
        # Apply once, then share the resulting mesh with the other review states.
        original = data
        obj.data = data.copy()
        activate(obj)
        mod = obj.modifiers.new("Small concealed machining detail budget", "DECIMATE")
        mod.ratio = .34
        bpy.ops.object.modifier_apply(modifier=mod.name)
        new = obj.data
        bm = bmesh.new()
        bm.from_mesh(new)
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(new)
        bm.free()
        physical_uv(new)
        replace_data(original, new)
        new.calc_loop_triangles()
        saved += before-len(new.loop_triangles)
    STATS["small_fastener_triangles_saved"] = saved


def simple_material(name, color, roughness, metal=0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    shader = tree.nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metal
    tree.links.new(shader.outputs[0], out.inputs["Surface"])
    mat.diffuse_color = (*color, 1)
    return mat, shader


def tune_materials():
    for mat in bpy.data.materials:
        if not mat.name.startswith("CAB /") or not mat.use_nodes:
            continue
        family = mat.get("finish_family")
        if family in ("powder", "polymer", "pvc"):
            for node in mat.node_tree.nodes:
                if node.type == "NORMAL_MAP":
                    node.inputs["Strength"].default_value = .16
                elif node.type == "BUMP":
                    node.inputs["Strength"].default_value = .035
        if mat.name == "CAB / HMI normal":
            shader = mat.node_tree.nodes.get("Principled BSDF")
            shader.inputs["Emission Strength"].default_value = .65
            shader.inputs["Roughness"].default_value = .46
            shader.inputs["Specular IOR Level"].default_value = .15
    glass, shader = simple_material("CAB / HMI cover glass", (.975, .985, .989), .038)
    shader.inputs["Transmission Weight"].default_value = 1
    shader.inputs["IOR"].default_value = 1.515
    shader.inputs["Coat Weight"].default_value = 0
    nodes, links = glass.node_tree.nodes, glass.node_tree.links
    coord = nodes.new("ShaderNodeTexCoord")
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 43
    noise.inputs["Detail"].default_value = 2
    links.new(coord.outputs["Object"], noise.inputs["Vector"])
    rough = nodes.new("ShaderNodeMapRange")
    rough.inputs["To Min"].default_value = .025
    rough.inputs["To Max"].default_value = .065
    links.new(noise.outputs["Fac"], rough.inputs["Value"])
    links.new(rough.outputs["Result"], shader.inputs["Roughness"])
    mat, shader = simple_material("CAB / moulded stop cap", (.34, .010, .006), .31)
    shader.inputs["Coat Weight"].default_value = .16
    shader.inputs["Coat Roughness"].default_value = .26
    mat, shader = simple_material("CAB / satin button bezel", (.31, .34, .35), .25, 1)
    mat, shader = simple_material("CAB / switch black thermoplastic", (.012, .015, .016), .34)
    shader.inputs["Coat Weight"].default_value = .10
    for name in ("CAB / green_lens", "CAB / amber_lens", "CAB / red_lens"):
        mat = bpy.data.materials[name]
        shader = mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Roughness"].default_value = .19
        shader.inputs["IOR"].default_value = 1.57
        shader.inputs["Transmission Weight"].default_value = .28
        shader.inputs["Coat Weight"].default_value = .32
        shader.inputs["Coat Roughness"].default_value = .13


def replace_formed_controls():
    specs = [
        ("CAB_08_ControlPanel_EstopMushroom", .435, -.316, 1.04,
         [(-.010, .012), (-.005, .018), (0, .0225), (.004, .0235),
          (.009, .022), (.0115, .018), (.0123, .005), (.0123, .0004),
          (-.010, .0004)], "CAB / moulded stop cap", 48),
    ]
    for i, z in enumerate((1.766, 1.605, 1.444)):
        specs.append((f"cabinet_lamp_lens_{i}", .435, -.304, z,
                      [(-.002, .0106), (.001, .0108), (.0028, .0096),
                       (.0038, .006), (.0041, .0003), (-.002, .0003)],
                      ("CAB / green_lens", "CAB / amber_lens", "CAB / green_lens")[i], 32))
    for name, x, y, z, profile, material, segments in specs:
        old = SCENE.objects[name]
        temp = lathe("FormedControlTemporary", (x, y, z), profile,
                     bpy.data.materials[material], axis=(0, -1, 0),
                     segments=segments, bevel=0)
        # Source coordinates may be baked or held in the object's location.
        matrix = old.matrix_world.inverted() @ temp.matrix_world
        temp.data.transform(matrix)
        new = temp.data
        prepare(temp)
        replace_data(old.data, new)
        bpy.data.objects.remove(temp, do_unlink=True)
    # Separate the button trim from the body, with a gasket compression seam.
    for obj in SCENE.objects:
        if obj.type != "MESH" or not obj.data.materials:
            continue
        if any(k in obj.name for k in ("PilotBezel", "EncoderBezel", "ModeSelector")):
            obj.data.materials[0] = bpy.data.materials["CAB / satin button bezel"]
        if any(k in obj.name for k in ("EncoderKnob", "SelectorGrip", "PilotCollar")):
            obj.data.materials[0] = bpy.data.materials["CAB / switch black thermoplastic"]
        if "MembraneKey" in obj.name:
            mat = obj.data.materials[0].copy()
            mat.name = "CAB / satin membrane " + obj.name.rsplit(".", 1)[-1]
            shader = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
            shader.inputs["Coat Weight"].default_value = .12
            obj.data.materials[0] = mat
    for i in range(12):
        a = math.tau*i/12
        obj = box("CAB_08_ControlPanel_EstopGripFlute",
                  (.435+.0228*math.cos(a), -.316, 1.04+.0228*math.sin(a)),
                  (.0009, .005, .0009), bpy.data.materials["CAB / moulded stop cap"],
                  group(SCENE, 8), 0)
        share_new(obj, 8)
    STATS["formed_control_heads"] = len(specs)


def synchronize_glass():
    # The prior pass added glass only to the unsuffixed assembled group.
    pane = SCENE.objects["CAB_15_OperatorHMI_CoverGlass"]
    for scene in bpy.data.scenes:
        if scene == SCENE or any(base_name(o.name) == pane.name for o in scene.objects):
            continue
        copy = pane.copy()
        scene.collection.objects.link(copy)
        copy.parent = group(scene, 15)
        copy.matrix_parent_inverse = pane.matrix_parent_inverse.copy()
        copy.matrix_basis = pane.matrix_basis.copy()


def cable_entries():
    centers = (-.34, -.17, 0, .17, .34)
    old_centers = (-.39, -.25, -.11, .03, .17)
    for scene in bpy.data.scenes:
        for obj in scene.objects:
            if "CableGland" not in obj.name or obj.type != "MESH":
                continue
            lo = min((obj.matrix_world @ v.co).x for v in obj.data.vertices)
            hi = max((obj.matrix_world @ v.co).x for v in obj.data.vertices)
            current_x = (lo+hi)/2
            index = min(range(5), key=lambda i: abs(current_x-old_centers[i]))
            obj.location.x += centers[index]-current_x
    # The five shielded field cables meet the lower shoulder of each gland.
    for obj in list(SCENE.objects):
        if obj.type != "MESH" or not obj.name.startswith("CAB_07_Wiring_ShieldedFieldCable"):
            continue
        old = obj.data
        seen = {old.as_pointer()}
        for scene in bpy.data.scenes:
            sibling = next((o for o in scene.objects if o.type == "MESH"
                            and base_name(o.name) == base_name(obj.name)), None)
            if not sibling or sibling.data.as_pointer() in seen:
                continue
            seen.add(sibling.data.as_pointer())
        # Each bevelled tube was built from sampled rings; move only the final
        # ring along Z so the insulation enters the gland rather than stopping
        # visibly inside its body.
        for data in [old] + [d for d in bpy.data.meshes
                             if d.name == old.name and d != old]:
            for vertex in data.vertices:
                if vertex.co.z <= .145:
                    vertex.co.z -= .016
            data.update()
            physical_uv(data)
    old = next(o for o in SCENE.objects if "BottomPanel" in o.name)
    temp = box("ReplacementBottomPanel", (0, .0075, .134),
               (1.196, .623, .0015), bpy.data.materials["CAB / paint"], bevel=0)
    for x in centers:
        tool = cylinder("GlandOpeningTool", (x, .055, .134), .014,
                        .025, None, axis=(0, 0, 1), segments=16, bevel=0)
        boolean(temp, tool)
    temp.data.transform(old.matrix_world.inverted() @ temp.matrix_world)
    bm = bmesh.new()
    bm.from_mesh(temp.data)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(temp.data)
    bm.free()
    prepare(temp)
    replace_data(old.data, temp.data)
    bpy.data.objects.remove(temp, do_unlink=True)
    for obj in list(SCENE.objects):
        if obj.type != "MESH":
            continue
        if "ShieldClamp" in obj.name:
            for scene_obj in bpy.data.objects:
                if scene_obj.type == "MESH" and scene_obj.data == obj.data:
                    scene_obj.location.y = .168
        elif "FieldCableClamp" in obj.name:
            for scene_obj in bpy.data.objects:
                if scene_obj.type == "MESH" and scene_obj.data == obj.data:
                    scene_obj.location.y = .186
                    scene_obj.location.z = .275
    rail = box("CAB_07_Wiring_FieldCableSupportRail", (0, .200, .275),
               (.91, .018, .025), bpy.data.materials["CAB / zinc"],
               group(SCENE, 7), .0005)
    share_new(rail, 7)
    STATS["gland_centres_x_m"] = centers


def add_hmi_connectors():
    zinc = bpy.data.materials["CAB / nickel fasteners"]
    black = bpy.data.materials["CAB / switch black thermoplastic"]
    for x, name, size in ((-.18, "Power", (.022, .018, .020)),
                           (-.05, "Ethernet", (.018, .019, .021))):
        obj = box("CAB_15_OperatorHMI_"+name+"Plug",
                  (x, -.190, 1.403), size, black, group(SCENE, 15), .0006)
        share_new(obj, 15)
        obj = box("CAB_15_OperatorHMI_"+name+"SocketShield",
                  (x, -.199, 1.412), (size[0]+.004, .006, .014),
                  zinc, group(SCENE, 15), .0004)
        share_new(obj, 15)
    for z in (1.40, 1.54, 1.66):
        obj = box("CAB_15_OperatorHMI_RearHousingMouldSeam",
                  (-.173, -.1968, z), (.55, .0003, .0005),
                  black, group(SCENE, 15), 0)
        share_new(obj, 15)


def arrange_lights():
    for scene in bpy.data.scenes:
        scene.view_settings.look = "AgX - Medium High Contrast"
        scene.view_settings.exposure = -.05
        scene.cycles.max_bounces = 12
        scene.cycles.transmission_bounces = 8
        scene.cycles.glossy_bounces = 5
        scene.cycles.use_denoising = True
        scene.cycles.adaptive_threshold = .012
        for obj in scene.objects:
            if obj.type != "LIGHT":
                continue
            if "WindowKey" in obj.name:
                obj.data.energy = 700
                obj.data.size, obj.data.size_y = 1.2, 3
            if "SoftFill" in obj.name:
                obj.data.energy = 190
            if "FrontReflector" in obj.name:
                obj.data.energy = 90
        data = bpy.data.lights.new(scene.name+"_GlassReflectionStrip", "AREA")
        data.shape, data.size, data.size_y, data.energy = "RECTANGLE", .035, .70, 5
        obj = bpy.data.objects.new(data.name, data)
        scene.collection.objects.link(obj)
        obj.location = (-1.9, -3.6, 1.06)
        obj.rotation_euler = (Vector((-.32, -.31, 1.57))-obj.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.resolution_x, scene.render.resolution_y = 2400, 1600
        scene.render.resolution_percentage = 100


def measure():
    result = {}
    for obj in SCENE.objects:
        if obj.type == "MESH" and obj.get("equipment_mesh"):
            obj.data.calc_loop_triangles()
            owner = obj.parent.name
            result[owner] = result.get(owner, 0)+len(obj.data.loop_triangles)
    STATS["triangles"] = sum(result.values())
    STATS["triangles_by_group"] = result
    return sum(result.values())


def render(names, draft):
    render_device()
    suffixes = {"01_ASSEMBLED": "assembled", "02_DOOR_OPEN": "door-open",
                "04_INTERNAL": "internal", "06_FRONT": "operator"}
    for name in names:
        scene = bpy.data.scenes[name]
        bpy.context.window.scene = scene
        scene.render.resolution_percentage = 50 if draft else 100
        scene.cycles.samples = 32 if draft else 128
        scene.render.filepath = str(MODEL / "previews/electrical-cabinet" /
                                   ("industrial-review-" + ("draft-" if draft else "")
                                    + suffixes[name] + ".png"))
        bpy.ops.render.render(write_still=True)
        print("INDUSTRIAL_REVIEW_RENDERED", scene.render.filepath, flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--render-only", action="store_true")
    parser.add_argument("--draft", action="store_true")
    parser.add_argument("--states", nargs="*", default=[])
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if not args.render_only:
        if not Path(bpy.data.filepath).name == "electrical-cabinet-realism-review.blend":
            raise RuntimeError("Use the preserved realism review source as input")
        bpy.context.window.scene = SCENE
        SCENE.frame_set(1)
        optimize_hidden_small_parts()
        revise_wires()
        tune_materials()
        replace_formed_controls()
        synchronize_glass()
        cable_entries()
        add_hmi_connectors()
        arrange_lights()
        measure()
        STATS["input"] = bpy.data.filepath
        STATS["output"] = str(OUTPUT)
        STATS["review_status"] = "Awaiting visual review; no Unity changes"
        STATS["limitations"] = [
            "Not a manufacturer-specific electrical design or certified assembly",
            "Wire-to-device and wire-to-wire clearance is not exhaustively validated",
            "Door-to-body flexible harness motion still needs dedicated review"]
        (MODEL / "reports/electrical-cabinet-industrial-review.json").write_text(
            json.dumps(STATS, indent=2), encoding="utf-8")
        if STATS["triangles"] > 110000:
            raise RuntimeError("Triangle budget exceeded: " + str(STATS["triangles"]))
        bpy.context.window.scene = SCENE
        SCENE.frame_set(1)
        SCENE["review_status"] = STATS["review_status"]
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT), check_existing=False)
        print("INDUSTRIAL_REVIEW_SAVED", json.dumps(STATS), flush=True)
    if args.states:
        render(args.states, args.draft)


if __name__ == "__main__":
    main()
