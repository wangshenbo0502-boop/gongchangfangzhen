"""Run in Blender with the reviewed tank loaded; never save or modify its source.

Export just the original shell hierarchy with a closed, 12 mm local perforation.
After Unity axis baking, mesh local coordinates are (Blender X, Blender Y, -Blender Z).
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree


def boundary_edges(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    count = sum(not edge.is_manifold for edge in bm.edges)
    bm.free()
    return count


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--report", required=True)
    parser.add_argument("--mechanism", choices=("perforation", "thinning"), default="perforation")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    output = Path(args.output).resolve()
    report = Path(args.report).resolve()
    if output.exists() or report.exists():
        raise ValueError("Refusing to overwrite a derived asset or evidence")
    bpy.context.window.scene = bpy.data.scenes["01_ASSEMBLED"]
    bpy.context.scene.frame_set(1)
    shell = bpy.data.objects["TNK_01_Shell_8mmWall"]
    graph = bpy.context.evaluated_depsgraph_get()
    intact = bpy.data.meshes.new_from_object(shell.evaluated_get(graph))
    shell.modifiers.clear()
    shell.data = intact.copy()
    before = [v.co.copy() for v in intact.vertices]
    original_bounds = [[min(v[i] for v in before), max(v[i] for v in before)] for i in range(3)]
    print("SOURCE_SHELL", original_bounds, [list(row) for row in shell.matrix_world], flush=True)
    anchor = Vector((-.4354, -1.1354, 3.0))
    normal = Vector((-.2903, -.9569, 0)).normalized()
    radius = .006
    hit, location, _, _ = shell.ray_cast(anchor + normal * .06, -normal)
    if not hit or (location - anchor).length > .002:
        raise ValueError("Anchor does not meet the continuous outer shell")
    thinning = args.mechanism == "thinning"
    if thinning:
        anchor = location.copy()
        bpy.ops.mesh.primitive_uv_sphere_add(segments=96, ring_count=64, radius=.08,
            location=shell.matrix_world @ (anchor + normal * .076))
    else:
        bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=radius, depth=.12,
                                          location=shell.matrix_world @ anchor)
    cutter = bpy.context.object
    cutter.name = "Temporary local perforation cutter"
    cutter.rotation_mode = "QUATERNION"
    cutter.rotation_quaternion = normal.to_track_quat("Z", "Y")
    bpy.context.view_layer.update()
    print("CUTTER", list(cutter.location), list(cutter.dimensions),
          "RAY", shell.ray_cast(anchor + normal * .06, -normal), flush=True)
    bpy.context.view_layer.objects.active = shell
    modifier = shell.modifiers.new("Local preset teaching defect", "BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.object = cutter
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    after = shell.data
    tree = KDTree(len(after.vertices))
    for i, vertex in enumerate(after.vertices):
        tree.insert(vertex.co, i)
    tree.balance()
    untouched = 0
    for point in before:
        delta = point - anchor
        distance = (delta - normal * delta.dot(normal)).length
        if distance > (.04 if thinning else radius + .01):
            if tree.find(point)[2] > 1e-6:
                raise ValueError("Nonlocal vertex changed")
            untouched += 1
    new_bounds = [[min(v.co[i] for v in after.vertices), max(v.co[i] for v in after.vertices)] for i in range(3)]
    if any(abs(a - b) > 1e-6 for first, second in zip(original_bounds, new_bounds) for a, b in zip(first, second)):
        raise ValueError("Whole-shell bounds changed")
    if len(after.vertices) <= len(before) or boundary_edges(after) > boundary_edges(intact):
        raise ValueError("Boolean did not produce a closed local opening: vertices %s/%s, nonmanifold %s/%s" %
                         (len(before), len(after.vertices), boundary_edges(intact), boundary_edges(after)))
    # Both cylinder-wall intersections must exist: this checks the inner/outer rim.
    rim = []
    for vertex in after.vertices:
        delta = vertex.co - anchor
        radial = (delta - normal * delta.dot(normal)).length
        if abs(radial - radius) < .0001 and abs(delta.dot(normal)) < .03:
            rim.append(delta.dot(normal))
    if not thinning and (len(rim) < 48 or max(rim) - min(rim) < .006):
        raise ValueError("Missing perforation rim or shell thickness")
    remaining = None
    if thinning:
        outer_hit, outer, _, _ = shell.ray_cast(anchor + normal * .03, -normal)
        inner_hit, inner, _, _ = shell.ray_cast(outer - normal * .00001, -normal)
        remaining = (outer - inner).dot(normal) if outer_hit and inner_hit else 0
        if not .0035 < remaining < .0045:
            raise ValueError("Preset thinning did not retain the nominal 4 mm wall: %s" % remaining)
    selected = []
    node = shell
    while node:
        selected.append(node)
        node = node.parent
    bpy.ops.object.select_all(action="DESELECT")
    for node in selected:
        node.select_set(True)
    bpy.context.view_layer.objects.active = shell
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(output), use_selection=True, object_types={"EMPTY", "MESH"},
        global_scale=1, apply_unit_scale=True, axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, apply_scale_options="FBX_SCALE_UNITS",
        use_mesh_modifiers=True, mesh_smooth_type="OFF", use_tspace=False,
        add_leaf_bones=False, bake_anim=False, path_mode="AUTO", use_custom_props=False)
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps({
        "source": str(Path(bpy.data.filepath).resolve()),
        "sourceSha256": hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),
        "derivedAsset": str(output), "derivedSha256": hashlib.sha256(output.read_bytes()).hexdigest(),
        "mechanism": "Preset spherical local thinning; not corrosion or stress solving" if thinning else
                     "Preset cylindrical wall perforation; not corrosion or leakage solving",
        "diameterM": None if thinning else radius * 2, "nominalWallM": .008,
        "centerRemainingWallM": remaining, "presetRemovalDepthM": .004 if thinning else None,
        "unityLocalAnchorM": [-.4354, -1.1354, -3.0],
        "originalVertices": len(before), "derivedVertices": len(after.vertices),
        "unchangedNonlocalVertices": untouched,
        "originalNonmanifoldEdges": boundary_edges(intact),
        "derivedNonmanifoldEdges": boundary_edges(after),
        "rimVertices": len(rim) if not thinning else None,
        "rimDepthM": max(rim) - min(rim) if not thinning else None,
        "originalBounds": original_bounds, "derivedBounds": new_bounds,
    }, indent=2), encoding="utf-8")
    print("LOCAL_PERFORATION_VERIFIED", len(before), len(after.vertices), len(rim), flush=True)


if __name__ == "__main__":
    main()
