"""Update only assembly trajectories; preserve the reviewed meshes and finishes."""
import json
import shutil
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from pump_components import GROUP_NAMES, OFFSETS, animate_groups, manifest
from build_pump_detailed import bearing_housing, parent_at
from pump_surface_finish import assign_machining_coordinates
from pump_uv import unwrap

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
root = bpy.data.objects["water-pump"]
groups = sorted(root.children, key=lambda g: g.name)
assert tuple(g.name for g in groups) == GROUP_NAMES
home = [g.location.copy() for g in groups]
old = bpy.data.objects["PMP_06_SealBearing_BearingHousing"]
old_data = old.data
old.name = "REPLACED_BEARING_HOUSING"
housing = bearing_housing(groups[10], old.data.materials[0])
assign_machining_coordinates(housing)
assert unwrap([housing])["pass"]
for obj in bpy.data.objects:
    if obj.type == "MESH" and obj.data == old_data:
        obj.data = housing.data
bpy.data.objects.remove(housing, do_unlink=True)
old.name = "PMP_06_SealBearing_BearingHousing"
for item in bpy.data.scenes:
    if item.name not in ("01_ASSEMBLED", "02_EXPLODED", "03_INTERNAL"):
        continue
    bpy.context.window.scene = item
    for obj in list(item.objects):
        target = None
        if obj.name.startswith("PMP_01_Motor_FootBolt_HexNut"):
            target = 0
        if obj.name.startswith("PMP_05_InletCover_Bolt_") and "_HexNut" in obj.name:
            target = 4
        if target is not None:
            group = next(o for o in item.objects if o.type == "EMPTY" and
                         o.name.startswith(GROUP_NAMES[target]))
            # Exploded scenes already carry their old group offsets.
            if item != scene:
                original = next(o for o in scene.objects if o.data == obj.data)
                matrix = original.matrix_world.copy()
                parent_at(obj, group)
                obj.matrix_world = matrix
            else:
                parent_at(obj, group)
bpy.context.window.scene = scene
animate_groups(scene, groups)
data = manifest(groups, [o for o in scene.objects if o.get("equipment_mesh")])
root["component_contract"] = json.dumps(data, ensure_ascii=False)
for other in (bpy.data.scenes["02_EXPLODED"], bpy.data.scenes["03_INTERNAL"]):
    for index, name in enumerate(GROUP_NAMES):
        group = next(o for o in other.objects if o.type == "EMPTY" and o.name.startswith(name))
        group.animation_data_clear()
        group.location = home[index]+Vector(OFFSETS[index])
for marker in list(scene.timeline_markers):
    scene.timeline_markers.remove(marker)
for frame, name in ((1, "ASSEMBLED"), (19, "GUARD CLEAR"), (65, "COVER CLEAR"),
                    (105, "SHAFT CLEAR"), (150, "EXPLODED"), (170, "REASSEMBLY"),
                    (320, "ASSEMBLED")):
    scene.timeline_markers.new(name, frame=frame)
scene.frame_set(1)
source = Path(bpy.data.filepath)
shutil.copy2(source, source.with_name("water-pump-before-motion-" +
             datetime.now().strftime("%Y%m%d-%H%M%S")+".blend"))
(HERE.parent/"reports/water-pump-components.json").write_text(
    json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")
bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
print("PUMP_MOTION_SAVED", flush=True)
