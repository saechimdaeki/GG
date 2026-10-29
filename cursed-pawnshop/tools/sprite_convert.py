"""Turn AI-generated art on a magenta background into game sprites.

    python3 tools/sprite_convert.py IN_DIR OUT_DIR HEIGHT
    python3 tools/sprite_convert.py IN_DIR OUT_DIR scene   # backgrounds: no keying, cover 640x360

Per image: every magenta-ish pixel becomes transparent (also enclosed gaps and
anti-aliased fringe, which a corner flood fill misses), crop to the subject, shrink
to HEIGHT px tall by area average (keeps the source colors instead of collapsing
them to a tiny palette), then make alpha hard-edged. Writes OUT_DIR/preview.png.
Run `godot --headless --path . --import` afterwards.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ALPHA_CUT = 110  # after shrinking, alpha below this is dropped


def key_magenta(im: Image.Image) -> Image.Image:
    a = np.array(im.convert("RGBA")).astype(np.int32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    # ponytail: hue test, not distance to one color; Grok backgrounds are gradients.
    magenta = (r > 110) & (b > 80) & (g < np.minimum(r, b) * 0.6) & (np.abs(r - b) < 120)
    a[..., 3][magenta] = 0
    return Image.fromarray(a.astype(np.uint8))


def shrink(im: Image.Image, height: int) -> Image.Image:
    im = im.crop(im.getbbox())
    w = max(1, round(im.width * height / im.height))
    # premultiply so transparent pixels don't bleed magenta into the edge colors
    a = np.array(im).astype(np.float32)
    a[..., :3] *= a[..., 3:4] / 255.0
    small = np.array(Image.fromarray(a.astype(np.uint8)).resize((w, height), Image.BOX)).astype(np.float32)
    alpha = small[..., 3:4]
    small[..., :3] = np.where(alpha > 0, small[..., :3] * 255.0 / np.maximum(alpha, 1), 0)
    small[..., 3] = np.where(small[..., 3] >= ALPHA_CUT, 255, 0)
    return Image.fromarray(np.clip(small, 0, 255).astype(np.uint8))


def cover(im: Image.Image, w: int = 640, h: int = 360) -> Image.Image:
    """Scale to cover w x h and center-crop: full-screen scenes."""
    im = im.convert("RGB")
    k = max(w / im.width, h / im.height)
    im = im.resize((round(im.width * k), round(im.height * k)), Image.BOX)
    x, y = (im.width - w) // 2, (im.height - h) // 2
    return im.crop((x, y, x + w, y + h))


def main() -> None:
    src, out = Path(sys.argv[1]), Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    if sys.argv[3] == "scene":
        for f in sorted(src.glob("*.png")):
            if f.stem != "shop_bg":  # the shop wall has its own crop in the game README
                cover(Image.open(f)).save(out / f.name)
        print(f"scenes -> {out}")
        return
    height = int(sys.argv[3])
    sprites = []
    for f in sorted(src.glob("*.png")):
        sprite = shrink(key_magenta(Image.open(f)), height)
        sprite.save(out / f.name)
        sprites.append(sprite)
    if not sprites:
        raise SystemExit(f"no images in {src}")
    scale = 3
    sheet = Image.new("RGBA", (sum(s.width + 4 for s in sprites) * scale, height * scale), (40, 36, 48, 255))
    x = 0
    for s in sprites:
        big = s.resize((s.width * scale, s.height * scale), Image.NEAREST)
        sheet.paste(big, (x, 0), big)
        x += (s.width + 4) * scale
    sheet.save(out / "preview.png")
    print(f"{len(sprites)} sprites -> {out}")


if __name__ == "__main__":
    main()
