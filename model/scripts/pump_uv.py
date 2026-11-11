"""Unwrap and measure source UVs; measurements come from mesh loop triangles."""
import math
from collections import defaultdict

import bpy
import bmesh
from mathutils import Vector


def triangle_metrics(data):
    uv = data.uv_layers.active
    if not uv:
        return [], len(data.polygons)
    data.calc_loop_triangles()
    result, tiny = [], 0
    for tri in data.loop_triangles:
        p0, p1, p2 = [data.vertices[v].co for v in tri.vertices]
        edge = p1-p0
        length = edge.length
        area = edge.cross(p2-p0).length
        if length < 1e-10 or area < 1e-14:
            tiny += 1
            continue
        direction = edge/length
        projection = (p2-p0).dot(direction)
        height = area/length
        u0, u1, u2 = [uv.data[v].uv.copy() for v in tri.loops]
        a = (u1-u0)/length
        b = ((u2-u0)-a*projection)/height
        xx, yy, xy = a.length_squared, b.length_squared, a.dot(b)
        delta = math.sqrt(max(0, (xx-yy)**2+4*xy*xy))
        high = (xx+yy+delta)/2
        low = (xx+yy-delta)/2
        stretch = math.sqrt(high/low) if low > 1e-20 else float("inf")
        result.append((tri.polygon_index, stretch, abs(a.cross(b))))
    return result, tiny


def planar_face(data, face):
    uv = data.uv_layers.active
    normal = face.normal
    axis = max(range(3), key=lambda i: abs(normal[i]))
    reference = Vector((0, 1, 0) if axis == 0 else (1, 0, 0))
    u = (reference-normal*reference.dot(normal)).normalized()
    v = normal.cross(u).normalized()
    for loop in face.loop_indices:
        point = data.vertices[data.loops[loop].vertex_index].co
        uv.data[loop].uv = (point.dot(u), point.dot(v))


def unwrap(objects):
    repaired, tiny_total, failures = 0, 0, []
    maxima = []
    for obj in objects:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.012,
                                 correct_aspect=True, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        metrics, tiny = triangle_metrics(obj.data)
        distorted = {p for p, s, _ in metrics if s > 1.24}
        if distorted:
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            bm.faces.ensure_lookup_table()
            bmesh.ops.triangulate(bm, faces=[bm.faces[p] for p in distorted])
            bm.to_mesh(obj.data)
            bm.free()
            obj.data.update()
            for face in obj.data.polygons:
                planar_face(obj.data, face)
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.uv.select_all(action="SELECT")
            bpy.ops.uv.pack_islands(rotate=False, margin=.003)
            bpy.ops.object.mode_set(mode="OBJECT")
        metrics, tiny = triangle_metrics(obj.data)
        density = sorted(d for _, _, d in metrics if d > 1e-20)
        median = density[len(density)//2] if density else 0
        bad = {p for p, s, d in metrics if s > 1.24 or
               (median > 0 and (d/median > 1.24 or d/median < 1/1.24))}
        if bad:
            # Give all faces of this small hard-surface part the same physical
            # density before packing; this avoids mixing atlas and metre units.
            for face in obj.data.polygons:
                planar_face(obj.data, face)
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.uv.select_all(action="SELECT")
            bpy.ops.uv.pack_islands(rotate=False, margin=.003)
            bpy.ops.object.mode_set(mode="OBJECT")
            repaired += 1
        metrics, tiny = triangle_metrics(obj.data)
        tiny_total += tiny
        stretch = max((s for _, s, _ in metrics), default=float("inf"))
        density = sorted(d for _, _, d in metrics if d > 1e-20)
        median = density[len(density)//2] if density else 0
        area_ratio = max((max(d/median, median/d) for _, _, d in metrics
                          if median > 0 and d > 1e-20), default=float("inf"))
        maxima.append((stretch, area_ratio))
        if stretch > 1.25 or area_ratio > 1.25:
            failures.append({"object": obj.name, "max_anisotropy": stretch,
                             "max_area_ratio_to_median": area_ratio})
    return {
        "method": "Smart UV 66deg; planar per-face islands for distorted parts; uniform packing",
        "objects_checked": len(objects), "planar_fallback_objects": repaired,
        "sub_1e_14_m2_triangles_excluded": tiny_total,
        "max_anisotropy": max(s for s, _ in maxima),
        "max_area_ratio_to_object_median": max(a for _, a in maxima),
        "failures": failures, "pass": not failures,
    }
