"""Optional native Blender review panel; launch with --python after the blend."""
from pathlib import Path
import json
import sys

import bpy
from bpy.props import FloatProperty, IntProperty
from mathutils import Vector

MODEL = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(MODEL/"scripts"))
from pump_components import COMPONENTS
SCENES = ("01_ASSEMBLED", "02_EXPLODED", "03_INTERNAL")
OFFSETS = ((0, 0, .64, 3.1), (.12, 0, .74, 4.8), (-.77, .06, .73, 2.5))
PARTS = tuple(p[2] for p in COMPONENTS)
with (MODEL/"reports"/"water-pump-detailed.json").open(encoding="utf-8") as source:
    REPORT = json.load(source)


def frame_from_progress(self, context):
    if context.scene.name == SCENES[0]:
        context.scene.frame_set(1+round(self.pump_review_progress*149))


def frame_view(index):
    x, y, z, distance = OFFSETS[index]
    if bpy.context.screen is None:
        return
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            space = area.spaces.active
            space.show_region_ui = True
            space.overlay.show_relationship_lines = False
            space.overlay.show_extras = False
            space.overlay.show_floor = False
            space.overlay.show_axis_x = False
            space.overlay.show_axis_y = False
            space.shading.type = "MATERIAL"
            space.shading.use_scene_world = True
            space.shading.use_scene_lights = True
            space.region_3d.view_location = (x, y, z)
            space.region_3d.view_distance = distance
            space.region_3d.view_rotation = Vector((-3.5, -4.4, 2.6)).to_track_quat("Z", "Y")
        elif area.type == "OUTLINER":
            area.spaces.active.use_filter_object_empty = True


def reveal_panel():
    if bpy.context.screen:
        for area in bpy.context.screen.areas:
            if area.type == "VIEW_3D":
                for region in area.regions:
                    if region.type == "UI":
                        region.active_panel_category = "Pump"
                area.tag_redraw()
    return None


class PUMP_OT_state(bpy.types.Operator):
    bl_idname = "pump.review_state"
    bl_label = "Pump state"
    index: IntProperty(default=0)

    def execute(self, context):
        context.window.scene = bpy.data.scenes[SCENES[self.index]]
        if self.index == 0:
            context.scene.frame_set(1)
            context.scene.pump_review_progress = 0
        frame_view(self.index)
        return {"FINISHED"}


class PUMP_OT_part(bpy.types.Operator):
    bl_idname = "pump.review_part"
    bl_label = "Select component"
    index: IntProperty(default=0)

    def execute(self, context):
        groups = [o for o in context.scene.objects if o.type == "EMPTY"
                  and o.name.startswith(f"PMP_{self.index:02}_")]
        if not groups:
            return {"CANCELLED"}
        bpy.ops.object.select_all(action="DESELECT")
        group = groups[0]
        for obj in group.children_recursive:
            if obj.type == "MESH":
                obj.select_set(True)
        context.view_layer.objects.active = group
        return {"FINISHED"}


class PUMP_PT_review(bpy.types.Panel):
    bl_label = "Water Pump / Source Review"
    bl_idname = "PUMP_PT_review"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Pump"

    def draw(self, context):
        layout = self.layout
        row = layout.row(align=True)
        for i, (label, icon) in enumerate((("Assembly", "OBJECT_DATA"),
                                          ("Exploded", "OUTLINER_OB_EMPTY"),
                                          ("Internal", "MESH_DATA"))):
            op = row.operator("pump.review_state", text=label, icon=icon,
                              depress=context.scene.name == SCENES[i])
            op.index = i
        if context.scene.name == SCENES[0]:
            layout.prop(context.scene, "pump_review_progress", text="Explode", slider=True)
            layout.prop(context.scene, "frame_current", text="Frame")
            layout.operator("screen.animation_play", text="Play / pause", icon="PLAY")
        layout.separator()
        for i, label in enumerate(PARTS):
            layout.operator("pump.review_part", text=f"{i:02}  {label}",
                            icon="RESTRICT_SELECT_OFF").index = i
        layout.separator()
        layout.label(text="Measured source geometry")
        layout.label(text=" x ".join(f"{v:.3f}" for v in REPORT["width_depth_height_m"])+" m")
        layout.label(text=f"{REPORT['triangles_evaluated']:,} triangles")
        layout.label(text=f"{REPORT['mesh_count']} meshes / {len(PARTS)} components")
        layout.label(text="Shared Blender / Unity parts", icon="INFO")


def register():
    for cls in (PUMP_OT_state, PUMP_OT_part, PUMP_PT_review):
        if hasattr(bpy.types, cls.__name__):
            bpy.utils.unregister_class(getattr(bpy.types, cls.__name__))
        bpy.utils.register_class(cls)
    bpy.types.Scene.pump_review_progress = FloatProperty(
        name="Explode", min=0, max=1, default=0, update=frame_from_progress)
    bpy.context.window.scene = bpy.data.scenes[SCENES[0]]
    frame_view(0)
    if not bpy.app.background:
        bpy.app.timers.register(reveal_panel, first_interval=1)


register()
