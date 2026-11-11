"""One component and motion contract shared by Blender and the Unity export."""

COMPONENTS = (
    ("PMP_00_Base", "底座", "Base", 1, (0, 0, 0)),
    ("PMP_01_Motor", "驱动电机", "Motor", 19, (1, 0, .06)),
    ("PMP_02_Coupling", "联轴器与轴", "Coupling / shaft", 80, (.8, -.60, .15)),
    ("PMP_03_Guard", "安全护罩", "Safety guard", 1, (.08, -.80, .60)),
    ("PMP_04_Volute", "泵体蜗壳", "Volute", 1, (0, 0, 0)),
    ("PMP_05_InletCover", "吸入端盖", "Inlet cover", 37, (-.95, -.50, .08)),
    ("PMP_06_SealBearing", "机械密封与支座", "Mechanical seal / pedestal", 126, (.28, .65, .12)),
    ("PMP_07_Impeller", "叶轮与叶片", "Impeller / vanes", 65, (-.62, .28, .16)),
    ("PMP_08_TerminalLid", "接线盒盖", "Terminal lid", 19, (1, 0, .41)),
    ("PMP_09_ImpellerFrontShroud", "叶轮前盖板", "Impeller front shroud", 65, (-1.15, .12, .50)),
    ("PMP_10_BearingHousing", "轴承外壳", "Bearing housing", 105, (.50, .50, .25)),
    ("PMP_11_BearingSet", "轴承总成", "Bearing set", 105, (.22, -.35, .25)),
)
GROUP_NAMES = tuple(p[0] for p in COMPONENTS)
OFFSETS = tuple(p[4] for p in COMPONENTS)
STARTS = tuple(p[3] for p in COMPONENTS)

# Absolute displacements in Blender metres. Nested parts share the extraction
# leg before separating, and reassembly evaluates this same path backwards.
MOTION = (
    ((0, (0, 0, 0)),),
    ((0, (0, 0, 0)), (.12, (0, 0, 0)), (.15, (0, 0, .06)),
     (.24, (1, 0, .06))),
    ((0, (0, 0, 0)), (.53, (0, 0, 0)), (.63, (.8, 0, 0)),
     (.66, (.8, -.60, .15))),
    ((0, (0, 0, 0)), (.02, (-.055, 0, 0)),
     (.08, (-.055, 0, .85)), (.10, (.08, -.80, .85)),
     (.12, (.08, -.80, .60))),
    ((0, (0, 0, 0)),),
    ((0, (0, 0, 0)), (.24, (0, 0, 0)), (.36, (-.95, 0, 0)),
     (.43, (-.95, -.50, .08))),
    ((0, (0, 0, 0)), (.91, (0, 0, 0)), (.955, (.28, 0, 0)),
     (1, (.28, .65, .12))),
    ((0, (0, 0, 0)), (.43, (0, 0, 0)), (.53, (-.62, 0, 0)),
     (.62, (-.62, .28, .16))),
    ((0, (0, 0, 0)), (.12, (0, 0, 0)), (.15, (0, 0, .06)),
     (.24, (1, 0, .06)), (.30, (1, 0, .41))),
    ((0, (0, 0, 0)), (.43, (0, 0, 0)), (.53, (-.62, 0, 0)),
     (.62, (-.62, .28, .16)), (.70, (-1.15, .28, .16)),
     (.76, (-1.15, .12, .50))),
    ((0, (0, 0, 0)), (.66, (0, 0, 0)), (.74, (.50, 0, 0)),
     (.80, (.50, .50, .25))),
    ((0, (0, 0, 0)), (.66, (0, 0, 0)), (.74, (.50, 0, 0)),
     (.80, (.50, .50, .25)), (.86, (.22, .50, .25)),
     (.91, (.22, -.35, .25))),
)
ANCHORS = (
    "PMP_00_Base_Tag", "PMP_01_Motor_Nameplate", "PMP_02_Coupling_MachinedHalf",
    "PMP_03_Guard_Perforated1mmSheet", "PMP_04_Volute_DataPlate",
    "PMP_05_InletCover_ContouredCover", "PMP_06_SealBearing_SealChamber",
    "PMP_07_Impeller_BackShroud", "PMP_01_Motor_TerminalLid",
    "PMP_07_Impeller_ConicalFrontShroud", "PMP_06_SealBearing_BearingHousing",
    "PMP_06_SealBearing_BearingOuterRace",
)


def displacement(index, progress):
    knots = MOTION[index]
    for (t0, p0), (t1, p1) in zip(knots, knots[1:]):
        if progress <= t1:
            t = max(0, min(1, (progress-t0)/(t1-t0)))
            t = t*t*t*(10+t*(-15+6*t))
            return tuple(a+(b-a)*t for a, b in zip(p0, p1))
    return knots[-1][1]


def animate_groups(scene, groups):
    """Bake the shared trajectory, including an exactly reversed return."""
    import bpy
    from mathutils import Vector
    scene.frame_set(1)
    initial = [g.location.copy() for g in groups]
    for i, (group, home) in enumerate(zip(groups, initial)):
        group.animation_data_clear()
        if not any(OFFSETS[i]):
            continue
        for frame in range(1, 321):
            p = min(1, (frame-1)/149) if frame <= 170 else (320-frame)/149
            group.location = home+Vector(displacement(i, p))
            group.keyframe_insert("location", frame=frame, group="Assembly")
        action = group.animation_data.action
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation = "LINEAR"
    scene.frame_set(1)


def manifest(groups, meshes):
    def owner(obj):
        while obj.parent:
            if obj.parent in groups:
                return obj.parent.name
            obj = obj.parent
        raise ValueError("Unowned equipment mesh: " + obj.name)

    members = {name: [] for name in GROUP_NAMES}
    for obj in meshes:
        members[owner(obj)].append(obj.name)
    if any(not value for value in members.values()):
        raise ValueError("Empty component in pump contract")
    return {
        "coordinateSystem": "Blender XYZ metres; Z up",
        "motionVersion": 3,
        "durationSeconds": 14,
        "meshCount": len(meshes),
        "parts": [
            dict(name=name, label=label, start=start-1,
                 offset=dict(zip(("x", "y", "z"), offset)), anchor=ANCHORS[i],
                 path=[dict(time=t, offset=dict(zip(("x", "y", "z"), p)))
                       for t, p in MOTION[i]],
                 meshes=sorted(members[name]))
            for i, (name, label, _, start, offset) in enumerate(COMPONENTS)
        ],
    }
