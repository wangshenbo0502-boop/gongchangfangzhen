"""Revise the photographed terminal area with covered, continuous field routing."""
import argparse
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import refine_cabinet_workmanship as work
from industrial_parts import box
from pump_surface_finish import render_device

shared = work.previous
SCENE = shared.SCENE
MODEL = HERE.parent
OUTPUT = MODEL / "source/electrical-cabinet-field-routing-review.blend"
REPORT_PATH = MODEL / "reports/electrical-cabinet-field-routing-review.json"
CONNECTIONS = []


def block(name, pos, size, material, bevel=0):
    obj = box(name, pos, size, material, shared.group(SCENE, 7), bevel)
    return shared.share_new(obj, 7)


def covered_ducts():
    grey = bpy.data.materials["CAB / ferrule nylon"]
    # Match the installed duct polymer, rather than introducing another finish.
    existing = next(o for o in SCENE.objects
                    if o.name.startswith("CAB_07_Wiring_CrossDuct_RemovableLid"))
    white = existing.data.materials[0]
    base = next(o for o in SCENE.objects
                if o.name.startswith("CAB_07_Wiring_CrossDuct_Base"))
    grey = base.data.materials[0]
    for obj in list(SCENE.objects):
        if (obj.type == "MESH" and
                (("CrossDuct_RemovableLid" in obj.name and
                  abs((obj.matrix_world @ obj.data.vertices[0].co).z-.575) < .025)
                 or "NetworkDuct_RemovableLid" in obj.name)):
            old = obj.data
            for copy in list(bpy.data.objects):
                if copy.type == "MESH" and copy.data == old:
                    bpy.data.objects.remove(copy, do_unlink=True)
    # Two snap-on cover sections have a narrow, physical butt joint.
    for x, width in ((-.23775, .4645), (.22775, .4645)):
        block("CAB_07_Wiring_X1ControlCover", (x, .147, .575),
              (width, .005, .043), white, .00045)
    block("CAB_07_Wiring_SafetyControlCover", (.273, .147, .789),
          (.377, .005, .038), white, .00045)
    block("CAB_07_Wiring_FieldDuct_Base", (-.005, .209, .392),
          (.900, .008, .035), grey)
    for i in range(40):
        x = -.444+i*.022
        for sign in (-1, 1):
            breakouts = [-.3655+(n*5+(min(5, 24-n*5)-1)/2)*.029
                         for n in range(5)]
            if sign < 0 and any(abs(x-b) < .021 for b in breakouts):
                continue
            block("CAB_07_Wiring_FieldDuct_CombFinger",
                  (x, .182, .392+sign*.016),
                  (.014, .048, .003), grey)
    for x, width in ((-.23025, .4495), (.22025, .4495)):
        block("CAB_07_Wiring_FieldDuct_RemovableLid",
              (x, .147, .392), (width, .005, .038), white, .00045)


def revise():
    # The curves enter actual slots before disappearing under the snap-on lid.
    for n, obj in enumerate(work.wire_family("TerminalUpper")):
        old = work.old_centers(obj, 5)
        p = Vector((old[0].x, .138, .500))
        end = old[-1].copy()
        bend = .519+(n % 4)*.0015
        entry = Vector((end.x, .177, .545))
        points = work.piecewise([
            (p, (p.x, .138, bend), (end.x, .153, .530), entry),
            (entry, (end.x, .190, .553), (end.x, .190, .566),
             (end.x, .190, .575)),
        ], count=5)
        # The concealed straight insertion needs no redundant sweep ring.
        points = points[:-2]+points[-1:]
        work.wire(obj, points, .0015)
        CONNECTIONS.append({"wire": obj.name, "terminal": n+1, "end": "upper",
                            "seated_m": .005, "covered_duct_end": True})

    for prefix, sign, z in (("SafetyLoop", 1, .749),
                             ("SafetyReturn", -1, .623)):
        for n, obj in enumerate(work.wire_family(prefix)):
            old = work.old_centers(obj, 5)
            p = Vector((old[0].x, .111, z))
            end = old[-1].copy()
            end.y = .192
            entry_z = .763 if sign == 1 else .605
            entry = Vector((end.x, .174, entry_z))
            points = work.piecewise([
                (p, p+Vector((0, 0, sign*.012)),
                 (end.x, .145, entry_z-sign*.005), entry),
                (entry, (end.x, .192, entry_z+sign*.008),
                 (end.x, .192, end.z-sign*.006), end),
            ], count=7)
            work.wire(obj, points, .0015)
            CONNECTIONS.append({"wire": obj.name, "bank": n//3+1,
                                "pole": n % 3+1, "end": prefix,
                                "seated_m": .001, "covered_duct_end": True})

    # Cores distribute horizontally inside the field duct, not in exposed fans.
    for n, obj in enumerate(work.wire_family("FieldTermination")):
        old = work.old_centers(obj, 5)
        p = Vector((old[0].x, .138, .441))
        cable, core = divmod(n, 5)
        count = min(5, 24-cable*5)
        breakout = -.3655+(cable*5+(count-1)/2)*.029
        end = Vector((breakout+(core-(count-1)/2)*.0021,
                      .184+(.00125 if core % 2 else -.00125), .389))
        slot_x = work.duct_slot(p.x, -.433, .001 if n % 2 else -.001)
        # The two cores beside the vertical centre duct go around its sides.
        if n in (13, 14, 15, 16):
            slot_x = {13: .0055, 14: .0090, 15: .0935, 16: .0965}[n]
        entry = Vector((slot_x, .190, .413))
        visible = work.cubic(
            p, (p.x, .138, .422),
            (slot_x, .138 if n in (14, 15) else .190,
             .423+(n % 3)*.001), entry, count=6)
        hidden = [Vector((slot_x, .190, .400)),
                  Vector((end.x, end.y, .400)), end]
        work.wire(obj, visible+hidden, .00115)
        CONNECTIONS.append({"wire": obj.name, "terminal": n+1, "end": "lower",
                            "seated_m": .006, "jacket": cable,
                            "jacket_endpoint_m": list(end)})

    for n, obj in enumerate(work.wire_family("ShieldedFieldCable")):
        count = min(5, 24-n*5)
        breakout = -.3655+(n*5+(count-1)/2)*.029
        x = (-.34, -.17, 0, .17, .34)[n]
        p = Vector((breakout, .184, .396))
        middle = Vector((x, .168, .275))
        end = Vector((x, .055, .126))
        work.wire(obj, work.piecewise([
            (p, (breakout, .184, .355), (x, .168, .309), middle),
            (middle, (x, .168, .226), (x, .055, .188), end),
        ], count=5), .006)
    # These old exposed fan breakouts are now inside the new field duct.
    work.remove_family("CAB_07_Wiring_JacketBreakoutSleeve")
    black = bpy.data.materials["CAB / switch black thermoplastic"]
    for n in range(5):
        count = min(5, 24-n*5)
        x = -.3655+(n*5+(count-1)/2)*.029
        work.add_cylinder("CAB_07_Wiring_FieldDuctGrommet", (x, .184, .3785),
                          .0070, .006, black, sides=10)


def connectivity(report):
    routes = {r["name"]: r for r in report["routes"]}
    jackets = work.wire_family("ShieldedFieldCable")
    failures = []
    for c in CONNECTIONS:
        r = routes[c["wire"]]
        p = Vector(r["points"][0])
        if c["end"] in ("upper", "lower"):
            x = -.3655+(c["terminal"]-1)*.029
            target = Vector((x, .138, .500 if c["end"] == "upper" else .441))
            if (p-target).length > .00001:
                failures.append(c["wire"]+": terminal misalignment")
        if "jacket" in c:
            jacket = routes[jackets[c["jacket"]].name]
            q = Vector(r["points"][-1])
            axis = Vector(jacket["points"][0])
            radial = Vector((q.x-axis.x, q.y-axis.y, 0)).length
            if radial+r["radius_m"] > jacket["radius_m"] or not .375 < q.z < axis.z:
                failures.append(c["wire"]+": core not inserted into jacket")
    return {"changed_connections": len(CONNECTIONS), "failures": failures,
            "pass": not failures, "connections": CONNECTIONS,
            "scope": "Terminal entry coordinates and all 24 core-to-jacket insertions. "
                     "Covered duct endpoints are concealed routing boundaries, not "
                     "a complete electrical netlist or circuit certification."}


def cameras():
    scene = bpy.data.scenes["04_INTERNAL"]
    for name, pos, target, lens in (
        ("Field_Terminals", (.40, -1.12, .71), (-.025, .13, .455), 58),
        ("Field_Safety", (.50, -.68, .82), (.204, .12, .642), 60),
        ("Field_Internal", (.95, -3.90, 1.60), (0, .10, 1.07), 48),
    ):
        data = bpy.data.cameras.new(name)
        data.lens, data.clip_start = lens, .02
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = pos
        obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()


def render(states, draft):
    render_device()
    for state in states:
        scene = bpy.data.scenes["02_DOOR_OPEN" if state == "door-open" else "04_INTERNAL"]
        bpy.context.window.scene = scene
        original = scene.camera
        if state != "door-open":
            scene.camera = scene.objects["Field_"+state.capitalize()]
        scene.render.resolution_x, scene.render.resolution_y = 2400, 1600
        scene.render.resolution_percentage = 50 if draft else 100
        scene.cycles.samples = 24 if draft else 72
        scene.cycles.use_denoising = True
        scene.render.filepath = str(MODEL / "previews/electrical-cabinet" /
                                   ("field-routing-"+("draft-" if draft else "")+state+".png"))
        bpy.ops.render.render(write_still=True)
        scene.camera = original
        print("FIELD_RENDERED", scene.render.filepath, flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--render-only", action="store_true")
    parser.add_argument("--draft", action="store_true")
    parser.add_argument("--states", nargs="*", default=[])
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if not args.render_only:
        if Path(bpy.data.filepath).name != "electrical-cabinet-housing-review.blend":
            raise RuntimeError("Use the preserved housing review source")
        bpy.context.window.scene = SCENE
        SCENE.frame_set(1)
        report = json.loads((MODEL / "reports/electrical-cabinet-workmanship-review.json").read_text())
        covered_ducts()
        revise()
        cameras()
        bpy.context.view_layer.update()
        changed = {r["name"]: r for r in work.REPORT["routes"]}
        report["routes"] = [changed.get(r["name"], r) for r in report["routes"]]
        report["revision"] = "field-routing"
        report["previous_routing_checks"] = report.pop("routing_checks", {})
        report["previous_operations"] = report.pop("operations", {})
        report["operations"] = {
            "field_distribution": "24 cores distributed inside a new covered duct; "
                                  "five jacket exits have actual broken-out comb fingers.",
            "control_ducts": "Full snap-on covers on the safety and X1 control ducts.",
            "source_routes_changed": len(changed),
            "preserved": "16 assemblies, device housings, finishes and operator HMI.",
        }
        report["input"], report["output"] = bpy.data.filepath, str(OUTPUT)
        report["triangles"] = shared.measure()
        report["connectivity"] = connectivity(report)
        report["status"] = "Awaiting user visual review; no Unity integration"
        REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
        print("FIELD_GEOMETRY", report["triangles"], report["connectivity"]["failures"], flush=True)
        if report["triangles"] > 110000 or not report["connectivity"]["pass"]:
            raise RuntimeError("Source constraints failed")
        bpy.context.preferences.filepaths.save_version = 0
        bpy.context.window.scene = SCENE
        bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT), check_existing=False)
        print("FIELD_SAVED", str(OUTPUT), flush=True)
    if args.states:
        render(args.states, args.draft)


if __name__ == "__main__":
    main()
