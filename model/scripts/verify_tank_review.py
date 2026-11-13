"""Reopen the tank and measure geometry, instrument passages and demo readings."""
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector

model = Path(__file__).resolve().parent.parent
scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
bpy.context.view_layer.update()
graph = bpy.context.evaluated_depsgraph_get()
equipment = [o for o in scene.objects if o.type == "MESH" and o.name.startswith("TNK_")]
groups = [o for o in scene.objects if o.type == "EMPTY" and o.name.startswith("TNK_")]
minimum, maximum = [float("inf")]*3, [float("-inf")]*3
triangles = 0
unowned, no_uv, no_material = [], [], []
for obj in equipment:
    evaluated = obj.evaluated_get(graph)
    data = evaluated.to_mesh()
    data.calc_loop_triangles()
    triangles += len(data.loop_triangles)
    for vertex in data.vertices:
        point = evaluated.matrix_world @ vertex.co
        for i in range(3):
            minimum[i] = min(minimum[i], point[i])
            maximum[i] = max(maximum[i], point[i])
    evaluated.to_mesh_clear()
    if obj.parent not in groups:
        unowned.append(obj.name)
    if not obj.data.uv_layers:
        no_uv.append(obj.name)
    if not obj.data.materials:
        no_material.append(obj.name)


def ray_hit(obj, position, direction, distance):
    inverse = obj.matrix_world.inverted()
    return obj.ray_cast(inverse @ Vector(position),
                        inverse.to_3x3() @ Vector(direction),
                        distance=distance)[0]


shell = bpy.data.objects["TNK_01_Shell_8mmWall"]
cover = bpy.data.objects["TNK_10_ManwayCover_CoverPlate"]
f = Vector(bpy.data.objects["TNK_04_Manway"]["mating_flange_center"])
shell_y = .30-(1.5**2-f.x**2)**.5
bore_clear = [
    not ray_hit(shell, (f.x+dx, shell_y-.30, f.z+dz), (0, 1, 0), .65)
    for dx, dz in ((0, 0), (.10, 0), (-.10, 0), (0, .10), (0, -.10))
]
solid_shell_exists = ray_hit(shell, (f.x, shell_y-.30, f.z+.40), (0, 1, 0), .65)
cover_solid = ray_hit(cover, (f.x, f.y-.15, f.z), (0, 1, 0), .25)
studs = [o for o in equipment
         if o.name.startswith("TNK_10_ManwayCover_CoverStud_") and o.name.endswith("_Stud")]
holes_clear = [
    not ray_hit(cover, (o.location.x, f.y-.15, o.location.z), (0, 1, 0), .25)
    for o in studs
]
flange = bpy.data.objects["TNK_04_Manway_NeckFlange_Flange"]
neck_holes_clear = [
    not ray_hit(flange, (o.location.x, f.y-.15, o.location.z), (0, 1, 0), .25)
    for o in studs
]
dimensions = [maximum[i]-minimum[i] for i in range(3)]
images = [image for image in bpy.data.images if image.source == "FILE"]
roof = bpy.data.objects["TNK_02_TopHead_2to1Ellipsoidal"]
vent = bpy.data.objects["TNK_11_TopAccessories_VentNeck"]
vent_points = [vent.matrix_world @ Vector(p) for p in vent.bound_box]
vent_x = sum(p.x for p in vent_points)/8
vent_y = sum(p.y for p in vent_points)/8
vent_z = max(p.z for p in vent_points)+.10
vent_bore_clear = not ray_hit(roof, (vent_x, vent_y, vent_z), (0, 0, -1), .50)
roof_at_vent_exists = ray_hit(
    roof, (vent_x+.10, vent_y, vent_z), (0, 0, -1), .50)
uv_report = json.loads((model / "reports" / "silo-uv.json").read_text(encoding="utf-8"))
prefix = "TNK_05_Instruments_"
thermometer_prefix = prefix+"TGI01_"
column = bpy.data.objects[thermometer_prefix+"SpiritColumn"]
column_top = max((column.matrix_world @ v.co).z for v in column.data.vertices)


def mesh_center_z(obj):
    heights = [(obj.matrix_world @ v.co).z for v in obj.data.vertices]
    return (max(heights)+min(heights))/2


scale_zero_z = mesh_center_z(bpy.data.objects[thermometer_prefix+"Tick_00"])
scale_full_z = mesh_center_z(bpy.data.objects[thermometer_prefix+"Tick_60"])
reading_c = (column_top-scale_zero_z)/(scale_full_z-scale_zero_z)*60
water = bpy.data.objects[prefix+"LevelLiquid"]
water_top = max((water.matrix_world @ v.co).z for v in water.data.vertices)
level_zero_z = mesh_center_z(bpy.data.objects[prefix+"LevelScaleTick_000"])
liquid_level_m = water_top-level_zero_z


def material_values(obj):
    material = obj.data.materials[0]
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    return {
        "name": material.name,
        "ior": bsdf.inputs["IOR"].default_value,
        "transmission": bsdf.inputs["Transmission Weight"].default_value,
        "roughness": bsdf.inputs["Roughness"].default_value,
    }


glass_values = material_values(bpy.data.objects[thermometer_prefix+"GlassCapillary"])
water_values = material_values(water)
level_glass = bpy.data.objects[prefix+"LevelGlassTube"]
level_glass_values = material_values(level_glass)
level_x = .91
level_wall_y = .30-math.sqrt(1.5**2-level_x**2)
level_tube_y = level_wall_y-.40
level_passages = []
for index, z in enumerate((1.98, 4.38)):
    path_names = [
        "LevelGlassTube", f"LevelTapPad_{index}", f"LevelTap_{index}",
        f"LevelRootValve_{index+1}", f"LevelValveToGauge_{index}",
        f"LevelUnion_{index}_0.115", f"LevelUnion_{index}_0.141",
    ]
    clear = {
        name: not ray_hit(bpy.data.objects[prefix+name],
                          (level_x, level_tube_y, z), (0, 1, 0), .55)
        for name in path_names
    }
    clear["shell"] = not ray_hit(shell, (level_x, level_tube_y, z),
                                 (0, 1, 0), .55)
    level_passages.append({"port_z_m": z, "clear": clear})


def contains_point(obj, point):
    local_point = obj.matrix_world.inverted() @ Vector(point)
    found, nearest, normal, _ = obj.closest_point_on_mesh(local_point)
    return found and (local_point-nearest).dot(normal) <= .000002


def shared_point(a, b, point):
    return contains_point(a, point) and contains_point(b, point)


def inside_closed_mesh(obj, point):
    # Ray parity also works outside the concave boundary of a curved nameplate.
    origin = obj.matrix_world.inverted() @ Vector(point)
    direction = Vector((1, .137, .219)).normalized()
    hits = 0
    for _ in range(100):
        found, position, _, _ = obj.ray_cast(origin, direction, distance=20)
        if not found:
            return hits % 2 == 1
        hits += 1
        origin = position+direction*.000001
    raise RuntimeError("Ray parity did not terminate: "+obj.name)


supports = []
for index, z in enumerate((2.50, 3.80)):
    suffix = "" if index == 0 else ".001"
    arm = bpy.data.objects[prefix+"LevelBracketArm"+suffix]
    bracket = bpy.data.objects[prefix+"LevelBracket"+suffix]
    shoe = bpy.data.objects[prefix+"LevelBracketShoe"+suffix]
    rail_arm = bpy.data.objects[prefix+"LevelScaleBracket"+suffix]
    rail = bpy.data.objects[prefix+"LevelScaleRail"]
    # Test inside both closed solids; a short ray from inside a long rail would
    # miss its distant exit face even when the support is correctly attached.
    supports.append({
        "height_m": z,
        "arm_to_clamp": shared_point(
            arm, bracket, (level_x, level_tube_y+.090, z)),
        "arm_to_wall_shoe": shared_point(
            arm, shoe, (level_x, level_wall_y-.0085, z)),
        "scale_to_bracket": shared_point(
            rail_arm, rail, (level_x+.166, level_tube_y+.039, z)),
    })
thermowell = bpy.data.objects[thermometer_prefix+"Thermowell"]
thermometer_x, thermometer_z = .14, 2.20
thermometer_wall_y = .30-math.sqrt(1.5**2-thermometer_x**2)
thermowell_bounds = [thermowell.matrix_world @ v.co for v in thermowell.data.vertices]
reservoir = bpy.data.objects[thermometer_prefix+"InternalReservoir"]
reservoir_points = [reservoir.matrix_world @ v.co for v in reservoir.data.vertices]
reservoir_inside = all(
    thermometer_wall_y < p.y < thermometer_wall_y+.28 and
    math.hypot(p.x-thermometer_x, p.z-thermometer_z) < .013
    for p in reservoir_points)


def world_dimensions(obj):
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    return [max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]


fitting_dimensions = {
    "display_xyz_m": world_dimensions(bpy.data.objects[prefix+"JunctionBox"]),
    "thermometer_case_xyz_m": world_dimensions(
        bpy.data.objects[thermometer_prefix+"CaseBack"]),
    "pressure_case_xyz_m": world_dimensions(
        bpy.data.objects[prefix+"PressureGaugeCase"]),
    "level_glass_xyz_m": world_dimensions(level_glass),
    "scale_rail_xyz_m": world_dimensions(bpy.data.objects[prefix+"LevelScaleRail"]),
}
level_switch = bpy.data.objects[prefix+"LevelSwitch"]
switch_mount = bpy.data.objects[prefix+"LevelSwitchMount"]
switch_right = max((level_switch.matrix_world @ v.co).x
                   for v in level_switch.data.vertices)
routes = json.loads(bpy.data.objects["TNK_05_Instruments"]["conduit_routes_json"])
tt_conduit = bpy.data.objects[prefix+"TT01_Conduit"]
tt_inverse = tt_conduit.matrix_world.inverted()
thermometer_case_parts = [bpy.data.objects[thermometer_prefix+name] for name in
                          ("CaseBack", "CaseTop", "CaseBottom",
                           "CaseLip_-1", "CaseLip_1")]
case_conduit_distances = []
for part in thermometer_case_parts:
    for vertex in part.data.vertices:
        point = part.matrix_world @ vertex.co
        found, nearest, _, _ = tt_conduit.closest_point_on_mesh(tt_inverse @ point)
        if found:
            case_conduit_distances.append(
                (point-tt_conduit.matrix_world @ nearest).length)
case_conduit_clearance = min(case_conduit_distances)
old_thermometer_wall_y = .30-math.sqrt(1.5**2-.35**2)
old_thermometer_opening_closed = ray_hit(
    shell, (.35, old_thermometer_wall_y-.08, 2.40), (0, 1, 0), .16)
new_thermometer_passage_clear = not ray_hit(
    shell, (thermometer_x, thermometer_wall_y-.08, thermometer_z),
    (0, 1, 0), .16)
route_checks = []
obstacles = [bpy.data.objects[name] for name in (
    "TNK_01_Shell_Nameplate", prefix+"JunctionBox", thermometer_prefix+"CaseBack",
    thermometer_prefix+"CaseLip_-1", thermometer_prefix+"CaseLip_1",
) if name in bpy.data.objects]
for route in routes:
    conduit = bpy.data.objects[prefix+route["name"]+"_Conduit"]
    end = Vector(route["centerline"][-1])
    gland = bpy.data.objects[prefix+route["name"]+"_Gland"]
    # The real mesh must reach the gland and fit through its opening.
    local_end = conduit.matrix_world.inverted() @ end
    found, nearest, _, _ = conduit.closest_point_on_mesh(local_end)
    end_distance = (nearest-local_end).length if found else float("inf")
    collisions = []
    for a, b in zip(route["centerline"], route["centerline"][1:]):
        for step in range(25):
            point = Vector(a).lerp(Vector(b), step/24)
            for obstacle in obstacles:
                local_point = obstacle.matrix_world.inverted() @ point
                hit, nearest, _, _ = obstacle.closest_point_on_mesh(local_point)
                if (inside_closed_mesh(obstacle, point) or
                        (hit and (local_point-nearest).length < .011)):
                    collisions.append(obstacle.name)
    route_checks.append({
        "name": route["name"], "end_mesh_distance_m": end_distance,
        "end_inside_gland_axial_span": (
            min((gland.matrix_world @ v.co).z for v in gland.data.vertices) <= end.z <=
            max((gland.matrix_world @ v.co).z for v in gland.data.vertices)),
        "obstacle_contacts": sorted(set(collisions)),
    })
checks = {
    "height_5_86m_within_3cm": abs(dimensions[2]-5.86) < .03,
    "twelve_groups_under_user_limit_16": len(groups) == 12,
    "all_meshes_owned_uv_material": not (unowned or no_uv or no_material),
    "manway_shell_bore_five_rays_clear": all(bore_clear),
    "adjacent_shell_still_present": solid_shell_exists,
    "cover_has_solid_center": cover_solid,
    "twelve_studs_match_real_cover_holes": len(studs) == 12 and all(holes_clear),
    "same_studs_match_neck_flange_holes": len(studs) == 12 and all(neck_holes_clear),
    "roof_vent_has_real_through_bore": vent_bore_clear,
    "roof_beside_vent_remains_solid": roof_at_vent_exists,
    "all_file_textures_packed": all(image.packed_file for image in images),
    "three_review_scenes_retained": all(name in bpy.data.scenes for name in
                                      ("01_ASSEMBLED", "02_EXPLODED", "03_WALL_DETAIL")),
    "thermometer_column_geometry_reads_24_8c": abs(reading_c-24.8) < .01,
    "thermometer_calibration_matches_engraved_scale": (
        abs(column["scale_zero_z"]-scale_zero_z) < .000001 and
        abs(column["scale_full_z"]-scale_full_z) < .000001),
    "thermometer_glass_transmits_and_refracts": (
        glass_values["transmission"] > .99 and abs(glass_values["ior"]-1.47) < .001),
    "thermowell_crosses_shell_wall": (
        min(p.y for p in thermowell_bounds) < thermometer_wall_y-.05 and
        max(p.y for p in thermowell_bounds) > thermometer_wall_y+.10),
    "thermometer_bulb_inside_thermowell": reservoir_inside,
    "relocated_thermometer_shell_passage_clear": new_thermometer_passage_clear,
    "previous_thermometer_shell_opening_removed": old_thermometer_opening_closed,
    "thermometer_case_vertex_samples_clear_tt_conduit_by_150mm": (
        case_conduit_clearance >= .150),
    "level_water_geometry_reads_1_24m": abs(liquid_level_m-1.24) < .001,
    "level_water_has_refractive_transmission": (
        abs(water_values["ior"]-1.333) < .001 and water_values["transmission"] > .99),
    "level_glass_is_transmissive": level_glass_values["transmission"] > .9,
    "both_level_ports_have_clear_passages": all(
        all(item["clear"].values()) for item in level_passages),
    "level_supports_contact_clamps_wall_shoes_and_scale": all(
        item["arm_to_clamp"] and item["arm_to_wall_shoe"] and item["scale_to_bracket"]
        for item in supports),
    "display_actual_mesh_650_by_490mm": (
        abs(fitting_dimensions["display_xyz_m"][0]-.650) < .001 and
        abs(fitting_dimensions["display_xyz_m"][2]-.490) < .001),
    "thermometer_case_actual_height_400mm": (
        abs(fitting_dimensions["thermometer_case_xyz_m"][2]-.400) < .001),
    "pressure_case_actual_diameter_260mm": (
        abs(fitting_dimensions["pressure_case_xyz_m"][0]-.260) < .001),
    "level_glass_actual_diameter_136mm": (
        abs(fitting_dimensions["level_glass_xyz_m"][0]-.136) < .001),
    "conduit_meshes_reach_underside_glands": all(
        item["end_mesh_distance_m"] <= .012 and item["end_inside_gland_axial_span"]
        for item in route_checks),
    "conduit_samples_clear_display_nameplate_and_thermometer_case": all(
        not item["obstacle_contacts"] for item in route_checks),
    "level_switch_outside_glass_with_connected_bracket": (
        level_x-switch_right > .068 and
        shared_point(level_switch, switch_mount,
                     (level_x-.110, level_tube_y+.055, 3.80)) and
        shared_point(switch_mount, bpy.data.objects[prefix+"LevelBracket.001"],
                     (level_x-.080, level_tube_y+.055, 3.80))),
}
result = {
    "blender": bpy.app.version_string, "source": str(bpy.data.filepath),
    "dimensions_m": dimensions, "minimum_m": minimum, "maximum_m": maximum,
    "mesh_count": len(equipment), "group_count": len(groups), "triangles": triangles,
    "checks": checks, "pass": all(checks.values()),
    "pass_scope": "Focused height, grouping, passages, selected support junctions, material settings and static reading geometry; not full specification acceptance or instrument calibration.",
    "instrument_measurements": {
        "thermometer_reading_from_vertices_c": reading_c,
        "level_reading_from_vertices_m": liquid_level_m,
        "thermometer_glass": glass_values, "level_water": water_values,
        "level_glass": level_glass_values,
        "level_passages": level_passages, "level_support_samples": supports,
        "enlarged_fitting_dimensions": fitting_dimensions,
        "conduit_end_and_obstacle_samples": route_checks,
        "thermometer_case_vertex_to_tt_conduit_minimum_m": case_conduit_clearance,
        "thermometer_mount_xz_m": [thermometer_x, thermometer_z],
    },
    "unowned": unowned, "missing_uv": no_uv, "missing_material": no_material,
    "file_images": len(images),
    "remaining": {
        "triangle_budget_90000_pass": triangles <= 90000,
        "legacy_front_projection_1_2m_pass": -minimum[1] <= 1.2,
        "legacy_installed_depth_3m_pass": abs(dimensions[1]-3) <= .09,
        "uv_distortion_pass": uv_report["pass"],
        "uv_distortion_failures": uv_report["failures"],
        "unity_export_integration": "Not executed; Blender review only.",
        "engineering_design_certification": "Not performed.",
    },
}
destination = model / "reports" / "silo-review-verification.json"
destination.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print("TANK_REOPEN_VERIFICATION", json.dumps(result, ensure_ascii=False), flush=True)
if not result["pass"]:
    raise RuntimeError("Tank review geometry checks failed; inspect the verification JSON")
