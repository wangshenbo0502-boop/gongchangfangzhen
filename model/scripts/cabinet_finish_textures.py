"""Author physically scaled cabinet surface maps, separate from earlier reviews."""
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

OUT = Path(__file__).resolve().parents[1] / "textures/electrical-cabinet/finish-review"


def noise(rng, size, sigma):
    field = gaussian_filter(rng.standard_normal((size, size)).astype("f"), sigma,
                            mode="wrap")
    return field / max(float(field.std()), 1e-6)


def write_set(name, size, tile, metallic, relief, seed):
    rng = np.random.default_rng(seed)
    broad = noise(rng, size, size / 12)
    grain = noise(rng, size, 2.6)
    fine = noise(rng, size, .65)
    if name == "powder":
        height = .5 + .115 * grain + .026 * fine
        base = .96 + .003 * grain + .004 * broad
        rough = .43 + .038 * grain + .013 * broad
    elif name == "zinc":
        # Fine, low-contrast electro-galvanized texture for formed sheet metal.
        cells = 48
        yy, xx = np.mgrid[:size, :size].astype("f") * cells / size
        ix, iy = np.floor(xx).astype(int), np.floor(yy).astype(int)
        jitter = rng.uniform(.15, .85, (cells, cells, 2)).astype("f")
        tone = rng.uniform(-1, 1, (cells, cells)).astype("f")
        best = np.full((size, size), np.inf, dtype="f")
        facets = np.zeros_like(best)
        slope = np.zeros_like(best)
        for oy in (-1, 0, 1):
            for ox in (-1, 0, 1):
                cx, cy = ix + ox, iy + oy
                center = jitter[cy % cells, cx % cells]
                dx, dy = xx - cx - center[..., 0], yy - cy - center[..., 1]
                distance = dx * dx + dy * dy
                mask = distance < best
                best[mask] = distance[mask]
                values = tone[cy % cells, cx % cells]
                facets[mask] = values[mask]
                slope[mask] = (dx * values + dy * .35)[mask]
        facets = gaussian_filter(facets, .85, mode="wrap")
        height = .5 + .035 * slope + .008 * fine
        base = .96 + .007 * facets + .002 * broad
        rough = .38 + .022 * facets + .009 * broad + .004 * fine
    elif name == "brushed":
        bands = gaussian_filter(rng.standard_normal(size).astype("f"), 1.1,
                                mode="wrap")
        bands /= bands.std()
        streak = np.broadcast_to(bands[:, None], (size, size))
        height = .5 + .07 * streak + .008 * fine
        base = .96 + .009 * streak + .003 * broad
        rough = .27 + .039 * streak + .016 * broad
    elif name == "rubber":
        height = .5 + .10 * grain + .04 * fine
        base = .96 + .008 * broad
        rough = .86 + .045 * grain + .025 * broad
    elif name == "pvc":
        height = .5 + .035 * grain + .012 * fine
        base = .97 + .003 * broad
        rough = .37 + .022 * grain + .022 * broad
    else:
        height = .5 + .075 * grain + .025 * fine
        base = .97 + .004 * broad
        rough = .49 + .04 * grain + .022 * broad
    dy, dx = np.gradient(height * relief, tile / size)
    normal = np.stack((-dx, -dy, np.ones_like(dx)), axis=-1)
    normal /= np.linalg.norm(normal, axis=-1)[..., None]
    channels = {
        "BaseColor": np.repeat(base[..., None], 3, axis=-1),
        "Normal": normal * .5 + .5,
        "Roughness": rough,
        "Metallic": np.full((size, size), metallic, dtype="f"),
        "Height": height,
    }
    stats = {}
    for channel, field in channels.items():
        pixels = np.uint8(np.clip(field, 0, 1) * 255 + .5)
        Image.fromarray(pixels).save(OUT / f"{name}_{channel}.png")
        stats[channel] = {"min": int(pixels.min()), "max": int(pixels.max()),
                          "standard_deviation": float(pixels.std())}
    return {"size": size, "tile_m": tile, "height_range_m": relief,
            "metallic": metallic, "statistics": stats}


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = {}
    for index, spec in enumerate((
        ("powder", 2048, .12, 0, .000080),
        ("zinc", 1024, .10, 1, .000010),
        ("brushed", 2048, .12, 1, .000012),
        ("polymer", 1024, .07, 0, .000035),
        ("rubber", 512, .04, 0, .000060),
        ("pvc", 512, .05, 0, .000020),
    )):
        manifest[spec[0]] = write_set(*spec, 64021 + index)
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print("CABINET_FINISH_TEXTURES", OUT, flush=True)
