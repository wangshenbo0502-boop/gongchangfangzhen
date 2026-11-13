"""Vertical tank assembly contract shared by Blender and the Unity viewer."""
import json
from pathlib import Path

COMPONENTS = (
    ("TNK_00_Legs", "支腿与锚固", "四根支腿通过加强板和支承环托住罐体，底板及锚栓将载荷传至地面。"),
    ("TNK_01_Shell", "筒体", "8 毫米厚卷制筒壁，包含环焊缝、曲面铭牌与接地端。"),
    ("TNK_02_TopHead", "上封头", "2:1 椭圆封头，顶部开孔供排气接管穿过。"),
    ("TNK_03_BottomHead", "下封头", "下封头将液体汇向排净口。支承环与支腿上端配合。"),
    ("TNK_04_Manway", "人孔颈", "包含检修开孔、颈部法兰和固定铰链座。"),
    ("TNK_05_Instruments", "液位与温压仪表", "玻璃管液位计、温度计、压力表及就地数显。屏幕数值用于静态演示。"),
    ("TNK_06_Ladder", "检修梯", "防滑横档、护笼和扶手通往上部检修平台。"),
    ("TNK_07_Platform", "顶部平台", "格栅踏面、护栏和斜撑构成排气口附近的检修平台。"),
    ("TNK_08_Inlet", "进口管路", "DN100 进口接管、隔离阀、法兰、盲板及管托。"),
    ("TNK_09_Outlet", "出口阀组", "底部排净管、隔离阀、手轮及落地管托。"),
    ("TNK_10_ManwayCover", "人孔盖", "带垫片、紧固件和把手，沿法兰轴向退出后移至侧方。"),
    ("TNK_11_TopAccessories", "顶部附件", "排气防雨帽、两只带通孔的吊耳及曲面安装座。"),
)

# Paths are absolute offsets in Blender metres. Fixed shell and legs establish
# the reference; detachable welded heads are shown as an educational exploded view.
ZERO = (0, 0, 0)
LIFT = (0, 0, 1.8)
MOTION = (
    ((0, ZERO), (.67, ZERO), (.77, LIFT), (.82, LIFT), (.88, (0, 0, .65)),
     (1, (0, 2.75, .10))),
    ((0, ZERO), (.67, ZERO), (.77, LIFT)),
    ((0, ZERO), (.60, ZERO), (.66, (0, 0, 2.25)), (.77, (0, 0, 4.65))),
    ((0, ZERO), (.67, ZERO), (.77, LIFT), (.90, LIFT), (1, (0, 0, .78))),
    ((0, ZERO), (.25, ZERO), (.285, (0, -.85, 0)), (.33, (2.25, -.85, 0))),
    ((0, ZERO), (.17, ZERO), (.21, (0, -1.0, 0)), (.25, (0, -1.75, 0))),
    ((0, ZERO), (.42, ZERO), (.45, (0, .75, 0)), (.50, (-1.60, .75, 0))),
    ((0, ZERO), (.34, ZERO), (.38, (0, 1.15, 0)), (.42, (2.65, 1.15, 0))),
    ((0, ZERO), (.09, ZERO), (.125, (0, -.8, 0)), (.17, (2.85, -.8, 0))),
    ((0, ZERO), (.67, ZERO), (.77, LIFT), (.80, (0, 0, 1.65)), (.86, (0, -2.45, 1.65))),
    ((0, ZERO), (.045, (0, -.85, 0)), (.09, (-1.95, -.85, 0))),
    ((0, ZERO), (.51, ZERO), (.55, (0, 0, .65)), (.60, (-3.25, 0, .65))),
)
ANCHORS = (
    "TNK_00_Legs_TopRing", "TNK_01_Shell_Nameplate", "TNK_02_TopHead_Crown",
    "TNK_03_BottomHead_DrainNozzle", "TNK_04_Manway_NeckFlange",
    "TNK_05_Instruments_LevelGauge", "TNK_06_Ladder_Cage",
    "TNK_07_Platform_Grating", "TNK_08_Inlet_Flange",
    "TNK_09_Outlet_Valve", "TNK_10_ManwayCover_CoverPlate",
    "TNK_11_TopAccessories_VentCap",
)


def displacement(index, progress):
    knots = MOTION[index]
    for (t0, p0), (t1, p1) in zip(knots, knots[1:]):
        if progress <= t1:
            t = max(0, min(1, (progress-t0)/(t1-t0)))
            t = t*t*t*(10+t*(-15+6*t))
            return tuple(a+(b-a)*t for a, b in zip(p0, p1))
    return knots[-1][1]


def manifest(groups, meshes):
    from math import isclose

    def owner(obj):
        while obj.parent:
            if obj.parent in groups:
                return obj.parent.name
            obj = obj.parent
        raise ValueError("Unowned tank mesh: " + obj.name)

    members = {name: [] for name, _, _ in COMPONENTS}
    for obj in meshes:
        members[owner(obj)].append(obj.name)
    if any(not items for items in members.values()):
        raise ValueError("Tank component has no mesh")
    return {
        "coordinateSystem": "Blender XYZ metres; Z up; Unity conversion (x,z,-y)",
        "motionVersion": 1,
        "durationSeconds": 14,
        "envelopeInterpretation": "Vessel axis y=+0.30 m; 3.00 m shell diameter gives front projection 1.20 m and rear projection 1.80 m.",
        "parts": [
            {
                "name": name, "label": label, "description": description,
                "start": index, "anchor": ANCHORS[index],
                "path": [{"time": t, "offset": dict(zip(("x", "y", "z"), point))}
                         for t, point in MOTION[index]],
                "meshes": sorted(members[name]),
            }
            for index, (name, label, description) in enumerate(COMPONENTS)
        ],
    }


def animate_groups(scene, groups):
    import bpy
    from mathutils import Vector

    homes = [group.location.copy() for group in groups]
    for i, group in enumerate(groups):
        group.animation_data_clear()
        for frame in range(1, 321):
            progress = min(1, (frame-1)/149) if frame <= 170 else (320-frame)/149
            group.location = homes[i] + Vector(displacement(i, progress))
            group.keyframe_insert("location", frame=frame, group="Assembly")
        action = group.animation_data.action
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation = "LINEAR"
    scene.frame_set(1)


def write_manifest(path, groups, meshes):
    Path(path).write_text(json.dumps(manifest(groups, meshes), ensure_ascii=False, indent=2),
                          encoding="utf-8")
