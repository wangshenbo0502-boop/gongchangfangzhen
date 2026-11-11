"""Blender stage A: visible, editable pump proportion study. No Unity edits."""
import json
import math
import sys
from datetime import datetime
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


MODEL = Path(__file__).resolve().parents[1]
SOURCE = MODEL / "source" / "water-pump.blend"
PREVIEW = MODEL / "previews" / "water-pump"
REPORT = MODEL / "reports"
GROUP_NAMES = (
    "PMP_00_Base", "PMP_01_Motor", "PMP_02_Coupling", "PMP_03_Guard",
    "PMP_04_Volute", "PMP_05_InletCover", "PMP_06_SealBearing",
    "PMP_07_Impeller",
)
SHAFT = Vector((-0.49, 0, 0.66))
PALETTE = (
    (0.29, 0.32, 0.34, 1), (0.22, 0.34, 0.41, 1),
    (0.51, 0.56, 0.59, 1), (0.83, 0.58, 0.07, 1),
    (0.25, 0.38, 0.44, 1), (0.44, 0.52, 0.56, 1),
    (0.35, 0.44, 0.49, 1), (0.65, 0.48, 0.23, 1),
)


def empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    geometry.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.empty_display_size = 0.06
    obj.empty_display_type = "PLAIN_AXES"
    return obj


def finish(obj, group, name):
    obj.name = name
    for col in list(obj.users_collection):
        col.objects.unlink(obj)
    geometry.objects.link(obj)
    obj.parent = groups[group]
    obj.matrix_parent_inverse = groups[group].matrix_world.inverted()
    obj.data.materials.append(materials[group])
    obj.color = PALETTE[group]
    obj["stage"] = "A_BLOCKOUT"
    return obj


def box(group, name, center, dimensions):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.data.transform(__import__("mathutils").Matrix.Diagonal((*dimensions, 1)))
    return finish(obj, group, name)


def cylinder(group, name, center, radius, length, axis="X", keep_axis=False):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32, radius=radius, depth=length, end_fill_type="NGON",
        location=center,
    )
    obj = bpy.context.object
    rotation = Vector((1, 0, 0) if axis == "X" else (0, 0, 1)).to_track_quat("Z", "Y")
    if keep_axis:
        obj.rotation_euler = rotation.to_euler()
    else:
        obj.data.transform(rotation.to_matrix().to_4x4())
    return finish(obj, group, name)


def bounds(objects):
    bpy.context.view_layer.update()
    points = [obj.matrix_world @ vert.co for obj in objects for vert in obj.data.vertices]
    lo = Vector(tuple(min(v[i] for v in points) for i in range(3)))
    hi = Vector(tuple(max(v[i] for v in points) for i in range(3)))
    return lo, hi


def inspect():
    meshes = [obj for obj in geometry.objects if obj.type == "MESH"]
    low, high = bounds(meshes)
    size = high - low
    non_manifold = 0
    negative = []
    triangles = 0
    groups_info = []
    for obj in meshes:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        non_manifold += sum(not edge.is_manifold for edge in bm.edges)
        if bm.calc_volume(signed=True) <= 0:
            negative.append(obj.name)
        bm.free()
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    for name in GROUP_NAMES:
        members = [o for o in groups[GROUP_NAMES.index(name)].children if o.type == "MESH"]
        lo, hi = bounds(members)
        groups_info.append({
            "name": name, "meshes": len(members),
            "min_xyz_m": list(lo), "max_xyz_m": list(hi),
            "dimensions_xyz_m": list(hi - lo),
        })
    rotor = bpy.data.objects["water-pump_rotor"]
    axis = (rotor.matrix_world.to_3x3() @ Vector((0, 0, 1))).normalized()
    checks = {
        "G1": abs(low.z) < .005 and abs((low.x + high.x) / 2) < .005
              and abs((low.y + high.y) / 2) < .005 and root.location.length < .00001,
        "G2": all((obj.scale - Vector((1, 1, 1))).length < 1e-6 for obj in geometry.objects),
        "G3": all(abs(actual / expected - 1) <= .03 for actual, expected in zip(size, (1.9, 1, 1.35))),
        "G4": sorted(o.name for o in root.children) == sorted(GROUP_NAMES),
        "G5": non_manifold == 0,
        "G6": not negative,
        "G7": triangles <= 120000,
        "G8": None,
        "G9": (rotor.matrix_world.translation - SHAFT).length <= .002
              and abs(axis.dot(Vector((1, 0, 0)))) > .99999,
        "G10": rotor.type == "MESH",
        "G11": all(obj.parent in groups for obj in meshes),
        "G12": high.y <= 1.2 and -low.y <= 1.2,
    }
    return {
        "stage": "A_BLOCKOUT", "blender": bpy.app.version_string,
        "measured_at": datetime.now().astimezone().isoformat(),
        "source": str(SOURCE), "mesh_count": len(meshes), "triangles": triangles,
        "bounds_min_blender_xyz_m": list(low), "bounds_max_blender_xyz_m": list(high),
        "width_depth_height_m": list(size), "groups": groups_info,
        "non_manifold_edges": non_manifold, "non_positive_volume_meshes": negative,
        "rotor_local_z_world": list(axis), "checks": checks,
    }


def write_report(data):
    (REPORT / "water-pump-stage-a.json").write_text(
        json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    descriptions = {
        "G1": "底面及水平中心误差小于 5 mm；根节点位于原点。",
        "G2": "建模对象及组件组缩放均为 1。",
        "G3": "网格实测宽/深/高分别与 1.90/1.00/1.35 m 比较。",
        "G4": "根节点恰有 PMP_00 至 PMP_07 八个组件组。",
        "G5": f"非流形边 {data['non_manifold_edges']}；本阶段全部为封闭实体占位。",
        "G6": "逐网格检查有符号体积为正；不代表已经检查最终蜗壳内流道。",
        "G7": f"实际三角面 {data['triangles']:,} / 上限 120,000。",
        "G8": "未验证：UV 与贴图属于 E 阶段；不将默认 UV 记为通过。",
        "G9": "转子源文件枢轴在设计轴心；本地 Z 与物理 X 轴平行，误差 ≤ 2 mm。",
        "G10": "存在名为 water-pump_rotor 的 Mesh。",
        "G11": f"{data['mesh_count']} 个网格均直属八个组件组之一；仅检查归属，细节接触待 C/D 阶段。",
        "G12": "前、后半深均按实际网格计算，均未超过 1.2 m。",
    }
    w, d, h = data["width_depth_height_m"]
    rows = [
        "# 离心泵 A 阶段：比例草模与实测",
        "", f"生成时间：{data['measured_at']}；Blender {data['blender']}。",
        "", "**当前仅完成 A 比例阶段，待用户确认。不是最终精细模型，未通过整机验收。**",
        "本阶段只用长方体和圆柱建立装配关系；蜗壳、叶轮、密封为实体占位，不能据此评估真实流道或仿真。",
        "", "## 文件与复现", "",
        "- 源文件：`../source/water-pump.blend`。",
        "- 脚本：`../scripts/build_pump_blockout.py`；在 Blender 的 Python Console 执行该脚本。",
        "- 预览：`../previews/water-pump/stage-a-*.png`，各 1920×1080。",
        "- 原始实测：`water-pump-stage-a.json`，所有尺寸来自模型实际顶点。",
        "- 渲染命令：`blender --background model/source/water-pump.blend --python model/scripts/build_pump_blockout.py -- --render-only`。",
        "", "## 实测尺寸", "",
        "| 项目 | 实测 m | 目标 m | 偏差 |", "| --- | ---: | ---: | ---: |",
    ]
    for label, actual, target in zip(("宽 X", "深 Blender Y / Unity Z", "高 Blender Z / Unity Y"), (w, d, h), (1.9, 1, 1.35)):
        rows.append(f"| {label} | {actual:.6f} | {target:.2f} | {100*(actual/target-1):+.4f}% |")
    rows.extend(["", "| 组件 | 网格数 | X / Y / Z 实测尺寸 m |",
                 "| --- | ---: | --- |"])
    for item in data["groups"]:
        dims = " / ".join(f"{v:.4f}" for v in item["dimensions_xyz_m"])
        rows.append(f"| {item['name']} | {item['meshes']} | {dims} |")
    rows.extend(["", "## G1–G12（仅 A 阶段）", "",
                 "| 检查 | 本阶段结果 | 证据及边界 |", "| --- | --- | --- |"])
    for key, value in data["checks"].items():
        rows.append(f"| {key} | {'未验证' if value is None else 'PASS' if value else 'FAIL'} | {descriptions[key]} |")
    rows.extend([
        "", "## 轴向决定与待解决事项", "",
        "1. 端吸泵的电机、联轴器、轴承、蜗壳及吸入口共用 X 轴。电机在 +X，吸入口在 −X。",
        "   规范 4.1 将前盖/叶轮拆解写成 −Unity Z，同时电机写成 +X，无法同时满足同轴装配；",
        "   后续建议前盖和叶轮沿 −X 拆开。本阶段未制作拆解动画，也未擅自改写规范。",
        "2. 保留转子源对象的定向旋转，使其本地 Z 对准物理 X；",
        "   这是“Apply All Transforms”与“保留本地 Z 转轴”之间的显式取舍，缩放仍为 1。",
        "3. 当前 Unity 生成器新建无旋转 Rotor pivot，并用 renderer bounds.center 作枢轴，",
        "   再将 rotorAxis 设为 forward。该逻辑会丢失源轴向；接入前须统一，不能声称运行旋转通过。",
        "", "## 后续验收边界", "",
        "- G5/G6 只验证当前封闭原语；B 阶段真实蜗壳、D 阶段内部装配完成后必须重验。",
        "- B–H 未开始：无真实流道、后弯叶片、螺栓、PBR 贴图、故障开裂或 FBX。",
        "- 未比较与小车的最终细节密度，不声称达到小车水准。",
        "- 未修改 Assets、Unity 场景、现有小车或另一个工程；无本次 Unity/导航/EXE 验证。",
        "- 规范第 11 章要求“先输出 blockout 的线框截图与尺寸表，等我确认比例后再做细节”。",
        "  因此交付本阶段供比例确认，其他三类设备尚未开始。",
        "",
    ])
    (REPORT / "water-pump.md").write_text("\n".join(rows), encoding="utf-8")


def prepare_views():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world.color = (.28, .28, .28)
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.45, .47, .49, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .7
    scene.view_settings.view_transform = "AgX"
    studio = bpy.data.collections.new("PREVIEW_ONLY_not_for_export")
    scene.collection.children.link(studio)
    points = [o.matrix_world @ v.co for o in geometry.objects if o.type == "MESH" for v in o.data.vertices]
    target = Vector((0, 0, .66))
    views = {
        "front": (0, -5, .66), "inlet-side": (-5, 0, .66),
        "top": (0, 0, 6), "three-quarter": (-3, -4, 2.9),
    }
    for label, position in views.items():
        cam_data = bpy.data.cameras.new(label)
        cam = bpy.data.objects.new("VIEW_" + label, cam_data)
        studio.objects.link(cam)
        cam.location = position
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam_data.type = "ORTHO"
        invrot = cam.rotation_euler.to_matrix().inverted()
        projected = [invrot @ (p - target) for p in points]
        span_x = max(v.x for v in projected) - min(v.x for v in projected)
        span_y = max(v.y for v in projected) - min(v.y for v in projected)
        cam_data.ortho_scale = max(span_x, span_y * 1920 / 1080) * 1.3
        cam.hide_set(True)
    for name, location, energy, size in (
        ("Key", (-3, -4, 5), 700, 5), ("Fill", (3, -1, 3), 400, 4),
        ("Rim", (1, 4, 5), 600, 3),
    ):
        light = bpy.data.lights.new(name, "AREA")
        light.energy = energy
        light.shape = "DISK"
        light.size = size
        obj = bpy.data.objects.new(name, light)
        studio.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()
        obj.hide_set(True)
    scene.camera = bpy.data.objects["VIEW_three-quarter"]
    wire = bpy.data.collections.new("WIREFRAME_PREVIEW_ONLY_not_for_export")
    scene.collection.children.link(wire)
    line_mat = bpy.data.materials.new("Preview graphite edges")
    line_mat.diffuse_color = (.018, .022, .025, 1)
    for obj in [o for o in geometry.objects if o.type == "MESH"]:
        dup = bpy.data.objects.new("WIRE_" + obj.name, obj.data.copy())
        wire.objects.link(dup)
        dup.matrix_world = obj.matrix_world.copy()
        dup.data.materials.clear()
        dup.data.materials.append(line_mat)
        mod = dup.modifiers.new("Technical edges only", "WIREFRAME")
        mod.thickness = .0015
        mod.use_replace = True
        mod.offset = 1
        dup.hide_set(True)


def render_views():
    scene = bpy.context.scene
    for label in ("front", "inlet-side", "top", "three-quarter"):
        scene.camera = bpy.data.objects["VIEW_" + label]
        scene.render.filepath = str(PREVIEW / f"stage-a-{label}.png")
        bpy.ops.render.render(write_still=True)
    print("PUMP_RENDER_COMPLETE", flush=True)


def build():
    global geometry, groups, materials, root
    for folder in (SOURCE.parent, PREVIEW, REPORT):
        folder.mkdir(parents=True, exist_ok=True)
    # Preserve the user's currently open session before replacing its contents.
    if not bpy.app.background:
        backup = SOURCE.parent / ("session-before-pump-" + datetime.now().strftime("%Y%m%d-%H%M%S") + ".blend")
        bpy.ops.wm.save_as_mainfile(filepath=str(backup), check_existing=False)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    geometry = bpy.data.collections.new("PUMP_MODEL_STAGE_A")
    bpy.context.scene.collection.children.link(geometry)
    root = empty("water-pump")
    root["stage"] = "A_BLOCKOUT_PENDING_PROPORTION_REVIEW"
    root["physical_shaft_axis"] = "Blender X; source rotor local Z aligned X"
    groups = [empty(name, root) for name in GROUP_NAMES]
    materials = []
    for name, color in zip(GROUP_NAMES, PALETTE):
        mat = bpy.data.materials.new(name + "_BLOCKOUT")
        mat.diffuse_color = color
        materials.append(mat)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene["delivery_stage"] = "A only - not final industrial geometry"
    box(0, "PMP_00_Base_Grout_01", (0, 0, .05), (1.9, 1, .1))
    box(0, "PMP_00_Base_Plate_01", (0, 0, .13), (1.82, .90, .06))
    cylinder(1, "PMP_01_Motor_Housing_01", (.53, 0, .66), .245, .59)
    cylinder(1, "PMP_01_Motor_FanCover_01", (.87, 0, .66), .235, .09)
    for y in (-.21, .21):
        box(1, "PMP_01_Motor_Foot_" + ("01" if y < 0 else "02"),
            (.52, y, .285), (.53, .13, .25))
    box(1, "PMP_01_Motor_TerminalBox_01", (.57, -.02, .96), (.24, .25, .12))
    cylinder(2, "PMP_02_Coupling_HalfPump_01", (.025, 0, .66), .098, .07)
    cylinder(2, "PMP_02_Coupling_Elastomer_01", (.075, 0, .66), .088, .03)
    cylinder(2, "PMP_02_Coupling_HalfMotor_01", (.125, 0, .66), .098, .07)
    box(3, "PMP_03_Guard_Blockout_01", (.075, 0, .67), (.29, .33, .29))
    cylinder(4, "PMP_04_Volute_CasingPlaceholder_01", (-.51, 0, .66), .40, .23)
    cylinder(4, "PMP_04_Volute_DischargeNeck_01", (-.51, .23, 1.13), .085, .38, axis="Z")
    cylinder(4, "PMP_04_Volute_DischargeFlange_01", (-.51, .23, 1.325), .16, .05, axis="Z")
    box(4, "PMP_04_Volute_Pedestal_01", (-.51, 0, .225), (.30, .50, .13))
    cylinder(5, "PMP_05_InletCover_PlatePlaceholder_01", (-.66, 0, .66), .33, .07)
    cylinder(5, "PMP_05_InletCover_SuctionNeck_01", (-.77, 0, .66), .105, .15)
    cylinder(5, "PMP_05_InletCover_SuctionFlange_01", (-.87, 0, .66), .20, .05)
    cylinder(6, "PMP_06_SealBearing_SealChamber_01", (-.31, 0, .66), .15, .17)
    cylinder(6, "PMP_06_SealBearing_BearingHousing_01", (-.15, 0, .66), .115, .15)
    box(6, "PMP_06_SealBearing_Support_01", (-.17, 0, .335), (.14, .23, .35))
    cylinder(7, "water-pump_rotor", SHAFT, .28, .055, keep_axis=True)
    cylinder(7, "PMP_07_Impeller_ShaftPlaceholder_01", (-.20, 0, .66), .035, .65)
    cylinder(7, "PMP_07_Impeller_HubPlaceholder_01", (-.49, 0, .66), .075, .10)
    bpy.context.view_layer.update()
    result = inspect()
    write_report(result)
    prepare_views()
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = None
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type != "VIEW_3D":
                    continue
                space.shading.color_type = "MATERIAL"
                space.shading.show_cavity = True
                space.shading.cavity_type = "BOTH"
                space.region_3d.view_location = Vector((0, 0, .65))
                space.region_3d.view_distance = 3.25
                space.region_3d.view_rotation = Vector((-3, -4, 2.8)).to_track_quat("Z", "Y")
                space.clip_end = 100
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE), check_existing=False)
    print("PUMP_BUILD_COMPLETE", json.dumps(result["checks"]), flush=True)


if "--render-only" in sys.argv:
    render_views()
else:
    build()
