"""Read-only measurements for the cabinet wiring revision."""
import json
from collections import defaultdict

import bpy
from mathutils import Vector

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
totals = defaultdict(lambda: [0, 0])
parts = []
for obj in scene.objects:
    if obj.type != "MESH" or not obj.get("equipment_mesh"):
        continue
    obj.data.calc_loop_triangles()
    category = obj.name.split(".")[0]
    totals[category][0] += len(obj.data.loop_triangles)
    totals[category][1] += 1
    if any(k in obj.name for k in ("PLCChannel", "FieldTermination", "ShieldedField",
                                   "IndustrialEthernet", "Pilot", "Isolator", "Estop",
                                   "TerminalUpper", "FieldNetwork", "Saddle")):
        coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
        parts.append({
            "name": obj.name,
            "vertices": len(coords),
            "first_vertices": [list(v) for v in coords[:6]],
            "center": list(sum(coords, Vector()) / len(coords)),
            "min": [min(v[i] for v in coords) for i in range(3)],
            "max": [max(v[i] for v in coords) for i in range(3)],
            "materials": [m.name for m in obj.data.materials if m],
            "matrix": [list(row) for row in obj.matrix_world],
        })
materials = {}
for mat in bpy.data.materials:
    if mat.name.startswith("CAB /") and mat.use_nodes:
        shader = next((n for n in mat.node_tree.nodes
                       if n.type == "BSDF_PRINCIPLED"), None)
        if shader:
            materials[mat.name] = {
                k: (list(shader.inputs[k].default_value)
                    if hasattr(shader.inputs[k].default_value, "__len__")
                    else shader.inputs[k].default_value)
                for k in ("Base Color", "Roughness", "Metallic", "Coat Weight",
                          "Emission Strength", "Transmission Weight",
                          "Specular IOR Level")
            }
print("CABINET_INSPECTION " + json.dumps({
    "triangles": sorted(totals.items(), key=lambda item: -item[1][0])[:80],
    "parts": parts,
    "materials": materials,
}))
