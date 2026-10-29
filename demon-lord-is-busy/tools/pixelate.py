"""Turn AI-generated sprite images into real game pixel art.

    python3 tools/pixelate.py            # art/raw/*.png -> assets/sprites/*.png + art/preview.png
    python3 tools/pixelate.py IN OUT     # other folders (preview goes to OUT/preview.png)

Per image: remove a solid background (flood fill from the corners) unless it is already
transparent, crop to the subject, reduce to a small palette, downscale by majority color,
and add a 1px dark outline (not on floor/wall tiles). `<name>_walk.png` sprite sheets
(4 frames in one row) are split into frames and saved as one horizontal strip. Then run
`godot --headless --path . --import` so the game can load the new PNGs.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
RAW = ROOT / "art" / "raw"
OUT = ROOT / "assets" / "sprites"
PREVIEW = ROOT / "art" / "preview.png"

# Target heights in game pixels (16 = one grid tile). Characters stand a bit taller than a tile.
SIZES = {"boss_hero": 40, "core": 40, "torch": 14}
DEFAULT_SIZE = 24
TILE_SIZE = 16
TILES = {"floor", "wall"}              # full-square tiles: no background removal, crop or outline
TILE_OBJECTS = {"spike", "fire_trap"}  # drawn on one grid tile
SPRITE_COLORS = 20                     # per sprite, keeps each sprite's own hues
BG_TOLERANCE = 48                      # how far from the corner color still counts as background
SENTINEL = (1, 254, 3)
OUTLINE = (18, 12, 22, 255)
ANIM_SUFFIX = "_walk"
FRAMES = 4


def remove_background(im: Image.Image) -> Image.Image:
    if im.getextrema()[3][0] < 250:  # already has transparent pixels
        return im
    rgb = im.convert("RGB")
    w, h = rgb.size
    for corner in [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]:
        if rgb.getpixel(corner) != SENTINEL:
            ImageDraw.floodfill(rgb, corner, SENTINEL, thresh=BG_TOLERANCE)
    out = im.copy()
    a = np.array(out.getchannel("A"))
    a[(np.array(rgb) == SENTINEL).all(axis=2)] = 0
    out.putalpha(Image.fromarray(a))
    return out


def to_square(im: Image.Image) -> Image.Image:
    bbox = im.getchannel("A").point(lambda a: 255 if a > 32 else 0).getbbox()
    if bbox:
        im = im.crop(bbox)
    side = max(im.size)
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(im, ((side - im.width) // 2, side - im.height))  # feet on the bottom edge
    return sq


def shrink(im: Image.Image, size: int, outline: bool) -> Image.Image:
    """Majority-color downscale: each output pixel takes the most common palette color of its
    block, so colors stay pure instead of averaging into mud."""
    q = im.convert("RGB").quantize(colors=SPRITE_COLORS, method=Image.Quantize.MEDIANCUT)
    pal = q.getpalette()[: SPRITE_COLORS * 3]
    idx = np.array(q)
    alpha = np.array(im.getchannel("A")) > 128
    h, w = idx.shape
    out = np.zeros((size, size, 4), dtype=np.uint8)
    for oy in range(size):
        for ox in range(size):
            y0, y1 = oy * h // size, (oy + 1) * h // size
            x0, x1 = ox * w // size, (ox + 1) * w // size
            a = alpha[y0:y1, x0:x1]
            if a.mean() < 0.4:
                continue
            c = np.bincount(idx[y0:y1, x0:x1][a]).argmax()
            out[oy, ox] = [pal[c * 3], pal[c * 3 + 1], pal[c * 3 + 2], 255]
    if outline:
        solid = out[:, :, 3] == 255
        p = np.pad(solid, 1)
        near = p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
        out[near & ~solid] = OUTLINE
    return Image.fromarray(out, "RGBA")


def split_frames(im: Image.Image, n: int = FRAMES) -> list[Image.Image]:
    """Cut a one-row sprite sheet into n frames: by transparent column gaps when they give
    exactly n pieces, otherwise into n equal slices."""
    cols = (np.array(im.getchannel("A")) > 32).any(axis=0)
    segs, start = [], None
    for x, v in enumerate(cols):
        if v and start is None:
            start = x
        elif not v and start is not None:
            segs.append((start, x))
            start = None
    if start is not None:
        segs.append((start, len(cols)))
    segs = [g for g in segs if g[1] - g[0] > im.width * 0.03]
    if len(segs) != n:
        w = im.width // n
        segs = [(i * w, (i + 1) * w) for i in range(n)]
    return [im.crop((x0, 0, x1, im.height)) for x0, x1 in segs]


def make_strip(im: Image.Image, size: int) -> Image.Image:
    """Frames share one scale (largest frame fits) and stand on the same bottom edge."""
    frames = []
    for f in split_frames(im):
        bbox = f.getchannel("A").point(lambda a: 255 if a > 32 else 0).getbbox()
        frames.append(f.crop(bbox) if bbox else f)
    side = max(max(f.size) for f in frames)
    strip = Image.new("RGBA", (size * len(frames), size), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        sq.paste(f, ((side - f.width) // 2, side - f.height))
        strip.paste(shrink(sq, size, outline=True), (i * size, 0))
    return strip


def main() -> None:
    global RAW, OUT, PREVIEW
    if len(sys.argv) == 3:
        RAW, OUT = Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve()
        PREVIEW = OUT / "preview.png"
    files = sorted(RAW.glob("*.png"))
    if not files:
        raise SystemExit(f"no images in {RAW}")
    sprites = {}
    for f in files:
        im = Image.open(f).convert("RGBA")
        name = f.stem
        if name.endswith(ANIM_SUFFIX):
            base = name[: -len(ANIM_SUFFIX)]
            sprites[name] = make_strip(remove_background(im), SIZES.get(base, DEFAULT_SIZE))
            continue
        if name not in TILES:
            im = to_square(remove_background(im))
        if name in TILES:
            size = TILE_SIZE * 4  # one texture spans 4x4 grid cells, so stones keep a sane size
        elif name in TILE_OBJECTS:
            size = TILE_SIZE
        else:
            size = SIZES.get(name, DEFAULT_SIZE)
        sprites[name] = shrink(im, size, outline=name not in TILES)
    OUT.mkdir(parents=True, exist_ok=True)
    for name, im in sprites.items():
        im.save(OUT / f"{name}.png")
    # Preview sheet at 8x so a human can judge it.
    scale = 8
    cell = 64 * scale
    widths = [max(cell, im.width * scale) for im in sprites.values()]
    sheet = Image.new("RGBA", (sum(widths), cell), (40, 38, 48, 255))
    x = 0
    for w, im in zip(widths, sprites.values()):
        big = im.resize((im.width * scale, im.height * scale), Image.Resampling.NEAREST)
        sheet.alpha_composite(big, (x + (w - big.width) // 2, cell - big.height))
        x += w
    sheet.save(PREVIEW)
    print(f"{len(sprites)} sprites -> {OUT}, preview -> {PREVIEW}")


if __name__ == "__main__":
    main()
