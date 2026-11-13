"""Shared fan assembly and staged extraction paths, in Blender metres."""
COMPONENTS = (
    ("FAN_00_Base", "Vibration isolation base"),
    ("FAN_01_Duct", "Duct and mounting flanges"),
    ("FAN_02_Impeller", "Eight-blade impeller"),
    ("FAN_03_InletGuard", "Inlet safety grille"),
    ("FAN_04_Motor", "Drive motor"),
    ("FAN_05_OutletGuard", "Outlet and bird grille"),
    ("FAN_06_Struts", "Guide vanes and motor cradle"),
    ("FAN_07_TerminalBox", "Terminal box"),
    ("FAN_08_AccessCover", "Curved inspection cover"),
    ("FAN_09_MotorVentCover", "Motor ventilation cover"),
    ("FAN_10_TerminalLid", "Terminal enclosure lid"),
)
GROUP_NAMES = tuple(item[0] for item in COMPONENTS)
AXIS_HEIGHT = 1.04
TARGET_SIZE = (1.60, 1.60, 1.80)


def ancestors(obj):
    result = []
    while obj.parent:
        obj = obj.parent
        result.append(obj)
    return result

# Clear both guards and the rear hood before withdrawing the drive cassette.
# Motor and cradle travel together, then separate outside the duct.
PATHS = (
    ((0, (0, 0, 0)), (1, (0, 0, 0))),
    ((0, (0, 0, 0)), (1, (0, 0, 0))),
    ((0, (0, 0, 0)), (.40, (0, 0, 0)), (.76, (-1.30, 0, 0)), (1, (-1.30, 0, 0))),
    ((0, (0, 0, 0)), (.18, (-.50, 0, 0)), (.34, (-.68, -1.70, 0)),
     (1, (-.68, -1.70, 0))),
    ((0, (0, 0, 0)), (.52, (0, 0, 0)), (.84, (1.70, 0, 0)),
     (.96, (1.00, 0, 0)), (1, (1.00, -.25, 0))),
    ((0, (0, 0, 0)), (.18, (.55, 0, 0)), (.34, (.72, 1.70, 0)),
     (1, (.72, 1.70, 0))),
    ((0, (0, 0, 0)), (.52, (0, 0, 0)), (.84, (1.70, 0, 0)),
     (1, (1.70, 0, 0))),
    ((0, (0, 0, 0)), (.28, (0, 0, 0)), (.46, (0, 0, .42)),
     (.64, (0, -.82, .46)), (1, (0, -.82, .46))),
    # Clear the shell screws and adjacent box before lifting above the duct.
    ((0, (0, 0, 0)), (.08, (0, 0, 0)), (.14, (0, -.08, .04)),
     (.22, (-.20, -.08, .04)), (.40, (-.20, -.16, .94)),
     (.48, (-.20, -.16, .94)),
     (.76, (-.48, -.16, .94)), (1, (-.68, -.16, .94))),
    ((0, (0, 0, 0)), (.34, (0, 0, 0)), (.42, (.26, 0, 0)),
     (.50, (.26, .25, .95)), (.82, (1.80, .25, .95)), (1, (1.80, .25, .95))),
    # Park the lid axially clear before moving the box body.
    ((0, (0, 0, 0)), (.14, (0, -.25, 0)), (.28, (.46, -.25, 0)),
     (.44, (.46, -.55, .42)),
     (.58, (.40, -1.20, .42)), (1, (.40, -1.20, .42))),
)


def displacement(index, progress):
    progress = min(1.0, max(0.0, progress))
    path = PATHS[index]
    for (a, start), (b, end) in zip(path, path[1:]):
        if progress <= b:
            t = max(0, (progress-a)/(b-a))
            t = t*t*(3-2*t)
            return tuple(x+(y-x)*t for x, y in zip(start, end))
    return path[-1][1]


def animate(groups):
    for i, group in enumerate(groups):
        for frame in range(1, 151):
            group.location = displacement(i, (frame-1)/149)
            group.keyframe_insert(data_path="location", frame=frame, group="Assembly")
            group.keyframe_insert(data_path="location", frame=321-frame, group="Assembly")
        group.location = displacement(i, 1)
        group.keyframe_insert(data_path="location", frame=170, group="Assembly")
        action = group.animation_data.action
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation = "LINEAR"


def manifest(groups, meshes):
    return {
        "model": "industrial-fan", "units": "metres", "axis": "Blender X",
        "rotorNode": "industrial-fan_rotor", "rotorLocalAxis": "Z",
        "axisHeight": AXIS_HEIGHT, "motionVersion": 4,
        "oneWaySeconds": 14,
        "components": [
            {"index": i, "name": group.name, "label": COMPONENTS[i][1],
             "meshCount": sum(group in ancestors(o) for o in meshes),
             "path": [{"t": t, "offset": list(p)} for t, p in PATHS[i]]}
            for i, group in enumerate(groups)
        ],
    }
