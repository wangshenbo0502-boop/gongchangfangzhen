"""Download the original public Unity CDN package, with verified parallel ranges."""
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import time
import urllib.request

ROOT = Path(__file__).resolve().parent.parent
DEST = ROOT / "Downloads/FactoryTraining-1.0-Unity6.3.unitypackage"
PREFIX = Path(str(DEST) + ".partial")
STATE = Path(str(DEST) + ".ranges.json")
MANIFEST = json.loads((ROOT / "Evidence/asset-service-0.json").read_text(encoding="utf-8-sig"))
UPLOAD = MANIFEST["uploads"]["6000.3.0f1"]
URL = "https://assetstorev1-prd-cdn.unity3d.com/" + UPLOAD["downloadS3key"]
EXPECTED = int(UPLOAD["downloadSize"])

with urllib.request.urlopen(urllib.request.Request(URL, method="HEAD"), timeout=30) as response:
    if int(response.headers["Content-Length"]) != EXPECTED:
        raise RuntimeError("The original CDN size does not match Unity's manifest")
    ETAG = response.headers["ETag"].strip('"')

DEST.parent.mkdir(exist_ok=True)
if DEST.exists():
    if DEST.stat().st_size != EXPECTED:
        raise RuntimeError("Existing package has wrong size; preserved")
else:
    if STATE.exists():
        state = json.loads(STATE.read_text())
        if state["expected"] != EXPECTED or state["etag"] != ETAG:
            raise RuntimeError("The CDN source changed; previous download was preserved")
    else:
        prefix_size = PREFIX.stat().st_size if PREFIX.exists() else 0
        span = (EXPECTED - prefix_size + 7) // 8
        state = {
            "expected": EXPECTED, "etag": ETAG, "prefix": prefix_size,
            "ranges": [[s, min(s + span, EXPECTED) - 1]
                       for s in range(prefix_size, EXPECTED, span)] if span else []
        }
        STATE.write_text(json.dumps(state, indent=2))

    def chunk_path(index):
        return Path(str(DEST) + f".chunk-{index}")

    def fetch_range(index, bounds):
        start, end = bounds
        output = chunk_path(index)
        attempt = 0
        while attempt < 5:
            existing = output.stat().st_size if output.exists() else 0
            if existing == end - start + 1:
                return
            if existing > end - start + 1:
                raise RuntimeError(f"Chunk {index} is too large; preserved")
            request_end = min(end, start + existing + 8 * 1024 * 1024 - 1)
            request = urllib.request.Request(URL, headers={
                "Range": f"bytes={start + existing}-{request_end}", "If-Range": f'"{ETAG}"'
            })
            try:
                with urllib.request.urlopen(request, timeout=40) as response:
                    expected_range = f"bytes {start + existing}-{request_end}/{EXPECTED}"
                    if response.status != 206 or response.headers.get("Content-Range") != expected_range:
                        raise RuntimeError(f"Server did not honor chunk {index}'s exact range")
                    with output.open("ab") as handle:
                        while data := response.read(1024 * 1024):
                            handle.write(data)
                if output.stat().st_size == end - start + 1:
                    return
                attempt = 0
            except Exception:
                attempt += 1
                if attempt == 5:
                    raise
                time.sleep(2)
        raise RuntimeError(f"Chunk {index} could not be completed")

    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
        futures = [executor.submit(fetch_range, i, bounds)
                   for i, bounds in enumerate(state["ranges"])]
        last = 0
        while not all(f.done() for f in futures):
            for future in futures:
                if future.done():
                    future.result()
            downloaded = state["prefix"] + sum(
                chunk_path(i).stat().st_size if chunk_path(i).exists() else 0
                for i in range(len(futures)))
            if time.monotonic() - last > 10:
                print(f"Official package: {downloaded / 1e9:.2f} / {EXPECTED / 1e9:.2f} GB "
                      f"({downloaded / EXPECTED:.1%})", flush=True)
                last = time.monotonic()
            time.sleep(1)
        for future in futures:
            future.result()

    assembling = Path(str(DEST) + ".assembling")
    with assembling.open("wb") as output:
        sources = ([PREFIX] if state["prefix"] else []) + [
            chunk_path(i) for i in range(len(state["ranges"]))]
        for source in sources:
            with source.open("rb") as handle:
                while data := handle.read(4 * 1024 * 1024):
                    output.write(data)
    if assembling.stat().st_size != EXPECTED:
        raise RuntimeError("Reassembled size mismatch")
    os.replace(assembling, DEST)

md5 = hashlib.md5()
sha256 = hashlib.sha256()
with DEST.open("rb") as handle:
    while data := handle.read(4 * 1024 * 1024):
        md5.update(data)
        sha256.update(data)
if md5.hexdigest() != ETAG:
    raise RuntimeError("Original CDN ETag/MD5 verification failed")
provenance = {
    "asset": "Factory Training | Industry Sample", "assetStoreId": 344832,
    "version": "1.0", "unityVersion": "6000.3.0f1", "source": URL,
    "bytes": EXPECTED, "md5": md5.hexdigest(), "sha256": sha256.hexdigest(),
    "license": "Unity Companion License",
    "licenseUrl": "https://unity.com/legal/licenses/unity-companion-license"
}
(DEST.parent / "FactoryTraining-source.json").write_text(
    json.dumps(provenance, indent=2), encoding="utf-8")
print("Original package downloaded; exact size and CDN MD5 verified.", flush=True)
