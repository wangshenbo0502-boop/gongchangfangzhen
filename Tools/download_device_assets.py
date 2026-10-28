"""Acquire existing CC0 equipment models and keep their upstream provenance."""
import hashlib
import json
from pathlib import Path
import sys
import urllib.request

ROOT = Path(__file__).resolve().parent.parent
TARGET = ROOT / "Assets/WorkshopDemo/External/Grab3D"
MODELS = {
    "water-pump": "https://grab3d.com/models/tools/water-pump/",
    "industrial-fan": "https://grab3d.com/models/electronics/industrial-fan/",
    "silo": "https://grab3d.com/models/buildings/silo/",
    "electrical-cabinet": "https://grab3d.com/models/electronics/electrical-cabinet/",
    "conveyor-belt": "https://grab3d.com/models/tools/conveyor-belt/",
    "pressure-tank": "https://grab3d.com/models/tools/pressure-tank/",
}

for slug, page in MODELS.items():
    if len(sys.argv) > 1 and slug not in sys.argv[1:]:
        continue
    folder = TARGET / slug
    folder.mkdir(parents=True, exist_ok=True)
    base = "https://grab3d.com/dl/" + slug + "/"
    with urllib.request.urlopen(base + "manifest.json", timeout=60) as response:
        raw_manifest = response.read()
    manifest = json.loads(raw_manifest)
    if manifest["extras"]["license"] != "CC0-1.0":
        raise RuntimeError(f"Unexpected license for {slug}")
    model = manifest["files"]["fbx"]
    filename = model["name"]
    if Path(filename).name != filename or not filename.endswith(".fbx"):
        raise RuntimeError("Unexpected model filename")
    dest = folder / filename
    if not dest.exists() or dest.stat().st_size != model["bytes"]:
        temporary = dest.with_suffix(".fbx.download")
        urllib.request.urlretrieve(base + filename, temporary)
        if temporary.stat().st_size != model["bytes"]:
            raise RuntimeError("Upstream file size mismatch")
        if not temporary.read_bytes().startswith(b"Kaydara FBX Binary"):
            raise RuntimeError("Download is not a binary FBX")
        temporary.replace(dest)
    (folder / "manifest.json").write_bytes(raw_manifest)
    urllib.request.urlretrieve(base + "LICENSE.txt", folder / "LICENSE.txt")
    source = {
        "page": page, "file": base + filename,
        "sha256": hashlib.sha256(dest.read_bytes()).hexdigest(),
        "bytes": dest.stat().st_size, "license": "CC0-1.0",
        "role": "Candidate existing model; inspect in Unity before use",
    }
    (folder / "source.json").write_text(json.dumps(source, indent=2), encoding="utf-8")
    print(f"Downloaded existing CC0 model: {slug} ({dest.stat().st_size:,} bytes)", flush=True)
