"""Original, physically scaled injection-moulded finishes and small device labels."""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import gaussian_filter

OUT = Path(__file__).resolve().parents[1] / "textures/electrical-cabinet/housing-review"


def font(size, bold=False):
    return ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf" if bold else
                              "C:/Windows/Fonts/arial.ttf", size)


def maps():
    rng = np.random.default_rng(20261112)
    size, tile = 1024, .045
    def field(sigma):
        a = gaussian_filter(rng.normal(size=(size, size)).astype("f"), sigma, mode="wrap")
        return a / a.std()
    fine, grain, broad = field(.65), field(2.0), field(53)
    height = .5 + .065 * grain + .018 * fine
    dy, dx = np.gradient(height * .000018, tile / size)
    normal = np.stack((-dx, -dy, np.ones_like(dx)), axis=-1)
    normal /= np.linalg.norm(normal, axis=-1)[..., None]
    channels = {
        "BaseColor": np.repeat((.97 + .0015 * broad + .002 * grain)[..., None], 3, axis=-1),
        "Roughness": .54 + .026 * grain + .008 * broad,
        "Normal": normal * .5 + .5,
        "Height": height,
        "Metallic": np.zeros_like(height),
    }
    for name, a in channels.items():
        Image.fromarray(np.uint8(np.clip(a, 0, 1) * 255 + .5)).save(OUT / ("mould_"+name+".png"))
    return {"size": size, "tile_m": tile, "relief_m": .000018,
            "broad_color_variation": .0015, "seed": 20261112}


def label(name, size, background, rows):
    im = Image.new("RGB", size, background)
    d = ImageDraw.Draw(im)
    for text, x, y, h, bold, color in rows:
        d.text((x, y), text, font=font(h, bold), fill=color)
    im.save(OUT / (name+".png"))


def labels():
    white, dark = (222, 226, 221), (39, 44, 43)
    label("vfd_rating", (1024, 512), (187, 192, 187), [
        ("AC DRIVE     400 V / 3~", 30, 18, 56, True, dark),
        ("7.5 kW    18.0 A    IP20", 30, 94, 42, False, dark),
        ("INPUT   R/L1   S/L2   T/L3", 30, 178, 37, False, dark),
        ("OUTPUT   U/T1   V/T2   W/T3", 30, 234, 37, False, dark),
        ("STO  24 V DC     PE", 30, 292, 37, False, dark),
        ("ISOLATE SUPPLY / WAIT 5 MIN", 30, 389, 37, True, dark),
        ("S/N  261104-075     50/60 Hz", 30, 452, 28, False, dark),
    ])
    label("contactor", (768, 256), (180, 187, 182), [
        ("KM / 3 POLE", 20, 13, 51, True, dark),
        ("AC-3   18 A    400 V", 20, 90, 36, False, dark),
        ("24 V DC COIL     A1+ / A2-", 20, 153, 29, False, dark),
        ("IEC 60947-4-1", 20, 210, 24, False, dark),
    ])
    label("overload", (768, 256), (166, 173, 169), [
        ("THERMAL OVERLOAD", 20, 15, 42, True, dark),
        ("12 ... 18 A", 25, 91, 35, False, dark),
        ("TEST      RESET", 390, 106, 33, True, dark),
        ("95  96 NC       97  98 NO", 20, 209, 25, False, dark),
    ])
    label("safety", (512, 1024), (61, 67, 63), [
        ("SAFETY", 30, 25, 67, True, white),
        ("24 V DC", 30, 126, 42, False, white),
        ("E-STOP / GUARD", 30, 204, 35, False, white),
        ("POWER", 30, 357, 30, False, white),
        ("CH 1", 30, 424, 30, False, white),
        ("CH 2", 30, 491, 30, False, white),
        ("K1 / K2", 30, 558, 30, False, white),
        ("13-14  23-24", 30, 735, 33, False, white),
        ("33-34  41-42", 30, 793, 33, False, white),
        ("S11-S12  S21-S22", 30, 901, 27, False, white),
    ])
    label("vfd_keypad", (768, 256), (44, 49, 47), [
        ("MODE", 25, 162, 33, False, white),
        ("ESC", 210, 162, 33, False, white),
        ("RUN", 431, 162, 33, True, white),
        ("STOP", 601, 162, 33, True, white),
    ])


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = maps()
    labels()
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print("HOUSING_TEXTURES", OUT)
