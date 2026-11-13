"""Read-only source inspection for the independent cabinet viewer."""
import json
import bpy

scene = bpy.data.scenes["01_ASSEMBLED"]
bpy.context.window.scene = scene
scene.frame_set(1)
for mat in bpy.data.materials:
    if not mat.name.startswith("CAB /") or not mat.use_nodes:
        continue
    nodes = []
    for node in mat.node_tree.nodes:
        if node.type in {"OUTPUT_MATERIAL", "TEX_COORD", "MAPPING", "TEX_NOISE", "BUMP"}:
            continue
        inputs = {}
        for socket in node.inputs:
            if socket.is_linked:
                inputs[socket.name] = [(link.from_node.name, link.from_socket.name)
                                       for link in socket.links]
            elif hasattr(socket, "default_value"):
                value = socket.default_value
                if isinstance(value, (float, int, str)):
                    inputs[socket.name] = value
                elif hasattr(value, "__len__"):
                    inputs[socket.name] = list(value)
        nodes.append({"name": node.name, "type": node.type, "inputs": inputs,
                      "image": node.image.filepath if node.type == "TEX_IMAGE" and node.image else None})
    print("MATERIAL", mat.name, json.dumps(nodes), flush=True)
for obj in scene.objects:
    if obj.type == "MESH" and any(text in obj.name.lower() for text in
                                  ("protectiveearth", "ethernet", "bond", "doorcable")):
        bounds = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        print("WIRE", obj.name, obj.parent.name, dict(obj.items()),
              [[min(v[i] for v in bounds), max(v[i] for v in bounds)] for i in range(3)],
              flush=True)
