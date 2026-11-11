"""Reusable native Blender hard-surface parts for workshop equipment."""
import math
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

TAU = math.tau


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_mod(obj, mod):
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def finish(obj, name, material, group=None, bevel=.001, smooth=True):
    obj.name = name
    if group:
        world = obj.matrix_world.copy()
        obj.parent = group
        obj.matrix_world = world
    if material:
        obj.data.materials.clear()
        obj.data.materials.append(material)
        obj.color = material.diffuse_color
    if obj.type == "MESH":
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
        for poly in obj.data.polygons:
            poly.use_smooth = smooth
        # Split machining edges; do not smooth the entire object indiscriminately.
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        for edge in bm.edges:
            if edge.is_manifold and edge.calc_face_angle() > math.radians(30):
                edge.smooth = False
        bm.to_mesh(obj.data)
        bm.free()
        if bevel:
            mod = obj.modifiers.new("Machined edge 1mm", "BEVEL")
            mod.width = bevel
            mod.segments = 2
            mod.limit_method = "ANGLE"
            apply_mod(obj, mod)
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bm.to_mesh(obj.data)
            bm.free()
    return obj


def mesh(name, vertices, faces, mat, group=None, bevel=.001):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    return finish(obj, name, mat, group, bevel)


def box(name, center, size, mat, group=None, bevel=.001):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.data.transform(Matrix.Diagonal((*size, 1)))
    return finish(obj, name, mat, group, bevel, False)


def axis_matrix(axis):
    return Vector(axis).to_track_quat("Z", "Y").to_matrix().to_4x4()


def cylinder(name, center, radius, length, mat, group=None,
             axis=(1, 0, 0), segments=48, bevel=.001):
    bpy.ops.mesh.primitive_cylinder_add(vertices=segments, radius=radius,
                                      depth=length, location=center)
    obj = bpy.context.object
    obj.data.transform(axis_matrix(axis))
    return finish(obj, name, mat, group, bevel)


def lathe(name, center, profile, mat, group=None, axis=(1, 0, 0),
          segments=48, bevel=.001):
    # Closed (axial, radial) cross section, revolved into a solid ring or shell.
    basis = axis_matrix(axis)
    center = Vector(center)
    vertices = []
    for a, r in profile:
        for i in range(segments):
            theta = TAU * i / segments
            vertices.append(center + basis @ Vector((r*math.cos(theta), r*math.sin(theta), a)))
    n = len(profile)
    faces = []
    for j in range(n):
        for i in range(segments):
            k = (i + 1) % segments
            faces.append((j*segments+i, j*segments+k,
                          ((j+1) % n)*segments+k, ((j+1) % n)*segments+i))
    return mesh(name, vertices, faces, mat, group, bevel)


def ring(name, center, outer, inner, length, mat, group=None,
         axis=(1, 0, 0), segments=48, bevel=.001):
    return lathe(name, center, [(-length/2, inner), (-length/2, outer),
                               (length/2, outer), (length/2, inner)],
                 mat, group, axis, segments, bevel)


def torus(name, center, major, minor, mat, group=None, axis=(1, 0, 0),
          segments=48, sides=8):
    bpy.ops.mesh.primitive_torus_add(major_segments=segments, minor_segments=sides,
                                    location=center, major_radius=major,
                                    minor_radius=minor)
    obj = bpy.context.object
    obj.data.transform(axis_matrix(axis))
    return finish(obj, name, mat, group, 0)


def pipe(name, points, radius, mat, group=None, sides=8):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = radius
    curve.bevel_resolution = 1
    curve.resolution_u = 2 if len(points) != 4 else 6
    curve.use_fill_caps = True
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points)-1)
    for point, co in zip(spline.bezier_points, points):
        point.co = co
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    activate(obj)
    bpy.ops.object.convert(target="MESH")
    return finish(bpy.context.object, name, mat, group, 0)


def boolean(obj, cutter, operation="DIFFERENCE"):
    mod = obj.modifiers.new("Actual machined opening", "BOOLEAN")
    mod.operation = operation
    mod.solver = "EXACT"
    mod.object = cutter
    apply_mod(obj, mod)
    bpy.data.objects.remove(cutter, do_unlink=True)


def flange(name, center, outer, bore, thickness, pcd, holes, hole_radius,
           mat, group=None, axis=(1, 0, 0)):
    obj = ring(name, center, outer, bore, thickness, mat, group, axis=axis, bevel=0)
    basis = axis_matrix(axis)
    for i in range(holes):
        theta = TAU*(i+.37)/holes
        pos = Vector(center) + basis @ Vector((pcd*math.cos(theta), pcd*math.sin(theta), 0))
        cutter = cylinder("tool", pos, hole_radius, thickness*4,
                          None, axis=axis, segments=12, bevel=0)
        boolean(obj, cutter)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.to_mesh(obj.data)
    bm.free()
    return finish(obj, name, mat, group, .0007)


def bolt(name, surface, axis, mat, group=None, radius=.010, length=.035):
    axis = Vector(axis).normalized()
    surface = Vector(surface)
    ring(name+"_Washer", surface+axis*.001, radius*1.55, radius*.55,
         .002, mat, group, axis=axis, segments=16, bevel=.0005)
    cylinder(name+"_HexHead", surface+axis*(.002+radius*.4), radius,
             radius*.8, mat, group, axis=axis, segments=6, bevel=.0006)
    cylinder(name+"_Stud", surface-axis*(length/2), radius*.52,
             length, mat, group, axis=axis, segments=8, bevel=0)
    ring(name+"_HexNut", surface-axis*(length-radius*.4), radius,
         radius*.65, radius*.8, mat, group, axis=axis, segments=6, bevel=.0005)


def label(name, text, center, size, mat, group=None, normal=(0, -1, 0)):
    curve = bpy.data.curves.new(name, "FONT")
    curve.body = text
    curve.resolution_u = 3
    curve.size = size
    curve.extrude = .00006
    curve.space_character = 1.1
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = center
    obj.rotation_euler = Vector(normal).to_track_quat("Z", "Y").to_euler()
    activate(obj)
    bpy.ops.object.convert(target="MESH")
    return finish(bpy.context.object, name, mat, group, 0, False)


def material(name, color, metallic, roughness, grain=0, texture_set=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1)
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if grain:
        tex = nodes.new("ShaderNodeTexNoise")
        tex.inputs["Scale"].default_value = 220
        tex.inputs["Detail"].default_value = 2
        bump = nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = .18
        bump.inputs["Distance"].default_value = grain
        mat.node_tree.links.new(tex.outputs["Fac"], bump.inputs["Height"])
        mat.node_tree.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
        ramp = nodes.new("ShaderNodeMapRange")
        ramp.inputs["To Min"].default_value = roughness*.85
        ramp.inputs["To Max"].default_value = min(1, roughness*1.12)
        mat.node_tree.links.new(tex.outputs["Fac"], ramp.inputs["Value"])
        mat.node_tree.links.new(ramp.outputs["Result"], bsdf.inputs["Roughness"])
    if texture_set:
        coord = nodes.new("ShaderNodeTexCoord")
        textures = {}
        for channel, image in texture_set.items():
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = image
            tex.label = channel
            tex.location = (-700, -170*len([n for n in nodes if n.type == "TEX_IMAGE"]))
            mat.node_tree.links.new(coord.outputs["UV"], tex.inputs["Vector"])
            textures[channel] = tex
        tint = nodes.new("ShaderNodeMixRGB")
        tint.blend_type = "MULTIPLY"
        tint.inputs[0].default_value = 1
        tint.inputs[2].default_value = (*color, 1)
        mat.node_tree.links.new(textures["BaseColor"].outputs["Color"], tint.inputs[1])
        mat.node_tree.links.new(tint.outputs[0], bsdf.inputs["Base Color"])
        normal = nodes.new("ShaderNodeNormalMap")
        normal.inputs["Strength"].default_value = .25
        mat.node_tree.links.new(textures["Normal"].outputs["Color"], normal.inputs["Color"])
        mat.node_tree.links.new(normal.outputs["Normal"], bump.inputs["Normal"] if grain else bsdf.inputs["Normal"])
        variation = nodes.new("ShaderNodeMapRange")
        variation.inputs["To Min"].default_value = roughness*.85
        variation.inputs["To Max"].default_value = min(1, roughness*1.12)
        mat.node_tree.links.new(textures["Roughness"].outputs["Color"], variation.inputs["Value"])
        mat.node_tree.links.new(variation.outputs["Result"], bsdf.inputs["Roughness"])
        mat.node_tree.links.new(textures["Metallic"].outputs["Color"], bsdf.inputs["Metallic"])
    return mat


def surface_library(folder):
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    result = {}
    rng = np.random.default_rng(431)

    def soft_noise(size, cells):
        tile = rng.random((cells, cells), dtype=np.float32)
        index = np.arange(size, dtype=np.float32)*cells/size
        lower = index.astype(np.int32)
        weight = index-lower
        weight = weight*weight*(3-2*weight)
        upper = (lower+1) % cells
        a = tile[lower[:, None], lower[None, :]]
        b = tile[lower[:, None], upper[None, :]]
        c = tile[upper[:, None], lower[None, :]]
        d = tile[upper[:, None], upper[None, :]]
        return ((a*(1-weight)[None, :]+b*weight[None, :])*(1-weight)[:, None]
                +(c*(1-weight)[None, :]+d*weight[None, :])*weight[:, None])-.5

    for family, size in (("cast", 2048), ("machined", 1024), ("rubber", 512)):
        noise = rng.random((size, size), dtype=np.float32)
        yy, xx = np.mgrid[:size, :size].astype(np.float32)/size
        fine = soft_noise(size, size//8)
        broad = soft_noise(size, 12)
        height = .5 + .16*fine + .035*(noise-.5)
        rough = .70 + .10*fine + .045*broad
        if family == "machined":
            lines = np.sin((yy*330+.04*broad)*math.tau)
            height = .5+.030*lines+.018*(noise-.5)+.016*broad
            rough = .26+.045*lines+.045*broad+.015*(noise-.5)
        elif family == "rubber":
            rough = .90+.035*fine
        dx = (np.roll(height, -1, axis=1)-np.roll(height, 1, axis=1))*.55
        dy = (np.roll(height, -1, axis=0)-np.roll(height, 1, axis=0))*.55
        values = {
            "BaseColor": np.repeat((.91 + .025*broad+.018*fine)[..., None], 3, axis=2),
            "Normal": np.stack((.5-dx, .5-dy, np.ones_like(dx)), axis=2),
            "Roughness": np.repeat(rough[..., None], 3, axis=2),
            "Metallic": np.full((size, size, 3), 1 if family == "machined" else 0, dtype=np.float32),
            "Height": np.repeat(height[..., None], 3, axis=2),
        }
        result[family] = {}
        for channel, rgb in values.items():
            path = folder / f"{family}_{channel}.png"
            if path.exists():
                image = bpy.data.images.load(str(path), check_existing=True)
            else:
                image = bpy.data.images.new(f"{family}_{channel}", size, size)
                rgba = np.concatenate((np.clip(rgb, 0, 1), np.ones((size, size, 1), dtype=np.float32)), axis=2)
                image.pixels.foreach_set(rgba.astype(np.float32).ravel())
                image.filepath_raw = str(path)
                image.file_format = "PNG"
                image.save()
            if channel != "BaseColor":
                image.colorspace_settings.name = "Non-Color"
            image.pack()
            result[family][channel] = image
    return result
