"""Independent FBX round-trip geometry and hierarchy verification."""
import json
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from cabinet_components import GROUP_NAMES

MODEL = HERE.parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(MODEL/"fbx"/"electrical-cabinet.fbx"),
                         use_anim=False)
scene = bpy.context.scene
objects = list(scene.objects)
root = scene.objects.get("electrical-cabinet")
meshes = [o for o in objects if o.type == "MESH"]
points = [o.matrix_world@v.co for o in meshes for v in o.data.vertices]
lo = [min(p[i] for p in points) for i in range(3)]
hi = [max(p[i] for p in points) for i in range(3)]
dimensions = [hi[i]-lo[i] for i in range(3)]
triangles = 0
for obj in meshes:
    obj.data.calc_loop_triangles()
    triangles += len(obj.data.loop_triangles)
checks = {
    "root_and_eight_groups": bool(root) and tuple(sorted(o.name for o in root.children)) == GROUP_NAMES,
    "dimensions_match_metres": all(abs(a/b-1) <= .03 for a, b in zip(dimensions, (1.2, .65, 2))),
    "unit_object_scale": all((o.scale-Vector((1, 1, 1))).length < 1e-5 for o in objects),
    "no_cameras_lights_armatures": all(o.type in {"EMPTY", "MESH"} for o in objects),
    "nine_structural_empties_only": sum(o.type == "EMPTY" for o in objects) == 9,
    "no_animation": all(o.animation_data is None for o in objects),
    "triangle_budget": triangles <= 110000,
    "uv_on_every_mesh": all(o.data.uv_layers for o in meshes),
    "materials_on_every_mesh": all(o.data.materials for o in meshes),
    "lamp_lens_name": any("lamp" in o.name and "lens" in o.name for o in meshes),
}
report = {
    "date": datetime.now().astimezone().isoformat(), "blender": bpy.app.version_string,
    "checks": checks, "passed": all(checks.values()), "dimensions_blender_xyz_m": dimensions,
    "bounds_min": lo, "bounds_max": hi, "triangles": triangles,
    "meshes": len(meshes), "objects": len(objects),
    "root_matrix": [list(row) for row in root.matrix_world] if root else None,
    "scope": "FBX independently imported into Blender; Unity importer and URP not tested",
}
path = MODEL/"reports"/"electrical-cabinet-fbx-verification.json"
path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("CABINET_FBX_VERIFIED", json.dumps(report), flush=True)
if not report["passed"]:
    raise RuntimeError("FBX checks failed; see "+str(path))
