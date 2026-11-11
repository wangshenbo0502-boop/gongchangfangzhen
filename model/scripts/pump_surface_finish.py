"""Physical-scale surface finishes and neutral inspection lighting."""
import math
from pathlib import Path

import bpy
from mathutils import Vector


def node(tree, kind, label):
    item = tree.nodes.new(kind)
    item.label = label
    return item


def noise(tree, vector, scale, detail=2):
    item = node(tree, "ShaderNodeTexNoise", f"{scale:g} per metre")
    item.inputs["Scale"].default_value = scale
    item.inputs["Detail"].default_value = detail
    tree.links.new(vector, item.inputs["Vector"])
    return item.outputs["Fac"]


def remap(tree, source, low, high):
    item = node(tree, "ShaderNodeMapRange", "Surface variation")
    item.inputs["To Min"].default_value = low
    item.inputs["To Max"].default_value = high
    tree.links.new(source, item.inputs["Value"])
    return item.outputs["Result"]


def bump(tree, source, distance, strength, previous=None):
    item = node(tree, "ShaderNodeBump", f"{distance*1e6:g} micron relief")
    item.inputs["Distance"].default_value = distance
    item.inputs["Strength"].default_value = strength
    tree.links.new(source, item.inputs["Height"])
    if previous:
        tree.links.new(previous, item.inputs["Normal"])
    return item.outputs["Normal"]


def finish_material(mat, color, roughness, metallic=0, family=None,
                    relief=.000025, coat=0, turned=False, anisotropy=0):
    # Retain the packed, UV-based five-channel set; add metre-scale microrelief.
    images = {n.label: n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE"}
    tree = mat.node_tree
    tree.nodes.clear()
    output = node(tree, "ShaderNodeOutputMaterial", "Surface")
    bsdf = node(tree, "ShaderNodeBsdfPrincipled", "Industrial finish")
    output.location = (900, 100)
    bsdf.location = (600, 100)
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    mat.diffuse_color = (*color, 1)
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Coat Weight"].default_value = coat
    bsdf.inputs["Coat Roughness"].default_value = .32
    bsdf.inputs["Anisotropic"].default_value = anisotropy
    coord = node(tree, "ShaderNodeTexCoord", "Object coordinates in metres")
    coord.location = (-1100, 100)
    vector = coord.outputs["Object"]
    macro = noise(tree, vector, 18, 3)
    micro = noise(tree, vector, 1100 if family == "cast" else 2100, 2)
    rough_input = remap(tree, macro, roughness*.92, min(1, roughness*1.08))
    normal = None
    if images:
        textures = {}
        for channel, image in images.items():
            tex = node(tree, "ShaderNodeTexImage", channel)
            tex.image = image
            tex.location = (-900, -180*len(textures))
            tree.links.new(coord.outputs["UV"], tex.inputs["Vector"])
            textures[channel] = tex
        tint = node(tree, "ShaderNodeMixRGB", "Paint / metal reflectance")
        tint.blend_type = "MULTIPLY"
        tint.inputs[0].default_value = 1
        tint.inputs[2].default_value = (*color, 1)
        tree.links.new(textures["BaseColor"].outputs["Color"], tint.inputs[1])
        tree.links.new(tint.outputs[0], bsdf.inputs["Base Color"])
        variation = node(tree, "ShaderNodeMixRGB", "Broad finish variation / fine texture")
        variation.blend_type = "MIX"
        variation.inputs[0].default_value = .25
        tree.links.new(macro, variation.inputs[1])
        tree.links.new(textures["Roughness"].outputs["Color"], variation.inputs[2])
        rough_input = remap(tree, variation.outputs[0],
                            max(.05, roughness-.10), min(1, roughness+.10))
        # Fine image normal is supplemental; the grain size comes from metres.
        mapped = node(tree, "ShaderNodeNormalMap", "Subtle UV micro-normal")
        mapped.inputs["Strength"].default_value = .10
        tree.links.new(textures["Normal"].outputs["Color"], mapped.inputs["Color"])
        normal = mapped.outputs["Normal"]
    normal = bump(tree, micro, relief*.35, .30, normal)
    if family == "cast":
        normal = bump(tree, noise(tree, vector, 620, 3), relief, .48, normal)
        normal = bump(tree, noise(tree, vector, 220, 2), relief*.35, .25, normal)
    if turned:
        attr = node(tree, "ShaderNodeAttribute", "Centred machining coordinates / m")
        attr.attribute_name = "manufacturing_position"
        radial = node(tree, "ShaderNodeVectorMath", "Radial plane")
        radial.operation = "MULTIPLY"
        radial.inputs[1].default_value = (1, 1, 0)
        tree.links.new(attr.outputs["Vector"], radial.inputs[0])
        length = node(tree, "ShaderNodeVectorMath", "Turning radius")
        length.operation = "LENGTH"
        tree.links.new(radial.outputs["Vector"], length.inputs[0])
        frequency = node(tree, "ShaderNodeMath", "0.28 mm tool feed")
        frequency.operation = "MULTIPLY"
        frequency.inputs[1].default_value = math.tau/.00028
        tree.links.new(length.outputs["Value"], frequency.inputs[0])
        wave = node(tree, "ShaderNodeMath", "Concentric tool marks")
        wave.operation = "SINE"
        tree.links.new(frequency.outputs[0], wave.inputs[0])
        normal = bump(tree, wave.outputs[0], .000004, .16, normal)
        rough_input = remap(tree, macro, roughness-.065, roughness+.065)
    tree.links.new(normal, bsdf.inputs["Normal"])
    tree.links.new(rough_input, bsdf.inputs["Roughness"])
    mat["finish_revision"] = "2026-11-11 physical-scale v2"
    mat["microrelief_m"] = relief
    return mat


def assign_machining_coordinates(obj, axis=(1, 0, 0)):
    data = obj.data
    world = [obj.matrix_world @ vertex.co for vertex in data.vertices]
    center = Vector([(min(p[i] for p in world)+max(p[i] for p in world))/2
                     for i in range(3)])
    basis = Vector(axis).to_track_quat("Z", "Y").to_matrix().inverted()
    attr = data.attributes.get("manufacturing_position")
    if not attr:
        attr = data.attributes.new("manufacturing_position", "FLOAT_VECTOR", "POINT")
    for item, position in zip(attr.data, world):
        item.vector = basis @ (position-center)


def refine_surfaces(scene):
    mats = bpy.data.materials
    finish_material(mats["Paint / blue grey"], (.038, .069, .086), .51,
                    family="cast", relief=.000070, coat=.08)
    finish_material(mats["Paint / industrial grey"], (.14, .155, .16), .53,
                    relief=.000014, coat=.08)
    finish_material(mats["Sand cast / blue grey"], (.042, .076, .090), .69,
                    family="cast", relief=.000180, coat=.025)
    finish_material(mats["Machined steel"], (.42, .45, .48), .30, 1,
                    relief=.000007, turned=True, anisotropy=.24)
    finish_material(mats["Powder coat / graphite"], (.018, .021, .023), .54,
                    relief=.000025, coat=.06)
    finish_material(mats["EPDM rubber"], (.010, .011, .012), .9,
                    relief=.000020)
    finish_material(mats["Safety yellow enamel"], (.72, .37, .008), .47,
                    relief=.000010, coat=.14)
    finish_material(mats["Impeller / machined bronze"], (.43, .275, .095), .35, 1,
                    relief=.000012, turned=True, anisotropy=.18)
    finish_material(mats["Clean cement grout"], (.22, .225, .22), .88,
                    relief=.00018)
    finish_material(mats["Studio neutral"], (.19, .195, .20), .75,
                    relief=.00007)
    zinc = mats["Machined steel"].copy()
    zinc.name = "Fasteners / zinc plated"
    finish_material(zinc, (.53, .56, .59), .32, 1, relief=.000009)
    shaft = mats["Machined steel"].copy()
    shaft.name = "Shaft / ground steel"
    finish_material(shaft, (.62, .65, .68), .22, 1,
                    relief=.000003, turned=True, anisotropy=.32)
    plate = mats["Machined steel"].copy()
    plate.name = "Nameplate / satin aluminium"
    finish_material(plate, (.67, .69, .71), .38, 1, relief=.000004)
    cast_bronze = mats["Impeller / machined bronze"].copy()
    cast_bronze.name = "Impeller / cast bronze vanes"
    finish_material(cast_bronze, (.38, .235, .080), .43, 1,
                    family="cast", relief=.000060)
    for obj in scene.objects:
        if obj.type != "MESH" or not obj.data.materials:
            continue
        name = obj.name
        if "BackwardBlade" in name:
            obj.data.materials[0] = cast_bronze
        if obj.data.materials[0] == mats["Machined steel"]:
            if any(key in name for key in ("Bolt", "Anchor", "Screw", "Rivet",
                                           "HoldDown", "GlandBolt", "HexNut")):
                obj.data.materials[0] = zinc
            elif any(key in name for key in ("Shaft", "Race", "Ball", "rotor")):
                obj.data.materials[0] = shaft
            elif any(key in name for key in ("Nameplate", "DataPlate")):
                obj.data.materials[0] = plate
        if obj.data.materials[0].node_tree:
            axis = (0, 0, 1) if "Discharge" in name else (1, 0, 0)
            assign_machining_coordinates(obj, axis)
        obj.color = obj.data.materials[0].diffuse_color
    scene["finish_revision"] = "physical-scale-v2"


def studio(scene, floor_mat, make_box):
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.cycles.adaptive_threshold = .025
    scene.cycles.max_bounces = 8
    scene.cycles.transparent_max_bounces = 6
    scene.render.resolution_x = 2400
    scene.render.resolution_y = 1350
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new(scene.name+"_InspectionWorld")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes["Background"]
    env_path = (Path(bpy.utils.resource_path("LOCAL")) /
                "datafiles" / "studiolights" / "world" / "studio.exr")
    if env_path.exists():
        env = node(scene.world.node_tree, "ShaderNodeTexEnvironment",
                   "Blender bundled CC0 studio / Greg Zaal")
        env.image = bpy.data.images.load(str(env_path), check_existing=True)
        env.image.pack()
        scene.world.node_tree.links.new(env.outputs["Color"], bg.inputs["Color"])
        bg.inputs["Strength"].default_value = .40
        scene.world["lighting_source"] = "Blender bundled studio.exr / CC0 / Greg Zaal"
    else:
        bg.inputs["Color"].default_value = (.24, .25, .27, 1)
        bg.inputs["Strength"].default_value = .23
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -.25
    for name, pos, energy, width, height in (
        ("WindowKey", (-3.2, -3.4, 4.3), 550, 2.2, 1.5),
        ("CeilingStrip", (.6, 1.8, 3.3), 650, 3.2, .55),
        ("MetalEdge", (-2.7, .8, 2.1), 230, .55, 2.0),
        ("SoftFill", (2.7, -2.0, 2.4), 120, 2.0, 2.0),
    ):
        data = bpy.data.lights.new(scene.name+"_"+name, "AREA")
        data.energy = energy
        data.shape = "RECTANGLE"
        data.size, data.size_y = width, height
        obj = bpy.data.objects.new(data.name, data)
        scene.collection.objects.link(obj)
        obj.location = pos
        obj.rotation_euler = (Vector((0, 0, .65))-obj.location).to_track_quat("-Z", "Y").to_euler()
        obj.hide_select = True
    floor = make_box(scene.name+"_STUDIO_FLOOR", (0, 0, -.019),
                     (200, 200, .03), floor_mat, bevel=0)
    floor["preview_only"] = True
    floor.hide_select = True


def render_device():
    prefs = bpy.context.preferences.addons["cycles"].preferences
    try:
        prefs.compute_device_type = "CUDA"
        prefs.get_devices()
        usable = [d for d in prefs.devices if d.type == "CUDA"]
        for device in prefs.devices:
            device.use = device.type == "CUDA"
    except (RuntimeError, TypeError):
        usable = []
    for scene in bpy.data.scenes:
        scene.cycles.device = "GPU" if usable else "CPU"
    print("PUMP_RENDER_DEVICE", [d.name for d in usable] or ["CPU"], flush=True)
