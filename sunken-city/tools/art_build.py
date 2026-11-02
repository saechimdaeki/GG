"""Turn the GPT/Gemini art in art/raw/ into game-size pixel art in assets/.

    python3 tools/art_build.py        # then: godot --headless --path . --import

Every file gets a box (max width, max height) in game pixels from SIZES below.
Sprites: magenta/transparent background removed (characters also lose stray
bubbles and sparkles), cropped to the subject, shrunk by
area average (keeps the colors), alpha made hard-edged. Tiles: shrunk to a square
texture the game repeats across the map. Scenes: cover-cropped to 640x360.
A file missing from SIZES is skipped with a warning; writes art/preview.png.
"""
from pathlib import Path

import numpy as np
from scipy import ndimage
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
RAW, OUT = ROOT / "art" / "raw", ROOT / "assets"
ALPHA_CUT = 110
TILE_TEX = 128  # one tile texture covers 8x8 map tiles

# Sizes are 2x the in-game size in tile pixels: the 3D view draws sprites at half a
# tile-pixel per texel, so they land ~1:1 on the 640x360 screen instead of blown up 2x.
SIZES = {
    # chibi characters read better a little bigger than their 12x14 hitbox
    "sprites/diver.png": (64, 60),
    "sprites/diver_stand.png": (52, 64),
    "sprites/eel_lantern.png": (68, 36),
    "sprites/crab_rubble.png": (52, 44),
    "sprites/jelly_ghost.png": (40, 48),
    "sprites/moru.png": (52, 64),
    "sprites/mulbangul.png": (48, 60),
    "portraits/moru.png": (64, 64),  # UI, not 3D: stays 1x
    "portraits/mulbangul.png": (64, 64),
    "props/air_bubble_plant.png": (36, 40),
    "props/gear.png": (22, 22),
    "props/pipe_part.png": (24, 22),
    "props/scrap_brass.png": (24, 20),
    "props/pump_broken.png": (68, 64),
    "props/pump_fixed.png": (68, 64),
    "props/floodgate.png": (64, 64),
    "props/building_workshop.png": (104, 80),
    "props/building_lodge.png": (120, 88),
    "props/building_tower.png": (60, 128),
}


def key_magenta(im: Image.Image) -> Image.Image:
    a = np.array(im.convert("RGBA")).astype(np.int32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    magenta = (r > 110) & (b > 80) & (g < np.minimum(r, b) * 0.6) & (np.abs(r - b) < 120)
    a[..., 3][magenta] = 0
    return Image.fromarray(a.astype(np.uint8))


def main_blob(im: Image.Image) -> Image.Image:
    """Characters only: drop small separate bits (bubbles, sparkles, specks) that GPT
    adds around the subject, so the crop centers on the body. Keeps any piece at least
    a fifth the size of the biggest one."""
    a = np.array(im)
    labels, n = ndimage.label(a[..., 3] > 0)
    if n <= 1:
        return im
    sizes = ndimage.sum(np.ones(labels.shape), labels, range(1, n + 1))
    keep = np.isin(labels, 1 + np.flatnonzero(sizes >= sizes.max() / 5))
    a[..., 3][~keep] = 0
    return Image.fromarray(a)


def fit(im: Image.Image, box: tuple[int, int]) -> Image.Image:
    im = im.crop(im.getbbox())
    k = min(box[0] / im.width, box[1] / im.height)
    w, h = max(1, round(im.width * k)), max(1, round(im.height * k))
    # premultiply so transparent pixels don't bleed into the edge colors
    a = np.array(im).astype(np.float32)
    a[..., :3] *= a[..., 3:4] / 255.0
    small = np.array(Image.fromarray(a.astype(np.uint8)).resize((w, h), Image.BOX)).astype(np.float32)
    alpha = small[..., 3:4]
    small[..., :3] = np.where(alpha > 0, small[..., :3] * 255.0 / np.maximum(alpha, 1), 0)
    small[..., 3] = np.where(small[..., 3] >= ALPHA_CUT, 255, 0)
    return Image.fromarray(np.clip(small, 0, 255).astype(np.uint8))


def cover(im: Image.Image, w: int = 640, h: int = 360) -> Image.Image:
    im = im.convert("RGB")
    k = max(w / im.width, h / im.height)
    im = im.resize((round(im.width * k), round(im.height * k)), Image.BOX)
    x, y = (im.width - w) // 2, (im.height - h) // 2
    return im.crop((x, y, x + w, y + h))


def main() -> None:
    made = []
    for f in sorted(RAW.glob("*/*.png")):
        rel = f"{f.parent.name}/{f.name}"
        dest = OUT / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        im = Image.open(f)
        if f.parent.name == "tiles":
            out = im.convert("RGB").resize((TILE_TEX, TILE_TEX), Image.BOX)
        elif f.parent.name == "scenes":
            out = cover(im)
        elif rel in SIZES:
            keyed = key_magenta(im)
            if f.parent.name in ("sprites", "portraits"):
                keyed = main_blob(keyed)
            out = fit(keyed, SIZES[rel])
        else:
            print(f"skip {rel}: add it to SIZES")
            continue
        out.save(dest)
        made.append(out.convert("RGBA"))
    small = [m for m in made if m.width <= 64]
    scale = 3
    sheet = Image.new("RGBA", (sum(s.width + 4 for s in small) * scale, 64 * scale), (40, 36, 48, 255))
    x = 0
    for s in small:
        big = s.resize((s.width * scale, s.height * scale), Image.NEAREST)
        sheet.paste(big, (x, 0), big)
        x += (s.width + 4) * scale
    sheet.save(ROOT / "art" / "preview.png")  # art/ is .gdignore-d, so not imported
    print(f"{len(made)} images -> {OUT}")


if __name__ == "__main__":
    main()
