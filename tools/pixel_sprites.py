"""Hand-drawn pixel sprites as ASCII grids -> assets/sprites/<name>.png.

For small props the GPT pipeline (art/PROMPTS.md -> tools/pixelate.py) is overkill; these are
drawn pixel by pixel here. Edit a grid, then:
    python3 tools/pixel_sprites.py && godot --headless --path . --import
"""
import os

from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "sprites")

PALETTE = {
    ".": None,
    "K": (26, 16, 32), "R": (200, 58, 58), "r": (138, 32, 48), "P": (255, 122, 122),
    "Y": (255, 211, 74), "y": (184, 134, 42), "W": (255, 244, 224), "O": (255, 138, 42),
    "G": (138, 138, 150), "g": (85, 82, 94), "L": (196, 196, 208),
    "B": (122, 74, 38), "b": (78, 46, 24),
    "V": (184, 108, 255), "v": (106, 58, 168), "M": (232, 184, 255),
    "N": (94, 196, 106), "n": (47, 122, 60), "E": (240, 230, 192),
}

SPRITES = {
    "imp": """
................
...K........K...
..KRK......KRK..
..KRRKKKKKKRRK..
...KRRRRRRRRK...
..KRRWKRRKWRRK..
..KRRKKRRKKRRK..
..KRRRRRRRRRRK..
...KRRrrrrRRK...
.K..KRRRRRRK..K.
KrK.KRRRRRRK.KrK
.KrKRRRRRRRRKrK.
..KKRRPRRPRRKK..
....KRRKKRRK....
....KrK..KrK....
....KK....KK....
""",
    "chest": """
................
.....YWY..Y.....
....YYYYYYYY....
...KKKKKKKKKK...
..KBBBBBBBBBBK..
..KByyyyyyyyBK..
..KBBBBBBBBBBK..
..KKKKKYYKKKKK..
..KBBBBYYBBBBK..
..KBbBBKKBBbBK..
..KBbBBBBBBbBK..
..KBbBBBBBBbBK..
..KyyyyyyyyyyK..
..KKKKKKKKKKKK..
................
................
""",
    "mine": """
................
.......K........
......KMK....K..
......KVK...KMK.
.....KMVvK..KVK.
.....KVVvK.KVvK.
..K..KVVvKKKVvK.
.KgK.KVVvKgKVvK.
KgGgKKKKKKgGKKK.
KgGGgGGgGGGGgGK.
KgGLGGgGGLGGGgK.
KggGGGGggGGGgGK.
.KggggggggggggK.
..KKKKKKKKKKKK..
................
................
""",
    "nest": """
................
................
................
................
.....KKKKKK.....
...KKEEKKEEKK...
..KnEEEKKEEEnK..
.KnNKEEKKEEKNnK.
.KnNNKKNNKKNNnK.
KnNNNNNNNNNNNNnK
KnnNNNNNNNNNNnnK
.KnnnnNNNNnnnnK.
..KKnnnnnnnnKK..
....KKKKKKKK....
................
................
""",
    "altar": """
.......KK.......
......KVMK......
......KVMK......
.....KVMMVK.....
......KVVK......
..KKKKKKKKKKKK..
..KLLLLLLLLLLK..
..KGGGGGGGGGGK..
...KGgGMMGgGK...
...KGgGVVGgGK...
...KGgGGGGgGK...
...KGgGGGGgGK...
..KGGGGGGGGGGK..
..KggggggggggK..
..KKKKKKKKKKKK..
................
""",
    "gate_closed": """
gggggggggggggggg
.LgK.LgK.LgK.LgK
.LgK.LgK.LgK.LgK
.LgK.LgK.LgK.LgK
LLLLLLLLLLLLLLLL
gggggggggggggggg
.LgK.LgK.LgK.LgK
.LgK.LgK.LgK.LgK
.LgK.LgK.LgK.LgK
.LgK.LgK.LgK.LgK
LLLLLLLLLLLLLLLL
gggggggggggggggg
.LgK.LgK.LgK.LgK
.LgK.LgK.LgK.LgK
..L...L...L...L.
..K...K...K...K.
""",
    "gate_open": """
gggggggggggggggg
.LgK.LgK.LgK.LgK
LLLLLLLLLLLLLLLL
gggggggggggggggg
..L...L...L...L.
..K...K...K...K.
................
................
................
................
................
................
................
................
................
................
""",
    "crown": """
Y..YY..Y
YY.YY.YY
YYYRRYYY
yYYYYYYy
KyyyyyyK
""",
}


def main():
    for name, art in SPRITES.items():
        rows = [r for r in art.strip("\n").split("\n")]
        w, h = len(rows[0]), len(rows)
        assert all(len(r) == w for r in rows), name
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                col = PALETTE[ch]
                if col is not None:
                    img.putpixel((x, y), col + (255,))
        img.save(os.path.join(OUT, name + ".png"))
    print("wrote", len(SPRITES), "sprites")


if __name__ == "__main__":
    main()
