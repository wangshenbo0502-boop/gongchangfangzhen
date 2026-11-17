"""Arrange existing Player captures for visual review; does not alter source images."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageOps, ImageStat


def validate(folder):
    paths = sorted(folder.glob("*.png"))
    records = []
    for path in paths:
        with Image.open(path) as source:
            image = source.convert("RGB").resize((160, 120))
            stats = ImageStat.Stat(image)
            spread = max(stats.stddev)
            pixels = list(image.getdata())
            background = image.getpixel((0, 0))
            lit = sum(max(pixel) > 24 for pixel in pixels) / (160 * 120)
            foreground = sum(max(abs(a - b) for a, b in zip(pixel, background)) > 8
                             for pixel in pixels) / (160 * 120)
            records.append({"file": path.name, "stddev": round(spread, 3),
                            "lit_fraction": round(lit, 4), "foreground_fraction": round(foreground, 4),
                            "pass": spread > 2 and lit > .02 and foreground > .01})
    passed = len(records) == 170 and all(record["pass"] for record in records)
    (folder / "image-validation.json").write_text(
        json.dumps({"expected": 170, "actual": len(records), "pass": passed, "images": records}, indent=2),
        encoding="utf-8")
    if not passed:
        raise SystemExit("Missing or blank visual captures: " +
                         ", ".join(record["file"] for record in records if not record["pass"]))
    print(f"Validated {len(records)} rendered captures: nonblank PASS")


def sheet(paths, output, columns=4, tile=(360, 280)):
    rows = (len(paths) + columns - 1) // columns
    canvas = Image.new("RGB", (columns * tile[0], rows * tile[1]), "#25282b")
    draw = ImageDraw.Draw(canvas)
    for index, path in enumerate(paths):
        x, y = index % columns * tile[0], index // columns * tile[1]
        with Image.open(path) as source:
            image = ImageOps.contain(source.convert("RGB"), (tile[0], tile[1] - 25))
            canvas.paste(image, (x + (tile[0] - image.width) // 2, y + 25))
        draw.text((x + 8, y + 6), path.stem, fill="white")
    canvas.save(output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("folder", type=Path)
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args()
    validate(args.folder)
    if args.validate_only:
        raise SystemExit(0)
    for code in ["TK-LEAK", "P-SEAL", "P-CAV", "F-CRACK", "F-IMBAL", "PLC-CONTACT"]:
        paths = [
            args.folder / f"{code}-{angle}-{progress:.3f}.png"
            for angle in ["front", "side", "oblique", "focus"]
            for progress in [0, .1, .5, 1]
        ]
        sheet(paths, args.folder / f"sheet-{code}.jpg")
        sheet(sorted(args.folder.glob(f"{code}-milestone-*.png")),
              args.folder / f"sheet-{code}-milestones.jpg")
    sheet(sorted(args.folder.glob("*-mechanism.png")), args.folder / "sheet-mechanisms.jpg")
