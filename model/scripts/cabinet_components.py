"""Shared cabinet hierarchy and reversible native Blender assembly motion."""
import math

from mathutils import Vector

GROUP_NAMES = (
    "CAB_00_Enclosure", "CAB_01_Backplate", "CAB_02_Terminals",
    "CAB_03_Door", "CAB_04_Breakers", "CAB_05_PLC",
    "CAB_06_Cooling", "CAB_07_Wiring",
    "CAB_08_ControlPanel", "CAB_09_PowerSupply",
    "CAB_10_PlinthGlands", "CAB_11_Contactors",
    "CAB_12_VariableSpeedDrives", "CAB_13_IndustrialNetwork",
    "CAB_14_SafetyRelays", "CAB_15_OperatorHMI",
)
DOOR_ATTACHED = (3, 6, 15)
HINGE = (-.574, -.279, .16)
DOOR_ANGLE = math.radians(-120)
OFFSETS = (
    (0, 0, 0), (1.55, -1.08, 0), (-1.25, -.75, -.12), (0, 0, 0),
    (1.25, -.37, .23), (1.25, -.36, -.28), (-.13, -.91, .02),
    (0, -1.0, .02),
    (1.95, .06, 0), (1.38, -.38, .38),
    (0, 0, -.35), (1.3, -.35, .30), (-1.75, -.45, -.32),
    (1.4, -.40, -.26), (1.4, -.38, -.78), (-.45, -1.2, .45),
)


def smooth(t):
    t = max(0, min(1, t))
    return t*t*(3-2*t)


def pose(index, progress):
    door = DOOR_ANGLE*smooth(progress/.25)
    move = smooth((progress-.30)/.70)
    return Vector(OFFSETS[index])*move, door if index in DOOR_ATTACHED else 0


def animate(scene, groups):
    homes = [g.location.copy() for g in groups]
    for step in range(121):
        t = step/120
        for index, group in enumerate(groups):
            offset, angle = pose(index, t)
            group.location = homes[index]+offset
            group.rotation_euler.z = angle
            for frame in (1+step, 281-step):
                group.keyframe_insert("location", frame=frame)
                group.keyframe_insert("rotation_euler", frame=frame)
    for g in groups:
        if g.animation_data and g.animation_data.action:
            for layer in g.animation_data.action.layers:
                for strip in layer.strips:
                    for slot in g.animation_data.action.slots:
                        bag = strip.channelbag(slot)
                        if bag:
                            for curve in bag.fcurves:
                                for point in curve.keyframe_points:
                                    point.interpolation = "LINEAR"
    scene.frame_start, scene.frame_end = 1, 281
    for frame, text in ((1, "ASSEMBLED"), (31, "DOOR OPEN 100 DEG"),
                        (121, "EXPLODED"), (161, "REASSEMBLY"),
                        (251, "DOOR CLOSE"), (281, "ASSEMBLED")):
        scene.timeline_markers.new(text, frame=frame)
    scene.frame_set(1)
