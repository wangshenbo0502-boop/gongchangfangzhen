"""Export the reviewed axial fan and manifests for its isolated Unity viewer."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from fan_components import COMPONENTS, GROUP_NAMES, manifest

ASSETS = ROOT / "FanViewerProject" / "Assets" / "IndustrialFan"
ASSETS.mkdir(parents=True, exist_ok=True)

PART_COPY = (
    ("减振底座", "承托风筒和驱动组件，通过橡胶减振垫及锚固板将载荷传递至基础，减小振动传递。"),
    ("风筒与法兰", "形成轴向气流通道；两端法兰连接护网和管路，加强环与鞍座保持筒体刚度。"),
    ("八叶片叶轮", "由扭曲翼型叶片、轮毂和整流帽组成，将电机扭矩转化为轴向气流。"),
    ("入口安全护网", "隔离入口旋转叶轮，降低误触风险；安装圈、同心环与放射筋共同支承护网。"),
    ("驱动电机", "经输出轴和轴键带动叶轮，机壳散热筋帮助散热；拆检时先解除外部遮挡，再沿轴向退出。"),
    ("出口防鸟护网", "隔离出口侧旋转结构并阻挡异物，由安装圈、支筋及金属网格组成。"),
    ("导流片与电机支承", "定位电机并将其载荷传至风筒，导流片约束出口气流方向。"),
    ("接线盒", "容纳和保护接线端子，盒体与独立盒盖配合；本展示模型不保留外露电线和接口。"),
    ("曲面检修盖", "封闭风筒侧面的检修开口，盖板与密封垫贴合筒壁，便于日常观察和维护。"),
    ("电机后风罩", "保护电机后部通风结构，通风网和支筋在防护的同时保留散热通道。"),
    ("接线盒盖", "封闭外置接线盒，承载设备编号、序列号、制造年月及条码标识。"),
)

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
root = bpy.data.objects["industrial-fan"]
groups = [bpy.data.objects[name] for name in GROUP_NAMES]


def descendants(group):
    result = []
    for obj in bpy.data.objects:
        current = obj
        while current.parent:
            if current.parent == group:
                result.append(obj)
                break
            current = current.parent
    return result


meshes = [obj for obj in scene.objects if obj.type == "MESH" and obj.get("equipment_mesh")]
used_names = {obj.name for obj in meshes}
parts = manifest(groups, meshes)
for item, group in zip(parts["components"], groups):
    owned = [obj for obj in descendants(group) if obj.type == "MESH" and obj.name in used_names]
    if not owned:
        raise RuntimeError(f"No exported meshes assigned to {group.name}")
    anchor = max(owned, key=lambda obj: (len(obj.data.polygons), obj.dimensions.length))
    item["meshes"] = sorted(obj.name for obj in owned)
    item["anchor"] = anchor.name
    item["label"], item["description"] = PART_COPY[item["index"]]
    # JsonUtility reads Vector3 fields as objects, not JSON arrays.
    for key in item["path"]:
        key["offset"] = dict(zip(("x", "y", "z"), key["offset"]))
if sorted(name for item in parts["components"] for name in item["meshes"]) != sorted(used_names):
    raise RuntimeError("Mesh ownership is missing or duplicated")
parts["meshCount"] = len(meshes)
parts["durationSeconds"] = parts["oneWaySeconds"]
parts["parts"] = parts.pop("components")
(ASSETS / "parts.json").write_text(json.dumps(parts, indent=2, ensure_ascii=False), encoding="utf-8")

materials = []
used_materials = {material for obj in meshes for material in obj.data.materials if material}
for material in sorted(used_materials, key=lambda item: item.name):
    if not material.node_tree:
        raise RuntimeError(f"Material has no node tree: {material.name}")
    bsdf = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if not bsdf:
        raise RuntimeError(f"Material has no Principled BSDF: {material.name}")
    images = [node.image.name.lower() for node in material.node_tree.nodes
              if node.type == "TEX_IMAGE" and node.image]
    family = next((name for name in ("cast", "machined", "rubber")
                   if any(name + "_" in image for image in images)), "")
    materials.append({
        "name": material.name,
        "color": list(material.diffuse_color),
        "metallic": float(bsdf.inputs["Metallic"].default_value),
        "roughness": float(bsdf.inputs["Roughness"].default_value),
        "family": family,
        "relief": float(material.get("microrelief_m", 0.00001)),
        "textures": images,
    })
(ASSETS / "materials.json").write_text(
    json.dumps({"materials": materials}, indent=2, ensure_ascii=False), encoding="utf-8")

selected = [root]
selected.extend(group for group in groups)
for obj in meshes:
    current = obj
    while current:
        selected.append(current)
        if current == root:
            break
        current = current.parent
if any(obj.parent and obj.parent not in selected for obj in selected):
    raise RuntimeError("Export selection is missing an intermediate parent transform")
# These invisible references go through the same FBX axis conversion as the mesh.
probes = []
for name, position in (
    ("Origin", (0, 0, 0)), ("X", (1, 0, 0)),
    ("Y", (0, 1, 0)), ("Z", (0, 0, 1)),
):
    probe = bpy.data.objects.new("FanMotion" + name, None)
    scene.collection.objects.link(probe)
    probe.parent = root
    probe.matrix_world = Matrix.Translation(position)
    probes.append(probe)
selected.extend(probes)
bpy.ops.object.select_all(action="DESELECT")
for obj in selected:
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(
    filepath=str(ASSETS / "industrial-fan.fbx"), use_selection=True,
    object_types={"EMPTY", "MESH"}, global_scale=1, apply_unit_scale=True,
    axis_forward="-Z", axis_up="Y", bake_space_transform=False,
    apply_scale_options="FBX_SCALE_UNITS", use_mesh_modifiers=True,
    mesh_smooth_type="OFF", use_tspace=False, add_leaf_bones=False,
    bake_anim=False, path_mode="AUTO", use_custom_props=False)
for probe in probes:
    bpy.data.objects.remove(probe, do_unlink=True)
print("FAN_VIEWER_EXPORTED", len(meshes), len(parts["parts"]), len(materials), flush=True)
