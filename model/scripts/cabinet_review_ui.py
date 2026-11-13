"""Optional Blender review panel; source scenes and animation work without it."""
import sys
from pathlib import Path

import bpy
from bpy.props import FloatProperty, IntProperty
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from cabinet_components import GROUP_NAMES

SCENES = ("01_ASSEMBLED", "02_DOOR_OPEN", "03_EXPLODED",
          "04_INTERNAL", "05_ELECTRICAL_FAULT", "06_FRONT")
LABELS = ("Assembly", "Door open", "Exploded", "Internal", "Fault", "Front")
PARTS = ("Enclosure frame", "Mounting backplate", "Field terminals / PE", "Door / lock",
         "Incoming / protection", "PLC / remote IO", "Filter cooling", "Ducts / wiring",
         "Safety operator strip", "24V power / UPS", "Plinth / cable glands",
         "Motor contactors", "Variable speed drives", "Industrial Ethernet",
         "Safety relay / interfaces", "Touch HMI / operator panel")


def progress_changed(self, context):
    if self.name == SCENES[0]:
        self.frame_set(1+round(self.cabinet_review_progress*120))


def frame_view(index):
    for area in bpy.context.screen.areas if bpy.context.screen else []:
        if area.type != "VIEW_3D":
            continue
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
        space.region_3d.view_location = (0, -.15, 1.03)
        space.region_3d.view_rotation = Vector((2.0, -6, 2.0)).to_track_quat("Z", "Y")
        space.region_3d.view_distance = (3.7, 4.0, 5.0, 2.5, 3.7, 3.7)[index]


class CABINET_OT_state(bpy.types.Operator):
    bl_idname = "cabinet.review_state"
    bl_label = "Cabinet state"
    index: IntProperty(default=0, min=0, max=5)

    def execute(self, context):
        context.window.scene = bpy.data.scenes[SCENES[self.index]]
        if self.index == 0:
            context.scene.cabinet_review_progress = 0
            context.scene.frame_set(1)
        frame_view(self.index)
        return {"FINISHED"}


class CABINET_OT_part(bpy.types.Operator):
    bl_idname = "cabinet.review_part"
    bl_label = "Select assembly"
    index: IntProperty(default=0, min=0, max=len(GROUP_NAMES)-1)

    def execute(self, context):
        group = next((o for o in context.scene.objects
                      if o.type == "EMPTY" and o.name.startswith(GROUP_NAMES[self.index])), None)
        if group is None:
            return {"CANCELLED"}
        bpy.ops.object.select_all(action="DESELECT")
        for obj in group.children_recursive:
            if obj.type == "MESH":
                obj.select_set(True)
        context.view_layer.objects.active = group
        return {"FINISHED"}


class CABINET_PT_review(bpy.types.Panel):
    bl_label = "Electrical Cabinet"
    bl_idname = "CABINET_PT_review"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Cabinet"

    def draw(self, context):
        layout = self.layout
        for i, text in enumerate(LABELS):
            layout.operator("cabinet.review_state", text=text,
                            icon=("OBJECT_DATA", "MOD_SIMPLEDEFORM", "OUTLINER_OB_EMPTY",
                                  "VIEWZOOM", "ERROR", "VIEW_ORTHO")[i],
                            depress=context.scene.name == SCENES[i]).index = i
        if context.scene.name == SCENES[0]:
            layout.prop(context.scene, "cabinet_review_progress", text="Disassemble", slider=True)
            layout.prop(context.scene, "frame_current", text="Frame")
            layout.operator("screen.animation_play", text="Play / pause", icon="PLAY")
        layout.separator()
        for i, text in enumerate(PARTS):
            layout.operator("cabinet.review_part", text=f"{i:02}  {text}",
                            icon="RESTRICT_SELECT_OFF").index = i


def reveal():
    for area in bpy.context.screen.areas if bpy.context.screen else []:
        if area.type == "VIEW_3D":
            for region in area.regions:
                if region.type == "UI":
                    region.active_panel_category = "Cabinet"
            area.tag_redraw()
    return None


def register():
    for cls in (CABINET_OT_state, CABINET_OT_part, CABINET_PT_review):
        existing = getattr(bpy.types, cls.__name__, None)
        if existing:
            bpy.utils.unregister_class(existing)
        bpy.utils.register_class(cls)
    bpy.types.Scene.cabinet_review_progress = FloatProperty(
        name="Disassemble", min=0, max=1, default=0, update=progress_changed)
    bpy.ops.cabinet.review_state(index=0)
    if not bpy.app.background:
        bpy.app.timers.register(reveal, first_interval=1)


register()
