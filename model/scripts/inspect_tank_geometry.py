"""Print geometry hotspots and equipment extents from the saved silo scene."""
import bpy
from mathutils import Vector

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
equipment = [obj for obj in scene.objects
             if obj.type == "MESH" and obj.name.startswith("TNK_")]
depsgraph = bpy.context.evaluated_depsgraph_get()
triangles = 0
points = []
profile = []
for obj in equipment:
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    count = len(mesh.loop_triangles)
    triangles += count
    bounds = [evaluated.matrix_world @ Vector(corner) for corner in obj.bound_box]
    points.extend(bounds)
    profile.append((count, obj.name))
    evaluated.to_mesh_clear()
minimum = [min(point[i] for point in points) for i in range(3)]
maximum = [max(point[i] for point in points) for i in range(3)]
print("TANK_GEOMETRY", {
    "triangles": triangles,
    "dimensions": [maximum[i] - minimum[i] for i in range(3)],
    "minimum": minimum,
    "maximum": maximum,
    "largest_meshes": sorted(profile, reverse=True)[:30],
})
