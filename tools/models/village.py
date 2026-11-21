"""Landmarks that make each village its own place (looks only; the game adds a plain box for collision).

    blender -b --factory-startup --python tools/models/village.py -- all
    blender -b --factory-startup --python tools/models/village.py -- village.pub

Written in GODOT coordinates through town.py's G()/S()/gbox (x right, y up, front facing +Z towards the street).
Pivot: ground, centre of the main building's footprint. Placed by game/scripts/World/VillageLandmarks.cs.

  Wrenfold      village.school, village.green
  Kettle Cross  village.pub (the Drover's Arms), village.boatyard
  Highmoor      village.chapel, village.weather-station
  Eastgate      village.parade (on the High Street itself, where the north row of houses runs out)
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(__file__))
import _kit as k  # noqa: E402
import town as t  # noqa: E402  (helpers and palette; its registry only builds under __main__)
from town import G, M, gbox, gcyl, poly, slab, label, gable_roof, framed_window  # noqa: E402

k.PALETTE.update({
    "stone": (0.62, 0.60, 0.55), "stone-dark": (0.48, 0.46, 0.42), "slate": (0.27, 0.29, 0.32), "whitewash": (0.93, 0.92, 0.87),
    "pub-black": (0.13, 0.13, 0.14), "pub-sign": (0.62, 0.20, 0.14), "pub-gold": (0.86, 0.70, 0.30), "grass": (0.38, 0.58, 0.26),
    "grass-dark": (0.30, 0.50, 0.22), "pond": (0.30, 0.46, 0.56), "reed": (0.50, 0.58, 0.30), "duck": (0.95, 0.94, 0.90),
    "duck-beak": (0.95, 0.65, 0.15), "tar": (0.16, 0.15, 0.14), "boat-blue": (0.20, 0.38, 0.60), "boat-red": (0.70, 0.18, 0.14),
    "boat-white": (0.92, 0.92, 0.88), "net": (0.42, 0.52, 0.38), "rope": (0.72, 0.62, 0.42), "school-red": (0.62, 0.30, 0.22),
    "playground": (0.40, 0.40, 0.42), "chalk": (0.95, 0.95, 0.90), "bell": (0.72, 0.58, 0.26), "screen-white": (0.96, 0.96, 0.94),
    "lattice": (0.70, 0.72, 0.74), "cup": (0.80, 0.20, 0.15), "notice": (0.40, 0.30, 0.20),
})


def gable_x(n, cx, cz, y, w, d, rise, roof, wall, thick=0.12):
    """A gable roof with its ridge along X, closed at both ends in the wall colour."""
    gable_roof(n, cx, cz, y, w + 0.4, d + 0.6, rise, roof, end_colour=wall, thick=thick, wall_w=w, wall_d=d)


def gable_z(n, cx, cz, y, w, d, rise, roof, wall, thick=0.12):
    """A gable roof with its ridge along Z (the gable end faces the street)."""
    hw, hd = w / 2 + 0.25, d / 2 + 0.3
    slab(f"{n}-l", (cx - hw, y, cz - hd), (cx - hw, y, cz + hd), (cx, y + rise, cz + hd), (cx, y + rise, cz - hd), thick, roof)
    slab(f"{n}-r", (cx + hw, y, cz + hd), (cx + hw, y, cz - hd), (cx, y + rise, cz - hd), (cx, y + rise, cz + hd), thick, roof)
    gbox(f"{n}-ridge", (cx, y + rise + 0.03, cz), (0.2, 0.1, d + 0.65), "roof-charcoal")
    for s in (-1, 1):
        z = cz + s * d / 2
        ey = y + rise * (1 - (w / 2) / hw) - thick
        poly(f"{n}-gable{s}", [(cx - w / 2, y - 0.3, z), (cx + w / 2, y - 0.3, z), (cx + w / 2, ey, z), (cx, y + rise - thick, z), (cx - w / 2, ey, z)],
             [(0, 1, 2, 3, 4)], wall)


def walls(n, w, d, h, colour, door_w=1.2, door_h=2.2, door_x=0.0):
    """Four walls 0.25 thick round a w x d footprint, a door gap in the front (+Z) wall."""
    th = 0.25
    gbox(f"{n}-back", (0, h / 2, -d / 2 + th / 2), (w, h, th), colour)
    for s in (-1, 1):
        gbox(f"{n}-side{s}", (s * (w / 2 - th / 2), h / 2, 0), (th, h, d - 2 * th), colour)
    lw = w / 2 + door_x - door_w / 2
    rw = w / 2 - door_x - door_w / 2
    gbox(f"{n}-front-l", (-w / 2 + lw / 2, h / 2, d / 2 - th / 2), (lw, h, th), colour)
    gbox(f"{n}-front-r", (w / 2 - rw / 2, h / 2, d / 2 - th / 2), (rw, h, th), colour)
    gbox(f"{n}-over-door", (door_x, (h + door_h) / 2, d / 2 - th / 2), (door_w, h - door_h, th), colour)
    gbox(f"{n}-floor", (0, 0.06, 0), (w - 0.1, 0.12, d - 0.1), "floorboard")


def door(n, x, z, w=1.0, h=2.1, colour="door-green", knob="brass"):
    gbox(f"{n}", (x, h / 2 + 0.05, z), (w, h, 0.06), colour)
    gbox(f"{n}-frame", (x, h + 0.1, z + 0.01), (w + 0.16, 0.1, 0.08), "trim")
    gbox(f"{n}-knob", (x + w * 0.35, 1.0, z + 0.05), (0.06, 0.06, 0.06), knob)


def arched_window(n, x, y, z, w, h, out=1, frame="stone-dark", glass="glass"):
    """A tall window with a pointed head, on a wall facing +Z (out=1) or -Z, or a side wall (out=+-2: faces +-X)."""
    side = abs(out) == 2
    o = 1 if out > 0 else -1
    def P(dx, dy, dz=0.0):
        return (x + dz * o, y + dy, z + dx) if side else (x + dx, y + dy, z + dz * o)
    thick = (0.04, 0, 0)
    pts = [P(-w / 2, -h / 2, 0.05), P(w / 2, -h / 2, 0.05), P(w / 2, h / 2 - w / 2, 0.05), P(0, h / 2, 0.05), P(-w / 2, h / 2 - w / 2, 0.05)]
    poly(f"{n}-glass", pts, [(0, 1, 2, 3, 4)], glass)
    fr = [P(-w / 2 - 0.1, -h / 2 - 0.1, 0.03), P(w / 2 + 0.1, -h / 2 - 0.1, 0.03), P(w / 2 + 0.1, h / 2 - w / 2, 0.03), P(0, h / 2 + 0.14, 0.03), P(-w / 2 - 0.1, h / 2 - w / 2, 0.03)]
    poly(f"{n}-frame", fr, [(0, 1, 2, 3, 4)], frame)
    if side:
        gbox(f"{n}-sill", (x + o * 0.1, y - h / 2 - 0.08, z), (0.18, 0.08, w + 0.3), frame)
        gbox(f"{n}-mullion", (x + o * 0.065, y - 0.05, z), (0.02, h - 0.2, 0.04), frame)
    else:
        gbox(f"{n}-sill", (x, y - h / 2 - 0.08, z + o * 0.1), (w + 0.3, 0.08, 0.18), frame)
        gbox(f"{n}-mullion", (x, y - 0.05, z + o * 0.065), (0.04, h - 0.2, 0.02), frame)
    del thick


def bench(n, x, z, rot=0.0, colour="wood"):
    c, s = math.cos(rot), math.sin(rot)
    def R(dx, dz):
        return (x + dx * c + dz * s, z - dx * s + dz * c)
    for i, dz in enumerate((-0.12, 0.0, 0.12)):
        px, pz = R(0, dz)
        gbox(f"{n}-slat{i}", (px, 0.45, pz), (1.6, 0.04, 0.1), colour, rot_y=rot)
    px, pz = R(0, -0.24)
    gbox(f"{n}-back", (px, 0.75, pz), (1.6, 0.25, 0.04), colour, rot_y=rot)
    for sx in (-0.7, 0.7):
        px, pz = R(sx, 0)
        gbox(f"{n}-leg{sx}", (px, 0.22, pz), (0.06, 0.45, 0.4), "ink", rot_y=rot)


def picnic_table(n, x, z):
    gbox(f"{n}-top", (x, 0.75, z), (1.8, 0.05, 0.7), "wood-light")
    for sz in (-1, 1):
        gbox(f"{n}-seat{sz}", (x, 0.45, z + sz * 0.6), (1.8, 0.04, 0.28), "wood-light")
    for sx in (-0.7, 0.7):
        for sz in (-1, 1):
            k.beam(f"{n}-leg{sx}{sz}", G(x + sx, 0, z + sz * 0.7), G(x + sx, 0.75, z), 0.06, M("wood"))


def duck(n, x, z, rot=0.0):
    gbox(f"{n}-body", (x, 0.08, z), (0.14, 0.09, 0.24), "duck", rot_y=rot)
    gbox(f"{n}-head", (x + 0.1 * math.sin(rot), 0.17, z + 0.1 * math.cos(rot)), (0.07, 0.08, 0.08), "duck", rot_y=rot)
    gbox(f"{n}-beak", (x + 0.15 * math.sin(rot), 0.16, z + 0.15 * math.cos(rot)), (0.04, 0.02, 0.06), "duck-beak", rot_y=rot)


# ======================================================================================== Wrenfold
def village_school():
    """Wrenfold School: a one-room board school, red brick, tall windows, a bell cote on the ridge, a playground."""
    w, d, h = 11.0, 6.5, 3.8
    gbox("plinth", (0, -0.15, 0), (w + 0.2, 0.3, d + 0.2), "stone-dark")
    walls("w", w, d, h, "school-red", door_w=1.3, door_x=-3.6)
    for i in range(int(h / 0.5)):
        gbox(f"band{i}", (0, 0.3 + i * 0.5, d / 2 + 0.005), (w, 0.03, 0.012), "brick-dark")
    for x in (-1.0, 1.4, 3.8):
        framed_window(f"win-f{x}", x, 1.9, d / 2, 1.3, 2.0)
    for x in (-3.5, -1.0, 1.4, 3.8):
        framed_window(f"win-b{x}", x, 1.9, -d / 2, 1.3, 2.0, out=-1)
    # porch over the door, a stone lintel with the name, the year
    gbox("porch-roof", (-3.6, 2.7, d / 2 + 0.7), (2.2, 0.12, 1.5), "slate")
    for sx in (-1, 1):
        gbox(f"porch-post{sx}", (-3.6 + sx * 0.95, 1.33, d / 2 + 1.35), (0.12, 2.66, 0.12), "trim")
    door("door", -3.6, d / 2 - 0.1, w=1.1, colour="shutter-blue")
    gbox("lintel", (0.4, 3.35, d / 2 + 0.02), (6.4, 0.42, 0.06), "stone")
    label("name", "WRENFOLD SCHOOL", (0.4, 3.35, d / 2 + 0.055), 0.3, "ink")
    gable_x("roof", 0, 0, h, w, d, 2.0, "slate", "school-red")
    # bell cote on the ridge: two posts, a little roof, a brass bell
    for sx in (-1, 1):
        gbox(f"cote-post{sx}", (2.5 + sx * 0.3, h + 2.35, 0), (0.12, 0.9, 0.12), "trim")
    gable_x("cote-roof", 2.5, 0, h + 2.8, 0.75, 0.7, 0.35, "slate", "trim", thick=0.06)
    gcyl("bell", (2.5, h + 2.15, 0), 0.18, 0.3, "bell", sides=10, r2=0.09)
    gcyl("chimney", (-4.6, h + 1.0, -1.2), 0.3, 1.8, "brick", sides=6)
    # playground in front (to one side of the path): tarmac, hopscotch, railings, a bike rack, a climbing frame
    gbox("yard", (1.8, 0.02, d / 2 + 3.2), (7.0, 0.04, 5.0), "playground")
    for i, (dx, dz) in enumerate(((0, 0), (0, 0.55), (-0.28, 1.1), (0.28, 1.1), (0, 1.65), (-0.28, 2.2), (0.28, 2.2), (0, 2.75))):
        gbox(f"hop{i}", (-0.3 + dx, 0.045, d / 2 + 1.7 + dz), (0.5, 0.006, 0.5), "chalk")
        gbox(f"hop-in{i}", (-0.3 + dx, 0.048, d / 2 + 1.7 + dz), (0.42, 0.006, 0.42), "playground")
    for i in range(15):
        x = -1.7 + i * 0.5
        gbox(f"rail{i}", (x, 0.55, d / 2 + 5.7), (0.04, 1.1, 0.04), "ink")
    gbox("rail-top", (1.8, 1.08, d / 2 + 5.7), (7.0, 0.05, 0.05), "ink")
    for sx in (-1, 1):
        for sz in (-1, 1):
            k.beam(f"climb{sx}{sz}", G(3.6 + sx * 0.8, 0, d / 2 + 3.4 + sz * 0.8), G(3.6 + sx * 0.3, 2.0, d / 2 + 3.4 + sz * 0.3), 0.07, M("shutter-red"))
    for y in (0.6, 1.2, 1.8):
        f = (2.0 - y) / 2.0
        gbox(f"climb-bar{y}", (3.6, y, d / 2 + 3.4), (0.6 + 1.0 * f, 0.05, 0.6 + 1.0 * f), "poster-yellow")
    for i in range(4):
        gbox(f"bike-hoop{i}", (5.2 - i * 0.0, 0.4, d / 2 + 1.0 + i * 0.6), (0.05, 0.8, 0.5), "steel")
    # bodge charm: a football stuck in the gutter
    gcyl("ball", (-2.2, h + 0.06, d / 2 + 0.35), 0.11, 0.22, "chalk", sides=8)


def village_green():
    """Wrenfold Green: a tufty oval of grass, a duck pond with reeds, a bench round an old oak, the parish noticeboard."""
    poly("green", [(math.cos(a) * 11, 0.03, math.sin(a) * 7) for a in (i * math.pi / 8 for i in range(16))],
         [tuple(range(16))], "grass")
    for i in range(14):
        a = i * 2.4
        gbox(f"tuft{i}", (math.cos(a) * (3 + i * 0.5), 0.07, math.sin(a) * (2 + i * 0.3)), (0.3, 0.08, 0.25), "grass-dark")
    # the pond
    poly("pond", [(-5 + math.cos(a) * 3.2, 0.05, 1.2 + math.sin(a) * 2.0) for a in (i * math.pi / 6 for i in range(12))], [tuple(range(12))], "pond")
    for i in range(9):
        a = i * 0.7 + 0.3
        x, z = -5 + math.cos(a) * 3.1, 1.2 + math.sin(a) * 1.95
        for j in range(3):
            gcyl(f"reed{i}{j}", (x + j * 0.08, 0.0, z + (j % 2) * 0.08), 0.02, 0.6 + j * 0.15, "reed", sides=4)
    for i, (x, z, r) in enumerate(((-5.6, 0.6, 0.4), (-4.2, 1.9, 2.0), (-4.9, 1.6, 4.2))):
        duck(f"duck{i}", x, z, r)
    # the oak and the bench that goes all the way round it
    gcyl("oak-trunk", (4, 0, -0.5), 0.45, 3.0, "bark", sides=8, r2=0.32)
    for i, (dx, dy, dz, r) in enumerate(((0, 4.2, 0, 2.8), (1.4, 3.6, 0.8, 2.0), (-1.3, 3.8, -0.6, 2.2))):
        t.blob(f"oak-canopy{i}", (4 + dx, dy, -0.5 + dz), r, r * 0.65, r * 0.55, ("leaf-1", "leaf-2", "leaf-3")[i], sides=7, rot=i)
    for i in range(6):
        a = i * math.pi / 3
        gbox(f"ring-seat{i}", (4 + math.cos(a) * 0.95, 0.45, -0.5 + math.sin(a) * 0.95), (0.95, 0.05, 0.35), "wood-light", rot_y=-a + math.pi / 2)
        gbox(f"ring-leg{i}", (4 + math.cos(a) * 0.95, 0.22, -0.5 + math.sin(a) * 0.95), (0.06, 0.45, 0.06), "wood")
    bench("bench", 0.5, 4.0)
    # parish noticeboard: two posts, a little roof, pinned notices
    for sx in (-1, 1):
        gbox(f"nb-post{sx}", (8 + sx * 0.6, 0.9, 3.5), (0.1, 1.8, 0.1), "notice")
    gbox("nb-board", (8, 1.35, 3.53), (1.2, 0.8, 0.06), "notice")
    gable_x("nb-roof", 8, 3.5, 1.8, 1.4, 0.3, 0.2, "slate", "notice", thick=0.05)
    for i, (dx, dy, c) in enumerate(((-0.3, 0.15, "paper-1"), (0.25, 0.2, "paper-3"), (-0.25, -0.2, "paper-4"), (0.3, -0.15, "label"))):
        gbox(f"nb-note{i}", (8 + dx, 1.35 + dy, 3.57), (0.4, 0.3, 0.01), c)
    label("nb-head", "PARISH NOTICES", (8, 1.68, 3.565), 0.07, "label")
    label("nb-cat", "LOST: CAT", (8 - 0.3, 1.52, 3.58), 0.05, "ink")


# ======================================================================================== Kettle Cross
def village_pub():
    """The Drover's Arms: a whitewashed two-storey pub on the crossroads, black trim, a swinging sign, benches outside."""
    w, d, h = 10.0, 7.0, 5.6
    gbox("plinth", (0, -0.15, 0), (w + 0.2, 0.3, d + 0.2), "pub-black")
    walls("w", w, d, h, "whitewash", door_w=1.2, door_x=0.0)
    gbox("skirt", (0, 0.3, d / 2 + 0.01), (w, 0.6, 0.04), "pub-black")
    for x in (-3.2, 3.2):
        framed_window(f"bay{x}", x, 1.5, d / 2, 2.0, 1.3, frame="pub-black")
        framed_window(f"up{x}", x, 4.1, d / 2, 1.3, 1.0, frame="pub-black")
    framed_window("up-mid", 0, 4.1, d / 2, 1.0, 1.0, frame="pub-black")
    for x in (-3.2, 3.2):
        framed_window(f"back{x}", x, 1.5, -d / 2, 1.3, 1.0, out=-1, frame="pub-black")
    door("door", 0, d / 2 - 0.1, w=1.0, colour="pub-black", knob="pub-gold")
    # fascia board with the name across the front
    gbox("fascia", (0, 2.75, d / 2 + 0.04), (w - 0.4, 0.55, 0.08), "pub-black")
    label("fascia-name", "THE DROVER'S ARMS", (0, 2.75, d / 2 + 0.09), 0.34, "pub-gold")
    gable_x("roof", 0, 0, h, w, d, 2.2, "slate", "whitewash")
    for sx in (-1, 1):
        gbox(f"chimney{sx}", (sx * (w / 2 - 0.4), h + 2.0, 0), (0.7, 1.6, 0.9), "whitewash")
        gcyl(f"pot{sx}", (sx * (w / 2 - 0.4), h + 2.8, 0), 0.12, 0.35, "chimney-pot", sides=6)
    # hanging sign on an iron bracket at the corner: a drover's crook and a cow, in paint
    k.beam("bracket", G(w / 2 - 0.3, 3.6, d / 2 + 0.05), G(w / 2 - 0.3, 3.6, d / 2 + 1.4), 0.06, M("pub-black"))
    k.beam("bracket-stay", G(w / 2 - 0.3, 3.0, d / 2 + 0.05), G(w / 2 - 0.3, 3.6, d / 2 + 1.0), 0.04, M("pub-black"))
    gbox("sign-frame", (w / 2 - 0.3, 2.95, d / 2 + 0.95), (0.06, 1.15, 0.85), "pub-black")
    gbox("sign-face", (w / 2 - 0.26, 2.95, d / 2 + 0.95), (0.04, 1.0, 0.72), "pub-sign")
    gbox("sign-cow", (w / 2 - 0.23, 2.85, d / 2 + 0.95), (0.02, 0.3, 0.45), "whitewash")
    k.beam("sign-crook", G(w / 2 - 0.22, 2.6, d / 2 + 0.75), G(w / 2 - 0.22, 3.35, d / 2 + 0.75), 0.03, M("pub-gold"))
    # outside: picnic tables, barrels, a chalkboard
    picnic_table("table-l", -3.2, d / 2 + 2.2)
    picnic_table("table-r", 3.0, d / 2 + 2.4)
    for i, x in enumerate((-5.6, -5.0)):
        gcyl(f"barrel{i}", (x, 0, d / 2 + 0.5), 0.3, 0.8, "wood", sides=10)
        gcyl(f"barrel-hoop{i}", (x, 0.6, d / 2 + 0.5), 0.31, 0.05, "steel", sides=10)
    for sz in (-1, 1):
        k.beam(f"aboard{sz}", G(1.4, 0, d / 2 + 0.9 + sz * 0.25), G(1.4, 1.0, d / 2 + 0.9), 0.05, M("wood"))
    gbox("chalkboard", (1.4, 0.55, d / 2 + 1.12), (0.55, 0.75, 0.03), "pub-black", rot_y=0.0)
    label("chalk-1", "QUIZ THURS", (1.4, 0.72, d / 2 + 1.14), 0.07, "chalk")
    label("chalk-2", "PIE £1", (1.4, 0.5, d / 2 + 1.14), 0.07, "chalk")


def village_boatyard():
    """Kettle Cross boatyard: a tarred boat shed, a crabber up on its trailer, lobster pots, nets drying on poles."""
    w, d, h = 6.0, 8.0, 3.0
    gbox("hard", (2.0, 0.02, 1.0), (13.0, 0.04, 11.0), "concrete")
    # the shed: tarred boards, big doors facing the street, open a crack
    walls("w", w, d, h, "tar", door_w=3.0, door_h=2.6, door_x=0.0)
    for i in range(int(w / 0.3)):
        gbox(f"board{i}", (-w / 2 + 0.15 + i * 0.3, h / 2, d / 2 + 0.01), (0.02, h, 0.02), "pub-black")
    gbox("door-l", (-1.1, 1.3, d / 2 + 0.06), (0.9, 2.6, 0.06), "tar")
    gbox("door-r", (1.4, 1.3, d / 2 + 0.6), (0.06, 2.6, 1.4), "tar")
    gable_z("roof", 0, 0, h, w, d, 1.6, "corrugated", "tar")
    label("shed-name", "K.X. BOATS", (0, h + 0.5, d / 2 + 0.04), 0.3, "boat-white")
    # the crabber on its trailer: hull from two slabs and a transom, wheelhouse, mast with a lamp
    bx, bz, by = 6.0, 1.5, 0.75
    L, B, D = 6.0, 2.2, 1.2
    hull = [(bx - B / 2, by + D, bz + L / 2 - 0.4), (bx + B / 2, by + D, bz + L / 2 - 0.4), (bx + B / 2, by + D, bz - L / 2),
            (bx - B / 2, by + D, bz - L / 2), (bx, by + D + 0.15, bz + L / 2 + 0.3), (bx, by, bz + L / 2 - 0.6),
            (bx - 0.3, by, bz - L / 2 + 0.2), (bx + 0.3, by, bz - L / 2 + 0.2)]
    poly("hull", hull, [(0, 4, 1), (1, 4, 5), (0, 5, 4), (1, 5, 7, 2), (0, 3, 6, 5), (5, 6, 7), (2, 7, 6, 3), (0, 1, 2, 3)], "boat-blue")
    gbox("rubbing-strake", (bx, by + D - 0.12, bz), (B + 0.06, 0.1, L - 0.5), "boat-red")
    gbox("wheelhouse", (bx, by + D + 0.7, bz - 1.2), (1.4, 1.4, 1.4), "boat-white")
    gbox("wheelhouse-roof", (bx, by + D + 1.45, bz - 1.2), (1.6, 0.1, 1.6), "boat-red")
    gbox("wheelhouse-glass", (bx, by + D + 1.0, bz - 0.49), (1.1, 0.5, 0.02), "glass")
    gcyl("mast", (bx, by + D + 1.5, bz - 1.2), 0.05, 1.6, "boat-white", sides=6)
    gcyl("mast-lamp", (bx, by + D + 3.1, bz - 1.2), 0.08, 0.15, "lampshade", sides=6)
    label("boat-name", "MARGIT", (bx + B / 2 + 0.01, by + D - 0.4, bz + 1.6), 0.25, "boat-white", face="+X")
    label("boat-no", "KX 12", (bx - B / 2 - 0.01, by + D - 0.4, bz + 1.6), 0.25, "boat-white", face="-X")
    # trailer: a frame, two wheels, a jockey wheel
    for sx in (-1, 1):
        gbox(f"trailer-rail{sx}", (bx + sx * 0.5, 0.55, bz), (0.12, 0.12, L), "steel")
        gcyl(f"wheel{sx}", (bx + sx * 1.0, 0.0, bz - 0.5), 0.35, 0.25, "tyre", sides=10)
    k.cylinder("axle", G(bx, 0.35, bz - 0.5), 0.04, 2.0, M("steel"), axis="X", sides=6)
    k.beam("drawbar", G(bx, 0.55, bz + L / 2), G(bx, 0.5, bz + L / 2 + 1.2), 0.1, M("steel"))
    # lobster pots stacked against the shed, a buoy or two, rope coils
    for i in range(7):
        row, col = divmod(i, 3)
        x, y, z = -w / 2 - 0.6 - col * 0.0, 0.25 + row * 0.5, 1.8 - col * 0.7
        if row == 2:
            x, z = -w / 2 - 0.6, 1.45
        gbox(f"pot{i}", (x, y, z), (0.6, 0.45, 0.6), "net")
        gbox(f"pot-base{i}", (x, y - 0.21, z), (0.62, 0.04, 0.62), "wood")
    for i, (x, z, c) in enumerate(((-w / 2 - 0.5, -1.0, "boat-red"), (-w / 2 - 0.9, -1.4, "poster-yellow"))):
        gcyl(f"buoy{i}", (x, 0, z), 0.25, 0.5, c, sides=8)
    gcyl("rope-coil", (2.5, 0, 6.0), 0.4, 0.15, "rope", sides=10)
    # nets drying between two poles
    for sx in (-1, 1):
        gcyl(f"net-pole{sx}", (10.0 + sx * 1.8, 0, 3.5), 0.06, 2.4, "wood", sides=6)
    poly("net-hang", [(8.2, 2.3, 3.5), (11.8, 2.3, 3.5), (11.4, 0.6, 3.55), (8.6, 0.5, 3.55)], [(0, 1, 2, 3)], "net")


# ======================================================================================== Highmoor
def village_chapel():
    """Highmoor chapel: grey stone, a steep slate roof, a bell gable over the door, a walled yard with a few headstones."""
    w, d, h = 6.0, 10.0, 4.2
    gbox("plinth", (0, -0.15, 0), (w + 0.3, 0.3, d + 0.3), "stone-dark")
    walls("w", w, d, h, "stone", door_w=1.2, door_h=2.4, door_x=0.0)
    for i in range(int(h / 0.4)):
        for s in (-1, 1):
            gbox(f"course-s{i}{s}", (s * (w / 2 + 0.005), 0.25 + i * 0.4, 0), (0.012, 0.03, d), "stone-dark")
    for z in (-3.0, 0.0, 3.0):
        for s in (-1, 1):
            arched_window(f"win{z}{s}", s * w / 2, 2.2, z, 0.8, 1.9, out=2 * s)
    arched_window("win-back", 0, 2.6, -d / 2, 1.0, 2.2, out=-1)
    # pointed door surround and a round window over it
    arched_window("door-arch", 0, 1.3, d / 2 + 0.02, 1.4, 2.7, frame="stone-dark", glass="wood")
    door("door", 0, d / 2 - 0.05, w=1.1, h=2.2, colour="wood")
    k.cylinder("rose", G(0, 4.9, d / 2 + 0.03), 0.45, 0.06, M("stone-dark"), axis="Y", sides=10)
    k.cylinder("rose-glass", G(0, 4.9, d / 2 + 0.065), 0.34, 0.02, M("glass"), axis="Y", sides=10)
    gable_z("roof", 0, 0, h, w, d, 3.2, "slate", "stone")
    # the bell gable rising above the front gable, a bell in the opening
    gbox("bellcote", (0, h + 3.4, d / 2 - 0.15), (1.4, 1.6, 0.4), "stone")
    gbox("bellcote-hole", (0, h + 3.35, d / 2 + 0.06), (0.7, 0.9, 0.02), "ink")
    gcyl("bell", (0, h + 3.0, d / 2 - 0.15), 0.2, 0.32, "bell", sides=10, r2=0.1)
    poly("bellcote-cap", [(-0.8, h + 4.2, d / 2 + 0.1), (0.8, h + 4.2, d / 2 + 0.1), (0, h + 4.7, d / 2 + 0.1),
                          (-0.8, h + 4.2, d / 2 - 0.4), (0.8, h + 4.2, d / 2 - 0.4), (0, h + 4.7, d / 2 - 0.4)],
         [(0, 1, 2), (3, 5, 4), (0, 3, 4, 1), (1, 4, 5, 2), (0, 2, 5, 3)], "stone-dark")
    gbox("cross", (0, h + 4.95, d / 2 - 0.15), (0.06, 0.5, 0.06), "stone-dark")
    gbox("cross-arm", (0, h + 5.05, d / 2 - 0.15), (0.3, 0.06, 0.06), "stone-dark")
    # a stone wall round the yard, a gate gap, some leaning headstones, a yew
    yw, yd = 14.0, 16.0
    for s in (-1, 1):
        gbox(f"yard-side{s}", (s * yw / 2, 0.45, 0.5), (0.4, 0.9, yd), "stone-dark")
        gbox(f"yard-front{s}", (s * (yw / 4 + 0.6), 0.45, yd / 2 + 0.5), (yw / 2 - 1.2, 0.9, 0.4), "stone-dark")
    gbox("yard-back", (0, 0.45, -yd / 2 + 0.5), (yw, 0.9, 0.4), "stone-dark")
    for i, (x, z, lean) in enumerate(((-4.6, -2.0, 0.05), (-4.6, 0.5, -0.1), (-4.6, 3.0, 0.0), (4.6, -1.0, 0.12), (4.6, 1.8, -0.06), (-5.6, 5.6, 0.08))):
        o = gbox(f"stone{i}", (x, 0.45, z), (0.12, 0.9, 0.55), "stone")
        o.rotation_euler = (lean, 0, 0)
    gcyl("yew-trunk", (5.2, 0, -5.5), 0.25, 1.0, "bark-dark", sides=6)
    t.blob("yew", (5.2, 2.4, -5.5), 1.4, 1.8, 1.2, "needle-1", sides=7)
    label("board", "HIGHMOOR CHAPEL", (2.8, 0.7, yd / 2 + 0.72), 0.13, "label")
    gbox("board-back", (2.8, 0.7, yd / 2 + 0.71), (1.7, 0.3, 0.02), "shutter-green")


def village_weather_station():
    """Highmoor weather station: a white louvred screen on legs, a lattice mast with spinning cups and a vane,
    a rain gauge, a hut for the logbook, the whole lot fenced. The data line you sell them ends at the hut."""
    # fenced plot
    pw, pd = 10.0, 9.0
    gbox("plot", (0, 0.02, 0), (pw, 0.04, pd), "grass-dark")
    for s in (-1, 1):
        for along, n in ((pw, int(pw / 1.5)), (pd, int(pd / 1.5))):
            for i in range(n + 1):
                x = -along / 2 + i * along / n
                pos = (x, 0.6, s * pd / 2) if along == pw else (s * pw / 2, 0.6, x)
                gbox(f"fp{along}{s}{i}", pos, (0.08, 1.2, 0.08), "wood")
            rail = (pw, 0.06, 0.04) if along == pw else (0.04, 0.06, pd)
            for y in (0.5, 1.0):
                gbox(f"fr{along}{s}{y}", (0, y, s * pd / 2) if along == pw else (s * pw / 2, y, 0), rail, "steel")
    # the screen: a louvred white box on four legs
    sx, sz = -2.2, 1.0
    for dx in (-0.35, 0.35):
        for dz in (-0.3, 0.3):
            gbox(f"leg{dx}{dz}", (sx + dx, 0.6, sz + dz), (0.06, 1.2, 0.06), "screen-white")
    gbox("screen-box", (sx, 1.55, sz), (0.85, 0.7, 0.7), "screen-white")
    for i in range(6):
        gbox(f"louvre{i}", (sx, 1.27 + i * 0.11, sz + 0.36), (0.8, 0.03, 0.03), "steel")
    gable_x("screen-roof", sx, sz, 1.9, 0.85, 0.7, 0.15, "screen-white", "screen-white", thick=0.04)
    # the mast: a triangular lattice, cups and vane on top
    mx, mz, mh = 2.4, -1.5, 9.0
    feet = [(mx + 0.4 * math.cos(a), mz + 0.4 * math.sin(a)) for a in (0, 2.094, 4.189)]
    for i, (fx, fz) in enumerate(feet):
        k.beam(f"mast-leg{i}", G(fx, 0, fz), G(mx + (fx - mx) * 0.3, mh, mz + (fz - mz) * 0.3), 0.05, M("lattice"), round_=True)
        nx, nz = feet[(i + 1) % 3]
        for j in range(6):
            y0, y1 = j * mh / 6, (j + 1) * mh / 6
            f0, f1 = 1 - 0.7 * y0 / mh, 1 - 0.7 * y1 / mh
            k.beam(f"brace{i}{j}", G(mx + (fx - mx) * f0, y0, mz + (fz - mz) * f0), G(mx + (nx - mx) * f1, y1, mz + (nz - mz) * f1), 0.025, M("lattice"))
    gcyl("mast-top", (mx, mh, mz), 0.03, 0.6, "steel", sides=6)
    for i in range(3):
        a = i * 2.094
        k.beam(f"cup-arm{i}", G(mx, mh + 0.6, mz), G(mx + math.cos(a) * 0.35, mh + 0.6, mz + math.sin(a) * 0.35), 0.015, M("steel"))
        gcyl(f"cup{i}", (mx + math.cos(a) * 0.38, mh + 0.53, mz + math.sin(a) * 0.38), 0.07, 0.1, "cup", sides=6, r2=0.03)
    k.beam("vane", G(mx - 0.5, mh + 0.25, mz), G(mx + 0.45, mh + 0.25, mz), 0.02, M("ink"))
    poly("vane-tail", [(mx - 0.5, mh + 0.1, mz), (mx - 0.5, mh + 0.42, mz), (mx - 0.2, mh + 0.25, mz)], [(0, 1, 2)], "ink")
    gcyl("vane-nose", (mx + 0.47, mh + 0.21, mz), 0.04, 0.08, "ink", sides=4, r2=0.0)
    for sgn in (-1, 1):
        k.beam(f"stay{sgn}", G(mx, mh * 0.7, mz), G(mx + sgn * 3.6, 0, mz - 2.5), 0.012, M("steel"))
    # rain gauge, sunshine recorder (a glass ball on a post)
    gcyl("gauge", (-0.5, 0, 2.8), 0.1, 0.35, "steel", sides=8)
    gcyl("gauge-funnel", (-0.5, 0.35, 2.8), 0.1, 0.06, "steel", sides=8, r2=0.13)
    gbox("sun-post", (0.6, 0.6, 2.8), (0.12, 1.2, 0.12), "screen-white")
    t.blob("sun-ball", (0.6, 1.32, 2.8), 0.12, 0.12, 0.12, "glass-in", sides=8)
    # the observer's hut, its door to the street; a logbook on a hook and the terminal box the data line ends at
    hx, hz = -2.0, -2.6
    gbox("hut", (hx, 1.2, hz), (3.0, 2.4, 2.4), "screen-white")
    gable_x("hut-roof", hx, hz, 2.4, 3.0, 2.4, 0.6, "slate", "screen-white", thick=0.06)
    door("hut-door", hx + 0.6, hz + 1.22, w=0.8, h=2.0, colour="shutter-blue")
    framed_window("hut-win", hx - 0.7, 1.5, hz + 1.2, 0.8, 0.6)
    gbox("hut-sign", (hx, 2.15, hz + 1.24), (2.4, 0.3, 0.03), "shutter-blue")
    label("hut-name", "HIGHMOOR WEATHER STN", (hx, 2.15, hz + 1.26), 0.11, "screen-white")
    gbox("data-box", (hx + 1.52, 1.1, hz + 0.6), (0.06, 0.4, 0.3), "steel")
    label("alt", "ALT 412 m", (hx + 1.55, 1.42, hz + 0.6), 0.06, "ink", face="+X")


# ======================================================================================== Eastgate
def village_parade():
    """Eastgate's parade: three shops in a brick terrace with flats over them: the post office, a bakery, a
    launderette. Each has a fascia, a window display and a door; a post box and a bench on the pavement."""
    w, d, h = 18.0, 8.0, 6.0
    gbox("plinth", (0, -0.15, 0), (w + 0.2, 0.3, d + 0.2), "stone-dark")
    gbox("back", (0, h / 2, -d / 2 + 0.125), (w, h, 0.25), "brick")
    for s_ in (-1, 1):
        gbox(f"side{s_}", (s_ * (w / 2 - 0.125), h / 2, 0), (0.25, h, d - 0.5), "brick")
    gbox("upper-front", (0, 4.3, d / 2 - 0.125), (w, 3.4, 0.25), "brick")
    gbox("floor", (0, 0.06, 0), (w - 0.3, 0.12, d - 0.3), "floorboard")
    shops = (("EASTGATE POST OFFICE", "shutter-red", "paper-3"), ("BAKERY", "wall-cream", "poster-yellow"), ("SUDS LAUNDERETTE", "shutter-blue", "label"))
    for i, (name, fascia, display) in enumerate(shops):
        x = -w / 3 + i * w / 3
        sw = w / 3
        gbox(f"pier{i}", (x - sw / 2 + 0.2, 1.3, d / 2 - 0.125), (0.4, 2.6, 0.25), "brick")
        gbox(f"window{i}", (x - 0.6, 1.4, d / 2 - 0.1), (sw - 2.4, 1.8, 0.05), "glass")
        gbox(f"stall{i}", (x - 0.6, 0.3, d / 2 - 0.05), (sw - 2.4, 0.6, 0.15), fascia)
        gbox(f"display{i}", (x - 0.6, 0.75, d / 2 - 0.4), (sw - 2.8, 0.3, 0.5), display)
        door(f"door{i}", x + sw / 2 - 1.0, d / 2 - 0.1, w=0.95, h=2.3, colour=fascia if fascia != "wall-cream" else "door-green")
        gbox(f"over-door{i}", (x + sw / 2 - 1.0, 2.5, d / 2 - 0.125), (1.2, 0.2, 0.25), "brick")
        gbox(f"fascia{i}", (x, 2.95, d / 2 + 0.05), (sw - 0.2, 0.6, 0.12), fascia)
        label(f"fascia-text{i}", name, (x, 2.95, d / 2 + 0.115), 0.26 if len(name) < 12 else 0.2, "label" if fascia != "wall-cream" else "ink")
        for j in (-1, 1):
            framed_window(f"flat{i}{j}", x + j * 1.5, 4.4, d / 2, 1.2, 1.2)
    # the post office's red box, the bakery's awning, the launderette's machines seen through the glass
    gcyl("postbox", (-w / 3 - 2.6, 0, d / 2 + 1.4), 0.25, 1.3, "shutter-red", sides=10)
    gcyl("postbox-cap", (-w / 3 - 2.6, 1.3, d / 2 + 1.4), 0.27, 0.12, "shutter-red", sides=10)
    gbox("postbox-slot", (-w / 3 - 2.6, 1.05, d / 2 + 1.66), (0.2, 0.04, 0.02), "ink")
    slab("awning", (-2.6, 2.6, d / 2 + 0.05), (2.0, 2.6, d / 2 + 0.05), (2.0, 2.2, d / 2 + 1.3), (-2.6, 2.2, d / 2 + 1.3), 0.05, "awning-cream")
    for i in range(3):
        x = w / 3 - 2.2 + i * 0.75
        gbox(f"washer{i}", (x, 0.45, d / 2 - 0.9), (0.6, 0.85, 0.6), "screen-white")
        k.cylinder(f"washer-door{i}", G(x, 0.5, d / 2 - 0.59), 0.2, 0.02, M("glass"), axis="Y", sides=10)
    gable_x("roof", 0, 0, h, w, d, 2.0, "slate", "brick")
    for x in (-w / 3, 0, w / 3):
        gbox(f"chimney{x}", (x, h + 1.8, -0.5), (0.8, 1.3, 0.6), "brick")
    bench("bench", w / 3 + 3.5, d / 2 + 1.6)


PROPS = {
    "village.school": (village_school, "Wrenfold School", "A red-brick board school with tall windows, a bell cote on the ridge, a porch, and a playground with hopscotch and a climbing frame.", "ground, centre of the building; front +Z", False),
    "village.green": (village_green, "Wrenfold Green", "An oval of tufty grass with a duck pond and reeds, an old oak with a ring seat, a bench and the parish noticeboard (LOST: CAT).", "ground, centre of the green", False),
    "village.pub": (village_pub, "The Drover's Arms", "A whitewashed two-storey pub with black trim, gold lettering, a swinging sign with a cow and a crook, picnic tables, barrels and a chalkboard (QUIZ THURS).", "ground, centre of the building; front +Z", False),
    "village.boatyard": (village_boatyard, "Kettle Cross boatyard", "A tarred boat shed, the crabber MARGIT (KX 12) up on its trailer, lobster pots and buoys, nets drying on poles.", "ground, centre of the shed; front +Z", False),
    "village.chapel": (village_chapel, "Highmoor Chapel", "A grey stone chapel with a steep slate roof, pointed windows, a bell gable and a round window over the door, in a walled yard with leaning headstones and a yew.", "ground, centre of the building; front +Z", False),
    "village.parade": (village_parade, "Eastgate parade", "A brick terrace of three shops with flats above on Eastgate High Street: the post office (with a post box), a bakery (awning), and SUDS LAUNDERETTE (machines through the glass).", "ground, centre of the terrace; front +Z", False),
    "village.weather-station": (village_weather_station, "Highmoor weather station", "A fenced plot with a white louvred screen, a lattice mast with anemometer cups and a vane, a rain gauge, a sunshine recorder and the observer's hut.", "ground, centre of the plot; front +Z", False),
}

if __name__ == "__main__":
    t.PROPS.clear()
    t.PROPS.update(PROPS)
    want = k.args() or ["all"]
    ids = list(PROPS) if want == ["all"] else [i for w in want for i in PROPS if i == w or (w.endswith("*") and i.startswith(w[:-1]))]
    results = [t.build(pid) for pid in ids]
    for r in results:
        print(f"BUILT {r['id']}: {r['tris']} tris, size {r['size']}")
    t.update_briefs(results)
    print(f"VILLAGE OK {len(results)} props")
