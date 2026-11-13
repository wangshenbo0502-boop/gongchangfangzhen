"""Build the reviewable 3 m vertical process tank and its Blender scenes."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from industrial_parts import (
    TAU, activate, bolt, box, cylinder, finish, flange, label, lathe, mesh,
    pipe, ring, surface_library, torus,
)
from pump_surface_finish import studio as finish_studio
from pump_uv import unwrap
from tank_components import COMPONENTS, animate_groups, manifest

MODEL = HERE.parent
OUT = MODEL / "source" / "silo.blend"
PREVIEW = MODEL / "previews" / "silo"
ASSETS = MODEL / "TankViewerProject" / "Assets" / "Tank"
Y = .30
GROUPS = []
MATS = {}


def log(text):
    print("TANK_DETAIL", text, flush=True)


def create_group(name):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.empty_display_size = .06
    GROUPS.append(obj)
    return obj


def part(group, short, center, size, mat, bevel=.002):
    return box(f"{group.name}_{short}", center, size, mat, group, bevel)


def bolt_circle(group, prefix, center, axis, radius, count, bolt_r=.012, length=.055):
    axis = Vector(axis).normalized()
    # Build a stable radial basis perpendicular to the nozzle axis.
    u = axis.cross(Vector((0, 0, 1)))
    if u.length < .1:
        u = axis.cross(Vector((0, 1, 0)))
    u.normalize()
    v = axis.cross(u).normalized()
    for i in range(count):
        theta = TAU*i/count
        p = Vector(center) + radius*(u*math.cos(theta)+v*math.sin(theta))
        bolt(f"{group.name}_{prefix}_{i+1:02d}", p, axis, MATS["steel"],
             group, bolt_r, length)


def nozzle(group, name, center, axis, outer, bore, neck_length, flange_r,
           flange_t, pcd, bolts=8):
    axis = Vector(axis).normalized()
    lathe(f"{group.name}_{name}_Neck", center,
          [(-neck_length/2, bore), (-neck_length/2, outer),
           (neck_length/2, outer), (neck_length/2, bore)],
          MATS["shell"], group, axis=axis, segments=64, bevel=.001)
    flange_center = Vector(center)+axis*(neck_length/2+flange_t/2)
    flange(f"{group.name}_{name}_Flange", flange_center, flange_r, bore,
           flange_t, pcd, bolts, .010, MATS["steel"], group, axis=axis)
    torus(f"{group.name}_{name}_WeldToe",
          Vector(center)-axis*(neck_length/2-.003), outer+.0025, .004,
          MATS["weld"], group, axis=axis, segments=64, sides=6)
    bolt_circle(group, name+"_Stud", flange_center+axis*(flange_t/2),
                axis, pcd, bolts, .010, .052)


def head(group, name, z_base, upward, radius=1.5, depth=.75, thickness=.008):
    # Closed meridian section gives both the 2:1 ellipse surface and its real
    # inner wall/edge return. The crown is a small manufacturable flat tangent.
    points = []
    for i in range(65):
        theta = (math.pi/2)*i/64
        r = radius*math.cos(theta)
        z = depth*math.sin(theta)
        points.append((z_base+upward*z, r))
    profile = [(points[i][0], points[i][1]) for i in range(len(points)-1)]
    for i in range(len(points)-1, -1, -1):
        z, r = points[i]
        inward = max(0, r-thickness)
        axial = z_base+upward*(z-z_base)
        profile.append((axial, inward))
    return lathe(name, (0, Y, 0), profile, MATS["shell"], group,
                 axis=(0, 0, 1), segments=128, bevel=0)


def legs():
    g = GROUPS[0]
    ring("TNK_00_Legs_TopRing", (0, Y, 1.54), 1.43, 1.37, .11,
         MATS["steel"], g, axis=(0, 0, 1), segments=96, bevel=.002)
    for i, (x, y) in enumerate(((-.86, Y-.86), (.86, Y-.86),
                                (-.86, Y+.86), (.86, Y+.86))):
        box(f"TNK_00_Legs_Post_{i+1:02d}", (x, y, .77), (.16, .16, 1.54),
            MATS["steel"], g, .008)
        box(f"TNK_00_Legs_BasePlate_{i+1:02d}", (x, y, .055),
            (.31, .31, .025), MATS["machined"], g, .003)
        for dx in (-.095, .095):
            for dy in (-.095, .095):
                bolt(f"TNK_00_Legs_Anchor_{i+1:02d}", (x+dx, y+dy, .07),
                     (0, 0, 1), MATS["steel"], g, .012, .115)
        for side in (-1, 1):
            brace = box(f"TNK_00_Legs_Gusset_{i+1:02d}", (x, y+side*.095, 1.28),
                        (.20, .025, .48), MATS["steel"], g, .003)
            brace.rotation_euler.y = math.radians(21)
    # Bonding strap is seated along the rear support, with visible lug washers.
    pipe("TNK_00_Legs_BondingStrap",
         [(-.86, Y+.96, .10), (-.86, Y+.96, .45), (-.86, Y+.96, 1.56),
          (-.70, Y+.96, 1.65)], .012, MATS["copper"], g, sides=8)
    for z in (.20, 1.40):
        bolt("TNK_00_Legs_BondLug", (-.86, Y+.96, z), (0, -1, 0),
             MATS["copper"], g, .009, .025)
    # Name used for assembly labels.
    part(g, "Tag", (-1.08, Y-.98, .31), (.30, .018, .12), MATS["dark"], .002)


def shell():
    g = GROUPS[1]
    ring("TNK_01_Shell_8mmWall", (0, Y, 2.80), 1.5, 1.492, 2.50,
         MATS["shell"], g, axis=(0, 0, 1), segments=160, bevel=0)
    # Three rolled courses and low-relief double-sided butt weld beads.
    for z in (2.05, 2.80, 3.55):
        torus("TNK_01_Shell_CourseSeam", (0, Y, z), 1.5015, .0007,
              MATS["weld"], g, axis=(0, 0, 1), segments=160, sides=6)
    for z in (2.42, 3.17):
        box("TNK_01_Shell_SeamBacking", (0, Y-1.497, z), (.075, .002, .022),
            MATS["shell"], g, .001)
    box("TNK_01_Shell_Nameplate", (-.52, Y-1.505, 2.75),
        (.34, .006, .18), MATS["machined"], g, .002)
    label("TNK_01_Shell_NameplateText", "TK-01  /  VERTICAL VESSEL",
          (-.675, Y-1.509, 2.76), .022, MATS["dark"], g)
    for x in (-.66, -.38):
        for z in (2.69, 2.81):
            cylinder("TNK_01_Shell_PlateRivet", (x, Y-1.512, z), .006,
                     .004, MATS["steel"], g, axis=(0, 1, 0), segments=16, bevel=.001)
    box("TNK_01_Shell_GroundLug", (.95, Y-1.50, 2.15),
        (.10, .018, .12), MATS["copper"], g, .004)


def heads():
    top, bottom = GROUPS[2], GROUPS[3]
    head(top, "TNK_02_TopHead_2to1Ellipsoidal", 4.05, 1)
    ring("TNK_02_TopHead_WeldNeck", (0, Y, 4.055), 1.50, 1.492, .035,
         MATS["weld"], top, axis=(0, 0, 1), segments=128, bevel=.001)
    head(bottom, "TNK_03_BottomHead_2to1Ellipsoidal", 1.55, -1)
    ring("TNK_03_BottomHead_SupportRing", (0, Y, 1.52), 1.48, 1.39, .09,
         MATS["steel"], bottom, axis=(0, 0, 1), segments=128, bevel=.002)
    nozzle(bottom, "DrainNozzle", (0, Y, .80), (0, 0, -1), .07, .052,
           .17, .14, .025, .105, 4)


def manway_neck():
    g = GROUPS[4]
    c = (-.30, Y-1.50, 3.24)
    axis = (0, -1, 0)
    nozzle(g, "NeckFlange", c, axis, .18, .15, .28, .28, .035, .235, 8)
    torus("TNK_04_Manway_ShellWeld", (-.30, Y-1.487, 3.24), .185,
          .005, MATS["weld"], g, axis=axis, segments=64, sides=8)
    for z in (3.15, 3.33):
        cylinder("TNK_04_Manway_HingeBarrel", (-.10, Y-1.82, z), .025,
                 .07, MATS["steel"], g, axis=(0, 0, 1), segments=24)


def instruments():
    g = GROUPS[5]
    # Magnetic level column parallel to the shell, connected at both ends.
    x, y = 1.23, Y-.84
    cylinder("TNK_05_Instruments_LevelColumn", (x, y, 2.74), .043, 1.12,
             MATS["steel"], g, axis=(0, 0, 1), segments=32)
    for z in (2.18, 3.30):
        pipe("TNK_05_Instruments_LevelTap",
             [(1.17, Y-1.05, z), (1.08, Y-1.05, z),
              (1.08, y, z), (x, y, z)], .018, MATS["steel"], g)
        ring("TNK_05_Instruments_LevelUnion", (x, y, z), .061, .043, .035,
             MATS["machined"], g, axis=(0, 0, 1), segments=32)
    # Scale strip remains fixed to the tube; float indicator sits inside a guard.
    box("TNK_05_Instruments_LevelScale", (1.279, y, 2.74),
        (.006, .025, 1.00), MATS["white"], g, .002)
    for i in range(21):
        z = 2.24+i*.05
        box("TNK_05_Instruments_ScaleTick", (1.285, y-.014, z),
            (.008, .012, .002 if i % 5 else .006), MATS["dark"], g, .0005)
    box("TNK_05_Instruments_JunctionBox", (1.34, Y-.88, 3.49),
        (.19, .10, .18), MATS["blue"], g, .006)
    for z in (3.44, 3.54):
        cylinder("TNK_05_Instruments_JunctionGland", (1.34, Y-.94, z),
                 .018, .04, MATS["steel"], g, axis=(0, 1, 0), segments=20)
    # Pressure instrument with a real pigtail/siphon loop and root valve.
    pipe("TNK_05_Instruments_Siphon",
         [(.22, Y-1.50, 3.68), (.22, Y-1.66, 3.68), (.22, Y-1.73, 3.76),
          (.22, Y-1.73, 3.91), (.22, Y-1.66, 3.99)], .012, MATS["steel"], g)
    cylinder("TNK_05_Instruments_PressureGaugeCase", (.22, Y-1.68, 4.045),
             .078, .045, MATS["machined"], g, axis=(0, -1, 0), segments=64)
    cylinder("TNK_05_Instruments_GaugeFace", (.22, Y-1.706, 4.045),
             .064, .004, MATS["white"], g, axis=(0, -1, 0), segments=64, bevel=0)
    pipe("TNK_05_Instruments_GaugeNeedle",
         [(.22, Y-1.711, 4.045), (.245, Y-1.712, 4.087)], .0025,
         MATS["red"], g, sides=6)
    cylinder("TNK_05_Instruments_GaugeRootValve", (.22, Y-1.51, 3.68),
             .04, .055, MATS["steel"], g, axis=(0, -1, 0), segments=32)


def ladder():
    g = GROUPS[6]
    x = -1.20
    for y in (Y+.72, Y+1.00):
        pipe("TNK_06_Ladder_Stringer",
             [(x, y, .28), (x, y, 3.80), (x, y-.05, 4.10)],
             .035, MATS["steel"], g, sides=12)
    for i in range(18):
        z = .42+i*.19
        cylinder("TNK_06_Ladder_AntiSlipRung", (x, Y+.86, z), .014, .28,
                 MATS["machined"], g, axis=(0, 1, 0), segments=20)
        for side in (-1, 1):
            box("TNK_06_Ladder_RungCollar", (x, Y+.86+side*.115, z),
                (.036, .018, .02), MATS["steel"], g, .002)
    # Cage hoops and longitudinal verticals start above the guarded lower section.
    for z in [1.88+i*.23 for i in range(10)]:
        torus("TNK_06_Ladder_CageHoop", (x, Y+.86, z), .46, .012,
              MATS["safety"], g, axis=(0, 0, 1), segments=64, sides=8)
    for i in range(8):
        a = TAU*i/8
        pipe("TNK_06_Ladder_CageUpright",
             [(x+.46*math.cos(a), Y+.86+.46*math.sin(a), 1.88),
              (x+.46*math.cos(a), Y+.86+.46*math.sin(a), 3.95)],
             .010, MATS["safety"], g)


def platform():
    g = GROUPS[7]
    z = 4.18
    # Deck segments frame around the vessel, with square drain openings.
    for x, width in ((-1.20, .55), (1.20, .55)):
        part(g, "GratingDeck", (x, Y+.72, z), (width, .56, .045), MATS["steel"])
    part(g, "GratingRear", (0, Y+1.20, z), (1.90, .36, .045), MATS["steel"])
    for x in (-1.44, -1.14, 1.14, 1.44):
        for y in (Y+.53, Y+.91):
            box("TNK_07_Platform_Post", (x, y, 4.63), (.035, .035, .88),
                MATS["safety"], g, .004)
    for zrail in (4.43, 4.80):
        pipe("TNK_07_Platform_GuardRail",
             [(-1.44, Y+.53, zrail), (-1.44, Y+1.25, zrail),
              (0, Y+1.25, zrail), (1.44, Y+1.25, zrail),
              (1.44, Y+.53, zrail)], .018, MATS["safety"], g)
    box("TNK_07_Platform_ToePlate", (0, Y+1.235, 4.24),
        (2.90, .025, .10), MATS["steel"], g, .003)
    for x in (-.35, .35):
        box("TNK_07_Platform_LadderGate", (x-1.2, Y+.54, 4.25),
            (.015, .38, .04), MATS["safety"], g, .002)


def inlet():
    g = GROUPS[8]
    axis = (0, -1, 0)
    nozzle(g, "Inlet", (.56, Y-1.48, 3.27), axis, .13, .105, .24,
           .23, .035, .19, 8)
    pipe("TNK_08_Inlet_ProcessPipe",
         [(.56, Y-1.60, 3.27), (.56, Y-1.78, 3.27),
          (.56, Y-2.05, 3.27), (.56, Y-2.25, 3.27)],
         .105, MATS["shell"], g, sides=48)
    # Pipe saddle physically touches vessel and pipe; the rest foot lands on leg frame.
    box("TNK_08_Inlet_PipeSaddle", (.56, Y-1.78, 3.13),
        (.20, .24, .035), MATS["steel"], g, .004)
    bolt("TNK_08_Inlet_SaddleBolt", (.48, Y-1.78, 3.15),
         (0, 0, 1), MATS["steel"], g, .010, .04)


def outlet():
    g = GROUPS[9]
    axis = (0, 0, -1)
    nozzle(g, "Outlet", (.90, Y-.95, 1.08), axis, .105, .08, .22,
           .19, .032, .15, 8)
    cylinder("TNK_09_Outlet_ValveBody", (.90, Y-.95, .72), .105, .26,
             MATS["steel"], g, axis=(0, 0, 1), segments=48, bevel=.006)
    for z in (.62, .82):
        torus("TNK_09_Outlet_BodyBonnetJoint", (.90, Y-.95, z), .105, .009,
              MATS["machined"], g, axis=(0, 0, 1), segments=48, sides=8)
    cylinder("TNK_09_Outlet_Stem", (.90, Y-.95, .91), .018, .19,
             MATS["machined"], g, axis=(0, 0, 1), segments=24)
    for a in range(5):
        theta = TAU*a/5
        pipe("TNK_09_Outlet_HandwheelSpoke",
             [(.90, Y-.95, 1.00),
              (.90+.105*math.cos(theta), Y-.95+.105*math.sin(theta), 1.00)],
             .008, MATS["steel"], g)
    torus("TNK_09_Outlet_Handwheel", (.90, Y-.95, 1.00), .105, .010,
          MATS["steel"], g, axis=(0, 0, 1), segments=48, sides=8)
    pipe("TNK_09_Outlet_DrainReturn",
         [(.90, Y-.95, .58), (.90, Y-1.25, .58), (.90, Y-1.25, .35)],
         .035, MATS["steel"], g)
    box("TNK_09_Outlet_PipeBracket", (.90, Y-1.13, .43),
        (.18, .20, .025), MATS["steel"], g, .003)


def cover():
    g = GROUPS[10]
    c = (-.30, Y-1.93, 3.24)
    flange(f"{g.name}_CoverPlate", c, .145, .012, .024, .235, 8, .010,
           MATS["steel"], g, axis=(0, 1, 0))
    cylinder(f"{g.name}_DishedCenter", c, .113, .010, MATS["shell"], g,
             axis=(0, 1, 0), segments=64)
    torus(f"{g.name}_Gasket", (-.30, Y-1.83, 3.24), .156, .008,
          MATS["rubber"], g, axis=(0, 1, 0), segments=64, sides=12)
    bolt_circle(g, "CoverStud", (c[0], c[1]-.014, c[2]), (0, -1, 0),
                .235, 8, .010, .055)
    # Hinge knuckles overlap their pins intentionally as a real captured joint.
    cylinder("TNK_10_ManwayCover_HingePin", (-.10, Y-1.82, 3.24),
             .012, .22, MATS["machined"], g, axis=(0, 0, 1), segments=24)
    for z in (3.18, 3.30):
        cylinder("TNK_10_ManwayCover_HingeLeaf", (-.10, Y-1.81, z),
                 .026, .042, MATS["steel"], g, axis=(0, 0, 1), segments=24)
    pipe("TNK_10_ManwayCover_Handle",
         [(-.40, Y-1.98, 3.29), (-.40, Y-2.03, 3.33),
          (-.20, Y-2.03, 3.33), (-.20, Y-1.98, 3.29)],
         .009, MATS["steel"], g)


def top_accessories():
    g = GROUPS[11]
    nozzle(g, "Vent", (.86, Y, 4.60), (0, 0, 1), .07, .052,
           .18, .14, .025, .105, 4)
    cylinder("TNK_11_TopAccessories_VentCap", (.86, Y, 4.74),
             .11, .028, MATS["steel"], g, axis=(0, 0, 1), segments=48)
    for i in range(8):
        a = TAU*i/8
        box("TNK_11_TopAccessories_VentSlot",
            (.86+.075*math.cos(a), Y+.075*math.sin(a), 4.758),
            (.032, .006, .004), MATS["dark"], g, .001)
    for x in (-.70, .70):
        torus("TNK_11_TopAccessories_LiftingLug",
              (x, Y, 4.55), .075, .012, MATS["steel"], g,
              axis=(1, 0, 0), segments=40, sides=8)
        cylinder("TNK_11_TopAccessories_LugFoot", (x, Y, 4.47),
                 .06, .025, MATS["steel"], g, axis=(0, 0, 1), segments=32)


def setup_materials():
    textures = surface_library(MODEL / "textures" / "silo")
    MATS.update({
        "shell": MATS.get("shell") or bpy.data.materials.get("TK Shell"),
    })
    # Create fresh names to avoid accidental dependency on unrelated scene data.
    from industrial_parts import material
    MATS.update({
        "shell": material("TK Shell", (.27, .32, .34), .86, .34, .000025, textures["cast"]),
        "steel": material("TK Stainless", (.42, .47, .49), .92, .24, .000012, textures["machined"]),
        "machined": material("TK Machined", (.50, .54, .55), .97, .18, .000006, textures["machined"]),
        "weld": material("TK Weld", (.31, .35, .36), .82, .39, .00003, textures["cast"]),
        "rubber": material("TK Gasket", (.045, .052, .052), .0, .86, .00004, textures["rubber"]),
        "dark": material("TK Marking", (.045, .055, .06), .12, .48),
        "white": material("TK GaugeFace", (.72, .76, .73), .0, .32),
        "red": material("TK Needle", (.68, .055, .025), .12, .34),
        "blue": material("TK JunctionBlue", (.12, .20, .24), .38, .4),
        "safety": material("TK SafetyYellow", (.82, .44, .035), .38, .36),
        "copper": material("TK BondingCopper", (.46, .19, .07), .82, .32),
    })


def create_scenes():
    assembled = bpy.context.scene
    assembled.name = "01_ASSEMBLED"
    assembled.frame_end = 320
    assembled.render.resolution_x, assembled.render.resolution_y = 2400, 1350
    assembled.render.resolution_percentage = 100
    assembled["model_status"] = "Engineering visualization; not pressure-vessel certification."
    assembled["nominal_shell_thickness_m"] = .008
    assembled["nominal_design_pressure_pa"] = 0
    assembled["exploded_view_note"] = "Welded heads are separated for inspection only; not a service disassembly procedure."
    # Camera and preview studio are scaled around the full five-metre vessel.
    floor_mat = bpy.data.materials.new("Tank Studio Floor")
    floor_mat.diffuse_color = (.105, .12, .13, 1)
    floor_mat.use_nodes = True
    floor_bsdf = floor_mat.node_tree.nodes.get("Principled BSDF")
    floor_bsdf.inputs["Base Color"].default_value = (.105, .12, .13, 1)
    floor_bsdf.inputs["Roughness"].default_value = .32
    floor_bsdf.inputs["Metallic"].default_value = .25
    finish_studio(assembled, floor_mat, box)
    cam_data = bpy.data.cameras.new("Tank Inspection Camera")
    cam = bpy.data.objects.new("Tank Inspection Camera", cam_data)
    assembled.collection.objects.link(cam)
    cam.location = (8.2, -11.7, 7.3)
    cam.data.lens = 55
    cam.rotation_euler = (Vector((0, .25, 2.55))-cam.location).to_track_quat("-Z", "Y").to_euler()
    assembled.camera = cam
    for obj in bpy.data.objects:
        if obj.type == "LIGHT":
            obj.location.z *= 1.55
            obj.data.energy *= 1.45
    animate_groups(assembled, GROUPS)
    for name, progress in (("02_EXPLODED", 1.0), ("03_WALL_DETAIL", .16)):
        scene = assembled.copy()
        scene.name = name
        bpy.context.window.scene = scene
        scene.frame_set(150 if progress == 1 else 25)
        if name == "03_WALL_DETAIL":
            scene["review_view"] = (
                "Wall-detail review scene. Heads remain visible because Blender object "
                "render visibility is shared by linked scenes."
            )
        scene["review_view"] = "Educational inspection presentation; welded pressure boundary not service-demountable."
    bpy.context.window.scene = assembled
    assembled.frame_set(1)


def render_previews():
    PREVIEW.mkdir(parents=True, exist_ok=True)
    assembled = bpy.data.scenes["01_ASSEMBLED"]
    exploded = bpy.data.scenes["02_EXPLODED"]
    camera = assembled.camera
    if not camera:
        raise RuntimeError("Studio camera missing; refusing to skip visual previews")

    shots = (
        (assembled, "01-assembled-front", (8.8, -13.0, 7.8), (0, .25, 2.55), 38),
        (assembled, "02-assembled-rear", (-9.5, 10.0, 7.4), (0, .30, 2.55), 38),
        (assembled, "03-assembled-side", (13.0, 1.0, 7.0), (0, .30, 2.55), 38),
        (exploded, "04-exploded-front", (9.8, -14.5, 8.0), (0, .25, 2.60), 36),
    )
    old_scene = bpy.context.window.scene
    old_location = camera.location.copy()
    old_rotation = camera.rotation_euler.copy()
    old_lens = camera.data.lens
    old_filepath = assembled.render.filepath
    try:
        for scene, filename, position, target, lens in shots:
            bpy.context.window.scene = scene
            scene.camera = camera
            camera.location = position
            camera.rotation_euler = (
                Vector(target)-camera.location
            ).to_track_quat("-Z", "Y").to_euler()
            camera.data.lens = lens
            scene.render.resolution_x = 1920
            scene.render.resolution_y = 1080
            scene.render.resolution_percentage = 100
            scene.cycles.samples = 24
            scene.render.image_settings.file_format = "PNG"
            scene.render.filepath = str(PREVIEW / filename)
            bpy.ops.render.render(write_still=True, scene=scene.name)
            log(f"Rendered {PREVIEW / (filename + '.png')}")
    finally:
        camera.location = old_location
        camera.rotation_euler = old_rotation
        camera.data.lens = old_lens
        assembled.render.filepath = old_filepath
        bpy.context.window.scene = old_scene


def write_geometry_report(meshes):
    from mathutils import Vector

    depsgraph = bpy.context.evaluated_depsgraph_get()
    equipment = [obj for obj in meshes if obj.name.startswith("TNK_")]
    points = [obj.evaluated_get(depsgraph).matrix_world @ Vector(corner)
              for obj in equipment for corner in obj.bound_box]
    mins = [min(point[i] for point in points) for i in range(3)]
    maxs = [max(point[i] for point in points) for i in range(3)]
    dimensions = [maxs[i]-mins[i] for i in range(3)]
    triangles = sum(len(poly.vertices)-2 for obj in equipment for poly in obj.data.polygons)
    lines = [
        "# 立式储罐模型构建实测",
        "",
        "- 状态：Blender 源模型与预览阶段；尚未导入 Unity。",
        f"- Blender：{bpy.app.version_string}",
        f"- 设备网格对象：{len(equipment)}（不含摄影棚地面）",
        f"- 组件组：{len(GROUPS)}",
        f"- 网格三角面估算：{triangles:,}",
        f"- 实测包围盒 X/Y/Z：{dimensions[0]:.4f} / {dimensions[1]:.4f} / {dimensions[2]:.4f} m",
        f"- 实测最小 Z：{mins[2]:.4f} m",
        "- 筒体名义壁厚：8 mm（脚本构造双层壳面；未做压力容器强度求解）。",
        "- 展开视图中的焊接封头分离仅为检查展示，不代表实际检修拆卸步骤。",
        "- Unity 组件接入、导航通行、故障演化、压力设计与规范认证：未执行。",
        "",
        "预览图：",
        "",
        *[f"- `previews/silo/{name}.png`" for name in (
            "01-assembled-front", "02-assembled-rear",
            "03-assembled-side", "04-exploded-front")],
    ]
    report = MODEL / "reports" / "silo.md"
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    log(f"Wrote {report}")


def export_assets():
    ASSETS.mkdir(parents=True, exist_ok=True)
    assembled = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.window.scene = assembled
    assembled.frame_set(1)
    meshes = [o for o in assembled.objects if o.type == "MESH" and o.name.startswith("TNK_")]
    result = manifest(GROUPS, meshes)
    result["meshCount"] = len(meshes)
    (ASSETS / "parts.json").write_text(json.dumps(result, ensure_ascii=False, indent=2),
                                        encoding="utf-8")
    mats = {}
    for mat in sorted({m for o in meshes for m in o.data.materials if m}, key=lambda m: m.name):
        bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if not bsdf:
            continue
        images = [n.image.name.lower() for n in mat.node_tree.nodes
                  if n.type == "TEX_IMAGE" and n.image]
        family = next((name for name in ("cast", "machined", "rubber")
                       if any(name+"_" in image for image in images)), "")
        rough = bsdf.inputs["Roughness"].default_value
        mats[mat.name] = {
            "name": mat.name,
            "color": list(mat.diffuse_color),
            "metallic": bsdf.inputs["Metallic"].default_value,
            "roughness": rough,
            "family": family,
            "relief": .00002,
        }
    (ASSETS / "materials.json").write_text(
        json.dumps({"materials": list(mats.values())}, ensure_ascii=False, indent=2),
        encoding="utf-8")
    bpy.ops.object.select_all(action="DESELECT")
    root = bpy.data.objects.get("silo")
    for obj in [root, *GROUPS, *meshes]:
        if obj:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=str(ASSETS / "silo.fbx"), use_selection=True,
        object_types={"EMPTY", "MESH"}, global_scale=1, apply_unit_scale=True,
        axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        apply_scale_options="FBX_SCALE_UNITS", use_mesh_modifiers=True,
        mesh_smooth_type="OFF", use_tspace=False, add_leaf_bones=False,
        bake_anim=False, path_mode="AUTO", use_custom_props=False)
    log(f"Exported tank FBX, {len(meshes)} meshes, {len(mats)} finishes")


def build():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                       bpy.data.cameras, bpy.data.lights, bpy.data.actions):
        for item in list(datablocks):
            if item.users == 0:
                datablocks.remove(item)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.context.scene.render.engine = "CYCLES"
    bpy.context.scene.cycles.samples = 64
    setup_materials()
    root = bpy.data.objects.new("silo", None)
    bpy.context.scene.collection.objects.link(root)
    root.empty_display_size = .1
    root["design_note"] = "Nominal 3 m diameter vertical process tank; illustrative, un-stamped model."
    for name, _, _ in COMPONENTS:
        group = create_group(name)
        group.parent = root
    legs()
    shell()
    heads()
    manway_neck()
    instruments()
    ladder()
    platform()
    inlet()
    outlet()
    cover()
    top_accessories()
    # Keep dimensions native: 3 m diameter x 5 m high, z base at floor.
    bpy.context.view_layer.update()
    roots = [root, *GROUPS]
    mesh_objects = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name.startswith("TNK_")]
    for obj in mesh_objects:
        obj["equipment_mesh"] = True
    unwrap(mesh_objects)
    create_scenes()
    render_previews()
    bpy.context.window.scene = bpy.data.scenes["01_ASSEMBLED"]
    export_assets()
    write_geometry_report(mesh_objects)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT), check_existing=False)
    log(f"Saved {OUT}")


if __name__ == "__main__":
    build()
