"""Extract only verified sample assets, preserving existing project GUID ownership."""
import json
from pathlib import Path, PurePosixPath
import shutil
import tarfile
import time

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Downloads/FactoryTraining-assets-only.unitypackage"
PREFIX = "Assets/WorkshopDemo/External/FactoryTraining"
owners = {}
for meta in (ROOT / "Assets").rglob("*.meta"):
    for line in meta.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        if line.startswith("guid: "):
            owners[line[6:].strip()] = str(meta.relative_to(ROOT))[:-5].replace("\\", "/")
            break
paths = {}
print("Indexing resource paths and existing GUID ownership...", flush=True)
with tarfile.open(SOURCE, "r|gz") as archive:
    for member in archive:
        if member.name.endswith("/pathname"):
            path = archive.extractfile(member).read().decode("utf-8-sig").splitlines()[0]
            if not (path == PREFIX or path.startswith(PREFIX + "/")) or ".." in PurePosixPath(path).parts:
                raise RuntimeError(f"Path outside the sample's destination: {path}")
            paths[member.name.split("/")[0]] = path
collisions = {guid: owners[guid] for guid in paths if guid in owners and owners[guid] != paths[guid]}
(ROOT / "Evidence/factory-guid-ownership.json").write_text(
    json.dumps({"existingAssetsPreserved": collisions}, indent=2), encoding="utf-8")
print(f"Preserving {len(collisions)} existing GUID owners.", flush=True)
count = 0
last = time.monotonic()
with tarfile.open(SOURCE, "r|gz") as archive:
    for member in archive:
        if not member.isfile():
            continue
        guid, entry = member.name.split("/")
        if guid in collisions or entry not in ("asset", "asset.meta"):
            continue
        dest = ROOT / (paths[guid] + (".meta" if entry == "asset.meta" else ""))
        dest.parent.mkdir(parents=True, exist_ok=True)
        if dest.exists():
            raise RuntimeError(f"Existing destination preserved: {dest}")
        with archive.extractfile(member) as data, dest.open("xb") as output:
            shutil.copyfileobj(data, output, 4 * 1024 * 1024)
        count += 1
        if time.monotonic() - last > 10:
            print(f"Extracted original asset/meta files: {count}", flush=True)
            last = time.monotonic()
print(f"Extraction complete: {count} original asset/meta files.", flush=True)
