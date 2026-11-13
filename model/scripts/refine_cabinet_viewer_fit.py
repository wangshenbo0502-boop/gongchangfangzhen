"""Create the viewer's fitted panels and roof hardware from the reviewed source."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

MODEL = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(MODEL / "scripts"))
import build_cabinet as cabinet

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
objects = bpy.data.objects
frame = objects["CAB_00_Enclosure"]
door = objects["CAB_03_Door"]
paint = bpy.data.materials["CAB / steel"]
black = bpy.data.materials["CAB / black"]
zinc = bpy.data.materials["CAB / zinc"]


def extend_edges(name, limits):
    obj = objects[name]
    obj.data = obj.data.copy()
    to_local = obj.matrix_world.inverted()
    for vertex in obj.data.vertices:
        point = obj.matrix_world @ vertex.co
        for axis, old, new in limits:
            if abs(point[axis] - old) < .001:
                point[axis] += new - old
        vertex.co = to_local @ point
    obj.data.update()


extend_edges("CAB_03_Door_1p5mmFoldedPanel",
             ((0, .286, .297), (2, .178, .170), (2, 1.927, 1.954)))
extend_edges("CAB_08_ControlPanel_FixedServiceStrip",
             ((2, .173, .170), (2, 1.930, 1.954)))

# These old projecting frame screws sat ahead of the new seats, in the
# opening paths. The replacement lips sit behind the faces.
for suffix in ("", ".001", ".002", ".003"):
    for kind in ("Head", "Slot"):
        bpy.data.objects.remove(objects["CAB_00_Enclosure_FrameScrew_" + kind + suffix], do_unlink=True)


def mark_equipment(obj):
    obj["equipment_mesh"] = True
    for name in ("UVMap", "FinishUV"):
        layer = obj.data.uv_layers.get(name) or obj.data.uv_layers.new(name=name)
        obj.data.uv_layers.active = layer
        for face in obj.data.polygons:
            cabinet.planar_face(obj.data, face)
    obj.data.uv_layers.active_index = 0
    return obj


def fold(name, center, size, material, bevel=.001, owner=frame):
    return mark_equipment(cabinet.box(owner.name + "_" + name, center, size, material, owner, bevel))


# Closed clearances are 5 mm at the center, 4 mm at the top, and
# 3.5 mm at the sill. Recessed overlaps block a view through the seams.
upper_return = objects["CAB_00_Enclosure_FrontReturn.001"]
upper_return.location += upper_return.parent.matrix_world.to_3x3().inverted() @ Vector((0, .027, 0))
fold("HeaderFascia", (0, -.282, 1.9635), (1.195, .036, .011), paint)
fold("HeaderStop", (0, -.259, 1.947), (1.176, .010, .027), black)
fold("SillStop", (0, -.255, .168), (1.170, .010, .018), black)
# This return is part of the removable service panel. It clears the internal
# extraction corridor together with the face rather than blocking the opening.
fold("MullionReturn", (.302, -.259, 1.055), (.050, .016, 1.750), paint, .0018,
     owner=objects["CAB_08_ControlPanel"])
fold("LeftJambFold", (-.592, -.260, 1.0615), (.013, .046, 1.794), paint)
fold("RightJambFold", (.5845, -.267, 1.0615), (.027, .037, 1.794), paint)

# Place a fixed hinge half on the jamb and keep the moving half on the door.
# The three knuckles interleave vertically, with a common outboard axis.
for obj in list(scene.objects):
    if obj.name.startswith(("CAB_03_Door_HingeLeaf", "CAB_03_Door_HingeKnuckle",
                            "CAB_03_Door_HingePin")):
        bpy.data.objects.remove(obj, do_unlink=True)

hinge_x, hinge_y = -.588, -.294
for i, z in enumerate((.45, 1.052, 1.75), 1):
    fold(f"HingeSeat_{i}", (-.592, -.274, z), (.012, .035, .021), zinc, .0006)
    for suffix, owner, center_z, length in (
        ("Fixed", frame, z, .022),
        ("Lower", door, z-.0255, .025),
        ("Upper", door, z+.0255, .025),
    ):
        mark_equipment(cabinet.ring(
            f"{owner.name}_HingeKnuckle_{i}_{suffix}", (hinge_x, hinge_y, center_z),
            .007, .0036, length, zinc, owner, axis=(0, 0, 1), segments=24, bevel=.0004))
        if owner == door:
            fold(f"HingeLeaf_{i}_{suffix}", (-.576, -.28725, center_z),
                 (.026, .0065, .025), zinc, .0006, owner=door)
            fold(f"HingeFoot_{i}_{suffix}", (-.568, -.2825, center_z),
                 (.010, .006, .024), zinc, .0005, owner=door)
    mark_equipment(cabinet.cylinder(
        f"{frame.name}_HingePin_{i}", (hinge_x, hinge_y, z), .0033, .022, zinc,
        frame, axis=(0, 0, 1), segments=20, bevel=.00025))

strap = objects["CAB_03_Door_BondingStrap"]
strap.location += strap.parent.matrix_world.to_3x3().inverted() @ Vector((.016, -.005, 0))
frame["viewer_hinge_origin_m"] = (hinge_x, hinge_y, .16)


def world_vertices(obj):
    return [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


roof = objects["CAB_00_Enclosure_TopPanel"]
roof_top = max(point.z for point in world_vertices(roof))
seat_top = roof_top + .0045
for i, x in enumerate((-.47, .47), 1):
    old = objects["CAB_00_Enclosure_LiftingLug" + ("" if i == 1 else ".001")]
    bpy.data.objects.remove(old, do_unlink=True)
    y, hole_z = .19, 1.988
    name = "CAB_00_Enclosure_LiftingLug_" + str(i)
    seat = fold("LiftingSeat_" + str(i), (x, y, roof_top + .00225), (.083, .036, .0045), zinc, .0006)
    # Flat base and rounded crown, with a real through hole across the plate.
    profile = [(x - .018, seat_top), (x + .018, seat_top), (x + .012, hole_z)]
    profile += [(x + .012 * math.cos(math.pi * step / 16),
                 hole_z + .012 * math.sin(math.pi * step / 16)) for step in range(1, 17)]
    n = len(profile)
    vertices = [(px, y + side * .004, z) for side in (-1, 1) for px, z in profile]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces += [(j, (j+1) % n, (j+1) % n + n, j+n) for j in range(n)]
    lug = cabinet.mesh(name, vertices, faces, zinc, frame, 0)
    cutter = cabinet.cylinder("LugBoreTool", (x, y, hole_z), .0075, .020, None,
                              axis=(0, 1, 0), segments=32, bevel=0)
    cabinet.boolean(lug, cutter)
    cabinet.finish(lug, name, zinc, frame, .00055, False)
    mark_equipment(lug)
    for side in (-1, 1):
        bx = x + side * .030
        prefix = name + ("_Left" if side < 0 else "_Right")
        washer = cabinet.cylinder(prefix + "Washer", (bx, y, seat_top + .00065), .0066, .0013,
                                   zinc, frame, axis=(0, 0, 1), segments=20, bevel=.00025)
        head = cabinet.cylinder(prefix + "BoltHead", (bx, y, seat_top + .00285), .0048, .0031,
                                 zinc, frame, axis=(0, 0, 1), segments=6, bevel=.00035)
        mark_equipment(washer)
        mark_equipment(head)
    bpy.context.view_layer.update()
    lug_points = world_vertices(lug)
    lug_bottom = min(point.z for point in lug_points)
    tree = BVHTree.FromPolygons(lug_points, [tuple(face.vertices) for face in lug.data.polygons])
    if (abs(lug_bottom - max(point.z for point in world_vertices(seat))) > 1e-5 or
            lug_bottom - roof_top < .0044 or
            tree.ray_cast(Vector((x, y-.015, hole_z)), Vector((0, 1, 0)), .03)[0] is not None):
        raise RuntimeError("Lifting ear does not seat cleanly or its through hole is obstructed: " + name)
    print("CABINET_LIFTING_LUG", name, f"roof clearance={(lug_bottom - roof_top)*1000:.2f}mm", "through-hole clear")

bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(MODEL / "source/electrical-cabinet-viewer-fit-review.blend"), compress=True)
