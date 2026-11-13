"""Export the latest reviewed vertical tank to its isolated Unity viewer."""
import hashlib
import json
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from tank_components import COMPONENTS, ANCHORS, MOTION, manifest

ASSETS = ROOT / "TankViewerProject" / "Assets" / "Tank"
ASSETS.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
root = bpy.data.objects["silo"]
groups = [bpy.data.objects[name] for name, _, _ in COMPONENTS]
meshes = [obj for obj in scene.objects
          if obj.type == "MESH" and obj.name.startswith("TNK_") and obj.get("equipment_mesh")]
used_names = {obj.name for obj in meshes}
parts = manifest(groups, meshes)
parts["schemaVersion"] = 2
parts["motionVersion"] = 3
parts["source"] = "model/source/" + Path(bpy.data.filepath).name
parts["sourceSha256"] = hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest()
parts["coordinateSystem"] = "Blender metres, Z up; FBX Up Y / Forward -Z; verify in Unity"
for i, part in enumerate(parts["parts"]):
    owned = part["meshes"]
    candidates = [obj for obj in meshes if obj.name in owned]
    anchor = max(candidates, key=lambda obj: (len(obj.data.polygons), obj.dimensions.length))
    part["anchor"] = anchor.name
    part["label"], part["description"] = COMPONENTS[i][1], COMPONENTS[i][2]
    # The motion path uses Blender-local metres; the runtime converts via axis probes.
    part["start"] = i
if sorted(name for part in parts["parts"] for name in part["meshes"]) != sorted(used_names):
    raise RuntimeError("Tank mesh ownership is missing or duplicated")
parts["meshCount"] = len(meshes)
(ASSETS / "parts.json").write_text(json.dumps(parts, indent=2, ensure_ascii=False), encoding="utf-8")

materials = []
used_materials = {m for obj in meshes for m in obj.data.materials if m}
for material in sorted(used_materials, key=lambda item: item.name):
    bsdf = next((n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if material.node_tree else None
    if not bsdf:
        raise RuntimeError("Material has no Principled BSDF: " + material.name)
    images = [n.image.name for n in material.node_tree.nodes if n.type == "TEX_IMAGE" and n.image]
    maps = {}
    for channel in ("BaseColor", "Normal", "Roughness"):
        node = next((n for n in material.node_tree.nodes if n.type == "TEX_IMAGE" and
                     n.image and ("_" + channel).lower() in n.image.name.lower()), None)
        if not node:
            continue
        path = Path(bpy.path.abspath(node.image.filepath))
        target = ASSETS / "FinishTextures" / path.name
        target.parent.mkdir(exist_ok=True)
        if node.image.packed_file:
            target.write_bytes(node.image.packed_file.data)
        else:
            shutil.copyfile(path, target)
        maps[channel] = "Assets/Tank/FinishTextures/" + target.name
    materials.append({
        "name": material.name, "color": list(material.diffuse_color),
        "metallic": float(bsdf.inputs["Metallic"].default_value),
        "roughness": float(bsdf.inputs["Roughness"].default_value),
        "relief": float(material.get("microrelief_m", 0.00001)),
        "baseMap": maps.get("BaseColor", ""), "normalMap": maps.get("Normal", ""),
        "roughMap": maps.get("Roughness", ""),
        "transmission": float(bsdf.inputs["Transmission Weight"].default_value),
        "emission": list(bsdf.inputs["Emission Color"].default_value),
        "emissionStrength": float(bsdf.inputs["Emission Strength"].default_value),
    })
(ASSETS / "materials.json").write_text(json.dumps({"materials": materials}, indent=2, ensure_ascii=False), encoding="utf-8")

selected = [root] + groups
for obj in meshes:
    current = obj
    while current:
        selected.append(current)
        if current == root:
            break
        current = current.parent
probes = []
for name, pos in (("Origin", (0, 0, 0)), ("X", (1, 0, 0)), ("Y", (0, 1, 0)), ("Z", (0, 0, 1))):
    probe = bpy.data.objects.new("TankMotion" + name, None)
    scene.collection.objects.link(probe)
    probe.parent = root
    probe.matrix_world = Matrix.Translation(pos)
    probes.append(probe)
selected.extend(probes)
bpy.ops.object.select_all(action="DESELECT")
for obj in set(selected):
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(
    filepath=str(ASSETS / "silo.fbx"), use_selection=True,
    object_types={"EMPTY", "MESH"}, global_scale=1, apply_unit_scale=True,
    axis_forward="-Z", axis_up="Y", bake_space_transform=False,
    apply_scale_options="FBX_SCALE_UNITS", use_mesh_modifiers=True,
    mesh_smooth_type="OFF", use_tspace=False, add_leaf_bones=False,
    bake_anim=False, path_mode="AUTO", use_custom_props=False)
for probe in probes:
    bpy.data.objects.remove(probe, do_unlink=True)
print("TANK_VIEWER_EXPORTED", len(meshes), len(parts["parts"]), len(materials), flush=True)
