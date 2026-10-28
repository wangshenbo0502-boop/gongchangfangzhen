"""Preserve Unity's package GUIDs while isolating its assets from project settings."""
import copy
import io
import json
from pathlib import Path
import tarfile
import time

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Downloads/FactoryTraining-1.0-Unity6.3.unitypackage"
DEST = ROOT / "Downloads/FactoryTraining-assets-only.unitypackage"
ASSET_ROOT = "Assets/WorkshopDemo/External/FactoryTraining"
paths = {}
members = {}
print("Indexing the verified original package...", flush=True)
with tarfile.open(SOURCE, "r|gz") as archive:
    for member in archive:
        parts = member.name.split("/")
        if len(parts) != 2:
            continue
        guid, name = parts
        members.setdefault(guid, []).append(name)
        if name == "pathname":
            paths[guid] = archive.extractfile(member).read().decode("utf-8-sig").splitlines()[0]

selected = {
    guid: ASSET_ROOT + path[len("Assets"):]
    for guid, path in paths.items()
    if path == "Assets" or path.startswith("Assets/")
}
excluded = [path for guid, path in paths.items() if guid not in selected]
inventory = {
    "source": str(SOURCE.name), "destination": str(DEST.name),
    "assetRoot": ASSET_ROOT, "assets": list(selected.values()),
    "excluded": excluded, "assetCount": len(selected),
}
(ROOT / "Evidence/factory-package-inventory.json").write_text(
    json.dumps(inventory, indent=2, ensure_ascii=False), encoding="utf-8")
print(f"Keeping {len(selected)} asset GUIDs; excluding {len(excluded)} settings entries.", flush=True)
temporary = Path(str(DEST) + ".partial")
count = 0
last = time.monotonic()
with tarfile.open(SOURCE, "r|gz") as source, tarfile.open(
        temporary, "w|gz", compresslevel=1) as target:
    for member in source:
        parts = member.name.split("/")
        if parts[0] not in selected:
            continue
        if member.isdir():
            target.addfile(copy.copy(member))
            continue
        if not member.isfile() or len(parts) != 2:
            raise RuntimeError(f"Unexpected archive entry: {member.name}")
        info = copy.copy(member)
        data = source.extractfile(member)
        if parts[1] == "pathname":
            payload = selected[parts[0]].encode("utf-8")
            info.size = len(payload)
            data = io.BytesIO(payload)
            count += 1
        target.addfile(info, data)
        if time.monotonic() - last > 10:
            print(f"Preparing isolated assets: {count}/{len(selected)}", flush=True)
            last = time.monotonic()
temporary.replace(DEST)
print(f"Resource-only package ready: {DEST.name}", flush=True)
