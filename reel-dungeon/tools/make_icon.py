"""Builds Assets/Icon/icon.png (512x512) from the game's own pixel art: the knight in front of three slot reels.

    python3 tools/make_icon.py
"""
import os

from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(__file__), "..")
ART = os.path.join(ROOT, "Assets", "Resources", "Art")
OUT = os.path.join(ROOT, "Assets", "Icon", "icon.png")
S = 512


def sprite(name, scale):
    im = Image.open(os.path.join(ART, name + ".png")).convert("RGBA")
    return im.resize((im.width * scale, im.height * scale), Image.NEAREST)


icon = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(icon)
# rounded plate: dark outline, gold rim, dungeon-purple fill
d.rounded_rectangle([8, 8, S - 8, S - 8], radius=72, fill=(28, 20, 14, 255))
d.rounded_rectangle([20, 20, S - 20, S - 20], radius=62, fill=(232, 192, 96, 255))
d.rounded_rectangle([36, 36, S - 36, S - 36], radius=50, fill=(26, 20, 36, 255))

# three reels across the top
faces = ["0x72/weapon_knight_sword", "shield", "0x72/coin_anim_f0"]
colors = [(201, 76, 76), (76, 124, 201), (200, 168, 50)]
for k, (face, col) in enumerate(zip(faces, colors)):
    x0 = 70 + k * 130
    d.rectangle([x0, 70, x0 + 112, 200], fill=(232, 192, 96, 255))
    d.rectangle([x0 + 6, 76, x0 + 106, 194], fill=tuple(c // 2 for c in col) + (255,))
    sp = sprite(face, {"0x72/weapon_knight_sword": 3, "shield": 5}.get(face, 9))
    icon.alpha_composite(sp, (x0 + 56 - sp.width // 2, 135 - sp.height // 2))

# the knight, big, standing in front
knight = sprite("0x72/knight_m_idle_anim_f0", 10)
icon.alpha_composite(knight, (S // 2 - knight.width // 2, S - 50 - knight.height))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
icon.save(OUT)
print("wrote", OUT, icon.size)
