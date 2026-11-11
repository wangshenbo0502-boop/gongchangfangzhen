"""Native Blender pump: B/C/D geometry, source materials and review scenes."""
import json
import math
import shutil
import sys
from datetime import datetime
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from industrial_parts import (
    TAU, activate, apply_mod, bolt, boolean, box, cylinder, finish, flange,
    label, lathe, material, mesh, pipe, ring, surface_library, torus,
)
from pump_uv import unwrap
from pump_surface_finish import refine_surfaces, render_device, studio as finish_studio
from pump_components import GROUP_NAMES, OFFSETS, manifest, animate_groups

MODEL = HERE.parent
OUT = MODEL / "source" / "water-pump.blend"
PREVIEW = MODEL / "previews" / "water-pump"
REPORT = MODEL / "reports"
Z = .66


def log(message):
    print("PUMP_DETAIL", message, flush=True)


def empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.empty_display_size = .04
    return obj


def parent_at(obj, parent):
    bpy.context.view_layer.update()
    matrix = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = matrix


def extruded_profile(name, profile, center, width, mat, group, along_x=True):
    vertices = []
    for end in (-width/2, width/2):
        for a, b in profile:
            vertices.append((center[0]+end, center[1]+a, center[2]+b)
                            if along_x else
                            (center[0]+a, center[1]+end, center[2]+b))
    n = len(profile)
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces += [(i, (i+1) % n, (i+1) % n+n, i+n) for i in range(n)]
    return mesh(name, vertices, faces, mat, group, .001)


def base():
    g = groups[0]
    box("PMP_00_Base_Grout", (0, 0, .035), (1.90, 1, .07), concrete, g, .002)
    box("PMP_00_Base_MachinedBed", (0, 0, .092), (1.82, .88, .044), grey, g, .002)
    for y in (-.36, .36):
        extruded_profile("PMP_00_Base_LongitudinalChannel",
                         [(-.0425, -.034), (.0425, -.034), (.0425, -.028),
                          (-.0365, -.028), (-.0365, .028), (.0425, .028),
                          (.0425, .034), (-.0425, .034)],
                         (0, y, .148), 1.76, blue, g)
    for x in (-.515, -.17, .32, .70):
        top = .1955 if x < -.3 else .1975
        box("PMP_00_Base_Stiffener", (x, 0, (.114+top)/2),
            (.16, .80, top-.114), blue, g, .002)
    for x in (-.82, .82):
        for y in (-.36, .36):
            box("PMP_00_Base_AnchorPad", (x, y, .190), (.10, .13, .014), steel, g)
            bolt("PMP_00_Base_Anchor", (x, y, .197), (0, 0, 1), steel, g, .016, .11)
    box("PMP_00_Base_Tag", (-.45, -.445, .097), (.39, .002, .030), black, g, .0005)
    label("PMP_00_Base_TagText", "P-01 / CENTRIFUGAL PUMP", (-.63, -.4463, .089), .016, white, g)


def motor():
    g = groups[1]
    lathe("PMP_01_Motor_CastHousing", (.53, 0, Z),
          [(-.30, .045), (-.30, .185), (-.285, .211), (.235, .211),
           (.26, .192), (.26, .045)], blue, g)
    for i in range(32):
        theta = TAU*i/32
        fin = box(f"PMP_01_Motor_CoolingFin_{i+1:02}", (.51, .231*math.cos(theta), Z+.231*math.sin(theta)),
                  (.47, .044, .004), blue, g, .001)
        fin.rotation_euler.x = theta
    for x in (.235, .79):
        lathe("PMP_01_Motor_EndBell", (x, 0, Z),
              [(-.022, .045), (-.022, .173), (-.012, .222), (.01, .222),
               (.025, .17), (.025, .045)], grey, g)
    for y in (-.19, .19):
        box("PMP_01_Motor_Foot", (.51, y, .2295), (.49, .15, .050), blue, g, .002)
        for x in (.32, .70):
            sign = 1 if y > 0 else -1
            extruded_profile("PMP_01_Motor_FootWeb",
                             [(sign*a, b) for a, b in
                              ((.115, .2545), (.255, .2545), (.255, .35),
                               (.160, .55), (.095, .55))],
                             (x, 0, 0), .042, blue, g)
            box("PMP_01_Motor_AlignmentShim", (x, y, .201), (.10, .13, .007), steel, g, .0005)
            bolt("PMP_01_Motor_FootBolt", (x, y, .2545), (0, 0, 1), steel, g, .012, .07)
    # Separate pressed-steel fan hood and real open grille at the rear.
    lathe("PMP_01_Motor_RearFanHood", (.856, 0, Z),
          [(-.05, .227), (.045, .227), (.065, .206), (.065, .199),
           (.037, .219), (-.05, .219)], black, g, bevel=.001)
    for r in (.055, .09, .125, .16, .195):
        torus("PMP_01_Motor_RearGrilleRing", (.924, 0, Z), r, .002, steel, g, segments=32, sides=4)
    for i in range(12):
        t = TAU*i/12
        pipe("PMP_01_Motor_RearGrilleSpoke",
             [(.924, .035*math.cos(t), Z+.035*math.sin(t)),
              (.924, .202*math.cos(t), Z+.202*math.sin(t))], .002, steel, g)
    cylinder("PMP_01_Motor_RearGrilleHub", (.924, 0, Z), .035, .008, grey, g)
    box("PMP_01_Motor_TerminalMount", (.55, 0, .903), (.22, .15, .032), blue, g, .002)
    box("PMP_01_Motor_TerminalBox", (.55, 0, .967), (.245, .235, .095), blue, g, .002)
    box("PMP_01_Motor_LidGasket", (.55, 0, 1.017), (.248, .238, .005), rubber, g, .001)
    box("PMP_01_Motor_TerminalLid", (.55, 0, 1.030), (.26, .25, .020), grey, g, .002)
    for x in (.45, .65):
        for y in (-.095, .095):
            bolt("PMP_01_Motor_LidScrew", (x, y, 1.040), (0, 0, 1), steel, g, .006, .025)
    cylinder("PMP_01_Motor_CableGland", (.57, -.144, .963), .022, .055, steel, g, axis=(0, 1, 0), segments=24)
    pipe("PMP_01_Motor_Cable", [(.57, -.178, .963), (.69, -.25, .92),
                               (.78, -.28, .55), (.78, -.26, .24)], .012, rubber, g)
    torus("PMP_01_Motor_LiftingEye", (.34, 0, .961), .035, .008, steel, g, axis=(1, 0, 0), segments=40)
    cylinder("PMP_01_Motor_LiftingEyeStud", (.34, 0, .913), .011, .035, steel, g, axis=(0, 0, 1), segments=16)
    box("PMP_01_Motor_Nameplate", (.55, -.263, .70), (.33, .004, .115), steel, g)
    for j, text in enumerate(("MOTOR / IEC STYLE", "3~  |  TRAINING ASSET", "SI UNITS / NOT A RATING")):
        label("PMP_01_Motor_PlateText", text, (.398, -.2655, .731-j*.032), .019 if j == 0 else .013, black, g)
    for x in (.40, .70):
        for z in (.657, .743):
            cylinder("PMP_01_Motor_PlateRivet", (x, -.267, z), .003, .003, steel, g, axis=(0, 1, 0), segments=12, bevel=.0005)
    for x in (.405, .695):
        box("PMP_01_Motor_PlateMount", (x, -.244, .70),
            (.015, .035, .10), blue, g, .0005)


def coupling():
    g = groups[2]
    cylinder("PMP_02_Coupling_PumpShaft", (-.1645, 0, Z), .029, .489, steel, g)
    for x in (.025, .137):
        lathe("PMP_02_Coupling_MachinedHalf", (x, 0, Z),
              [(-.03, .03), (-.03, .057), (-.017, .057), (-.017, .093),
               (.020, .093), (.03, .073), (.03, .03)], steel, g)
    for i in range(6):
        t = TAU*i/6
        piece = box("PMP_02_Coupling_ElastomerJaw", (.082, .061*math.cos(t), Z+.061*math.sin(t)),
                    (.035, .035, .033), rubber, g)
        piece.rotation_euler.x = t


def guard_sheet(g, paint):
    # Perforated curved sheet: neighboring cells share their outer boundaries.
    verts, faces, indices = [], [], {}
    def point(u, v):
        x = -.06 + u*.25
        theta = math.pi*v
        co = (x, .169*math.cos(theta), Z+.169*math.sin(theta))
        key = tuple(round(a, 8) for a in co)
        if key not in indices:
            indices[key] = len(verts)
            verts.append(co)
        return indices[key]
    for i in range(8):
        for j in range(16):
            a, b = i/8, (i+1)/8
            c, d = j/16, (j+1)/16
            outer = [point(a, c), point(b, c), point(b, d), point(a, d)]
            inner = [point(a+.004/.25, c+.004/.53), point(b-.004/.25, c+.004/.53),
                     point(b-.004/.25, d-.004/.53), point(a+.004/.25, d-.004/.53)]
            for k in range(4):
                faces.append((outer[k], outer[(k+1)%4], inner[(k+1)%4], inner[k]))
    obj = mesh("PMP_03_Guard_Perforated1mmSheet", verts, faces, paint, g, 0)
    solid = obj.modifiers.new("True 1mm safety sheet", "SOLIDIFY")
    solid.thickness = .001
    apply_mod(obj, solid)
    return obj


def guard():
    g = groups[3]
    from repair_pump_clearances import guard_support_layout
    for name, center, size in guard_support_layout():
        box(name, center, size, grey, g, .001)
    guard_sheet(g, yellow)
    for x in (-.065, .195):
        points = [(x, .170*math.cos(t*math.pi/24), Z+.170*math.sin(t*math.pi/24)) for t in range(25)]
        pipe("PMP_03_Guard_RolledEdge", points, .004, yellow, g)
    for y in (-.17, .17):
        box("PMP_03_Guard_BottomRail", (.065, y, Z), (.27, .016, .016), yellow, g)
        for x in (-.045, .175):
            cylinder("PMP_03_Guard_Hinge", (x, y, Z-.006), .009, .032, steel, g, segments=24)
    pipe("PMP_03_Guard_Handle", [(.02, -.055, .827), (.02, -.055, .865),
                                (.15, -.055, .865), (.15, -.055, .827)], .006, black, g)
    box("PMP_03_Guard_WarningLabel", (.085, -.152, .739), (.092, .003, .040), black, g, .001)
    label("PMP_03_Guard_WarningText", "ROTATING", (.045, -.154, .736), .012, white, g)
    g["hinge_axis"] = "Blender X at (0.085, 0.170, 0.660)"


def volute_body(g, cast):
    # A growing spiral cross section, not a circular cylindrical placeholder.
    n = 112
    vertices, faces = [], []
    for i in range(n):
        u = i/n
        t = .10 + TAU*u
        growth = min(u/.84, 1)
        if u > .84:
            blend = (u-.84)/.16
            growth = 1-blend*blend*(3-2*blend)
        r = .294 + .065*growth
        cross = [
            (-.640, .2505), (-.640, r-.032), (-.630, r-.011), (-.608, r),
            (-.441, r), (-.410, r-.020), (-.397, .100), (-.385, .047),
            (-.397, .047), (-.409, .105), (-.422, r-.030), (-.445, r-.012),
            (-.603, r-.012), (-.621, r-.023), (-.628, r-.042), (-.628, .2505),
        ]
        for x, radial in cross:
            vertices.append((x, radial*math.cos(t), Z+radial*math.sin(t)))
    stride = len(cross)
    for i in range(n):
        for j in range(stride):
            faces.append((i*stride+j, i*stride+(j+1)%stride,
                          ((i+1)%n)*stride+(j+1)%stride, ((i+1)%n)*stride+j))
    body = mesh("PMP_04_Volute_GrowingSpiral12mm", vertices, faces, cast, g, 0)
    body["wall_nominal_m"] = .012
    body["scroll_growth_m"] = .065
    # The cast neck curls into the scroll; its lower cap is inside the cavity.
    # Swept bores follow the same centreline instead of leaving a hanging pipe end.
    sections = [
        (.620, .150, .069), (.655, .163, .080),
        (.690, .181, .092), (.725, .205, .104),
        (.760, .230, .113), (.800, .251, .115),
        (.845, .269, .109), (.890, .280, .102),
        (.940, .284, .096), (1.010, .284, .095),
        (1.336, .284, .095),
    ]

    def swept_neck(name, wall_offset, top):
        points = []
        for i in range(len(sections)-1):
            p1, p2 = Vector(sections[i]), Vector(sections[i+1])
            p0 = Vector(sections[max(0, i-1)])
            p3 = Vector(sections[min(len(sections)-1, i+2)])
            count = 4 if i < len(sections)-2 else 1
            for k in range(count):
                t = k/count
                # Hermite tangents account for unequal vertical sample spacing.
                m1 = (p2-p0)/(p2.x-p0.x)*(p2.x-p1.x)
                m2 = (p3-p1)/(p3.x-p1.x)*(p2.x-p1.x)
                points.append((2*t**3-3*t*t+1)*p1 +
                              (t**3-2*t*t+t)*m1 +
                              (-2*t**3+3*t*t)*p2 + (t**3-t*t)*m2)
        points.append(Vector((top, .284, .095)))
        verts, polys = [], []
        sides = 48
        for z, y, radius in points:
            for j in range(sides):
                angle = TAU*j/sides
                verts.append((-.518+(radius-wall_offset)*math.cos(angle),
                              y+(radius-wall_offset)*math.sin(angle), z))
        polys.append(tuple(reversed(range(sides))))
        for i in range(len(points)-1):
            for j in range(sides):
                a, b = i*sides+j, i*sides+(j+1)%sides
                polys.append((a, b, b+sides, a+sides))
        polys.append(tuple(range((len(points)-1)*sides, len(points)*sides)))
        return mesh(name, verts, polys, None, bevel=0)

    neck = swept_neck("PMP_04_Volute_CastOutletSweep", 0, 1.336)
    boolean(body, neck, "UNION")
    cutter = swept_neck("tool", .012, 1.37)
    boolean(body, cutter)
    finish(body, "PMP_04_Volute_GrowingSpiral12mm", cast, g, bevel=0)
    # Boolean intersections can leave sub-micron slivers with unstable UVs.
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=list(bm.edges))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    return body


def volute():
    g = groups[4]
    volute_body(g, cast)
    flange("PMP_04_Volute_DischargeFlange12mm", (-.518, .284, 1.344),
           .159, .083, .012, .128, 8, .0085, steel, g, axis=(0, 0, 1))
    ring("PMP_04_Volute_DischargeRaisedFace", (-.518, .284, 1.335),
         .110, .083, .005, steel, g, axis=(0, 0, 1), bevel=.0005)
    box("PMP_04_Volute_CastFoot", (-.515, 0, .218), (.34, .53, .045), cast, g, .002)
    for y in (-.19, .19):
        extruded_profile("PMP_04_Volute_FootRib",
                         [(-.145, .2405), (.145, .2405), (.075, .45), (-.075, .45)],
                         (-.515, y, 0), .035, cast, g, along_x=False)
        for x in (-.64, -.39):
            bolt("PMP_04_Volute_HoldDown", (x, y, .2405), (0, 0, 1), steel, g, .011, .06)
    cylinder("PMP_04_Volute_DrainBoss", (-.52, -.09, .316), .026, .038, cast, g, axis=(0, 0, 1), segments=32)
    cylinder("PMP_04_Volute_DrainPlug", (-.52, -.09, .290), .019, .020, steel, g, axis=(0, 0, 1), segments=6)
    ring("PMP_04_Volute_FrontRegister", (-.643, 0, Z), .283, .2515, .006, steel, g)
    # A visible tapped boss, compression union and bottom-entry gauge stem.
    cylinder("PMP_04_Volute_InstrumentBoss", (-.518, .190, 1.07),
             .021, .024, cast, g, axis=(0, 1, 0), segments=24)
    cylinder("PMP_04_Volute_InstrumentUnion", (-.518, .169, 1.07),
             .016, .024, steel, g, axis=(0, 1, 0), segments=6)
    pipe("PMP_04_Volute_InstrumentTube",
         [(-.518, .158, 1.07), (-.518, .06, 1.07),
          (-.518, -.065, 1.074), (-.518, -.065, 1.104)], .006, steel, g)
    cylinder("PMP_04_Volute_GaugeUnion", (-.518, -.065, 1.108),
             .0135, .022, steel, g, axis=(0, 0, 1), segments=6)
    cylinder("PMP_04_Volute_GaugeStem", (-.518, -.065, 1.124),
             .009, .022, steel, g, axis=(0, 0, 1), segments=24)
    cylinder("PMP_04_Volute_GaugeCase", (-.518, -.065, 1.176), .058, .040, steel, g, axis=(0, 1, 0))
    cylinder("PMP_04_Volute_GaugeDial", (-.518, -.088, 1.176), .051, .002, white, g, axis=(0, 1, 0), bevel=.0005)
    torus("PMP_04_Volute_GaugeBezel", (-.518, -.091, 1.176), .053, .004, steel, g, axis=(0, 1, 0), segments=48)
    for i in range(21):
        a = math.radians(225-270*i/20)
        inner = .040 if i % 5 == 0 else .044
        pipe("PMP_04_Volute_GaugeTick",
             [(-.518+r*math.cos(a), -.091, 1.176+r*math.sin(a)) for r in (inner, .048)],
             .00055, black, g)
    pipe("PMP_04_Volute_GaugeNeedle", [(-.518, -.093, 1.176), (-.538, -.093, 1.204)], .0012, red, g)
    label("PMP_04_Volute_GaugeUnits", "bar", (-.530, -.093, 1.154), .009, black, g)
    box("PMP_04_Volute_DataPlate", (-.515, -.357, .683), (.20, .003, .095), steel, g)
    for j, text in enumerate(("WP / 01", "END SUCTION", "MODEL / SI UNITS")):
        label("PMP_04_Volute_DataText", text, (-.606, -.359, .709-j*.026), .020 if j == 0 else .012, black, g)
    for x in (-.602, -.428):
        cylinder("PMP_04_Volute_PlateMount", (x, -.342, .683),
                 .008, .028, steel, g, axis=(0, 1, 0), segments=12, bevel=0)
        cylinder("PMP_04_Volute_PlateRivet", (x, -.360, .716),
                 .0025, .003, steel, g, axis=(0, 1, 0), segments=12, bevel=0)


def cover():
    g = groups[5]
    lathe("PMP_05_InletCover_ContouredCover", (0, 0, Z),
          [(-.681, .104), (-.681, .135), (-.675, .190),
           (-.675, .279), (-.671, .283), (-.652, .283),
           (-.6465, .245), (-.6465, .151), (-.658, .104)], cast, g, bevel=.001)
    ring("PMP_05_InletCover_RegisterORing", (-.6462, 0, Z), .246, .241, .0005, rubber, g, bevel=0)
    ring("PMP_05_InletCover_SuctionPipe", (-.752, 0, Z), .113, .100, .155, cast, g)
    flange("PMP_05_InletCover_SuctionFlange12mm", (-.839, 0, Z), .189, .100,
           .012, .158, 12, .008, steel, g)
    ring("PMP_05_InletCover_SuctionRaisedFace", (-.847, 0, Z), .131, .100, .004, steel, g, bevel=.0005)
    for i in range(12):
        t = TAU*(i+.5)/12
        y, z = .261*math.cos(t), Z+.261*math.sin(t)
        bolt(f"PMP_05_InletCover_Bolt_{i+1:02}", (-.675, y, z), (-1, 0, 0),
             steel, g, .010, .048)
    # Register grooves are actual annular machined steps; the gasket is separate.
    torus("PMP_05_InletCover_ORing", (-.647, 0, Z), .233, .002, rubber, g)


def seal_bearing():
    g = groups[6]
    lathe("PMP_06_SealBearing_SealChamber", (-.32, 0, Z),
          [(-.055, .039), (-.055, .133), (-.035, .143), (.035, .143),
           (.055, .105), (.055, .039)], cast, g)
    bearing_housing(g, blue)
    ring("PMP_06_SealBearing_StaticSealFace", (-.357, 0, Z), .074, .033, .014, graphite, g)
    ring("PMP_06_SealBearing_RotatingSealFace", (-.370, 0, Z), .069, .033, .011, ceramic, g)
    for x in (-.21, -.125):
        ring("PMP_06_SealBearing_BearingOuterRace", (x, 0, Z), .081, .067, .018, steel, g)
        ring("PMP_06_SealBearing_BearingInnerRace", (x, 0, Z), .049, .033, .018, steel, g)
        for i in range(10):
            t = TAU*i/10
            bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=.009,
                                                location=(x, .058*math.cos(t), Z+.058*math.sin(t)))
            finish(bpy.context.object, "PMP_06_SealBearing_Ball", steel, g, 0)
    box("PMP_06_SealBearing_SupportFoot", (-.17, 0, .228), (.20, .31, .041), blue, g)
    box("PMP_06_SealBearing_BaseShim", (-.17, 0, .2025),
        (.20, .31, .010), steel, g, .0005)
    for y in (-.08, .08):
        extruded_profile("PMP_06_SealBearing_PedestalWeb",
                         [(-.085, .2485), (.085, .2485), (.045, .61), (-.045, .61)],
                         (-.17, y, 0), .025, blue, g, along_x=False)
    cylinder("PMP_06_SealBearing_GreaseNipple", (-.17, 0, .792), .010, .038, steel, g, axis=(0, 0, 1), segments=12)
    for i in range(4):
        t = TAU*(i+.5)/4
        bolt("PMP_06_SealBearing_GlandBolt", (-.375, .113*math.cos(t), Z+.113*math.sin(t)),
             (-1, 0, 0), steel, g, .008, .045)


def bearing_housing(group, mat):
    return lathe("PMP_06_SealBearing_BearingHousing", (-.168, 0, Z),
                 [(-.090, .083), (-.090, .10), (-.060, .113), (.045, .113),
                  (.065, .080), (.065, .039), (.055, .039), (.055, .083)],
                 mat, group, bevel=0)


def impeller():
    g = groups[7]
    cylinder("water-pump_rotor", (-.513, 0, Z), .033, .20, steel, g)
    rotor = bpy.data.objects["water-pump_rotor"]
    # Keep a real local Z shaft axis without introducing object scale.
    world = rotor.matrix_world.copy()
    rot = Vector((1, 0, 0)).to_track_quat("Z", "Y").to_matrix().to_4x4()
    rotor.data.transform(rot.inverted())
    rotor.rotation_euler = rot.to_euler()
    cylinder("PMP_07_Impeller_Hub", (-.513, 0, Z), .069, .105, bronze, g)
    rear = flange("PMP_07_Impeller_BackShroud", (-.463, 0, Z), .249, .034,
                  .008, .085, 6, .007, bronze, g)
    front = lathe("PMP_07_Impeller_ConicalFrontShroud", (0, 0, Z),
                  [(-.562, .104), (-.559, .16), (-.551, .244), (-.543, .244),
                   (-.551, .16), (-.554, .104)], bronze, g, segments=96)
    for blade in range(6):
        vertices, faces = [], []
        for j in range(25):
            u = j/24
            r = .068+.176*u
            angle = TAU*blade/6 - .90*u
            thickness = .006*(1-u)+.003*u
            for x in (-.539, -.468):
                for side in (-1, 1):
                    a = angle+side*thickness/(2*r)
                    vertices.append((x, r*math.cos(a), Z+r*math.sin(a)))
        for j in range(24):
            a, b = j*4, (j+1)*4
            for p, q in ((0, 1), (1, 3), (3, 2), (2, 0)):
                faces.append((a+p, b+p, b+q, a+q))
        faces += [(0, 2, 3, 1), (96, 97, 99, 98)]
        mesh(f"PMP_07_Impeller_BackwardBlade_{blade+1:02}", vertices, faces, bronze, g, .0005)
    cylinder("PMP_07_Impeller_LockNut", (-.578, 0, Z), .039, .019, steel, g, segments=6)
    for child in list(g.children):
        if child != rotor:
            parent_at(child, rotor)
    # Front shroud is removable in the internal review scene, retained assembled.
    front["review_removable"] = True
    rotor["shaft_axis"] = "local Z = physical Blender X"
    rotor["stage"] = "D_SOURCE_PIVOT; Unity pivot replacement not validated"


def organize_components(scene):
    """Promote removable source parts without changing any assembled transform."""
    assignments = {
        8: ("PMP_01_Motor_TerminalLid", "PMP_01_Motor_LidGasket", "PMP_01_Motor_LidScrew"),
        9: ("PMP_07_Impeller_ConicalFrontShroud",),
        10: ("PMP_06_SealBearing_BearingHousing", "PMP_06_SealBearing_GreaseNipple"),
    }
    for obj in list(scene.objects):
        if not obj.get("equipment_mesh"):
            continue
        target = next((i for i, prefixes in assignments.items()
                       if obj.name.startswith(prefixes)), None)
        if obj.name.startswith(("PMP_06_SealBearing_BearingOuterRace",
                                "PMP_06_SealBearing_BearingInnerRace",
                                "PMP_06_SealBearing_Ball")):
            target = 11
        if obj.name.startswith("PMP_01_Motor_FootBolt_HexNut"):
            target = 0
        if obj.name.startswith("PMP_05_InletCover_Bolt_") and "_HexNut" in obj.name:
            target = 4
        if target is not None:
            parent_at(obj, groups[target])
    meshes = [o for o in scene.objects if o.get("equipment_mesh")]
    data = manifest(groups, meshes)
    root["component_contract"] = json.dumps(data, ensure_ascii=False)
    (REPORT/"water-pump-components.json").write_text(
        json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")


def measured(scene):
    objects = [o for o in scene.objects if o.type == "MESH" and o.get("equipment_mesh")]
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    points, issues, triangles = [], [], 0
    for obj in objects:
        evaluated = obj.evaluated_get(deps)
        data = evaluated.to_mesh()
        points.extend(obj.matrix_world @ v.co for v in data.vertices)
        data.calc_loop_triangles()
        triangles += len(data.loop_triangles)
        bm = bmesh.new()
        bm.from_mesh(data)
        edges = sum(not e.is_manifold for e in bm.edges)
        volume = bm.calc_volume(signed=True)
        if edges or volume <= 0:
            issues.append({"object": obj.name, "non_manifold_edges": edges,
                           "signed_volume_m3": volume})
        bm.free()
        evaluated.to_mesh_clear()
    low = Vector([min(p[i] for p in points) for i in range(3)])
    high = Vector([max(p[i] for p in points) for i in range(3)])
    size = high-low
    rotor = bpy.data.objects["water-pump_rotor"]
    axis = rotor.matrix_world.to_3x3() @ Vector((0, 0, 1))
    def owner(obj):
        while obj.parent:
            if obj.parent in groups:
                return obj.parent.name
            obj = obj.parent
        return None
    checks = {
        "G1": abs(low.z) < .005 and abs((low.x+high.x)/2) < .005
              and abs((low.y+high.y)/2) < .005,
        "G2": all((o.scale-Vector((1, 1, 1))).length < 1e-5 for o in objects+groups),
        "G3": all(abs(a/b-1) <= .03 for a, b in zip(size, (1.90, 1, 1.35))),
        "G4": sorted(o.name for o in root.children) == sorted(GROUP_NAMES),
        "G5": not any(i["non_manifold_edges"] for i in issues),
        "G6": not any(i["signed_volume_m3"] <= 0 for i in issues),
        "G7": triangles <= 120000, "G8": uv_result["pass"],
        "G9": (rotor.matrix_world.translation-Vector((-.513, 0, Z))).length < .002
              and abs(axis.normalized().dot(Vector((1, 0, 0)))) > .999,
        "G10": rotor.type == "MESH",
        "G11": all(owner(o) for o in objects),
        "G12": max(high.y, -low.y) <= 1.2,
    }
    return {
        "date": datetime.now().astimezone().isoformat(), "blender": bpy.app.version_string,
        "stages": "Source review; isolated Unity viewer; main workshop integration pending",
        "mesh_count": len(objects), "triangles_evaluated": triangles,
        "min_xyz_m": list(low), "max_xyz_m": list(high), "width_depth_height_m": list(size),
        "groups": [{"name": g.name, "meshes": sum(owner(o) == g.name for o in objects)} for g in groups],
        "geometry_issues": issues, "checks": checks,
        "source_pivot_axis_world": list(axis),
        "uv": uv_result,
    }


def write_report(data):
    (REPORT/"water-pump-detailed.json").write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    rows = [
        "# 离心泵精细建模阶段报告", "", f"生成：{data['date']}；Blender {data['blender']}。",
        "", "状态：B/C/D 源模型已生成供视觉审核；E 为初步材质。不是全部规范验收完成。",
        "本次用户在草模后要求继续细化；保留 A 阶段备份，先只做离心泵。",
        "", "## 文件与观察", "",
        "- `../source/water-pump.blend`：整机、拆开、内部观察三个独立 Scene；默认打开整机。",
        "- Scene 下拉菜单：`01_ASSEMBLED` / `02_EXPLODED` / `03_INTERNAL`。",
        "- 整机 Scene 的 1–320 帧为拆装动画：1 帧整机、150–170 帧拆开、320 帧复原。",
        "- `../scripts/pump_review_ui.py` 提供本次 Blender 会话中的观察侧栏，不依赖它也可播放已保存动画。",
        "- `../scripts/build_pump_detailed.py`：可重复建模，依赖同目录 `industrial_parts.py`、`pump_uv.py` 与 `pump_surface_finish.py`。",
        "- `../previews/water-pump/realistic-*.png`：本轮 2400×1350 整机、拆解、内部近景；旧 detailed 图保留。",
        "- `../textures/water-pump/physical-v2/`：三类五通道纹理库（2048/1024/512），已打包到 blend。",
        "- 米制铸造微表面、车削纹、镀锌紧固件、磨削轴和缎面铭牌分别设材质。",
        "- Cycles 预览配合 Blender 内置 CC0 studio.exr（Greg Zaal），环境贴图已打包。",
        "", "## 实际几何", "",
        "| 项目 | 测量 |", "| --- | ---: |",
        f"| 宽 × 深 × 高 m | {' × '.join(f'{v:.6f}' for v in data['width_depth_height_m'])} |",
        f"| 整机网格对象 | {data['mesh_count']} |",
        f"| 修改器求值后三角面 | {data['triangles_evaluated']:,} / 120,000 |",
        "", "| 组 | 实际网格数 |", "| --- | ---: |",
    ]
    rows += [f"| {g['name']} | {g['meshes']} |" for g in data["groups"]]
    rows += ["", "## 源几何自检 G1–G12", "", "| 项目 | 结果 |", "| --- | --- |"]
    rows += [f"| {k} | {'未验证 / E 阶段 UV 拉伸待检' if v is None else 'PASS' if v else 'FAIL，详见 JSON'} |" for k, v in data["checks"].items()]
    rows += [
        "", "G5/G6 是逐对象封闭性与有符号体积检查，不是装配碰撞、流体密封或应力验证。",
        "非流形/体积问题清单保存在 detailed JSON；有问题不得宣称几何全部合格。",
        "", "## 已制作的结构", "",
        "- 渐增截面蜗壳、约 12 mm 壁厚、内腔、切向排出口与实际通孔法兰。",
        "- 竖直出口外径由 154 mm 调整为 190 mm，内径 166 mm，壁厚 12 mm；顶部法兰外径 318 mm。",
        "- 压力表增加取压凸台、六角接头、引压管、底部表接头；润滑嘴随轴承外壳拆开。",
        "- 校正前盖螺栓承压平面、底座横梁、脚座垫片及铭牌安装座。",
        "- 电机 32 条实体散热筋、端盖、后风罩与通透格栅、脚座、吊环、接线盒、格兰头、电缆及铭牌。",
        "- 六角螺栓、实体环形垫圈、螺杆和螺母；底座四处锚固及电机垫片。",
        "- 1 mm 厚曲面冲孔护罩、卷边、把手、铰链；联轴器两半及六块弹性块。",
        "- 前盖止口、独立 O 圈、吸入口和 12 颗前盖螺栓；工艺法兰保留空孔等待配管。",
        "- 密封动/静环、轴承内外圈与滚珠、支架及润滑嘴。",
        "- 六片后弯叶片、前后盖板、轮毂、锁紧螺母及平衡孔；内部 Scene 移开前盖板供查看。",
        "", "## 与规范的取舍及未验证项", "",
        "1. 泵各轴系沿 Blender X 同轴；规范前盖/叶轮的 −Unity Z 与电机 +X 拆解相冲突。",
        "   观察 Scene 使用轴向拉开，并加大电机间距避免联轴器重叠，非逐字复现原位移量；",
        "   当前源动画和独立 Unity 展示器共享 pump_components.py 定义的部件与位移。",
        "2. 转子保留源本地 Z 对准物理 X 的旋转。现有 Unity 生成器重建 forward 枢轴问题仍待接入时处理。",
        f"3. 当前 {len(GROUP_NAMES)} 个总成在三个 Scene 和 Unity 一一对应；螺栓、铭牌随所属部件移动。",
        "4. 材质使用 Blender Principled，漆面/铸件/橡胶已接入 UV 贴图与程序微表面；五通道库已打包。",
        f"   UV 实测见 JSON 的 uv 字段（G8={'PASS' if data['checks']['G8'] else 'FAIL'}）；独立展示器采用 URP PBR 材质。",
        "5. 铭牌及压力表是教学模型标记，不是实机厂家铭牌、测量或求解数据。",
        "6. 轴承等子件允许独立封闭网格；本轮尚未做全装配干涉、铸造工艺、CFD 或有限元校验。",
        "7. F 故障形态、主车间接入与导航未完成；独立 EXE 验证见 Evidence/pump-viewer-20261111.md。",
        "8. 尚未完成与现有小车的并排材质/细节密度验收，不声称达到相同水准。",
        "9. Assets、原有小车、场景和另一工程未修改；其他三类模型尚未开始。",
        "",
    ]
    (REPORT/"water-pump.md").write_text("\n".join(rows), encoding="utf-8")


def camera(scene, name, position, target, scale):
    data = bpy.data.cameras.new(name)
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()
    data.type = "ORTHO"
    data.ortho_scale = scale
    data.lens = 58
    obj.hide_set(True)
    scene.camera = obj
    return obj


def studio(scene):
    finish_studio(scene, floor_mat, box)


def duplicate_scene(source, name, offsets):
    scene = bpy.data.scenes.new(name)
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    bpy.context.window.scene = scene
    copies = {}
    for original in [o for o in source.objects if o == root or o in groups or o.get("equipment_mesh")]:
        copy = original.copy()
        scene.collection.objects.link(copy)
        copy.matrix_world = original.matrix_world.copy()
        copies[original] = copy
    for original, copy in copies.items():
        copy.parent = copies.get(original.parent)
        copy.matrix_parent_inverse = original.matrix_parent_inverse.copy()
        copy.matrix_basis = original.matrix_basis.copy()
    for group, offset in zip(groups, offsets):
        copies[group].location += Vector(offset)
    scene["review_state"] = name
    studio(scene)
    return scene, copies


def configure_viewports():
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.shading.type = "MATERIAL"
                    space.shading.use_scene_world = True
                    space.shading.use_scene_lights = True
                    space.overlay.show_relationship_lines = False
                    space.overlay.show_extras = False
                    space.region_3d.view_location = (0, 0, .60)
                    space.region_3d.view_rotation = Vector((-3, -4, 2.8)).to_track_quat("Z", "Y")
                    space.region_3d.view_distance = 3.1
                    space.clip_end = 500
                if space.type == "OUTLINER":
                    space.use_filter_object_empty = True
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = None


def build():
    global root, groups, blue, grey, cast, steel, black, rubber, bronze
    global yellow, concrete, white, red, graphite, ceramic, floor_mat
    global uv_result
    for folder in (OUT.parent, PREVIEW, REPORT):
        folder.mkdir(parents=True, exist_ok=True)
    if OUT.exists():
        backup = OUT.with_name("water-pump-before-detail-" + datetime.now().strftime("%Y%m%d-%H%M%S") + ".blend")
        shutil.copy2(OUT, backup)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.name = "01_ASSEMBLED"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    log("building shared texture library")
    library = surface_library(MODEL/"textures"/"water-pump"/"physical-v2")
    blue = material("Paint / blue grey", (.050, .102, .135), 0, .50, .00010, library["cast"])
    grey = material("Paint / industrial grey", (.156, .175, .188), 0, .53, .00007, library["cast"])
    cast = material("Sand cast / blue grey", (.050, .102, .135), .03, .71, .00032, library["cast"])
    steel = material("Machined steel", (.48, .51, .55), 1, .26, .00003, library["machined"])
    black = material("Powder coat / graphite", (.025, .030, .034), 0, .51, .00008)
    rubber = material("EPDM rubber", (.014, .017, .020), 0, .91, .00012, library["rubber"])
    yellow = material("Safety yellow enamel", (.807, .445, .012), 0, .48, .00005, library["cast"])
    bronze = material("Impeller / machined bronze", (.42, .29, .12), 1, .32, .00003)
    concrete = material("Clean cement grout", (.19, .20, .21), 0, .88, .0005)
    white = material("Dial / ceramic white", (.77, .79, .77), 0, .48)
    red = material("Gauge needle", (.50, .024, .015), 0, .42)
    graphite = material("Seal / carbon graphite", (.045, .048, .05), .05, .28)
    ceramic = material("Seal / silicon carbide", (.25, .27, .29), .3, .18)
    floor_mat = material("Studio neutral", (.24, .25, .26), 0, .73)
    root = empty("water-pump")
    root["stages"] = "B/C/D source geometry, E preliminary"
    groups = [empty(name, root) for name in GROUP_NAMES]
    for action in (base, motor, coupling, guard, volute, cover, seal_bearing, impeller):
        log("building "+action.__name__)
        action()
    bpy.context.view_layer.update()
    refine_surfaces(scene)
    for obj in list(scene.objects):
        if obj.type == "MESH":
            obj["equipment_mesh"] = True
    # Preserve every child's world transform while giving groups useful anchors.
    organize_components(scene)
    from repair_pump_clearances import apply_clearances
    apply_clearances(scene, root, groups, generated=True)
    pivots = [(0, 0, 0), (.53, 0, Z), (.08, 0, Z), (.08, .17, Z),
              (-.51, 0, Z), (-.66, 0, Z), (-.17, 0, Z), (-.513, 0, Z),
              (.55, 0, 1.03), (-.55, 0, Z), (-.168, 0, Z), (-.17, 0, Z)]
    for group, pivot in zip(groups, pivots):
        bpy.context.view_layer.update()
        children = [(o, o.matrix_world.copy()) for o in group.children]
        group.location = pivot
        bpy.context.view_layer.update()
        for obj, matrix in children:
            obj.matrix_world = matrix
    bpy.context.view_layer.update()
    log("unwrapping and measuring source UVs")
    uv_result = unwrap([o for o in scene.objects if o.get("equipment_mesh")])
    log("measuring assembled geometry")
    data = measured(scene)
    write_report(data)
    log(json.dumps({"triangles": data["triangles_evaluated"], "checks": data["checks"]}))
    studio(scene)
    assembled_camera = camera(scene, "Assembled camera", (-3.5, -4.4, 2.6), (0, 0, .64), 3.05)
    camera(scene, "Surface detail camera", (-3.5, -4.4, 2.6), (-.55, -.025, .92), 1.15)
    scene.camera = assembled_camera
    exploded, copies = duplicate_scene(scene, "02_EXPLODED", OFFSETS)
    camera(exploded, "Exploded camera", (-4.5, -6.2, 3.2), (.12, 0, .74), 4.65)
    internal, icopies = duplicate_scene(scene, "03_INTERNAL", OFFSETS)
    camera(internal, "Flow passage camera", (-4.2, -3.7, 2.8), (-.77, .06, .73), 2.25)
    # Native keyframes work without installing an add-on or enabling scripts.
    bpy.context.window.scene = scene
    scene.frame_start, scene.frame_end = 1, 320
    animate_groups(scene, groups)
    for frame, name in ((1, "ASSEMBLED"), (45, "GUARD OPEN"),
                        (150, "EXPLODED"), (170, "REASSEMBLY"), (320, "ASSEMBLED")):
        scene.timeline_markers.new(name, frame=frame)
    scene.frame_set(1)
    for frame, scale in ((1, 3.05), (70, 4.65), (250, 4.65), (320, 3.05)):
        scene.camera.data.ortho_scale = scale
        scene.camera.data.keyframe_insert("ortho_scale", frame=frame)
    scene.frame_set(1)
    # Lights and floor remain in source scenes only; there is no FBX export here.
    bpy.context.window.scene = scene
    configure_viewports()
    render_device()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT), check_existing=False)
    log("BUILD_SAVED "+str(OUT))


def render():
    for name, filename in (
        ("01_ASSEMBLED", "realistic-assembled.png"),
        ("02_EXPLODED", "realistic-exploded.png"),
        ("03_INTERNAL", "realistic-internal.png"),
    ):
        scene = bpy.data.scenes[name]
        bpy.context.window.scene = scene
        scene.render.filepath = str(PREVIEW/filename)
        bpy.ops.render.render(write_still=True)
        log("rendered "+filename)
    scene = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.window.scene = scene
    original_camera = scene.camera
    try:
        scene.camera = bpy.data.objects["Surface detail camera"]
        scene.render.filepath = str(PREVIEW/"realistic-surface-detail.png")
        bpy.ops.render.render(write_still=True)
    finally:
        scene.camera = original_camera
    log("RENDER_COMPLETE")


if __name__ == "__main__":
    if "--render-only" in sys.argv:
        render()
    else:
        build()
        if "--render" in sys.argv:
            render()
