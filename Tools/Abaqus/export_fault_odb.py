"""Run with Abaqus Python; never fabricates results or submits solver jobs."""
from __future__ import print_function

import argparse
import hashlib
import json
import math
import os
import re
from datetime import datetime, timezone


def finite(value):
    value = float(value)
    if not math.isfinite(value):
        raise ValueError("ODB contains a non-finite value")
    return value


def export_frames(step, region, fields, stress_scale, displacement_scale, temperature_offset):
    frames = []
    skipped = 0
    previous = -1.0
    for frame in step.frames:
        if any(name not in frame.fieldOutputs for name in fields):
            skipped += 1
            continue
        time = finite(frame.frameValue)
        if time < 0 or time <= previous:
            raise ValueError("Frame times must be nonnegative and strictly increasing")
        item = {"time": time}
        for name in fields:
            field = frame.fieldOutputs[name].getSubset(region=region)
            values = field.values
            if not values:
                raise ValueError("Empty field {} in selected instance".format(name))
            if name == "S":
                from abaqusConstants import MISES
                if MISES not in field.validInvariants:
                    raise ValueError("Selected stress field does not support the Mises invariant")
                item["maxStressMPa"] = max(finite(value.mises) for value in values) * stress_scale
            elif name == "U":
                item["maxDisplacementMm"] = max(finite(value.magnitude) for value in values) * displacement_scale
            else:
                item["maxTemperatureC"] = max(finite(value.data) for value in values) + temperature_offset
        frames.append(item)
        previous = time
    if not frames:
        raise ValueError("No frames contain all requested fields in the selected instance")
    if len(frames) > 20000:
        raise ValueError("More than 20000 frames; reduce the ODB output frequency")
    return frames, skipped


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--odb", required=True)
    parser.add_argument("--step", required=True)
    parser.add_argument("--instance", required=True, help="Exact ODB assembly instance name")
    parser.add_argument("--device", required=True)
    parser.add_argument("--fault", required=True)
    parser.add_argument("--fields", default="S,U", help="S,U,NT11 or TEMP (comma separated)")
    parser.add_argument("--stress-to-mpa", type=float, required=True)
    parser.add_argument("--displacement-to-mm", type=float, required=True)
    parser.add_argument("--temperature-offset-c", type=float, required=True)
    parser.add_argument("--confirm-physical-time", action="store_true",
                        help="Confirm step time is physical seconds, not static load factor or frequency")
    parser.add_argument("--analysis-description", required=True,
                        help="Geometry, material, boundary conditions, loads, and limitations")
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def main():
    args = parse_args()
    if not args.confirm_physical_time:
        raise ValueError("Confirm physical seconds; static/modal results require a separate workflow")
    if not args.analysis_description.strip():
        raise ValueError("Analysis description is required")
    for identifier in (args.device, args.fault):
        if not re.fullmatch(r"[A-Z0-9]+(?:-[A-Z0-9]+)*", identifier):
            raise ValueError("Device and fault identifiers must use uppercase letters, digits, and hyphens")
    fields = list(dict.fromkeys(args.fields.split(",")))
    if not fields or any(name not in ("S", "U", "NT11", "TEMP") for name in fields):
        raise ValueError("Supported fields are S, U, NT11, TEMP")
    if "NT11" in fields and "TEMP" in fields:
        raise ValueError("Select one temperature field")
    for scale in (args.stress_to_mpa, args.displacement_to_mm):
        if finite(scale) <= 0:
            raise ValueError("Unit conversion scales must be positive")
    finite(args.temperature_offset_c)
    from odbAccess import openOdb
    from abaqusConstants import TIME
    odb = openOdb(path=os.path.abspath(args.odb), readOnly=True)
    try:
        step = odb.steps[args.step]
        if step.domain != TIME:
            raise ValueError("Only physical time-domain results can be replayed")
        region = odb.rootAssembly.instances[args.instance]
        frames, skipped = export_frames(step, region, fields, args.stress_to_mpa,
                                        args.displacement_to_mm, args.temperature_offset_c)
        result = {
            "schemaVersion": 1, "source": "Abaqus ODB",
            "deviceId": args.device, "faultCode": args.fault,
            "job": os.path.splitext(os.path.basename(args.odb))[0],
            "step": args.step, "region": args.instance,
            "analysisDescription": args.analysis_description,
            "timeMeaning": "physical",
            "exportedAt": datetime.now(timezone.utc).isoformat(),
            "timeUnit": "s", "stressUnit": "MPa",
            "displacementUnit": "mm", "temperatureUnit": "C",
            "hasStress": "S" in fields, "hasDisplacement": "U" in fields,
            "hasTemperature": "NT11" in fields or "TEMP" in fields,
            "frames": frames,
        }
    finally:
        odb.close()
    digest = hashlib.sha256()
    with open(args.odb, "rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    result["odbSha256"] = digest.hexdigest()
    encoded = json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False).encode("utf-8")
    if len(encoded) > 8 * 1024 * 1024:
        raise ValueError("Result exceeds Unity's 8 MiB import limit")
    # Exclusive creation preserves previous engineering results.
    with open(args.output, "xb") as stream:
        stream.write(encoded)
    print("Exported {} frames; skipped {} frames missing requested fields.".format(len(frames), skipped))
    print("Independent instance envelopes only; no field mesh or spatial registration exported.")


if __name__ == "__main__":
    main()
