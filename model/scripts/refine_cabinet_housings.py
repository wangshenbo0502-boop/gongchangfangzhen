"""Replace block-like cabinet devices with serviceable moulded assemblies."""
import argparse
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import refine_cabinet_industrial as shared
from industrial_parts import mesh, cylinder, activate, boolean
from pump_surface_finish import render_device

MODEL = HERE.parent
TEXTURES = MODEL / "textures/electrical-cabinet/housing-review"
OUTPUT = MODEL / "source/electrical-cabinet-housing-review.blend"
SCENE = shared.SCENE
REPORT = {"revision": "formed-device-housings", "operations": {}}
M = {}


def delete_matching(predicate):
    data_ids = {o.data.as_pointer() for o in SCENE.objects
                if o.type == "MESH" and predicate(o)}
    count = 0
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.data.as_pointer() in data_ids:
            bpy.data.objects.remove(obj, do_unlink=True)
            count += 1
    return count


def complete(obj, index):
    # Triangulate before physical UVs so chamfer corner patches cannot stretch.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return shared.share_new(obj, index)


def shape(name, vertices, faces, mat, index, bevel=0):
    return complete(mesh(name, vertices, faces, M.get(mat, mat),
                         shared.group(SCENE, index), bevel), index)


def block(name, at, size, mat, index, bevel=.0005, segments=1):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector(tuple(v.co[i]*size[i] for i in range(3)))
    if bevel:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel,
                       segments=segments, affect="EDGES")
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    data = bpy.data.meshes.new(name)
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    SCENE.collection.objects.link(obj)
    obj.location = at
    obj.parent = shared.group(SCENE, index)
    obj.matrix_parent_inverse = obj.parent.matrix_world.inverted()
    data.materials.append(M.get(mat, mat))
    return shared.share_new(obj, index)


def profile_x(name, x, width, yz, mat, index, bevel=.0004):
    # Chamfer the moulded cross-section directly instead of subdividing every edge.
    if bevel:
        rounded = []
        for i, point in enumerate(yz):
            p = Vector(point)
            a, b = Vector(yz[i-1])-p, Vector(yz[(i+1) % len(yz)])-p
            reach = min(bevel, a.length*.20, b.length*.20)
            rounded.extend([tuple(p+a.normalized()*reach), tuple(p+b.normalized()*reach)])
        yz = rounded
    n = len(yz)
    vertices = [(xx, y, z) for xx in (x-width/2, x+width/2) for y, z in yz]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces += [(i, (i+1) % n, (i+1) % n+n, i+n) for i in range(n)]
    return shape(name, vertices, faces, mat, index)


def fascia(name, x, y, z, w, h, depth, mat, index, corner=.003):
    c = min(corner, w*.18, h*.18)
    outline = [(-w/2+c, -h/2), (w/2-c, -h/2), (w/2, -h/2+c),
               (w/2, h/2-c), (w/2-c, h/2), (-w/2+c, h/2),
               (-w/2, h/2-c), (-w/2, -h/2+c)]
    # The front rim has its own physical edge break; rear edges seat into the case.
    rim = min(.00035, depth*.18)
    vertices = [(x+xx*(1-inset/(w/2)), yy, z+zz*(1-inset/(h/2)))
                for yy, inset in ((y, rim), (y+rim, 0), (y+depth, 0))
                for xx, zz in outline]
    faces = [tuple(reversed(range(8))), tuple(range(16, 24))]
    faces += [(k*8+i, k*8+(i+1) % 8, (k+1)*8+(i+1) % 8, (k+1)*8+i)
              for k in range(2) for i in range(8)]
    return shape(name, vertices, faces, mat, index)


def well(name, x, front, z, w, h, depth, mat, index):
    # A tapered open tunnel, with a real aperture and a recessed screw behind it.
    outer = [(-w/2, -h/2), (w/2, -h/2), (w/2, h/2), (-w/2, h/2)]
    inner = [(xx*.68, zz*.64) for xx, zz in outer]
    vertices = [(x+xx, y, z+zz) for y in (front, front+depth)
                for loop in (outer, inner) for xx, zz in loop]
    faces = []
    for i in range(4):
        j = (i+1) % 4
        faces.extend([(i, j, 4+j, 4+i), (8+i, 12+i, 12+j, 8+j),
                      (i, 8+i, 8+j, j), (4+i, 4+j, 12+j, 12+i)])
    return shape(name, vertices, faces, mat, index)


def screw(name, x, y, z, radius, index):
    obj = cylinder(name+"_SlottedHead", (x, y, z), radius, .0014, M["nickel"],
                   shared.group(SCENE, index), axis=(0, -1, 0), segments=8, bevel=0)
    complete(obj, index)
    block(name+"_ToolSlot", (x, y-.00074, z), (radius*1.35, .00012, .0007),
          "dark", index, 0)


def image(name, filename, x, y, z, w, h, index, roughness=.58, glass=False):
    mat, bsdf = shared.simple_material("CAB / housing print / "+filename,
                                      (.2, .2, .2), roughness)
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(str(TEXTURES / (filename+".png")), check_existing=True)
    tex.image.pack()
    uv = mat.node_tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "LabelUV"
    mat.node_tree.links.new(uv.outputs[0], tex.inputs["Vector"])
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Specular IOR Level"].default_value = .27
    if glass:
        bsdf.inputs["Roughness"].default_value = .15
        bsdf.inputs["Coat Weight"].default_value = .24
    obj = block(name, (x, y+.00010, z), (w, .00020, h), "dark", index, 0)
    obj.data.materials.append(mat)
    label_uv = obj.data.uv_layers.new(name="LabelUV")
    for face in obj.data.polygons:
        if face.normal.y < -.9:
            face.material_index = 1
        for li in face.loop_indices:
            co = obj.data.vertices[obj.data.loops[li].vertex_index].co
            label_uv.data[li].uv = (co.x/w+.5, co.z/h+.5)
    obj.data.uv_layers.active = obj.data.uv_layers["UVMap"]
    return obj


def material(name, color, roughness=.56):
    mat, bsdf = shared.simple_material("CAB / formed "+name, color, roughness)
    bsdf.inputs["IOR"].default_value = 1.48
    bsdf.inputs["Specular IOR Level"].default_value = .29
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    uv = nodes.new("ShaderNodeUVMap")
    uv.uv_map = "FinishUV"
    scale = nodes.new("ShaderNodeVectorMath")
    scale.operation = "SCALE"
    scale.inputs[3].default_value = 1/.045
    links.new(uv.outputs[0], scale.inputs[0])
    maps = {}
    for channel in ("BaseColor", "Normal", "Roughness"):
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(str(TEXTURES / ("mould_"+channel+".png")),
                                         check_existing=True)
        if channel != "BaseColor":
            tex.image.colorspace_settings.name = "Non-Color"
        tex.image.pack()
        links.new(scale.outputs[0], tex.inputs["Vector"])
        maps[channel] = tex
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1
    multiply.inputs[2].default_value = (*color, 1)
    links.new(maps["BaseColor"].outputs["Color"], multiply.inputs[1])
    links.new(multiply.outputs[0], bsdf.inputs["Base Color"])
    rough = nodes.new("ShaderNodeMapRange")
    rough.inputs["From Min"].default_value = .35
    rough.inputs["From Max"].default_value = .72
    rough.inputs["To Min"].default_value = roughness-.055
    rough.inputs["To Max"].default_value = roughness+.055
    links.new(maps["Roughness"].outputs["Color"], rough.inputs[0])
    links.new(rough.outputs[0], bsdf.inputs["Roughness"])
    normal = nodes.new("ShaderNodeNormalMap")
    normal.inputs["Strength"].default_value = .48
    normal.uv_map = "FinishUV"
    links.new(maps["Normal"].outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs[0], bsdf.inputs["Normal"])
    mat["finish_tile_m"] = .045
    mat["finish_family"] = "injection-moulded ABS/PA"
    return mat


def materials():
    for key, color, rough in (
        ("charcoal", (.038, .045, .044), .52),
        ("dark", (.009, .012, .011), .62),
        ("grey", (.24, .265, .25), .55),
        ("light", (.53, .55, .51), .57),
        ("ivory", (.49, .51, .46), .59),
        ("blue", (.018, .10, .18), .56),
        ("yellow", (.57, .40, .012), .59),
        ("green", (.012, .12, .035), .61),
        ("red", (.28, .014, .008), .60),
        ("teal", (.019, .13, .11), .56),
    ):
        M[key] = material(key, color, rough)
    M["nickel"] = bpy.data.materials["CAB / nickel fasteners"]
    M["aluminium"] = bpy.data.materials["CAB / aluminium"]
    glass, bsdf = shared.simple_material("CAB / relay smoked polycarbonate",
                                         (.18, .22, .19), .24)
    bsdf.inputs["Transmission Weight"].default_value = .42
    bsdf.inputs["IOR"].default_value = 1.58
    M["relayglass"] = glass


def drives():
    routing = json.loads((MODEL / "reports/electrical-cabinet-workmanship-review.json").read_text())
    output_routes = [r for r in routing["routes"] if r["kind"].startswith("DriveOutput")]
    for number, x in enumerate((-.360, -.143), 1):
        p = f"CAB_12_VariableSpeedDrives_VFD{number}"
        # Keep original terminals and mounting hardware at their wired positions.
        delete_matching(lambda o: o.name.startswith(p) and not any(k in o.name for k in
                        ("_Tag", "_MountingEar", "_FixingBolt", "_PowerTerminal", "_TerminalClamp")))
        z = 1.339
        block(p+"_HeatSinkBase", (x, .199, z), (.171, .024, .365),
              "aluminium", 12)
        for j in range(11):
            block(p+"_ExtrudedCoolingFin", (x-.083+j*.0166, .130, z),
                  (.0032, .117, .355), "aluminium", 12, 0)
        yz = [(.113, z-.180), (.113, z+.180), (.011, z+.180),
              (-.033, z+.155), (-.040, z+.103), (-.040, z-.115),
              (-.021, z-.158), (.012, z-.180)]
        profile_x(p+"_TaperedRearHousing", x, .167, yz, "charcoal", 12, .001)
        # The front is split into a keypad carrier, lower service door and vents.
        fascia(p+"_KeypadCarrier", x, -.053, z+.041, .153, .133, .012,
               "grey", 12, .005)
        fascia(p+"_RecessedKeypad", x, -.0555, z+.046, .132, .112, .003,
               "charcoal", 12, .004)
        for side in (-1, 1):
            block(p+"_DisplayRebate", (x+side*.055, -.058, z+.070),
                  (.004, .005, .041), "dark", 12, .0004)
        # Retain the actual drive display art, now set into a shallower opening.
        source = bpy.data.materials.get("CAB / equipment print / drive_display")
        display = block(p+"_LCDWindow", (x, -.0582, z+.070),
                        (.105, .001, .038), "dark", 12, 0)
        if source:
            display.data.materials.append(source)
            uv = display.data.uv_layers["UVMap"]
            for face in display.data.polygons:
                if face.normal.y < -.9:
                    face.material_index = 1
                for li in face.loop_indices:
                    co = display.data.vertices[display.data.loops[li].vertex_index].co
                    uv.data[li].uv = (co.x+.0525, co.z+.019)
            # A separate texture coordinate avoids distorting the measured UV set.
            art = display.data.uv_layers.new(name="DisplayUV")
            for face in display.data.polygons:
                for li in face.loop_indices:
                    co = display.data.vertices[display.data.loops[li].vertex_index].co
                    # The square source art reserves empty space above/below.
                    art.data[li].uv = (co.x/.105+.5, co.z/.038*.36+.5)
            shared.physical_uv(display.data)
            # Copy the existing material so only these two LCDs use DisplayUV.
            mat, bsdf = shared.simple_material("CAB / recessed drive LCD "+str(number),
                                              (.01, .03, .02), .18)
            source_image = next(n.image for n in source.node_tree.nodes
                                if n.type == "TEX_IMAGE" and n.image)
            tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
            tex.image = source_image
            coord = mat.node_tree.nodes.new("ShaderNodeUVMap")
            coord.uv_map = "DisplayUV"
            mat.node_tree.links.new(coord.outputs[0], tex.inputs["Vector"])
            mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
            mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = .07
            bsdf.inputs["Coat Weight"].default_value = .16
            display.data.materials[1] = mat
        for j in range(4):
            keymat = "grey" if j < 2 else "green" if j == 2 else "red"
            fascia(p+"_MembraneKey", x-.044+j*.029, -.0595, z+.023,
                   .022, .016, .0013, keymat, 12, .003)
        image(p+"_KeyLegend", "vfd_keypad", x, -.0578, z+.011, .116, .010, 12)
        fascia(p+"_RemovableLowerCover", x, -.052, z-.070, .153, .091,
               .010, "charcoal", 12, .004)
        image(p+"_FlushRatingPlate", "vfd_rating", x, -.0523, z-.067,
              .112, .056, 12)
        # Both vent banks are open geometry, recessed into the housing face.
        for center_z, height, slats in ((z+.137, .044, 6), (z-.143, .044, 6)):
            block(p+"_VentDarkInterior", (x, -.026, center_z),
                  (.138, .004, height), "dark", 12, 0)
            for side in (-1, 1):
                block(p+"_VentSideWall", (x+side*.073, -.040, center_z),
                      (.006, .025, height+.008), "charcoal", 12)
            for j in range(slats+1):
                zz = center_z-height/2+j*height/slats
                profile_x(p+"_OpenLouvre", x, .140,
                          [(-.052, zz-.001), (-.047, zz-.001),
                           (-.033, zz+.0014), (-.033, zz+.003),
                           (-.039, zz+.003), (-.052, zz+.0005)],
                          "grey", 12, 0)
        for side in (-1, 1):
            block(p+"_CoverRelease", (x+side*.073, -.051, z-.104),
                  (.008, .005, .014), "grey", 12, .0007)
            block(p+"_AssemblySeam", (x+side*.081, .033, z),
                  (.001, .003, .292), "dark", 12, 0)
        # Visible supply plugs account for the existing three input leads.
        for dx in (-.040, 0, .040):
            block(p+"_SupplyPlug", (x+dx, .106, 1.525),
                  (.033, .025, .020), "dark", 12)
            screw(p+"_SupplyClamp", x+dx, .092, 1.526, .003, 12)
        # The original output blocks were solid where each lead bends into them.
        for j, route in enumerate(output_routes[(number-1)*4:number*4]):
            start, next_point = (Vector(point) for point in route["points"][:2])
            target = min((o for o in SCENE.objects if o.name.startswith(p+"_PowerTerminal")),
                         key=lambda o: (o.matrix_world.translation-start).length)
            original = target.data
            target.data = original.copy()
            direction = (next_point-start).normalized()
            cutter = cylinder("DriveCableEntryTool", start+direction*.005, .0046, .040,
                              None, axis=direction, segments=10, bevel=0)
            boolean(target, cutter)
            bm = bmesh.new()
            bm.from_mesh(target.data)
            bmesh.ops.triangulate(bm, faces=list(bm.faces))
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bm.to_mesh(target.data)
            bm.free()
            shared.physical_uv(target.data)
            shared.replace_data(original, target.data)
    REPORT["operations"]["drives"] = "Tapered multi-part bodies, deep open louvres, exposed heat sinks, recessed keypad/LCD and flush rating plates."


def contactors():
    delete_matching(lambda o: o.name.startswith("CAB_11_Contactors_KM"))
    for i, x in enumerate((-.39, -.25, -.11), 1):
        p, z = f"CAB_11_Contactors_KM{i}", .837
        profile_x(p+"_CoilHousing", x, .101,
                  [(.181, z-.065), (.181, z+.065), (.120, z+.065),
                   (.108, z+.045), (.093, z+.037), (.093, z-.037),
                   (.108, z-.045), (.120, z-.065)], "charcoal", 11, .0007)
        fascia(p+"_ContactChamber", x, .073, z, .090, .072, .028,
               "grey", 11, .005)
        block(p+"_ActuatorRecess", (x, .070, z+.022), (.037, .005, .017),
              "dark", 11)
        block(p+"_MechanicalIndicator", (x, .066, z+.022), (.017, .003, .009),
              "charcoal", 11)
        image(p+"_CoilRatings", "contactor", x, .0727, z-.007, .074, .025, 11)
        for sign in (-1, 1):
            zz = z+sign*.055
            block(p+"_PoleCarrier", (x, .113, zz), (.099, .020, .024),
                  "dark", 11)
            for dx in (-.034, 0, .034):
                well(p+"_TerminalTunnel", x+dx, .074, zz, .029, .025, .021,
                     "grey", 11)
                screw(p+"_Terminal", x+dx, .090, zz, .0032, 11)
            for dx in (-.051, -.017, .017, .051):
                block(p+"_PhaseBarrier", (x+dx, .090, zz+sign*.003),
                      (.0025, .041, .031), "charcoal", 11, 0)
        # Auxiliary side module has its own joints and release latch.
        profile_x(p+"_AuxiliaryContact", x+.058, .014,
                  [(.166, z-.047), (.166, z+.047), (.092, z+.047),
                   (.083, z+.032), (.083, z-.033), (.096, z-.047)],
                  "light", 11)
        block(p+"_AuxRelease", (x+.058, .080, z), (.009, .006, .019),
              "charcoal", 11)
        profile_x(p+"_ThermalRelayBody", x, .100,
                  [(.179, .694), (.179, .756), (.099, .756), (.085, .747),
                   (.085, .709), (.100, .694), (.114, .694), (.114, .708),
                   (.131, .708), (.131, .694)], "charcoal", 11, .0006)
        fascia(p+"_OverloadFace", x, .079, .733, .094, .040, .009,
               "grey", 11, .003)
        image(p+"_OverloadRating", "overload", x, .0787, .746, .086, .010, 11)
        for dx, radius in ((-.024, .009), (.023, .007)):
            obj = cylinder(p+"_DialRecess", (x+dx, .077, .726), radius+.002,
                           .002, M["dark"], shared.group(SCENE, 11),
                           axis=(0, -1, 0), segments=12, bevel=0)
            complete(obj, 11)
            obj = cylinder(p+"_Adjuster" if dx < 0 else p+"_Reset",
                           (x+dx, .0755, .726), radius, .0025,
                           M["grey" if dx < 0 else "blue"], shared.group(SCENE, 11),
                           axis=(0, -1, 0), segments=12, bevel=0)
            complete(obj, 11)
        block(p+"_DialPointer", (x-.024, .074, .728), (.0009, .0002, .008),
              "ivory", 11, 0)
        block(p+"_TestButton", (x+.004, .075, .727), (.008, .002, .006),
              "red", 11, .0006)
        for dx in (-.034, 0, .034):
            well(p+"_LoadTerminal", x+dx, .108, .704, .028, .018, .025,
                 "charcoal", 11)
            screw(p+"_LoadClamp", x+dx, .126, .704, .0026, 11)
    REPORT["operations"]["contactors"] = "Stepped coil housings, divided pole chambers, recessed terminals, auxiliary latches and thermal relay controls."


def terminals():
    delete_matching(lambda o: o.name.startswith("CAB_02_Terminals_") and any(
        k in o.name for k in ("FeedThroughTerminal", "ClampRecess", "ScrewClamp", "GroupDivider")))
    # Paired end towers and recessed central marker bridges are visible in silhouette.
    yz = [(.188, .439), (.188, .457), (.181, .457), (.181, .481),
          (.188, .481), (.188, .501), (.153, .505), (.126, .505),
          (.133, .501), (.133, .481), (.124, .479), (.124, .461),
          (.133, .459), (.133, .439), (.127, .435), (.153, .435)]
    for i in range(24):
        x = -.3655+i*.029
        p = f"CAB_02_Terminals_X1_{i+1:02}"
        mat = "yellow" if i >= 20 else "blue" if i % 6 == 5 else "ivory"
        body = profile_x(p+"_ProfiledInsulator", x, .025, yz, mat, 2, .00035)
        original = body.data
        body.data = original.copy()
        for zz in (.501, .439):
            cutter = cylinder("TerminalBoreTool", (x, .138, zz), .0023, .016,
                              None, axis=(0, 0, 1), segments=8, bevel=0)
            # All review states share this mesh, so the cut propagates to each.
            boolean(body, cutter)
        bm = bmesh.new()
        bm.from_mesh(body.data)
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(body.data)
        bm.free()
        shared.physical_uv(body.data)
        shared.replace_data(original, body.data)
        for zz in (.449, .491):
            # Open screw tunnel, backed by the metal pressure screw.
            well(p+"_ScrewTunnel", x, .111, zz, .020, .020, .014, mat, 2)
            block(p+"_ClampCage", (x, .127, zz), (.0135, .004, .012),
                  "dark", 2, 0)
            screw(p+"_Clamp", x, .1235, zz, .0031, 2)
        block(p+"_MarkerBridge", (x, .115, .470), (.0225, .005, .010),
              "light", 2)
        # The DIN rail foot and release nib remain distinct from the wire entries.
        block(p+"_RailFoot", (x, .184, .470), (.022, .009, .026),
              "charcoal", 2, 0)
        block(p+"_ReleaseNib", (x, .173, .435), (.012, .015, .004),
              mat, 2, 0)
    for i in (0, 5, 11, 17, 24):
        x = -.380+i*.029
        profile_x("CAB_02_Terminals_EndPartition", x, .002,
                  [(.19, .430), (.19, .510), (.125, .510), (.109, .501),
                   (.109, .440), (.125, .430)], "grey", 2, .0002)
    REPORT["operations"]["terminals"] = "24 stepped PA insulators with open screw tunnels, metal clamp cages, marker bridges and DIN feet; wire insertion coordinates preserved."


def safety():
    delete_matching(lambda o: o.name.startswith("CAB_14_SafetyRelays_") and
                    "_DIN" not in o.name)
    for i, x in enumerate((.155, .225), 1):
        p = f"CAB_14_SafetyRelays_SR{i}"
        yz = [(.182, .625), (.182, .747), (.120, .747), (.101, .733),
              (.086, .725), (.086, .647), (.101, .639), (.120, .625)]
        profile_x(p+"_MouldedHousing", x, .057, yz, "yellow", 14, .0008)
        fascia(p+"_RecessedFace", x, .078, .686, .048, .091, .008,
               "charcoal", 14, .002)
        image(p+"_LaserMarking", "safety", x, .0777, .686, .041, .082, 14)
        for sign in (-1, 1):
            zz = .686+sign*.055
            block(p+"_DetachableTerminalBank", (x, .119, zz),
                  (.053, .041, .018), "grey", 14)
            for dx in (-.016, 0, .016):
                well(p+"_PlugTunnel", x+dx, .085, zz, .014, .015, .026,
                     "charcoal", 14)
                screw(p+"_CaptiveScrew", x+dx, .105, zz, .002, 14)
            block(p+"_TerminalLatch", (x+.026, .113, zz),
                  (.003, .025, .010), "yellow", 14)
        for j in range(4):
            obj = cylinder(p+"_StatusLED", (x+.0135, .0768, .698-j*.0055),
                           .0009, .0005, M["green"],
                           shared.group(SCENE, 14), axis=(0, -1, 0), segments=8, bevel=0)
            complete(obj, 14)
        # Actual side slots, with a recessed shadow visible between the ribs.
        for side in (-1, 1):
            block(p+"_HousingSplit", (x+side*.0286, .146, .686),
                  (.0004, .001, .095), "dark", 14, 0)
            for j in range(5):
                block(p+"_SideRelief", (x+side*.0288, .150+j*.005, .686),
                      (.0005, .0018, .049), "grey", 14, 0)
    for i in range(8):
        x, p = .292+i*.020, f"CAB_14_SafetyRelays_KA{i+1}"
        profile_x(p+"_DINSocket", x, .017,
                  [(.180, .637), (.180, .735), (.128, .735), (.106, .725),
                   (.106, .647), (.128, .637)], "charcoal", 14)
        block(p+"_RelayMechanism", (x, .105, .686), (.010, .009, .055),
              "grey", 14)
        block(p+"_CopperCoil", (x, .099, .684), (.008, .005, .022),
              bpy.data.materials["CAB / copper"], 14, .0005)
        fascia(p+"_ClearRelayCover", x, .088, .686, .014, .066, .022,
               "relayglass", 14, .001)
        block(p+"_RetainingLever", (x, .0835, .699), (.006, .005, .035),
              "teal", 14, .0006)
        for zz in (.642, .730):
            well(p+"_SocketTunnel", x, .106, zz, .014, .010, .016, "grey", 14)
        block(p+"_Marker", (x, .0865, .660), (.009, .001, .006),
              "ivory", 14, 0)
    REPORT["operations"]["safety"] = "Tapered yellow bodies, detachable multi-well plugs and smoked relay covers with visible mechanisms."


def plc_details():
    # Keep the accepted connector locations and all routed PLC leads.
    for obj in list(SCENE.objects):
        if obj.type != "MESH" or not any(k in obj.name for k in
                                       ("CAB_05_PLC_CPUCase", "CAB_05_PLC_IOModule")):
            continue
        old = obj.data
        center = sum((obj.matrix_world @ v.co for v in old.vertices), Vector())/len(old.vertices)
        x = center.x
        width = .107 if "CPUCase" in obj.name else .053
        data_obj = profile_x(obj.name+"_ShoulderedMoulding", x, width,
                            [(.184, 1.284), (.184, 1.472), (.091, 1.472),
                             (.063, 1.457), (.063, 1.299), (.091, 1.284)],
                            "grey", 5, .0006)
        # The replacement uses absolute vertices; remove all old shared copies.
        delete_matching(lambda o: o.data == old)
        for dz in (-.091, .091):
            for j in range(4 if width < .06 else 7):
                xx = x-width*.36+j*width*.12
                block("CAB_05_PLC_MouldedVentLip", (xx, .127, 1.378+dz),
                      (.002, .072, .003), "charcoal", 5, 0)
        for dz in (-.077, .077):
            block("CAB_05_PLC_CoverLatch", (x+width*.40, .043, 1.378+dz),
                  (.004, .006, .009), "dark", 5, .0003)
    REPORT["operations"]["plc"] = "Shouldered rear shells, moulded vent lips and service cover latches, preserving plug and wire positions."


def optimize_concealed():
    # Redistribute budget from hidden washers/knuckles, retaining circular contours.
    saved = 0
    seen = set()
    for obj in list(SCENE.objects):
        if obj.type != "MESH" or not obj.get("equipment_mesh"):
            continue
        if obj.name.startswith(("CAB_08_ControlPanel", "CAB_15_OperatorHMI")):
            continue
        if any(k in obj.name for k in ("_DIN_EndClamp", "_MountingEar")):
            continue
        if not any(k in obj.name for k in ("_Washer", "_HexNut", "HingeKnuckle",
                                           "RingCrimp", "MountingSlot", "_Head",
                                           "_DIN", "_MountingEar", "FieldCableClamp",
                                           "CableClampingRail", "ClampBracket")):
            continue
        old = obj.data
        if old.as_pointer() in seen:
            continue
        seen.add(old.as_pointer())
        old.calc_loop_triangles()
        before = len(old.loop_triangles)
        if before < 28:
            continue
        obj.data = old.copy()
        activate(obj)
        mod = obj.modifiers.new("Concealed fastener budget", "DECIMATE")
        mod.ratio = .42 if "_Head" in obj.name else .32
        bpy.ops.object.modifier_apply(modifier=mod.name)
        new = obj.data
        bm = bmesh.new()
        bm.from_mesh(new)
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(new)
        bm.free()
        audit = bmesh.new()
        audit.from_mesh(new)
        invalid = any(not edge.is_manifold for edge in audit.edges)
        audit.free()
        if invalid:
            obj.data = old
            bpy.data.meshes.remove(new)
            continue
        shared.physical_uv(new)
        shared.replace_data(old, new)
        new.calc_loop_triangles()
        saved += before-len(new.loop_triangles)
    REPORT["operations"]["concealed_triangles_reallocated"] = saved


def cameras():
    scene = bpy.data.scenes["04_INTERNAL"]
    for name, pos, target, lens in (
        ("Housing_Drives", (.37, -1.40, 1.52), (-.246, .05, 1.34), 65),
        ("Housing_Terminations", (.59, -1.35, .90), (-.025, .10, .681), 56),
    ):
        data = bpy.data.cameras.new(name)
        data.lens, data.clip_start = lens, .02
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = pos
        obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()


def render(states, draft):
    render_device()
    suffixes = {"02_DOOR_OPEN": "door-open", "06_FRONT": "operator",
                "DRIVES": "drives", "TERMINATIONS": "terminations"}
    for state in states:
        scene = bpy.data.scenes["04_INTERNAL" if state in ("DRIVES", "TERMINATIONS") else state]
        bpy.context.window.scene = scene
        original = scene.camera
        if state in ("DRIVES", "TERMINATIONS"):
            scene.camera = scene.objects["Housing_"+("Drives" if state == "DRIVES" else "Terminations")]
        scene.render.resolution_x, scene.render.resolution_y = 2400, 1600
        scene.render.resolution_percentage = 50 if draft else 100
        scene.cycles.samples = 24 if draft else 72
        scene.cycles.use_denoising = True
        scene.render.filepath = str(MODEL / "previews/electrical-cabinet" /
                                   ("housing-review-"+("draft-" if draft else "")+suffixes[state]+".png"))
        bpy.ops.render.render(write_still=True)
        scene.camera = original
        print("HOUSING_RENDERED", scene.render.filepath, flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--render-only", action="store_true")
    parser.add_argument("--draft", action="store_true")
    parser.add_argument("--states", nargs="*", default=[])
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if not args.render_only:
        if Path(bpy.data.filepath).name != "electrical-cabinet-workmanship-review.blend":
            raise RuntimeError("Start from the preserved workmanship source")
        bpy.context.window.scene = SCENE
        SCENE.frame_set(1)
        materials()
        optimize_concealed()
        drives()
        contactors()
        terminals()
        safety()
        plc_details()
        cameras()
        bpy.context.view_layer.update()
        total = shared.measure()
        REPORT.update({"input": bpy.data.filepath, "output": str(OUTPUT),
                       "triangles": total, "triangles_by_group": shared.STATS["triangles_by_group"],
                       "status": "Awaiting visual review. No Unity integration.",
                       "design_scope": "Original generic training geometry, not a manufacturer CAD replica."})
        path = MODEL / "reports/electrical-cabinet-housing-review.json"
        path.write_text(json.dumps(REPORT, indent=2), encoding="utf-8")
        print("HOUSING_GEOMETRY", total, REPORT["operations"], flush=True)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.context.window.scene = SCENE
        bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT), check_existing=False)
        if total > 110000:
            raise RuntimeError("Over budget; revise concealed geometry before final delivery.")
    if args.states:
        render(args.states, args.draft)


if __name__ == "__main__":
    main()
