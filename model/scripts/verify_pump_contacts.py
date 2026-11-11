"""Focused saved-mesh seating checks, not a full interference certification."""
import json
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

MODEL = Path(__file__).resolve().parent.parent
guard_only = "--guard-only" in sys.argv
scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
bpy.context.view_layer.update()
objects = [o for o in scene.objects if o.get("equipment_mesh")]
cache = {}


def geometry(obj):
    if obj.name not in cache:
        vertices = [obj.matrix_world @ v.co for v in obj.data.vertices]
        polygons = [tuple(p.vertices) for p in obj.data.polygons]
        cache[obj.name] = vertices, polygons, BVHTree.FromPolygons(vertices, polygons)
    return cache[obj.name]


def matching(prefix):
    return [o for o in objects if o.name == prefix or o.name.startswith(prefix+".")]


washer_rules = (
    ("PMP_00_Base_Anchor", "PMP_00_Base_AnchorPad", (0, 0, 1)),
    ("PMP_01_Motor_FootBolt", "PMP_01_Motor_Foot", (0, 0, 1)),
    ("PMP_01_Motor_LidScrew", "PMP_01_Motor_TerminalLid", (0, 0, 1)),
    ("PMP_04_Volute_HoldDown", "PMP_04_Volute_CastFoot", (0, 0, 1)),
    ("PMP_05_InletCover_Bolt_", "PMP_05_InletCover_ContouredCover", (-1, 0, 0)),
    ("PMP_06_SealBearing_GlandBolt", "PMP_06_SealBearing_SealChamber", (-1, 0, 0)),
)
washer_results = []
for prefix, support_prefix, axis_tuple in (() if guard_only else washer_rules):
    axis = Vector(axis_tuple)
    supports = matching(support_prefix)
    washers = [o for o in objects if o.name.startswith(prefix) and "_Washer" in o.name]
    for washer in washers:
        vertices, polygons, _ = geometry(washer)
        bottom = min(v.dot(axis) for v in vertices)
        probes = []
        for indices in polygons:
            points = [vertices[i] for i in indices]
            # Ignore bevels; evaluate only the flat annular bearing face.
            if all(abs(p.dot(axis)-bottom) < 2e-6 for p in points):
                probes.append(sum(points, Vector())/len(points))
        gaps = []
        for point in probes:
            hits = []
            for support in supports:
                hit, _, _, distance = geometry(support)[2].ray_cast(
                    point+axis*.003, -axis, .013)
                if hit is not None:
                    hits.append(distance-.003)
            gaps.append(min(hits) if hits else None)
        passed = bool(gaps) and all(g is not None and abs(g) <= .0002 for g in gaps)
        washer_results.append({
            "washer": washer.name, "support_prefix": support_prefix,
            "bearing_face_probes": len(probes),
            "gap_min_mm": min((g*1000 for g in gaps if g is not None), default=None),
            "gap_max_mm": max((g*1000 for g in gaps if g is not None), default=None),
            "missing_support_probes": sum(g is None for g in gaps),
            "pass": passed,
        })


def nearest_contact(a, b):
    va, _, ba = geometry(a)
    vb, _, bb = geometry(b)
    if ba.overlap(bb):
        return 0.0, "surfaces_touch_or_intersect"
    distances = [bb.find_nearest(p)[3] for p in va]
    distances.extend(ba.find_nearest(p)[3] for p in vb)
    return min(d for d in distances if d is not None), "nearest_vertex_to_surface"


contact_rules = (
    ("pump foot / crossmember", "PMP_04_Volute_CastFoot", "PMP_00_Base_Stiffener"),
    ("motor foot / shim", "PMP_01_Motor_Foot", "PMP_01_Motor_AlignmentShim"),
    ("motor shim / crossmember", "PMP_01_Motor_AlignmentShim", "PMP_00_Base_Stiffener"),
    ("bearing foot / shim", "PMP_06_SealBearing_SupportFoot", "PMP_06_SealBearing_BaseShim"),
    ("bearing shim / crossmember", "PMP_06_SealBearing_BaseShim", "PMP_00_Base_Stiffener"),
    ("pump ribs / foot", "PMP_04_Volute_FootRib", "PMP_04_Volute_CastFoot"),
    ("pump ribs / casing", "PMP_04_Volute_FootRib", "PMP_04_Volute_GrowingSpiral12mm"),
    ("motor webs / foot", "PMP_01_Motor_FootWeb", "PMP_01_Motor_Foot"),
    ("motor webs / housing", "PMP_01_Motor_FootWeb", "PMP_01_Motor_CastHousing"),
    ("bearing webs / foot", "PMP_06_SealBearing_PedestalWeb", "PMP_06_SealBearing_SupportFoot"),
    ("bearing webs / housing", "PMP_06_SealBearing_PedestalWeb", "PMP_06_SealBearing_BearingHousing"),
    ("gauge tapping boss / discharge", "PMP_04_Volute_InstrumentBoss", "PMP_04_Volute_GrowingSpiral12mm"),
    ("gauge union / tapping boss", "PMP_04_Volute_InstrumentUnion", "PMP_04_Volute_InstrumentBoss"),
    ("gauge tube / union", "PMP_04_Volute_InstrumentTube", "PMP_04_Volute_InstrumentUnion"),
    ("gauge tube / bottom union", "PMP_04_Volute_InstrumentTube", "PMP_04_Volute_GaugeUnion"),
    ("gauge stem / bottom union", "PMP_04_Volute_GaugeStem", "PMP_04_Volute_GaugeUnion"),
    ("gauge stem / case", "PMP_04_Volute_GaugeStem", "PMP_04_Volute_GaugeCase"),
    ("grease nipple / housing", "PMP_06_SealBearing_GreaseNipple", "PMP_06_SealBearing_BearingHousing"),
    ("motor nameplate / mounts", "PMP_01_Motor_Nameplate", "PMP_01_Motor_PlateMount"),
    ("pump nameplate / mounts", "PMP_04_Volute_DataPlate", "PMP_04_Volute_PlateMount"),
    ("guard mounting sill / bed", "PMP_03_Guard_MountingSill", "PMP_00_Base_MachinedBed"),
    ("guard post / mounting sill", "PMP_03_Guard_SupportPost", "PMP_03_Guard_MountingSill"),
    ("guard post / upper rail", "PMP_03_Guard_SupportPost", "PMP_03_Guard_BottomRail"),
)
contact_results = []
for description, child_prefix, support_prefix in contact_rules:
    if guard_only and not description.startswith("guard "):
        continue
    children, supports = matching(child_prefix), matching(support_prefix)
    if not children or not supports:
        contact_results.append({"joint": description, "pass": False, "reason": "missing geometry"})
    for child in children:
        results = [(nearest_contact(child, support), support.name) for support in supports]
        (distance, method), support_name = min(results, key=lambda r: r[0][0])
        contact_results.append({
            "joint": description, "object": child.name, "support": support_name,
            "surface_separation_mm": distance*1000, "method": method,
            "pass": distance <= .001,
        })

report = {
    "date": datetime.now().astimezone().isoformat(),
    "source": bpy.data.filepath, "scene": scene.name, "frame": scene.frame_current,
    "scope": ("Guard supports, mounting sills and their component ownership only." if guard_only else
              "Selected washer bearing faces and support/instrument joints only."),
    "limits": "Touch/intersection is accepted for modeled welded/cast joints; "
              "this does not certify full interference, thread engagement, welds or sealing.",
    "washer_tolerance_mm": .2, "joint_separation_tolerance_mm": 1,
    "washers": washer_results, "joints": contact_results,
    "guard_supports_follow_guard": (
        len(matching("PMP_03_Guard_MountingSill")) == 2
        and len(matching("PMP_03_Guard_SupportPost")) == 4
        and all(o.parent.name == "PMP_03_Guard" for o in
                matching("PMP_03_Guard_MountingSill")+matching("PMP_03_Guard_SupportPost"))
    ),
}
report["focused_contact_checks_pass"] = (
    len(washer_results) == (0 if guard_only else 32)
    and all(r["pass"] for r in washer_results+contact_results)
    and report["guard_supports_follow_guard"]
)
report_name = ("water-pump-guard-support-contact-verification.json" if guard_only else
               "water-pump-contact-verification.json")
(MODEL/"reports"/report_name).write_text(
    json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
failed = [r for r in washer_results+contact_results if not r["pass"]]
print("PUMP_CONTACT_CHECK", json.dumps({
    "washers": len(washer_results), "joints": len(contact_results),
    "pass": report["focused_contact_checks_pass"], "failed": failed,
}, ensure_ascii=False), flush=True)
if not report["focused_contact_checks_pass"]:
    raise RuntimeError("Focused assembly contact check failed; inspect report.")
