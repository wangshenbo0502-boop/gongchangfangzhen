"""Repair exposed routing boundaries and inspect their actual cover occlusion."""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import refine_cabinet_field_routes as field

work, shared, SCENE = field.work, field.shared, field.SCENE
MODEL = HERE.parent
OUTPUT = MODEL / "source/electrical-cabinet-duct-entry-review.blend"
REPORT = MODEL / "reports/electrical-cabinet-duct-entry-review.json"
DUCTS = [
    (-.005, .93, 1.910, .040), (-.005, .93, 1.588, .040),
    (-.231, .47, 1.057, .040), (.273, .377, 1.217, .040),
    (-.005, .93, .575, .040), (.273, .377, .789, .035),
    (-.005, .900, .392, .035),
]


def delete_shared(obj):
    data = obj.data
    for copy in list(bpy.data.objects):
        if copy.type == "MESH" and copy.data == data:
            bpy.data.objects.remove(copy, do_unlink=True)


def covers():
    original = next(o for o in SCENE.objects
                    if "CrossDuct_RemovableLid" in o.name)
    material = original.data.materials[0]
    for obj in list(SCENE.objects):
        if obj.type != "MESH" or "RemovableLid" not in obj.name:
            continue
        z = (obj.matrix_world @ obj.data.vertices[0].co).z
        if any(abs(z-target) < .025 for target in (1.910, 1.588, 1.057, 1.217)):
            delete_shared(obj)
    # At the crossing, horizontal lids butt against the vertical lid.
    sections = [
        (1.910, -.470, -.0055), (1.910, -.0045, .460),
        (1.588, -.470, .0375), (1.588, .0825, .460),
        (1.057, -.466, -.2315), (1.057, -.2305, .004),
        (1.217, .0845, .2725), (1.217, .2735, .4615),
    ]
    for z, left, right in sections:
        field.block("CAB_07_Wiring_InstalledDuct_RemovableLid",
                    ((left+right)/2, .147, z),
                    (right-left, .005, .043), material, .00045)
    return sections


def repair_feeds():
    ferrule = bpy.data.materials["CAB / ferrule nylon"]
    brown = work.wire_family("MCBFeed")[0].data.materials[0]
    blue = work.wire_family("24VFeed")[1].data.materials[0]
    red = work.wire_family("24VFeed")[0].data.materials[0]
    pe = work.wire_family("DriveOutput")[3].data.materials[0]
    materials = (brown, blue, pe, red, blue)
    for n, obj in enumerate(work.wire_family("24VFeed")):
        p = work.old_centers(obj, 5)[0]
        # One output per slot: the second PSU no longer converges in pairs.
        x = work.duct_slot(p.x, -.448)
        entry = Vector((x, .177, 1.613))
        shoulder = Vector((p.x, .056, 1.620))
        points = work.cubic(p, (p.x, p.y, 1.627),
                            (p.x, .051, 1.624), shoulder, count=3)
        points += work.cubic(shoulder, (p.x, .066, 1.612),
                             (x, .138, 1.613), entry, count=4)[1:]
        points += work.cubic(entry, (x, .190, 1.607), (x, .191, 1.595),
                             (x, .191, 1.588), count=3)[1:]
        work.wire(obj, points, .0018, materials[n % 5])
        obj["terminal_designation"] = ("L", "N", "PE", "+V", "-V")[n % 5]
        work.add_cylinder("CAB_07_Wiring_PSUOutputFerrule",
                          (p.x, p.y, 1.639), .00215, .007,
                          ferrule, sides=6)
    for obj in work.wire_family("PLCChannel"):
        old = work.old_centers(obj, 5)
        p, end = old[0], old[-1]
        sign = 1 if end.z > p.z else -1
        shoulder = end.z-sign*.029
        entry = Vector((end.x, .175, shoulder))
        points = work.cubic(p, p+Vector((0, 0, sign*.023)),
                            (end.x, .114, shoulder-sign*.020), entry, count=6)
        points += work.cubic(entry, (end.x, .188, shoulder+sign*.010),
                             (end.x, .190, end.z-sign*.007),
                             (end.x, .190, end.z), count=4)[1:]
        work.wire(obj, points, .0013)


def pe_identification():
    yellow = bpy.data.materials["CAB / satin control yellow"].copy()
    yellow.name = "CAB / PE jacket yellow stripe"
    work.shader(yellow).inputs["Roughness"].default_value = .63
    targets = [work.wire_family("24VFeed")[i] for i in (2, 7)]
    targets += [work.wire_family("DriveOutput")[i] for i in (3, 7)]
    for obj in targets:
        sides = 6 if obj.get("wire_radius_m", .0032) >= .0023 else 5
        if yellow.name not in obj.data.materials:
            obj.data.materials.append(yellow)
        index = obj.data.materials.find(yellow.name)
        for face in obj.data.polygons:
            # Longitudinal extrusion stripe follows the tube's transported frame.
            value = sum(math.cos(math.tau*(v % sides)/sides) for v in face.vertices)
            face.material_index = index if value/len(face.vertices) > .20 else 0


def concealed(p, radius):
    return p.y-radius > .164 and p.y+radius < .203 and any(
        abs(p.x-x)+radius < width/2-.002 and
        abs(p.z-z)+radius < height/2-.003 for x, width, z, height in DUCTS)


def optimize_covered_rings(report):
    changed = {r["name"]: r for r in work.REPORT["routes"]}
    saved = 0
    for previous in report["routes"]:
        route = changed.get(previous["name"], previous)
        points = [Vector(p) for p in route["points"]]
        radius = route["radius_m"]
        before = len(points)
        # Only simplify triples entirely inside a covered horizontal channel.
        i = len(points)-2
        while i > 0:
            if all(concealed(p, radius) for p in points[i-1:i+2]):
                points.pop(i)
            i -= 1
        if len(points) != before:
            obj = SCENE.objects[route["name"]]
            work.wire(obj, points, radius)
            saved += (before-len(points))*(12 if radius >= .0023 else 10)
    return saved


def clear_cover_seams():
    obj = work.wire_family("TerminalUpper")[12]
    points = work.old_centers(obj, 5)
    # Keep the concealed routing boundary away from the real lid butt joint.
    points.append(points[-1]+Vector((-.010, 0, 0)))
    work.wire(obj, points, .0015)


def occlusion(report):
    vertices, faces, owners = [], [], []
    for obj in SCENE.objects:
        if obj.type != "MESH" or not obj.name.startswith("CAB_07_Wiring_"):
            continue
        if "Cover" not in obj.name and "RemovableLid" not in obj.name:
            continue
        offset = len(vertices)
        vertices.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
        obj.data.calc_loop_triangles()
        for tri in obj.data.loop_triangles:
            faces.append(tuple(offset+i for i in tri.vertices))
            owners.append(obj.name)
    tree = BVHTree.FromPolygons(vertices, faces, all_triangles=True)
    tested, failures = [], []
    for route in report["routes"]:
        p, radius = Vector(route["points"][-1]), route["radius_m"]
        if not concealed(p, radius):
            continue
        results = []
        # Probe the cap centre and perimeter from frontal and oblique views.
        for dx, dz in ((0, 0), (radius, 0), (-radius, 0),
                       (0, radius), (0, -radius)):
            start = p+Vector((dx, 0, dz))
            for sx, sz in ((0, 0), (-.7, .25), (.7, -.25), (0, .45), (0, -.45)):
                target = Vector((start.x+sx, -1.3, start.z+sz))
                delta = target-start
                hit = tree.ray_cast(start, delta.normalized(), delta.length)
                results.append(hit[0] is not None)
        tested.append(route["name"])
        if not all(results):
            failures.append({"wire": route["name"], "uncovered_rays": results.count(False)})
    return {"tested_end_caps": len(tested), "rays_per_cap": 25,
            "failures": failures, "pass": not failures,
            "scope": "Actual horizontal-duct end-cap centre/perimeter rays against "
                     "installed cover triangles at five frontal/oblique directions. "
                     "Does not certify arbitrary viewpoints or electrical continuity."}


def cameras():
    scene = bpy.data.scenes["04_INTERNAL"]
    for name, pos, target, lens in (
        ("Entry_Psu", (.48, -1.10, 1.59), (.285, .11, 1.57), 68),
        ("Entry_Drives", (.08, -.99, 1.29), (-.236, .11, 1.032), 62),
        ("Entry_Plc", (.61, -.78, 1.40), (.291, .11, 1.255), 67),
    ):
        data = bpy.data.cameras.new(name)
        data.lens, data.clip_start = lens, .02
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = pos
        obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()


def render(states, draft):
    field.render_device()
    for state in states:
        scene = bpy.data.scenes["04_INTERNAL"]
        bpy.context.window.scene = scene
        original = scene.camera
        scene.camera = scene.objects["Field_Internal" if state == "internal"
                                     else "Entry_"+state.capitalize()]
        scene.render.resolution_x, scene.render.resolution_y = 2400, 1600
        scene.render.resolution_percentage = 50 if draft else 100
        scene.cycles.samples = 24 if draft else 72
        scene.cycles.use_denoising = True
        scene.render.filepath = str(MODEL / "previews/electrical-cabinet" /
                                   ("duct-entry-"+("draft-" if draft else "")+state+".png"))
        bpy.ops.render.render(write_still=True)
        scene.camera = original
        print("ENTRY_RENDERED", scene.render.filepath, flush=True)


def inspect_saved():
    report = json.loads(REPORT.read_text())
    previous = json.loads((MODEL / "reports/electrical-cabinet-field-routing-review.json").read_text())
    old_routes = {r["name"]: r for r in previous["routes"]}
    routes = {r["name"]: r for r in report["routes"]}
    failures = []
    feeds = work.wire_family("24VFeed")
    slots = [routes[obj.name]["points"][-1][0] for obj in feeds]
    if len({round(x, 5) for x in slots}) != 10:
        failures.append("PSU output slots are not distinct")
    measured = []
    for obj in feeds+work.wire_family("PLCChannel"):
        actual = work.old_centers(obj, 5)[0]
        target = Vector(old_routes[obj.name]["points"][0])
        measured.append((actual-target).length)
    if max(measured) > .00001:
        failures.append("A preserved device seating position moved")
    result = {"file": bpy.data.filepath, "psu_distinct_slots": len(set(slots)),
              "preserved_device_seats": len(measured),
              "maximum_seating_change_m": max(measured),
              "end_cap_occlusion": occlusion(report), "failures": failures}
    result["pass"] = not failures and result["end_cap_occlusion"]["pass"]
    path = MODEL / "reports/electrical-cabinet-duct-entry-inspection.json"
    path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print("ENTRY_INSPECTED", json.dumps(result), flush=True)
    if not result["pass"]:
        raise RuntimeError("Saved source entry inspection failed")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--render-only", action="store_true")
    parser.add_argument("--check-only", action="store_true")
    parser.add_argument("--draft", action="store_true")
    parser.add_argument("--states", nargs="*", default=[])
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if args.check_only:
        inspect_saved()
        return
    if not args.render_only:
        if Path(bpy.data.filepath).name != "electrical-cabinet-field-routing-review.blend":
            raise RuntimeError("Use the preserved field-routing review source")
        bpy.context.window.scene = SCENE
        SCENE.frame_set(1)
        report = json.loads((MODEL / "reports/electrical-cabinet-field-routing-review.json").read_text())
        sections = covers()
        repair_feeds()
        saved = optimize_covered_rings(report)
        clear_cover_seams()
        pe_identification()
        cameras()
        bpy.context.view_layer.update()
        changed = {r["name"]: r for r in work.REPORT["routes"]}
        report["routes"] = [changed.get(r["name"], r) for r in report["routes"]]
        report["revision"] = "duct-entry"
        report["covered_horizontal_ducts"] = DUCTS
        report["previous_operations"] = report["operations"]
        report["operations"] = {
            "cover_sections_z_left_right_m": sections,
            "changed_route_count": len(changed),
            "concealed_ring_triangles_saved": saved,
            "power_outputs": "10 distinct slots, formed bends, seated ferrule collars; "
                             "straight terminal exits clear the PSU bottom sheet before "
                             "turning; L/N/PE/+V/-V colors match printed PSU terminals",
            "pe_jackets": "Longitudinal yellow stripe on PSU and drive PE conductors",
            "plc": "32 smooth lead-ins with original device seating coordinates",
            "preserved": "16 assemblies, housings, finishes, HMI and previous field distribution",
        }
        report["input"], report["output"] = bpy.data.filepath, str(OUTPUT)
        report["triangles"] = shared.measure()
        report["end_cap_occlusion"] = occlusion(report)
        report["status"] = "Awaiting visual review; no Unity integration"
        REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
        print("ENTRY_GEOMETRY", report["triangles"], report["end_cap_occlusion"], flush=True)
        if report["triangles"] > 110000 or not report["end_cap_occlusion"]["pass"]:
            raise RuntimeError("Source constraints failed; inspect report")
        bpy.context.preferences.filepaths.save_version = 0
        bpy.context.window.scene = SCENE
        bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT), check_existing=False)
        print("ENTRY_SAVED", str(OUTPUT), flush=True)
    if args.states:
        render(args.states, args.draft)


if __name__ == "__main__":
    main()
