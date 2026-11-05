"""Generate src/data/zone1.gd: a wide flooded basin with drowned city buildings.

    python3 tools/gen_zone1.py      # deterministic (fixed seed); prints the map

Layout (128 x 64 tiles): sky rows 0-5, sea surface at row 6, a dock at the top-left
with the start, a seafloor around row 40, hollow buildings standing on it (rooms,
windows, doors, and staggered inner floors you can climb once the water is gone),
the pump and three building plots on rooftops, and a hard-wall plug in the floor
leading down a shaft to a deep cavern (crushing pressure) with the sealed floodgate.
Legend matches Zone1's header comment.
"""
import random
from pathlib import Path

W, H = 128, 64
SURFACE_ROW, DRAINED_ROW, PRESSURE_ROW = 6, 36, 48
REFLOOD_ROW = 26  # the pump valve refills to here: below every rooftop, above the sea pillar
FLOOR = 40
random.seed(4)
g = [["." for _ in range(W)] for _ in range(H)]


def put(x, y, ch):
    if 0 <= x < W and 0 <= y < H:
        g[y][x] = ch


def rect(x0, y0, x1, y1, ch):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(x, y, ch)


# basin: side walls, bottom, wavy seafloor
rect(0, 0, 1, H - 1, "#")
rect(W - 2, 0, W - 1, H - 1, "#")
rect(0, H - 2, W - 1, H - 1, "#")
for x in range(W):
    top = FLOOR + random.choice([0, 0, 0, 1, 1, -1])
    rect(x, top, x, H - 1, "#")

# dock with the start, top-left, just above the sea
rect(0, SURFACE_ROW, 9, SURFACE_ROW + 1, "#")
put(4, SURFACE_ROW - 1, "S")


def building(x0, width, roof, cracked_room=None):
    """Hollow tower: walls, roof, inner floors every 3 rows with a 2-tile gap that
    alternates sides (a climbable stair once drained), windows and ground doors."""
    x1 = x0 + width - 1
    rect(x0, roof, x1, FLOOR - 1, "#")
    rect(x0 + 1, roof + 1, x1 - 1, FLOOR - 1, ".")
    left = True
    for fy in range(roof + 3, FLOOR - 1, 3):
        rect(x0 + 1, fy, x1 - 1, fy, "#")
        gap = x0 + 1 if left else x1 - 2
        rect(gap, fy, gap + 1, fy, ".")
        left = not left
    rect(x0 + 1, roof, x0 + 2, roof, ".") if left else rect(x1 - 2, roof, x1 - 1, roof, ".")  # roof hatch
    for wy in range(roof + 2, FLOOR - 3, 6):  # windows
        rect(x0, wy, x0, wy + 1, ".")
        rect(x1, wy, x1, wy + 1, ".")
    rect(x0, FLOOR - 2, x0, FLOOR - 1, ".")  # doors
    rect(x1, FLOOR - 2, x1, FLOOR - 1, ".")
    if cracked_room:
        rx, ry = cracked_room
        rect(rx - 1, ry - 1, rx + 1, ry + 1, "x")
        put(rx, ry, ".")
    return x0, x1, roof


towers = [
    building(14, 9, 22),
    building(30, 11, 18, cracked_room=(35, 25)),
    building(68, 12, 20),   # pump roof
    building(86, 10, 24),
    building(102, 11, 16, cracked_room=(107, 30)),
]

# deep: hard plug in the floor, shaft, crushing cavern, sealed floodgate
PLUG_X = 56
rect(PLUG_X - 3, FLOOR - 1, PLUG_X + 4, FLOOR - 1, ".")   # flatten the floor around it
rect(PLUG_X, FLOOR, PLUG_X + 1, FLOOR, "h")
rect(PLUG_X, FLOOR + 1, PLUG_X + 1, PRESSURE_ROW - 1, ".")
rect(PLUG_X - 1, FLOOR + 3, PLUG_X + 2, FLOOR + 5, ".")
put(PLUG_X, FLOOR + 4, "p")                                  # pipe for the suit, above the pressure line
for x in range(24, 112):
    top = PRESSURE_ROW - 1 + (2 if (x // 9) % 2 else 0)
    bottom = H - 4 - ((x * 7) % 3)
    rect(x, top, x, bottom, ".")
rect(PLUG_X, PRESSURE_ROW - 2, PLUG_X + 1, PRESSURE_ROW, ".")
put(104, H - 5, "G")
rect(102, H - 7, 106, H - 7, "x")

# sea pillar: a lone column whose top is too high to jump to from the seabed; the gear
# on it is only reachable by swimming, after refilling the basin with the pump valve
PILLAR_X, PILLAR_TOP = 48, 30
rect(PILLAR_X, PILLAR_TOP, PILLAR_X + 1, FLOOR - 1, "#")
put(PILLAR_X, PILLAR_TOP - 1, "g")

# crabs: on the inner tower floors that go dry when drained (between REFLOOD_ROW and
# DRAINED_ROW, so refilling buries them again). One per tower, mid-floor, clear of gaps.
for tx0, tx1, troof in towers:
    for fy in range(troof + 3, DRAINED_ROW, 3):
        if fy - 1 <= REFLOOD_ROW:
            continue
        mid = (tx0 + tx1) // 2
        if g[fy][mid] == "#" and g[fy - 1][mid] == "." and g[fy][mid - 1] == "#" and g[fy][mid + 1] == "#":
            put(mid, fy - 1, "C")
            break

# rooftops: pump and three plots
x0, x1, roof = towers[2]
put(x0 + 6, roof - 1, "P")
for (tx0, tx1, troof), dx in ((towers[1], 3), (towers[2], 2), (towers[3], 6)):
    put(tx0 + dx, troof - 1, "B")

# loose things
for x, y in [(20, 39), (46, 39), (64, 39), (80, 39), (98, 39), (118, 39), (37, 33), (91, 36),
             (40, 55), (62, 58), (78, 52), (92, 57), (110, 56), (30, 50)]:
    put(x, y, "r")
for x, y in [(35, 25), (107, 30), (88, 60)]:
    put(x, y, "g")
for x, y in [(20, 30), (75, 39), (70, 56)]:
    put(x, y, "p")
for x, y in [(12, 39), (44, 30), (60, 20), (84, 39), (98, 12), (122, 39), (50, 56), (96, 54)]:
    put(x, y, "O")
for x, y in [(26, 12), (52, 26), (62, 10), (96, 34), (118, 22), (44, 53), (86, 58)]:
    put(x, y, "E")

rows = ["".join(r) for r in g]
assert all(len(r) == W for r in rows)
out = Path(__file__).resolve().parent.parent / "src" / "data" / "zone1.gd"
out.write_text(f'''class_name Zone1
## First flooded zone — generated by tools/gen_zone1.py (edit that, not this). One char per 16 px tile:
##   # wall  x cracked wall (2 hits)  r rubble (1 hit, drops scrap)  . open
##   h hard wall (needs the upgraded pick)  S start  O air plant  P pump  E eel
##   g gear  p pipe  B building plot  G sealed floodgate (M2 goal)  C crab (out once drained)
## The water surface starts at SURFACE_ROW; repairing the pump drains it to DRAINED_ROW.

const SURFACE_ROW := {SURFACE_ROW}
const DRAINED_ROW := {DRAINED_ROW}
const PRESSURE_ROW := {PRESSURE_ROW}  # deeper water crushes the diver without the upgraded suit
const REFLOOD_ROW := {REFLOOD_ROW}  # the repaired pump works as a valve: refills the basin up to here

const MAP := [
''' + "".join(f'\t"{r}",\n' for r in rows) + "]\n")
print("\n".join(rows))
