"""Export the reviewed cabinet to the isolated Unity viewer with stable manifests."""
import hashlib
import json
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from cabinet_components import DOOR_ANGLE, DOOR_ATTACHED, GROUP_NAMES, HINGE

SOURCE = Path(bpy.data.filepath)
ASSETS = ROOT / "CabinetViewerProject" / "Assets" / "ElectricalCabinet"
TEXTURES = ROOT / "textures" / "electrical-cabinet" / "finish-review"
ASSETS.mkdir(parents=True, exist_ok=True)

LABELS = (
    ("柜体框架", "承载门板、安装背板与底座的钣金外壳，提供设备防护和安装基准。"),
    ("安装背板", "承载导轨及内部电气元件，并将器件载荷传递至柜体结构。"),
    ("端子排与接地排", "用于现场导线端接和保护接地汇接；模型展示不代表完整回路校核。"),
    ("柜门与门锁", "带铰链的钣金门板，承载门锁、观察窗及随门安装部件。"),
    ("断路器", "展示进线及支路保护器件，用于隔离和过流保护的结构识别。"),
    ("PLC 与 I/O", "控制器及输入输出模块，用于呈现自动化控制系统的主要控制单元。"),
    ("柜门散热组件", "门板上的风扇、滤框与百叶组件，用于设备通风结构展示。"),
    ("线槽与线束", "承载柜内导线及线号，展示分区布线；不构成经认证的电气网表。"),
    ("固定操作面板", "安装于柜体固定横条上的按钮与指示件，不随柜门转动。"),
    ("24 V 电源", "向控制回路提供直流电源的工业电源外形展示件。"),
    ("底座与电缆格兰", "承托柜体并展示底部电缆引入与密封结构。"),
    ("接触器与热过载", "展示电机支路控制及过载保护器件的安装结构。"),
    ("变频器", "展示变频驱动器及散热外壳，模型屏显为静态演示内容。"),
    ("工业网络交换机", "展示工业以太网设备及端口结构，不表示网络配置状态。"),
    ("安全继电器", "展示安全及接口继电器模块，不构成安全回路认证。"),
    ("门板操作 HMI", "安装于柜门上的人机界面演示模型；屏幕内容为静态示意。"),
)

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
root = bpy.data.objects["electrical-cabinet"]
groups = [bpy.data.objects[name] for name in GROUP_NAMES]
hinge = groups[0].get("viewer_hinge_origin_m", HINGE)
meshes = [obj for obj in scene.objects if obj.type == "MESH" and obj.get("equipment_mesh")]

def ancestors(obj):
    result = []
    while obj:
        result.append(obj)
        obj = obj.parent
    return result

parts = []
extract_order = (8, 7, 2, 4, 12, 5, 9, 11, 13, 14)
parked_centres = (
    (-1.25, -3.1, 0.35), (0.0, -3.1, 0.35), (1.25, -3.1, 0.35),
    (-1.25, -3.1, 1.05), (0.0, -3.1, 1.05), (1.25, -3.1, 1.05),
    (-1.25, -3.1, 1.75), (0.0, -3.1, 1.75), (1.25, -3.1, 1.75),
    (-1.25, -3.1, 2.45), (0.0, -3.1, 2.45), (1.25, -3.1, 2.45),
)
extraction_phase = {index: order for order, index in enumerate(extract_order)}
for index, group in enumerate(groups):
    owned = [obj for obj in meshes if group in ancestors(obj)]
    if not owned:
        raise RuntimeError(f"Empty cabinet assembly: {group.name}")
    anchor = max(owned, key=lambda obj: (len(obj.data.polygons), obj.dimensions.length))
    origin = {"x": 0.0, "y": 0.0, "z": 0.0}
    path = [{"t": 0.0, "offset": origin}]
    if index in extraction_phase:
        order = extraction_phase[index]
        start = 0.31 + order * 0.065
        clear = start + 0.040
        finish = start + 0.060
        vertices = [obj.matrix_world @ vertex.co for obj in owned
                    for vertex in obj.data.vertices]
        centre = tuple((min(vertex[axis] for vertex in vertices) +
                        max(vertex[axis] for vertex in vertices)) / 2
                       for axis in range(3))
        park = ((-3.0, -3.1, 0.35) if index == 8 else
                (3.0, -3.1, 1.05) if index == 4 else
                (2.2, -4.5, 2.4) if index == 7 else
                (2.2, -4.5, 1.05) if index == 12 else parked_centres[order])
        exit_offset = {
            "x": 0.0,
            "y": -4.5 if index in (7, 12) else -3.0,
            "z": 0.0,
        }
        park_offset = {axis: float(park[coordinate] - centre[coordinate])
                       for coordinate, axis in enumerate(("x", "y", "z"))}
        if index == 7:
            # The rigid exported harness cannot be pulled through its captive
            # cable glands and installed terminals as one object. Stage the
            # disconnected harness during an explicit inspection transition.
            path.extend(({"t": .39, "offset": origin}, {"t": .40, "offset": park_offset},
                         {"t": 1.0, "offset": park_offset}))
        else:
            path.extend((
                {"t": start, "offset": origin},
                {"t": clear, "offset": exit_offset},
                {"t": finish, "offset": park_offset},
                {"t": 1.0, "offset": park_offset},
            ))
    elif index in DOOR_ATTACHED:
        # Unseat the complete door assembly before changing its angle.
        path.extend(({"t": .065, "offset": {"x": 0.0, "y": -.16, "z": 0.0}},
                     {"t": 1.0, "offset": {"x": 0.0, "y": -.16, "z": 0.0}}))
    else:
        path.append({"t": 1.0, "offset": origin})
    parts.append({
        "index": index,
        "name": group.name,
        "id": group.name,
        "label": LABELS[index][0],
        "description": LABELS[index][1],
        "meshes": sorted(obj.name for obj in owned),
        "meshCount": len(owned),
        "anchor": anchor.name,
        "pivot": {"x": float(group.location.x), "y": float(group.location.y),
                  "z": float(group.location.z)},
        "doorAttached": index in DOOR_ATTACHED,
        "path": path,
    })
    if index == 7:
        parts[-1]["transfer"] = {"fadeOutStart": .365, "hiddenAt": .39,
                                 "visibleFrom": .40, "fadeInEnd": .435}
if sorted(name for part in parts for name in part["meshes"]) != sorted(obj.name for obj in meshes):
    raise RuntimeError("Mesh ownership is incomplete or duplicated")

source_hash = hashlib.sha256(SOURCE.read_bytes()).hexdigest().upper()
for obj in meshes:
    obj.data.calc_loop_triangles()
materials = []
used_materials = {mat for obj in meshes for mat in obj.data.materials if mat}

def linked_node(socket):
    return socket.links[0].from_node if socket.is_linked else None


def texture_file(node):
    if not node or node.type != "TEX_IMAGE" or not node.image:
        return ""
    image = node.image
    source = Path(bpy.path.abspath(image.filepath))
    target = ASSETS / "SourceTextures" / source.name
    target.parent.mkdir(parents=True, exist_ok=True)
    if image.packed_file:
        target.write_bytes(image.packed_file.data)
    elif source.is_file():
        shutil.copy2(source, target)
    else:
        raise RuntimeError(f"Missing used image: {image.name}")
    return "Assets/ElectricalCabinet/SourceTextures/" + target.name


def image_upstream(socket, visited=None):
    visited = set() if visited is None else visited
    node = linked_node(socket)
    if not node or node in visited:
        return None
    visited.add(node)
    if node.type == "TEX_IMAGE":
        return node
    for item in node.inputs:
        result = image_upstream(item, visited)
        if result:
            return result
    return None


for mat in sorted(used_materials, key=lambda value: value.name):
    bsdf = next((node for node in mat.node_tree.nodes
                 if node.type == "BSDF_PRINCIPLED"), None) if mat.node_tree else None
    if not bsdf:
        raise RuntimeError(f"Missing Principled BSDF in material {mat.name}")
    images = [node.image.name for node in mat.node_tree.nodes
              if node.type == "TEX_IMAGE" and node.image]
    image_names = " ".join(images).lower()
    text = image_names + " " + mat.name.lower()
    family = next((value for value, aliases in (
        ("powder", ("powder", "powdercoat")),
        ("zinc", ("zinc", "galvanized")),
        ("brushed", ("brushed", "machined")),
        ("rubber", ("rubber",)),
        ("pvc", ("pvc",)),
        ("polymer", ("polymer", "plastic")),
        ("glass", ("glass",)),
    ) if any(alias in text for alias in aliases)), "polymer")
    base = bsdf.inputs["Base Color"]
    base_node = linked_node(base)
    color = list(base.default_value)
    if base_node and base_node.type == "MIX_RGB" and base_node.blend_type == "MULTIPLY":
        color = list(base_node.inputs[2].default_value)
    elif base_node and base_node.type == "TEX_IMAGE":
        color = [1, 1, 1, 1]
    base_image = image_upstream(base)
    rough_image = image_upstream(bsdf.inputs["Roughness"])
    normal_image = next((n for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and
                         n.image and "_Normal" in n.image.filepath), None)
    rough_node = linked_node(bsdf.inputs["Roughness"])
    rough_scale, rough_bias = 0.0, float(bsdf.inputs["Roughness"].default_value)
    if rough_image:
        rough_scale, rough_bias = 1.0, 0.0
        if rough_node.type == "MATH" and rough_node.operation == "ADD":
            rough_bias = float(rough_node.inputs[1].default_value)
        elif rough_node.type == "MAP_RANGE":
            low, high = (float(rough_node.inputs[i].default_value) for i in (3, 4))
            rough_scale, rough_bias = high - low, low
    elif rough_node and rough_node.type == "MAP_RANGE":
        rough_bias = sum(float(rough_node.inputs[i].default_value) for i in (3, 4)) / 2
    repeat, uv_channel = 1.0, 0
    if base_image:
        vector_node = linked_node(base_image.inputs["Vector"])
        if vector_node and vector_node.type == "VECT_MATH":
            repeat = float(vector_node.inputs["Scale"].default_value)
            uv_node = linked_node(vector_node.inputs[0])
            uv_channel = 1 if uv_node and getattr(uv_node, "uv_map", "") == "FinishUV" else 0
    metal_image = image_upstream(bsdf.inputs["Metallic"])
    normal_node = next((n for n in mat.node_tree.nodes if n.type == "NORMAL_MAP"), None)
    emission_image = image_upstream(bsdf.inputs["Emission Color"])
    if family in ("powder", "rubber", "pvc") or (metal_image and "polymer_" in metal_image.image.name):
        metallic = 0.0
        metal_image = None
        normal_strength = max(
            float(normal_node.inputs["Strength"].default_value), 0.0
        ) if normal_node else 0.0
        grain = 0.0
    else:
        metallic = 1.0 if metal_image else float(bsdf.inputs["Metallic"].default_value)
        normal_strength = float(normal_node.inputs["Strength"].default_value) if normal_node else 0.0
        grain = 0.0
    materials.append({
        "name": mat.name,
        "family": family,
        "color": [float(v) for v in color],
        "metallic": metallic,
        "roughness": rough_bias,
        "roughScale": rough_scale,
        "grain": grain,
        "uvChannel": uv_channel,
        "repeat": repeat,
        "normalStrength": normal_strength,
        "transmission": float(bsdf.inputs["Transmission Weight"].default_value),
        "emissionStrength": float(bsdf.inputs["Emission Strength"].default_value),
        "emissionColor": list(bsdf.inputs["Emission Color"].default_value),
        "baseMap": texture_file(base_image),
        "roughMap": texture_file(rough_image),
        "metalMap": texture_file(metal_image),
        "normalMap": texture_file(normal_image),
        "emissionMap": texture_file(emission_image),
        "textures": images,
    })

parts_doc = {
    "schemaVersion": 1,
    "motionVersion": 4,
    "model": "electrical-cabinet",
    "oneWaySeconds": 14.0,
    "source": str(SOURCE),
    "sourceSha256": source_hash,
    "blenderVersion": bpy.app.version_string,
    "units": "metres",
    "sourceAxes": "Blender XYZ (Z up); Unity basis is measured from FBX probes",
    "expectedSourceBounds": {"x": 1.208147, "y": 0.655, "z": 2.0},
    "hinge": {"x": hinge[0], "y": hinge[1], "z": hinge[2],
              "sourceAxis": "Z", "angleDegrees": float(DOOR_ANGLE * 180.0 / 3.141592653589793),
              "rotationStart": .065, "rotationEnd": .25},
    "parts": parts,
    "meshCount": len(meshes),
    "triangleCount": sum(len(obj.data.loop_triangles) for obj in meshes),
}
(ASSETS / "parts.json").write_text(json.dumps(parts_doc, indent=2, ensure_ascii=False), encoding="utf-8")
(ASSETS / "materials.json").write_text(
    json.dumps({"schemaVersion": 2, "colorSpace": "linear tint; sRGB color images; linear data maps",
                "materials": materials}, indent=2, ensure_ascii=False),
    encoding="utf-8")

for image in list(bpy.data.images):
    if image.source != "FILE":
        continue
    try:
        image.filepath = bpy.path.abspath(image.filepath)
        if not Path(image.filepath).is_file():
            continue
        target = ASSETS / "SourceTextures" / Path(image.filepath).name
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            shutil.copy2(image.filepath, target)
    except (OSError, RuntimeError):
        pass
for texture in TEXTURES.glob("*"):
    if texture.is_file():
        target = ASSETS / "Finishes" / texture.name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(texture, target)

selected = [root]
for group in groups:
    selected.extend(ancestors(group))
for obj in meshes:
    selected.extend(ancestors(obj))
selected = list(dict.fromkeys(selected))
if any(obj.parent and obj.parent not in selected for obj in selected):
    raise RuntimeError("Export selection omitted an intermediate parent")
probes = []
for name, position in (
    ("Origin", (0, 0, 0)), ("X", (1, 0, 0)),
    ("Y", (0, 1, 0)), ("Z", (0, 0, 1)),
):
    probe = bpy.data.objects.new("CabinetMotion" + name, None)
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
    filepath=str(ASSETS / "electrical-cabinet.fbx"), use_selection=True,
    object_types={"EMPTY", "MESH"}, global_scale=1, apply_unit_scale=True,
    axis_forward="-Z", axis_up="Y", bake_space_transform=False,
    apply_scale_options="FBX_SCALE_UNITS", use_mesh_modifiers=True,
    mesh_smooth_type="OFF", use_tspace=False, add_leaf_bones=False,
    bake_anim=False, path_mode="AUTO", use_custom_props=False)
for probe in probes:
    bpy.data.objects.remove(probe, do_unlink=True)
manifest = {
    "model": parts_doc["model"], "source": str(SOURCE),
    "sourceSha256": source_hash, "blenderVersion": bpy.app.version_string,
    "fbx": "electrical-cabinet.fbx", "meshCount": len(meshes),
    "assemblyCount": len(parts), "textureFiles": len(list((ASSETS / "SourceTextures").glob("*")))
}
(ASSETS / "export-manifest.json").write_text(
    json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
print("CABINET_VIEWER_EXPORTED", json.dumps(manifest, ensure_ascii=False), flush=True)
