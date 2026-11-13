"""Apply the small HMI lead attachment detail to a review candidate."""
import bpy
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from industrial_parts import box

target = Path(bpy.data.filepath)
if not target.name.endswith("routing-review-v2.blend"):
    raise RuntimeError(f"Unexpected review candidate: {target}")

starts = {
    "CAB_15_OperatorHMI_HMIPowerLoom": (-.18, 1.40),
    "CAB_15_OperatorHMI_EthernetLead": (-.05, 1.40),
}
seen_meshes = set()
for scene in bpy.data.scenes:
    bpy.context.window.scene = scene
    for name, (x, z) in starts.items():
        obj = scene.objects.get(name)
        if not obj or obj.type != "MESH" or obj.data.as_pointer() in seen_meshes:
            continue
        seen_meshes.add(obj.data.as_pointer())
        inverse = obj.matrix_world.inverted()
        for vertex in obj.data.vertices:
            world = obj.matrix_world @ vertex.co
            if abs(world.x-x) < .006 and world.z > z-.008:
                world.y -= .006
                vertex.co = inverse @ world
        obj.data.update()
    group = scene.objects.get("CAB_15_OperatorHMI")
    if group and not scene.objects.get("CAB_15_OperatorHMI_EthernetLeadSaddle"):
        box("CAB_15_OperatorHMI_EthernetLeadSaddle",
            (-.12, -.184, 1.305), (.014, .008, .012),
            bpy.data.materials["CAB / zinc"], group, .001)

bpy.ops.wm.save_as_mainfile(filepath=str(target), check_existing=False)
print("CABINET_REVIEW_TOUCHUP", target, flush=True)
