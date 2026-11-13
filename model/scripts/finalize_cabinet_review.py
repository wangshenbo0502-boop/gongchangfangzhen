"""Synchronize review poses after a configuration-only cabinet revision."""
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from cabinet_components import GROUP_NAMES, HINGE, OFFSETS, animate

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
groups = [scene.objects[name] for name in GROUP_NAMES]
animate(scene, groups)
for frame in range(1, 282):
    scene.frame_set(frame)
    groups[6].rotation_euler.z = groups[3].rotation_euler.z
    groups[6].keyframe_insert("rotation_euler", frame=frame)
scene.frame_set(1)
exploded = bpy.data.scenes["03_EXPLODED"]
panel = next(o for o in exploded.objects
             if o.type == "EMPTY" and o.name.startswith(GROUP_NAMES[8]))
panel.location = OFFSETS[8]
for state in bpy.data.scenes:
    for obj in state.objects:
        if obj.type == "MESH" and obj.parent and obj.parent.name.startswith(GROUP_NAMES[9]):
            obj.name = obj.name.replace("CAB_05_PLC_", "CAB_09_AuxPowerNetwork_")
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath, check_existing=False)
print("CABINET_REVIEW_FINALIZED", flush=True)
