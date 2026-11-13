"""Build a native axial fan, review scenes, packed PBR textures and FBX."""
import argparse
import csv
import json
import math
import sys
from datetime import datetime
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from industrial_parts import (
    TAU, activate, apply_mod, boolean, box, cylinder, finish,
    lathe, material, mesh, pipe, ring as basic_ring, surface_library, torus,
)
from pump_surface_finish import finish_material, render_device
from pump_uv import unwrap, triangle_metrics
from fan_components import (
    AXIS_HEIGHT as H, TARGET_SIZE, GROUP_NAMES, ancestors, animate, manifest,
)

MODEL = HERE.parent
PREVIEW = MODEL / "previews" / "industrial-fan"
REPORTS = MODEL / "reports"
SOURCE = MODEL / "source"
MATS = {}


def ring(name, center, outer, inner, length, mat, group=None,
         axis=(1, 0, 0), segments=48, bevel=.001):
    segments = min(segments, 64 if outer > .70 else 48)
    if not bevel:
        return basic_ring(name, center, outer, inner, length, mat, group,
                          axis, segments, 0)
    edge = min(bevel, length*.22, (outer-inner)*.22)
    a, b = -length/2, length/2
    profile = [(a, inner), (a, outer-edge),
               (a+edge*.293, outer-edge*.293), (a+edge, outer),
               (b-edge, outer), (b-edge*.293, outer-edge*.293),
               (b, outer-edge), (b, inner)]
    return lathe(name, center, profile, mat, group, axis, segments, 0)


def bolt(name, surface, axis, mat, group=None, radius=.010, length=.035):
    axis = Vector(axis).normalized()
    surface = Vector(surface)
    ring(name+"_Washer", surface+axis*.001, radius*1.55, radius*.55,
         .002, mat, group, axis=axis, segments=8, bevel=0)
    # Axial edge chamfers; explicit profiles avoid beveling every
    # vertical hex edge of each sub-centimetre fastener.
    height, edge = radius*.8, min(.0006, radius*.08)
    profile = [(0, radius-edge), (edge, radius),
               (height-edge, radius), (height, radius-edge)]
    basis = axis.to_track_quat("Z", "Y").to_matrix()
    origin = surface+axis*.002
    vertices = [origin+basis @ Vector((r*math.cos(TAU*i/6),
                                      r*math.sin(TAU*i/6), z))
                for z, r in profile for i in range(6)]
    faces = [tuple(reversed(range(6))), tuple(range(18, 24))]
    faces += [(j*6+i, j*6+(i+1) % 6, (j+1)*6+(i+1) % 6, (j+1)*6+i)
              for j in range(3) for i in range(6)]
    mesh(name+"_HexHead", vertices, faces, mat, group, 0)
    cylinder(name+"_Stud", surface-axis*(length/2), radius*.52, length,
             mat, group, axis=axis, segments=8, bevel=0)
    nut_profile = [(0, radius*.65), (0, radius-edge), (edge, radius),
                   (height-edge, radius), (height, radius-edge),
                   (height, radius*.65)]
    lathe(name+"_HexNut", surface-axis*length, nut_profile,
          mat, group, axis=axis, segments=6, bevel=0)


def label(name, text, center, size, mat, group=None, normal=(0, -1, 0)):
    curve = bpy.data.curves.new(name, "FONT")
    curve.body, curve.size = text, size
    curve.resolution_u = 2 if text in ("SI UNITS / TRAINING MODEL", "DIRECT / SI UNITS") else 1
    curve.extrude = .00006
    curve.space_character = 1.1
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = center
    obj.rotation_euler = Vector(normal).to_track_quat("Z", "Y").to_euler()
    activate(obj)
    bpy.ops.object.convert(target="MESH")
    return finish(bpy.context.object, name, mat, group, 0, False)


def log(message):
    print("FAN_BUILD", message, flush=True)


def empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.empty_display_size = .035
    return obj


def parent_preserve(obj, parent):
    bpy.context.view_layer.update()
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world


def setup():
    for folder in (PREVIEW, REPORTS, SOURCE, MODEL/"fbx"):
        folder.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "01_ASSEMBLED"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    root = empty("industrial-fan")
    root["model_role"] = "Workshop F-01 axial fan / educational nominal design"
    groups = [empty(name, root) for name in GROUP_NAMES]
    return scene, root, groups


def make_materials():
    textures = surface_library(MODEL/"textures"/"industrial-fan")
    settings = (
        ("blue", "Fan / blue grey enamel", (.032, .061, .077), 0, .48, "cast"),
        ("grey", "Fan / industrial grey", (.105, .119, .124), 0, .51, "cast"),
        ("lining", "Fan / dark duct interior", (.023, .033, .037), 0, .64, "cast"),
        ("graphite", "Fan / graphite powder coat", (.026, .030, .034), 0, .56, "cast"),
        ("yellow", "Fan / safety yellow", (.68, .327, .008), 0, .43, "cast"),
        ("steel", "Fan / machined steel", (.42, .45, .48), 1, .32, "machined"),
        ("zinc", "Fan / zinc plated fasteners", (.45, .48, .51), 1, .39, "machined"),
        ("guard", "Fan / dull galvanized wire", (.22, .245, .255), 1, .49, "machined"),
        ("blade", "Fan / satin cast aluminium", (.43, .46, .48), 1, .44, "machined"),
        ("rubber", "Fan / EPDM vibration pads", (.009, .010, .012), 0, .91, "rubber"),
        ("black", "Fan / label black", (.008, .010, .012), 0, .55, None),
        ("white", "Fan / label white", (.78, .80, .82), 0, .58, None),
        ("copper", "Fan / tinned earth terminal", (.46, .26, .09), 1, .38, "machined"),
        ("fracture", "Fan / exposed fracture interior", (.045, .048, .052), .85, .73, None),
        ("floor", "Fan studio neutral", (.22, .225, .23), 0, .78, None),
    )
    for key, name, color, metal, rough, family in settings:
        mat = material(name, color, metal, rough,
                       texture_set=textures.get(family) if family else None)
        finish_material(mat, color, rough, metal,
                        family="cast" if key in ("blue", "grey", "graphite", "lining", "blade") else None,
                        relief=.000055 if key in ("blue", "grey", "graphite") else (
                            .000035 if key == "blade" else .000008),
                        coat=.16 if key in ("blue", "grey", "yellow") else 0,
                        anisotropy=.22 if key == "steel" else .04 if key == "blade" else 0)
        # Cast texture is appropriate for aluminium; paint uses a finer orange peel.
        for item in mat.node_tree.nodes:
            if item.type == "TEX_NOISE" and key in ("blue", "grey", "graphite"):
                if item.inputs["Scale"].default_value == 620:
                    item.inputs["Scale"].default_value = 1450
                elif item.inputs["Scale"].default_value == 220:
                    item.inputs["Scale"].default_value = 480
            if item.type == "NORMAL_MAP":
                item.inputs["Strength"].default_value = .035
        MATS[key] = mat


def extrude(name, yz, x, width, mat, group, bevel=.001):
    verts = [(x+side, y, z) for side in (-width/2, width/2) for y, z in yz]
    n = len(yz)
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces += [(i, (i+1) % n, (i+1) % n+n, i+n) for i in range(n)]
    return mesh(name, verts, faces, mat, group, bevel)


def beam_between(name, a, b, width, thickness, mat, group):
    a, b = Vector(a), Vector(b)
    obj = box(name, (a+b)/2, (width, thickness, (b-a).length), mat, group)
    obj.rotation_euler = (b-a).to_track_quat("Z", "Y").to_euler()
    return obj


def annular_arc(name, x, r0, r1, a0, a1, width, mat, group):
    verts = []
    steps = 40
    for side in (-width/2, width/2):
        for radius in (r0, r1):
            for i in range(steps+1):
                t = a0+(a1-a0)*i/steps
                verts.append((x+side, radius*math.cos(t), H+radius*math.sin(t)))
    stride = steps+1
    faces = []
    for i in range(steps):
        for a, b in ((0, 1), (1, 3), (3, 2), (2, 0)):
            faces.append((a*stride+i, a*stride+i+1, b*stride+i+1, b*stride+i))
    faces += [(0, 2*stride, 3*stride, stride),
              (steps, stride+steps, 3*stride+steps, 2*stride+steps)]
    return mesh(name, verts, faces, mat, group, .0008)


def make_base(g):
    for x in (-.67, .67):
        for y in (-.67, .67):
            plate = box("FAN_00_Base_AnchorPlate", (x, y, .007), (.26, .26, .014),
                        MATS["grey"], g, bevel=0)
            for dx in (-.091, .091):
                cutter = cylinder("Fan_anchor_hole_tool", (x+dx, y, .007),
                                  .0058, .06, None, axis=(0, 0, 1),
                                  segments=12, bevel=0)
                boolean(plate, cutter)
            finish(plate, plate.name, MATS["grey"], g, .001)
            box("FAN_00_Base_EPDM_Isolator", (x, y, .044), (.17, .17, .060),
                MATS["rubber"], g, .002)
            box("FAN_00_Base_IsolatorCap", (x, y, .089), (.20, .20, .030),
                MATS["zinc"], g)
            for dx in (-.091, .091):
                bolt("FAN_00_Base_Anchor", (x+dx, y, .014), (0, 0, 1),
                     MATS["zinc"], g, .010, .012)
            bolt("FAN_00_Base_IsolatorStud", (x, y, .196), (0, 0, 1),
                 MATS["zinc"], g, .013, .15)
    for y in (-.63, .63):
        # Real C-section rather than a solid rectangular block.
        yz = [(y-.065, .104), (y+.065, .104), (y+.065, .114),
              (y-.055, .114), (y-.055, .186), (y+.065, .186),
              (y+.065, .196), (y-.065, .196)]
        extrude("FAN_00_Base_RolledChannel", yz, 0, 1.60, MATS["blue"], g)
    for x in (-.37, .37):
        box("FAN_00_Base_Crossmember", (x, 0, .164), (.12, 1.23, .065),
            MATS["blue"], g)
        annular_arc("FAN_00_Base_CurvedSaddle", x, .7345, .749,
                    math.radians(215), math.radians(325), .095, MATS["grey"], g)
        for sign in (-1, 1):
            yz = [(sign*.64, .197), (sign*.47, .197),
                  (sign*.425, .429), (sign*.505, .496), (sign*.57, .365)]
            extrude("FAN_00_Base_SaddleWeb", yz, x, .072, MATS["blue"], g)
            box("FAN_00_Base_SaddleFoot", (x, sign*.55, .207),
                (.20, .23, .022), MATS["grey"], g)
            for dx in (-.067, .067):
                bolt("FAN_00_Base_SaddleBolt", (x+dx, sign*.55, .218),
                     (0, 0, 1), MATS["zinc"], g, .009, .034)
            for dx in (-.036, .036):
                yz = [(sign*.62, .220), (sign*.48, .220),
                      (sign*.515, .412)]
                extrude("FAN_00_Base_TriangularGusset", yz, x+dx, .006,
                        MATS["grey"], g)
    box("FAN_00_Base_AssetTag", (-.21, -.696, .155), (.43, .002, .048),
        MATS["black"], g, .0006)
    label("FAN_00_Base_AssetTagText", "F-01  /  AXIAL VENTILATION",
          (-.405, -.6974, .143), .018, MATS["white"], g)


def drilled_flange(x, group):
    obj = ring("FAN_01_Duct_12mmDrilledFlange", (x, 0, H), .76, .728,
               .012, MATS["grey"], group, segments=96, bevel=0)
    for i in range(12):
        t = TAU*(i+.5)/12
        center = (x, .742*math.cos(t), H+.742*math.sin(t))
        cutter = cylinder("Fan_flange_hole_tool", center, .0047, .06,
                          None, segments=12, bevel=0)
        boolean(obj, cutter)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.to_mesh(obj.data)
    bm.free()
    finish(obj, obj.name, MATS["grey"], group, .00065)
    obj["thickness_m"] = .012
    obj["through_holes"] = 12
    return obj


def make_duct(g, cover_group):
    # Solidify is deliberately kept in the build history, then applied.
    verts = [(x, .734*math.cos(TAU*i/96), H+.734*math.sin(TAU*i/96))
             for x in (-.534, .534) for i in range(96)]
    faces = [(i, (i+1) % 96, (i+1) % 96+96, i+96) for i in range(96)]
    duct = mesh("FAN_01_Duct_Rolled8mmShell", verts, faces, MATS["blue"], g, 0)
    mod = duct.modifiers.new("Actual inward 8mm steel wall", "SOLIDIFY")
    mod.thickness, mod.offset = .008, -1
    apply_mod(duct, mod)
    finish(duct, duct.name, MATS["blue"], g, .0007)
    cutter = box("Fan_inspection_opening_tool", (-.16, -.669, 1.365),
                 (.248, .16, .138), None, bevel=0)
    boolean(duct, cutter)
    for x in (-.299, -.021):
        for t in (math.radians(143)+.018, math.radians(164)-.018):
            direction = Vector((0, math.cos(t), math.sin(t)))
            cutter = cylinder("Fan_cover_thread_clearance_tool",
                              Vector((x, 0, H))+direction*.731,
                              .0043, .040, None, axis=direction, segments=12, bevel=0)
            boolean(duct, cutter)
    finish(duct, duct.name, MATS["blue"], g, 0)
    duct["wall_thickness_m"] = .008
    duct["circumference_segments"] = 96
    for x in (-.54, .54):
        drilled_flange(x, g)
    for x in (-.34, .34):
        ring("FAN_01_Duct_StiffeningBand", (x, 0, H), .744, .7343, .028,
             MATS["grey"], g, segments=96, bevel=.001)
    for degrees in (54, 126):
        t = math.radians(degrees)
        normal = Vector((0, math.cos(t), math.sin(t)))
        tangent = Vector((0, -math.sin(t), math.cos(t)))
        curved_panel("FAN_01_Duct_LiftingShoe", -.025, .105, t-.078, t+.078,
                     .7342, .740, MATS["grey"], g)
        profile = [(-.045, .739), (.045, .739), (.045, .790)]
        profile += [(.045*math.cos(math.pi*i/16), .790+.045*math.sin(math.pi*i/16))
                    for i in range(1, 17)]
        yz = [(normal*r+tangent*u+Vector((0, 0, H)))[1:]
              for u, r in profile]
        eye = extrude("FAN_01_Duct_LiftingEyePlate", yz, .04, .016,
                      MATS["grey"], g)
        center = Vector((.04, 0, H))+normal*.790
        cutter = cylinder("Fan_lifting_eye_tool", center, .022, .045,
                          None, axis=(1, 0, 0), segments=24, bevel=0)
        boolean(eye, cutter)
        eye["eye_bore_radius_m"] = .022
        eye["radial_base_m"] = .739
        for x in (.031, .049):
            points = [Vector((x, 0, H))+normal*.741+tangent*u
                      for u in (-.043, 0, .043)]
            pipe("FAN_01_Duct_LiftingFootWeld", points, .002,
                 MATS["grey"], g)
    # Facing the inspection aisle (-Blender Y maps to +Unity Z).
    curved_panel("FAN_01_Duct_NameplateShoe", -.305, .125,
                 math.radians(169), math.radians(184), .7343, .737,
                 MATS["blue"], g)
    box("FAN_01_Duct_NameplateMount", (-.09, -.7385, 1.09),
        (.421, .005, .158), MATS["graphite"], g)
    box("FAN_01_Duct_Nameplate", (-.09, -.742, 1.09),
        (.402, .003, .142), MATS["steel"], g, .0006)
    for row, text in enumerate(("AXIAL FAN / F-01", "8 BLADE  |  DIRECT DRIVE",
                                "MOTOR 3~ / SERVICE ACCESS", "ROTATION  >   AIRFLOW  >")):
        label("FAN_01_Duct_NameplateText", text,
              (-.275, -.744, 1.128-row*.030), .022 if row == 0 else .013,
              MATS["black"], g)
    for x in (-.265, .085):
        for z in (1.032, 1.148):
            cylinder("FAN_01_Duct_NameplateRivet", (x, -.745, z), .003, .003,
                     MATS["zinc"], g, axis=(0, 1, 0), segments=12, bevel=.0005)
    box("FAN_01_Duct_AirflowLabel", (-.36, -.693, .91), (.22, .003, .05),
        MATS["yellow"], g)
    label("FAN_01_Duct_AirflowText", "AIR  >>>", (-.451, -.695, .897), .021,
          MATS["black"], g)
    cylinder("FAN_01_Duct_CondensateDrain", (.11, 0, .302), .013, .025,
             MATS["zinc"], g, axis=(0, 0, 1), segments=6)
    make_inspection_hatch(cover_group)
    for x in (-.465, .465):
        ring("FAN_01_Duct_EndRolledLip", (x, 0, H), .738, .7344, .018,
             MATS["blue"], g, segments=96, bevel=.0007)
    duct.data.materials.append(MATS["lining"])
    for face in duct.data.polygons:
        radial = Vector((0, face.center.y, face.center.z-H))
        if radial.length and face.normal.dot(radial.normalized()) < -.75:
            face.material_index = len(duct.data.materials)-1


def curved_panel(name, x0, x1, t0, t1, r0, r1, mat, group):
    verts = []
    count = 20
    for x in (x0, x1):
        for radius in (r0, r1):
            for i in range(count+1):
                t = t0+(t1-t0)*i/count
                verts.append((x, radius*math.cos(t), H+radius*math.sin(t)))
    stride = count+1
    faces = []
    for i in range(count):
        for a, b in ((0, 1), (1, 3), (3, 2), (2, 0)):
            faces.append((a*stride+i, a*stride+i+1, b*stride+i+1, b*stride+i))
    faces += [(0, 2*stride, 3*stride, stride),
              (count, stride+count, 3*stride+count, 2*stride+count)]
    return mesh(name, verts, faces, mat, group, .0005)


def make_inspection_hatch(g):
    a0, a1 = math.radians(143), math.radians(164)
    curved_panel("FAN_01_Duct_ServiceHatchGasket", -.312, -.008, a0, a1,
                 .7346, .7361, MATS["rubber"], g)
    cover = curved_panel("FAN_01_Duct_CurvedServiceCover", -.322, .002,
                         a0-.008, a1+.008, .7365, .7405, MATS["grey"], g)
    cover["functional_role"] = "Curved bolted access cover over real shell opening"
    for x in (-.299, -.021):
        for t in (a0+.018, a1-.018):
            direction = Vector((0, math.cos(t), math.sin(t)))
            center = Vector((x, 0, H))+direction*.741
            bolt("FAN_01_Duct_ServiceCoverBolt", center, direction,
                 MATS["zinc"], g, .0065, .012)
    # Captive screws seat in the drilled shell; no separate embedded back nuts.
    for obj in list(g.children_recursive):
        if "ServiceCoverBolt_HexNut" in obj.name:
            bpy.data.objects.remove(obj, do_unlink=True)
    for x in (-.22, -.10):
        t = (a0+a1)/2
        normal = Vector((0, math.cos(t), math.sin(t)))
        center = Vector((x, 0, H))+normal*.741
        beam_between("FAN_01_Duct_HandleStandoff", center, center+normal*.025,
                     .012, .012, MATS["graphite"], g)
    t = (a0+a1)/2
    y, z = .766*math.cos(t), H+.766*math.sin(t)
    straight_wire("FAN_01_Duct_InspectionHandle", (-.22, y, z), (-.10, y, z),
                  .0065, g, MATS["graphite"], segments=12)
    for obj in g.children_recursive:
        obj.name = obj.name.replace("FAN_01_Duct_", "FAN_08_AccessCover_")


def airfoil():
    # A finite trailing edge avoids degenerate zero-area closing faces.
    us = [.5*(1-math.cos(math.pi*i/18)) for i in range(19)]
    def thickness(u):
        return .2969*math.sqrt(u)-.126*u-.3516*u*u+.2843*u**3-.1015*u**4
    maximum = max(thickness(u) for u in us)
    outline = [(u, 1) for u in us] + [(u, -1) for u in reversed(us[1:])]
    return us, outline, maximum, thickness


def blade_geometry(index, group, fault=False):
    us, outline, max_foil, thickness = airfoil()
    n = len(outline)
    nr = 22
    theta = TAU*(index/8)+math.radians(11)
    radial = Vector((0, math.cos(theta), math.sin(theta)))
    tangent = Vector((0, -math.sin(theta), math.cos(theta)))
    vertices, rows = [], []
    for j in range(nr+1):
        t = j/nr
        radius = .178+.494*t
        beta = math.radians(43-20*t)
        chord = .252-.068*t
        full_thickness = .016*(1-t)+.0035*t
        chord_axis = Vector((math.sin(beta), 0, 0))+tangent*math.cos(beta)
        normal = Vector((math.cos(beta), 0, 0))-tangent*math.sin(beta)
        center = Vector((-.245+.035*t*t, 0, H))+radial*radius+tangent*(.025*t*t)
        rows.append({"r": radius, "chord": chord, "beta": beta,
                     "center": center, "normal": normal, "chord_axis": chord_axis})
        for u, sign in outline:
            camber = .014*chord*math.sin(math.pi*u)
            half_thick = .5*full_thickness*thickness(u)/max_foil
            vertices.append(center+chord_axis*((u-.44)*chord)+normal*(camber+sign*half_thick))
    faces = []
    seam_row = 3
    cut_u = us[12]
    duplicates = {}
    if fault:
        for k, (u, _) in enumerate(outline):
            if u < cut_u-1e-9:
                base = seam_row*n+k
                duplicates[base] = len(vertices)
                vertices.append(vertices[base].copy())

    def vindex(j, k, upper_region=False):
        v = j*n+(k % n)
        return duplicates.get(v, v) if upper_region else v

    for j in range(nr):
        for k in range(n):
            upper = fault and j >= seam_row
            faces.append((vindex(j, k, upper), vindex(j, k+1, upper),
                          vindex(j+1, k+1, upper), vindex(j+1, k, upper)))
    faces += [tuple(reversed(range(n))), tuple(nr*n+i for i in range(n))]
    seam_caps = []
    if fault:
        # Only the leading 75% is pre-seamed; the trailing ligament stays welded.
        cap = [seam_row*n+i for i in range(13)]
        cap += [seam_row*n+k for k in range(n-12, n)]
        seam_caps = [len(faces), len(faces)+1]
        faces.append(tuple(cap))
        faces.append(tuple(reversed([duplicates.get(v, v) for v in cap])))
    obj = mesh(f"FAN_02_Impeller_TwistedAirfoil_{index+1:02}",
               vertices, faces, MATS["blade"], group, bevel=0)
    obj["blade_index"] = index+1
    obj["radial_sections"] = nr+1
    obj["root_pitch_deg"] = 43.0
    obj["tip_pitch_deg"] = 23.0
    obj["root_thickness_m"] = .016
    obj["tip_thickness_m"] = .0035
    obj["fault_region"] = fault
    if fault:
        # Finish() welds coincident vertices. Restore the deliberately doubled
        # closed seam before adding keys, while retaining a shared rear ligament.
        data = bpy.data.meshes.new(obj.name+"_FractureTopology")
        data.from_pydata(vertices, [], faces)
        data.update()
        old = obj.data
        obj.data = data
        bpy.data.meshes.remove(old)
        data.materials.append(MATS["blade"])
        data.materials.append(MATS["fracture"])
        for p in data.polygons:
            p.use_smooth = p.index not in seam_caps
            p.material_index = 1 if p.index in seam_caps else 0
        bm = bmesh.new()
        bm.from_mesh(data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        for e in bm.edges:
            if e.is_manifold and e.calc_face_angle() > math.radians(30):
                e.smooth = False
        bm.to_mesh(data)
        bm.free()
        obj.shape_key_add(name="Basis")
        obj.shape_key_add(name="F-CRACK_intact")
        bulged = obj.shape_key_add(name="F-CRACK_bulged")
        opened = obj.shape_key_add(name="F-CRACK_opened")
        inverse_dup = {v: k for k, v in duplicates.items()}
        for i, vertex in enumerate(data.vertices):
            source = inverse_dup.get(i, i)
            j, k = divmod(source, n)
            u, _ = outline[k]
            upper = j > seam_row or i in inverse_dup
            r_progress = max(0, (j-seam_row)/(nr-seam_row))
            if upper:
                bend = .018*r_progress*r_progress
                gap = .010*max(0, 1-u/cut_u)*(1-r_progress)**2
                bulged.data[i].co.x += bend
                opened.data[i].co.x += .043*r_progress*r_progress+gap
        obj["fault_code"] = "F-CRACK"
        obj["fault_model"] = "Pre-seamed leading chord, shared trailing ligament; teaching geometry"
        obj["seam_pairs"] = json.dumps(list(duplicates.items()))
        obj["seam_upper_path"] = json.dumps([seam_row*n+k for k in range(13)])
        obj["seam_cut_chord_fraction"] = cut_u
    return obj


def make_impeller(g):
    rotor = empty("industrial-fan_rotor", g, (-.245, 0, H))
    rotor.rotation_euler.y = math.pi/2
    rotor["axis_local"] = "Z"
    rotor["axis_blender_world"] = "X"
    rotor["axis_height_m"] = H
    bpy.context.view_layer.update()
    # Build in world space, then preserve world coordinates while parenting.
    parts = []
    parts.append(lathe("FAN_02_Impeller_Spinner", (-.325, 0, H),
                      [(-.105, .014), (-.100, .047), (-.080, .104),
                       (-.035, .164), (.008, .183), (.024, .183),
                       (.024, .035), (-.105, .008)],
                      MATS["blade"], g, segments=64, bevel=.001))
    parts.append(ring("FAN_02_Impeller_Hub", (-.224, 0, H), .185, .034,
                      .125, MATS["steel"], g, segments=64))
    parts.append(cylinder("FAN_02_Impeller_Locknut", (-.438, 0, H), .035, .018,
                          MATS["zinc"], g, segments=6))
    parts.append(ring("FAN_02_Impeller_LockWasher", (-.426, 0, H), .041, .020,
                      .004, MATS["zinc"], g, segments=32, bevel=.0005))
    for i in range(8):
        blade = blade_geometry(i, g, fault=i == 0)
        parts.append(blade)
        theta = TAU*(i/8)+math.radians(11)
        r = .169
        pos = (-.284, r*math.cos(theta), H+r*math.sin(theta))
        before = set(bpy.context.scene.objects)
        bolt("FAN_02_Impeller_RootClamp", pos, (-1, 0, 0), MATS["zinc"], g,
             .008, .028)
        parts.extend(o for o in bpy.context.scene.objects if o not in before)
    for obj in parts:
        parent_preserve(obj, rotor)
    return rotor


def straight_wire(name, a, b, radius, group, mat=None, segments=6):
    a, b = Vector(a), Vector(b)
    return cylinder(name, (a+b)/2, radius, (b-a).length,
                    mat or MATS["steel"], group, axis=b-a,
                    segments=segments, bevel=0)


def make_guard(g, inlet):
    prefix = "FAN_03_InletGuard" if inlet else "FAN_05_OutletGuard"
    sign = -1 if inlet else 1
    x = sign*.566
    ring(prefix+"_Gasket", (sign*.547, 0, H), .758, .720, .001,
         MATS["rubber"], g, segments=96, bevel=0)
    ring(prefix+"_MountRing", (x, 0, H), .759, .711, .010,
         MATS["yellow"], g, segments=96, bevel=.0007)
    radii = (.075, .146, .217, .288, .359, .430, .501, .572, .643, .710) if inlet else (.710,)
    for r in radii:
        torus(prefix+"_ConcentricWire", (sign*.585, 0, H), r, .0018,
              MATS["guard"], g, segments=48, sides=3)
    for i in range(16 if inlet else 8):
        t = TAU*i/(16 if inlet else 8)
        straight_wire(prefix+"_RadialStiffener",
                      (sign*.588, .032*math.cos(t), H+.032*math.sin(t)),
                      (sign*.588, .714*math.cos(t), H+.714*math.sin(t)),
                      .0030, g, MATS["guard"], segments=8)
    cylinder(prefix+"_CentreBadge", (sign*.593, 0, H), .052, .008,
             MATS["graphite"], g, segments=48)
    for i in range(12):
        t = TAU*(i+.5)/12
        y, z = .742*math.cos(t), H+.742*math.sin(t)
        bolt(prefix+"_CaptiveBolt", (sign*.5715, y, z), (sign, 0, 0),
             MATS["zinc"], g, .0075, .036)
    for obj in list(g.children_recursive):
        if "_CaptiveBolt_HexNut" in obj.name:
            bpy.data.objects.remove(obj, do_unlink=True)
    for i in range(4):
        t = TAU*(i+.5)/4
        y, z = .708*math.cos(t), H+.708*math.sin(t)
        latch = box(prefix+"_RetainingClip", (sign*.578, y, z),
                    (.027, .042, .016), MATS["graphite"], g)
        latch.rotation_euler.x = t
    for y in (-.30, .30):
        z = H+math.sqrt(.69**2-y*y)
        pipe(prefix+"_ServiceHandle",
             [(sign*.581, y-.052, z-.021), (sign*.637, y-.052, z-.021),
              (sign*.637, y+.052, z-.021), (sign*.581, y+.052, z-.021)],
             .005, MATS["graphite"], g)
    if not inlet:
        # An additional fine square bird screen behind the radial safety grille.
        for step in range(-9, 10):
            offset = step*.071
            end = math.sqrt(.693**2-offset**2)
            for direction in (0, 1):
                a = (sign*.573, offset, H-end) if direction == 0 else (
                    sign*.575, -end, H+offset)
                b = (sign*.573, offset, H+end) if direction == 0 else (
                    sign*.575, end, H+offset)
                straight_wire(prefix+"_BirdMesh", a, b, .0010, g, MATS["guard"], segments=4)


def make_motor(g, vent_group):
    lathe("FAN_04_Motor_FinnedHousing", (.327, 0, H),
          [(-.168, .033), (-.168, .148), (-.151, .160),
           (.151, .160), (.168, .145), (.168, .033)],
          MATS["blue"], g, segments=64)
    for i in range(32):
        t = TAU*i/32
        fin = box("FAN_04_Motor_CoolingFin", (.327, .175*math.cos(t), H+.175*math.sin(t)),
                  (.270, .034, .003), MATS["blue"], g, .00065)
        fin.rotation_euler.x = t
    for x in (.154, .501):
        lathe("FAN_04_Motor_EndBell", (x, 0, H),
              [(-.014, .034), (-.014, .135), (-.008, .172),
               (.010, .172), (.022, .130), (.022, .034)],
              MATS["grey"], g, segments=64)
    for i in range(4):
        t = TAU*(i+.5)/4
        straight_wire("FAN_04_Motor_ExternalTieRod",
                      (.173, .194*math.cos(t), H+.194*math.sin(t)),
                      (.485, .194*math.cos(t), H+.194*math.sin(t)), .004,
                      g, MATS["zinc"], segments=12)
    cylinder("FAN_04_Motor_OutputShaft", (-.04, 0, H), .029, .405,
             MATS["steel"], g, segments=48)
    box("FAN_04_Motor_ShaftKey", (-.202, 0, H+.029), (.052, .010, .007),
        MATS["steel"], g, .0005)
    ring("FAN_04_Motor_CouplingGuard", (.107, 0, H), .067, .061, .082,
         MATS["yellow"], g, segments=48)
    for i in range(4):
        t = TAU*(i+.5)/4
        for x in (.138, .515):
            bolt("FAN_04_Motor_EndBellBolt",
                 (x, .143*math.cos(t), H+.143*math.sin(t)),
                 (-1, 0, 0) if x < .3 else (1, 0, 0),
                 MATS["zinc"], g, .007, .025)
    lathe("FAN_04_Motor_RearVentHood", (.531, 0, H),
          [(-.014, .174), (.013, .174), (.029, .153),
           (.029, .147), (.009, .168), (-.014, .168)],
          MATS["graphite"], vent_group, segments=64)
    for r in (.035, .073, .111, .145):
        torus("FAN_04_Motor_VentRing", (.562, 0, H), r, .0015,
              MATS["steel"], vent_group, segments=40, sides=4)
    for i in range(12):
        t = TAU*i/12
        straight_wire("FAN_04_Motor_VentSpoke",
                      (.563, .014*math.cos(t), H+.014*math.sin(t)),
                      (.563, .147*math.cos(t), H+.147*math.sin(t)), .0016, vent_group)
    for obj in vent_group.children_recursive:
        obj.name = obj.name.replace("FAN_04_Motor_", "FAN_09_MotorVentCover_")
    box("FAN_04_Motor_TerminalPad", (.32, -.008, H+.178),
        (.17, .12, .023), MATS["grey"], g)
    box("FAN_04_Motor_TerminalCase", (.32, -.008, H+.221),
        (.185, .153, .065), MATS["blue"], g)
    box("FAN_04_Motor_TerminalLid", (.32, -.008, H+.260),
        (.197, .164, .012), MATS["grey"], g)
    for x in (.247, .393):
        for y in (-.065, .048):
            bolt("FAN_04_Motor_TerminalScrew", (x, y, H+.266), (0, 0, 1),
                 MATS["zinc"], g, .0045, .020)
    box("FAN_04_Motor_DataPlate", (.33, -.192, H+.012),
        (.23, .003, .078), MATS["steel"], g, .0006)
    for i, text in enumerate(("3~  DRIVE MOTOR", "DIRECT / SI UNITS", "TRAINING ASSET")):
        label("FAN_04_Motor_DataText", text, (.225, -.194, H+.036-.023*i),
              .012, MATS["black"], g)


def make_struts(g):
    ring("FAN_06_Struts_RearCradle", (.48, 0, H), .224, .205, .052,
         MATS["grey"], g, segments=64)
    ring("FAN_06_Struts_MountingFlange", (.447, 0, H), .242, .205, .012,
         MATS["steel"], g, segments=64, bevel=.0007)
    for i in range(3):
        t = TAU*i/3+math.radians(30)
        radial = Vector((0, math.cos(t), math.sin(t)))
        tangent = Vector((0, -math.sin(t), math.cos(t)))
        # Closed lenticular section: a guide vane rather than a flat beam.
        outline = [(-.13, 0), (-.10, .010), (.055, .012),
                   (.145, .0015), (.145, -.0015), (.055, -.012), (-.10, -.010)]
        verts = []
        for radius in (.223, .7240):
            for x, y in outline:
                verts.append(Vector((.36+x, 0, H))+radial*radius+tangent*y)
        n = len(outline)
        faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
        faces += [(j, (j+1) % n, (j+1) % n+n, j+n) for j in range(n)]
        vane = mesh("FAN_06_Struts_StreamlinedGuideVane", verts, faces,
                    MATS["grey"], g, .0007)
        vane["functional_vane_index"] = i+1
        for r in (.229, .704):
            pos = Vector((.49, 0, H))+radial*r
            bolt("FAN_06_Struts_CradleFastener", pos, (1, 0, 0),
                 MATS["zinc"], g, .007, .028)


def make_terminal_mount(g):
    # Curved saddle feet and relieved sheet webs clear the lifting shoe axially.
    box_z = 1.56
    box("FAN_07_TerminalBox_MountRail", (.16, -.650, box_z),
        (.292, .018, .190), MATS["graphite"], g)
    radius = .7388
    for x, bolt_side in ((.145, -1), (.274, 1)):
        t0 = math.pi-math.asin((box_z+.095-H)/radius)
        t1 = math.pi-math.asin((box_z-.095-H)/radius)
        foot = curved_panel("FAN_07_TerminalBox_ContouredSaddle",
                            x-.020, x+.020, t0, t1, .7344, .7410,
                            MATS["grey"], g)
        foot["shell_fit_radius_m"] = .7344
        # The rear edge follows the saddle; the straight front bears on the rail.
        top = math.pi-math.asin((box_z+.070-H)/radius)
        bottom = math.pi-math.asin((box_z-.070-H)/radius)
        outline = [(-.644, box_z-.070), (-.644, box_z+.070)]
        outline += [(radius*math.cos(top+(bottom-top)*i/12),
                     H+radius*math.sin(top+(bottom-top)*i/12))
                    for i in range(13)]
        web = extrude("FAN_07_TerminalBox_RelievedSupportWeb",
                      outline, x, .006, MATS["grey"], g, bevel=0)
        opening = [(-.624, box_z-.050), (-.624, box_z+.047)]
        opening += [(-math.sqrt(radius**2-(z-H)**2)-.021, z)
                    for z in (box_z+.047, box_z-.050)]
        cutter = extrude("Fan_terminal_web_relief_tool", opening,
                         x, .030, None, None, bevel=0)
        boolean(web, cutter)
        finish(web, web.name, MATS["grey"], g, .0006)
        # Short studs terminate inside the mounting pad, clear of the shell.
        for z in (box_z-.075, box_z+.075):
            t = math.pi-math.asin((z-H)/radius)
            normal = Vector((0, math.cos(t), math.sin(t)))
            anchor = Vector((x+bolt_side*.012, 0, H))+normal*.7410
            bolt("FAN_07_TerminalBox_SaddleAnchor", anchor, normal,
                 MATS["zinc"], g, .0045, .005)


def make_terminal(g, lid_group):
    box_z = 1.56
    box_y = -.700
    make_terminal_mount(g)
    outer = box("FAN_07_TerminalBox_CastEnclosure", (.16, box_y, box_z),
                (.268, .098, .18), MATS["grey"], g, bevel=0)
    cutter = box("Fan_terminal_cavity_tool", (.16, box_y-.015, box_z),
                 (.254, .114, .166), None, bevel=0)
    boolean(outer, cutter)
    finish(outer, outer.name, MATS["grey"], g, .001)
    box("FAN_07_TerminalBox_LidGasket", (.16, box_y-.050, box_z),
        (.273, .003, .184), MATS["rubber"], lid_group, .0005)
    box("FAN_07_TerminalBox_FrontLid", (.16, box_y-.055, box_z),
        (.282, .008, .193), MATS["grey"], lid_group)
    for x in (.046, .274):
        for z in (box_z-.074, box_z+.074):
            bolt("FAN_07_TerminalBox_LidScrew", (x, box_y-.0536, z), (0, -1, 0),
                 MATS["zinc"], lid_group, .0055, .024)
    box("FAN_07_TerminalBox_DataPlateBacking",
        (.16, box_y-.0612, box_z), (.204, .002, .128),
        MATS["graphite"], lid_group, .001)
    box("FAN_07_TerminalBox_DataPlate",
        (.16, box_y-.0625, box_z), (.196, .001, .120),
        MATS["steel"], lid_group, .0007)
    for row, text in enumerate((
            "AXIAL FAN  /  F-01",
            "S/N  AF160-2611-042",
            "MFG  2026-11")):
        label("FAN_07_TerminalBox_DataText", text,
              (.071, box_y-.0633, 1.604-row*.027),
              .009 if row == 1 else .0085, MATS["black"], lid_group)
    barcode_pattern = "1011010011101001011011100101101001110100101101"
    bar_x, bar_step = .215, .00086
    for index, bit in enumerate(barcode_pattern):
        if bit == "1":
            box("FAN_07_TerminalBox_BarcodeBar",
                (bar_x+index*bar_step, box_y-.0633, 1.535),
                (bar_step*.58, .0007, .027), MATS["black"], lid_group, bevel=0)
    label("FAN_07_TerminalBox_BarcodeDigits", "0160 2611 042",
          (.211, box_y-.0633, 1.516), .0058, MATS["black"], lid_group)
    for obj in lid_group.children_recursive:
        obj.name = obj.name.replace("FAN_07_TerminalBox_", "FAN_10_TerminalLid_")
    for i in range(3):
        cylinder("FAN_07_TerminalBox_CopperPost", (.10+i*.06, box_y+.012, box_z),
                 .008, .034, MATS["copper"], g, axis=(0, 1, 0), segments=16)
        box("FAN_07_TerminalBox_Insulator", (.10+i*.06, box_y+.035, box_z),
            (.033, .015, .040), MATS["black"], g)
def equipment(scene):
    return [o for o in scene.objects if o.type == "MESH" and o.get("equipment_mesh")]


def snapshot_scene(source, name, progress=0):
    scene = bpy.data.scenes.new(name)
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    lookup = {}
    for obj in source.objects:
        if obj.get("preview_only"):
            continue
        copy = obj.copy()
        copy.animation_data_clear()
        scene.collection.objects.link(copy)
        lookup[obj] = copy
    for old, new in lookup.items():
        if old.parent in lookup:
            new.parent = lookup[old.parent]
    from fan_components import displacement
    for i, name in enumerate(GROUP_NAMES):
        obj = next(v for k, v in lookup.items() if k.name == name)
        obj.location = displacement(i, progress)
    return scene, lookup


def studio(scene):
    bpy.context.window.scene = scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.cycles.adaptive_threshold = .035
    scene.cycles.max_bounces = 6
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -.40
    scene.world = bpy.data.worlds.new(scene.name+"_Studio")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes["Background"]
    envpath = Path(bpy.utils.resource_path("LOCAL"))/"datafiles/studiolights/world/studio.exr"
    if envpath.exists():
        env = scene.world.node_tree.nodes.new("ShaderNodeTexEnvironment")
        env.image = bpy.data.images.load(str(envpath), check_existing=True)
        env.image.pack()
        scene.world.node_tree.links.new(env.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = .27
    scene.world["lighting_source"] = "Blender bundled studio.exr / CC0 / Greg Zaal"
    for name, pos, power, sx, sy in (
        ("Key", (-3.8, -4.2, 5.2), 820, 2.4, 3.0),
        ("Rim", (2.3, 2.8, 4.0), 720, 2.5, 1.2),
        ("FrontFill", (-4.1, .8, 2.2), 160, 1.5, 2.4),
        ("PlateFill", (.6, -3.2, 2.9), 210, 2.5, 2.5),
    ):
        data = bpy.data.lights.new(scene.name+"_"+name, "AREA")
        data.energy, data.shape, data.size, data.size_y = power, "RECTANGLE", sx, sy
        obj = bpy.data.objects.new(data.name, data)
        scene.collection.objects.link(obj)
        obj.location = pos
        obj.rotation_euler = (Vector((0, 0, H))-obj.location).to_track_quat("-Z", "Y").to_euler()
        obj["preview_only"] = True
        obj.hide_select = True
    floor = box(scene.name+"_StudioFloor", (0, 0, -.018), (200, 200, .034),
                MATS["floor"], bevel=0)
    floor["preview_only"] = True
    floor.hide_select = True
    camera = bpy.data.cameras.new(scene.name+"_Camera")
    cam = bpy.data.objects.new(camera.name, camera)
    scene.collection.objects.link(cam)
    cam["preview_only"] = True
    cam.hide_select = True
    camera.type = "ORTHO"
    scene.camera = cam
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                space = area.spaces.active
                space.clip_end = 1000
                space.overlay.show_relationship_lines = False
                space.overlay.show_extras = False
                space.overlay.show_floor = False
                space.shading.type = "MATERIAL"
                space.region_3d.view_location = (0, 0, .92)
                space.region_3d.view_distance = 4.4
                space.region_3d.view_rotation = Vector((-3.6, -4.4, 2.8)).to_track_quat("Z", "Y")


def camera(scene, pos, target, ortho):
    scene.camera.location = pos
    scene.camera.rotation_euler = (Vector(target)-Vector(pos)).to_track_quat("-Z", "Y").to_euler()
    scene.camera.data.ortho_scale = ortho


def render(scene, name, fit=True):
    bpy.context.window.scene = scene
    from render_fan_review import fit_camera
    if fit and name != "blade-detail.png":
        fit_camera(scene)
    render_device()
    scene.render.filepath = str(PREVIEW/name)
    log("render "+name)
    bpy.ops.render.render(write_still=True)


def measure(scene, root, groups):
    bpy.context.window.scene = scene
    scene.frame_set(1)
    bpy.context.view_layer.update()
    objects = equipment(scene)
    points, triangles = [], 0
    nonmanifold, volumes, badscale, uvbad = [], [], [], []
    stretches, ratios = [], []
    for obj in objects:
        points.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bad = sum(not e.is_manifold for e in bm.edges)
        if bad:
            nonmanifold.append({"name": obj.name, "edges": bad})
        volume = bm.calc_volume(signed=True)
        if volume <= 0:
            volumes.append({"name": obj.name, "volume": volume})
        bm.free()
        metrics, tiny = triangle_metrics(obj.data)
        stretch = max((s for _, s, _ in metrics), default=float("inf"))
        density = sorted(d for _, _, d in metrics if d > 1e-20)
        median = density[len(density)//2] if density else 0
        ratio = max((max(d/median, median/d) for d in density), default=float("inf"))
        stretches.append(stretch)
        ratios.append(ratio)
        if stretch > 1.25 or ratio > 1.25:
            uvbad.append(obj.name)
    for obj in root.children_recursive+[root]:
        if max(abs(s-1) for s in obj.scale) > 1e-5:
            badscale.append(obj.name)
    low = [min(p[i] for p in points) for i in range(3)]
    high = [max(p[i] for p in points) for i in range(3)]
    size = [b-a for a, b in zip(low, high)]
    rotor = bpy.data.objects["industrial-fan_rotor"]
    axis = rotor.matrix_world.to_3x3() @ Vector((0, 0, 1))
    axis_error = min((axis-Vector((1, 0, 0))).length, (axis+Vector((1, 0, 0))).length)
    origin_error = (rotor.matrix_world.translation-Vector((-.245, 0, H))).length
    checks = {
        "G1": abs(low[2]) < .005 and all(abs((low[i]+high[i])/2) < .005 for i in (0, 1)),
        "G2": not badscale,
        "G3": all(abs(s/t-1) <= .03 for s, t in zip(size, TARGET_SIZE)),
        "G4": tuple(g.name for g in sorted(root.children, key=lambda x: x.name)) == GROUP_NAMES,
        "G5": not nonmanifold,
        "G6": not volumes,
        "G7": triangles <= 100000,
        "G8": not uvbad,
        "G9": origin_error <= .002 and axis_error < 1e-5,
        "G10": rotor.name == "industrial-fan_rotor",
        "G11": all(any(g in ancestors(o) for g in groups) for o in objects),
        "G12": max(abs(low[1]), abs(high[1])) <= 1.2,
    }
    return {
        "date": datetime.now().astimezone().isoformat(), "blender": bpy.app.version_string,
        "width_depth_height_m": size, "bounds_min": low, "bounds_max": high,
        "mesh_count": len(objects), "triangles": triangles, "checks": checks,
        "non_manifold": nonmanifold, "non_positive_volumes": volumes,
        "bad_scale": badscale, "uv_failures": uvbad,
        "uv_max_anisotropy": max(stretches),
        "uv_max_area_density_ratio": max(ratios),
        "rotor_axis_direction": list(axis), "rotor_origin_error_m": origin_error,
        "rotor_axis_error": axis_error,
        "manifest": manifest(groups, objects),
    }


def fault_export(blade):
    keys = blade.data.shape_keys.key_blocks
    pairs = json.loads(blade["seam_pairs"])
    path = json.loads(blade["seam_upper_path"])
    basis = keys["Basis"]
    result = {}
    for keyname in ("F-CRACK_intact", "F-CRACK_bulged", "F-CRACK_opened"):
        key = keys[keyname]
        result[keyname] = {
            "max_vertex_displacement_m": max((p.co-q.co).length for p, q in zip(key.data, basis.data)),
            "max_crack_opening_m": max((key.data[a].co-key.data[b].co).length for a, b in pairs),
            "crack_surface_path_length_m": sum(
                (key.data[a].co-key.data[b].co).length for a, b in zip(path, path[1:])),
        }
    with (REPORTS/"industrial-fan-fault-vertices.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream)
        writer.writerow(("vertex", "intact_x", "intact_y", "intact_z",
                         "bulged_dx", "bulged_dy", "bulged_dz",
                         "opened_dx", "opened_dy", "opened_dz"))
        for i, vertex in enumerate(basis.data):
            writer.writerow((i, *vertex.co,
                             *(keys["F-CRACK_bulged"].data[i].co-vertex.co),
                             *(keys["F-CRACK_opened"].data[i].co-vertex.co)))
    result["method"] = "Same mesh vertices and paired seam IDs; metres, not solver output"
    result["seam_pairs"] = pairs
    result["crack_path_vertex_ids"] = path
    data = blade.data
    # The retained rear ligament is measured on the actual upper skin path.
    n = 37
    rear_path = [3*n+k for k in range(12, 19)]
    result["remaining_ligament_upper_surface_m"] = sum(
        (basis.data[a].co-basis.data[b].co).length for a, b in zip(rear_path, rear_path[1:]))
    return result


def export_fbx(scene, root):
    bpy.context.window.scene = scene
    scene.frame_set(1)
    bpy.ops.object.select_all(action="DESELECT")
    objects = [root]+list(root.children_recursive)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(MODEL/"fbx"/"industrial-fan.fbx"), use_selection=True,
        object_types={"EMPTY", "MESH"}, global_scale=1,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        # Keep the nested rotor's local axis: Blender's experimental baking
        # transforms meshes differently from structural Empty parents.
        axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        use_mesh_modifiers=False, mesh_smooth_type="FACE", use_tspace=True,
        use_triangles=False, add_leaf_bones=False, bake_anim=False,
        path_mode="RELATIVE", use_custom_props=False)


def write_report(data):
    (REPORTS/"industrial-fan-geometry.json").write_text(
        json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False), encoding="utf-8")
    (REPORTS/"industrial-fan-components.json").write_text(
        json.dumps(data["manifest"], indent=2), encoding="utf-8")
    log(json.dumps({k: data[k] for k in ("width_depth_height_m", "mesh_count", "triangles", "checks")}))


def blockout():
    scene, root, groups = setup()
    wire = material("Blockout faces and topology", (.34, .39, .43), 0, .7)
    tree = wire.node_tree
    node = tree.nodes.new("ShaderNodeWireframe")
    node.use_pixel_size = True
    node.inputs["Size"].default_value = .8
    mix = tree.nodes.new("ShaderNodeMixRGB")
    mix.inputs[1].default_value = (.34, .39, .43, 1)
    mix.inputs[2].default_value = (.008, .012, .014, 1)
    tree.links.new(node.outputs[0], mix.inputs[0])
    tree.links.new(mix.outputs[0], tree.nodes["Principled BSDF"].inputs["Base Color"])
    MATS["floor"] = material("Blockout floor", (.35, .35, .35), 0, .8)
    box("BlockoutBase", (0, 0, .10), (1.6, 1.6, .2), wire, groups[0])
    ring("BlockoutDuct", (0, 0, H), .76, .72, 1.08, wire, groups[1], segments=64)
    cylinder("BlockoutRotorHub", (-.24, 0, H), .18, .12, wire, groups[2])
    for i in range(8):
        t = TAU*i/8
        part = box("BlockoutBlade", (-.24, .44*math.cos(t), H+.44*math.sin(t)),
                   (.08, .51, .12), wire, groups[2])
        part.rotation_euler.x = t
    for group, x, r, depth in ((groups[3], -.585, .71, .01),
                                (groups[4], .33, .18, .35),
                                (groups[5], .585, .71, .01),
                                (groups[6], .47, .22, .05)):
        ring("BlockoutComponent", (x, 0, H), r, r*.8, depth, wire, group, segments=64)
    box("BlockoutTerminal", (.24, -.70, 1.40), (.28, .16, .19), wire, groups[7])
    for obj in root.children_recursive:
        if obj.type == "MESH":
            obj["equipment_mesh"] = True
    studio(scene)
    scene.cycles.samples = 16
    views = (
        ("blockout-perspective.png", (-3.8, -4.4, 2.8), (0, 0, .9), 4.15),
        ("blockout-inlet.png", (-5, 0, H), (0, 0, .9), 3.9),
        ("blockout-front.png", (0, -5, .9), (0, 0, .9), 3.9),
        ("blockout-top.png", (0, 0, 6), (0, 0, .5), 3.8),
    )
    for filename, pos, target, size in views:
        camera(scene, pos, target, size)
        render(scene, filename)
    camera(scene, views[0][1], views[0][2], views[0][3])
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"industrial-fan-blockout.blend"))
    points = [o.matrix_world @ v.co for o in equipment(scene) for v in o.data.vertices]
    low = [min(v[i] for v in points) for i in range(3)]
    high = [max(v[i] for v in points) for i in range(3)]
    size = [b-a for a, b in zip(low, high)]
    result = {"date": datetime.now().astimezone().isoformat(), "bounds_min": low,
              "bounds_max": high, "size_m": size,
              "dimensions_pass": all(abs(s/t-1) < .03 for s, t in zip(size, TARGET_SIZE)),
              "preview_images": [v[0] for v in views]}
    (REPORTS/"industrial-fan-stage-a.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    if not result["dimensions_pass"]:
        raise RuntimeError("Blockout dimensions exceed tolerance")
    log("blockout dimensions passed")


def detailed(no_render=False, geometry_only=False):
    scene, root, groups = setup()
    make_materials()
    for name, fn, group in (
        ("base", make_base, groups[0]), ("duct", lambda g: make_duct(g, groups[8]), groups[1]),
        ("impeller", make_impeller, groups[2]),
        ("inlet grille", lambda g: make_guard(g, True), groups[3]),
        ("motor", lambda g: make_motor(g, groups[9]), groups[4]),
        ("outlet grille", lambda g: make_guard(g, False), groups[5]),
        ("guide vanes", make_struts, groups[6]),
        ("terminals", lambda g: make_terminal(g, groups[10]), groups[7]),
    ):
        log("construct "+name)
        fn(group)
    objects = [o for o in root.children_recursive if o.type == "MESH"]
    for obj in objects:
        obj["equipment_mesh"] = True
        # Bake mesh rotations, retaining the dedicated rotor's axis transform.
        activate(obj)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if geometry_only:
        counts = []
        for obj in objects:
            obj.data.calc_loop_triangles()
            counts.append({"name": obj.name, "triangles": len(obj.data.loop_triangles)})
        counts.sort(key=lambda item: item["triangles"], reverse=True)
        (REPORTS/"industrial-fan-triangle-profile.json").write_text(
            json.dumps({"total": sum(item["triangles"] for item in counts),
                        "objects": counts}, indent=2), encoding="utf-8")
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"industrial-fan-work.blend"))
        log("geometry audit saved")
        return
    blade = next(o for o in objects if o.get("fault_region"))
    log("unwrap and measure "+str(len(objects))+" meshes")
    uv_result = unwrap(objects)
    data = measure(scene, root, groups)
    data["uv_unwrap"] = uv_result
    data["fault_geometry"] = fault_export(blade)
    write_report(data)
    if not all(data["checks"].values()):
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"industrial-fan-work.blend"))
        raise RuntimeError("Geometry checks failed; inspect industrial-fan-geometry.json")
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"industrial-fan-work.blend"))
    log("create source review scenes")
    exploded, exploded_map = snapshot_scene(scene, "02_EXPLODED", 1)
    internal, internal_map = snapshot_scene(scene, "03_INTERNAL", 0)
    bpy.context.window.scene = internal
    bpy.context.view_layer.update()
    for old, obj in internal_map.items():
        if any(groups[i] in ancestors(old) for i in (1, 3, 8)):
            obj.hide_render = True
            obj.hide_set(True)
        if groups[5] in ancestors(old):
            obj.location.x += .9
    # Fault views share exactly the exported blade mesh and its shape keys.
    faults = []
    for index, name in enumerate(("04_FAULT_INTACT", "05_FAULT_BULGED", "06_FAULT_OPENED")):
        fault_scene = bpy.data.scenes.new(name)
        fault_scene.unit_settings.system = "METRIC"
        only = blade.copy()
        only.data = blade.data.copy()
        only.animation_data_clear()
        only.parent = None
        fault_scene.collection.objects.link(only)
        only.matrix_world = blade.matrix_world.copy()
        for key in only.data.shape_keys.key_blocks:
            key.value = 0
        if index:
            only.data.shape_keys.key_blocks["F-CRACK_bulged" if index == 1 else "F-CRACK_opened"].value = 1
        faults.append((fault_scene, only))
    for review in (scene, exploded, internal, *(s for s, _ in faults)):
        studio(review)
    camera(scene, (-3.7, -4.5, 2.95), (0, -.025, .90), 4.15)
    camera(exploded, (-4.5, -6.2, 3.8), (0, 0, .90), 6.1)
    camera(internal, (-3.6, -3.4, 2.7), (0, 0, .90), 4.0)
    for fault_scene, obj in faults:
        camera(fault_scene, (-2.8, -1.0, 2.5), (-.23, .44, 1.125), 1.0)
    bpy.context.window.scene = scene
    scene.frame_start, scene.frame_end = 1, 320
    scene.render.fps = 24
    scene.render.fps_base = 24*14/149
    for frame, name in ((1, "Assembled"), (150, "Exploded"), (170, "Return"), (320, "Assembled again")):
        scene.timeline_markers.new(name, frame=frame)
    animate(groups)
    scene.frame_set(1)
    scene["fault_note"] = "F-CRACK geometry is pedagogical; no Abaqus or fatigue solution."
    scene["review_scenes"] = "Assembly / exploded / internals / three fault states"
    detail, lookup = snapshot_scene(scene, "07_BLADE_DETAIL", 0)
    for old, obj in lookup.items():
        if not (groups[2] in ancestors(old) or old == groups[2] or old == root):
            obj.hide_render = True
    studio(detail)
    camera(detail, (-3.0, -.9, 2.5), (-.24, .20, 1.16), 1.65)
    bpy.context.window.scene = scene
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = root
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"industrial-fan.blend"))
    export_fbx(scene, root)
    if not no_render:
        for filename, pos, target, scale in (
            ("inlet-front.png", (-5, -.08, 1.27), (0, 0, .90), 3.90),
            ("motor-side.png", (3.8, -4.5, 2.70), (0, -.025, .90), 4.15),
            ("elevated.png", (-3.6, -4.2, 4.55), (0, 0, .90), 4.25),
        ):
            camera(scene, pos, target, scale)
            render(scene, filename)
        camera(scene, (-3.7, -4.5, 2.95), (0, -.025, .90), 4.15)
        for review, filename in (
            (scene, "assembled.png"), (exploded, "exploded.png"),
            (internal, "internal.png"),
            *( (s, f"fault-{name}.png") for (s, _), name in zip(faults, ("intact", "bulged", "opened")) ),
        ):
            render(review, filename)
        bpy.context.window.scene = scene
        render(detail, "blade-detail.png")
        for filename, pos, target, scale in (
            ("terminal-detail.png", (1.45, -3.7, 2.15), (.14, -.64, 1.19), 1.15),
            ("lifting-detail.png", (-1.7, -3.3, 2.5), (.04, -.43, 1.68), .65),
        ):
            camera(scene, pos, target, scale)
            render(scene, filename, fit=False)
        hidden = [obj for obj in equipment(scene) if groups[3] in ancestors(obj)]
        for obj in hidden:
            obj.hide_render = True
        camera(scene, (-4, -.07, 1.18), (-.24, -.50, 1.44), 1.03)
        render(scene, "blade-clearance.png", fit=False)
        for obj in hidden:
            obj.hide_render = False
        camera(scene, (-3.7, -4.5, 2.95), (0, -.025, .90), 4.15)
        from render_fan_review import fit_camera
        fit_camera(scene)
        bpy.context.window.scene = scene
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"industrial-fan.blend"))
    log("source model and FBX saved")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=("blockout", "detailed"), default="detailed")
    parser.add_argument("--no-render", action="store_true")
    parser.add_argument("--geometry-only", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    blockout() if args.stage == "blockout" else detailed(args.no_render, args.geometry_only)
