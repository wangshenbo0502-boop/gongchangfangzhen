"""Capture the source studio's softboxes for the real-time reflection environment."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from pump_surface_finish import render_device

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
for obj in scene.objects:
    if obj.get("equipment_mesh"):
        obj.hide_render = True

lights = [o for o in scene.objects if o.type == "LIGHT" and o.data.type == "AREA"]
for light in lights:
    width, height = light.data.size, light.data.size_y
    bpy.ops.mesh.primitive_plane_add(size=1, location=light.location)
    panel = bpy.context.object
    panel.name = "Reflection_" + light.name
    panel.rotation_euler = light.rotation_euler
    panel.scale = (width, height, 1)
    mat = bpy.data.materials.new(panel.name)
    mat.use_nodes = True
    mat.node_tree.nodes.clear()
    output = mat.node_tree.nodes.new("ShaderNodeOutputMaterial")
    emission = mat.node_tree.nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (*light.data.color, 1)
    emission.inputs["Strength"].default_value = light.data.energy/(width*height*math.pi)
    mat.node_tree.links.new(emission.outputs[0], output.inputs["Surface"])
    panel.data.materials.append(mat)
    # The existing area light illuminates the floor; its visible panel is
    # camera-only so it does not count a second time in the floor illumination.
    panel.visible_diffuse = False
    panel.visible_glossy = False
    panel.visible_shadow = False

camera = bpy.data.objects.new("Reflection capture", bpy.data.cameras.new("Reflection capture"))
scene.collection.objects.link(camera)
camera.location = (0, 0, .66)
camera.rotation_euler = Vector((0, -1, 0)).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "PANO"
camera.data.panorama_type = "EQUIRECTANGULAR"
scene.camera = camera
scene.render.resolution_x, scene.render.resolution_y = 2048, 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "OPEN_EXR"
scene.render.image_settings.color_mode = "RGB"
scene.render.image_settings.color_depth = "16"
scene.cycles.samples = 32
scene.cycles.use_denoising = True
destination = HERE.parent/"PumpViewerProject/Assets/Textures/inspection-studio.exr"
scene.render.filepath = str(destination)
render_device()
bpy.ops.render.render(write_still=True)
(HERE.parent/"reports/pump-studio-reflection.json").write_text(json.dumps({
    "source": bpy.data.filepath,
    "output": str(destination),
    "resolution": [2048, 1024],
    "softboxes": len(lights),
    "camera_blender_xyz_m": list(camera.location),
    "linear_hdr": True,
    "method": "Source area lights as camera-visible emissive panels; equipment hidden",
}, indent=2), encoding="utf-8")
print("PUMP_STUDIO_CAPTURED", destination, flush=True)
