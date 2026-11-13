"""Optional session-local Blender panel for the axial fan source model."""
import sys
from pathlib import Path

import bpy
from bpy.props import FloatProperty, IntProperty
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from fan_components import COMPONENTS

SCENES = (
    "01_ASSEMBLED", "02_EXPLODED", "03_INTERNAL", "07_BLADE_DETAIL",
    "04_FAULT_INTACT", "05_FAULT_BULGED", "06_FAULT_OPENED",
)


def set_progress(self, context):
    if context.scene.name == "01_ASSEMBLED":
        context.scene.frame_set(1+round(self.fan_review_progress*149))


def frame_view(index):
    scene = bpy.context.scene
    camera = scene.camera
    if not bpy.context.screen or not camera:
        return
    targets = ((0, 0, .90, 4.4), (0, 0, .90, 6.7), (0, 0, .90, 4.1),
               (-.24, .20, 1.16, 2.4), (-.23, .44, 1.125, 1.4))
    x, y, z, distance = targets[min(index, 4)]
    for area in bpy.context.screen.areas:
        if area.type != "VIEW_3D":
            continue
        space = area.spaces.active
        space.show_region_ui = True
        space.overlay.show_relationship_lines = False
        space.overlay.show_floor = False
        space.overlay.show_axis_x = False
        space.overlay.show_axis_y = False
        space.overlay.show_extras = False
        space.shading.type = "MATERIAL"
        space.shading.use_scene_world = True
        space.shading.use_scene_lights = True
        space.region_3d.view_location = (x, y, z)
        space.region_3d.view_distance = distance
        space.region_3d.view_rotation = camera.rotation_euler.to_quaternion()


class FAN_OT_view(bpy.types.Operator):
    bl_idname = "fan.review_view"
    bl_label = "Fan source view"
    index: IntProperty(default=0)

    def execute(self, context):
        name = SCENES[self.index]
        if name not in bpy.data.scenes:
            return {"CANCELLED"}
        context.window.scene = bpy.data.scenes[name]
        if self.index == 0:
            context.scene.fan_review_progress = 0
        frame_view(self.index)
        return {"FINISHED"}


class FAN_OT_component(bpy.types.Operator):
    bl_idname = "fan.review_component"
    bl_label = "Select component"
    index: IntProperty(default=0)

    def execute(self, context):
        group = next((o for o in context.scene.objects
                      if o.name.startswith(f"FAN_{self.index:02}_") and o.type == "EMPTY"), None)
        if not group:
            return {"CANCELLED"}
        bpy.ops.object.select_all(action="DESELECT")
        for obj in group.children_recursive:
            if obj.type == "MESH" and not obj.hide_get():
                obj.select_set(True)
        context.view_layer.objects.active = group
        return {"FINISHED"}


class FAN_PT_review(bpy.types.Panel):
    bl_label = "Axial Fan / F-01"
    bl_idname = "FAN_PT_review"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Fan"

    def draw(self, context):
        layout = self.layout
        for start, entries in (
            (0, (("Assembly", "OBJECT_DATA"), ("Exploded", "OUTLINER_OB_EMPTY"))),
            (2, (("Internals", "MESH_DATA"), ("Airfoils", "MOD_SMOOTH"))),
        ):
            row = layout.row(align=True)
            for i, (text, icon) in enumerate(entries):
                row.operator("fan.review_view", text=text, icon=icon,
                             depress=context.scene.name == SCENES[start+i]).index = start+i
        if context.scene.name == "01_ASSEMBLED":
            layout.prop(context.scene, "fan_review_progress", text="Disassembly", slider=True)
            layout.operator("screen.animation_play", text="Play / pause", icon="PLAY")
        layout.separator()
        for index, (_, text) in enumerate(COMPONENTS):
            layout.operator("fan.review_component", text=f"{index:02}  {text}",
                            icon="RESTRICT_SELECT_OFF").index = index
        layout.separator()
        layout.label(text="F-CRACK / blade deformation")
        row = layout.row(align=True)
        for index, text in enumerate(("Intact", "Bent", "Opened")):
            row.operator("fan.review_view", text=text,
                         depress=context.scene.name == SCENES[4+index]).index = 4+index


def reveal():
    if bpy.context.screen:
        for area in bpy.context.screen.areas:
            if area.type == "VIEW_3D":
                for region in area.regions:
                    if region.type == "UI":
                        region.active_panel_category = "Fan"
                area.tag_redraw()
    return None


def register():
    for cls in (FAN_OT_view, FAN_OT_component, FAN_PT_review):
        previous = getattr(bpy.types, cls.__name__, None)
        if previous:
            bpy.utils.unregister_class(previous)
        bpy.utils.register_class(cls)
    bpy.types.Scene.fan_review_progress = FloatProperty(
        name="Disassembly", min=0, max=1, default=0, update=set_progress)
    bpy.context.window.scene = bpy.data.scenes["01_ASSEMBLED"]
    frame_view(0)
    if not bpy.app.background:
        bpy.app.timers.register(reveal, first_interval=1)


register()
