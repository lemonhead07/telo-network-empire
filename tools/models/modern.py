"""The later ages, modelled ahead of their systems: the handsets PIP lives in as the years go by, the screens of the
dashboard era, mobile (Act 6) and orbit (Act 7). Nothing places these yet; each is a prop with a brief, so the game
picks it up by id the day a system asks for it.

    blender -b --factory-startup --python tools/models/modern.py -- all

Godot coordinates through town.py's G()/gbox (x right, y up, the front faces +Z). Every screen a game view could be
drawn on is marked with an empty named screen (or screen-0, screen-1 ...) at the centre of its glass, facing +Z.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(__file__))
import _kit as k  # noqa: E402
import town as t  # noqa: E402
from town import G, M, gbox, gcyl, poly, slab, label, gable_roof, framed_window, person  # noqa: E402

k.PALETTE.update({
    "phone-black": (0.10, 0.10, 0.11), "phone-grey": (0.45, 0.46, 0.48), "phone-silver": (0.76, 0.77, 0.79), "glass-black": (0.04, 0.05, 0.07),
    "screen-lit": (0.30, 0.55, 0.85), "key-grey": (0.62, 0.63, 0.64), "antenna-black": (0.15, 0.15, 0.16), "lcd": (0.62, 0.70, 0.52),
    "panel-white": (0.92, 0.92, 0.90), "mast-grey": (0.66, 0.68, 0.70), "lamp-green": (0.30, 0.95, 0.40), "lamp-red": (0.95, 0.20, 0.16),
    "shop-glass": (0.42, 0.56, 0.66), "shop-white": (0.94, 0.94, 0.92), "telo-orange": (0.95, 0.48, 0.15), "floor-light": (0.80, 0.79, 0.76),
    "rocket-white": (0.93, 0.93, 0.91), "rocket-black": (0.14, 0.14, 0.15), "flame": (1.0, 0.65, 0.2), "gantry": (0.70, 0.22, 0.14),
    "solar": (0.12, 0.20, 0.42), "solar-line": (0.55, 0.60, 0.70), "foil": (0.88, 0.72, 0.30), "dish-white": (0.90, 0.90, 0.88),
    "drone-grey": (0.30, 0.31, 0.33), "prop-black": (0.08, 0.08, 0.09), "desk-white": (0.90, 0.89, 0.86), "wood-desk": (0.60, 0.46, 0.30),
    "bench-green": (0.20, 0.36, 0.26), "clock-face": (0.95, 0.94, 0.88), "stone": (0.62, 0.60, 0.55), "slate": (0.27, 0.29, 0.32),
    "pad-concrete": (0.60, 0.59, 0.56), "hi-vis": (0.95, 0.75, 0.15),
})


def screen_empty(name, c):
    k.empty(name, G(*c))


# ======================================================================================== PIP through the years
def phone_brick():
    """The brick phone (late 1980s): a grey slab with a long aerial, a tiny green LCD and a keypad."""
    w, h, d = 0.06, 0.23, 0.045
    gbox("body", (0, h / 2, 0), (w, h, d), "phone-grey", bevel=0.006)
    gcyl("aerial", (0.018, h, -0.01), 0.004, 0.11, "antenna-black", sides=6)
    gcyl("aerial-tip", (0.018, h + 0.11, -0.01), 0.006, 0.01, "antenna-black", sides=6)
    gbox("lcd", (0, h - 0.04, d / 2 + 0.001), (0.042, 0.022, 0.003), "lcd")
    screen_empty("screen", (0, h - 0.04, d / 2 + 0.003))
    for r in range(5):
        for c in range(3):
            gbox(f"key{r}{c}", (-0.015 + c * 0.015, 0.03 + r * 0.022, d / 2 + 0.002), (0.012, 0.014, 0.004), "key-grey")
    gbox("grille", (0, h - 0.012, d / 2 + 0.001), (0.03, 0.006, 0.002), "antenna-black")


def phone_flip():
    """The flip phone (late 1990s): silver clamshell opened, a colour screen in the lid, a stubby aerial."""
    w, d = 0.05, 0.018
    gbox("base", (0, 0.045, 0), (w, 0.09, d), "phone-silver", bevel=0.004)
    for r in range(4):
        for c in range(3):
            gbox(f"key{r}{c}", (-0.013 + c * 0.013, 0.015 + r * 0.016, d / 2 + 0.001), (0.01, 0.01, 0.002), "key-grey")
    k.cylinder("hinge", G(0, 0.092, 0), 0.007, w, M("phone-grey"), axis="X", sides=8)
    lid = gbox("lid", (0, 0.14, -0.006), (w, 0.095, 0.012), "phone-silver", bevel=0.004)
    gbox("lid-screen", (0, 0.145, 0.0005), (0.038, 0.05, 0.002), "screen-lit")
    screen_empty("screen", (0, 0.145, 0.002))
    gcyl("aerial", (0.018, 0.187, -0.006), 0.004, 0.02, "antenna-black", sides=6)
    del lid


def phone_touch():
    """The touchscreen phone (2007 on, never named): a black glass slab, one home button, a speaker slot."""
    w, h, d = 0.062, 0.118, 0.011
    gbox("body", (0, h / 2, 0), (w, h, d), "phone-black", bevel=0.004)
    gbox("rim", (0, h / 2, -0.001), (w + 0.002, h + 0.002, d - 0.004), "phone-silver")
    gbox("glass", (0, h / 2 + 0.002, d / 2 + 0.0005), (w - 0.008, h - 0.03, 0.001), "glass-black")
    screen_empty("screen", (0, h / 2 + 0.002, d / 2 + 0.001))
    k.cylinder("home", G(0, 0.0075, d / 2 + 0.0005), 0.0045, 0.001, M("phone-grey"), axis="Y", sides=10)
    gbox("speaker", (0, h - 0.008, d / 2 + 0.0006), (0.012, 0.0015, 0.001), "phone-grey")
    gbox("camera", (0.02, h - 0.01, -d / 2 - 0.0005), (0.006, 0.006, 0.001), "glass-black")


# ======================================================================================== screens of the dashboard era
def room_flat_monitor():
    """A flat monitor on a stand (2000s on): thin bezel, a power light. The game draws the view on `screen`."""
    w, h = 0.56, 0.34
    gbox("panel", (0, 0.42, 0), (w + 0.03, h + 0.03, 0.04), "phone-black", bevel=0.005)
    gbox("glass", (0, 0.42, 0.0205), (w, h, 0.002), "glass-black")
    screen_empty("screen", (0, 0.42, 0.022))
    gbox("neck", (0, 0.17, -0.04), (0.06, 0.26, 0.03), "phone-grey")
    gbox("foot", (0, 0.01, -0.02), (0.24, 0.02, 0.18), "phone-grey")
    gbox("led", (w / 2 - 0.01, 0.42 - h / 2 - 0.008, 0.021), (0.006, 0.004, 0.002), "lamp-green")


def room_laptop():
    """An open laptop: keyboard deck, trackpad, the lid tilted back. `screen` at the middle of the lid's glass."""
    w, d = 0.33, 0.23
    gbox("deck", (0, 0.009, 0), (w, 0.018, d), "phone-silver", bevel=0.004)
    gbox("keys", (0, 0.0185, -0.02), (w - 0.04, 0.001, 0.1), "phone-black")
    gbox("pad", (0, 0.0185, 0.07), (0.1, 0.001, 0.06), "phone-grey")
    a = math.radians(15)
    lh = 0.21
    cy, cz = 0.018 + math.cos(a) * lh / 2, -d / 2 - math.sin(a) * lh / 2
    o = gbox("lid", (0, cy, cz), (w, lh, 0.008), "phone-silver", bevel=0.003)
    o.rotation_euler = (-a, 0, 0)
    g = gbox("glass", (0, cy + 0.004 * math.sin(a), cz + 0.0045 * math.cos(a)), (w - 0.02, lh - 0.025, 0.001), "glass-black")
    g.rotation_euler = (-a, 0, 0)
    screen_empty("screen", (0, cy, cz + 0.006))


def room_ops_wall():
    """The operations room's wall of screens (Acts 6-7): a 4 x 2 grid of big panels on a frame, a long desk in
    front with three monitors, a ticker strip along the top. Empties screen-0..7 (wall) and desk-0..2."""
    cols, rows, pw, ph = 4, 2, 1.2, 0.7
    gbox("frame", (0, 1.9, -0.05), (cols * pw + 0.3, rows * ph + 0.3, 0.1), "phone-black")
    n = 0
    for r in range(rows):
        for c in range(cols):
            x, y = -(cols - 1) * pw / 2 + c * pw, 1.55 + r * ph
            gbox(f"panel{n}", (x, y, 0.005), (pw - 0.03, ph - 0.03, 0.01), "glass-black")
            screen_empty(f"screen-{n}", (x, y, 0.012))
            n += 1
    gbox("ticker", (0, 2.75, 0.01), (cols * pw, 0.12, 0.02), "phone-black")
    gbox("ticker-text", (0, 2.75, 0.021), (cols * pw - 0.1, 0.06, 0.002), "telo-orange")
    # the desk, curved in three straight pieces
    for i, (x, rot) in enumerate(((-1.6, 0.25), (0, 0), (1.6, -0.25))):
        gbox(f"desk{i}", (x, 0.74, 1.6 + abs(x) * 0.12), (1.6, 0.04, 0.8), "desk-white", rot_y=rot)
        gbox(f"desk-leg{i}", (x, 0.36, 1.6 + abs(x) * 0.12), (1.5, 0.72, 0.05), "phone-grey", rot_y=rot)
        gbox(f"mon{i}", (x, 0.98, 1.4 + abs(x) * 0.12), (0.55, 0.33, 0.03), "phone-black", rot_y=rot)
        gbox(f"mon-glass{i}", (x, 0.98, 1.417 + abs(x) * 0.12), (0.52, 0.3, 0.002), "glass-black", rot_y=rot)
        screen_empty(f"desk-{i}", (x, 0.98, 1.42 + abs(x) * 0.12))
        gbox(f"mon-neck{i}", (x, 0.82, 1.38 + abs(x) * 0.12), (0.05, 0.14, 0.03), "phone-grey", rot_y=rot)
    label("brand", "TELO OPERATIONS", (0, 2.75 + 0.16, 0.01), 0.07, "shop-white")


# ======================================================================================== mobile (Act 6)
def mast_small_cell_bench():
    """Not In My Park: a park bench with a small cell hidden in it. The fat back rail is the antenna; a little
    green box under the seat, a plaque thanking the council, and one blinking light that gives it away."""
    gbox("seat", (0, 0.45, 0), (1.8, 0.05, 0.45), "wood-desk")
    gbox("back-rail", (0, 0.82, -0.22), (1.8, 0.28, 0.12), "wood-desk")
    for sx in (-0.8, 0.8):
        gbox(f"leg{sx}", (sx, 0.22, 0), (0.07, 0.45, 0.5), "bench-green")
        gbox(f"arm{sx}", (sx, 0.62, 0.0), (0.07, 0.05, 0.45), "bench-green")
        k.beam(f"back-strut{sx}", G(sx, 0.45, -0.18), G(sx, 0.9, -0.24), 0.06, M("bench-green"))
    gbox("box", (0, 0.2, -0.05), (0.5, 0.25, 0.3), "bench-green")
    gbox("vent", (0, 0.2, 0.101), (0.4, 0.12, 0.002), "antenna-black")
    gbox("plaque", (0, 0.82, -0.155), (0.2, 0.06, 0.004), "foil")
    label("plaque-text", "WITH THANKS", (0, 0.82, -0.152), 0.018, "antenna-black")
    gbox("blink", (0.86, 0.95, -0.22), (0.015, 0.015, 0.015), "lamp-red")
    gbox("duct", (0, 0.02, -0.1), (0.15, 0.04, 0.6), "antenna-black")


def mast_clock_tower_cell():
    """The chaos shortcut: a village clock tower with three panel antennas poking out of the belfry louvres."""
    w, h = 3.0, 12.0
    gbox("tower", (0, h / 2, 0), (w, h, w), "stone")
    for i in range(int(h / 0.6)):
        gbox(f"course{i}", (0, 0.3 + i * 0.6, w / 2 + 0.005), (w, 0.03, 0.01), "slate")
    k.cylinder("clock", G(0, h - 3.2, w / 2 + 0.03), 0.8, 0.06, M("clock-face"), axis="Y", sides=16)
    k.beam("hand-h", G(0, h - 3.2, w / 2 + 0.07), G(0.3, h - 2.9, w / 2 + 0.07), 0.05, M("antenna-black"))
    k.beam("hand-m", G(0, h - 3.2, w / 2 + 0.07), G(0, h - 2.55, w / 2 + 0.07), 0.035, M("antenna-black"))
    for i in range(4):
        gbox(f"louvre{i}", (0, h - 1.6 + i * 0.25, w / 2 + 0.01), (1.2, 0.08, 0.06), "slate")
    poly("spire", [(-w / 2 - 0.1, h, -w / 2 - 0.1), (w / 2 + 0.1, h, -w / 2 - 0.1), (w / 2 + 0.1, h, w / 2 + 0.1), (-w / 2 - 0.1, h, w / 2 + 0.1), (0, h + 3.5, 0)],
         [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (0, 3, 2, 1)], "slate")
    for i, (x, z, rot) in enumerate(((-0.4, w / 2 + 0.2, 0), (w / 2 + 0.2, 0.3, math.pi / 2), (-w / 2 - 0.2, -0.3, -math.pi / 2))):
        gbox(f"antenna{i}", (x, h - 1.2, z), (0.3, 1.3, 0.12), "panel-white", rot_y=rot)
    k.beam("cable", G(w / 2 + 0.05, h - 1.0, 0.5), G(w / 2 + 0.05, 0.3, 0.5), 0.05, M("antenna-black"))
    gbox("cabinet", (w / 2 + 0.5, 0.75, 0.5), (0.6, 1.5, 0.6), "phone-grey")
    gbox("sticker", (w / 2 + 0.81, 1.1, 0.5), (0.01, 0.2, 0.3), "hi-vis")


def shop_phone_shop():
    """TELO Mobile: a bright shopfront for the touchscreen years. Glass front, orange fascia, a counter, tables
    of phones on stands, a poster with a phone the size of a door. Footprint 10 x 8 (Sol's lot, in time)."""
    w, d, h = 10.0, 8.0, 4.0
    gbox("floor", (0, 0.06, 0), (w, 0.12, d), "floor-light")
    gbox("back", (0, h / 2, -d / 2 + 0.1), (w, h, 0.2), "shop-white")
    for s in (-1, 1):
        gbox(f"side{s}", (s * (w / 2 - 0.1), h / 2, 0), (0.2, h, d), "shop-white")
    gbox("roof", (0, h + 0.1, 0), (w + 0.2, 0.2, d + 0.2), "phone-grey")
    # the glass front: panes, a door gap in the middle, the fascia
    for x in (-3.4, -1.6, 1.6, 3.4):
        gbox(f"pane{x}", (x, 1.5, d / 2 - 0.05), (1.75, 3.0, 0.04), "shop-glass")
        gbox(f"mullion{x}", (x + 0.9 * (1 if x > 0 else -1), 1.5, d / 2 - 0.03), (0.06, 3.0, 0.08), "phone-grey")
    gbox("fascia", (0, 3.5, d / 2), (w + 0.1, 1.0, 0.2), "telo-orange")
    label("fascia-text", "TELO  mobile", (0, 3.5, d / 2 + 0.11), 0.55, "shop-white")
    # inside: tables with phones on stands, a counter, a door-sized poster of a phone
    for i, x in enumerate((-2.5, 2.5)):
        gbox(f"table{i}", (x, 0.85, 0.8), (1.8, 0.06, 0.8), "shop-white")
        gbox(f"table-leg{i}", (x, 0.43, 0.8), (0.2, 0.8, 0.2), "phone-grey")
        for j in range(4):
            gbox(f"stand{i}{j}", (x - 0.6 + j * 0.4, 0.95, 0.8), (0.07, 0.13, 0.015), "phone-black")
            gbox(f"stand-screen{i}{j}", (x - 0.6 + j * 0.4, 0.955, 0.8085), (0.06, 0.1, 0.002), "screen-lit")
    gbox("counter", (0, 0.55, -2.4), (4.0, 1.1, 0.7), "telo-orange")
    gbox("counter-top", (0, 1.12, -2.4), (4.1, 0.05, 0.8), "shop-white")
    gbox("till", (1.0, 1.25, -2.4), (0.35, 0.2, 0.3), "phone-black")
    gbox("poster", (0, 2.0, -d / 2 + 0.21), (1.0, 2.0, 0.02), "phone-black")
    gbox("poster-screen", (0, 2.05, -d / 2 + 0.225), (0.85, 1.6, 0.005), "screen-lit")
    label("poster-text", "NO BUTTONS.", (0, 3.25, -d / 2 + 0.225), 0.16, "telo-orange")
    # outside: the launch queue's barrier and an A-board
    for i in range(6):
        gcyl(f"q-post{i}", (-4.5 + i * 1.3, 0.0, d / 2 + 1.5), 0.04, 1.0, "phone-silver", sides=8)
        if i < 5:
            k.beam(f"q-rope{i}", G(-4.5 + i * 1.3, 0.9, d / 2 + 1.5), G(-3.2 + i * 1.3, 0.9, d / 2 + 1.5), 0.04, M("telo-orange"))
    for sz in (-1, 1):
        k.beam(f"aboard{sz}", G(4.3, 0, d / 2 + 1.0 + sz * 0.25), G(4.3, 1.0, d / 2 + 1.0), 0.05, M("phone-grey"))
    gbox("aboard-face", (4.3, 0.55, d / 2 + 1.12), (0.55, 0.75, 0.03), "shop-white")
    label("aboard-text", "OPEN 8AM", (4.3, 0.7, d / 2 + 1.14), 0.07, "telo-orange")


# ======================================================================================== orbit (Act 7)
def space_satellite():
    """A small communications satellite: a gold-foil body, two solar wings, a dish and a horn. About 4 m span."""
    gbox("body", (0, 0, 0), (0.9, 0.9, 0.9), "foil", bevel=0.03)
    for s in (-1, 1):
        k.beam(f"boom{s}", G(s * 0.45, 0, 0), G(s * 0.85, 0, 0), 0.05, M("phone-grey"))
        gbox(f"wing{s}", (s * 1.75, 0, 0), (1.8, 0.02, 0.7), "solar")
        for i in range(5):
            gbox(f"wing-line{s}{i}", (s * (0.95 + i * 0.36), 0.012, 0), (0.02, 0.005, 0.7), "solar-line")
    k.cone("dish", G(0, 0, 0.75), 0.42, 0.08, 0.25, M("dish-white"), sides=14, rot=(math.pi / 2, 0, 0))
    gcyl("feed", (0, -0.05, 0.9), 0.03, 0.35, "phone-grey", sides=6)
    k.cone("horn", G(0.3, 0.55, 0.1), 0.06, 0.12, 0.2, M("phone-grey"), sides=8)
    gbox("thruster", (0, -0.5, 0), (0.2, 0.1, 0.2), "rocket-black")


def space_rocket():
    """The launcher at Cape Lantern: two stages, a fairing, four fins, TELO up the side. 24 m tall."""
    r, h = 1.0, 24.0
    gcyl("stage1", (0, 0.8, 0), r, 13.0, "rocket-white", sides=16)
    gcyl("interstage", (0, 13.8, 0), r, 0.6, "rocket-black", sides=16)
    gcyl("stage2", (0, 14.4, 0), r * 0.95, 5.5, "rocket-white", sides=16)
    gcyl("fairing", (0, 19.9, 0), r * 1.05, 2.2, "rocket-white", sides=16)
    gcyl("nose", (0, 22.1, 0), r * 1.05, 1.9, "rocket-white", sides=16, r2=0.05)
    for i in range(4):
        a = i * math.pi / 2
        x, z = math.cos(a), math.sin(a)
        poly(f"fin{i}", [(x * r, 0.8, z * r), (x * 2.1, 0.0, z * 2.1), (x * 2.1, 1.4, z * 2.1), (x * r, 4.0, z * r)], [(0, 1, 2, 3)], "rocket-black")
    gcyl("engine", (0, 0.0, 0), 0.7, 0.8, "rocket-black", sides=12, r2=0.9)
    for i in range(3):
        gcyl(f"band{i}", (0, 4.0 + i * 3.5, 0), r + 0.01, 0.25, "rocket-black", sides=16)
    label("name", "T E L O", (0, 8.5, r + 0.02), 0.9, "telo-orange")
    k.empty("payload", G(0, 20.5, 0))


def space_launch_pad():
    """Cape Lantern's pad: a concrete deck with a flame trench, a red lattice tower with swing arms, floodlights,
    a lightning mast. The rocket stands on `rocket-foot`."""
    gbox("deck", (0, 0.5, 0), (24, 1.0, 24), "pad-concrete")
    gbox("trench", (0, 0.95, 6), (5, 0.2, 14), "rocket-black")
    tx, tz, th = -5.5, 0.0, 28.0
    for sx in (-1, 1):
        for sz in (-1, 1):
            gbox(f"tower-leg{sx}{sz}", (tx + sx * 1.2, 1 + th / 2, tz + sz * 1.2), (0.25, th, 0.25), "gantry")
    for i in range(int(th / 3)):
        y = 1.5 + i * 3
        for sz in (-1, 1):
            k.beam(f"x-a{i}{sz}", G(tx - 1.2, y, tz + sz * 1.2), G(tx + 1.2, y + 3, tz + sz * 1.2), 0.12, M("gantry"))
        for sx in (-1, 1):
            k.beam(f"x-b{i}{sx}", G(tx + sx * 1.2, y, tz - 1.2), G(tx + sx * 1.2, y + 3, tz + 1.2), 0.12, M("gantry"))
        gbox(f"floor{i}", (tx, y, tz), (2.6, 0.12, 2.6), "gantry")
    for y in (14.5, 21.0):
        gbox(f"arm{y}", (tx + 3.0, y, tz), (3.6, 0.5, 0.8), "gantry")
    gcyl("lightning", (8, 1.0, -8), 0.2, 34, "phone-grey", sides=6)
    for x, z in ((10, 10), (-10, 10), (10, -10)):
        gcyl(f"flood{x}{z}", (x, 1.0, z), 0.15, 8, "phone-grey", sides=6)
        gbox(f"flood-head{x}{z}", (x, 9.2, z), (1.2, 0.6, 0.4), "panel-white")
    label("pad", "CAPE LANTERN  PAD 1", (0, 1.01, 11.5), 0.8, "hi-vis", face="up")
    k.empty("rocket-foot", G(0, 1.0, 0))


def space_ground_dish():
    """A ground-station dish on a concrete plinth: big white bowl tilted at the sky, feed on struts, a hut."""
    gbox("plinth", (0, 0.5, 0), (3.0, 1.0, 3.0), "pad-concrete")
    gcyl("pedestal", (0, 1.0, 0), 0.6, 3.0, "dish-white", sides=10)
    tilt = math.radians(40)
    o = k.cone("bowl", G(0, 5.2, 0.4), 3.2, 0.6, 1.2, M("dish-white"), sides=18, rot=(math.pi / 2 - tilt, 0, 0))
    del o
    fy, fz = 5.2 + math.sin(tilt) * 3.0, 0.4 + math.cos(tilt) * 3.0
    gcyl("feed", (0, fy - 0.3, fz), 0.18, 0.6, "phone-grey", sides=8)
    for a in (0, 2.094, 4.189):
        k.beam(f"strut{a:.1f}", G(math.cos(a) * 2.6, 5.2 + math.sin(a) * 1.2, 0.4 + 0.6), G(0, fy, fz), 0.07, M("phone-grey"))
    gbox("hut", (3.5, 1.2, -1.0), (2.4, 2.4, 2.0), "panel-white")
    gbox("hut-door", (3.5, 1.0, 0.01), (0.8, 2.0, 0.04), "telo-orange")


def kit_drone():
    """A crew drone for Act 7: four arms, four propellers, a camera under the nose, a TELO orange stripe."""
    gbox("body", (0, 0.08, 0), (0.22, 0.08, 0.3), "drone-grey", bevel=0.02)
    gbox("stripe", (0, 0.122, 0), (0.06, 0.005, 0.3), "telo-orange")
    for sx in (-1, 1):
        for sz in (-1, 1):
            k.beam(f"arm{sx}{sz}", G(0, 0.09, 0), G(sx * 0.28, 0.1, sz * 0.28), 0.03, M("drone-grey"))
            gcyl(f"motor{sx}{sz}", (sx * 0.28, 0.08, sz * 0.28), 0.025, 0.05, "prop-black", sides=8)
            gbox(f"prop{sx}{sz}", (sx * 0.28, 0.135, sz * 0.28), (0.3, 0.004, 0.03), "prop-black", rot_y=0.4 * sx * sz)
            gbox(f"leg{sx}{sz}", (sx * 0.08, 0.02, sz * 0.1), (0.015, 0.06, 0.015), "prop-black")
    gcyl("camera", (0, 0.0, 0.12), 0.035, 0.05, "prop-black", sides=8)
    gbox("lamp", (0, 0.06, -0.155), (0.03, 0.015, 0.01), "lamp-red")


PROPS = {
    "phone.brick": (phone_brick, "Brick phone", "PIP in the late 1980s: a grey slab, a long aerial, a green LCD (empty screen), a keypad.", "bottom centre; screen faces +Z", False),
    "phone.flip": (phone_flip, "Flip phone", "PIP around 2000: a silver clamshell opened, a colour screen in the lid (empty screen).", "bottom centre; screen faces +Z", False),
    "phone.touch": (phone_touch, "The touchscreen phone", "PIP from 2007: a black glass slab with one button (empty screen). Never given a brand.", "bottom centre; screen faces +Z", False),
    "room.flat-monitor": (room_flat_monitor, "Flat monitor", "A thin-bezelled monitor on a stand for the database and dashboard years (empty screen).", "foot, centre", False),
    "room.laptop": (room_laptop, "Laptop", "An open silver laptop, lid tilted back (empty screen on the lid's glass).", "base centre", False),
    "room.ops-wall": (room_ops_wall, "Operations wall", "Acts 6-7: a 4 x 2 wall of big screens (screen-0..7), a ticker, and a long desk with three monitors (desk-0..2).", "floor, centre of the wall", False),
    "mast.small-cell-bench": (mast_small_cell_bench, "Small cell bench", "Not In My Park: a park bench with a small cell in its fat back rail, a box under the seat, a WITH THANKS plaque, one blinking light.", "ground, centre", False),
    "mast.clock-tower-cell": (mast_clock_tower_cell, "Clock tower cell site", "A stone clock tower with three white panel antennas poking out of the belfry, a cable down the side, a cabinet at the foot.", "ground, centre", False),
    "shop.phone-shop": (shop_phone_shop, "TELO Mobile shop", "A glass-fronted phone shop with an orange fascia, phones on stands, a counter, a NO BUTTONS. poster, the launch-day queue barrier.", "ground, centre of footprint; front +Z", False),
    "space.satellite": (space_satellite, "Satellite", "Gold-foil body, two blue solar wings, a dish and a horn; about 4 m across.", "centre of the body", False),
    "space.rocket": (space_rocket, "Launcher", "A two-stage white rocket with black bands and fins, T E L O up the side, 24 m tall. Empty payload.", "base of the engine", False),
    "space.launch-pad": (space_launch_pad, "Cape Lantern pad", "A concrete pad with a flame trench, a red lattice tower with swing arms, floodlights and a lightning mast. Empty rocket-foot.", "ground, centre of the deck", False),
    "space.ground-dish": (space_ground_dish, "Ground station dish", "A big white dish tilted at the sky on a pedestal, feed on three struts, a hut with an orange door.", "ground, centre", False),
    "kit.drone": (kit_drone, "Crew drone", "A grey quadcopter with an orange stripe, a camera under the nose and a red tail light.", "ground under the body", False),
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
    print(f"MODERN OK {len(results)} props")
