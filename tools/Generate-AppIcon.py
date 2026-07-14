#!/usr/bin/env python3
"""Generate deterministic Windows icon assets from the approved source image."""

from __future__ import annotations

import argparse
import shutil
from pathlib import Path

from PIL import Image, ImageFilter


PNG_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256, 512)
ICO_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    args.output.mkdir(parents=True, exist_ok=True)
    png_dir = args.output / "Icons"
    png_dir.mkdir(parents=True, exist_ok=True)

    shutil.copy2(args.source, args.output / "OfficialIconSource.png")
    with Image.open(args.source) as source:
        source.load()
        rgb = source.convert("RGB")
        square = min(rgb.width, rgb.height)

        # The approved portrait is almost square. Crop only the lower surplus,
        # keeping the face, hair, and gesture intact without stretching pixels.
        left = (rgb.width - square) // 2
        top = 0
        cropped = rgb.crop((left, top, left + square, top + square))
        master = cropped.resize((1024, 1024), Image.Resampling.LANCZOS)
        master.save(args.output / "IconMaster.png", format="PNG", optimize=True)

        for size in PNG_SIZES:
            icon = master.resize((size, size), Image.Resampling.LANCZOS)
            if size <= 48:
                icon = icon.filter(ImageFilter.UnsharpMask(radius=0.55, percent=115, threshold=2))
            icon.save(png_dir / f"app-icon-{size}.png", format="PNG", optimize=True)

        master.save(
            args.output / "AppIcon.ico",
            format="ICO",
            sizes=[(size, size) for size in ICO_SIZES],
            bitmap_format="png",
        )


if __name__ == "__main__":
    main()
