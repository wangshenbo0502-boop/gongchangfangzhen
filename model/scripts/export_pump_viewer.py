"""Export the reviewed assembled pump and its material values for the isolated viewer."""
import json
import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/"scripts"))
from pump_components import manifest
ASSETS = ROOT / "PumpViewerProject" / "Assets" / "Pump"
ASSETS.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
bpy.ops.object.select_all(action="DESELECT")
objects = [o for o in scene.objects
           if o.name == "water-pump" or o.name.startswith("PMP_") and
           (o.type == "EMPTY" or o.get("equipment_mesh")) or
           o.get("equipment_mesh")]
for obj in objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects["water-pump"]
bpy.context.view_layer.update()
groups = sorted(bpy.data.objects["water-pump"].children, key=lambda o: o.name)
(ASSETS / "parts.json").write_text(
    json.dumps(manifest(groups, [o for o in objects if o.get("equipment_mesh")]),
               indent=2, ensure_ascii=False), encoding="utf-8")

materials = []
used = {m for obj in objects if obj.type == "MESH" for m in obj.data.materials if m}
for mat in sorted(used, key=lambda m: m.name):
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    images = [n.image.name.lower() for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image]
    family = "cast" if any("cast_" in name for name in images) else (
        "machined" if any("machined_" in name for name in images) else (
            "rubber" if any("rubber_" in name for name in images) else ""))
    rough = bsdf.inputs["Roughness"].default_value
    # Linked inputs retain their physical finish's central value in this source.
    color = list(mat.diffuse_color)
    materials.append(dict(name=mat.name, color=color,
                          metallic=bsdf.inputs["Metallic"].default_value,
                          roughness=rough, family=family,
                          relief=mat.get("microrelief_m", .00001)))
(ASSETS / "materials.json").write_text(
    json.dumps({"materials": materials}, indent=2), encoding="utf-8")
bpy.ops.export_scene.fbx(
    filepath=str(ASSETS / "water-pump.fbx"), use_selection=True,
    object_types={"EMPTY", "MESH"}, global_scale=1, apply_unit_scale=True,
    axis_forward="-Z", axis_up="Y", bake_space_transform=False,
    apply_scale_options="FBX_SCALE_UNITS",
    use_mesh_modifiers=True, mesh_smooth_type="OFF", use_tspace=False,
    add_leaf_bones=False, bake_anim=False, path_mode="AUTO",
    use_custom_props=False)
print("PUMP_VIEWER_EXPORTED", len([o for o in objects if o.type == "MESH"]),
      len(materials), flush=True)

# Repair the bundled-HDRI lookup in existing source scenes without rebuilding geometry.
env_path = (Path(bpy.utils.resource_path("LOCAL")) /
            "datafiles/studiolights/world/studio.exr")
if env_path.exists():
    image = bpy.data.images.load(str(env_path), check_existing=True)
    image.pack()
    for item in bpy.data.scenes:
        if not item.world or not item.world.use_nodes:
            continue
        tree = item.world.node_tree
        bg = next(n for n in tree.nodes if n.type == "BACKGROUND")
        env = next((n for n in tree.nodes if n.type == "TEX_ENVIRONMENT"), None)
        if not env:
            env = tree.nodes.new("ShaderNodeTexEnvironment")
        env.image = image
        tree.links.new(env.outputs["Color"], bg.inputs["Color"])
        bg.inputs["Strength"].default_value = .4
        item.world["lighting_source"] = "Blender bundled studio.exr / CC0 / Greg Zaal"
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath, check_existing=False)
