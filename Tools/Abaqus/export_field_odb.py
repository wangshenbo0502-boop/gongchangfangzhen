"""Abaqus Python: export one DC3D4 instance and original NODAL NT11.

OdbMeshNode.coordinates are already global Cartesian coordinates. Instance
rotation/translation must NOT be applied a second time. No solver is submitted.
"""
import argparse
import hashlib
import json
import math
import os
import re
from datetime import datetime, timezone

EXPORTER_VERSION = "workshop-field-2.0"
AXES = {"physicalTime": "s", "loadParameter": "1", "frequency": "Hz",
        "mode": "1", "cycles": "cycle"}


def finite(value):
    value = float(value)
    if not math.isfinite(value):
        raise ValueError("Nonfinite value")
    return value


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def subtract(a, b):
    return [x - y for x, y in zip(a, b)]


def cross(a, b):
    return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]


def dot(a, b):
    return sum(x*y for x, y in zip(a, b))


def export_mesh(instance, scale):
    if finite(scale) <= 0:
        raise ValueError("Length conversion must be positive")
    nodes = sorted(instance.nodes, key=lambda n: n.label)
    if not nodes or len(nodes) > 200000:
        raise ValueError("Node budget: 1..200000")
    indices = {}
    positions = []
    for index, node in enumerate(nodes):
        key = (instance.name, node.label)
        if node.label <= 0 or key in indices or len(node.coordinates) != 3:
            raise ValueError("Invalid node label or coordinate")
        indices[key] = index
        positions.append([finite(c) * scale for c in node.coordinates])
    elements, faces, labels = [], {}, set()
    # Face numbers match tetrahedron faces S1=123,S2=142,S3=243,S4=341.
    for element in instance.elements:
        if element.type != "DC3D4":
            raise ValueError("Unsupported element type: " + element.type)
        if element.label <= 0 or element.label in labels or len(element.connectivity) != 4:
            raise ValueError("Invalid element")
        labels.add(element.label)
        ix = [indices[(instance.name, label)] for label in element.connectivity]
        if len(set(ix)) != 4:
            raise ValueError("Degenerate connectivity")
        points = [positions[i] for i in ix]
        if abs(dot(cross(subtract(points[1], points[0]), subtract(points[2], points[0])),
                   subtract(points[3], points[0]))) < 1e-24:
            raise ValueError("Zero volume element")
        elements.append({"elementLabel": element.label, "type": element.type, "nodeIndices": ix})
        for face, (a, b, c, opposite) in enumerate(((0, 1, 2, 3), (0, 3, 1, 2),
                                                  (1, 3, 2, 0), (2, 3, 0, 1)), 1):
            tri = [ix[a], ix[b], ix[c]]
            normal = cross(subtract(positions[tri[1]], positions[tri[0]]),
                           subtract(positions[tri[2]], positions[tri[0]]))
            if dot(normal, subtract(positions[ix[opposite]], positions[tri[0]])) > 0:
                tri[1], tri[2] = tri[2], tri[1]
            key = tuple(sorted(tri))
            faces.setdefault(key, []).append((tri, element.label, face))
            if len(faces[key]) > 2:
                raise ValueError("Nonmanifold face")
    if not elements:
        raise ValueError("Empty element region")
    triangles, owners, sides = [], [], []
    for entries in faces.values():
        if len(entries) == 1:
            tri, element, side = entries[0]
            triangles.extend(tri)
            owners.append(element)
            sides.append(side)
    return {"meshId": "fixed-0", "instanceName": instance.name,
            "nodeLabels": [n.label for n in nodes],
            "coordinatesM": [v for p in positions for v in p],
            "elements": elements, "triangles": triangles,
            "triangleElementLabels": owners, "triangleFaces": sides}


def export_frame(frame, index, instance, labels, offset, nodal):
    if "NT11" not in frame.fieldOutputs:
        raise ValueError("Missing NT11 at frame " + str(index))
    values = frame.fieldOutputs["NT11"].getSubset(region=instance).values
    temperatures = {}
    for value in values:
        if value.position != nodal or value.instance.name != instance.name:
            raise ValueError("Only original NODAL temperature of selected instance is supported")
        key = value.nodeLabel
        if key in temperatures:
            raise ValueError("Duplicate nodal temperature")
        temperatures[key] = finite(value.data) + finite(offset)
    if set(temperatures) != set(labels):
        raise ValueError("Temperature labels do not cover the exact mesh")
    ordered = [temperatures[label] for label in labels]
    if min(ordered) < -273.15:
        raise ValueError("Temperature below absolute zero")
    return {"frameIndex": index, "axisValue": finite(frame.frameValue), "meshId": "fixed-0",
            "temperatureC": ordered}


def write_json(root, relative, value):
    encoded = json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")
    if len(encoded) > 32 * 1024 * 1024:
        raise ValueError("Data file exceeds 32 MiB")
    path = os.path.join(root, relative)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "xb") as stream:
        stream.write(encoded)
    return {"path": relative, "sha256": sha256(path)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("odb", "input", "step", "instance", "device", "fault", "case-id",
                 "component-id", "model-version", "analysis-description", "output",
                 "source-length-unit", "source-temperature-unit"):
        parser.add_argument("--" + name, required=True)
    parser.add_argument("--axis-kind", choices=tuple(AXES), required=True)
    parser.add_argument("--confirm-axis-semantics", action="store_true")
    parser.add_argument("--length-to-m", type=float, required=True)
    parser.add_argument("--temperature-offset-c", type=float, required=True)
    args = parser.parse_args()
    if not args.confirm_axis_semantics:
        raise ValueError("Explicit confirmation of the step axis semantics is required")
    for identifier in (args.device, args.fault, args.case_id):
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,95}", identifier):
            raise ValueError("Unsafe identifier")
    if os.path.exists(args.output):
        raise ValueError("Output must be a new directory; partial exports must not be imported")
    from odbAccess import openOdb
    from abaqusConstants import NODAL, TIME
    odb = openOdb(path=os.path.abspath(args.odb), readOnly=True)
    try:
        step = odb.steps[args.step]
        # This first exporter is thermal only. Static thermal steps use a load parameter.
        if step.domain != TIME or args.axis_kind not in ("physicalTime", "loadParameter"):
            raise ValueError("First thermal exporter supports TIME domain only; confirm physicalTime or loadParameter")
        instance = odb.rootAssembly.instances[args.instance]
        mesh = export_mesh(instance, args.length_to_m)
        files = [write_json(args.output, "mesh.json", mesh)]
        frames, previous = [], -1.0
        if not 1 <= len(step.frames) <= 20000:
            raise ValueError("Frame count exceeds budget")
        for index, frame in enumerate(step.frames):
            item = export_frame(frame, index, instance, mesh["nodeLabels"], args.temperature_offset_c, NODAL)
            if item["axisValue"] < 0 or item["axisValue"] <= previous:
                raise ValueError("Axis must strictly increase")
            relative = "frames/frame-{:06d}.json".format(index)
            files.append(write_json(args.output, relative, item))
            frames.append({"frameIndex": index, "axisValue": item["axisValue"], "path": relative})
            previous = item["axisValue"]
        manifest = {
            "schemaVersion": 2, "source": "Abaqus ODB", "deviceId": args.device,
            "faultCode": args.fault, "caseId": args.case_id, "componentId": args.component_id,
            "modelVersion": args.model_version, "job": os.path.splitext(os.path.basename(args.odb))[0],
            "step": args.step, "region": args.instance, "odbSha256": sha256(args.odb),
            "inputSha256": sha256(args.input), "exporterVersion": EXPORTER_VERSION,
            "solverVersion": str(odb.jobData.version), "analysisDescription": args.analysis_description,
            "exportedAt": datetime.now(timezone.utc).isoformat(),
            "axisKind": args.axis_kind, "axisUnit": AXES[args.axis_kind],
            "coordinatesSpace": "globalCartesian", "coordinatesUnit": "m",
            "sourceLengthUnit": args.source_length_unit, "sourceTemperatureUnit": args.source_temperature_unit,
            "lengthToM": args.length_to_m, "temperatureOffsetC": args.temperature_offset_c,
            "coordinateConversion": "ODB global XYZ * lengthToM; identity display transform; instance placement already applied",
            "displayTransform": [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1],
            "fieldName": "temperatureC", "fieldUnit": "C", "outputPosition": "NODAL",
            "averaging": "none", "meshPath": "mesh.json", "files": files, "frames": frames}
        if sum(os.path.getsize(os.path.join(args.output, f["path"])) for f in files) > 255*1024*1024:
            raise ValueError("Package exceeds 256 MiB")
        encoded = json.dumps(manifest, ensure_ascii=False, allow_nan=False, indent=2).encode("utf-8")
        if len(encoded) > 1024*1024:
            raise ValueError("Manifest exceeds 1 MiB")
        # Manifest is the completion marker; failed partial exports have no manifest.
        with open(os.path.join(args.output, "manifest.json"), "xb") as stream:
            stream.write(encoded)
    finally:
        odb.close()
    print("Exported original nodal temperatures; no stress, deformation or crack interpolation.")


if __name__ == "__main__":
    main()
