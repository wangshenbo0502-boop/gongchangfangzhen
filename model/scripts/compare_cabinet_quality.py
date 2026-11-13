"""Read-only source comparison under the same studio and framing settings."""
import json
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from industrial_parts import box, material
from pump_surface_finish import studio, render_device
from build_cabinet import light_studio, M

MODEL = HERE.parent
OUT = MODEL/"previews"/"electrical-cabinet"
cabinet_closed = bpy.data.scenes["01_ASSEMBLED"]
cabinet_open = bpy.data.scenes["02_DOOR_OPEN"]
cabinet_closed.frame_set(1)
with bpy.data.libraries.load(str(MODEL/"source"/"water-pump.blend"), link=False) as (src, dst):
    dst.scenes = ["01_ASSEMBLED"]
pump_scene = dst.scenes[0]
bpy.context.window.scene = pump_scene
pump_scene.frame_set(1)

cart_scene = bpy.data.scenes.new("REFERENCE_CART_IMPORT")
bpy.context.window.scene = cart_scene
factory = MODEL.parent/"Assets"/"WorkshopDemo"/"External"/"FactoryTraining"
bpy.ops.import_scene.fbx(filepath=str(factory/"Fbx"/"moto_bot.fbx"))
cart_meshes = [o for o in cart_scene.objects if o.type == "MESH"]
cart_materials = {mat for obj in cart_meshes for mat in obj.data.materials if mat}
texture_count = 0
for mat in cart_materials:
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (1, 1, 1, 1)
    for suffix, socket in (("Base_color", "Base Color"), ("Roughness", "Roughness"),
                           ("Metallic", "Metallic"), ("Normal", None)):
        path = factory/"Textures"/"moto"/f"{mat.name}_{suffix}.png"
        if not path.exists():
            continue
        node = nodes.new("ShaderNodeTexImage")
        node.image = bpy.data.images.load(str(path), check_existing=True)
        if suffix != "Base_color":
            node.image.colorspace_settings.name = "Non-Color"
        if socket:
            links.new(node.outputs["Color"], bsdf.inputs[socket])
        else:
            normal = nodes.new("ShaderNodeNormalMap")
            links.new(node.outputs["Color"], normal.inputs["Color"])
            links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])
        texture_count += 1

manifest = {"method": "Identical neutral Cycles studio, camera, exposure and pixel size. "
            "Each asset uniformly fitted to a 2 m maximum dimension for detail comparison; "
            "this is not a real-size layout. Cart original PBR maps reconnected in Blender, "
            "not a reproduction of Unity-specific shaders.", "cart_maps_connected": texture_count,
            "assets": []}
floor_mat = material("COMPARISON / floor", (.22, .23, .24), 0, .76)
M["floor"] = floor_mat
for key, source, selected in (
    ("quality-cabinet", cabinet_closed, None),
    ("quality-cabinet-open", cabinet_open, None),
    ("quality-pump", pump_scene, None),
    ("quality-cart", cart_scene, cart_meshes),
):
    bpy.context.window.scene = source
    source.frame_set(1)
    objects = selected if selected is not None else [o for o in source.objects if o.get("equipment_mesh")]
    points = [o.matrix_world@v.co for o in objects for v in o.data.vertices]
    lo = Vector([min(p[i] for p in points) for i in range(3)])
    hi = Vector([max(p[i] for p in points) for i in range(3)])
    size = hi-lo
    factor = 2/max(size)
    center = Vector(((hi.x+lo.x)/2, (hi.y+lo.y)/2, lo.z))
    fit = Matrix.Scale(factor, 4)@Matrix.Translation(-center)
    matrices = {o: o.matrix_world.copy() for o in objects}
    scene = bpy.data.scenes.new(key)
    bpy.context.window.scene = scene
    for original in objects:
        obj = original.copy()
        obj.animation_data_clear()
        obj.parent = None
        scene.collection.objects.link(obj)
        obj.matrix_world = fit@matrices[original]
    light_studio(scene)
    scene.render.resolution_x = scene.render.resolution_y = 1200
    scene.cycles.samples = 40
    data = bpy.data.cameras.new(key)
    cam = bpy.data.objects.new(key, data)
    scene.collection.objects.link(cam)
    cam.location = (3.4, -5.4, 2.7)
    cam.rotation_euler = (Vector((0, 0, .96))-cam.location).to_track_quat("-Z", "Y").to_euler()
    data.type, data.ortho_scale = "ORTHO", 3.15
    scene.camera = cam
    render_device()
    scene.render.filepath = str(OUT/f"{key}.png")
    bpy.ops.render.render(write_still=True)
    triangles = 0
    for obj in objects:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    manifest["assets"].append({"name": key, "meshes": len(objects), "triangles": triangles,
                               "source_dimensions_m": list(size), "display_scale": factor})
(MODEL/"reports"/"electrical-cabinet-quality-comparison.json").write_text(
    json.dumps(manifest, indent=2), encoding="utf-8")
