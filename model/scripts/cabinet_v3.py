"""Cabinet-specific, editable automation layout and large operator console."""
import math

import bpy
from mathutils import Vector


def rebuild(c):
    global C, M, G
    C, M, G = c, c.M, c.groups
    shell()
    operator_door()
    fixed_controls()
    protection()
    drives()
    control()
    power()
    network()
    safety()
    contactors()
    field_terminals()
    cable_routes()


def box(name, at, size, mat, group, bevel=.0007):
    if min(size) <= .006:
        bevel = 0
    return C.box(name, at, size, M[mat], G[group], bevel)


def screw(name, x, y, z, group, radius=.003):
    C.screw(name, x, y, z, G[group], radius)


def print_at(name, kind, x, y, z, w, h, group):
    return C.printed(name, kind, x, y, z, w, h, G[group])


def wire(name, points, mat, radius=.002, group=7):
    return C.wire(name, points, M[mat], radius, G[group])


def remove(obj):
    bpy.data.objects.remove(obj, do_unlink=True)


def rail(name, x, z, w, group):
    C.din_rail(name, x, z, w, G[group])
    for px in (x-w/2+.010, x+w/2-.010):
        box(name+"_EndClamp", (px, .179, z), (.014, .023, .049), "zinc", group)
        screw(name+"_ClampScrew", px, .165, z, group)


def led(name, x, y, z, group, color="green_lens", r=.0016):
    C.cylinder(name, (x, y, z), r, .001, M[color], G[group],
               axis=C.FRONT, segments=8, bevel=0)


def connector(name, x, y, z, group, connected=False):
    C.frame(name+"_Shield", x, y, z, .020, .019, .0015, .006,
            M["zinc"], G[group])
    box(name+"_Cavity", (x, y+.002, z), (.016, .002, .015), "black", group, 0)
    for i in range(8):
        box(name+"_GoldContact", (x-.006+i*.0017, y-.0002, z+.004),
            (.0006, .0003, .005), "copper", group, 0)
    if connected:
        box(name+"_Plug", (x, y-.009, z), (.0135, .018, .011),
            "white", group, .0005)
        box(name+"_Boot", (x, y-.022, z), (.012, .014, .012),
            "wire_pe", group, .0006)
        box(name+"_Latch", (x, y-.007, z-.007), (.005, .014, .001),
            "white", group, .0003)


def shell():
    for obj in list(G[0].children):
        if any(s in obj.name for s in ("Plinth", "Anchor", "CableGland", "BottomPanel")):
            world = obj.matrix_world.copy()
            obj.parent = G[10]
            obj.matrix_world = world
            obj.name = obj.name.replace("CAB_00_Enclosure_", "CAB_10_PlinthGlands_")
        elif any(s in obj.name for s in ("SidePanel", "RearPanel", "TopPanel")):
            obj.data.materials[0] = M["paint"]
    for x in (-.53, .53):
        for z in (.055, .105):
            screw("CAB_10_PlinthGlands_AccessFastener", x, -.326, z, 10)
    for x in (-.46, -.23, 0, .23, .46):
        box("CAB_10_PlinthGlands_CableClampingRail", (x, .094, .224),
            (.036, .025, .018), "zinc", 10)
    # Recessed removable side panel with compressed perimeter gasket.
    for y in (-.273, .288):
        box("CAB_00_Enclosure_SidePanelJoint", (.6001, y, 1.055),
            (.0002, .0018, 1.688), "rubber", 0, 0)
    for z in (.211, 1.899):
        box("CAB_00_Enclosure_SidePanelJoint", (.6001, .0075, z),
            (.0002, .563, .0018), "rubber", 0, 0)
    side = next(o for o in G[0].children if "SidePanel" in o.name
                and sum(v.co.x for v in o.data.vertices) > 0)
    C.cut_box(side, (.599, .06, 1.68), (.03, .24, .22))
    C.frame("CAB_00_Enclosure_ExhaustSupport", 0, 0, 0, .26, .24, .012,
            .010, M["blue"], G[0])
    # Turn the front-oriented grille frame to the right side.
    frame_parts = [o for o in G[0].children if "ExhaustSupport" in o.name]
    for obj in frame_parts:
        obj.rotation_euler.z = math.pi/2
        obj.location = Vector((.599, .06, 1.68))+Vector((-obj.location.y, obj.location.x, obj.location.z))
    for i in range(10):
        obj = box("CAB_00_Enclosure_SideExhaustLouver",
                  (.601, .06, 1.588+i*.020), (.013, .222, .007), "grey", 0)
        obj.rotation_euler.y = -.3
    for x in (-.496, .496):
        box("CAB_01_Backplate_PunchedMountingUpright", (x, .219, 1.08),
            (.021, .008, 1.58), "zinc", 1)
        for i in range(28):
            box("CAB_01_Backplate_MountingSlot", (x, .2147, .32+i*.055),
                (.007, .0003, .014), "black", 1, 0)


def operator_door():
    keep = ("FoldedReturn", "EPDMSeal", "Hinge", "Lock", "LiftHandle",
            "HandleStandoff", "InnerCableTrunk", "Bonding")
    for obj in list(G[3].children):
        if not any(s in obj.name for s in keep):
            remove(obj)
    panel = C.solid_sheet("CAB_03_Door_1p5mmFoldedPanel",
                         [(-.574, -.280, .178), (.286, -.280, .178),
                          (.286, -.280, 1.927), (-.574, -.280, 1.927)], M["steel"], G[3])
    for x, z, w, h in ((-.173, 1.51, .586, .360),
                        (-.173, .967, .526, .206),
                        (-.17, .553, .232, .232)):
        C.cut_box(panel, (x, -.280, z), (w, .045, h))
    C.frame("CAB_03_Door_WindowGasket", -.173, -.285, .967, .555, .233,
            .016, .008, M["black"], G[3])
    C.box("CAB_03_Door_InspectionGlass", (-.173, -.286, .967),
          (.523, .003, .201), M["glass"], G[3], .0005)
    for x in (-.439, .093):
        for z in (.858, 1.076):
            screw("CAB_03_Door_WindowFastener", x, -.291, z, 3, .0024)
    print_at("CAB_03_Door_RatingPlate", "rating", -.32, -.281, .298, .19, .075, 3)
    print_at("CAB_03_Door_SystemIdentifier", "operator", -.20, -.281, 1.827, .55, .055, 3)
    # Integrated touch panel with milled rim, cover glass and tactile controls.
    g = 15
    C.frame("CAB_15_OperatorHMI_MachinedRim", -.173, -.288, 1.478,
            .694, .582, .008, .014, M["aluminium"], G[g])
    box("CAB_15_OperatorHMI_ConsoleFascia", (-.173, -.293, 1.478),
        (.678, .016, .566), "blue", g, .001)
    box("CAB_15_OperatorHMI_ScreenGasket", (-.173, -.302, 1.54),
        (.615, .003, .364), "rubber", g, .001)
    C.frame("CAB_15_OperatorHMI_CoverGlassBorder", -.173, -.306, 1.54,
            .607, .356, .014, .004, M["black"], G[g])
    screen = box("CAB_15_OperatorHMI_HMIDisplay", (-.173, -.3085, 1.54),
                 (.576, .001, .324), "black", g, 0)
    screen.data.materials.append(M["hmi"])
    screen["screen_uv"] = True
    screen["label_width_m"], screen["print_height_m"] = .576, .324
    print_at("CAB_15_OperatorHMI_TopSilkscreen", "operator",
             -.173, -.302, 1.729, .57, .057, g)
    box("CAB_15_OperatorHMI_HMIRearHousing", (-.173, -.233, 1.54),
        (.596, .072, .344), "grey", g, .0015)
    for x in (-.412, .066):
        for z in (1.385, 1.695):
            box("CAB_15_OperatorHMI_ClampBracket", (x, -.228, z),
                (.025, .032, .017), "zinc", g)
            screw("CAB_15_OperatorHMI_ClampScrew", x, -.248, z, g)
    # Separate industrial membrane keys and a rotary encoder below the glass.
    for i, mat in enumerate(("white", "white", "green_lens", "red_lens")):
        x = -.406+i*.086
        box("CAB_15_OperatorHMI_KeySocket", (x, -.304, 1.268),
            (.063, .004, .037), "black", g)
        box("CAB_15_OperatorHMI_MembraneKey", (x, -.307, 1.268),
            (.054, .003, .028), mat, g, .0012)
        box("CAB_15_OperatorHMI_KeyMark", (x, -.309, 1.268),
            (.013, .0002, .002), "black", g, 0)
    C.cylinder("CAB_15_OperatorHMI_EncoderBezel", (.045, -.306, 1.268),
               .026, .007, M["aluminium"], G[g], axis=C.FRONT, segments=48, bevel=.001)
    C.cylinder("CAB_15_OperatorHMI_EncoderKnob", (.045, -.319, 1.268),
               .021, .022, M["black"], G[g], axis=C.FRONT, segments=48, bevel=.001)
    for i in range(24):
        a = i*math.tau/24
        box("CAB_15_OperatorHMI_EncoderKnurl",
            (.045+.0208*math.cos(a), -.318, 1.268+.0208*math.sin(a)),
            (.0018, .014, .0018), "grey", g, .0003)
    print_at("CAB_15_OperatorHMI_ControlLegend", "zones",
             -.173, -.302, 1.212, .56, .025, g)
    for x in (-.479, .133):
        for z in (1.224, 1.733):
            screw("CAB_15_OperatorHMI_FasciaFastener", x, -.303, z, g, .0025)
    # A covered door duct carries the HMI harness down to the hinge side.
    box("CAB_15_OperatorHMI_DoorDuctBase", (-.506, -.229, .985),
        (.038, .009, .94), "grey", 15, .0006)
    for side in (-1, 1):
        box("CAB_15_OperatorHMI_DoorDuctWall",
            (-.506+side*.017, -.209, .985), (.003, .035, .94),
            "grey", 15, .0005)
    box("CAB_15_OperatorHMI_DoorDuctLid", (-.506, -.190, .985),
        (.041, .004, .94), "grey", 15, .0005)
    box("CAB_15_OperatorHMI_HorizontalDuctBase", (-.343, -.229, 1.305),
        (.326, .009, .038), "grey", 15, .0005)
    for side in (-1, 1):
        box("CAB_15_OperatorHMI_HorizontalDuctWall",
            (-.343, -.209, 1.305+side*.017), (.326, .035, .003),
            "grey", 15, .0004)
    box("CAB_15_OperatorHMI_HorizontalDuctLid", (-.343, -.190, 1.305),
        (.329, .004, .041), "grey", 15, .0004)
    for z in (.43, .74, 1.05, 1.36, 1.67):
        box("CAB_15_OperatorHMI_DoorDuctRetainer",
            (-.506, -.184, z), (.045, .008, .010), "zinc", 15, .001)
    for x in (-.465, -.343, -.221):
        box("CAB_15_OperatorHMI_HorizontalDuctRetainer",
            (x, -.184, 1.305), (.010, .008, .045), "zinc", 15, .001)
    box("CAB_15_OperatorHMI_EthernetLeadSaddle",
        (-.12, -.184, 1.305), (.014, .008, .012), "zinc", 15, .001)
    wire("CAB_15_OperatorHMI_HMIPowerLoom",
         [(-.18, -.197, 1.40), (-.18, -.211, 1.305),
          (-.343, -.211, 1.305), (-.506, -.211, 1.305),
          (-.506, -.211, .75), (-.506, -.211, .39),
          (-.548, -.220, .39), (-.588, -.252, .39),
          (-.574, -.279, .39)],
         "black", .006, 15)
    wire("CAB_15_OperatorHMI_EthernetLead",
         [(-.05, -.197, 1.40), (-.05, -.211, 1.305),
          (-.12, -.211, 1.305), (-.343, -.211, 1.305),
          (-.506, -.211, 1.305), (-.506, -.211, .75),
          (-.506, -.211, .39), (-.548, -.220, .39),
          (-.588, -.252, .39), (-.574, -.279, .39)],
         "wire_blue", .0025, 15)


def fixed_controls():
    for obj in list(G[8].children):
        remove(obj)
    C.solid_sheet("CAB_08_ControlPanel_FixedServiceStrip",
                  [(.302, -.283, .173), (.568, -.283, .173),
                   (.568, -.283, 1.930), (.302, -.283, 1.930)], M["steel"], G[8])
    box("CAB_08_ControlPanel_OperatorInlay", (.435, -.285, 1.531),
        (.172, .003, .657), "blue", 8, .001)
    for i, (z, color, key) in enumerate(((1.766, "green_lens", "run"),
                                        (1.605, "amber_lens", "alarm"),
                                        (1.444, "green_lens", "power"))):
        C.cylinder("CAB_08_ControlPanel_PilotCollar", (.435, -.292, z),
                   .015, .012, M["black"], G[8], axis=C.FRONT, segments=16)
        C.ring("CAB_08_ControlPanel_PilotBezel", (.435, -.300, z),
               .014, .011, .004, M["zinc"], G[8], axis=C.FRONT, segments=16, bevel=.0004)
        C.cylinder(f"cabinet_lamp_lens_{i}", (.435, -.304, z),
                   .011, .004, M[color], G[8], axis=C.FRONT, segments=16, bevel=.0006)
        C.plaque("CAB_08_ControlPanel_PilotTag", key, .435, -.288, z-.039, .19, G[8])
    C.cylinder("CAB_08_ControlPanel_ModeSelector", (.435, -.295, 1.26),
               .016, .014, M["zinc"], G[8], axis=C.FRONT, segments=32)
    box("CAB_08_ControlPanel_SelectorGrip", (.435, -.308, 1.26),
        (.009, .018, .029), "black", 8, .0012)
    C.plaque("CAB_08_ControlPanel_ModeTag", "auto", .435, -.288, 1.215, .20, G[8])
    C.cylinder("CAB_08_ControlPanel_EstopLegendRing", (.435, -.289, 1.04),
               .041, .006, M["yellow"], G[8], axis=C.FRONT, segments=48, bevel=.0005)
    C.cylinder("CAB_08_ControlPanel_EstopNeck", (.435, -.303, 1.04),
               .013, .025, M["black"], G[8], axis=C.FRONT, segments=32)
    C.cylinder("CAB_08_ControlPanel_EstopMushroom", (.435, -.321, 1.04),
               .024, .014, M["red"], G[8], axis=C.FRONT, segments=48, bevel=.002)
    C.plaque("CAB_08_ControlPanel_EstopTag", "estop", .435, -.285, .976, .22, G[8])
    box("CAB_08_ControlPanel_IsolatorBase", (.435, -.291, .76),
        (.083, .013, .083), "yellow", 8, .0015)
    C.cylinder("CAB_08_ControlPanel_IsolatorHub", (.435, -.303, .76),
               .022, .015, M["red"], G[8], axis=C.FRONT, segments=32)
    lever = box("CAB_08_ControlPanel_IsolatorLever", (.435, -.317, .76),
                (.015, .021, .065), "red", 8, .0015)
    lever.rotation_euler.y = -.6
    C.plaque("CAB_08_ControlPanel_IsolatorTag", "main", .435, -.285, .694, .20, G[8])
    print_at("CAB_08_ControlPanel_HazardSticker", "hazard", .435, -.284, .43, .078, .094, 8)
    for x in (.319, .551):
        for z in (.19, 1.912):
            screw("CAB_08_ControlPanel_CaptiveScrew", x, -.286, z, 8)


def protection():
    rail("CAB_04_Breakers_DIN", -.236, 1.724, .485, 4)
    box("CAB_04_Breakers_MCCBBody", (-.397, .134, 1.724),
        (.121, .108, .172), "grey", 4, .001)
    box("CAB_04_Breakers_MCCBFace", (-.397, .071, 1.724),
        (.101, .020, .132), "black", 4, .001)
    box("CAB_04_Breakers_MCCBToggleRecess", (-.397, .060, 1.725),
        (.063, .004, .056), "grey", 4, .0005)
    box("CAB_04_Breakers_MCCBToggle", (-.397, .047, 1.725),
        (.042, .023, .018), "black", 4, .001)
    print_at("CAB_04_Breakers_Q0Data", "mcb", -.397, .059, 1.767, .071, .029, 4)
    for z in (1.645, 1.803):
        for dx in (-.036, 0, .036):
            box("CAB_04_Breakers_MCCBTerminalCage", (-.397+dx, .091, z),
                (.027, .015, .023), "black", 4)
            screw("CAB_04_Breakers_MCCBTerminal", -.397+dx, .081, z, 4, .0038)
    for i in range(6):
        x = -.282+i*.053
        before = set(G[4].children)
        C.breaker(x, 1.728, i+1, G[4])
        for obj in set(G[4].children)-before:
            for v in obj.data.vertices:
                v.co.x *= .64
                if obj.get("label_key") or obj.get("print_kind"):
                    v.co.z *= .64
            obj.location.x = x+(obj.location.x-x)*.64
            if "label_width_m" in obj:
                obj["label_width_m"] *= .64
            if "print_height_m" in obj:
                obj["print_height_m"] *= .64
    C.plaque("CAB_04_Breakers_Q0Tag", "q0", -.38, .211, 1.864, .22, G[4])


def drives():
    # The two ventilated drives occupy the power side, with front service clearance.
    for i, x in enumerate((-.360, -.143)):
        prefix = f"CAB_12_VariableSpeedDrives_VFD{i+1}"
        z = 1.339
        box(prefix+"_AluminiumHeatSink", (x, .150, z),
            (.172, .135, .373), "aluminium", 12)
        for j in range(10):
            box(prefix+"_CoolingFin", (x-.077+j*.017, .103, z),
                (.005, .102, .355), "aluminium", 12, .0004)
        box(prefix+"_MouldedShell", (x, .024, z),
            (.181, .125, .366), "grey", 12, .0015)
        box(prefix+"_FrontServiceCover", (x, -.044, z-.010),
            (.168, .016, .334), "drive", 12, .001)
        box(prefix+"_UpperVentRecess", (x, -.054, z+.129),
            (.140, .004, .049), "black", 12, .0005)
        for j in range(9):
            box(prefix+"_IntakeVentRib", (x-.062+j*.0155, -.057, z+.129),
                (.005, .006, .043), "grey", 12, .0004)
        box(prefix+"_LocalPanel", (x, -.055, z+.035),
            (.131, .008, .102), "black", 12)
        print_at(prefix+"_LocalDisplay", "drive_display", x, -.060, z+.060,
                 .105, .038, 12)
        for j in range(4):
            box(prefix+"_LocalKey", (x-.042+j*.028, -.063, z+.006),
                (.017, .006, .013), "grey" if j < 2 else "green_lens" if j == 2 else "red_lens",
                12, .0008)
        print_at(prefix+"_DriveSilkscreen", "drive", x, -.054, z-.080, .132, .067, 12)
        for j in range(11):
            box(prefix+"_LowerCoolingSlot", (x-.069+j*.0138, -.054, z-.143),
                (.006, .003, .026), "black", 12, .0003)
        for dz in (-.197, .197):
            box(prefix+"_MountingEar", (x, .158, z+dz),
                (.128, .026, .025), "zinc", 12)
            for dx in (-.044, .044):
                screw(prefix+"_FixingBolt", x+dx, .142, z+dz, 12, .004)
        for j in range(4):
            px = x-.056+j*.037
            box(prefix+"_PowerTerminal", (px, .032, z-.192),
                (.027, .034, .024), "grey", 12)
            screw(prefix+"_TerminalClamp", px, .013, z-.192, 12, .003)
        C.plaque(prefix+"_Tag", f"q{i+1}", x, .212, 1.579, .15, G[12])


def control():
    rail("CAB_05_PLC_DIN", .281, 1.378, .385, 5)
    x, z = .153, 1.378
    box("CAB_05_PLC_CPUCase", (x, .124, z), (.107, .123, .192), "grey", 5, .001)
    box("CAB_05_PLC_CPUFront", (x, .055, z), (.099, .019, .170), "grey", 5)
    print_at("CAB_05_PLC_CPUSilkscreen", "cpu", x, .044, z+.014, .087, .072, 5)
    for dz in (-.074, .074):
        box("CAB_05_PLC_CPUPluggableTerminal", (x, .044, z+dz),
            (.091, .014, .022), "black", 5)
        for j in range(8):
            screw("CAB_05_PLC_CPUTerminalClamp", x-.037+j*.0105, .034, z+dz, 5, .0017)
    connector("CAB_05_PLC_Profinet", x+.018, .038, z-.040, 5, True)
    for i in range(3):
        led("CAB_05_PLC_CPUStatusLED", x-.032, .043, z+.023-i*.012, 5)
    for i in range(4):
        px = .232+i*.058
        box("CAB_05_PLC_IOModule", (px, .123, z), (.053, .124, .192), "grey", 5)
        box("CAB_05_PLC_IOTerminalDoor", (px, .053, z), (.048, .018, .167), "grey", 5)
        print_at("CAB_05_PLC_IOSilkscreen", "io", px, .043, z+.008, .042, .079, 5)
        for dz in (-.070, .070):
            box("CAB_05_PLC_IOPluggableConnector", (px, .042, z+dz),
                (.043, .018, .024), "black", 5)
            for j in range(4):
                screw("CAB_05_PLC_IOClamp", px-.015+j*.010, .030, z+dz, 5, .0015)
        for j in range(4):
            led("CAB_05_PLC_IOChannelLED", px-.014+j*.009, .042, z-.025, 5, r=.0012)
    C.plaque("CAB_05_PLC_Tag", "cpu", .27, .211, 1.522, .28, G[5])


def power():
    rail("CAB_09_PowerSupply_DIN", .282, 1.744, .38, 9)
    for i, (x, width) in enumerate(((.184, .155), (.374, .145))):
        prefix = "CAB_09_PowerSupply_"+("ACDC24V" if i == 0 else "DCUPS")
        box(prefix+"_Body", (x, .128, 1.727), (width, .112, .201), "aluminium", 9)
        box(prefix+"_Front", (x, .064, 1.727), (width-.012, .018, .185), "white", 9)
        print_at(prefix+"_Specification", "supply", x, .053, 1.764, width-.029, .052, 9)
        for j in range(7):
            box(prefix+"_CoolingSlot", (x-width*.34+j*width*.113, .053, 1.711),
                (.005, .003, .032), "black", 9, .0003)
        box(prefix+"_TerminalStrip", (x, .052, 1.655),
            (width-.026, .024, .026), "teal", 9)
        for j in range(5):
            screw(prefix+"_Terminal", x-(width-.046)/2+j*(width-.046)/4, .037, 1.655, 9, .0022)
        led(prefix+"_DCOK", x-.045, .052, 1.727, 9)
    C.plaque("CAB_09_PowerSupply_Tag", "psu", .27, .211, 1.864, .28, G[9])


def network():
    rail("CAB_13_IndustrialNetwork_DIN", .283, 1.025, .38, 13)
    x, z = .270, 1.029
    box("CAB_13_IndustrialNetwork_ManagedSwitchMetalCase", (x, .130, z),
        (.249, .105, .188), "aluminium", 13)
    for px in (.136, .404):
        box("CAB_13_IndustrialNetwork_SwitchMountingEar", (px, .164, z),
            (.020, .018, .178), "zinc", 13)
        for dz in (-.072, .072):
            screw("CAB_13_IndustrialNetwork_MountingBolt", px, .151, z+dz, 13)
    box("CAB_13_IndustrialNetwork_DarkFrontFace", (x, .069, z),
        (.239, .016, .178), "blue", 13)
    print_at("CAB_13_IndustrialNetwork_SwitchSilkscreen", "network",
             x, .059, z+.051, .217, .047, 13)
    for row in range(2):
        for col in range(4):
            px, pz = .181+col*.040, 1.027-row*.038
            connector(f"CAB_13_IndustrialNetwork_RJ45_{row*4+col+1}",
                      px, .055, pz, 13, col < 3)
            led("CAB_13_IndustrialNetwork_LinkLED", px-.012, .054, pz+.007, 13)
            led("CAB_13_IndustrialNetwork_ActivityLED", px-.012, .054, pz-.002,
                13, "amber_lens", .0011)
    box("CAB_13_IndustrialNetwork_Redundant24VTerminal", (.37, .052, 1.052),
        (.022, .018, .036), "teal", 13)
    for dz in (-.010, 0, .010):
        screw("CAB_13_IndustrialNetwork_PowerClamp", .37, .041, 1.052+dz, 13, .0015)
    for i in range(8):
        box("CAB_13_IndustrialNetwork_HeatsinkSlot", (.159+i*.031, .123, 1.126),
            (.012, .060, .002), "black", 13, .0003)
    C.plaque("CAB_13_IndustrialNetwork_Tag", "switch", .275, .211, 1.173, .28, G[13])
    # Shielded field-network couplers below the managed switch.
    box("CAB_13_IndustrialNetwork_FieldPatchCarrier", (.273, .134, .858),
        (.27, .082, .056), "black", 13)
    for i in range(4):
        connector("CAB_13_IndustrialNetwork_FieldPatch", .175+i*.062, .086, .858, 13, True)


def safety():
    rail("CAB_14_SafetyRelays_DIN", .283, .686, .38, 14)
    for i in range(2):
        x = .155+i*.070
        box("CAB_14_SafetyRelays_SafetyLogicModule", (x, .133, .686),
            (.059, .100, .126), "yellow", 14)
        box("CAB_14_SafetyRelays_GreyFace", (x, .075, .686),
            (.051, .015, .093), "grey", 14)
        print_at("CAB_14_SafetyRelays_Silkscreen", "safety", x, .066, .689, .044, .064, 14)
        for dz in (-.051, .051):
            box("CAB_14_SafetyRelays_Plug", (x, .071, .686+dz),
                (.050, .025, .019), "black", 14)
        for j in range(3):
            led("CAB_14_SafetyRelays_StatusLED", x-.013+j*.013, .065, .675, 14, r=.0012)
    for i in range(8):
        x = .292+i*.020
        box("CAB_14_SafetyRelays_InterfaceSocket", (x, .142, .686),
            (.017, .078, .094), "black", 14)
        box("CAB_14_SafetyRelays_InterfaceRelay", (x, .095, .686),
            (.014, .017, .067), "white", 14)
        box("CAB_14_SafetyRelays_RetainingClip", (x, .084, .693),
            (.009, .003, .041), "teal", 14, .0004)
        led("CAB_14_SafetyRelays_RelayLED", x, .082, .670, 14, r=.0011)


def contactors():
    rail("CAB_11_Contactors_DIN", -.252, .835, .455, 11)
    for i in range(3):
        x, z = -.39+i*.14, .837
        prefix = f"CAB_11_Contactors_KM{i+1}"
        box(prefix+"_MouldedBody", (x, .135, z), (.104, .100, .136), "black", 11)
        box(prefix+"_Face", (x, .075, z), (.096, .021, .082), "grey", 11)
        print_at(prefix+"_Print", "contactor", x, .063, z, .080, .050, 11)
        for dx in (-.034, 0, .034):
            for dz in (-.055, .055):
                box(prefix+"_RecessedCage", (x+dx, .081, z+dz),
                    (.025, .015, .023), "grey", 11)
                screw(prefix+"_Clamp", x+dx, .071, z+dz, 11, .003)
        box(prefix+"_AuxContact", (x+.054, .128, z), (.014, .070, .093), "white", 11)
        box(prefix+"_Overload", (x, .133, .725), (.100, .095, .063), "grey", 11)
        C.cylinder(prefix+"_OverloadDial", (x-.022, .080, .729), .011, .009,
                   M["blue"], G[11], axis=C.FRONT, segments=24, bevel=.0005)
        box(prefix+"_Reset", (x+.022, .078, .729), (.019, .009, .017), "red", 11)


def field_terminals():
    rail("CAB_02_Terminals_DIN", -.015, .470, .905, 2)
    for i in range(24):
        x = -.3655+i*.029
        mat = "yellow" if i >= 20 else "wire_blue" if i % 6 == 5 else "white"
        box("CAB_02_Terminals_FeedThroughTerminal", (x, .151, .470),
            (.026, .074, .069), mat, 2)
        for z in (.449, .491):
            box("CAB_02_Terminals_ClampRecess", (x, .111, z),
                (.017, .004, .017), "black", 2, 0)
            screw("CAB_02_Terminals_ScrewClamp", x, .108, z, 2, .0026)
        C.plaque("CAB_02_Terminals_Identification", f"t{i+1:02}", x, .111, .470,
                 .022, G[2])
    for i in range(5):
        x = -.3885+i*.145
        box("CAB_02_Terminals_GroupDivider", (x, .15, .470),
            (.006, .08, .080), "grey", 2)
    bus = box("CAB_02_Terminals_CopperPEBusbar", (-.015, .159, .320),
              (.85, .008, .025), "copper", 2)
    for x in (-.40, .37):
        C.cylinder("CAB_02_Terminals_PEStandOff", (x, .192, .320), .014,
                   .060, M["yellow"], G[2], axis=C.FRONT, segments=16, bevel=.0007)
    for i in range(16):
        x = -.40+i*.0515
        screw("CAB_02_Terminals_EarthClamp", x, .152, .320, 2, .003)
        C.ring("CAB_02_Terminals_RingCrimp", (x, .153, .320), .0055, .0027,
               .001, M["copper"], G[2], axis=C.FRONT, segments=12, bevel=0)
    C.plaque("CAB_02_Terminals_PELabel", "pe", .0, .149, .277, .28, G[2])


def duct(name, x, z, width, height, vertical=False, exposed=False):
    g = 7
    box(name+"_Base", (x, .209, z), (width, .008, height), "grey", g)
    length, span = (height, width) if vertical else (width, height)
    count = int(length/.022)
    # Comb fingers are real geometry, with covers broken only at cable entry.
    for i in range(count):
        p = -length/2+.011+i*.022
        for sign in (-1, 1):
            at = (x+sign*(span-.003)/2, .182, z+p) if vertical else (x+p, .182, z+sign*(span-.003)/2)
            size = (.003, .048, .014) if vertical else (.014, .048, .003)
            box(name+"_CombFinger", at, size, "grey", g, 0)
    if exposed:
        covers = [(-.18, .58)]
    else:
        covers = [(0, 1)]
    for offset, fraction in covers:
        at = (x, .154, z+offset*length) if vertical else (x+offset*length, .147, z)
        size = (width+.003, .005, height*fraction) if vertical else (width*fraction, .005, height+.003)
        box(name+"_RemovableLid", at, size, "white", g, .0007)


def cable_routes():
    for x in (-.493, .060, .484):
        duct("CAB_07_Wiring_VerticalDuct", x, 1.090, .039, 1.52, True)
    for z in (1.91, 1.588, .575):
        duct("CAB_07_Wiring_CrossDuct", -.005, z, .93, .040, exposed=True)
    duct("CAB_07_Wiring_PowerDuct", -.231, 1.057, .47, .040, exposed=True)
    duct("CAB_07_Wiring_ControlDuct", .273, 1.217, .377, .040, exposed=True)
    duct("CAB_07_Wiring_NetworkDuct", .273, .789, .377, .035, exposed=True)
    # Keep power wiring left, controls central, and the network bundle at the right.
    for i in range(6):
        x = -.282+i*.053
        for pole in range(3):
            px = x+(pole-1)*.015
            mat = ("wire_brown", "black", "grey")[pole]
            wire("CAB_07_Wiring_MCBFeed",
                 [(px, .142, 1.786), (px, .144, 1.823),
                  (px-.005, .184, 1.876), (px-.005, .184, 1.910)], mat, .0017)
            wire("CAB_07_Wiring_MCBLoad",
                 [(px, .142, 1.670), (px, .146, 1.642),
                  (px+.009, .183, 1.603), (px+.009, .183, 1.588)], mat, .0017)
    for i, x in enumerate((-.360, -.143)):
        for j in range(4):
            px = x-.056+j*.037
            mat = ("wire_brown", "black", "grey", "wire_pe")[j]
            wire("CAB_07_Wiring_DriveOutput",
                 [(px, .030, 1.147), (px, .032, 1.115),
                  (px+.007, .160, 1.090), (px+.007, .184, 1.057)], mat, .0032)
        for j in range(3):
            px = x-.040+j*.040
            wire("CAB_07_Wiring_DriveInput",
                 [(px, .090, 1.535), (px, .095, 1.556), (px, .183, 1.588)],
                 ("wire_brown", "black", "grey")[j], .0027)
    for i in range(3):
        x = -.39+i*.14
        for j in range(3):
            px = x-.034+j*.034
            for start, dest in ((.905, 1.057), (.692, .575)):
                wire("CAB_07_Wiring_ContactorPower",
                     [(px, .130, start), (px, .134, start+(dest-start)*.4),
                      (px+.005, .182, dest)], ("wire_brown", "black", "grey")[j], .0025)
    for i in range(4):
        x = .232+i*.058
        for j in range(4):
            px = x-.015+j*.010
            for start, dest, mat in ((1.474, 1.588, "wire_blue"), (1.282, 1.217, "black")):
                wire("CAB_07_Wiring_PLCChannel",
                     [(px, .112, start), (px, .112, start+(dest-start)*.45),
                      (px+.004, .182, dest)], mat, .0013)
    for x in (.184, .374):
        for j in range(5):
            px = x-.044+j*.022
            wire("CAB_07_Wiring_24VFeed",
                 [(px, .065, 1.641), (px, .065, 1.621), (px+.003, .184, 1.588)],
                 "wire_blue" if j % 2 else "red", .0018)
    # Patch leads follow the switch edge and enter the network duct without
    # crossing device faces; green/yellow is reserved for protective earth.
    for row in range(2):
        for col in range(3):
            x, z = .181+col*.040, 1.027-row*.038
            lane_x = .305+col*.026
            dest_z = .789
            wire("CAB_07_Wiring_IndustrialEthernet",
                 [(x, .027, z), (x, .018, z-.014),
                  (lane_x, .018, z-.014), (lane_x, .112, z-.027),
                  (lane_x, .184, dest_z)],
                 "wire_blue", .0025)
    for z in (.943, .854):
        box("CAB_07_Wiring_NetworkTieMount", (.437, .150, z),
            (.018, .010, .012), "black", 7, .0005)
    wire("CAB_07_Wiring_CPUProfinet",
         [(.171, .010, 1.338), (.171, .018, 1.312), (.300, .018, 1.300),
          (.326, .112, 1.270), (.326, .184, 1.217)], "wire_blue", .0025)
    for i in range(4):
        x = .175+i*.062
        lane_x = .300+i*.021
        wire("CAB_07_Wiring_FieldNetworkLead",
             [(x, .058, .858), (x, .030, .842), (lane_x, .030, .825),
              (lane_x, .145, .807), (lane_x, .184, .789)], "wire_blue", .0023)
    for x in (.155, .225):
        for j in range(3):
            px = x-.016+j*.016
            wire("CAB_07_Wiring_SafetyLoop",
                 [(px, .111, .749), (px, .112, .764), (px+.004, .182, .789)],
                 "wire_blue", .0015)
            wire("CAB_07_Wiring_SafetyReturn",
                 [(px, .111, .623), (px, .112, .605), (px+.004, .182, .575)],
                 "black", .0015)
    # Field cores emerge from five jacketed cables and terminate at the
    # underside clamp positions of X1; no conductor ends free in the cabinet.
    for i in range(24):
        x = -.3655+i*.029
        mat = "wire_blue" if i % 3 else "black"
        wire("CAB_07_Wiring_TerminalUpper",
             [(x, .149, .505), (x, .15, .529), (x+.005, .184, .557), (x+.005, .184, .575)],
             mat, .0015)
        C.cylinder("CAB_07_Wiring_FerruleSleeve", (x, .149, .522), .0025,
                   .011, M["white"], G[7], axis=(0, 0, 1), segments=6, bevel=0)
    cable_centers = (-.34, -.17, 0, .17, .34)
    for i, x in enumerate(cable_centers):
        first = i*5
        last = min(first+5, 24)
        core_colors = ("wire_blue", "black", "wire_brown", "grey", "wire_blue")
        for terminal_index in range(first, last):
            tx = -.3655+terminal_index*.029
            mat = core_colors[(terminal_index-first) % len(core_colors)]
            wire("CAB_07_Wiring_FieldTermination",
                 [(tx, .108, .449), (tx, .130, .414),
                  (x+(tx-x)*.38, .160, .365), (x, .178, .330)],
                 mat, .00135)
        wire("CAB_07_Wiring_ShieldedFieldCable",
             [(x, .178, .330), (x, .168, .275), (x, .120, .214),
              (x, .055, .176), (x, .055, .142)],
             "black", .006)
        C.ring("CAB_07_Wiring_ShieldClamp", (x, .12, .275), .009, .007,
               .016, M["zinc"], G[7], axis=(0, 0, 1), segments=12, bevel=0)
        box("CAB_07_Wiring_FieldCableClamp", (x, .085, .205),
            (.024, .018, .010), "zinc", 7, .0007)
    # PE conductors terminate under the copper busbar screws and bond the
    # backplate at a visible lug instead of fanning loosely across it.
    for i in range(5):
        x = -.36+i*.172
        pe_x = -.40+i*.192
        wire("CAB_07_Wiring_ProtectiveEarth",
             [(x, .149, .320), (x, .171, .338),
              (pe_x, .189, .355), (pe_x, .189, .320)],
             "wire_pe", .0020)
    wire("CAB_07_Wiring_BackplateBond",
         [(.46, .206, .32), (.445, .204, .305), (.405, .204, .305),
          (.37, .181, .320)], "wire_pe", .003)
    for z in (.93, 1.08, 1.18):
        box("CAB_07_Wiring_NetworkSaddle", (.459, .165, z),
            (.014, .020, .010), "black", 7, .0005)
