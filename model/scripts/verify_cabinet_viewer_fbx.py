"""Round-trip inspect the cabinet viewer FBX in a clean Blender scene."""
import json
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "CabinetViewerProject" / "Assets" / "ElectricalCabinet"
FBX = ASSETS / "electrical-cabinet.fbx"
PARTS = json.loads((ASSETS / "parts.json").read_text(encoding="utf-8"))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(FBX), use_anim=False)
root = bpy.data.objects.get("electrical-cabinet")
if not root:
    raise RuntimeError("FBX root missing")
objects = list(bpy.context.scene.objects)
groups = [bpy.data.objects.get(part["id"]) for part in PARTS["parts"]]
if any(group is None for group in groups):
    raise RuntimeError("One or more stable assembly nodes are missing")

def descendants(group):
    owned = []
    for obj in objects:
        current = obj.parent
        while current:
            if current == group:
                owned.append(obj)
                break
            current = current.parent
    return owned

meshes = [obj for obj in objects if obj.type == "MESH"]
owned_names = {part["id"]: sorted(obj.name for obj in descendants(group)
                                    if obj.type == "MESH")
               for part, group in zip(PARTS["parts"], groups)}
ownership_pass = all(owned_names[part["id"]] == part["meshes"] for part in PARTS["parts"])
world_vertices = []
for obj in meshes:
    for vertex in obj.data.vertices:
        world_vertices.append(obj.matrix_world @ vertex.co)
minimum = [min(v[i] for v in world_vertices) for i in range(3)]
maximum = [max(v[i] for v in world_vertices) for i in range(3)]
dimensions = [maximum[i] - minimum[i] for i in range(3)]
probes = {}
for axis in ("Origin", "X", "Y", "Z"):
    node = bpy.data.objects.get("CabinetMotion" + axis)
    if node:
        probes[axis] = list(node.matrix_world.translation)
origin = Vector(probes["Origin"]) if "Origin" in probes else None
basis = {axis: list(Vector(probes[axis]) - origin) for axis in "XYZ"} if origin is not None else {}
uv_failures = []
material_slots = 0
for obj in meshes:
    material_slots += len(obj.material_slots)
    if not obj.data.uv_layers:
        uv_failures.append(obj.name)
for obj in meshes:
    obj.data.calc_loop_triangles()
result = {
    "fbx": str(FBX),
    "assemblyCount": len(groups),
    "meshCount": len(meshes),
    "triangleCount": sum(len(obj.data.loop_triangles) for obj in meshes),
    "boundsMinMetres": minimum,
    "boundsMaxMetres": maximum,
    "dimensionsMetres": dimensions,
    "root": root.name,
    "probePositions": probes,
    "basisVectors": basis,
    "groupMeshCounts": {part["id"]: len(owned_names[part["id"]]) for part in PARTS["parts"]},
    "ownershipPass": ownership_pass,
    "uvMissingMeshCount": len(uv_failures),
    "materialSlots": material_slots,
    "checks": {
        "assemblies": len(groups) == 16,
        "meshCount": len(meshes) == PARTS["meshCount"],
        "dimensions": all(abs(a-b) < 0.003 for a, b in
                          zip(dimensions, (1.208147, 0.655, 2.0))),
        "axisProbes": len(basis) == 3,
        "meshOwnership": ownership_pass,
        "uvs": not uv_failures,
    },
}
result["pass"] = all(result["checks"].values())
out = ROOT / "reports" / "electrical-cabinet-viewer-fbx-roundtrip.json"
out.write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result, indent=2), flush=True)
if not result["pass"]:
    raise RuntimeError("Cabinet viewer FBX round-trip validation failed")
