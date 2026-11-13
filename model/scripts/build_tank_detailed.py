"""Build the 5.86 m tank review; keep Unity exports explicitly opt-in."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from industrial_parts import (
    TAU, activate, axis_matrix, boolean, bolt, box, cylinder, finish, flange,
    label, lathe, mesh, pipe, ring, surface_library, torus,
)
from pump_surface_finish import studio as finish_studio, finish_material
from pump_uv import unwrap
from tank_components import COMPONENTS, animate_groups, manifest

MODEL = HERE.parent
OUT = MODEL / "source" / "silo.blend"
PREVIEW = MODEL / "previews" / "silo"
ASSETS = MODEL / "TankViewerProject" / "Assets" / "Tank"
Y = .30
SHELL_BOTTOM = 1.55
SHELL_TOP = 5.10
SHELL_HEIGHT = SHELL_TOP - SHELL_BOTTOM
DECK_Z = 4.70
MANWAY_X, MANWAY_Z = -.53, 2.50
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
    basis = axis_matrix(axis)
    for i in range(count):
        theta = TAU*(i+.37)/count
        p = Vector(center) + basis @ Vector(
            (radius*math.cos(theta), radius*math.sin(theta), 0))
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
    return flange_center


def shell_front(x):
    return Y-math.sqrt(1.5**2-x**2)


def straight(group, name, start, end, radius, mat=None, segments=20, bore=0):
    start, end = Vector(start), Vector(end)
    if bore:
        return ring(f"{group.name}_{name}", (start+end)/2, radius,
                    bore, (end-start).length, mat or MATS["steel"], group,
                    axis=end-start, segments=segments, bevel=.0004)
    return cylinder(f"{group.name}_{name}", (start+end)/2, radius,
                    (end-start).length, mat or MATS["steel"], group,
                    axis=end-start, segments=segments, bevel=.0006)


def routed_tube(group, name, points, radius, mat, bend=.035):
    # Round each polyline corner inside its two segments, without AUTO overshoot.
    points = [Vector(p) for p in points]
    samples = [points[0]]
    for a, b, c in zip(points, points[1:], points[2:]):
        before, after = a-b, c-b
        trim = min(bend, before.length*.30, after.length*.30)
        if trim < 1e-6:
            samples.append(b)
            continue
        entry, exit = b+before.normalized()*trim, b+after.normalized()*trim
        for i in range(7):
            t = i/6
            samples.append((1-t)**2*entry+2*(1-t)*t*b+t*t*exit)
    samples.append(points[-1])
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    curve.use_fill_caps = True
    spline = curve.splines.new("POLY")
    spline.points.add(len(samples)-1)
    for point, co in zip(spline.points, samples):
        point.co = (*co, 1)
    obj = bpy.data.objects.new(f"{group.name}_{name}", curve)
    bpy.context.scene.collection.objects.link(obj)
    activate(obj)
    bpy.ops.object.convert(target="MESH")
    return finish(bpy.context.object, obj.name, mat, group, 0)


def resize_local_parts(objects, anchor, factor):
    """Resize a local fitting about its wall connection, keeping unit transforms."""
    anchor = Vector(anchor)
    for obj in objects:
        obj.location = anchor+(obj.location-anchor)*factor
        obj.data.transform(Matrix.Scale(factor, 4))
        obj.data.update()


def conduit_clip(group, name, x, z, vertical):
    wall_y = shell_front(x)
    axis = (0, 0, 1) if vertical else (1, x/(Y-wall_y), 0)
    ring(f"{group.name}_{name}_Saddle", (x, wall_y-.045, z),
         .015, .0108, .018, MATS["steel"], group,
         axis=axis, segments=24, bevel=.0004)
    box(f"{group.name}_{name}_Foot", (x, wall_y-.017, z),
        (.052 if vertical else .036, .028, .036 if vertical else .052),
        MATS["steel"], group, .001)
    for sign in (-1, 1):
        dx, dz = (sign*.022, 0) if vertical else (0, sign*.022)
        cylinder(f"{group.name}_{name}_Screw", (x+dx, wall_y-.033, z+dz),
                 .004, .006, MATS["machined"], group,
                 axis=(0, -1, 0), segments=12, bevel=.0004)


def restore_shell_normals():
    # Boolean triangulation must not change the analytic rolled-shell shading.
    obj = bpy.data.objects["TNK_01_Shell_8mmWall"]
    data = obj.data
    normals = [(0, 0, 0)]*len(data.loops)
    for face in data.polygons:
        radial = Vector((face.center.x, face.center.y-Y, 0)).normalized()
        alignment = face.normal.dot(radial)
        cylindrical = abs(alignment) > .90 and abs(face.normal.z) < .25
        face.use_smooth = cylindrical
        for index in face.loop_indices:
            point = data.vertices[data.loops[index].vertex_index].co
            if cylindrical:
                normal = Vector((point.x, point.y-Y, 0)).normalized()
                if alignment < 0:
                    normal.negate()
            else:
                normal = face.normal
            normals[index] = tuple(normal)
    data.normals_split_custom_set(normals)


def saddle_pad(group, name, x, z, outer, inner=0, thickness=.008):
    # Both faces follow the cylindrical wall instead of floating on a tangent.
    segments = 64
    vertices = []
    for offset, radius in ((.002, outer), (.002, inner),
                           (.002+thickness, outer), (.002+thickness, inner)):
        for i in range(segments):
            a = TAU*i/segments
            px = x+radius*math.cos(a)
            vertices.append((px, shell_front(px)-offset, z+radius*math.sin(a)))
    faces = []
    for i in range(segments):
        j = (i+1) % segments
        for a, b in ((0, 1), (1, 3), (3, 2), (2, 0)):
            faces.append((a*segments+i, a*segments+j,
                          b*segments+j, b*segments+i))
    return mesh(f"{group.name}_{name}", vertices, faces, MATS["shell"],
                group, bevel=0)


def wall_patch(group, name, x, z, width, height, thickness=.008,
               offset=.002, material=None):
    # Closed shallow plate with both skins conforming to the rolled shell.
    nx, nz = 24, 10
    vertices = []
    for layer_offset in (offset, offset+thickness):
        for iz in range(nz+1):
            pz = z-height/2+height*iz/nz
            for ix in range(nx+1):
                px = x-width/2+width*ix/nx
                vertices.append((px, shell_front(px)-layer_offset, pz))
    row = nx+1
    layer_size = (nx+1)*(nz+1)
    faces = []
    for iz in range(nz):
        for ix in range(nx):
            a = iz*row+ix
            quad = (a, a+1, a+row+1, a+row)
            faces.append(quad)
            faces.append(tuple(index+layer_size for index in reversed(quad)))
    for ix in range(nx):
        a, b = ix, ix+1
        faces.append((a, b, b+layer_size, a+layer_size))
        a, b = nz*row+ix, nz*row+ix+1
        faces.append((a, a+layer_size, b+layer_size, b))
    for iz in range(nz):
        a, b = iz*row, (iz+1)*row
        faces.append((a, a+layer_size, b+layer_size, b))
        a, b = iz*row+nx, (iz+1)*row+nx
        faces.append((a, b, b+layer_size, a+layer_size))
    return mesh(f"{group.name}_{name}", vertices, faces,
                material or MATS["shell"], group, bevel=0)


def wall_label(group, name, text, center, size, mat, offset=.014):
    obj = label(name, text, center, size, mat, group)
    inverse = obj.matrix_world.inverted()
    for vertex in obj.data.vertices:
        point = obj.matrix_world @ vertex.co
        point.y += shell_front(point.x)-shell_front(center[0])
        vertex.co = inverse @ point
    obj.data.update()
    return obj


def wall_nozzle(group, name, x, z, outer, bore, flange_r, pcd,
                reach=.20, count=8):
    wall_y = shell_front(x)
    end_y = wall_y-reach
    start_y = max(shell_front(x-outer), shell_front(x+outer))+.025
    neck = ring(f"{group.name}_{name}_Neck",
                (x, (start_y+end_y)/2, z), outer, bore, start_y-end_y,
                MATS["shell"], group, axis=(0, -1, 0), segments=64, bevel=0)
    cutter = cylinder("tool_inner_vessel", (0, Y, 3), 1.492, 8,
                      None, axis=(0, 0, 1), segments=128, bevel=0)
    boolean(neck, cutter)
    shell_obj = bpy.data.objects["TNK_01_Shell_8mmWall"]
    cutter = cylinder("tool_process_opening", (x, wall_y, z),
                      bore, 1.2, None, axis=(0, -1, 0), segments=64, bevel=0)
    boolean(shell_obj, cutter)
    saddle_pad(group, name+"_Reinforcement", x, z, outer+.065, outer)
    weld_points = []
    for i in range(65):
        a = TAU*i/64
        px = x+(outer+.004)*math.cos(a)
        weld_points.append((px, shell_front(px)-.010,
                            z+(outer+.004)*math.sin(a)))
    pipe(f"{group.name}_{name}_SaddleWeld", weld_points, .002,
         MATS["weld"], group)
    f = Vector((x, end_y-.0175, z))
    flange(f"{group.name}_{name}_Flange", f, flange_r, bore, .035,
           pcd, count, .009, MATS["machined"], group, axis=(0, -1, 0))
    return f


def blind_plate(group, name, center, radius, pcd, count, thickness=.025,
                axis=(0, -1, 0)):
    plate = cylinder(f"{group.name}_{name}", center, radius, thickness,
                     MATS["steel"], group, axis=axis, segments=64, bevel=0)
    basis = axis_matrix(axis)
    for i in range(count):
        a = TAU*(i+.37)/count
        p = Vector(center)+basis @ Vector((pcd*math.cos(a), pcd*math.sin(a), 0))
        cutter = cylinder("tool_bolt", p, .009, thickness*4, None,
                          axis=axis, segments=12, bevel=0)
        boolean(plate, cutter)
    return finish(plate, plate.name, MATS["steel"], group, .0007)


def head(group, name, z_base, upward, radius=1.5, depth=.75, thickness=.008):
    # Closed meridian section gives both the 2:1 ellipse surface and its real
    # inner wall/edge return. The crown is a small manufacturable flat tangent.
    points = []
    for i in range(33):
        theta = (math.pi/2)*i/32
        r = radius*math.cos(theta)
        z = depth*math.sin(theta)
        points.append((z_base+upward*z, r))
    profile = [(points[i][0], points[i][1]) for i in range(len(points)-1)]
    for i in range(len(points)-1, -1, -1):
        z, r = points[i]
        inward = max(0, r-thickness)
        profile.append((z, inward))
    return lathe(name, (0, Y, 0), profile, MATS["shell"], group,
                 axis=(0, 0, 1), segments=64, bevel=0)


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
                     (0, 0, 1), MATS["steel"], g, .012, .075)
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


def shell():
    g = GROUPS[1]
    ring("TNK_01_Shell_8mmWall", (0, Y, (SHELL_BOTTOM+SHELL_TOP)/2),
         1.5, 1.492, SHELL_HEIGHT,
         MATS["shell"], g, axis=(0, 0, 1), segments=64, bevel=0)
    # Three rolled courses and low-relief double-sided butt weld beads.
    for z in (2.72, 3.91):
        torus("TNK_01_Shell_CourseSeam", (0, Y, z), 1.5015, .0007,
              MATS["weld"], g, axis=(0, 0, 1), segments=64, sides=6)
    plate_x, plate_z = -.78, 3.35
    plate_width, plate_height, plate_offset = .60, .28, .010
    wall_patch(g, "Nameplate", plate_x, plate_z, plate_width, plate_height,
               thickness=.006, offset=.002, material=MATS["machined"])
    for i, text in enumerate(("TK-01  PROCESS WATER", "DIA 3000  /  WALL 8 mm",
                              "VIZ DEMO  /  2026-11")):
        wall_label(g, f"TNK_01_Shell_NameplateText_{i}", text,
                   (plate_x-.26, shell_front(plate_x-.26)-plate_offset-.001,
                    plate_z+.073-i*.067),
                   .028, MATS["dark"], offset=plate_offset+.001)
    for x in (plate_x-.265, plate_x+.265):
        for z in (plate_z-.11, plate_z+.11):
            cylinder("TNK_01_Shell_PlateRivet",
                     (x, shell_front(x)-.014, z), .006, .008,
                     MATS["steel"], g, axis=(0, -1, 0), segments=16, bevel=.001)
    ground_x, ground_z = -.86, 1.70
    ground_y = shell_front(ground_x)
    saddle_pad(g, "GroundingPad", ground_x, ground_z, .065)
    box("TNK_01_Shell_GroundLug", (ground_x, ground_y-.018, ground_z),
        (.055, .018, .085), MATS["copper"], g, .002)
    bolt("TNK_01_Shell_GroundStud", (ground_x, ground_y-.029, ground_z),
         (0, -1, 0), MATS["copper"], g, .009, .025)
    pipe("TNK_01_Shell_BondToLeg",
         [(ground_x, ground_y-.035, ground_z),
          (ground_x, -.62, 1.51), (ground_x, -.57, 1.25)],
         .008, MATS["copper"], g)
    bolt("TNK_01_Shell_LegBondStud", (-.86, -.65, 1.25), (0, -1, 0),
         MATS["copper"], g, .009, .025)


def heads():
    top, bottom = GROUPS[2], GROUPS[3]
    head(top, "TNK_02_TopHead_2to1Ellipsoidal", SHELL_TOP, 1)
    ring("TNK_02_TopHead_WeldNeck", (0, Y, SHELL_TOP+.005), 1.50, 1.492, .035,
         MATS["weld"], top, axis=(0, 0, 1), segments=64, bevel=.001)
    head(bottom, "TNK_03_BottomHead_2to1Ellipsoidal", SHELL_BOTTOM, -1)
    ring("TNK_03_BottomHead_SupportRing", (0, Y, 1.52), 1.48, 1.39, .09,
         MATS["steel"], bottom, axis=(0, 0, 1), segments=64, bevel=.002)
    nozzle(bottom, "DrainNozzle", (0, Y, .80), (0, 0, -1), .07, .052,
           .17, .14, .025, .105, 4)


def manway_neck():
    g = GROUPS[4]
    f = wall_nozzle(g, "NeckFlange", MANWAY_X, MANWAY_Z, .240, .225,
                    .325, .280, reach=.19, count=12)
    g["mating_flange_center"] = list(f)
    g["clear_opening_diameter_m"] = .450
    hinge_x = MANWAY_X-.365
    hinge_y = f.y+.020
    for z in (MANWAY_Z-.14, MANWAY_Z+.14):
        box("TNK_04_Manway_HingeBracket", (hinge_x+.060, hinge_y+.025, z),
            (.16, .060, .035), MATS["steel"], g, .002)
        cylinder("TNK_04_Manway_HingeBarrel", (hinge_x, hinge_y, z),
                 .024, .070, MATS["steel"], g, axis=(0, 0, 1), segments=24)


def glass_thermometer(g):
    """Protected 400 x 72 mm local indicator with a back-entry thermowell."""
    previous_objects = set(bpy.data.objects)
    fitting_scale = 2.0
    x, z = .14, 2.20
    wall_y = shell_front(x)
    y = wall_y-.145
    prefix = "TNK_05_Instruments_TGI01_"
    pad = saddle_pad(g, "TGI01_Pad", x, z, .041*fitting_scale,
                     .011*fitting_scale, .006*fitting_scale)
    shell_obj = bpy.data.objects["TNK_01_Shell_8mmWall"]
    cutter = cylinder("tool_thermowell_opening", (x, wall_y, z),
                      .011*fitting_scale, .12, None,
                      axis=(0, 1, 0), segments=32, bevel=0)
    boolean(shell_obj, cutter)
    ring(prefix+"Thermowell", (x, (y+wall_y+.14)/2, z),
         .010, .0065, wall_y+.14-y, MATS["machined"], g,
         axis=(0, 1, 0), segments=32, bevel=.0004)
    cylinder(prefix+"ClosedTip", (x, wall_y+.14, z),
             .010, .003, MATS["machined"], g,
             axis=(0, 1, 0), segments=32, bevel=.001)
    cylinder(prefix+"HexUnion", (x, wall_y-.030, z),
             .023, .025, MATS["machined"], g,
             axis=(0, 1, 0), segments=6, bevel=.0005)
    ring(prefix+"MountingShoulder", (x, wall_y-.011, z),
         .025, .010, .010, MATS["steel"], g,
         axis=(0, 1, 0), segments=32, bevel=.0004)
    cylinder(prefix+"ElbowSocket", (x, y, z+.012),
             .016, .040, MATS["steel"], g,
             axis=(0, 0, 1), segments=32, bevel=.001)
    cylinder(prefix+"CaseCollar", (x, y, z+.039),
             .013, .023, MATS["machined"], g,
             axis=(0, 0, 1), segments=32, bevel=.0007)

    bottom, top = z+.048, z+.248
    mid = (bottom+top)/2
    # Recessed open-front housing: the capillary sits between protective lips.
    box(prefix+"CaseBack", (x, y+.006, mid), (.036, .010, .200),
        MATS["dark"], g, .001)
    for sign in (-1, 1):
        box(prefix+f"CaseLip_{sign}", (x+sign*.016, y-.004, mid),
            (.004, .018, .200), MATS["steel"], g, .0007)
        box(prefix+f"ScaleCheek_{sign}", (x+sign*.009, y-.003, mid),
            (.011, .004, .188), MATS["thermometer_scale"], g, .0005)
    for name, height in (("Top", top), ("Bottom", bottom)):
        box(prefix+"Case"+name, (x, y-.002, height),
            (.036, .022, .006), MATS["steel"], g, .0007)
    scale_bottom, scale_top = bottom+.018, top-.025
    reading_c, range_c = 24.8, 60.0
    column_top = scale_bottom+(scale_top-scale_bottom)*reading_c/range_c
    glass_y = y-.009
    ring(prefix+"GlassCapillary", (x, glass_y, (bottom+top)/2),
         .0032, .00085, .187, MATS["thermometer_glass"], g,
         axis=(0, 0, 1), segments=40, bevel=0)
    cylinder(prefix+"SealedGlassTip", (x, glass_y, top-.006),
             .0032, .0015, MATS["thermometer_glass"], g,
             axis=(0, 0, 1), segments=32, bevel=.0003)
    column = cylinder(prefix+"SpiritColumn",
                      (x, glass_y, (bottom+.004+column_top)/2),
                      .0008, column_top-bottom-.004,
                      MATS["gauge_liquid"], g, axis=(0, 0, 1),
                      segments=24, bevel=0)
    column["indicated_c"] = reading_c
    column["scale_zero_z"] = scale_bottom
    column["scale_full_z"] = scale_top
    column["full_scale_c"] = range_c
    # The reservoir is inside the closed thermowell, not exposed to room air.
    cylinder(prefix+"InternalReservoir", (x, wall_y+.106, z),
             .003, .050, MATS["gauge_liquid"], g,
             axis=(0, 1, 0), segments=24, bevel=.001)
    routed_tube(g, "TGI01_InternalLiquidLink",
                [(x, wall_y+.081, z), (x, glass_y, z),
                 (x, glass_y, bottom+.005)], .0008, MATS["gauge_liquid"],
                bend=.006)
    for index in range(61):
        height = scale_bottom+(scale_top-scale_bottom)*index/range_c
        major = index % 10 == 0
        medium = index % 5 == 0
        length = .005 if major else (.0035 if medium else .002)
        box(prefix+f"Tick_{index:02d}",
            (x-.0035-length/2, y-.0053, height),
            (length, .00035, .00045), MATS["dark"], g, 0)
        if major:
            label(prefix+f"Value_{index:02d}", str(index),
                  (x+.0044, y-.0055, height-.0015),
                  .0034, MATS["dark"], g)
    label(prefix+"Unit", "C", (x-.0015, y-.0055, top-.017),
          .0045, MATS["dark"], g)
    label(prefix+"Tag", "TI-01", (x-.009, y-.0055, bottom+.0045),
          .003, MATS["dark"], g)
    resize_local_parts(
        [obj for obj in set(bpy.data.objects)-previous_objects if obj != pad],
        (x, wall_y, z), fitting_scale)
    column["scale_zero_z"] = z+(scale_bottom-z)*fitting_scale
    column["scale_full_z"] = z+(scale_top-z)*fitting_scale
    g["local_thermometer_case_scale"] = fitting_scale
    g["local_thermometer_reference"] = (
        "Generic 400 x 72 mm protected machine glass thermometer layout "
        "with a 90-degree back-entry thermowell; no certified product selection."
    )
    g["local_thermometer_static_reading_c"] = reading_c


def instruments():
    g = GROUPS[5]
    level_x = .91
    level_z0, level_z1 = 1.98, 4.38
    level_y = shell_front(level_x)-.40
    gauge_bottom, gauge_top = level_z0-.21, level_z1+.21
    gauge_tube = ring("TNK_05_Instruments_LevelGlassTube",
                      (level_x, level_y, (gauge_bottom+gauge_top)/2),
                      .068, .052, gauge_top-gauge_bottom,
                      MATS["gauge_glass"], g, axis=(0, 0, 1),
                      segments=64, bevel=.0005)
    liquid_top = level_z0+.05+1.24
    liquid_bottom = gauge_bottom+.006
    cylinder("TNK_05_Instruments_LevelLiquid",
             (level_x, level_y, (liquid_bottom+liquid_top)/2),
             .0514, liquid_top-liquid_bottom, MATS["level_water"], g,
             axis=(0, 0, 1), segments=48, bevel=.0003)
    torus("TNK_05_Instruments_LevelLiquidMeniscus",
          (level_x, level_y, liquid_top), .0494, .002,
          MATS["level_water"], g, axis=(0, 0, 1), segments=48, sides=8)
    for index, z in enumerate((level_z0, level_z1)):
        wall_y = shell_front(level_x)
        port_tool = cylinder("tool_level_gauge_port",
                             (level_x, level_y+.052, z), .032, .180,
                             None, axis=(0, 1, 0), segments=40, bevel=0)
        boolean(gauge_tube, port_tool)
        shell_obj = bpy.data.objects["TNK_01_Shell_8mmWall"]
        shell_tool = cylinder("tool_level_shell_port",
                              (level_x, wall_y, z), .028, .15,
                              None, axis=(0, 1, 0), segments=32, bevel=0)
        boolean(shell_obj, shell_tool)
        saddle_pad(g, f"LevelTapPad_{index}", level_x, z, .110, .036, .012)
        straight(g, f"LevelTap_{index}", (level_x, wall_y+.006, z),
                 (level_x, wall_y-.18, z), .036, bore=.028)
        for dy in (.115, .141):
            ring(f"TNK_05_Instruments_LevelUnion_{index}_{dy}",
                 (level_x, wall_y-dy, z), .096, .036, .024,
                 MATS["machined"], g, axis=(0, -1, 0), segments=24)
        bolt_circle(g, f"LevelUnionBolts_{index}",
                    (level_x, wall_y-.155, z), (0, -1, 0),
                    .068, 4, .009, .050)
        ring(f"TNK_05_Instruments_LevelRootValve_{index+1}",
                 (level_x, wall_y-.235, z), .060, .028, .130,
                 MATS["steel"], g, axis=(0, 1, 0), segments=24)
        straight(g, f"LevelValveToGauge_{index}",
                 (level_x, wall_y-.298, z),
                 (level_x, level_y+.036, z), .036, bore=.028)
        straight(g, f"LevelValveStem_{index}",
                 (level_x, wall_y-.235, z+.040),
                 (level_x, wall_y-.235, z+.130), .016)
        box(f"TNK_05_Instruments_LevelValveLever_{index}",
            (level_x+.086, wall_y-.235, z+.140), (.22, .036, .020),
            MATS["dark"], g, .002)
        cap_z = z+(-.26 if index == 0 else .26)
        ring(f"TNK_05_Instruments_LevelEndFlange_{index}",
             (level_x, level_y, cap_z), .116, .050, .036,
             MATS["machined"], g, axis=(0, 0, 1), segments=32)
        ring(f"TNK_05_Instruments_LevelGlassGland_{index}",
             (level_x, level_y, z+(-.20 if index == 0 else .20)),
             .086, .052, .110, MATS["steel"], g,
             axis=(0, 0, 1), segments=40, bevel=.001)
        cylinder(f"TNK_05_Instruments_LevelEndCap_{index}",
                 (level_x, level_y, cap_z+(-.026 if index == 0 else .026)),
                 .116, .016, MATS["steel"], g, axis=(0, 0, 1), segments=32)
        bolt_circle(g, f"LevelEndBolts_{index}",
                    (level_x, level_y, cap_z+.038), (0, 0, 1),
                    .088, 4, .009, .056)
    scale_x, scale_y = level_x+.166, level_y-.008
    box("TNK_05_Instruments_LevelScaleRail",
        (scale_x, level_y+.030, (level_z0+level_z1)/2),
        (.140, .024, 2.42), MATS["machined"], g, .002)
    box("TNK_05_Instruments_LevelScaleInsert",
        (scale_x, level_y+.014, (level_z0+level_z1)/2),
        (.116, .008, 2.37), MATS["white"], g, .001)
    for index in range(116):
        z = level_z0+.05+index*.02
        major = index % 5 == 0
        box(f"TNK_05_Instruments_LevelScaleTick_{index:03d}",
            (scale_x-.027, scale_y, z),
            (.036 if major else .020, .002, .0018), MATS["dark"], g, 0)
        if index % 25 == 0:
            label(f"TNK_05_Instruments_LevelScaleNumber_{index}",
                  f"{index*.02:.1f}", (scale_x+.010, scale_y-.002, z-.010),
                  .025, MATS["dark"], g)
    label("TNK_05_Instruments_LevelScaleUnit", "m",
          (scale_x+.010, scale_y-.002, level_z1-.030),
          .024, MATS["dark"], g)
    for z in (2.50, 3.80):
        wall_y = shell_front(level_x)
        wall_patch(g, "LevelBracketShoe", level_x, z, .20, .13,
                   thickness=.012, offset=.002)
        arm_start, arm_end = level_y+.082, wall_y-.010
        box("TNK_05_Instruments_LevelBracketArm",
            (level_x, (arm_start+arm_end)/2, z),
            (.052, arm_end-arm_start+.006, .048), MATS["steel"], g, .001)
        ring("TNK_05_Instruments_LevelBracket",
             (level_x, level_y, z), .100, .070, .064,
             MATS["steel"], g, axis=(0, 0, 1), segments=48, bevel=.001)
        ring("TNK_05_Instruments_LevelClamp", (level_x, level_y, z),
             .080, .068, .060, MATS["machined"], g, axis=(0, 0, 1), segments=32)
        box("TNK_05_Instruments_LevelScaleBracket",
            (level_x+.140, level_y+.046, z),
            (.200, .020, .028), MATS["steel"], g, .0006)

    # Independent local gauge and electronic pressure/temperature sensors.
    gauge_x, gauge_z = -.88, 4.52
    gauge_wall = shell_front(gauge_x)
    previous_objects = set(bpy.data.objects)
    straight(g, "PressureTap", (gauge_x, gauge_wall+.015, gauge_z-.17),
             (gauge_x, gauge_wall-.12, gauge_z-.17), .014)
    pipe("TNK_05_Instruments_PressureGaugeSiphon",
         [(gauge_x, gauge_wall-.12, gauge_z-.17),
          (gauge_x-.050, gauge_wall-.15, gauge_z-.19),
          (gauge_x-.085, gauge_wall-.15, gauge_z-.11),
          (gauge_x, gauge_wall-.15, gauge_z-.080)],
         .008, MATS["steel"], g)
    straight(g, "GaugeStem", (gauge_x, gauge_wall-.15, gauge_z-.080),
             (gauge_x, gauge_wall-.15, gauge_z-.045), .011)
    cylinder("TNK_05_Instruments_PressureGaugeCase",
             (gauge_x, gauge_wall-.15, gauge_z), .065, .044,
             MATS["machined"], g, axis=(0, -1, 0), segments=48)
    torus("TNK_05_Instruments_PressureGaugeBezel",
          (gauge_x, gauge_wall-.176, gauge_z), .057, .004,
          MATS["steel"], g, axis=(0, -1, 0), segments=48, sides=8)
    cylinder("TNK_05_Instruments_PressureGaugeFace",
             (gauge_x, gauge_wall-.177, gauge_z), .053, .002,
             MATS["white"], g, axis=(0, -1, 0), segments=48, bevel=0)
    for i in range(31):
        a = math.radians(225-i*9)
        r = .044
        obj = box(f"TNK_05_Instruments_GaugeTick_{i:02d}",
                  (gauge_x+r*math.cos(a), gauge_wall-.179,
                   gauge_z+r*math.sin(a)),
                  (.007 if i % 5 == 0 else .004, .0008, .001),
                  MATS["dark"], g, 0)
        obj.rotation_euler.y = -a
        if i % 10 == 0:
            label(f"TNK_05_Instruments_GaugeNumber_{i}", f"{i//10}",
                  (gauge_x+.032*math.cos(a)-.004, gauge_wall-.180,
                   gauge_z+.032*math.sin(a)-.004), .009, MATS["dark"], g)
    straight(g, "GaugeNeedle", (gauge_x, gauge_wall-.181, gauge_z),
             (gauge_x-.025, gauge_wall-.181, gauge_z-.025),
             .0012, MATS["red"], segments=8)
    label("TNK_05_Instruments_PressureGaugeUnit", "bar",
          (gauge_x-.010, gauge_wall-.181, gauge_z-.024),
          .008, MATS["dark"], g)
    resize_local_parts(set(bpy.data.objects)-previous_objects,
                       (gauge_x, gauge_wall, gauge_z), 2.0)
    saddle_pad(g, "PressureTapPad", gauge_x, gauge_z-.34, .090, .028)
    sensor_ends = []
    for name, x, z in (("PT01", -.40, 4.40), ("TT01", .35, 1.80)):
        wall_y = shell_front(x)
        sensor_scale = 1.8
        saddle_pad(g, name+"_Pad", x, z, .050*sensor_scale, .018*sensor_scale)
        previous_objects = set(bpy.data.objects)
        straight(g, name+"_Coupling", (x, wall_y+.01, z),
                 (x, wall_y-.085, z), .021)
        cylinder(f"TNK_05_Instruments_{name}_Hex",
                 (x, wall_y-.065, z), .028, .027, MATS["machined"], g,
                 axis=(0, -1, 0), segments=6)
        cylinder(f"TNK_05_Instruments_{name}_Housing",
                 (x, wall_y-.115, z), .035, .075, MATS["steel"], g,
                 axis=(0, -1, 0), segments=32)
        cylinder(f"TNK_05_Instruments_{name}_Connector",
                 (x, wall_y-.163, z), .018, .025, MATS["dark"], g,
                 axis=(0, -1, 0), segments=20)
        resize_local_parts(set(bpy.data.objects)-previous_objects,
                           (x, wall_y, z), sensor_scale)
        sensor_ends.append((name, (x, wall_y-.178*sensor_scale, z)))

    glass_thermometer(g)

    # Larger wall display; retain real curved mounting shoes and underside entries.
    hx, hz = .05, 3.43
    display_width, display_height, display_depth = .650, .490, .160
    back_y = shell_front(0)-.035
    front_y = back_y-display_depth
    for x in (hx-.24, hx+.24):
        wall_y = shell_front(x)
        wall_patch(g, "DisplayShoe", x, hz, .12, .25,
                   thickness=.010, offset=.002, material=MATS["steel"])
        box("TNK_05_Instruments_DisplayStandOff", (x, (wall_y+back_y)/2, hz),
            (.075, wall_y-back_y+.006, .22), MATS["steel"], g, .001)
    box("TNK_05_Instruments_JunctionBox", (hx, back_y-display_depth/2, hz),
        (display_width, display_depth, display_height), MATS["steel"], g, .004)
    box("TNK_05_Instruments_DisplayGasket", (hx, front_y-.006, hz),
        (.615, .012, .455), MATS["rubber"], g, .002)
    box("TNK_05_Instruments_DisplayBezel", (hx, front_y-.016, hz),
        (.595, .012, .435), MATS["machined"], g, .002)
    box("TNK_05_Instruments_DisplayGlass", (hx, front_y-.026, hz+.024),
        (.505, .008, .319), MATS["screen"], g, .001)
    screen_y = front_y-.031
    for i, text in enumerate(("TK-01  /  LOCAL STATUS", "LEVEL   1.24 m",
                              "TEMP   24.8 C", "PRESS  0.01 bar")):
        label(f"TNK_05_Instruments_DisplayText_{i}", text,
              (hx-.220, screen_y, hz+.124-i*.068),
              .024 if i == 0 else .034, MATS["screen_text"], g)
    label("TNK_05_Instruments_DisplayDemo", "DEMO / INDICATION ONLY",
          (hx-.218, front_y-.024, hz-.174), .017, MATS["dark"], g)
    for dx in (-.271, .271):
        for dz in (-.192, .192):
            cylinder("TNK_05_Instruments_DisplayScrew",
                     (hx+dx, front_y-.024, hz+dz), .007, .004,
                     MATS["steel"], g, axis=(0, -1, 0), segments=12)

    # The level switch is on the left of the tube, clear of the enlarged scale.
    switch_x, switch_y = level_x-.135, level_y+.030
    sensor_ends.append(("LT01", (switch_x, switch_y+.034, 3.80)))
    box("TNK_05_Instruments_LevelSwitch",
        (switch_x, switch_y, 3.80), (.080, .068, .120),
        MATS["dark"], g, .002)
    box("TNK_05_Instruments_LevelSwitchMount",
        (level_x-.095, level_y+.055, 3.80), (.085, .028, .050),
        MATS["steel"], g, .001)
    route_settings = {
        "PT01": (-.33, 3.02),
        "TT01": (.53, 2.90),
        "LT01": (.73, 2.99),
    }
    route_records = []
    for i, (name, endpoint) in enumerate(sensor_ends):
        gland_x = hx-.20+i*.20
        gland_y = back_y-.085
        gland_z = hz-display_height/2-.018
        ring(f"TNK_05_Instruments_{name}_Gland",
             (gland_x, gland_y, gland_z), .022, .011, .040,
             MATS["machined"], g, axis=(0, 0, 1), segments=12)
        ring(f"TNK_05_Instruments_{name}_GlandNut",
             (gland_x, gland_y, gland_z+.014), .027, .011, .014,
             MATS["machined"], g, axis=(0, 0, 1), segments=6)
        ring(f"TNK_05_Instruments_{name}_EntrySeal",
             (gland_x, gland_y, gland_z-.020), .017, .0105, .012,
             MATS["rubber"], g, axis=(0, 0, 1), segments=24)
        ex, ey, ez = endpoint
        run_x, route_z = route_settings[name]
        start_z = ez-.16
        wall_entry = (run_x, shell_front(run_x)-.045, start_z)
        routed_tube(g, name+"_FlexibleTail",
                    [endpoint, (ex, ey-.025, ez-.060), wall_entry],
                    .010, MATS["rubber"], bend=.050)
        ring(f"TNK_05_Instruments_{name}_TailUnion", wall_entry,
             .018, .010, .030,
                 MATS["machined"], g, axis=(0, 0, 1), segments=12)
        route_xs = [run_x+(gland_x-run_x)*j/12 for j in range(13)]
        points = [wall_entry, (run_x, shell_front(run_x)-.045, route_z)]
        points += [(x, shell_front(x)-.045, route_z) for x in route_xs[1:]]
        points += [(gland_x, gland_y, route_z),
                   (gland_x, gland_y, gland_z+.002)]
        conduit = routed_tube(g, name+"_Conduit", points, .011,
                              MATS["steel"], bend=.055)
        conduit["outer_diameter_m"] = .022
        for j in range(1, 4):
            z = start_z+(route_z-start_z)*j/4
            conduit_clip(g, name+f"_VerticalClip_{j}", run_x, z, True)
        if abs(gland_x-run_x) > .22:
            x = (run_x+gland_x)/2
            conduit_clip(g, name+"_HorizontalClip", x, route_z, False)
        route_records.append({
            "name": name, "centerline": [list(point) for point in points],
            "entry_center": [gland_x, gland_y, gland_z],
            "outside_radius_m": .011,
        })
    g["conduit_routes_json"] = json.dumps(route_records)
    g["display_enclosure_dimensions_m"] = [display_width, display_depth, display_height]
    g["instrument_size_revision"] = "2026-11-12 approximately double-size wall instruments"
    g["display_values_are_static_demo"] = True
    g["instrument_architecture"] = (
        "2.4 m bypass chamber, top/bottom isolation valves and a round glass "
        "sight tube with transparent process-water level at 1.24 m; "
        "separate protected 24.8 C local glass thermometer; "
        "local backup pressure gauge; PT/TT sensors in 22 mm protected conduits through underside "
        "glands to a tank-mounted local display. Static demonstration readings."
    )


def ladder():
    g = GROUPS[6]
    x, ladder_y = -.99, Y+1.60
    for rail_x in (x-.23, x+.23):
        box("TNK_06_Ladder_Stringer", (rail_x, ladder_y, DECK_Z/2+.15),
            (.055, .025, DECK_Z-.10), MATS["steel"], g, .002)
        straight(g, "LandingHandHold", (rail_x, ladder_y, DECK_Z-.20),
                 (rail_x, ladder_y, DECK_Z+1.08), .018)
    rung_count = 17
    for i in range(rung_count):
        z = .35+i*(DECK_Z-.35)/(rung_count-1)
        cylinder(f"TNK_06_Ladder_AntiSlipRung_{i:02d}",
                 (x, ladder_y, z), .015, .46,
                 MATS["machined"], g, axis=(1, 0, 0), segments=16)
    for z in (2.20, 2.85, 3.50, 4.15, 4.80):
        points = [(x-.23, ladder_y, z)]
        points += [(x+.36*math.cos(a), ladder_y+.36*math.sin(a), z)
                   for a in [math.pi-i*math.pi/28 for i in range(29)]]
        points += [(x+.23, ladder_y, z)]
        pipe("TNK_06_Ladder_CageHoop", points, .009,
             MATS["safety"], g)
    for a in (0, math.pi/4, math.pi/2, 3*math.pi/4, math.pi):
        straight(g, "CageUpright",
                 (x+.36*math.cos(a), ladder_y+.36*math.sin(a), 2.20),
                 (x+.36*math.cos(a), ladder_y+.36*math.sin(a), 4.80),
                 .008, MATS["safety"], segments=12)
    for z in (1.90, 3.15, 4.40):
        for rail_x in (x-.23, x+.23):
            wall_y = Y+math.sqrt(1.5**2-rail_x**2)
            box("TNK_06_Ladder_WallShoe", (rail_x, wall_y-.012, z),
                (.10, .030, .16), MATS["shell"], g, .002)
            straight(g, "WallBracket", (rail_x, wall_y, z),
                     (rail_x, ladder_y, z), .018)
    for rail_x in (x-.23, x+.23):
        box("TNK_06_Ladder_FootPlate", (rail_x, ladder_y, .028),
            (.14, .15, .018), MATS["steel"], g, .002)
        box("TNK_06_Ladder_LowerPost", (rail_x, ladder_y, .115),
            (.055, .025, .22), MATS["steel"], g, .001)


def platform():
    g = GROUPS[7]
    deck_z = DECK_Z
    inner_y, outer_y = Y+1.57, Y+2.22
    xmin, xmax = -.70, .90
    deck_width = xmax-xmin
    for index in range(33):
        x = xmin+index*deck_width/32
        box(f"TNK_07_Platform_GratingBar_{index+1:02d}",
            (x, (inner_y+outer_y)/2, deck_z),
            (.005, outer_y-inner_y, .030), MATS["steel"], g, .0006)
    for index in range(7):
        box("TNK_07_Platform_GratingCrossTie",
            ((xmin+xmax)/2, inner_y+index*(outer_y-inner_y)/6, deck_z+.003),
            (deck_width, .005, .008), MATS["steel"], g, .0005)
    for x in (xmin, xmax):
        box("TNK_07_Platform_ChannelBearer", (x, (inner_y+outer_y)/2, deck_z-.045),
            (.055, outer_y-inner_y+.05, .075), MATS["steel"], g, .002)
    for y in (inner_y, outer_y):
        box("TNK_07_Platform_CrossMember", ((xmin+xmax)/2, y, deck_z-.05),
            (deck_width+.10, .055, .08), MATS["machined"], g, .004)
    for x in (-.48, .70):
        shell_y = Y+math.sqrt(1.5**2-x**2)
        for z in (deck_z-.12, deck_z-.78):
            box("TNK_07_Platform_ShellPad", (x, shell_y-.015, z),
                (.16, .040, .22), MATS["shell"], g, .002)
        straight(g, "HorizontalCantilever", (x, shell_y, deck_z-.08),
                 (x, outer_y, deck_z-.08), .028)
        straight(g, "DiagonalBrace", (x, shell_y, deck_z-.78),
                 (x, outer_y-.06, deck_z-.08), .025)
    for x, y in ((xmin, inner_y), (xmax, inner_y),
                 (xmin, outer_y), (.10, outer_y), (xmax, outer_y)):
        straight(g, "GuardPost", (x, y, deck_z),
                 (x, y, deck_z+1.10), .018, MATS["safety"])
    for dz in (.55, 1.10):
        for a, b in (((xmin, outer_y), (xmax, outer_y)),
                     ((xmax, inner_y), (xmax, outer_y))):
            straight(g, "GuardRail", (*a, deck_z+dz), (*b, deck_z+dz),
                     .018, MATS["safety"])
    for a, b in (((xmin, outer_y), (xmax, outer_y)),
                 ((xmax, inner_y), (xmax, outer_y))):
        start, end = Vector((*a, deck_z+.075)), Vector((*b, deck_z+.075))
        if a[1] == b[1]:
            size = ((end-start).length, .008, .15)
        else:
            size = (.008, (end-start).length, .15)
        box("TNK_07_Platform_ToePlate", (start+end)/2, size,
            MATS["steel"], g, .001)
    # The left-side gate aligns with the ladder landing.
    for dz in (.55, 1.10):
        straight(g, "LandingGate", (xmin, inner_y, deck_z+dz),
                 (xmin, outer_y, deck_z+dz), .014, MATS["safety"])
    for z in (deck_z+.55, deck_z+1.05):
        cylinder("TNK_07_Platform_GateHinge", (xmin, inner_y, z),
                 .024, .060, MATS["steel"], g, axis=(0, 0, 1), segments=20)
    box("TNK_07_Platform_LandingStep", (-.79, Y+1.60, deck_z),
        (.20, .42, .025), MATS["steel"], g, .001)


def inlet():
    g = GROUPS[8]
    x, z = .46, 3.98
    f = wall_nozzle(g, "InletDN100", x, z, .057, .050, .110, .0875,
                    reach=.18, count=8)
    joint_y = f.y-.0175
    ring("TNK_08_Inlet_FlangeGasket", (x, joint_y-.001, z),
         .077, .050, .002, MATS["rubber"], g, axis=(0, -1, 0), segments=48)
    flange("TNK_08_Inlet_ValveMatingFlange", (x, joint_y-.016, z),
           .110, .050, .028, .0875, 8, .009, MATS["machined"], g,
           axis=(0, -1, 0))
    bolt_circle(g, "MatingBolts", (x, joint_y-.030, z), (0, -1, 0),
                .0875, 8, .008, .068)
    valve_y = joint_y-.116
    lathe("TNK_08_Inlet_IsolationValveBody", (x, valve_y, z),
          [(-.09, .050), (-.09, .057), (-.06, .080), (.06, .080),
           (.09, .057), (.09, .050)], MATS["steel"], g,
          axis=(0, -1, 0), segments=48, bevel=.001)
    cylinder("TNK_08_Inlet_ValveStem", (x, valve_y, z+.105), .015, .10,
             MATS["machined"], g, axis=(0, 0, 1), segments=20)
    box("TNK_08_Inlet_LeverHandle", (x+.090, valve_y, z+.157),
        (.23, .025, .012), MATS["dark"], g, .003)
    front_y = valve_y-.105
    flange("TNK_08_Inlet_EndFlange", (x, front_y, z), .110, .050,
           .028, .0875, 8, .009, MATS["machined"], g, axis=(0, -1, 0))
    ring("TNK_08_Inlet_EndGasket", (x, front_y-.015, z),
         .077, .050, .002, MATS["rubber"], g, axis=(0, -1, 0), segments=32)
    blind_plate(g, "ShippingBlind", (x, front_y-.029, z), .110, .0875, 8)
    bolt_circle(g, "BlindBolts", (x, front_y-.0415, z), (0, -1, 0),
                .0875, 8, .008, .057)
    label("TNK_08_Inlet_Tag", "DN100", (x-.032, front_y-.042, z-.009),
          .017, MATS["dark"], g)
    wall_y = shell_front(x)
    saddle_pad(g, "ValveSupportPad", x, z-.19, .065)
    box("TNK_08_Inlet_ValveSupport", (x, (wall_y+valve_y)/2, z-.095),
        (.080, wall_y-valve_y, .020), MATS["steel"], g, .001)
    straight(g, "SupportDiagonal", (x, wall_y, z-.23),
             (x, valve_y, z-.095), .014)


def outlet():
    g = GROUPS[9]
    flange("TNK_09_Outlet_UpperFlange", (0, Y, .676),
           .14, .052, .025, .105, 4, .010,
           MATS["machined"], g, axis=(0, 0, -1))
    ring("TNK_09_Outlet_UpperGasket", (0, Y, .689),
         .085, .052, .001, MATS["rubber"], g, axis=(0, 0, 1), segments=32)
    bolt_circle(g, "UpperJointBolts", (0, Y, .664), (0, 0, -1),
                .105, 4, .010, .053)
    cylinder("TNK_09_Outlet_ValveBody", (0, Y, .525), .080, .28,
             MATS["steel"], g, axis=(0, 0, 1), segments=48, bevel=.006)
    for z in (.42, .63):
        torus("TNK_09_Outlet_BodyBonnetJoint", (0, Y, z), .080, .005,
              MATS["machined"], g, axis=(0, 0, 1), segments=48, sides=8)
    cylinder("TNK_09_Outlet_Stem", (0, Y-.145, .525), .014, .16,
             MATS["machined"], g, axis=(0, 1, 0), segments=24)
    for a in range(5):
        theta = TAU*a/5
        pipe("TNK_09_Outlet_HandwheelSpoke",
             [(0, Y-.225, .525),
              (.10*math.cos(theta), Y-.225, .525+.10*math.sin(theta))],
             .006, MATS["steel"], g)
    torus("TNK_09_Outlet_Handwheel", (0, Y-.225, .525), .10, .008,
          MATS["steel"], g, axis=(0, 1, 0), segments=48, sides=8)
    ring("TNK_09_Outlet_LowerUnion", (0, Y, .37), .090, .052, .035,
         MATS["machined"], g, axis=(0, 0, 1), segments=32)
    routed_tube(g, "DrainReturn",
                [(0, Y, .365), (0, Y, .20), (0, Y-.73, .20)],
                .057, MATS["steel"], bend=.12)
    flange("TNK_09_Outlet_TerminalFlange", (0, Y-.74, .20),
           .14, .052, .025, .105, 4, .009, MATS["machined"], g,
           axis=(0, -1, 0))
    blind_plate(g, "TerminalBlind", (0, Y-.767, .20), .14, .105, 4)
    bolt_circle(g, "TerminalBolts", (0, Y-.7795, .20), (0, -1, 0),
                .105, 4, .009, .054)
    box("TNK_09_Outlet_PipeBracket", (0, Y-.57, .129),
        (.14, .10, .025), MATS["steel"], g, .002)
    box("TNK_09_Outlet_SupportPost", (0, Y-.57, .065),
        (.050, .050, .11), MATS["steel"], g, .002)
    box("TNK_09_Outlet_SupportFoot", (0, Y-.57, .014),
        (.16, .14, .018), MATS["steel"], g, .001)


def cover():
    g = GROUPS[10]
    f = Vector(GROUPS[4]["mating_flange_center"])
    joint_y = f.y-.0175
    ring(f"{g.name}_Gasket", (MANWAY_X, joint_y-.001, MANWAY_Z),
         .263, .225, .002, MATS["rubber"], g, axis=(0, -1, 0),
         segments=64, bevel=0)
    c = (MANWAY_X, joint_y-.020, MANWAY_Z)
    blind_plate(g, "CoverPlate", c, .325, .280, 12, .036)
    cylinder(f"{g.name}_DishedCenter", (c[0], c[1]-.020, c[2]),
             .238, .008, MATS["shell"], g, axis=(0, -1, 0), segments=64)
    bolt_circle(g, "CoverStud", (c[0], c[1]-.018, c[2]), (0, -1, 0),
                .280, 12, .011, .077)
    hinge_x, hinge_y = MANWAY_X-.365, f.y+.020
    cylinder("TNK_10_ManwayCover_HingePin", (hinge_x, hinge_y, MANWAY_Z),
             .011, .38, MATS["machined"], g, axis=(0, 0, 1), segments=24)
    for z in (MANWAY_Z-.08, MANWAY_Z+.08):
        cylinder("TNK_10_ManwayCover_HingeLeaf", (hinge_x, hinge_y, z),
                 .024, .045, MATS["steel"], g, axis=(0, 0, 1), segments=24)
        box("TNK_10_ManwayCover_HingeArm",
            (hinge_x+.063, (hinge_y+c[1])/2, z), (.155, .090, .020),
            MATS["steel"], g, .002)
    pipe("TNK_10_ManwayCover_Handle",
         [(c[0]-.11, c[1]-.025, c[2]),
          (c[0]-.11, c[1]-.085, c[2]+.025),
          (c[0]+.11, c[1]-.085, c[2]+.025),
          (c[0]+.11, c[1]-.025, c[2])], .010, MATS["steel"], g)


def top_accessories():
    g = GROUPS[11]
    # Place the vent near the rear access deck, on a neck trimmed to the head.
    vent_x, vent_y = .42, Y+.86
    surface_z = SHELL_TOP+.75*math.sqrt(
        1-(vent_x**2+(vent_y-Y)**2)/1.5**2)
    vent_base_z, vent_top_z = surface_z-.050, surface_z+.11
    neck = ring("TNK_11_TopAccessories_VentNeck",
                (vent_x, vent_y, (vent_base_z+vent_top_z)/2),
                .043, .036, vent_top_z-vent_base_z,
                MATS["steel"], g, axis=(0, 0, 1), segments=48)
    # Clear the roof inside the bore; the neck overlaps the local head thickness.
    roof = bpy.data.objects["TNK_02_TopHead_2to1Ellipsoidal"]
    tool = cylinder("tool_vent", (vent_x, vent_y, surface_z), .036, .5,
                    None, axis=(0, 0, 1), segments=48, bevel=0)
    boolean(roof, tool)
    ring("TNK_11_TopAccessories_VentFlange",
         (vent_x, vent_y, vent_top_z), .074, .036, .016,
         MATS["machined"], g, axis=(0, 0, 1), segments=40)
    for i in range(4):
        a = TAU*i/4
        straight(g, "VentCapStandOff",
                 (vent_x+.049*math.cos(a), vent_y+.049*math.sin(a), vent_top_z),
                 (vent_x+.049*math.cos(a), vent_y+.049*math.sin(a), vent_top_z+.045),
                 .004, segments=12)
    lathe("TNK_11_TopAccessories_VentWeatherCap",
          (vent_x, vent_y, vent_top_z+.045),
          [(-.010, .080), (0, .080), (.018, .025), (.020, 0),
           (.015, 0), (-.005, .025)], MATS["steel"], g,
          axis=(0, 0, 1), segments=48, bevel=.0006)
    for x in (-1.02, 1.02):
        z = SHELL_TOP+.75*math.sqrt(1-(x/1.5)**2)
        # Small sloped shoes follow the roof surface underneath a plate lug.
        half_x, half_y = .10, .075
        vertices = []
        for dz in (0, .009):
            for px, py in ((x-half_x, Y-half_y), (x+half_x, Y-half_y),
                           (x+half_x, Y+half_y), (x-half_x, Y+half_y)):
                pz = SHELL_TOP+.75*math.sqrt(1-((px/1.5)**2+((py-Y)/1.5)**2))
                vertices.append((px, py, pz+dz))
        mesh("TNK_11_TopAccessories_LugShoe", vertices,
             [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
              (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)],
             MATS["shell"], g, .001)
        lug = box("TNK_11_TopAccessories_LiftingLug", (x, Y, z+.055),
                  (.14, .018, .16), MATS["steel"], g, .012)
        hole = cylinder("tool_lug_eye", (x, Y, z+.085), .027, .10,
                        None, axis=(0, 1, 0), segments=32, bevel=0)
        boolean(lug, hole)


def setup_materials():
    textures = surface_library(MODEL / "textures" / "silo")
    MATS.update({
        "shell": MATS.get("shell") or bpy.data.materials.get("TK Shell"),
    })
    # Create fresh names to avoid accidental dependency on unrelated scene data.
    from industrial_parts import material
    MATS.update({
        "shell": material("TK Shell", (.16, .21, .23), .12, .60, .000025, textures["cast"]),
        "steel": material("TK Stainless", (.33, .38, .40), .94, .31, .000012, textures["machined"]),
        "machined": material("TK Machined", (.47, .51, .53), .98, .24, .000006, textures["machined"]),
        "weld": material("TK Weld", (.17, .21, .23), .12, .66, .00003, textures["cast"]),
        "rubber": material("TK Gasket", (.045, .052, .052), .0, .86, .00004, textures["rubber"]),
        "dark": material("TK Marking", (.045, .055, .06), .12, .48),
        "white": material("TK GaugeFace", (.72, .76, .73), .0, .32),
        "red": material("TK Needle", (.68, .055, .025), .12, .34),
        "blue": material("TK JunctionBlue", (.12, .20, .24), .38, .4),
        "safety": material("TK SafetyYellow", (.48, .29, .055), .08, .58),
        "copper": material("TK BondingCopper", (.46, .19, .07), .82, .32),
        "screen": material("TK Display Screen", (.008, .018, .022), 0, .12),
        "screen_text": material("TK Display Characters", (.63, .85, .78), 0, .38),
    })
    display_bsdf = MATS["screen_text"].node_tree.nodes.get("Principled BSDF")
    display_bsdf.inputs["Emission Color"].default_value = (.63, .85, .78, 1)
    display_bsdf.inputs["Emission Strength"].default_value = .65
    glass = bpy.data.materials.new("TK Gauge Borosilicate Glass")
    glass.use_nodes = True
    glass.diffuse_color = (.72, .88, .92, .22)
    glass_bsdf = glass.node_tree.nodes.get("Principled BSDF")
    glass_bsdf.inputs["Base Color"].default_value = (.72, .88, .92, 1)
    glass_bsdf.inputs["Metallic"].default_value = 0
    glass_bsdf.inputs["Roughness"].default_value = .045
    glass_bsdf.inputs["IOR"].default_value = 1.47
    if "Transmission Weight" in glass_bsdf.inputs:
        glass_bsdf.inputs["Transmission Weight"].default_value = .92
    elif "Transmission" in glass_bsdf.inputs:
        glass_bsdf.inputs["Transmission"].default_value = .92
    liquid = bpy.data.materials.new("TK Gauge Red Process Liquid")
    liquid.use_nodes = True
    liquid.diffuse_color = (.38, .004, .001, 1)
    liquid_bsdf = liquid.node_tree.nodes.get("Principled BSDF")
    liquid_bsdf.inputs["Base Color"].default_value = (.38, .004, .001, 1)
    liquid_bsdf.inputs["Metallic"].default_value = 0
    liquid_bsdf.inputs["Roughness"].default_value = .22
    liquid_bsdf.inputs["IOR"].default_value = 1.34
    if "Transmission Weight" in liquid_bsdf.inputs:
        liquid_bsdf.inputs["Transmission Weight"].default_value = .035
    elif "Transmission" in liquid_bsdf.inputs:
        liquid_bsdf.inputs["Transmission"].default_value = .035
    MATS["gauge_glass"] = glass
    MATS["gauge_liquid"] = liquid
    water = glass.copy()
    water.name = "TK Sight Glass Process Water"
    water.diffuse_color = (.94, .985, 1, 1)
    bsdf = water.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = water.diffuse_color
    bsdf.inputs["IOR"].default_value = 1.333
    bsdf.inputs["Transmission Weight"].default_value = 1
    bsdf.inputs["Roughness"].default_value = .025
    absorption = water.node_tree.nodes.new("ShaderNodeVolumeAbsorption")
    absorption.inputs["Color"].default_value = (.35, .78, .82, 1)
    absorption.inputs["Density"].default_value = 1.5
    water.node_tree.links.new(
        absorption.outputs["Volume"],
        water.node_tree.nodes["Material Output"].inputs["Volume"])
    MATS["level_water"] = water
    thermometer_glass = glass.copy()
    thermometer_glass.name = "TK Thermometer Clear Glass"
    thermometer_glass.diffuse_color = (.98, .99, 1, 1)
    bsdf = thermometer_glass.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (.98, .99, 1, 1)
    bsdf.inputs["Transmission Weight"].default_value = 1
    bsdf.inputs["Roughness"].default_value = .018
    MATS["thermometer_glass"] = thermometer_glass
    scale = bpy.data.materials.new("TK Thermometer Satin Scale")
    scale.diffuse_color = (.62, .64, .63, 1)
    scale.use_nodes = True
    bsdf = scale.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = scale.diffuse_color
    bsdf.inputs["Metallic"].default_value = .35
    bsdf.inputs["Roughness"].default_value = .43
    MATS["thermometer_scale"] = scale
    finish_material(MATS["shell"], (.045, .074, .087), .53, .0,
                    relief=.000018, coat=.08)
    finish_material(MATS["steel"], (.29, .33, .35), .34, .88,
                    relief=.000009, turned=True, anisotropy=.22)
    finish_material(MATS["machined"], (.46, .49, .51), .26, 1,
                    relief=.000004, turned=True, anisotropy=.28)
    finish_material(MATS["weld"], (.15, .19, .20), .58, .08,
                    family="cast", relief=.000035)
    finish_material(MATS["safety"], (.48, .29, .055), .54, .06,
                    relief=.000012, coat=.08)


def create_scenes():
    assembled = bpy.context.scene
    assembled.name = "01_ASSEMBLED"
    assembled.use_fake_user = True
    assembled.frame_end = 320
    assembled.render.resolution_x, assembled.render.resolution_y = 2400, 1350
    assembled.render.resolution_percentage = 100
    assembled["model_status"] = "Engineering visualization; not pressure-vessel certification."
    assembled["nominal_shell_thickness_m"] = .008
    assembled["nominal_design_pressure_pa"] = 0
    assembled["exploded_view_note"] = "Welded heads are separated for inspection only; not a service disassembly procedure."
    # Keep the HDR environment in reflections, with a quiet camera background.
    floor_mat = bpy.data.materials.new("Tank Studio Floor")
    floor_mat.diffuse_color = (.19, .195, .20, 1)
    floor_mat.use_nodes = True
    floor_bsdf = floor_mat.node_tree.nodes.get("Principled BSDF")
    floor_bsdf.inputs["Base Color"].default_value = (.19, .195, .20, 1)
    floor_bsdf.inputs["Roughness"].default_value = .72
    floor_bsdf.inputs["Metallic"].default_value = 0
    finish_studio(assembled, floor_mat, box)
    world_tree = assembled.world.node_tree
    environment_bg = world_tree.nodes.get("Background")
    camera_bg = world_tree.nodes.new("ShaderNodeBackground")
    camera_bg.inputs["Color"].default_value = (.085, .095, .105, 1)
    camera_bg.inputs["Strength"].default_value = .5
    ray = world_tree.nodes.new("ShaderNodeLightPath")
    mix = world_tree.nodes.new("ShaderNodeMixShader")
    world_tree.links.new(ray.outputs["Is Camera Ray"], mix.inputs[0])
    world_tree.links.new(environment_bg.outputs[0], mix.inputs[1])
    world_tree.links.new(camera_bg.outputs[0], mix.inputs[2])
    world_tree.links.new(mix.outputs[0], world_tree.nodes["World Output"].inputs["Surface"])
    tank_lights = {
        "WindowKey": ((-4.0, -5.0, 7.0), 1800, 5.0, 4.0),
        "CeilingStrip": ((1.0, 2.5, 8.1), 2200, 4.5, .65),
        "MetalEdge": ((-3.0, 3.0, 4.5), 1300, .8, 4.0),
        "SoftFill": ((4.0, -4.0, 3.5), 650, 3.5, 3.5),
    }
    for light in (obj for obj in assembled.objects if obj.type == "LIGHT"):
        data = tank_lights.get(light.name.rsplit("_", 1)[-1])
        if data:
            light.location, light.data.energy = data[0], data[1]
            light.data.size, light.data.size_y = data[2], data[3]
            light.rotation_euler = (
                Vector((0, .2, 2.4))-light.location
            ).to_track_quat("-Z", "Y").to_euler()
    floor = next(obj for obj in assembled.objects
                 if obj.type == "MESH" and obj.get("preview_only"))
    floor.location.z = -.005
    cam_data = bpy.data.cameras.new("Tank Inspection Camera")
    cam = bpy.data.objects.new("Tank Inspection Camera", cam_data)
    assembled.collection.objects.link(cam)
    cam.location = (6.4, -11.4, 7.0)
    cam.data.lens = 65
    cam.rotation_euler = (Vector((0, .25, 3.0))-cam.location).to_track_quat("-Z", "Y").to_euler()
    assembled.camera = cam
    # Use the same restrained studio setup as the approved pump renders.
    animate_groups(assembled, GROUPS)
    for name, progress in (("02_EXPLODED", 1.0), ("03_WALL_DETAIL", .16)):
        scene = assembled.copy()
        scene.name = name
        scene.use_fake_user = True
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


def render_previews(instrument_only=False, context_only=False, fittings_only=False,
                    layout_only=False):
    from bpy_extras.object_utils import world_to_camera_view

    PREVIEW.mkdir(parents=True, exist_ok=True)
    assembled = bpy.data.scenes["01_ASSEMBLED"]
    exploded = bpy.data.scenes.get("02_EXPLODED")
    if not (instrument_only or context_only) and exploded is None:
        raise RuntimeError("Exploded review scene missing from saved tank")
    camera = assembled.camera
    if not camera:
        raise RuntimeError("Studio camera missing; refusing to skip visual previews")

    shots = (
        (assembled, "01-assembled-front", (5.8, -11.3, 7.2), (0, .30, 2.90), 65),
        (assembled, "02-assembled-rear", (-8.8, 10.3, 7.9), (0, .55, 2.90), 65),
        (assembled, "03-assembled-side", (12.0, -.8, 7.0), (0, .50, 2.90), 65),
        (exploded, "04-exploded-front", (8.4, -12.0, 8.3), (0, .40, 3.05), 60),
        (assembled, "05-instrument-detail", (.47, -2.15, 2.58),
         (.35, -1.30, 2.535), 70),
        (assembled, "06-instrument-connections", (1.08, -2.05, 2.67),
         (.35, -1.24, 2.52), 75),
        (assembled, "07-instrument-layout", (1.3, -8.7, 4.3),
         (0, -1.0, 3.25), 70),
        (assembled, "08-instrument-level-port", (1.74, -1.95, 4.65),
         (.91, -1.10, 4.38), 75),
    )
    if layout_only:
        shots = (
            (assembled, "13-spacing-assembled", (4.8, -12.0, 6.5),
             (0, .30, 2.90), 65),
            (assembled, "14-instrument-spacing-layout", (.8, -8.8, 4.0),
             (0, -1.10, 3.25), 70),
            (assembled, "15-instrument-thermometer-clearance", (.55, -4.8, 3.35),
             (.15, -1.30, 2.95), 58),
        )
    elif context_only:
        shots = shots[-2:]
    elif instrument_only:
        shots = shots[4:6]
    elif fittings_only:
        shots = (
            (assembled, "09-enlarged-assembled", (4.8, -12.0, 6.5),
             (0, .30, 2.90), 65),
            (assembled, "10-instrument-enlarged-layout", (.8, -8.8, 4.0),
             (0, -1.10, 3.25), 70),
            (assembled, "11-instrument-display-conduits", (.65, -3.5, 3.65),
             (.06, -1.30, 3.31), 65),
            (assembled, "12-instrument-level-enlarged", (2.2, -3.25, 3.7),
             (.86, -1.18, 3.40), 65),
        )
    old_scene = bpy.context.window.scene
    old_location = camera.location.copy()
    old_rotation = camera.rotation_euler.copy()
    old_lens = camera.data.lens
    old_filepath = assembled.render.filepath
    try:
        for scene, filename, position, target, lens in shots:
            bpy.context.window.scene = scene
            scene.frame_set(150 if scene == exploded else 1)
            scene.camera = camera
            camera.location = position
            camera.rotation_euler = (
                Vector(target)-camera.location
            ).to_track_quat("-Z", "Y").to_euler()
            camera.data.lens = lens
            detail = "instrument" in filename
            scene.render.resolution_x = 2880 if detail else 2400
            scene.render.resolution_y = 2160 if detail else 2700
            scene.render.resolution_percentage = 100
            bpy.context.view_layer.update()
            if not detail:
                equipment = [o for o in scene.objects
                             if o.type == "MESH" and o.name.startswith("TNK_")]
                corners = [o.matrix_world @ Vector(corner)
                           for o in equipment for corner in o.bound_box]
                for _ in range(6):
                    projected = [world_to_camera_view(scene, camera, p) for p in corners]
                    margin_scale = max(
                        max(abs(p.x-.5)/.42, abs(p.y-.5)/.43) for p in projected)
                    if abs(margin_scale-1) < .005:
                        break
                    camera.location = Vector(target)+(camera.location-Vector(target))*margin_scale
                    bpy.context.view_layer.update()
            scene.cycles.samples = 128
            scene.cycles.use_denoising = True
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
    triangles = 0
    for obj in equipment:
        evaluated = obj.evaluated_get(depsgraph)
        evaluated_mesh = evaluated.to_mesh()
        evaluated_mesh.calc_loop_triangles()
        triangles += len(evaluated_mesh.loop_triangles)
        evaluated.to_mesh_clear()
    target_dimensions = (3.0, 3.0, 5.86)
    dimension_names = ("width X", "depth Y", "height Z")
    dimension_rows = [
        f"- {name}: {actual:.4f} m vs {target:.2f} m target "
        f"({(actual/target-1)*100:+.2f}%; {'PASS' if abs(actual/target-1) <= .03 else 'FAIL'})"
        for name, actual, target in zip(dimension_names, dimensions, target_dimensions)
    ]
    front_projection = max(0.0, -mins[1])
    rear_projection = max(0.0, maxs[1])
    triangle_status = "PASS" if triangles <= 90_000 else "FAIL"
    uv_report = json.loads((MODEL / "reports" / "silo-uv.json").read_text(encoding="utf-8"))
    lines = [
        "# 立式储罐模型构建实测",
        "",
        "- 状态：Blender 源模型与预览阶段；尚未导入 Unity。",
        f"- Blender：{bpy.app.version_string}",
        f"- 设备网格对象：{len(equipment)}（不含摄影棚地面）",
        f"- 组件组：{len(GROUPS)}",
        f"- 网格三角面实测：{triangles:,}",
        f"- 实测包围盒 X/Y/Z：{dimensions[0]:.4f} / {dimensions[1]:.4f} / {dimensions[2]:.4f} m",
        *dimension_rows,
        f"- 巡检前侧投影：{front_projection:.4f} m（上限 1.20 m；{'PASS' if front_projection <= 1.2 else 'FAIL'}）",
        f"- 后侧投影：{rear_projection:.4f} m（设计包络 1.80 m；{'PASS' if rear_projection <= 1.8 else 'FAIL'}）",
        f"- 三角面预算：{triangles:,} / 90,000（{triangle_status}）",
        f"- UV 畸变检查：{'PASS' if uv_report['pass'] else 'FAIL'}；"
        f"{len(uv_report['failures'])} 个网格未通过，详见 `silo-uv.json`。UV 存在不等于畸变指标通过。",
        f"- 实测最小 Z：{mins[2]:.4f} m",
        "- 筒体名义壁厚：8 mm（脚本构造双层壳面；未做压力容器强度求解）。",
        "- 用户本次指定总高约 5.86 m，直径仍为 3.00 m；筒体切线高度 1.55–5.10 m。",
        "- 后平台深 650 mm，护栏高 1100 mm；安装包络包含平台、梯笼和全部阀组。",
        "- 展开视图中的焊接封头分离仅为检查展示，不代表实际检修拆卸步骤。",
        "- Unity 组件接入、导航通行、故障演化、压力设计与规范认证：未执行。",
        "",
        "预览图：",
        "",
        *[f"- `previews/silo/{name}.png`" for name in (
            "01-assembled-front", "02-assembled-rear",
            "03-assembled-side", "04-exploded-front",
            "05-instrument-detail", "06-instrument-connections",
            "07-instrument-layout", "08-instrument-level-port",
            "09-enlarged-assembled", "10-instrument-enlarged-layout",
            "11-instrument-display-conduits", "12-instrument-level-enlarged",
            "13-spacing-assembled", "14-instrument-spacing-layout",
            "15-instrument-thermometer-clearance")],
        "",
        "## 仪表设计与参考",
        "",
        "- 用户要求罐壁附件约放大两倍：液位管外径 136 mm、侧尺宽 140 mm，连通支管、阀体、接头和支架同步加粗；测量高度及 1.24 m 静态示值不变。",
        "- TI-01 就地玻璃温度计：400×72 mm 开口金属护壳、细玻璃毛细管和红色液柱；90°后接式热套管穿入罐壁，感温液泡在套管内部。0–60 C 刻度与液柱几何对应静态 24.8 C。",
        "- 温度计采用通用工业护壳与后接结构，并非厂牌产品复刻或选型结果。电子 TT01 仍独立接入数显盒。",
        "- 就地压力表外径 260 mm；取压短管、表弯、表头同步放大并连续连接。PT/TT 电子传感器放大 1.8 倍。",
        "- 罐壁显示盒 650×490×160 mm，屏幕采用低强度自发光字符。传感器软尾线转入外径 22 mm 金属保护线管，经固定抱箍接入底部格兰头；显示值为静态演示值。",
        "- 曲面铭牌增至 600×280 mm，继续贴合罐壁；附件放大图为 09–12，最新间距修订图为 13–15。",
        "- TI-01 安装点改至 X=0.14 m、Z=2.20 m，较放大初版向左移 210 mm、向下移 200 mm；护壳尺寸不变，曲面垫座、热套管和筒壁开孔在新位置重建，避免紧贴上方和右侧线管。",
        "- 人孔盖与颈部共用 650 mm 法兰外径、560 mm 螺栓圆及 12 孔相位；450 mm 通孔贯穿筒壁。",
        "- DN100 进口阀组采用同口径连接与端部盲板，不再以细管插入大法兰；排净阀组连接下封头中心。",
        "- 铭牌座、接地耳和仪表支座按筒壁曲率定位；吊耳采用有孔板式吊耳及曲面垫板。",
        "- 本组按《建模目标与验收标准》第 4.2 节收敛；仪表外形是示意性工程可视化，不代表已完成仪表选型或压力计算。",
        "- 当前几何仍未满足整机深度及前侧净空要求：前侧人孔盖/把手与后侧梯笼/平台共同扩大包络，需在导入 Unity 前重新布置，避免碰撞体侵入巡检通道。",
        "- 后平台保留可站立踏面及完整护栏，未为缩小包围盒而压扁；因此安装深度超过旧包络，后续接入需配套修改设备布局/导航。",
        "- 当前仅为 Blender 审图；未覆盖 TankViewer/FBX 输出、未接入 Unity，未做压力容器设计或仪表选型校核。",
        "",
        "适用规范：",
        "",
        "- `建模目标与验收标准.md` 第 1.2、2.2、4.2、5.2 节。",
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


def build(review_only=False):
    log("Building 5.86 m tank; review-only=" + str(review_only))
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
    bpy.context.scene.cycles.samples = 128
    setup_materials()
    root = bpy.data.objects.new("silo", None)
    bpy.context.scene.collection.objects.link(root)
    root.empty_display_size = .1
    root["design_note"] = "User target: approximately 5.86 m installed height, 3 m shell diameter. Full installed envelope reported; illustrative, un-stamped model."
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
    bpy.context.view_layer.update()
    roots = [root, *GROUPS]
    mesh_objects = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name.startswith("TNK_")]
    bottom_head = bpy.data.objects["TNK_03_BottomHead_2to1Ellipsoidal"]
    bottom_bounds = [bottom_head.matrix_world @ Vector(corner)
                     for corner in bottom_head.bound_box]
    bottom_min_z = min(point.z for point in bottom_bounds)
    bottom_max_z = max(point.z for point in bottom_bounds)
    if bottom_max_z > SHELL_BOTTOM + .001 or bottom_min_z < .79:
        raise RuntimeError(
            f"Bottom head profile outside expected envelope: {bottom_min_z:.4f}.."
            f"{bottom_max_z:.4f} m"
        )
    for obj in mesh_objects:
        obj["equipment_mesh"] = True
    log(f"Unwrapping {len(mesh_objects)} equipment meshes")
    uv_report = unwrap(mesh_objects)
    restore_shell_normals()
    (MODEL / "reports" / "silo-uv.json").write_text(
        json.dumps(uv_report, ensure_ascii=False, indent=2), encoding="utf-8")
    create_scenes()
    from pump_surface_finish import render_device
    render_device()
    bpy.context.window.scene = bpy.data.scenes["01_ASSEMBLED"]
    if not review_only:
        export_assets()
    write_geometry_report(mesh_objects)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                space = area.spaces.active
                space.shading.type = "MATERIAL"
                space.shading.studiolight_rotate_z = .35
                space.overlay.show_overlays = False
                space.region_3d.view_location = (0, .30, 2.95)
                space.region_3d.view_distance = 11
                space.region_3d.view_rotation = bpy.context.scene.camera.rotation_euler.to_quaternion()
    OUT.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT), check_existing=False)
    log(f"Saved {OUT}")
    render_previews(instrument_only="--instrument-only" in sys.argv,
                    fittings_only="--fittings-only" in sys.argv,
                    layout_only="--layout-only" in sys.argv)


if __name__ == "__main__":
    if "--render-saved" in sys.argv:
        from pump_surface_finish import render_device
        render_device()
        render_previews(
            instrument_only="--instrument-only" in sys.argv,
            context_only="--instrument-context-only" in sys.argv,
            fittings_only="--fittings-only" in sys.argv,
            layout_only="--layout-only" in sys.argv)
    else:
        build(review_only="--review-only" in sys.argv)
