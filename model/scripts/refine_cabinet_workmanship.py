"""Cabinet review: restrained controls, routed conductors and real terminations."""
import argparse
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import refine_cabinet_industrial as previous
from industrial_parts import box, cylinder, ring, activate
from pump_surface_finish import render_device

MODEL = HERE.parent
OUTPUT = MODEL / "source/electrical-cabinet-workmanship-review.blend"
SCENE = previous.SCENE
REPORT = {"revision": "workmanship", "routes": [], "operations": {}}
SLOT_USAGE = {}


def shader(mat):
    return next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")


def plastic(name, color, roughness, specular=.27):
    mat, node = previous.simple_material(name, color, roughness)
    node.inputs["Specular IOR Level"].default_value = specular
    node.inputs["IOR"].default_value = 1.48
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    coord = nodes.new("ShaderNodeTexCoord")
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 4800
    noise.inputs["Detail"].default_value = 2
    links.new(coord.outputs["Object"], noise.inputs["Vector"])
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .11
    bump.inputs["Distance"].default_value = .000014
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], node.inputs["Normal"])
    return mat


def controls():
    red = plastic("CAB / moulded stop cap", (.255, .008, .004), .57)
    black = plastic("CAB / switch black thermoplastic", (.009, .012, .013), .56)
    yellow = plastic("CAB / satin control yellow", (.55, .36, .015), .59)
    bezel = bpy.data.materials["CAB / satin button bezel"]
    node = shader(bezel)
    node.inputs["Base Color"].default_value = (.18, .195, .20, 1)
    node.inputs["Roughness"].default_value = .52
    node.inputs["Coat Weight"].default_value = 0
    for name in ("green_lens", "amber_lens", "red_lens"):
        mat = bpy.data.materials["CAB / " + name]
        node = shader(mat)
        for socket in ("Roughness", "Coat Weight", "Emission Strength",
                       "Transmission Weight", "Specular IOR Level"):
            for link in list(node.inputs[socket].links):
                mat.node_tree.links.remove(link)
        node.inputs["Roughness"].default_value = .43
        node.inputs["Coat Weight"].default_value = 0
        node.inputs["Transmission Weight"].default_value = .06
        node.inputs["Specular IOR Level"].default_value = .23
        node.inputs["IOR"].default_value = 1.49
        node.inputs["Emission Strength"].default_value = .065 if name == "green_lens" else 0
        node.inputs["Base Color"].default_value = {
            "green_lens": (.006, .105, .015, 1),
            "amber_lens": (.37, .095, .003, 1),
            "red_lens": (.26, .004, .002, 1),
        }[name]
    for obj in SCENE.objects:
        if obj.type != "MESH" or not obj.data.materials:
            continue
        name = obj.name
        if "CAB_08_ControlPanel" in name:
            if "IsolatorBase" in name or "EstopLegendRing" in name:
                obj.data.materials[0] = yellow
            elif any(s in name for s in ("EstopMushroom", "EstopGrip", "IsolatorHub",
                                         "IsolatorLever")):
                obj.data.materials[0] = red
            elif "SelectorGrip" in name:
                obj.data.materials[0] = black
        if "IndustrialNetwork" in name and "_Boot" in name:
            obj.data.materials[0] = black
    REPORT["operations"]["controls"] = {
        "lens_roughness": .43, "lens_coat": 0,
        "green_emission_strength": .065, "stop_roughness": .57,
        "bezel_roughness": .52, "hmi_materials_unchanged": True,
    }


def remove_family(prefix):
    targets = [o for o in SCENE.objects if o.name.startswith(prefix)]
    data_ids = {o.data.as_pointer() for o in targets if o.type == "MESH"}
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.data.as_pointer() in data_ids:
            bpy.data.objects.remove(obj, do_unlink=True)


def simplify_small_socket_edges():
    # One-segment edge breaks retain socket silhouettes while freeing wire budget.
    keys = ("ToggleWell", "RecessedCage", "IOPluggableConnector",
            "InterfaceSocket", "InterfaceRelay", "PowerTerminal",
            "FieldPatch_Plug", "FieldPatch_Boot", "SafetyRelays_Plug",
            "TerminalWell", "ClampRecess", "TerminalScrew_Slot",
            "IOClamp_Slot", "ScrewClamp_Slot", "MCCBTerminalCage",
            "CPUPluggableTerminal", "Switch_RJ45_Plug", "Switch_RJ45_Boot",
            "CoolingSlot", "LowerCoolingSlot", "IntakeVentRib",
            "RetainingClip", "LocalKey", "IOChannelLED", "Toggle",
            "SteppedFront", "LinkedToggleBar", "Shield_Vertical", "Shield_Horizontal")
    saved = 0
    for obj in list(SCENE.objects):
        if obj.type != "MESH" or not any(k in obj.name for k in keys):
            continue
        data = obj.data
        data.calc_loop_triangles()
        before = len(data.loop_triangles)
        if before < 70:
            continue
        lo = Vector(tuple(min(v.co[i] for v in data.vertices) for i in range(3)))
        hi = Vector(tuple(max(v.co[i] for v in data.vertices) for i in range(3)))
        size, center = hi-lo, (lo+hi)*.5
        if min(size) < .0001:
            continue
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        for vertex in bm.verts:
            vertex.co = Vector(tuple(vertex.co[i]*size[i] for i in range(3))) + center
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(.00045, min(size)*.12),
                       segments=1, affect="EDGES")
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        new = bpy.data.meshes.new(data.name+"_EdgeBreak")
        bm.to_mesh(new)
        bm.free()
        for mat in data.materials:
            new.materials.append(mat)
        previous.physical_uv(new)
        previous.replace_data(data, new)
        new.calc_loop_triangles()
        saved += before-len(new.loop_triangles)
    REPORT["operations"]["small_socket_triangles_saved"] = saved


def cubic(a, b, c, d, count=10):
    a, b, c, d = map(Vector, (a, b, c, d))
    return [(1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t*t*c+t**3*d
            for t in (i/(count-1) for i in range(count))]


def piecewise(segments, count=8):
    points = []
    for segment in segments:
        points.extend(cubic(*segment, count=count)[bool(points):])
    return points


def duct_slot(x, origin, offset=0):
    return origin + round((x-origin)/.022)*.022 + offset


def occupied_slot(x, origin, radius, key):
    center = duct_slot(x, origin)
    slot = (key, round(center, 5))
    used = SLOT_USAGE.get(slot, 0)
    SLOT_USAGE[slot] = used+1
    capacity = 2 if radius <= .0018 else 1
    offset = (-.0018 if used % 2 == 0 else .0018) if capacity == 2 else 0
    return center+offset, (used//capacity)*.009


def wire(obj, points, radius, material=None, label=None):
    points = [Vector(p) for p in points]
    data = previous.tube_data(obj.name+"_Sweep", points, radius,
                              6 if radius >= .0023 else 5)
    data.transform(obj.matrix_world.inverted())
    previous.physical_uv(data)
    for mat in ([material] if material else obj.data.materials):
        data.materials.append(mat)
    previous.replace_data(obj.data, data)
    obj["routing_revision"] = "workmanship"
    obj["centerline_m"] = [float(c) for p in points for c in p]
    obj["wire_radius_m"] = radius
    REPORT["routes"].append({
        "name": obj.name, "kind": label or obj.name.split("_")[-1],
        "radius_m": radius, "points": [list(p) for p in points],
    })
    return obj


def add_wire(name, points, radius, material, index=7):
    obj = bpy.data.objects.new(name, bpy.data.meshes.new(name+"_Initial"))
    SCENE.collection.objects.link(obj)
    obj.parent = previous.group(SCENE, index)
    obj.matrix_parent_inverse = obj.parent.matrix_world.inverted()
    wire(obj, points, radius, material)
    return previous.share_new(obj, index)


def add_cylinder(name, pos, radius, length, mat, axis=(0, 0, 1), sides=6):
    obj = cylinder(name, pos, radius, length, mat, previous.group(SCENE, 7),
                   axis=axis, segments=sides, bevel=0)
    return previous.share_new(obj, 7)


def old_centers(obj, sides):
    vertices = obj.data.vertices
    return [obj.matrix_world @
            (sum((v.co for v in vertices[i:i+sides]), Vector())/sides)
            for i in range(0, len(vertices), sides)]


def wire_family(prefix):
    return sorted((o for o in SCENE.objects if o.type == "MESH"
                   and o.name.startswith("CAB_07_Wiring_"+prefix)), key=lambda o: o.name)


def module_and_power_wires():
    ferrule = plastic("CAB / ferrule nylon", (.45, .47, .46), .68)
    for n, obj in enumerate(wire_family("PLCChannel")):
        old = old_centers(obj, 5)
        p = old[0]
        top = p.z > 1.4
        direction = 1 if top else -1
        # Wires enter the top/bottom of the plug, behind its screw face.
        p.z = 1.458 if top else 1.298
        dest_z = 1.588 if top else 1.217
        bank = max(0, min(3, round((p.x-.232)/.058)))
        channel = max(0, min(3, round((p.x-(.217+bank*.058))/.010)))
        target = .217+bank*.058+(channel//2)*.022
        end_x = duct_slot(target, -.448 if top else .106,
                          .00165 if channel % 2 else -.00165)
        end = Vector((end_x, .180+(channel % 2)*.004, dest_z))
        entry_z = dest_z-direction*.030
        points = piecewise([
            (p, p+Vector((0, 0, direction*.028)),
             (end_x, .105, entry_z-direction*.018),
             (end_x, .171, entry_z)),
            ((end_x, .171, entry_z), (end_x, .180, entry_z+direction*.010),
             end-Vector((0, 0, direction*.008)), end),
        ], count=5)
        wire(obj, points, .0013)
        add_cylinder("CAB_07_Wiring_IOFerrule", (p.x, p.y, p.z+direction*.006),
                     .00185, .008, ferrule)

    for obj in wire_family("24VFeed"):
        old = old_centers(obj, 5)
        p = old[0]
        p.z = 1.645
        end_x, layer = occupied_slot(old[-1].x, -.448, .0018, "24V")
        end = (end_x, .184+layer, 1.588)
        points = piecewise([
            (p, p+Vector((0, 0, -.014)), (end_x, .051, 1.626),
             (end_x, .051, 1.619)),
            ((end_x, .051, 1.619), (end_x, .154, 1.617),
             (end_x, .184, 1.600), end),
        ], count=5)
        wire(obj, points, .0018)

    for prefix, radius in (("MCBFeed", .0017), ("MCBLoad", .0017),
                            ("DriveInput", .0027), ("DriveOutput", .0032),
                            ("ContactorPower", .0025), ("SafetyLoop", .0015),
                            ("SafetyReturn", .0015)):
        for n, obj in enumerate(wire_family(prefix)):
            old = old_centers(obj, 5)
            start, end = old[0].copy(), old[-1].copy()
            direction = 1 if end.z > start.z else -1
            dest_z = end.z
            if prefix == "ContactorPower":
                start.z = .898 if direction > 0 else .698
                start.y = .089 if direction > 0 else .122
            elif prefix == "DriveOutput":
                start.z = 1.143
            origin = -.448
            if abs(dest_z-1.057) < .01:
                origin = -.444
            elif abs(dest_z-.789) < .01:
                origin = .1065
            end.x, layer = occupied_slot(end.x, origin, radius, prefix)
            end.y = .182+layer
            shoulder = dest_z-direction*(.029 if abs(dest_z-.789) > .01 else .026)
            entry = Vector((end.x, .173+layer, shoulder))
            span = abs(shoulder-start.z)
            points = piecewise([
                (start, start+Vector((0, 0, direction*max(.010, span*.32))),
                 (end.x, .115+(n % 3)*.007, shoulder-direction*min(.035, span*.35)),
                 entry),
                (entry, (end.x, .182+layer, shoulder+direction*.007),
                 end-Vector((0, 0, direction*.008)), end),
            ], count=5 if span < .085 else 6)
            wire(obj, points, radius)


def network():
    mat = plastic("CAB / network cable PVC", (.009, .065, .106), .63)
    remove_family("CAB_07_Wiring_FieldNetworkLead")
    objs = wire_family("IndustrialEthernet")
    for obj in objs:
        centers = old_centers(obj, 5)
        p = centers[0]
        col = round((p.x-.181)/.040)
        row = round((1.027-p.z)/.038)
        if row == 1:
            end = Vector((.175+col*.062, .058, .858))
            depth = -.018-col*.009
            middle = Vector((p.x+.008+col*.006, depth, .921-col*.006))
            points = piecewise([
                (p, p+Vector((0, -.040, 0)),
                 middle+Vector((-.010, -.014, .024)), middle),
                (middle, middle+Vector((.010, .008, -.026)),
                 end+Vector((0, -.042, 0)), end),
            ], count=9)
        elif col == 1:
            end = Vector((.361, .058, .858))
            middle = Vector((.398, -.040, .949))
            points = piecewise([
                (p, p+Vector((0, -.050, 0)), (.370, -.060, 1.000), middle),
                (middle, (.407, -.030, .905),
                 end+Vector((0, -.049, 0)), end),
            ], count=10)
        else:
            lane = .430 if col == 0 else .447
            depth = -.067 if col == 0 else -.039
            mid = (lane, depth, .969 if col == 0 else .985)
            end = Vector((duct_slot(lane if col == 0 else lane+.012, .1065), .183, .789))
            points = piecewise([
                (p, p+Vector((0, -.065, 0)), (lane-.020, depth, p.z-.003), mid),
                (mid, (lane+.006, depth, .930),
                 (end.x, .155, .846), end),
            ], count=10)
        wire(obj, points, .0025, mat)

    cpu = wire_family("CPUProfinet")[0]
    start = Vector((.171, .010, 1.338))
    end = Vector((.1725, .184, 1.217))
    mid = Vector((.208, -.019, 1.284))
    wire(cpu, piecewise([
        (start, (.171, -.030, 1.338), (.210, -.034, 1.300), mid),
        (mid, (.183, -.003, 1.270), (.1725, .120, 1.255), (.1725, .172, 1.248)),
        ((.1725, .172, 1.248), (.1725, .184, 1.241),
         (.1725, .184, 1.228), end),
    ], count=6), .0025, mat)
    # Keep original supports only where a routed bundle now passes through them.
    for prefix in ("CAB_07_Wiring_NetworkSaddle", "CAB_07_Wiring_NetworkTieMount"):
        remove_family(prefix)
    rubber = bpy.data.materials["CAB / switch black thermoplastic"]
    for x, y, z in ((.432, .032, .914), (.443, .112, .850)):
        anchor = box("CAB_07_Wiring_NetworkSupport",
                     (x, (y+.198)/2, z), (.012, .198-y, .009),
                     rubber, previous.group(SCENE, 7), .0004)
        previous.share_new(anchor, 7)


def field_wiring():
    ferrule = bpy.data.materials["CAB / ferrule nylon"]
    metal = bpy.data.materials["CAB / nickel fasteners"]
    black = bpy.data.materials["CAB / switch black thermoplastic"]
    remove_family("CAB_07_Wiring_FerruleSleeve")
    for n, obj in enumerate(wire_family("TerminalUpper")):
        old = old_centers(obj, 5)
        p = old[0].copy()
        p.z = .500
        p.y = .138
        end_x = duct_slot(p.x+.004, -.448, -.0015 if n % 2 else .0015)
        if n in (14, 15):
            end_x = .0158 if n == 14 else .1002
        end = Vector((end_x, .184, .575))
        entry = Vector((end_x, .174, .545))
        wire(obj, piecewise([
            (p, p+Vector((0, 0, .020)),
             (end_x, .154, .531+(n % 3)*.002), entry),
            (entry, (end_x, .184, .554), (end_x, .184, .567), end),
        ], count=5), .0015)
        add_cylinder("CAB_07_Wiring_X1UpperFerrule", (p.x, p.y, .512),
                     .0022, .010, ferrule)

    centers = (-.34, -.17, 0, .17, .34)
    for obj in wire_family("FieldTermination"):
        old = old_centers(obj, 5)
        tx = old[0].x
        n = round((tx+.3655)/.029)
        cable = n//5
        core = n % 5
        count = 4 if cable == 4 else 5
        breakout_x = -.3655+(cable*5+(count-1)/2)*.029
        end = Vector((breakout_x+(core-(count-1)/2)*.0023,
                      .183+(.0007 if core % 2 else -.0007), .357))
        start = Vector((tx, .138, .441))
        points = cubic(start, (tx, .138, .405-(core % 3)*.004),
                       (end.x, end.y, .388+(core % 2)*.005), end, count=11)
        wire(obj, points, .00115)
        add_cylinder("CAB_07_Wiring_X1LowerFerrule", (tx, .138, .428),
                     .0021, .010, ferrule)
        add_cylinder("CAB_07_Wiring_X1CrimpPin", (tx, .138, .436),
                     .0014, .007, metal)
    for n, obj in enumerate(wire_family("ShieldedFieldCable")):
        x = centers[n]
        count = 4 if n == 4 else 5
        breakout_x = -.3655+(n*5+(count-1)/2)*.029
        start, end = Vector((breakout_x, .183, .361)), Vector((x, .055, .126))
        mid = Vector((x, .168, .275))
        points = piecewise([
            (start, (breakout_x, .183, .330), (x, .168, .305), mid),
            (mid, (x, .168, .226), (x, .055, .188), end),
        ], count=10)
        wire(obj, points, .006)
        add_cylinder("CAB_07_Wiring_JacketBreakoutSleeve", (breakout_x, .183, .357),
                     .0068, .018, black, sides=10)
        # A band overlaps the jacket and bears against its support bracket.
        obj = ring("CAB_07_Wiring_FieldCableBand", (x, .168, .275),
                   .0067, .0058, .008, metal, previous.group(SCENE, 7),
                   axis=(0, 0, 1), segments=10, bevel=0)
        previous.share_new(obj, 7)


def hmi_leads():
    mat = bpy.data.materials["CAB / network cable PVC"]
    obj = SCENE.objects.get("CAB_15_OperatorHMI_EthernetLead")
    if not obj:
        return
    # Cabinet coordinates at the closed-door pose; the door group carries it.
    start = Vector((-.05, -.190, 1.399))
    end = Vector((-.31, -.185, 1.305))
    points = piecewise([
        (start, (-.05, -.181, 1.360), (-.055, -.175, 1.293),
         (-.12, -.182, 1.296)),
        ((-.12, -.182, 1.296), (-.18, -.187, 1.295),
         (-.275, -.185, 1.305), end),
    ], count=8)
    wire(obj, points, .0024, mat, "door Ethernet")


def detail_cameras():
    scene = bpy.data.scenes["04_INTERNAL"]
    for name, position, target, lens in (
        ("Workmanship_NetworkCamera", (.69, -1.95, 1.30), (.20, .11, 1.23), 55),
        ("Workmanship_TerminalCamera", (.84, -1.70, .72), (0, .11, .43), 55),
    ):
        data = bpy.data.cameras.new(name)
        data.lens = lens
        data.clip_start = .02
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = position
        obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()


def validate_routes():
    # Changed field cores must clear the copper PE bar with their full radius.
    field = [r for r in REPORT["routes"] if "FieldTermination" in r["name"]]
    jacket = [r for r in REPORT["routes"] if "ShieldedFieldCable" in r["name"]]
    collisions = []
    closest = 1
    for route in field+jacket:
        for point in route["points"]:
            if .305 <= point[2] <= .335:
                clearance = (max(.155-point[1], point[1]-.163)
                             -route["radius_m"])
                closest = min(closest, clearance)
                if clearance < .001:
                    collisions.append(route["name"])

    device_keys = ("MouldedShell", "ManagedSwitchMetalCase", "FieldPatchCarrier",
                   "CPUCase", "IOModule", "ACDC24V_Body", "DCUPS_Body",
                   "MouldedBody", "_Overload", "CopperPEBusbar")
    blockers = []
    for obj in SCENE.objects:
        if obj.type != "MESH" or not any(k in obj.name for k in device_keys):
            continue
        verts = [obj.matrix_world @ v.co for v in obj.data.vertices]
        obj.data.calc_loop_triangles()
        faces = [tuple(t.vertices) for t in obj.data.loop_triangles]
        blockers.append((obj.name, BVHTree.FromPolygons(verts, faces, all_triangles=True)))
    crossings = []
    for route in REPORT["routes"]:
        points = [Vector(p) for p in route["points"]]
        for i, (a, b) in enumerate(zip(points, points[1:])):
            if i == 0 or i == len(points)-2:
                continue
            direction = b-a
            for name, tree in blockers:
                hit = tree.ray_cast(a, direction.normalized(), direction.length)
                if hit[0] is not None:
                    crossings.append({"wire": route["name"], "solid": name,
                                      "point": list(hit[0])})
    REPORT["routing_checks"] = {
        "route_count": len(REPORT["routes"]),
        "field_core_count": len(field),
        "field_jacket_count": len(jacket),
        "minimum_sampled_pe_bar_clearance_m": closest,
        "pe_bar_clearance_failures": sorted(set(collisions)),
        "changed_centerline_device_surface_crossings": crossings,
        "scope": "Sampled new centerlines against major device shells; terminal insertion "
                 "segments excluded. Not full cable-surface, wire-wire, duct or moving-harness certification.",
    }


def render(states, draft):
    render_device()
    suffixes = {"01_ASSEMBLED": "assembled", "02_DOOR_OPEN": "door-open",
                "04_INTERNAL": "internal", "06_FRONT": "operator",
                "07_NETWORK": "wiring", "08_TERMINALS": "terminals"}
    for name in states:
        scene = bpy.data.scenes["04_INTERNAL" if name in ("07_NETWORK", "08_TERMINALS") else name]
        bpy.context.window.scene = scene
        saved_camera = scene.camera
        if name == "07_NETWORK":
            scene.camera = scene.objects["Workmanship_NetworkCamera"]
        elif name == "08_TERMINALS":
            scene.camera = scene.objects["Workmanship_TerminalCamera"]
        scene.render.resolution_x, scene.render.resolution_y = 2400, 1600
        scene.render.resolution_percentage = 50 if draft else 100
        scene.cycles.samples = 24 if draft else 96
        scene.cycles.use_denoising = True
        scene.render.filepath = str(MODEL / "previews/electrical-cabinet" /
                                   ("workmanship-review-"+("draft-" if draft else "")
                                    +suffixes[name]+".png"))
        bpy.ops.render.render(write_still=True)
        scene.camera = saved_camera
        print("WORKMANSHIP_RENDERED", scene.render.filepath, flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--render-only", action="store_true")
    parser.add_argument("--draft", action="store_true")
    parser.add_argument("--states", nargs="*", default=[])
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if not args.render_only:
        if Path(bpy.data.filepath).name != "electrical-cabinet-industrial-review.blend":
            raise RuntimeError("Use the preserved industrial-review input")
        bpy.context.window.scene = SCENE
        SCENE.frame_set(1)
        controls()
        simplify_small_socket_edges()
        module_and_power_wires()
        network()
        field_wiring()
        hmi_leads()
        detail_cameras()
        bpy.context.view_layer.update()
        validate_routes()
        total = previous.measure()
        REPORT["triangles"] = total
        REPORT["triangles_by_group"] = previous.STATS["triangles_by_group"]
        REPORT["input"] = bpy.data.filepath
        REPORT["output"] = str(OUTPUT)
        REPORT["review_status"] = "Awaiting user visual review; no Unity integration"
        (MODEL / "reports/electrical-cabinet-workmanship-review.json").write_text(
            json.dumps(REPORT, indent=2), encoding="utf-8")
        print("WORKMANSHIP_GEOMETRY", total, REPORT["routing_checks"], flush=True)
        if total > 110000:
            raise RuntimeError("Triangle budget exceeded: "+str(total))
        bpy.context.preferences.filepaths.save_version = 0
        bpy.context.window.scene = SCENE
        SCENE.frame_set(1)
        bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT), check_existing=False)
        print("WORKMANSHIP_SAVED", str(OUTPUT), flush=True)
    if args.states:
        render(args.states, args.draft)


if __name__ == "__main__":
    main()
