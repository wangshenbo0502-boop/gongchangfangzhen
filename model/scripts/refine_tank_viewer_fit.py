"""Prepare the tank viewer's curved lug seats and detachable joints."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

MODEL = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(MODEL / "scripts"))
from industrial_parts import boolean, bolt, box, cylinder, finish, mesh, pipe, ring
from pump_uv import planar_face

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
objects = bpy.data.objects
accessories = objects["TNK_11_TopAccessories"]
steel = bpy.data.materials["TK Stainless"]
machined = bpy.data.materials["TK Machined"]
coat = bpy.data.materials["TK Shell"]
roof = objects["TNK_02_TopHead_2to1Ellipsoidal"]


def bvh(obj):
    return BVHTree.FromPolygons([obj.matrix_world @ v.co for v in obj.data.vertices],
                               [tuple(p.vertices) for p in obj.data.polygons])


roof_tree = bvh(roof)


def roof_height(x, y):
    point = roof_tree.ray_cast((x, y, 6.5), (0, 0, -1), 2)[0]
    if point is None:
        raise RuntimeError("Lug shoe is outside the actual roof surface")
    return point.z


def mark(obj):
    obj["equipment_mesh"] = True
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    for face in obj.data.polygons:
        planar_face(obj.data, face)
    return obj


for obj in list(scene.objects):
    if obj.name.startswith(("TNK_11_TopAccessories_LugShoe",
                            "TNK_11_TopAccessories_LiftingLug",
                            "TNK_10_ManwayCover_HingePin")):
        bpy.data.objects.remove(obj, do_unlink=True)

for i, x in enumerate((-1.02, 1.02), 1):
    y = .30
    n = 16
    perimeter = [(x + .101*math.cos(math.tau*k/n), y + .070*math.sin(math.tau*k/n))
                 for k in range(n)]
    base = [(x, y, roof_height(x, y)-.001)] + [(px, py, roof_height(px, py)-.001) for px, py in perimeter]
    seat_top = max(p[2] for p in base) + .011
    points = base + [(px, py, seat_top) for px, py, _ in base]
    top = n+1
    faces = []
    for k in range(1, n+1):
        nxt = 1 + k % n
        faces.extend(((0, nxt, k), (top, top+k, top+nxt), (k, nxt, top+nxt, top+k)))
    name = f"TNK_11_TopAccessories_LugSeat_{i}"
    mark(finish(mesh(name, points, faces, machined, accessories, 0), name, machined, accessories, 0, False))
    radius = .050
    eye_z = seat_top + .055
    profile = [(x-.063, seat_top), (x+.063, seat_top), (x+radius, eye_z)]
    profile.extend((x+radius*math.cos(math.pi*k/20), eye_z+radius*math.sin(math.pi*k/20))
                   for k in range(1, 21))
    count = len(profile)
    points = [(px, y+side*.010, z) for side in (-1, 1) for px, z in profile]
    faces = [tuple(reversed(range(count))), tuple(range(count, 2*count))]
    faces.extend((k, (k+1)%count, (k+1)%count+count, k+count) for k in range(count))
    name = f"TNK_11_TopAccessories_LiftingLug_{i}"
    lug = mesh(name, points, faces, steel, accessories, 0)
    cutter = cylinder("Eye tool", (x, y, eye_z), .027, .060, None, axis=(0,1,0), segments=40, bevel=0)
    boolean(lug, cutter)
    mark(finish(lug, name, steel, accessories, .0013, False))
    for side in (-1, 1):
        foot_y = y + side*.040
        mark(cylinder(name+"_Washer", (x, foot_y, seat_top+.002), .014, .004, machined,
                      accessories, axis=(0,0,1), segments=20, bevel=.0005))
        mark(cylinder(name+"_Bolt", (x, foot_y, seat_top+.009), .010, .010, steel,
                      accessories, axis=(0,0,1), segments=6, bevel=.0008))
    bpy.context.view_layer.update()
    points = [lug.matrix_world @ v.co for v in lug.data.vertices]
    clearance = min(v.z for v in points) - max(roof_height(v.x, v.y) for v in points)
    bore = bvh(lug).ray_cast((x,y-.03,eye_z), (0,1,0), .06)[0]
    if clearance < .005 or bore is not None:
        raise RuntimeError(f"Roof lug {i} fails seat or through-hole check")
    print(f"TANK_LUG {i}: roof clearance {clearance*1000:.2f} mm, bore open")

# The cover's hinge pin is withdrawn before the demonstrator separates the cover.
# Rebuild the stationary hinge barrels as sleeves with bores, leaving the pin
# represented as two seated caps on the neck assembly.
neck = objects["TNK_04_Manway"]
for obj in [obj for obj in scene.objects if obj.name.startswith("TNK_04_Manway_HingeBarrel")]:
    center = obj.matrix_world.translation.copy()
    name = obj.name
    bpy.data.objects.remove(obj, do_unlink=True)
    mark(ring(name, center, .024, .0125, .07, steel, neck, axis=(0,0,1), segments=28, bevel=.001))
    cap = center + Vector((0,0,.035 if center.z > 2.5 else -.035))
    mark(cylinder("TNK_04_Manway_WithdrawablePinCap", cap, .014, .008, machined,
                  neck, axis=(0,0,1), segments=24, bevel=.0008))

# Give the ladder cage and platform a shared landing without overlapping the
# cage upright with the walking surface or the guard post with the top hoop.
platform = objects["TNK_07_Platform"]
step = objects["TNK_07_Platform_LandingStep"]
bpy.data.objects.remove(step, do_unlink=True)
mark(box("TNK_07_Platform_LandingStep", (-.802, 1.902, 4.70),
         (.244, .099, .025), machined, platform, .001))
post = objects["TNK_07_Platform_GuardPost"]
post.location += post.parent.matrix_world.to_3x3().inverted() @ Vector((.073, 0, 0))
for obj in scene.objects:
    if obj.name.startswith(("TNK_06_Ladder_CageUpright", "TNK_06_Ladder_CageHoop.004")):
        obj.data = obj.data.copy()
        inverse = obj.matrix_world.inverted()
        for v in obj.data.vertices:
            p = obj.matrix_world @ v.co
            if p.z > 4.54:
                p.z = 4.54 + (p.z-4.54)*.04
                v.co = inverse @ p
        obj.data.update()

# The seating ring stays on the lower head when the frame is lowered.
def reparent(obj, parent):
    pose = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = pose


lower = objects["TNK_03_BottomHead"]
legs = objects["TNK_00_Legs"]
obj = objects["TNK_00_Legs_TopRing"]
obj.name = "TNK_03_BottomHead_FrameSeat"
reparent(obj, lower)
for obj in list(scene.objects):
    if obj.name.startswith(("TNK_01_Shell_LegBondStud", "TNK_01_Shell_BondToLeg",
                            "TNK_00_Legs_BondLug", "TNK_00_Legs_BondingStrap")):
        bpy.data.objects.remove(obj, do_unlink=True)

# Cope the tops of the supports to the underside so lowering the frame
# releases the joint without sweeping a leg or gusset through the head.
head_tree = bvh(objects["TNK_03_BottomHead_2to1Ellipsoidal"])
for obj in list(scene.objects):
    if not obj.name.startswith(("TNK_00_Legs_Post_", "TNK_00_Legs_Gusset_")):
        continue
    bounds = [obj.matrix_world @ Vector(p) for p in obj.bound_box]
    xmin, xmax = min(v.x for v in bounds)-.01, max(v.x for v in bounds)+.01
    ymin, ymax = min(v.y for v in bounds)-.01, max(v.y for v in bounds)+.01
    steps = 8
    stride = steps + 1
    points = []
    for j in range(stride):
        for i in range(stride):
            x = xmin + (xmax-xmin)*i/steps
            y = ymin + (ymax-ymin)*j/steps
            point = head_tree.ray_cast((x,y,.50), (0,0,1), 1.10)[0]
            points.append((x, y, point.z-.001 if point else 1.58))
    n = len(points)
    points += [(x,y,1.80) for x,y,_ in points]
    faces = []
    for j in range(steps):
        for i in range(steps):
            k = j*stride+i
            faces += [(k,k+stride,k+1), (k+1,k+stride,k+stride+1),
                      (n+k,n+k+1,n+k+stride), (n+k+1,n+k+stride+1,n+k+stride)]
    edges = list(range(stride)) + [j*stride+steps for j in range(1,stride)]
    edges += [steps*stride+i for i in range(steps-1,-1,-1)] + [j*stride for j in range(steps-1,0,-1)]
    for k, nxt in zip(edges, edges[1:]+edges[:1]):
        faces.append((k,nxt,n+nxt,n+k))
    obj.data = obj.data.copy()
    boolean(obj, mesh("Support saddle trim", points, faces, None, bevel=0))
    mark(obj)

# Bonding fasteners on the outer faces of the square legs remain accessible
# below the seating ring and outside the envelope of the lower head.
copper = bpy.data.materials["TK BondingCopper"]
for name, surface, normal in (
    ("FrontBond", (-.86, -.641, .81), (0,-1,0)),
    ("RearBond", (-.86, 1.241, .81), (0,1,0)),
):
    before = set(scene.objects)
    bolt("TNK_00_Legs_" + name, surface, normal, copper, legs, .009, .014)
    for item in set(scene.objects) - before:
        mark(item)
mark(pipe("TNK_00_Legs_BondingLead",
          [(-.86,-.656,.81),(-.86,-.671,.72),(-.86,-.675,.24),(-.86,-.655,.067)],
          .006, copper, legs, sides=8))

bpy.ops.wm.save_as_mainfile(filepath=str(MODEL / "source/silo-viewer-fit-review.blend"), compress=True)
