"""Editable, original electrical cabinet; source, FBX and review renders."""
import argparse
import json
import math
import shutil
import sys
from datetime import datetime
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.geometry import interpolate_bezier

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from industrial_parts import (
    activate, apply_mod, bolt, boolean, box, cylinder, finish, material,
    mesh, pipe, ring, torus,
)
from pump_uv import planar_face, unwrap
from pump_surface_finish import render_device, studio
from cabinet_components import GROUP_NAMES, HINGE, DOOR_ANGLE, OFFSETS, DOOR_ATTACHED, animate

MODEL = HERE.parent
TEX = MODEL / "textures" / "electrical-cabinet"
SOURCE = MODEL / "source" / "electrical-cabinet.blend"
PREVIEW = MODEL / "previews" / "electrical-cabinet"
REPORT = MODEL / "reports"
FBX = MODEL / "fbx" / "electrical-cabinet.fbx"
FRONT = (0, -1, 0)
M = {}


def log(text):
    print("CABINET", text, flush=True)


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.empty_display_size = .025
    return obj


def image_material(name, filename, emission=False):
    mat = material(name, (.8, .8, .8), 0, .11 if emission else .48)
    tree = mat.node_tree
    image = bpy.data.images.load(str(TEX / filename), check_existing=True)
    image.pack()
    tex = tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    bsdf = tree.nodes.get("Principled BSDF")
    tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if emission:
        tree.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = .25
    return mat


def create_materials():
    for key, color, metal, rough, family in (
        ("steel", (.36, .375, .385), .9, .36, "brushed"),
        ("zinc", (.46, .49, .51), 1, .40, "zinc"),
        ("blue", (.043, .051, .057), 0, .48, "polymer"),
        ("black", (.009, .011, .012), 0, .48, "polymer"),
        ("white", (.54, .55, .52), 0, .58, "polymer"),
        ("grey", (.095, .108, .112), 0, .55, "polymer"),
        ("teal", (.018, .105, .090), 0, .56, "polymer"),
        ("yellow", (.79, .47, .012), 0, .55, "polymer"),
        ("red", (.48, .018, .012), 0, .55, "polymer"),
        ("wire_blue", (.018, .12, .32), 0, .55, "polymer"),
        ("wire_brown", (.16, .065, .026), 0, .55, "polymer"),
        ("wire_pe", (.044, .31, .075), 0, .55, "polymer"),
        ("copper", (.58, .26, .10), 1, .36, None),
        ("rubber", (.008, .011, .012), 0, .91, None),
        ("filter", (.29, .31, .29), 0, .92, None),
        ("floor", (.105, .112, .119), 0, .79, None),
        ("paint", (.43, .455, .45), 0, .43, "polymer"),
        ("drive", (.025, .075, .13), 0, .46, "polymer"),
        ("aluminium", (.55, .57, .58), 1, .30, "brushed"),
    ):
        mat = material("CAB / "+key, color, metal, rough)
        M[key] = mat
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if family:
            nodes, links = mat.node_tree.nodes, mat.node_tree.links
            for channel in ("BaseColor", "Normal", "Roughness", "Metallic", "Height"):
                tex = nodes.new("ShaderNodeTexImage")
                tex.label = channel
                image = bpy.data.images.load(
                    str(TEX / f"{family}_{channel}.png"), check_existing=True)
                if channel != "BaseColor":
                    image.colorspace_settings.name = "Non-Color"
                image.pack()
                tex.image = image
                if channel == "BaseColor":
                    tint = nodes.new("ShaderNodeMixRGB")
                    tint.blend_type = "MULTIPLY"
                    tint.inputs[0].default_value = 1
                    tint.inputs[2].default_value = (*color, 1)
                    links.new(tex.outputs["Color"], tint.inputs[1])
                    links.new(tint.outputs[0], bsdf.inputs["Base Color"])
                elif channel == "Normal":
                    normal = nodes.new("ShaderNodeNormalMap")
                    normal.inputs["Strength"].default_value = .075
                    links.new(tex.outputs["Color"], normal.inputs["Color"])
                    links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])
                elif channel == "Roughness":
                    remap = nodes.new("ShaderNodeMapRange")
                    remap.inputs["To Min"].default_value = rough-.035
                    remap.inputs["To Max"].default_value = rough+.035
                    links.new(tex.outputs["Color"], remap.inputs["Value"])
                    links.new(remap.outputs[0], bsdf.inputs["Roughness"])
            if key == "steel":
                bsdf.inputs["Anisotropic"].default_value = .22
                # Explicit world-X brushing avoids different directions on UV islands.
                coords = nodes.new("ShaderNodeTexCoord")
                scale = nodes.new("ShaderNodeVectorMath")
                scale.operation = "MULTIPLY"
                scale.inputs[1].default_value = (9, 6500, 6500)
                links.new(coords.outputs["Object"], scale.inputs[0])
                noise = nodes.new("ShaderNodeTexNoise")
                noise.inputs["Scale"].default_value = 1
                links.new(scale.outputs[0], noise.inputs["Vector"])
                bump = nodes.new("ShaderNodeBump")
                bump.inputs["Distance"].default_value = .000002
                bump.inputs["Strength"].default_value = .16
                links.new(noise.outputs["Fac"], bump.inputs["Height"])
                links.new(normal.outputs["Normal"], bump.inputs["Normal"])
                links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
        mat["pbr_metallic"] = metal
        mat["pbr_roughness"] = rough
    M["glass"] = material("CAB / inspection glass", (.88, .95, .94), 0, .09)
    glass = M["glass"].node_tree.nodes.get("Principled BSDF")
    glass.inputs["Transmission Weight"].default_value = .97
    glass.inputs["IOR"].default_value = 1.46
    M["atlas"] = image_material("CAB / engraved labels", "labels_BaseColor.png")
    M["hmi"] = image_material("CAB / HMI normal", "hmi_Emission.png", True)
    M["hmi_fault"] = image_material("CAB / HMI fault", "hmi_fault_Emission.png", True)
    M["hmi_fault"].use_fake_user = True
    for key, color in (("green_lens", (.006, .18, .025)),
                       ("amber_lens", (.62, .19, .004)),
                       ("red_lens", (.36, .006, .003))):
        mat = material("CAB / "+key, color, 0, .22)
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Coat Weight"].default_value = .5
        bsdf.inputs["Emission Color"].default_value = (*color, 1)
        bsdf.inputs["Emission Strength"].default_value = .35 if key == "green_lens" else 0
        M[key] = mat
    for key in ("mcb", "cpu", "io", "contactor", "supply", "rating", "hazard",
                "drive", "network", "safety", "operator", "zones", "drive_display"):
        M["print_"+key] = image_material("CAB / equipment print / "+key,
                                         key+"_BaseColor.png")
        bsdf = M["print_"+key].node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Roughness"].default_value = .59


def plaque(name, key, x, y, z, width, group):
    if key in ("hmi", "start", "fan", "manufacturer", "disconnect", "inside", "duct"):
        return None
    if not key.startswith("t"):
        width *= .53
    obj = box(name, (x, y, z), (width, .00012, width/4),
              M["white"], group, 0)
    obj.data.materials.append(M["atlas"])
    obj["label_key"] = key
    obj["label_width_m"] = width
    return obj


def printed(name, kind, x, y, z, width, height, group):
    obj = box(name, (x, y, z), (width, .00010, height), M["black"], group, 0)
    obj.data.materials.append(M["print_"+kind])
    obj["print_kind"] = kind
    obj["label_width_m"] = width
    obj["print_height_m"] = height
    return obj


def label_uv(obj, rect):
    data = obj.data
    if not data.uv_layers.active:
        data.uv_layers.new(name="UVMap")
    width = obj["label_width_m"]
    height = obj.get("print_height_m", width/4 if obj.get("label_key") else width/2)
    scale = (rect[2]-rect[0])/width
    for face in data.polygons:
        planar_face(data, face)
        for index in face.loop_indices:
            data.uv_layers.active.data[index].uv *= scale
        if face.normal.y < -.9:
            face.material_index = 1
            for index in face.loop_indices:
                co = data.vertices[data.loops[index].vertex_index].co
                data.uv_layers.active.data[index].uv = (
                    rect[0]+(co.x/width+.5)*(rect[2]-rect[0]),
                    rect[1]+(co.z/height+.5)*(rect[3]-rect[1]),
                )


def frame(name, x, y, z, width, height, border, depth, mat, group):
    for sign in (-1, 1):
        box(name+"_Vertical", (x+sign*(width-border)/2, y, z),
            (border, depth, height), mat, group, .0008)
        box(name+"_Horizontal", (x, y, z+sign*(height-border)/2),
            (width-2*border, depth, border), mat, group, .0008)


def screw(name, x, y, z, group, radius=.004):
    sides = 6 if radius <= .0031 else 8
    vertices = []
    for depth, r in ((-.003, radius*.84), (-.0025, radius), (0, radius)):
        for i in range(sides):
            a = math.tau*i/sides
            vertices.append((x+r*math.cos(a), y+depth, z+r*math.sin(a)))
    faces = [tuple(reversed(range(sides))), tuple(range(2*sides, 3*sides))]
    faces += [(j*sides+i, j*sides+(i+1) % sides,
               (j+1)*sides+(i+1) % sides, (j+1)*sides+i)
              for j in range(2) for i in range(sides)]
    mesh(name+"_Head", vertices, faces, M["zinc"], group, 0)
    box(name+"_Slot", (x, y-.0031, z), (radius*1.1, .0003, .0008),
        M["black"], group, 0)


def solid_sheet(name, corners, mat, group, thickness=.0015):
    obj = mesh(name, corners, [tuple(range(len(corners)))], mat, group, 0)
    mod = obj.modifiers.new("Actual sheet thickness", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = 0
    apply_mod(obj, mod)
    obj["sheet_thickness_m"] = thickness
    return finish(obj, name, mat, group, .0005, False)


def cut_box(obj, center, size):
    cutter = box("OpeningTool", center, size, None, bevel=0)
    boolean(obj, cutter)


def cleanup(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def enclosure():
    g = groups[0]
    # A hollow plinth and individually thickened panels, never a solid box shell.
    for x in (-.597, .597):
        box("CAB_00_Enclosure_PlinthSide", (x, 0, .065), (.006, .65, .13),
            M["blue"], g, .001)
    for y in (-.322, .322):
        obj = box("CAB_00_Enclosure_PlinthFace", (0, y, .065),
                  (1.188, .006, .13), M["blue"], g, .001)
        for x in (-.48, .48):
            cut_box(obj, (x, y, .055), (.065, .03, .023))
        cleanup(obj)
    for x in (-.55, .55):
        for y in (-.24, .24):
            box("CAB_00_Enclosure_AnchorFoot", (x, y, .008),
                (.07, .10, .016), M["zinc"], g, .001)
            bolt("CAB_00_Enclosure_Anchor", (x, y, .016), (0, 0, 1),
                 M["zinc"], g, .008, .012)
    for x in (-.59925, .59925):
        solid_sheet("CAB_00_Enclosure_SidePanel",
                    [(x, -.304, .13), (x, .319, .13),
                     (x, .319, 1.969), (x, -.304, 1.969)], M["steel"], g)
        for y in (-.263, .278):
            for z in (.205, 1.895):
                cylinder("CAB_00_Enclosure_SideCaptiveHead", (x, y, z), .004,
                         .0015, M["zinc"], g, axis=(1, 0, 0), segments=12, bevel=0)
                box("CAB_00_Enclosure_SideFastenerSlot", (x+(.0008 if x > 0 else -.0008), y, z),
                    (.00012, .004, .0007), M["black"], g, 0)
    solid_sheet("CAB_00_Enclosure_RearPanel",
                [(-.598, .320, .13), (.598, .320, .13),
                 (.598, .320, 1.969), (-.598, .320, 1.969)], M["steel"], g)
    solid_sheet("CAB_00_Enclosure_TopPanel",
                [(-.598, -.304, 1.969), (.598, -.304, 1.969),
                 (.598, .319, 1.969), (-.598, .319, 1.969)], M["steel"], g)
    floor = solid_sheet("CAB_00_Enclosure_BottomPanel",
                        [(-.598, -.304, .134), (.598, -.304, .134),
                         (.598, .319, .134), (-.598, .319, .134)], M["steel"], g)
    for x in (-.39, -.25, -.11, .03, .17):
        tool = cylinder("GlandHole", (x, .055, .134), .018, .03,
                        None, axis=(0, 0, 1), segments=24, bevel=0)
        boolean(floor, tool)
        ring("CAB_00_Enclosure_CableGland", (x, .055, .14),
             .025, .014, .025, M["black"], g, axis=(0, 0, 1), segments=24)
    cleanup(floor)
    for x in (-.573, .573):
        for y in (-.225, .285):
            box("CAB_00_Enclosure_FoldedUpright", (x, y, 1.053),
                (.025, .025, 1.838), M["steel"], g, .001)
    for z in (.154, 1.950):
        box("CAB_00_Enclosure_FrontReturn", (0, -.269, z),
            (1.138, .031, .025), M["steel"], g, .001)
    # Fixed service strip makes the 100 degree opening fit the stated envelope.
    g = groups[8]
    solid_sheet("CAB_08_ControlPanel_FixedServiceStrip",
                [(.302, -.283, .173), (.568, -.283, .173),
                 (.568, -.283, 1.930), (.302, -.283, 1.930)], M["steel"], g)
    g = groups[0]
    for x in (-.47, .47):
        lug = box("CAB_00_Enclosure_LiftingLug", (x, .19, 1.978),
                  (.07, .008, .044), M["zinc"], g, .001)
        tool = cylinder("LiftingHole", (x, .19, 1.984), .010, .03,
                        None, axis=(0, 1, 0), segments=24, bevel=0)
        boolean(lug, tool)
        cleanup(lug)
    g = groups[8]
    for i, (z, color, key) in enumerate(((1.67, "green_lens", "run"),
                                         (1.54, "amber_lens", "alarm"),
                                         (1.41, "green_lens", "power"))):
        cylinder(f"CAB_08_ControlPanel_PilotCollar_{i}", (.434, -.290, z),
                 .016, .011, M["black"], g, axis=FRONT, segments=32, bevel=.0006)
        ring(f"CAB_08_ControlPanel_PilotBezel_{i}", (.434, -.298, z),
             .014, .0115, .004, M["zinc"], g, axis=FRONT, segments=32, bevel=.0004)
        cylinder(f"cabinet_lamp_lens_{i}", (.434, -.302, z),
                 .011, .005, M[color], g, axis=FRONT, segments=32, bevel=.0007)
        plaque("CAB_08_ControlPanel_PilotTag", key, .434, -.284, z-.035, .16, g)
    box("CAB_08_ControlPanel_IsolatorBase", (.434, -.290, 1.104),
        (.073, .012, .073), M["yellow"], g, .0014)
    cylinder("CAB_08_ControlPanel_IsolatorSpindle", (.434, -.301, 1.104),
             .019, .012, M["red"], g, axis=FRONT, segments=32)
    handle = box("CAB_08_ControlPanel_IsolatorLever", (.434, -.314, 1.104),
                 (.014, .018, .060), M["red"], g, .0015)
    handle.rotation_euler.y = math.radians(-35)
    plaque("CAB_08_ControlPanel_IsolatorTag", "main", .434, -.284, 1.044, .17, g)
    cylinder("CAB_08_ControlPanel_EstopBase", (.434, -.289, .820),
             .036, .008, M["yellow"], g, axis=FRONT, segments=48, bevel=.0005)
    cylinder("CAB_08_ControlPanel_EstopNeck", (.434, -.301, .820),
             .013, .023, M["black"], g, axis=FRONT, segments=24, bevel=.0006)
    cylinder("CAB_08_ControlPanel_EstopMushroom", (.434, -.319, .820),
             .022, .015, M["red"], g, axis=FRONT, segments=48, bevel=.003)
    plaque("CAB_08_ControlPanel_EstopTag", "estop", .434, -.284, .763, .20, g)
    printed("CAB_08_ControlPanel_HazardSticker", "hazard", .434, -.284, .49,
            .072, .087, g)
    g = groups[0]
    # Narrow service label behind the closed door, and real ground hardware.
    plaque("CAB_00_Enclosure_InnerTag", "inside", 0, -.289, 1.946, .28, g)
    for x in (-.54, .54):
        for z in (.22, 1.90):
            screw("CAB_00_Enclosure_FrameScrew", x, -.291, z, g)


def backplate():
    g = groups[1]
    plate = box("CAB_01_Backplate_GalvanizedMountingPlate", (0, .228, 1.05),
                (1.06, .0025, 1.68), M["zinc"], g, .0006)
    plate["sheet_thickness_m"] = .0025
    for x in (-.49, .49):
        for z in (.26, 1.84):
            tool = cylinder("MountingHole", (x, .228, z), .006, .03,
                            None, axis=FRONT, segments=16, bevel=0)
            boolean(plate, tool)
            cylinder("CAB_01_Backplate_StandOff", (x, .267, z), .012,
                     .075, M["zinc"], g, axis=FRONT, segments=16)
            bolt("CAB_01_Backplate_Clamp", (x, .226, z), FRONT, M["zinc"],
                 g, .007, .015)
    cleanup(plate)
    cylinder("CAB_01_Backplate_GroundStud", (.46, .212, .32),
             .005, .026, M["copper"], g, axis=FRONT, segments=16, bevel=.0005)
    ring("CAB_01_Backplate_GroundWasher", (.46, .204, .32), .012,
         .005, .003, M["zinc"], g, axis=FRONT, segments=16)
    plaque("CAB_01_Backplate_EarthTag", "pe", .43, .223, .365, .14, g)


def din_rail(name, x, z, width, group, y=.192):
    # TS35 section: return lips, legs and back form an actual open channel.
    profile = [(-.0175, -.008), (-.012, -.008), (-.012, -.002),
               (.012, -.002), (.012, -.008), (.0175, -.008),
               (.0175, -.0065), (.0135, -.0065), (.0135, -.0005),
               (-.0135, -.0005), (-.0135, -.0065), (-.0175, -.0065)]
    vertices = [(x+end, y+depth, z+height)
                for end in (-width/2, width/2) for height, depth in profile]
    n = len(profile)
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces += [(i, (i+1) % n, (i+1) % n+n, i+n) for i in range(n)]
    mesh(name, vertices, faces, M["zinc"], group, .0005)
    for px in (x-width*.40, x+width*.40):
        screw(name+"_RailScrew", px, y-.002, z, group, .003)


def terminals():
    g = groups[2]
    din_rail("CAB_02_Terminals_DIN35", -.08, .462, .84, g)
    plaque("CAB_02_Terminals_Header", "x1", -.075, .203, .542, .29, g)
    for index in range(20):
        x = -.45+index*.037
        insulator = M["yellow"] if index >= 17 else M["grey"] if index % 2 else M["white"]
        box(f"CAB_02_Terminals_X1_{index+1:02}", (x, .153, .462),
            (.033, .075, .063), insulator, g, .0008)
        box("CAB_02_Terminals_Recess", (x, .114, .465), (.018, .003, .042),
            M["black"], g, .0005)
        for z in (.443, .481):
            screw("CAB_02_Terminals_ClampingScrew", x, .110, z, g, .004)
        plaque("CAB_02_Terminals_Number", f"t{index+1:02}", x, .109, .466, .029, g)
    for x in (-.482, .277):
        box("CAB_02_Terminals_EndStop", (x, .166, .462), (.021, .053, .08),
            M["blue"], g, .001)
    bar = box("CAB_02_Terminals_CopperPEBusbar", (-.08, .157, .325),
              (.69, .012, .026), M["copper"], g, .001)
    for x in (-.355, .195):
        cylinder("CAB_02_Terminals_PEInsulatingPost", (x, .196, .325),
                 .019, .066, M["yellow"], g, axis=FRONT, segments=16)
    for i in range(12):
        x = -.37+i*.052
        hole = cylinder("BusbarDrill", (x, .157, .325), .003, .03,
                        None, axis=FRONT, segments=12, bevel=0)
        boolean(bar, hole)
        screw("CAB_02_Terminals_PEScrew", x, .149, .325, g, .0045)
    cleanup(bar)
    plaque("CAB_02_Terminals_PELabel", "pe", -.08, .149, .282, .24, g)


def door():
    g = groups[3]
    cx, cy = -.144, -.280
    panel = solid_sheet("CAB_03_Door_1p5mmFoldedPanel",
                        [(-.574, cy, .178), (.286, cy, .178),
                         (.286, cy, 1.927), (-.574, cy, 1.927)], M["steel"], g)
    for x, z, w, h in ((-.17, 1.597, .415, .242),
                        (-.17, 1.110, .522, .312),
                        (-.17, .553, .232, .232)):
        cut_box(panel, (x, cy, z), (w, .06, h))
    cleanup(panel)
    # Folded returns and rubber seal remain attached to the door.
    frame("CAB_03_Door_FoldedReturn", cx, -.264, 1.0525, .846, 1.735,
          .012, .025, M["steel"], g)
    frame("CAB_03_Door_EPDMSeal", cx, -.247, 1.0525, .82, 1.71,
          .006, .006, M["rubber"], g)
    frame("CAB_03_Door_WindowGasket", -.17, -.286, 1.110, .55, .34,
          .021, .009, M["black"], g)
    glass = box("CAB_03_Door_InspectionGlass", (-.17, -.287, 1.110),
                (.510, .003, .300), M["glass"], g, .0008)
    glass["glass_thickness_m"] = .003
    for x in (-.424, .084):
        for z in (.958, 1.262):
            screw("CAB_03_Door_WindowScrew", x, -.293, z, g, .003)
    frame("CAB_03_Door_HMIPolymerBezel", -.17, -.290, 1.597, .449, .276,
          .024, .018, M["black"], g)
    screen = box("CAB_03_Door_HMIDisplay", (-.17, -.300, 1.597),
                 (.395, .001, .1975), M["black"], g, 0)
    screen.data.materials.append(M["hmi"])
    screen["screen_uv"] = True
    screen["label_width_m"] = .395
    box("CAB_03_Door_HMIRearHousing", (-.17, -.233, 1.597),
        (.407, .071, .230), M["grey"], g, .002)
    plaque("CAB_03_Door_HMITag", "hmi", -.17, -.284, 1.778, .39, g)
    plaque("CAB_03_Door_AssetTag", "asset", -.39, -.282, 1.85, .42, g)
    printed("CAB_03_Door_RatingPlate", "rating", -.31, -.281, .295, .17, .067, g)
    for z in (.45, 1.052, 1.75):
        box("CAB_03_Door_HingeLeaf", (-.568, -.266, z), (.032, .012, .085),
            M["zinc"], g, .0008)
        for dz in (-.027, 0, .027):
            cylinder("CAB_03_Door_HingeKnuckle", (-.574, -.279, z+dz),
                     .0085, .025, M["zinc"], g, axis=(0, 0, 1), segments=24,
                     bevel=.0005)
        cylinder("CAB_03_Door_HingePin", (-.574, -.279, z),
                 .0035, .086, M["zinc"], g, axis=(0, 0, 1), segments=16, bevel=0)
    box("CAB_03_Door_LockEscutcheon", (.201, -.291, 1.10),
        (.041, .016, .205), M["black"], g, .002)
    box("CAB_03_Door_LiftHandle", (.201, -.318, 1.10),
        (.018, .014, .146), M["zinc"], g, .002)
    for z in (1.021, 1.179):
        box("CAB_03_Door_HandleStandoff", (.201, -.307, z),
            (.017, .031, .016), M["zinc"], g, .001)
    cylinder("CAB_03_Door_LockCylinder", (.201, -.304, .964), .010,
             .006, M["zinc"], g, axis=FRONT, segments=24, bevel=.0005)
    for x, color in ((-.23, "green_lens"), (-.11, "red_lens")):
        cylinder("CAB_03_Door_PushButtonBezel", (x, -.286, .827), .015,
                 .010, M["zinc"], g, axis=FRONT, segments=32)
        cylinder("CAB_03_Door_PushButton", (x, -.295, .827), .011,
                 .009, M[color], g, axis=FRONT, segments=32, bevel=.001)
    plaque("CAB_03_Door_StartStopTag", "start", -.175, -.284, .762, .33, g)
    for x in (-.454, .148):
        box("CAB_03_Door_InnerCableTrunk", (x, -.231, 1.30),
            (.023, .027, .91), M["grey"], g, .001)
    pipe("CAB_03_Door_HMILoom", [(-.17, -.196, 1.54), (-.17, -.155, 1.46),
                                 (-.44, -.159, 1.46), (-.45, -.210, 1.15),
                                 (-.54, -.210, 1.12)], .009, M["black"], g)
    pipe("CAB_03_Door_BondingStrap", [(-.47, -.238, .36), (-.515, -.238, .36),
                                      (-.542, -.238, .405), (-.562, -.238, .48)],
         .0030, M["wire_pe"], g)
    for x, z in ((-.47, .36), (-.562, .48)):
        screw("CAB_03_Door_BondingScrew", x, -.244, z, g)


def breaker(x, z, index, g):
    key = f"q{index}"
    box(f"CAB_04_Breakers_Q{index}_MouldedCase", (x, .142, z),
        (.072, .079, .116), M["white"], g, .0008)
    box("CAB_04_Breakers_SteppedFront", (x, .091, z),
        (.070, .024, .076), M["white"], g, .0008)
    for dx in (-.0235, 0, .0235):
        box("CAB_04_Breakers_ToggleWell", (x+dx, .077, z+.009),
            (.016, .004, .031), M["black"], g, .0004)
        box("CAB_04_Breakers_Toggle", (x+dx, .069, z+.013),
            (.016, .013, .012), M["black"], g, .0007)
        for dz in (-.046, .046):
            cylinder("CAB_04_Breakers_TerminalWell", (x+dx, .099, z+dz),
                     .0065, .004, M["black"], g, axis=FRONT, segments=12, bevel=0)
            screw("CAB_04_Breakers_TerminalScrew", x+dx, .096, z+dz, g, .0031)
    for dx in (-.012, .012):
        box("CAB_04_Breakers_PoleJoin", (x+dx, .078, z),
            (.0005, .0004, .072), M["grey"], g, 0)
    box("CAB_04_Breakers_LinkedToggleBar", (x, .062, z+.013),
        (.064, .008, .007), M["black"], g, .0005)
    printed("CAB_04_Breakers_TechnicalPrint", "mcb", x, .078, z-.023, .062, .025, g)
    plaque("CAB_04_Breakers_ID", key, x, .184, z+.097, .085, g)


def breakers():
    g = groups[4]
    din_rail("CAB_04_Breakers_UpperDIN", -.08, 1.674, .85, g)
    din_rail("CAB_04_Breakers_ContactorDIN", -.08, 1.341, .85, g)
    box("CAB_04_Breakers_MainSwitchBody", (-.357, .139, 1.67),
        (.175, .110, .190), M["grey"], g, .002)
    box("CAB_04_Breakers_MainSwitchFace", (-.357, .076, 1.67),
        (.148, .016, .139), M["black"], g, .002)
    box("CAB_04_Breakers_MainSwitchToggle", (-.357, .056, 1.68),
        (.065, .026, .034), M["black"], g, .002)
    plaque("CAB_04_Breakers_Q0Tag", "q0", -.357, .064, 1.793, .184, g)
    for i in range(6):
        breaker(-.167+i*.100, 1.674, i+1, g)
    for i in range(3):
        x = -.33+i*.23
        box(f"CAB_04_Breakers_K{i+1}_ContactorBody", (x, .137, 1.34),
            (.117, .099, .15), M["black"], g, .001)
        box("CAB_04_Breakers_ContactorFace", (x, .077, 1.34),
            (.111, .025, .089), M["grey"], g, .0007)
        printed("CAB_04_Breakers_ContactorData", "contactor", x, .064, 1.335,
                .088, .055, g)
        box("CAB_04_Breakers_ContactorTestActuator", (x, .060, 1.369),
            (.016, .008, .012), M["black"], g, .0005)
        plaque("CAB_04_Breakers_ContactorTag", f"k{i+1}", x, .175, 1.452, .156, g)
        for dx in (-.045, 0, .045):
            for dz in (-.060, .060):
                box("CAB_04_Breakers_RecessedTerminal", (x+dx, .084, 1.34+dz),
                    (.023, .015, .024), M["grey"], g, .0005)
                screw("CAB_04_Breakers_ContactorTerminal", x+dx, .074, 1.34+dz, g, .0036)
    box("CAB_04_Breakers_ThermalOverload", (-.33, .122, 1.208),
        (.135, .10, .086), M["grey"], g, .001)
    cylinder("CAB_04_Breakers_OverloadDial", (-.36, .065, 1.213), .018,
             .010, M["blue"], g, axis=FRONT, segments=24)
    box("CAB_04_Breakers_ResetTab", (-.29, .065, 1.21), (.026, .012, .025),
        M["red"], g, .001)
    plaque("CAB_04_Breakers_OverloadTag", "fr", -.33, .064, 1.148, .16, g)
    # Insulated 3-phase comb bus below the upper DIN line.
    box("CAB_04_Breakers_CombInsulator", (.083, .186, 1.554),
        (.60, .025, .025), M["black"], g, .001)
    for i in range(6):
        box("CAB_04_Breakers_CopperCombTooth", (-.167+i*.100, .182, 1.582),
            (.009, .006, .038), M["copper"], g, .0005)
    plaque("CAB_04_Breakers_L1L2L3", "wireL1", -.357, .070, 1.546, .09, g)


def plc():
    g = groups[5]
    din_rail("CAB_05_PLC_ControlDIN", -.04, .967, .91, g)
    box("CAB_05_PLC_CPUCase", (-.29, .123, .962), (.132, .124, .191),
        M["grey"], g, .0015)
    box("CAB_05_PLC_CPUFace", (-.29, .052, .962), (.119, .018, .149),
        M["grey"], g, .001)
    printed("CAB_05_PLC_CPUSilkscreen", "cpu", -.29, .0425, .972, .103, .085, g)
    for dz in (-.064, .064):
        box("CAB_05_PLC_CPUConnector", (-.29, .043, .962+dz),
            (.103, .013, .020), M["black"], g, .0005)
    for j in range(3):
        cylinder("CAB_05_PLC_CPUStatusLED", (-.327+j*.011, .040, .993),
                 .0015, .002, M["green_lens"], g, axis=FRONT, segments=8, bevel=0)
    frame("CAB_05_PLC_CPUProfinetShield", -.29, .039, .902, .026, .024,
          .0025, .005, M["zinc"], g)
    box("CAB_05_PLC_CPUProfinetSocket", (-.29, .039, .902),
        (.020, .005, .018), M["black"], g, 0)
    for i in range(4):
        x = -.177+i*.062
        box("CAB_05_PLC_IOModuleCase", (x, .123, .962), (.057, .124, .191),
            M["grey"], g, .001)
        box("CAB_05_PLC_IOModuleFace", (x, .052, .962), (.052, .018, .149),
            M["grey"], g, .0008)
        printed("CAB_05_PLC_IOSilkscreen", "io", x, .0425, .972, .045, .085, g)
        for dz in (-.056, .056):
            box("CAB_05_PLC_PlugInTerminal", (x, .044, .962+dz),
                (.047, .010, .024), M["black"], g, .0005)
        for j in range(4):
            cylinder("CAB_05_PLC_IOStatusLED", (x-.015+j*.010, .040, .962),
                     .002, .002, M["green_lens"], g, axis=FRONT, segments=8, bevel=0)
    g = groups[9]
    box("CAB_09_AuxPowerNetwork_PowerSupply", (.27, .125, .960), (.158, .12, .196),
        M["zinc"], g, .0015)
    box("CAB_05_PLC_PowerSupplyFace", (.27, .057, .960), (.140, .013, .177),
        M["grey"], g, .001)
    printed("CAB_09_AuxPowerNetwork_PowerData", "supply", .27, .049, 1.01,
            .125, .052, g)
    for i in range(7):
        box("CAB_05_PLC_PowerSupplyVent", (.218+i*.017, .048, .944),
            (.005, .002, .045), M["black"], g, .0005)
    box("CAB_05_PLC_PowerTerminal", (.27, .046, .888), (.11, .016, .025),
        M["teal"], g, .0005)
    din_rail("CAB_05_PLC_NetworkDIN", -.02, .686, .92, g)
    box("CAB_05_PLC_EthernetSwitch", (-.18, .122, .690), (.19, .12, .13),
        M["blue"], g, .0015)
    plaque("CAB_05_PLC_SwitchTag", "switch", -.18, .051, .729, .17, g)
    for i in range(5):
        x = -.246+i*.033
        frame("CAB_05_PLC_RJ45Shield", x, .055, .671, .028, .030,
              .003, .01, M["zinc"], g)
        box("CAB_05_PLC_RJ45Socket", (x, .056, .671), (.020, .007, .022),
            M["black"], g, .0005)
    box("CAB_05_PLC_AuxTerminalStrip", (.207, .135, .690), (.23, .074, .079),
        M["black"], g, .001)
    for i in range(8):
        x = .112+i*.027
        box("CAB_05_PLC_AuxTerminal", (x, .091, .69), (.023, .02, .065),
            M["teal"], g, .0006)
        screw("CAB_05_PLC_AuxScrew", x, .078, .702, g, .0025)
    plaque("CAB_05_PLC_EthLabel", "ethernet", -.18, .051, .596, .17, g)


def cooling():
    g = groups[6]
    # The complete filter-fan cartridge travels with the door hinge.
    x, z = -.17, .553
    frame("CAB_06_Cooling_FilterFrame", x, -.290, z, .267, .267,
          .023, .019, M["black"], g)
    frame("CAB_06_Cooling_CartridgeFrame", x, -.253, z, .229, .229,
          .012, .056, M["grey"], g)
    box("CAB_06_Cooling_FilterFleece", (x, -.274, z), (.212, .004, .212),
        M["filter"], g, .0005)
    for i in range(11):
        slat = box("CAB_06_Cooling_AngledLouver", (x, -.303, z-.104+i*.0208),
                   (.217, .017, .006), M["grey"], g, .0007)
        slat.rotation_euler.x = math.radians(24)
    ring("CAB_06_Cooling_FanShroud", (x, -.213, z), .099, .090, .026,
         M["black"], g, axis=FRONT, segments=48)
    cylinder("CAB_06_Cooling_FanHub", (x, -.209, z), .025, .028,
             M["black"], g, axis=FRONT, segments=48)
    for i in range(7):
        a = math.tau*i/7
        shape = [(.024, -.005), (.088, -.014), (.091, .011), (.038, .029)]
        verts = []
        for y in (-.219, -.216):
            for r, tangent in shape:
                verts.append((x+r*math.cos(a)-tangent*math.sin(a), y,
                              z+r*math.sin(a)+tangent*math.cos(a)))
        mesh("CAB_06_Cooling_FanBlade", verts,
             [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4),
              (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], M["black"], g, .0005)
    for r in (.036, .061, .086):
        torus("CAB_06_Cooling_FanSafetyRing", (x, -.194, z), r, .0015,
              M["zinc"], g, axis=FRONT, segments=32, sides=4)
    for sign in (-1, 1):
        box("CAB_06_Cooling_FanSafetySpoke", (x, -.194, z),
            (.185, .003, .003), M["zinc"], g, .0005).rotation_euler.y = sign*.75
    for dx in (-.113, .113):
        for dz in (-.113, .113):
            screw("CAB_06_Cooling_CaptiveScrew", x+dx, -.303, z+dz, g, .0035)
    plaque("CAB_06_Cooling_FilterTag", "fan", x, -.311, z-.148, .24, g)
    box("CAB_06_Cooling_Thermostat", (.026, -.219, .722), (.074, .06, .108),
        M["white"], g, .001)
    cylinder("CAB_06_Cooling_ThermostatDial", (.026, -.182, .722), .021,
             .014, M["blue"], g, axis=(0, 1, 0), segments=24)


def wire(name, points, mat, radius=.0022, group=None):
    # Keep curved elbows while allocating only six radial vertices to thin wire.
    curve = bpy.data.curves.new("WirePath", "CURVE")
    curve.dimensions = "3D"
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points)-1)
    for point, co in zip(spline.bezier_points, points):
        point.co = co
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    path = bpy.data.objects.new("WirePath", curve)
    bpy.context.scene.collection.objects.link(path)
    bpy.context.view_layer.update()
    centers = []
    for a, b in zip(spline.bezier_points, list(spline.bezier_points)[1:]):
        centers.extend(interpolate_bezier(a.co, a.handle_right, b.handle_left, b.co, 2)[:-1])
    centers.append(spline.bezier_points[-1].co.copy())
    vertices = []
    previous = None
    for i, center in enumerate(centers):
        tangent = (centers[min(i+1, len(centers)-1)]-centers[max(i-1, 0)]).normalized()
        if previous is None:
            reference = Vector((1, 0, 0)) if abs(tangent.x) < .9 else Vector((0, 1, 0))
            u = tangent.cross(reference).normalized()
        else:
            u = (previous-tangent*previous.dot(tangent)).normalized()
        v = tangent.cross(u).normalized()
        previous = u
        for j in range(4):
            angle = math.tau*j/4
            vertices.append(center+radius*(u*math.cos(angle)+v*math.sin(angle)))
    faces = [tuple(reversed(range(4))), tuple(range((len(centers)-1)*4, len(centers)*4))]
    faces += [(i*4+j, i*4+(j+1) % 4, (i+1)*4+(j+1) % 4, (i+1)*4+j)
              for i in range(len(centers)-1) for j in range(4)]
    bpy.data.objects.remove(path, do_unlink=True)
    bpy.data.curves.remove(curve)
    return mesh(name, vertices, faces, mat, group or groups[7], 0)


def duct(name, x, z, width, height, g, vertical=False):
    box(name+"_Base", (x, .209, z), (width, .008, height), M["grey"], g, .0007)
    if vertical:
        for s in (-1, 1):
            box(name+"_Wall", (x+s*(width-.004)/2, .184, z),
                (.004, .047, height), M["grey"], g, .0006)
        box(name+"_RemovableLid", (x, .155, z), (width+.004, .005, height),
            M["white"], g, .0008)
        for i in range(int(height/.024)):
            for s in (-1, 1):
                box(name+"_Slot", (x+s*width/2, .175, z-height/2+.014+i*.024),
                    (.001, .020, .006), M["black"], g, 0)
    else:
        # Expose a short slotted section at the left so the wiring remains visible.
        lid_left = x+width*.14-width*.70/2
        for i in range(int(width/.024)):
            finger_x = x-width/2+.012+i*.024
            if finger_x+.007 >= lid_left:
                continue
            for s in (-1, 1):
                box(name+"_CombFinger", (finger_x, .184, z+s*(height-.004)/2),
                    (.014, .047, .004), M["grey"], g, .0005)
        covered_right = x+width/2
        for s in (-1, 1):
            box(name+"_CoveredWall", ((lid_left+covered_right)/2, .184, z+s*(height-.004)/2),
                (covered_right-lid_left, .047, .004), M["grey"], g, .0005)
        box(name+"_PartialLid", (x+width*.14, .155, z),
            (width*.70, .005, height+.004), M["white"], g, .0008)


def wiring():
    g = groups[7]
    for x in (-.49, .485):
        duct("CAB_07_Wiring_VerticalTrunk", x, 1.04, .049, 1.46, g, True)
    for z in (1.866, 1.535, 1.108, .811, .572):
        duct("CAB_07_Wiring_SlottedTrunk", -.04, z, .85, .043, g)
    plaque("CAB_07_Wiring_TrunkLabel", "duct", -.01, .150, .811, .28, g)
    for i in range(6):
        x = -.167+i*.10
        for pole in range(3):
            px = x+(pole-1)*.0235
            color = (M["wire_brown"], M["black"], M["grey"])[pole]
            wire("CAB_07_Wiring_BreakerFeed",
                 [(px, .142, 1.732), (px, .134, 1.78+pole*.007),
                  (px-.012, .180, 1.829+pole*.006), (px-.012, .180, 1.866)], color, .0019)
            wire("CAB_07_Wiring_BreakerLoad",
                 [(px, .142, 1.616), (px, .135, 1.586-pole*.005),
                  (px-.010, .179, 1.535)], color, .0019)
    for i in range(3):
        x = -.33+i*.23
        for j in range(3):
            px = x+(j-1)*.045
            color = (M["wire_brown"], M["black"], M["grey"])[j]
            wire("CAB_07_Wiring_ContactorPower",
                 [(px, .133, 1.415), (px, .133, 1.47),
                  (px+.012, .180, 1.51), (px+.012, .180, 1.535)], color, .003)
    for i in range(20):
        x = -.45+i*.037
        color = M["wire_pe"] if i >= 17 else M["wire_blue"] if i % 3 else M["black"]
        wire(f"CAB_07_Wiring_X1_{i+1:02}_Signal",
             [(x, .147, .494), (x, .147, .521),
              (x-.008, .181, .548), (x-.008, .181, .572)], color, .0018)
        cylinder(f"CAB_07_Wiring_X1_{i+1:02}_MarkerSleeve", (x, .147, .515),
                 .0032, .014, M["white"], g, axis=(0, 0, 1), segments=8, bevel=0)
        wire(f"CAB_07_Wiring_X1_{i+1:02}_Field",
             [(x, .148, .430), (x, .147, .403), (x-.025, .183, .382)],
             color, .0018)
    for i in range(4):
        x = -.177+i*.062
        for j in range(3):
            px = x+(j-1)*.012
            wire("CAB_07_Wiring_IOInput",
                 [(px, .106, 1.056), (px, .106, 1.075),
                  (px+.005, .182, 1.096), (px+.005, .182, 1.108)],
                 M["wire_blue"], .0015)
            wire("CAB_07_Wiring_IOOutput",
                 [(px, .106, .866), (px, .106, .844),
                  (px+.005, .182, .828), (px+.005, .182, .811)],
                 M["black"], .0015)
    for i, x in enumerate((.22, .25, .28, .31)):
        wire("CAB_07_Wiring_24VPower",
             [(x, .100, .864), (x, .100, .84),
              (x+.012, .181, .823), (x+.012, .181, .811)],
             M["wire_blue"] if i % 2 else M["red"], .002)
    wire("CAB_07_Wiring_EthernetCable",
         [(-.246, .036, .671), (-.246, -.017, .66),
          (-.37, -.014, .72), (-.40, .035, .82),
          (-.29, .034, .90)], M["wire_pe"], .004)
    # Staged, clamped field bundle from terminal trunk to real floor glands.
    for i in range(5):
        x = -.39+i*.14
        wire("CAB_07_Wiring_FieldCable",
             [(x, .182, .382), (x, .12, .29),
              (x, .055, .19), (x, .055, .134)], M["black"], .007)
        box("CAB_07_Wiring_FieldCableClamp", (x, .053, .211),
            (.021, .024, .009), M["white"], g, .0008)
    for j in range(4):
        x = -.405+j*.023
        wire("CAB_07_Wiring_BundledSupply",
             [(x, .180, 1.86), (x, .183, 1.74),
              (x, .183, 1.59), (x+.03, .150, 1.58)],
             (M["wire_brown"], M["black"], M["grey"], M["wire_blue"])[j], .003)
    for z in (1.63, 1.77):
        box("CAB_07_Wiring_CableTie", (-.370, .181, z),
            (.10, .013, .005), M["black"], g, .0005)
    wire("CAB_07_Wiring_PEBackplate",
         [(.46, .206, .32), (.40, .172, .30), (.24, .171, .325)],
         M["wire_pe"], .003)
    for i in range(8):
        x = -.37+i*.052
        wire("CAB_07_Wiring_EarthBranch",
             [(x, .143, .325), (x, .122, .35),
              (x-.014, .177, .376)], M["wire_pe"], .0018)


def model_objects(scene):
    return [o for o in scene.objects if o.type == "MESH" and o.get("equipment_mesh")]


def camera(scene, name, pos, target, scale):
    data = bpy.data.cameras.new(name)
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = pos
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()
    data.type = "PERSP"
    data.lens = 64
    distance = (Vector(pos)-Vector(target)).length
    obj.location = Vector(target)+(Vector(pos)-Vector(target)).normalized()*scale*64/36
    data.clip_start = .05
    obj.hide_set(True)
    scene.camera = obj
    return obj


def light_studio(scene):
    studio(scene, M["floor"], box)
    old_floor = scene.objects.get(scene.name+"_STUDIO_FLOOR")
    if old_floor:
        bpy.data.objects.remove(old_floor, do_unlink=True)
    profile = [(-100, -.004), (3, -.004)]
    for i in range(1, 33):
        a = math.pi*.5*i/32
        profile.append((3+3*math.sin(a), 2.996-3*math.cos(a)))
    profile.append((6, 15))
    vertices = [(x, y, z) for y, z in profile for x in (-40, 40)]
    faces = [(2*i, 2*i+1, 2*i+3, 2*i+2) for i in range(len(profile)-1)]
    backdrop_data = bpy.data.meshes.new(scene.name+"_Cyclorama")
    backdrop_data.from_pydata(vertices, [], faces)
    backdrop_data.materials.append(M["floor"])
    for face in backdrop_data.polygons:
        face.use_smooth = True
    backdrop = bpy.data.objects.new(backdrop_data.name, backdrop_data)
    scene.collection.objects.link(backdrop)
    backdrop.hide_select = True
    scene.render.resolution_x, scene.render.resolution_y = 2400, 1600
    scene.cycles.samples = 96
    scene.cycles.adaptive_threshold = .018
    scene.view_settings.exposure = -.2
    world_nodes, world_links = scene.world.node_tree.nodes, scene.world.node_tree.links
    bg = world_nodes.get("Background")
    bg.inputs["Strength"].default_value = .32
    camera_bg = world_nodes.new("ShaderNodeBackground")
    camera_bg.inputs["Color"].default_value = (.16, .175, .19, 1)
    camera_bg.inputs["Strength"].default_value = .8
    ray = world_nodes.new("ShaderNodeLightPath")
    mix = world_nodes.new("ShaderNodeMixShader")
    world_links.new(ray.outputs["Is Camera Ray"], mix.inputs[0])
    world_links.new(bg.outputs[0], mix.inputs[1])
    world_links.new(camera_bg.outputs[0], mix.inputs[2])
    world_links.new(mix.outputs[0], world_nodes.get("World Output").inputs[0])
    for obj in scene.objects:
        if obj.type == "LIGHT":
            # Tall reflectors reveal the brushing on cabinet panels.
            if "WindowKey" in obj.name:
                obj.data.size, obj.data.size_y = 1.7, 3.2
                obj.data.energy = 500
            if "SoftFill" in obj.name:
                obj.data.energy = 180
            if "CeilingStrip" in obj.name:
                obj.data.energy = 180
            if "MetalEdge" in obj.name:
                obj.data.energy = 160
    fill_data = bpy.data.lights.new(scene.name+"_FrontReflector", "AREA")
    fill_data.energy, fill_data.shape = 165, "RECTANGLE"
    fill_data.size, fill_data.size_y = 1.4, 2.8
    fill_obj = bpy.data.objects.new(fill_data.name, fill_data)
    scene.collection.objects.link(fill_obj)
    fill_obj.location = (.2, -4.0, 2.3)
    fill_obj.rotation_euler = (Vector((0, .1, 1.1))-fill_obj.location).to_track_quat("-Z", "Y").to_euler()
    fill_obj.hide_select = True
    scene.render.image_settings.color_mode = "RGB"


def clone_state(source, name, open_door=False, explode=False):
    scene = bpy.data.scenes.new(name)
    scene.unit_settings.system = "METRIC"
    bpy.context.window.scene = scene
    copies = {}
    originals = [o for o in source.objects if o == root or o in groups or o.get("equipment_mesh")]
    for original in originals:
        obj = original.copy()
        obj.animation_data_clear()
        scene.collection.objects.link(obj)
        copies[original] = obj
    for original, obj in copies.items():
        obj.parent = copies.get(original.parent)
        obj.matrix_parent_inverse = original.matrix_parent_inverse.copy()
        obj.matrix_basis = original.matrix_basis.copy()
    for i, g in enumerate(groups):
        obj = copies[g]
        if open_door and i in DOOR_ATTACHED:
            obj.rotation_euler.z = DOOR_ANGLE
        if explode:
            obj.location += Vector(OFFSETS[i])
    light_studio(scene)
    return scene, copies


def set_pivots():
    for i in DOOR_ATTACHED:
        group = groups[i]
        bpy.context.view_layer.update()
        children = [(obj, obj.matrix_world.copy()) for obj in group.children]
        group.location = HINGE
        bpy.context.view_layer.update()
        for obj, world in children:
            obj.matrix_world = world
        group["hinge_axis"] = "Blender Z / Unity Y"
        group["hinge_origin_m"] = list(HINGE)
        group["open_angle_deg"] = -100


def export_fbx(scene):
    bpy.context.window.scene = scene
    scene.frame_set(1)
    bpy.ops.object.select_all(action="DESELECT")
    export_objects = [o for o in scene.objects if o == root or o in groups or o.get("equipment_mesh")]
    for obj in export_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    FBX.parent.mkdir(parents=True, exist_ok=True)
    # Structural empties carry the required root/groups. Cameras, lamps and
    # unrelated empties are excluded explicitly through the selected hierarchy.
    bpy.ops.export_scene.fbx(
        filepath=str(FBX), use_selection=True, object_types={"MESH", "EMPTY"},
        global_scale=1, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=True,
        use_triangles=False, add_leaf_bones=False, bake_anim=False,
        path_mode="COPY", embed_textures=True, use_custom_props=True,
    )
    bpy.ops.object.select_all(action="DESELECT")
    manifest = {
        "root": root.name, "units": "metres", "source_front": "-Y",
        "unity_front": "+Z", "groups": list(GROUP_NAMES),
        "hinge_source_xyz_m": list(HINGE), "hinge_unity_xyz_m": [HINGE[0], HINGE[2], -HINGE[1]],
        "door_angle_deg": -100,
        "export": {"up": "Y", "forward": "-Z", "apply_transform": True,
                   "scale": 1, "animation": False, "cameras": False, "lights": False,
                   "structural_empty_groups": True, "textures_embedded": True},
        "recommended_unity_importer": {
            "globalScale": 1, "useFileScale": True, "bakeAxisConversion": False,
            "meshCompression": 0, "addColliders": False, "isReadable": False,
            "normalSmoothAngle": 60, "materialImportMode": 2,
        },
        "materials": [{"name": m.name, "metallic": m.get("pbr_metallic"),
                       "roughness": m.get("pbr_roughness")} for m in M.values()],
    }
    (REPORT / "electrical-cabinet-export.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8")
    log("FBX_SAVED "+str(FBX))


def viewport():
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.shading.type = "MATERIAL"
                    space.shading.use_scene_world = True
                    space.shading.use_scene_lights = True
                    space.overlay.show_relationship_lines = False
                    space.overlay.show_extras = False
                    space.region_3d.view_location = (0, 0, 1)
                    space.region_3d.view_rotation = Vector((3, -5, 2.2)).to_track_quat("Z", "Y")
                    space.region_3d.view_distance = 3.7
    bpy.ops.object.select_all(action="DESELECT")


def build():
    global root, groups
    for folder in (SOURCE.parent, PREVIEW, REPORT, FBX.parent):
        folder.mkdir(parents=True, exist_ok=True)
    if SOURCE.exists():
        shutil.copy2(SOURCE, SOURCE.with_name(
            "electrical-cabinet-before-"+datetime.now().strftime("%Y%m%d-%H%M%S")+".blend"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "01_ASSEMBLED"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    create_materials()
    root = empty("electrical-cabinet")
    root["hinge_axis"] = "Blender Z / Unity Y"
    root["front"] = "Blender -Y / Unity +Z"
    root["asset_note"] = "Original educational model / no manufacturer rating or electrical certification"
    groups = [empty(name, root) for name in GROUP_NAMES]
    from cabinet_v3 import rebuild
    for builder in (enclosure, backplate, door, cooling):
        log("BUILD "+builder.__name__)
        builder()
    rebuild(sys.modules[__name__])
    for obj in list(scene.objects):
        if obj.type == "MESH":
            cleanup(obj)
    bpy.context.view_layer.update()
    for obj in scene.objects:
        if obj.type == "MESH":
            obj["equipment_mesh"] = True
    set_pivots()
    meshes = model_objects(scene)
    log(f"UV_UNWRAP {len(meshes)} meshes")
    uv = unwrap([o for o in meshes if not (o.get("label_key") or o.get("screen_uv") or o.get("print_kind"))])
    atlas = json.loads((TEX / "labels.json").read_text(encoding="utf-8"))
    for obj in meshes:
        if obj.get("label_key"):
            label_uv(obj, atlas[obj["label_key"]])
        elif obj.get("screen_uv"):
            label_uv(obj, (0, .21875, 1, .78125))
        elif obj.get("print_kind"):
            ratio = obj["print_height_m"]/obj["label_width_m"]
            rect = (0, (1-ratio)/2, 1, (1+ratio)/2) if ratio <= 1 else (
                (1-1/ratio)/2, 0, (1+1/ratio)/2, 1)
            label_uv(obj, rect)
    (REPORT / "electrical-cabinet-build-uv.json").write_text(
        json.dumps(uv, indent=2), encoding="utf-8")
    light_studio(scene)
    camera(scene, "CAM_Assembled", (2.7, -7, 2.65), (0, 0, 1.0), 3.75)
    opened, _ = clone_state(scene, "02_DOOR_OPEN", True)
    camera(opened, "CAM_Open", (1.7, -7, 2.75), (-.25, -.22, 1.0), 4.05)
    exploded, _ = clone_state(scene, "03_EXPLODED", True, True)
    camera(exploded, "CAM_Exploded", (3.1, -6.5, 3.1), (.03, -.20, 1.02), 4.95)
    internal, internal_copies = clone_state(scene, "04_INTERNAL", True)
    internal_copies[groups[8]].location.x += 1.6
    camera(internal, "CAM_Internal", (.5, -7, 2.3), (.0, .10, 1.08), 2.95)
    front, _ = clone_state(scene, "06_FRONT")
    camera(front, "CAM_Front", (-2.3, -7, 2.6), (-.11, -.24, 1.40), 2.05)
    fault, fault_copies = clone_state(scene, "05_ELECTRICAL_FAULT")
    for original, obj in fault_copies.items():
        if original.get("screen_uv"):
            obj.data = original.data.copy()
            obj.data.materials[1] = M["hmi_fault"]
        if original.name == "cabinet_lamp_lens_1":
            obj.data = original.data.copy()
            obj.data.materials[0] = M["red_lens"]
    camera(fault, "CAM_Fault", (3.5, -6, 3.1), (0, 0, 1.0), 3.55)
    bpy.context.window.scene = scene
    animate(scene, groups)
    scene.frame_set(1)
    viewport()
    render_device()
    scene["review_scenes"] = "01_ASSEMBLED / 02_DOOR_OPEN / 03_EXPLODED / 04_INTERNAL / 05_ELECTRICAL_FAULT / 06_FRONT"
    scene["review_status"] = "Redesigned revision 3 / 16 functional assemblies / pending user approval / Unity deferred"
    scene["uv_method"] = uv["method"]
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE), check_existing=False)
    log("BUILD_SAVED "+str(SOURCE))


def render(names=None, draft=False, prefix=""):
    states = (
        ("01_ASSEMBLED", "assembled.png"), ("02_DOOR_OPEN", "door-open.png"),
        ("03_EXPLODED", "exploded.png"), ("04_INTERNAL", "internal-detail.png"),
        ("05_ELECTRICAL_FAULT", "electrical-fault.png"),
        ("06_FRONT", "front.png"),
    )
    render_device()
    for state, filename in states:
        if names and state not in names:
            continue
        scene = bpy.data.scenes[state]
        bpy.context.window.scene = scene
        if draft:
            scene.render.resolution_percentage = 50
            scene.cycles.samples = 24
        else:
            scene.render.resolution_percentage = 100
            scene.cycles.samples = 64
        scene.render.filepath = str(
            PREVIEW / (prefix+("draft-" if draft else "")+filename))
        bpy.ops.render.render(write_still=True)
        log("RENDERED "+filename)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--render", action="store_true")
    parser.add_argument("--render-only", action="store_true")
    parser.add_argument("--draft", action="store_true")
    parser.add_argument("--states", nargs="*")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--preview-prefix", default="")
    options = parser.parse_args(args)
    if options.output:
        SOURCE = options.output.resolve()
    if not options.render_only:
        build()
    if options.render or options.render_only:
        render(options.states, options.draft, options.preview_prefix)
