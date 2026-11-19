"""Draws the 16x16 pixel icons that 0x72 DungeonTileset II has no art for (new relics and symbols).

    python3 tools/make_item_icons.py

Each icon is a list of rows; one character per pixel, '.' is transparent. Output: Assets/Resources/Art/<name>.png
"""
import os

from PIL import Image

ART = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art")

PALETTE = {
    "k": (20, 16, 24), "w": (240, 236, 224), "g": (150, 150, 160), "s": (200, 204, 214),
    "y": (240, 200, 70), "o": (190, 130, 40), "r": (200, 50, 50), "R": (130, 20, 30),
    "b": (80, 130, 220), "p": (160, 90, 210), "n": (120, 80, 40),
}

ICONS = {
    # 도박꾼의 주사위
    "dice": [
        "................",
        "..kkkkkkkkkkkk..",
        ".kwwwwwwwwwwwwk.",
        ".kwrrwwwwwwrrwk.",
        ".kwrrwwwwwwrrwk.",
        ".kwwwwwwwwwwwwk.",
        ".kwwwwwrrwwwwwk.",
        ".kwwwwwrrwwwwwk.",
        ".kwwwwwwwwwwwwk.",
        ".kwrrwwwwwwrrwk.",
        ".kwrrwwwwwwrrwk.",
        ".kwwwwwwwwwwwwk.",
        ".kgwwwwwwwwwwgk.",
        "..kkkkkkkkkkkk..",
        "................",
        "................",
    ],
    # 해골 왕관
    "crown": [
        "................",
        "................",
        ".k....k..k....k.",
        ".ky..kyk.kyk.yk.",
        ".kyykyyykyyykyk.",
        ".kyyyyyyyyyyyyk.",
        ".kyyryyyyyyryyk.",
        ".kyyyyyyyyyyyyk.",
        ".koooooooooooook",
        "..kkkkkkkkkkkkk.",
        "....kwwwwwwk....",
        "...kwkkwwkkwk...",
        "...kwkkwwkkwk...",
        "....kwwkkwwk....",
        ".....kwkwkwk....",
        "......kkkkk.....",
    ],
    # 콤보 메달
    "medal": [
        "....rr....bb....",
        "....rr....bb....",
        ".....rr..bb.....",
        "......rrbb......",
        ".......kk.......",
        ".....kkyykk.....",
        "....kyyyyyyk....",
        "...kyyoyyoyyk...",
        "...kyyyoyyyyk...",
        "...kyyoyoyyyk...",
        "...kyyyyyyyyk...",
        "....kyyyyyyk....",
        ".....kkyykk.....",
        ".......kk.......",
        "................",
        "................",
    ],
    # 피의 성배
    "goblet": [
        "................",
        "..kkkkkkkkkkkk..",
        "..kyRRRRRRRRyk..",
        "..kyRrrrrrrRyk..",
        "..kyyRrrrrRyyk..",
        "...kyyRRRRyyk...",
        "....kyyyyyyk....",
        ".....kyyyyk.....",
        "......kyyk......",
        "......kyok......",
        "......kyok......",
        ".....kyyyok.....",
        "....kyyyyyok....",
        "....kkkkkkkk....",
        "................",
        "................",
    ],
    # 쌍검 (심볼)
    "twin_swords": [
        "k..............k",
        "ks............sk",
        ".ks..........sk.",
        "..ks........sk..",
        "...ks......sk...",
        "....ks....sk....",
        ".....ks..sk.....",
        "......kssk......",
        "......kssk......",
        ".....ks..sk.....",
        "...nks....skn...",
        "...nnk....knn...",
        "..nn.n....n.nn..",
        ".yy..........yy.",
        ".yy..........yy.",
        "................",
    ],
}

for name, rows in ICONS.items():
    assert len(rows) == 16 and all(len(r) == 16 for r in rows), name
    im = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                im.putpixel((x, y), PALETTE[ch] + (255,))
    im.save(os.path.join(ART, name + ".png"))
    print("wrote", name)
