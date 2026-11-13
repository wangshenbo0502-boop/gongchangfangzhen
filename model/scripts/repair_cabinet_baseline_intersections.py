"""Repair the duplicated door duct and HMI loom route in the reviewed source."""
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)

# The HMI assembly already carries its own covered door duct.
for obj in list(scene.objects):
    if obj.type == "MESH" and obj.name.startswith("CAB_03_Door_InnerCableTrunk"):
        bpy.data.objects.remove(obj, do_unlink=True)

# Route the flexible loop in front of the hinge upright instead of through it.
name = "CAB_15_OperatorHMI_HMIPowerLoom"
old = bpy.data.objects.get(name)
if old is None:
    raise RuntimeError(f"Missing source cable: {name}")
material = old.data.materials[0] if old.data.materials else None
parent = bpy.data.objects["CAB_15_OperatorHMI"]
bpy.data.objects.remove(old, do_unlink=True)

curve = bpy.data.curves.new(name, "CURVE")
curve.dimensions = "3D"
curve.resolution_u = 2
curve.bevel_depth = 0.006
curve.bevel_resolution = 2
curve.use_fill_caps = True
spline = curve.splines.new("BEZIER")
points = [
    (-0.180, -0.197, 1.400),
    (-0.180, -0.211, 1.305),
    (-0.343, -0.211, 1.305),
    (-0.506, -0.211, 1.305),
    (-0.506, -0.211, 0.750),
    (-0.506, -0.211, 0.390),
    (-0.548, -0.252, 0.390),
    (-0.588, -0.258, 0.390),
    (-0.574, -0.279, 0.390),
]
spline.bezier_points.add(len(points) - 1)
for point, coordinate in zip(spline.bezier_points, points):
    point.co = coordinate
    point.handle_left_type = "AUTO"
    point.handle_right_type = "AUTO"
cable = bpy.data.objects.new(name, curve)
scene.collection.objects.link(cable)
cable.parent = parent
cable.matrix_parent_inverse = parent.matrix_world.inverted()
if material:
    cable.data.materials.append(material)
cable["equipment_mesh"] = True
cable["component_group"] = parent.name

blend = Path(bpy.data.filepath)
bpy.ops.wm.save_as_mainfile(filepath=str(blend))
print("CABINET_INTERSECTION_REPAIR",
      {"removedDoorTrunks": True, "reroutedHmiLoom": True, "source": str(blend)},
      flush=True)
