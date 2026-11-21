"""The Data Centre on Halden Flats (Act 5, The Core): the building, its rooms' kit, the rack and its boxes, the pin
board, and the people who work there. Prop ids are the ones DATA_CENTRE_DESIGN.md §12 asks for, so the game's
Props.Make("dc.rack-42u") etc. picks these up with no code change.

    blender -b --factory-startup --python tools/models/dc.py -- all
    blender -b --factory-startup --python tools/models/dc.py -- dc.rack-42u "dc.box-*"

Godot coordinates through town.py's G()/gbox (x right, y up, the front faces +Z). Pivots are given per prop in the
registry at the bottom; rack kit follows 19-inch sizes (1U = 44.45 mm, 0.4826 m across the ears).
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
    "rack-black": (0.12, 0.12, 0.13), "rack-grey": (0.30, 0.31, 0.33), "rail": (0.66, 0.67, 0.68), "kit-beige": (0.84, 0.80, 0.70),
    "kit-blue": (0.22, 0.30, 0.42), "kit-grey": (0.55, 0.56, 0.58), "kit-dark": (0.20, 0.21, 0.23), "port": (0.06, 0.06, 0.07),
    "lamp-green": (0.30, 0.95, 0.40), "lamp-amber": (1.0, 0.72, 0.18), "lamp-red": (0.95, 0.20, 0.16), "fibre": (0.98, 0.84, 0.15),
    "copper-lead": (0.22, 0.45, 0.85), "power-red": (0.80, 0.15, 0.12), "tape": (0.95, 0.90, 0.40), "cork": (0.72, 0.55, 0.36),
    "map-paper": (0.93, 0.90, 0.80), "map-sea": (0.62, 0.76, 0.84), "map-land": (0.80, 0.84, 0.66), "pin-brass": (0.85, 0.68, 0.25),
    "string-brown": (0.50, 0.32, 0.18), "string-white": (0.95, 0.95, 0.92), "string-blue": (0.20, 0.40, 0.85), "bulb": (1.0, 0.92, 0.55),
    "cladding": (0.72, 0.74, 0.76), "cladding-dark": (0.58, 0.60, 0.63), "trim-blue": (0.18, 0.30, 0.52), "slab": (0.66, 0.65, 0.62),
    "cabin": (0.88, 0.86, 0.78), "cabin-trim": (0.36, 0.48, 0.38), "gen-yellow": (0.92, 0.72, 0.14), "battery": (0.18, 0.18, 0.20),
    "crac": (0.86, 0.86, 0.84), "grille": (0.30, 0.31, 0.32), "mesh": (0.50, 0.52, 0.54), "desk-top": (0.74, 0.70, 0.62),
    "monitor": (0.80, 0.78, 0.72), "screen-dark": (0.05, 0.10, 0.08), "chair": (0.20, 0.22, 0.30), "polo-navy": (0.16, 0.22, 0.38),
    "shirt-check": (0.62, 0.48, 0.40), "cardigan-green": (0.32, 0.42, 0.30), "fleece": (0.35, 0.36, 0.42), "dial-face": (0.96, 0.95, 0.90),
    "lanyard": (0.85, 0.20, 0.18), "crate": (0.70, 0.56, 0.36), "floor-tile": (0.78, 0.78, 0.76),
})

U = 0.04445          # one rack unit
EAR = 0.4826         # 19-inch across the mounting ears
BODY = 0.44          # box body width between the rails


# ---------------------------------------------------------------------------------------- small shared pieces
def ports_row(n, x0, y, z, count, pitch=0.016, colour="port", w=0.012, h=0.011):
    for i in range(count):
        gbox(f"{n}{i}", (x0 + i * pitch, y, z), (w, h, 0.006), colour)


def rack_box(n, units, body="kit-dark", face=None, depth=0.45):
    """A 19-inch box: body, front panel with ears and four screws; returns (front z, centre y, height)."""
    h = units * U - 0.001
    face = face or body
    gbox(f"{n}-body", (0, h / 2, -depth / 2), (BODY, h, depth), body)
    gbox(f"{n}-face", (0, h / 2, 0.002), (EAR, h, 0.004), face)
    for sx in (-1, 1):
        for sy in ((0.5,) if units == 1 else (0.25, 0.75)):
            gcyl(f"{n}-screw{sx}{sy}", (sx * 0.232, h * sy - 0.002, 0.002), 0.004, 0.004, "rail", sides=6)
    return 0.006, h / 2, h


def chair(n, x, z, rot=0.0):
    gcyl(f"{n}-base", (x, 0.0, z), 0.3, 0.05, "rack-black", sides=5)
    gcyl(f"{n}-stem", (x, 0.05, z), 0.03, 0.4, "rail", sides=6)
    gbox(f"{n}-seat", (x, 0.48, z), (0.46, 0.08, 0.46), "chair", rot_y=rot, bevel=0.02)
    dx, dz = -math.sin(rot) * 0.22, -math.cos(rot) * 0.22
    gbox(f"{n}-back", (x + dx, 0.8, z + dz), (0.44, 0.5, 0.06), "chair", rot_y=rot, bevel=0.02)


def crt_monitor(n, x, y, z, s=0.36, screen="screen-dark", rot=0.0):
    """A boxy monitor of the period on its desk, facing +Z (turned by rot). Its screen face is a named node
    `n-screen` the game can draw onto."""
    c, sn = math.cos(rot), math.sin(rot)
    def R(dx, dz):
        return (x + dx * c + dz * sn, z - dx * sn + dz * c)
    bx, bz = R(0, -s * 0.2)
    gbox(f"{n}-case", (bx, y + s * 0.42, bz), (s, s * 0.8, s * 0.9), "monitor", rot_y=rot, bevel=0.015)
    sx_, sz_ = R(0, s * 0.255)
    gbox(f"{n}-screen", (sx_, y + s * 0.44, sz_), (s * 0.8, s * 0.6, 0.01), screen, rot_y=rot)
    fx, fz = R(0, -s * 0.15)
    gbox(f"{n}-foot", (fx, y + 0.01, fz), (s * 0.5, 0.02, s * 0.45), "monitor", rot_y=rot)
    lx, lz = R(s * 0.34, s * 0.255)
    gbox(f"{n}-led", (lx, y + s * 0.12, lz), (0.012, 0.012, 0.006), "lamp-green", rot_y=rot)


# ======================================================================================== the rack and its boxes
def dc_rack_42u():
    """An open-front 42U rack: frame, side panels, numbered rails, a cable tray on top, a blanking-tape strip."""
    w, d, h = 0.6, 1.0, 2.0
    for sx in (-1, 1):
        for sz in (-1, 1):
            gbox(f"post{sx}{sz}", (sx * (w / 2 - 0.02), h / 2, sz * (d / 2 - 0.02)), (0.04, h, 0.04), "rack-black")
        gbox(f"side{sx}", (sx * (w / 2 - 0.005), h / 2, 0), (0.01, h - 0.1, d - 0.08), "rack-grey")
        for i in range(6):
            gbox(f"vent{sx}{i}", (sx * (w / 2 + 0.001), 0.3 + i * 0.28, 0), (0.004, 0.02, d - 0.3), "rack-black")
    for y in (0.04, h - 0.02):
        gbox(f"frame-f{y}", (0, y, d / 2 - 0.02), (w, 0.04, 0.04), "rack-black")
        gbox(f"frame-b{y}", (0, y, -d / 2 + 0.02), (w, 0.04, 0.04), "rack-black")
        for sx in (-1, 1):
            gbox(f"frame-s{y}{sx}", (sx * (w / 2 - 0.02), y, 0), (0.04, 0.04, d), "rack-black")
    gbox("roof", (0, h + 0.005, 0), (w, 0.01, d), "rack-black")
    # the four mounting rails, front and back, with U marks every five units
    y0 = 0.1
    for sz in (d / 2 - 0.1, -d / 2 + 0.15):
        for sx in (-1, 1):
            gbox(f"rail{sx}{sz}", (sx * 0.235, y0 + 21 * U, sz), (0.02, 42 * U, 0.03), "rail")
    for u in range(0, 43, 5):
        gbox(f"umark{u}", (-0.255, y0 + u * U, d / 2 - 0.085), (0.02, 0.004, 0.002), "label")
    # feet, castors, a plinth; a cable tray and ladder on the roof; a nameplate
    for sx in (-1, 1):
        for sz in (-1, 1):
            gcyl(f"foot{sx}{sz}", (sx * (w / 2 - 0.06), -0.02, sz * (d / 2 - 0.06)), 0.03, 0.04, "rail", sides=6)
    for i in range(5):
        gbox(f"tray-rung{i}", (0, h + 0.06, -d / 2 + 0.1 + i * 0.2), (0.4, 0.02, 0.03), "rail")
    for sx in (-1, 1):
        gbox(f"tray-side{sx}", (sx * 0.2, h + 0.07, 0), (0.02, 0.06, d), "rail")
    gbox("plate", (0, h - 0.06, d / 2 + 0.002), (0.24, 0.06, 0.004), "label")
    label("plate-text", "RACK 1", (0, h - 0.06, d / 2 + 0.005), 0.035, "ink")
    # bodge charm: tape across the empty slots, written on
    gbox("tape", (0, y0 + 30 * U, d / 2 - 0.08), (EAR, 0.03, 0.002), "tape")
    label("tape-text", "DO NOT USE - DANA", (0, y0 + 30 * U, d / 2 - 0.077), 0.016, "ink")
    for i, name in enumerate(("u01", "u42")):
        k.empty(name, G(0, y0 + (0 if i == 0 else 41) * U, d / 2 - 0.1))


def dc_rack_12u():
    """A 12U floor cabinet for the exchange hut: glass door, a lock, a small fan on top."""
    w, d, h = 0.6, 0.6, 0.72
    gbox("back", (0, h / 2, -d / 2 + 0.01), (w, h, 0.02), "rack-grey")
    for sx in (-1, 1):
        gbox(f"side{sx}", (sx * (w / 2 - 0.01), h / 2, 0), (0.02, h, d), "rack-grey")
    gbox("top", (0, h - 0.01, 0), (w, 0.02, d), "rack-grey")
    gbox("base", (0, 0.03, 0), (w, 0.06, d), "rack-black")
    for sx in (-1, 1):
        gbox(f"rail{sx}", (sx * 0.235, 0.08 + 6 * U, d / 2 - 0.08), (0.02, 12 * U, 0.03), "rail")
    gbox("door-frame", (0, h / 2, d / 2 + 0.01), (w, h, 0.02), "rack-black")
    gbox("door-glass", (0, h / 2, d / 2 + 0.022), (w - 0.08, h - 0.08, 0.004), "glass")
    gbox("lock", (w / 2 - 0.05, h / 2, d / 2 + 0.03), (0.02, 0.08, 0.02), "rail")
    gcyl("fan", (0, h, 0), 0.06, 0.02, "grille", sides=8)


def dc_wallbox_6u():
    """A 6U wall box for a street cabinet: hinged to the wall, a smoked door, a pole-line label."""
    w, d, h = 0.55, 0.45, 0.37
    gbox("back", (0, 0, -d + 0.01), (w, h, 0.02), "rack-grey")
    for sx in (-1, 1):
        gbox(f"side{sx}", (sx * (w / 2 - 0.01), 0, -d / 2), (0.02, h, d), "rack-grey")
    for sy in (-1, 1):
        gbox(f"tb{sy}", (0, sy * (h / 2 - 0.01), -d / 2), (w, 0.02, d), "rack-grey")
    gbox("door", (0, 0, 0.005), (w, h, 0.01), "rack-black")
    gbox("door-glass", (0, 0, 0.012), (w - 0.06, h - 0.06, 0.004), "glass")
    gbox("label", (0, h / 2 - 0.04, 0.016), (0.2, 0.03, 0.002), "label")
    label("label-text", "TELO 6U", (0, h / 2 - 0.04, 0.018), 0.018, "ink")


def dc_box_router():
    """2U router: a row of line modules, each with ports and a lamp; the badge says only ROUTER."""
    fz, cy, h = rack_box("r", 2, body="kit-blue", face="kit-blue")
    for m in range(4):
        x = -0.16 + m * 0.085
        gbox(f"module{m}", (x, cy, fz), (0.08, h - 0.012, 0.004), "kit-dark")
        ports_row(f"mod{m}-port", x - 0.025, cy - 0.01, fz + 0.003, 4, pitch=0.017)
        gbox(f"mod{m}-lamp", (x + 0.03, cy + 0.025, fz + 0.003), (0.008, 0.006, 0.004), "lamp-green")
    gbox("badge", (0.19, cy + 0.02, fz + 0.002), (0.05, 0.016, 0.003), "label")
    label("badge-text", "ROUTER", (0.19, cy + 0.02, fz + 0.004), 0.008, "ink")
    for i, c in enumerate(("lamp-green", "lamp-green", "lamp-amber")):
        gbox(f"status{i}", (0.175 + i * 0.012, cy - 0.015, fz + 0.003), (0.006, 0.006, 0.004), c)
    k.empty("port-1", G(-0.185, cy - 0.01, fz + 0.006))


def dc_box_switch():
    """1U switch: 24 ports in two rows, a lamp over each; customers and servers plug in here."""
    fz, cy, h = rack_box("s", 1, body="kit-grey", face="kit-beige")
    ports_row("port-top", -0.19, cy + 0.006, fz + 0.003, 12, pitch=0.025, w=0.014, h=0.01)
    ports_row("port-bot", -0.19, cy - 0.01, fz + 0.003, 12, pitch=0.025, w=0.014, h=0.01)
    for i in range(12):
        gbox(f"lamp{i}", (-0.19 + i * 0.025 + 0.009, cy + 0.016, fz + 0.003), (0.004, 0.003, 0.003), "lamp-green" if i % 5 else "lamp-amber")
    gbox("power-lamp", (0.2, cy, fz + 0.003), (0.006, 0.006, 0.004), "lamp-green")
    k.empty("port-1", G(-0.19, cy + 0.006, fz + 0.006))


def dc_box_patch():
    """1U patch panel: 24 sockets and a white write-on strip above them, every one labelled in someone's hand."""
    fz, cy, h = rack_box("p", 1, body="rack-black", face="rack-black")
    gbox("strip", (0, cy + 0.013, fz + 0.002), (0.42, 0.012, 0.002), "label")
    ports_row("socket", -0.2, cy - 0.006, fz + 0.003, 24, pitch=0.0174, w=0.012, h=0.011)
    for i in range(0, 24, 3):
        gbox(f"scrawl{i}", (-0.2 + i * 0.0174 + 0.01, cy + 0.013, fz + 0.004), (0.03, 0.003, 0.001), "ink")
    k.empty("port-1", G(-0.2, cy - 0.006, fz + 0.006))


def dc_box_fibretray():
    """1U fibre drawer, pulled out a little: yellow ports with dust caps, a coil of fibre inside."""
    fz, cy, h = rack_box("f", 1, body="rack-grey", face="kit-grey")
    gbox("drawer", (0, cy - 0.004, 0.06), (0.42, 0.012, 0.12), "kit-grey")
    for i in range(12):
        x = -0.17 + i * 0.03
        gbox(f"adapter{i}", (x, cy + 0.004, fz + 0.003), (0.014, 0.012, 0.006), "fibre")
        if i % 3:
            gbox(f"cap{i}", (x, cy + 0.004, fz + 0.008), (0.01, 0.009, 0.006), "port")
    gcyl("coil", (0.0, cy - 0.006, 0.06), 0.045, 0.006, "fibre", sides=12)
    gcyl("coil-hole", (0.0, cy - 0.005, 0.06), 0.03, 0.007, "kit-grey", sides=12)
    gbox("handle", (0, cy, 0.123), (0.08, 0.01, 0.008), "rail")
    k.empty("port-1", G(-0.17, cy + 0.004, fz + 0.006))


def dc_pdu():
    """A 0U power strip that bolts up the side of a rack: two rows of sockets, a breaker, a big A (or B) label."""
    w, h, d = 0.055, 1.8, 0.05
    gbox("body", (0, h / 2, 0), (w, h, d), "rack-black")
    for i in range(16):
        y = 0.2 + i * 0.095
        gbox(f"socket{i}", (0, y, d / 2 + 0.002), (0.03, 0.045, 0.004), "kit-dark")
        gbox(f"socket-hole{i}", (0, y, d / 2 + 0.005), (0.016, 0.022, 0.002), "port")
    gbox("breaker", (0, h - 0.06, d / 2 + 0.006), (0.03, 0.04, 0.012), "lamp-red")
    gbox("feed", (0, 0.02, 0), (0.02, 0.04, 0.02), "power-red")
    gbox("tag", (0, h - 0.14, d / 2 + 0.003), (0.045, 0.06, 0.002), "power-red")
    label("tag-text", "A", (0, h - 0.14, d / 2 + 0.006), 0.04, "label")


def dc_patch_lead():
    """The end of a patch lead (the game draws the cable between two of these): a plug and its boot, a tag."""
    gbox("plug", (0, 0, 0.012), (0.012, 0.009, 0.022), "glass-in")
    gbox("boot", (0, 0, -0.012), (0.014, 0.011, 0.03), "copper-lead")
    k.beam("cable", G(0, 0, -0.027), G(0, 0, -0.08), 0.006, M("copper-lead"), round_=True)
    gbox("tag", (0.012, 0, -0.04), (0.012, 0.004, 0.02), "label")


def dc_trolley():
    """The kit trolley you push to the rack: two steel shelves, boxes on the top, reels of lead on the bottom."""
    w, d = 0.9, 0.55
    for y in (0.25, 0.8):
        gbox(f"shelf{y}", (0, y, 0), (w, 0.03, d), "rail")
        gbox(f"lip{y}", (0, y + 0.03, d / 2), (w, 0.04, 0.01), "rail")
    for sx in (-1, 1):
        for sz in (-1, 1):
            gbox(f"leg{sx}{sz}", (sx * (w / 2 - 0.02), 0.5, sz * (d / 2 - 0.02)), (0.03, 0.9, 0.03), "rail")
            gcyl(f"castor{sx}{sz}", (sx * (w / 2 - 0.04), 0.0, sz * (d / 2 - 0.04)), 0.04, 0.06, "rack-black", sides=8)
    k.beam("handle", G(-w / 2, 0.95, -d / 2 + 0.02), G(-w / 2, 0.95, d / 2 - 0.02), 0.025, M("rack-black"), round_=True)
    for sz in (-1, 1):
        k.beam(f"handle-up{sz}", G(-w / 2, 0.82, sz * (d / 2 - 0.02)), G(-w / 2, 0.95, sz * (d / 2 - 0.02)), 0.025, M("rack-black"))
    gbox("box1", (0.1, 0.87, 0.02), (0.48, 0.09, 0.45), "kit-blue")
    gbox("box2", (0.12, 0.95, 0.0), (0.48, 0.045, 0.45), "kit-beige")
    for i, c in enumerate(("copper-lead", "fibre", "power-red")):
        k.cylinder(f"reel{i}", G(-0.25 + i * 0.25, 0.37, 0), 0.1, 0.06, M(c), axis="X", sides=10)
        k.cylinder(f"reel-hub{i}", G(-0.25 + i * 0.25, 0.37, 0), 0.04, 0.07, M("rack-black"), axis="X", sides=8)
    gbox("sheet", (0.25, 0.27, 0.1), (0.21, 0.004, 0.297), "paper")
    label("sheet-text", "PATCH SHEET", (0.25, 0.275, 0.05), 0.02, "ink", face="up")


def dc_test_plug():
    """The loopback test plug: a plug, a short loop of cable back into itself, a red tag that says LOOP."""
    gbox("plug", (0, 0, 0.012), (0.012, 0.009, 0.022), "glass-in")
    gbox("body", (0, 0, -0.012), (0.02, 0.016, 0.03), "lamp-red")
    k.cylinder("loop", G(0, -0.03, -0.03), 0.025, 0.005, M("lamp-red"), axis="X", sides=10)
    gbox("tag", (0, -0.065, -0.03), (0.03, 0.02, 0.002), "label")
    label("tag-text", "LOOP", (0, -0.065, -0.028), 0.01, "ink")


def dc_fibre_pen():
    """The fibre cleaning pen: a fat yellow pen with a push collar and a cap on a strap."""
    k.cylinder("body", G(0, 0, 0), 0.011, 0.14, M("fibre"), axis="Y", sides=8)
    k.cylinder("collar", G(0, 0, 0.075), 0.012, 0.015, M("rack-black"), axis="Y", sides=8)
    k.cylinder("tip", G(0, 0, 0.09), 0.004, 0.02, M("rail"), axis="Y", sides=6)
    k.cylinder("cap", G(0.02, 0, 0.09), 0.008, 0.03, M("rack-black"), axis="Y", sides=8)
    gbox("clip", (0, 0.012, -0.03), (0.004, 0.004, 0.05), "rail")


# ======================================================================================== power and cooling
def dc_ups_batteries():
    """A battery string: a two-shelf steel rack of six big batteries, linked with red and black straps."""
    w, d = 1.6, 0.5
    for y in (0.05, 0.6):
        gbox(f"shelf{y}", (0, y, 0), (w, 0.04, d), "rack-grey")
    for sx in (-1, 1):
        for sz in (-1, 1):
            gbox(f"post{sx}{sz}", (sx * (w / 2 - 0.02), 0.6, sz * (d / 2 - 0.02)), (0.04, 1.2, 0.04), "rack-grey")
    for i in range(6):
        row, col = divmod(i, 3)
        x, y = -0.5 + col * 0.5, 0.07 + row * 0.55
        gbox(f"bat{i}", (x, y + 0.17, 0), (0.42, 0.34, 0.4), "battery", bevel=0.01)
        gbox(f"bat-label{i}", (x, y + 0.2, 0.201), (0.2, 0.08, 0.002), "gen-yellow")
        for s, c in ((-1, "power-red"), (1, "ink")):
            gcyl(f"post{i}{s}", (x + s * 0.15, y + 0.34, 0.1), 0.02, 0.03, c, sides=6)
        if col < 2:
            k.beam(f"link{i}", G(x + 0.15, y + 0.37, 0.1), G(x + 0.35, y + 0.37, 0.1), 0.02, M("power-red"))
    gbox("warning", (w / 2 - 0.25, 1.1, d / 2 + 0.01), (0.3, 0.15, 0.004), "gen-yellow")
    label("warning-text", "DC 48V", (w / 2 - 0.25, 1.1, d / 2 + 0.014), 0.05, "ink")


def dc_generator():
    """The standby generator in its yellow box: louvres, exhaust stack, a fuel tank underneath, the log book on a nail."""
    w, h, d = 3.2, 2.0, 1.4
    gbox("tank", (0, 0.2, 0), (w + 0.2, 0.4, d + 0.1), "rack-black")
    gbox("canopy", (0, 0.4 + h / 2, 0), (w, h, d), "gen-yellow", bevel=0.02)
    for side in (-1, 1):
        for i in range(10):
            gbox(f"louvre{side}{i}", (-w / 2 + 0.6 + i * 0.12, 0.4 + h * 0.55, side * (d / 2 + 0.005)), (0.06, h * 0.55, 0.01), "grille")
    for i in range(6):
        gbox(f"rad{i}", (w / 2 + 0.005, 0.6 + i * 0.25, 0), (0.01, 0.12, d - 0.3), "grille")
    gcyl("exhaust", (-w / 2 + 0.4, 0.4 + h, -0.2), 0.08, 0.9, "kit-dark", sides=8)
    gcyl("exhaust-cap", (-w / 2 + 0.4, 0.4 + h + 0.9, -0.2), 0.11, 0.06, "kit-dark", sides=8)
    gbox("panel", (-w / 2 + 0.5, 1.4, d / 2 + 0.02), (0.6, 0.5, 0.04), "kit-dark")
    gbox("panel-lamp", (-w / 2 + 0.35, 1.5, d / 2 + 0.045), (0.05, 0.05, 0.01), "lamp-green")
    gbox("panel-button", (-w / 2 + 0.6, 1.3, d / 2 + 0.045), (0.08, 0.08, 0.02), "lamp-red")
    gbox("logbook", (-w / 2 + 0.95, 1.2, d / 2 + 0.02), (0.18, 0.25, 0.02), "notebook-red")
    label("name", "STANDBY", (0.6, 0.4 + h * 0.85, d / 2 + 0.015), 0.12, "ink")


def dc_load_dial():
    """The Plant Room's big load dial on a grey panel: a white face, a red zone, one needle, LOAD kW."""
    w, h = 0.8, 0.9
    gbox("panel", (0, 0, 0), (w, h, 0.06), "kit-grey")
    k.cylinder("bezel", G(0, 0.06, 0.035), 0.32, 0.03, M("rack-black"), axis="Y", sides=20)
    k.cylinder("face", G(0, 0.06, 0.052), 0.29, 0.005, M("dial-face"), axis="Y", sides=20)
    for i in range(11):
        a = math.radians(-120 + i * 24)
        x, y = math.sin(a) * 0.25, 0.06 + math.cos(a) * 0.25
        gbox(f"tick{i}", (x, y, 0.056), (0.008, 0.03, 0.003), "lamp-red" if i >= 8 else "ink", rot_y=0)
    a = math.radians(-20)
    k.beam("needle", G(0, 0.06, 0.06), G(math.sin(a) * 0.24, 0.06 + math.cos(a) * 0.24, 0.06), 0.012, M("lamp-red"))
    k.cylinder("hub", G(0, 0.06, 0.06), 0.02, 0.012, M("ink"), axis="Y", sides=8)
    label("unit", "LOAD kW", (0, -0.08, 0.056), 0.04, "ink")
    gbox("plate", (0, -0.34, 0.032), (0.4, 0.08, 0.004), "label")
    label("plate-text", "HALL A + B", (0, -0.34, 0.035), 0.035, "ink")
    k.empty("needle-pivot", G(0, 0.06, 0.06))


def dc_crac_unit():
    """A floor-standing cooling unit: tall cream cabinet, return grille on top, a little display, a drip tray."""
    w, h, d = 1.8, 1.95, 0.9
    gbox("cabinet", (0, h / 2, 0), (w, h, d), "crac", bevel=0.01)
    for i in range(3):
        gbox(f"door-line{i}", (-w / 2 + (i + 1) * w / 4, h / 2, d / 2 + 0.003), (0.006, h - 0.1, 0.004), "grille")
    gbox("grille-top", (0, h - 0.25, d / 2 + 0.004), (w - 0.1, 0.35, 0.004), "grille")
    for i in range(8):
        gbox(f"slat{i}", (0, h - 0.4 + i * 0.04, d / 2 + 0.008), (w - 0.12, 0.012, 0.006), "crac")
    gbox("display", (w / 2 - 0.3, 1.2, d / 2 + 0.006), (0.2, 0.1, 0.008), "screen-dark")
    label("display-text", "18C", (w / 2 - 0.3, 1.2, d / 2 + 0.012), 0.05, "lamp-green")
    gbox("badge", (-w / 2 + 0.3, 1.2, d / 2 + 0.006), (0.25, 0.06, 0.004), "trim-blue")
    gbox("tray", (0, 0.03, d / 2 + 0.1), (w - 0.2, 0.04, 0.15), "rail")


# ======================================================================================== rooms' walls and furniture
def dc_pinboard():
    """The planning board: a cork board on a pine frame with a hand-drawn map of the planet's network, coloured pins,
    string, reels of string on a shelf, and a string of fairy lights round the edge."""
    w, h = 2.4, 1.5
    gbox("frame", (0, 0, -0.03), (w + 0.1, h + 0.1, 0.05), "wood-light")
    gbox("cork", (0, 0, -0.002), (w, h, 0.01), "cork")
    gbox("map", (0, 0.02, 0.004), (w - 0.25, h - 0.25, 0.002), "map-paper")
    # a coast, the sea and three islands of land
    poly("sea", [(-1.05, -0.62, 0.006), (1.05, -0.62, 0.006), (1.05, -0.05, 0.006), (0.5, 0.05, 0.006), (0.2, -0.25, 0.006), (-1.05, -0.3, 0.006)],
         [(0, 1, 2, 3, 4, 5)], "map-sea")
    for i, (cx, cy, r) in enumerate(((-0.5, 0.25, 0.32), (0.35, 0.35, 0.25), (0.75, -0.42, 0.16))):
        poly(f"land{i}", [(cx + math.cos(a) * r, cy + math.sin(a) * r * 0.7, 0.007) for a in (j * math.pi / 5 for j in range(10))], [tuple(range(10))], "map-land")
    sites = {"core": (0.0, 0.0), "valley": (-0.55, 0.3), "eastgate": (-0.25, 0.45), "cold": (-0.75, 0.05),
             "millbrook": (0.4, 0.4), "kestrel": (0.15, -0.2), "farland": (0.75, -0.42)}
    for nme, (x, y) in sites.items():
        big = nme == "core"
        k.cylinder(f"pin-{nme}", G(x, y, 0.02), 0.022 if big else 0.013, 0.03, M("pin-brass" if big else ("lamp-red" if nme in ("cold", "farland") else "lamp-green")), axis="Y", sides=8)
        k.empty(f"site-{nme}", G(x, y, 0.035))
    for i, (a, b, c) in enumerate((("valley", "core", "string-brown"), ("eastgate", "core", "fibre"), ("cold", "valley", "string-brown"),
                                   ("millbrook", "core", "string-white"), ("kestrel", "core", "fibre"), ("farland", "kestrel", "string-blue"),
                                   ("eastgate", "valley", "fibre"))):
        (ax, ay), (bx, by) = sites[a], sites[b]
        k.beam(f"string{i}", G(ax, ay, 0.03), G(bx, by, 0.03), 0.004, M(c))
    # pinned notes and a work order
    for i, (x, y, c) in enumerate(((-1.0, 0.55, "paper-1"), (0.95, 0.55, "paper-3"), (0.95, 0.3, "paper-4"))):
        gbox(f"note{i}", (x, y, 0.008), (0.16, 0.12, 0.002), c)
    label("title", "THE NETWORK (DO NOT TOUCH - MOSS)", (0, h / 2 - 0.08, 0.008), 0.035, "ink")
    # a shelf with reels of string, the fairy lights round the frame
    gbox("shelf", (0, -h / 2 - 0.12, 0.08), (w, 0.03, 0.2), "wood-light")
    for i, c in enumerate(("string-brown", "string-white", "fibre", "string-blue")):
        k.cylinder(f"reel{i}", G(-0.9 + i * 0.25, -h / 2 - 0.06, 0.08), 0.045, 0.06, M(c), axis="X", sides=8)
    n = 0
    for x in [-w / 2 + i * w / 16 for i in range(17)]:
        for y in (h / 2 + 0.06, -h / 2 - 0.06):
            gbox(f"bulb{n}", (x, y, 0.01), (0.018, 0.026, 0.018), "bulb"); n += 1
    for y in [-h / 2 + i * h / 10 for i in range(1, 10)]:
        for x in (-w / 2 - 0.06, w / 2 + 0.06):
            gbox(f"bulb{n}", (x, y, 0.01), (0.026, 0.018, 0.018), "bulb"); n += 1
    gbox("big-switch", (w / 2 + 0.2, -0.3, 0.04), (0.14, 0.24, 0.08), "kit-dark")
    gbox("big-switch-lever", (w / 2 + 0.2, -0.26, 0.1), (0.04, 0.12, 0.04), "lamp-red")


def dc_clip_rail():
    """A wooden rail of bulldog clips with work orders hanging from it, one in red."""
    w = 1.4
    gbox("rail", (0, 0, 0), (w, 0.06, 0.03), "wood")
    for i in range(7):
        x = -w / 2 + 0.12 + i * 0.19
        gbox(f"clip{i}", (x, -0.01, 0.025), (0.05, 0.05, 0.015), "rail")
        if i in (0, 1, 2, 4, 5):
            gbox(f"order{i}", (x, -0.17, 0.03), (0.15, 0.21, 0.003), "lamp-red" if i == 2 else "paper")
            for j in range(4):
                gbox(f"order{i}-line{j}", (x, -0.1 - j * 0.035, 0.0325), (0.1, 0.005, 0.001), "label" if i == 2 else "ink")
    label("head", "WORK ORDERS", (0, 0.06, 0.016), 0.03, "ink")


def dc_odf_wall():
    """The Meet-Me Room's fibre wall: a tall frame of trays, yellow leads looping down, luggage tags on every cable end."""
    w, h, d = 2.4, 2.2, 0.35
    gbox("back", (0, h / 2, -d / 2), (w, h, 0.03), "kit-grey")
    for sx in (-1, 0, 1):
        gbox(f"upright{sx}", (sx * (w / 2 - 0.04), h / 2, 0), (0.08, h, d), "rack-black")
    for row in range(12):
        y = 0.25 + row * 0.15
        for half in (-1, 1):
            x = half * w / 4
            gbox(f"tray{row}{half}", (x, y, 0.05), (w / 2 - 0.16, 0.1, 0.25), "kit-grey")
            for p in range(8):
                gbox(f"ad{row}{half}{p}", (x - 0.4 + p * 0.11, y, 0.18), (0.03, 0.04, 0.01), "fibre")
    for i in range(9):
        x = -1.0 + i * 0.25
        top = 0.25 + (i * 5 % 12) * 0.15
        k.beam(f"lead{i}", G(x, top, 0.19), G(x + 0.05, 0.12, 0.25), 0.008, M("fibre"))
        gbox(f"luggage{i}", (x + 0.04, 0.35, 0.235), (0.05, 0.08, 0.003), "paper")
    gbox("duct", (0, 0.06, 0.12), (w, 0.12, 0.3), "rack-black")
    gbox("sign", (0, h + 0.1, 0), (0.9, 0.15, 0.02), "trim-blue")
    label("sign-text", "MEET-ME", (0, h + 0.1, 0.012), 0.08, "label")


def dc_cage():
    """A customer's cage in the Hall: steel mesh walls on posts round a 3 x 3 m pen, a door with a padlock, a name card."""
    w, h, d = 3.0, 2.4, 3.0
    for sx in (-1, 0, 1):
        for sz in (-1, 0, 1):
            if sx and sz or sx == 0 and sz != 0 or sz == 0 and sx != 0:
                gbox(f"post{sx}{sz}", (sx * w / 2, h / 2, sz * d / 2), (0.05, h, 0.05), "rack-black")
    def mesh_panel(n, a, b):
        poly(n, [(a[0], 0.1, a[1]), (b[0], 0.1, b[1]), (b[0], h, b[1]), (a[0], h, a[1])], [(0, 1, 2, 3)], "mesh")
    mesh_panel("back", (-w / 2, -d / 2), (w / 2, -d / 2))
    mesh_panel("left", (-w / 2, -d / 2), (-w / 2, d / 2))
    mesh_panel("right", (w / 2, -d / 2), (w / 2, d / 2))
    mesh_panel("front-l", (-w / 2, d / 2), (-0.5, d / 2))
    mesh_panel("front-r", (0.5, d / 2), (w / 2, d / 2))
    for i in range(8):
        y = 0.3 + i * 0.28
        for nme, a, b in (("back", (-w / 2, -d / 2), (w / 2, -d / 2)), ("left", (-w / 2, -d / 2), (-w / 2, d / 2)), ("right", (w / 2, -d / 2), (w / 2, d / 2))):
            k.beam(f"wire-{nme}{i}", G(a[0], y, a[1]), G(b[0], y, b[1]), 0.012, M("rack-black"))
    gbox("door", (0.0, h / 2 + 0.05, d / 2 + 0.05), (1.0, h - 0.1, 0.03), "mesh")
    gbox("door-frame", (0.0, h / 2 + 0.05, d / 2 + 0.06), (1.04, 0.05, 0.035), "rack-black")
    gbox("padlock", (0.45, 1.1, d / 2 + 0.09), (0.05, 0.07, 0.03), "brass")
    gbox("card", (-0.9, 1.6, d / 2 + 0.02), (0.3, 0.2, 0.004), "label")
    label("card-text", "CAGE 3", (-0.9, 1.62, d / 2 + 0.025), 0.05, "ink")
    gbox("floor-tiles", (0, 0.05, 0), (w, 0.1, d), "floor-tile")


def dc_noc_desk():
    """The night shift's desk: a long desk facing a bank of lamps (one per site), four monitors, two phones,
    two chairs, mugs, and a sticky note on the alarm. Monitor faces are nodes named screen-0..3 for the game to draw on."""
    w, d = 3.2, 0.9
    gbox("top", (0, 0.74, 0), (w, 0.04, d), "desk-top")
    for sx in (-1, 0, 1):
        gbox(f"leg{sx}", (sx * (w / 2 - 0.05), 0.36, 0), (0.06, 0.72, d - 0.1), "kit-grey")
    gbox("modesty", (0, 0.45, -d / 2 + 0.03), (w, 0.5, 0.02), "kit-grey")
    # the lamp bank on its upstand behind the monitors
    gbox("upstand", (0, 1.25, -d / 2 + 0.05), (w, 1.0, 0.08), "kit-dark")
    gbox("upstand-label", (0, 1.7, -d / 2 + 0.095), (1.2, 0.08, 0.004), "label")
    label("upstand-text", "SITES - GREEN IS GOOD", (0, 1.7, -d / 2 + 0.098), 0.04, "ink")
    names = ("VALLEY", "EASTGATE", "COLD STORE", "MILLBROOK", "KESTREL", "FARLAND", "CORE", "SPARE")
    for i, nme in enumerate(names):
        x = -1.3 + i * 0.37
        k.cylinder(f"lamp{i}", G(x, 1.5, -d / 2 + 0.1), 0.035, 0.02, M("lamp-amber" if nme == "FARLAND" else "lamp-green"), axis="Y", sides=10)
        label(f"lamp-name{i}", nme, (x, 1.43, -d / 2 + 0.095), 0.022, "label")
    gbox("alarm", (1.4, 1.2, -d / 2 + 0.1), (0.12, 0.12, 0.04), "lamp-red")
    gbox("alarm-note", (1.4, 1.2, -d / 2 + 0.125), (0.1, 0.1, 0.002), "tape")
    label("alarm-text", "IGNORE", (1.4, 1.2, -d / 2 + 0.128), 0.018, "ink")
    for i in range(4):
        x = -1.2 + i * 0.8
        crt_monitor(f"mon{i}", x, 0.76, -0.1, s=0.42, rot=(-0.12 if i < 2 else 0.12))
        k.empty(f"screen-{i}", G(x, 0.76 + 0.42 * 0.44, -0.1 + 0.42 * 0.26))
        gbox(f"kbd{i}", (x, 0.77, 0.25), (0.42, 0.02, 0.15), "monitor")
    for i, x in enumerate((-0.8, 0.8)):
        gbox(f"phone{i}", (x + 0.3, 0.79, 0.2), (0.18, 0.06, 0.2), "kit-dark", bevel=0.01)
        gbox(f"handset{i}", (x + 0.3, 0.84, 0.2), (0.2, 0.035, 0.05), "kit-dark")
        gcyl(f"mug{i}", (x - 0.35, 0.76, 0.28), 0.04, 0.1, "mug", sides=8)
        chair(f"chair{i}", x, 0.9, rot=math.pi)
    gbox("biscuits", (0.0, 0.79, 0.3), (0.18, 0.06, 0.08), "poster-yellow")


# ======================================================================================== the building on Halden Flats
def dc_slab_portacabin():
    """Stage 1 on Halden Flats: a fresh concrete slab with shuttering still round one edge, and a portacabin office
    on blocks in the corner (the pin board starts in here). Slab 34 x 26 m."""
    sw, sd = 34.0, 26.0
    gbox("slab", (0, 0.15, 0), (sw, 0.3, sd), "slab")
    for i in range(8):
        gbox(f"joint{i}", (-sw / 2 + (i + 1) * sw / 9, 0.301, 0), (0.03, 0.004, sd), "concrete")
    for i in range(10):
        gbox(f"shutter{i}", (sw / 2 + 0.06, 0.2, -sd / 2 + 1.3 + i * 2.6), (0.05, 0.35, 2.5), "wood-light")
        gbox(f"stake{i}", (sw / 2 + 0.2, 0.3, -sd / 2 + 1.3 + i * 2.6), (0.06, 0.6, 0.06), "wood")
    # the portacabin, 9.6 x 3 x 2.8, on blocks near the front-left corner, its door and steps facing +Z
    cx, cz, cw, cd, ch = -sw / 2 + 6.0, sd / 2 - 3.0, 9.6, 3.0, 2.6
    for i in range(4):
        for sz in (-1, 1):
            gbox(f"block{i}{sz}", (cx - cw / 2 + 0.6 + i * 2.8, 0.45, cz + sz * (cd / 2 - 0.3)), (0.4, 0.3, 0.4), "concrete")
    gbox("cabin", (cx, 0.6 + ch / 2, cz), (cw, ch, cd), "cabin")
    gbox("cabin-roof", (cx, 0.6 + ch + 0.05, cz), (cw + 0.1, 0.1, cd + 0.1), "cabin-trim")
    gbox("cabin-skirt", (cx, 0.7, cz), (cw + 0.02, 0.2, cd + 0.02), "cabin-trim")
    for x in (-3.4, -1.2, 2.4, 3.8):
        framed_window(f"cwin{x}", cx + x, 0.6 + 1.6, cz + cd / 2, 1.0, 0.8, glass="glass", frame="cabin-trim")
    gbox("cabin-door", (cx + 0.6, 0.6 + 1.05, cz + cd / 2 + 0.03), (0.9, 2.05, 0.05), "trim-blue")
    for i in range(3):
        gbox(f"step{i}", (cx + 0.6, 0.12 + i * 0.18, cz + cd / 2 + 0.9 - i * 0.25), (1.1, 0.18, 0.3), "rail")
    gbox("cabin-sign", (cx - 2.2, 0.6 + ch - 0.3, cz + cd / 2 + 0.02), (2.6, 0.35, 0.03), "label")
    label("cabin-sign-text", "TELO SITE OFFICE", (cx - 2.2, 0.6 + ch - 0.3, cz + cd / 2 + 0.04), 0.18, "trim-blue")
    gbox("ac-box", (cx + cw / 2 + 0.25, 1.9, cz), (0.4, 0.5, 0.6), "crac")
    # a builder's sign at the front edge, a stack of steel and a skip
    for sx in (-1, 1):
        gbox(f"sign-post{sx}", (6 + sx * 1.2, 1.2, sd / 2 + 1.5), (0.1, 2.4, 0.1), "wood")
    gbox("sign", (6, 2.0, sd / 2 + 1.55), (3.0, 1.2, 0.05), "trim-blue")
    label("sign-1", "HALDEN FLATS", (6, 2.3, sd / 2 + 1.58), 0.25, "label")
    label("sign-2", "DATA CENTRE - COMING SOON", (6, 1.85, sd / 2 + 1.58), 0.13, "label")
    label("sign-3", "council: \"delighted\"", (6, 1.6, sd / 2 + 1.58), 0.09, "tape")
    for i in range(5):
        gbox(f"steel{i}", (8.0, 0.4 + i * 0.12, -4.0 + (i % 2) * 0.1), (8.0, 0.12, 0.25), "trim-blue")
    gbox("skip", (12.0, 0.9, 8.0), (3.6, 1.2, 1.8), "gen-yellow")


def dc_hall_shell():
    """Stage 2: the hall itself, 32 x 24 x 7 m on the slab. Clad steel walls, a roller door into the loading bay,
    a people door, a parapet roof with cooling fans and a ladder up (the finale is watched from here). Hollow: the
    five rooms go inside it."""
    w, d, h, th = 32.0, 24.0, 7.0, 0.3
    gbox("slab", (0, 0.15, 0), (w + 2, 0.3, d + 2), "slab")
    gbox("floor", (0, 0.32, 0), (w - 0.2, 0.04, d - 0.2), "floor-tile")
    # walls with a roller door (x = 9, 5 wide, 5 tall) and a people door (x = -6) in the front
    gbox("back", (0, h / 2, -d / 2 + th / 2), (w, h, th), "cladding")
    for s in (-1, 1):
        gbox(f"side{s}", (s * (w / 2 - th / 2), h / 2, 0), (th, h, d - 2 * th), "cladding")
    zf = d / 2 - th / 2
    gbox("front-a", (-11.25, h / 2, zf), (9.5, h, th), "cladding")
    gbox("front-b", (1.25, h / 2, zf), (12.5, h, th), "cladding")
    gbox("front-c", (14.25, h / 2, zf), (3.5, h, th), "cladding")
    gbox("over-door", (-6.0, (h + 2.3) / 2, zf), (1.0, h - 2.3, th), "cladding")
    gbox("over-roller", (9.75, (h + 5.0) / 2, zf), (5.5, h - 5.0, th), "cladding")
    for i in range(int(w / 0.6)):
        x = -w / 2 + 0.3 + i * 0.6
        gbox(f"rib-b{i}", (x, h / 2, -d / 2 - 0.01), (0.04, h, 0.04), "cladding-dark")
    for i in range(int(d / 0.6)):
        z = -d / 2 + 0.3 + i * 0.6
        for s in (-1, 1):
            gbox(f"rib-s{i}{s}", (s * (w / 2 + 0.01), h / 2, z), (0.04, h, 0.04), "cladding-dark")
    gbox("band", (0, h - 0.6, d / 2 + 0.01), (w, 0.6, 0.04), "trim-blue")
    label("name", "TELO  -  THE CORE", (-6.0, h - 0.6, d / 2 + 0.04), 0.4, "label")
    # roller door, half up; the people door; lamps
    for i in range(10):
        gbox(f"roller{i}", (9.75, 2.6 + i * 0.24, zf + 0.25), (5.0, 0.2, 0.05), "rail")
    gbox("roller-drum", (9.75, 5.2, zf + 0.35), (5.4, 0.45, 0.45), "kit-grey")
    gbox("door", (-6.0, 1.15, zf + 0.12), (1.0, 2.25, 0.06), "trim-blue")
    gbox("door-pad", (-5.3, 1.2, zf + 0.17), (0.12, 0.18, 0.04), "kit-dark")
    for x in (-6.0, 9.75):
        gbox(f"lamp{x}", (x, 5.7 if x > 0 else 2.8, zf + 0.3), (0.4, 0.15, 0.3), "bulb")
    # roof: deck, parapet, four fans, the ladder up the right side
    gbox("roof", (0, h + 0.1, 0), (w, 0.2, d), "kit-grey")
    for s in (-1, 1):
        gbox(f"parapet-f{s}", (0, h + 0.55, s * (d / 2 - 0.1)), (w, 0.7, 0.2), "cladding-dark")
        gbox(f"parapet-s{s}", (s * (w / 2 - 0.1), h + 0.55, 0), (0.2, 0.7, d), "cladding-dark")
    for i, (x, z) in enumerate(((-8, -4), (-2, -4), (4, -4), (10, -4))):
        gbox(f"fan-box{i}", (x, h + 0.7, z), (2.6, 1.2, 2.6), "crac")
        gcyl(f"fan{i}", (x, h + 1.3, z), 1.0, 0.05, "grille", sides=12)
        for b in range(3):
            a = b * 2.094
            k.beam(f"blade{i}{b}", G(x, h + 1.36, z), G(x + math.cos(a) * 0.9, h + 1.36, z + math.sin(a) * 0.9), 0.12, M("rack-black"))
    for sx in (-0.25, 0.25):
        gbox(f"ladder-rail{sx}", (w / 2 + 0.25, h / 2 + 0.6, 6 + sx), (0.05, h + 1.2, 0.05), "rail")
    for i in range(int(h / 0.3) + 3):
        gbox(f"ladder-rung{i}", (w / 2 + 0.25, 0.3 + i * 0.3, 6), (0.05, 0.04, 0.5), "rail")
    # outside: a generator compound fence and the duct pit where the cables come in
    gbox("duct-pit", (-w / 2 - 2.0, 0.35, -6), (1.6, 0.1, 1.6), "rack-black")
    label("duct-text", "DUCTS", (-w / 2 - 2.0, 0.41, -6), 0.25, "tape", face="up")
    k.empty("door-people", G(-6.0, 0.3, d / 2))
    k.empty("door-roller", G(9.75, 0.3, d / 2))


# ======================================================================================== the people
def dc_person(top, legs, skin, hair, **kw):
    return lambda: person(top, legs, skin, hair, **kw)


def person_dana():
    """Dana Wilke, DC technician: navy polo, lanyard, a yellow fibre lead looped over one shoulder."""
    person("polo-navy", "jeans", "skin-2", "hair-dark", holding=True, extras=("long-hair",))
    gbox("lanyard", (0, 1.3, 0.13), (0.02, 0.3, 0.01), "lanyard")
    gbox("badge", (0, 1.13, 0.135), (0.07, 0.09, 0.005), "label")
    k.beam("lead-a", G(-0.2, 1.48, 0.06), G(0.12, 0.98, 0.14), 0.015, M("fibre"))
    k.beam("lead-b", G(-0.2, 1.48, -0.06), G(0.1, 0.98, -0.1), 0.015, M("fibre"))
    gbox("pen", (0.11, 1.38, 0.13), (0.015, 0.1, 0.015), "fibre")


def person_moss():
    """Moss Achebe, network planner: check shirt, green cardigan, a pencil, a rolled plan under one arm."""
    person("cardigan-green", "trouser-grey", "skin-2", "hair-dark", holding=False, extras=("glasses", "cardigan"))
    k.cylinder("plan-roll", G(-0.33, 1.1, 0.02), 0.04, 0.6, M("map-paper"), axis="Y", sides=8)
    gbox("pencil", (0.11, 1.38, 0.13), (0.012, 0.12, 0.012), "pencil")


def person_noc(top, skin, hair, extras=()):
    def build():
        person(top, "jeans", skin, hair, holding=True, headphones=True, extras=extras)
        gcyl("mug", (-0.2, 1.05, 0.29), 0.04, 0.09, "mug", sides=8)
    return build


PROPS = {
    # the rack and its kit
    "dc.rack-42u": (dc_rack_42u, "42U rack", "An open-front 19-inch rack, 0.6 x 2.0 x 1.0 m: black frame, grey side panels, numbered rails, a cable tray on the roof, tape across unused slots. Empties u01 (bottom slot, front rail) and u42.", "floor, centre; front +Z", False),
    "dc.rack-12u": (dc_rack_12u, "12U cabinet", "A small floor cabinet for an exchange hut: glass door, lock, fan.", "floor, centre; front +Z", False),
    "dc.wallbox-6u": (dc_wallbox_6u, "6U wall box", "A wall-hung box for a street cabinet: smoked door, a TELO 6U label.", "front face centre (the wall is at -0.45 Z)", False),
    "dc.box-router": (dc_box_router, "Router (2U)", "Blue-grey 2U box: four line modules of four ports, a lamp on each, ROUTER badge. Empty port-1.", "bottom centre of the front panel", False),
    "dc.box-switch": (dc_box_switch, "Switch (1U)", "1U box with 24 ports in two rows and a lamp over each pair. Empty port-1.", "bottom centre of the front panel", False),
    "dc.box-patch": (dc_box_patch, "Patch panel (1U)", "1U panel of 24 sockets with a hand-labelled write-on strip. Empty port-1.", "bottom centre of the front panel", False),
    "dc.box-fibretray": (dc_box_fibretray, "Fibre tray (1U)", "1U drawer pulled out a little: twelve yellow adapters, dust caps, a coil of fibre. Empty port-1.", "bottom centre of the front panel", False),
    "dc.pdu": (dc_pdu, "Power strip (PDU)", "0U vertical strip, 1.8 m: sixteen sockets, a red breaker, a big A tag (tint it for B).", "bottom centre", False),
    "dc.patch-lead": (dc_patch_lead, "Patch lead end", "One end of a lead: clear plug, blue boot, a stub of cable, a tag. The game draws the cable between two ends.", "plug tip", False),
    "dc.trolley": (dc_trolley, "Kit trolley", "Two-shelf steel trolley with boxes on top and reels of blue, yellow and red lead underneath, the patch sheet on the bottom shelf.", "floor, centre", False),
    "dc.test-plug": (dc_test_plug, "Loopback test plug", "A plug with a red loop of cable back into itself and a LOOP tag.", "plug tip", False),
    "dc.fibre-pen": (dc_fibre_pen, "Fibre cleaning pen", "A fat yellow click pen with a cap, for cleaning fibre ends.", "centre", False),
    # power and cooling
    "dc.ups-batteries": (dc_ups_batteries, "Battery string", "A steel rack of six big batteries linked with red straps; DC 48V warning.", "floor, centre", False),
    "dc.generator": (dc_generator, "Standby generator", "Yellow enclosure on a fuel tank: louvres, radiator, exhaust stack, start panel, the log book.", "floor, centre", False),
    "dc.load-dial": (dc_load_dial, "Load dial", "Grey panel with a big white dial, red zone, one needle (empty needle-pivot), LOAD kW, HALL A + B.", "panel centre", False),
    "dc.crac-unit": (dc_crac_unit, "Cooling unit", "Floor-standing cream cooling cabinet with a return grille, a display reading 18C, a drip tray.", "floor, centre", False),
    # rooms
    "dc.pinboard": (dc_pinboard, "The pin board", "2.4 x 1.5 m cork board: hand-drawn map, a brass pin for the core and coloured pins (empties site-*), strings by link kind, reels on a shelf, fairy lights all round, the big switch.", "board centre, back face", False),
    "dc.clip-rail": (dc_clip_rail, "Clip rail", "Wooden rail of bulldog clips with work orders hanging, one urgent in red.", "rail centre", False),
    "dc.odf-wall": (dc_odf_wall, "Fibre wall (ODF)", "The Meet-Me Room's wall of fibre trays: 24 trays, yellow leads looping down, luggage tags, a floor duct, a MEET-ME sign.", "floor, centre", False),
    "dc.cage": (dc_cage, "Customer cage", "A 3 x 3 m mesh pen on posts with a padlocked door and a CAGE 3 card.", "floor, centre", False),
    "dc.noc-desk": (dc_noc_desk, "Night shift desk", "A long desk with a bank of site lamps (green is good), four monitors (empties screen-0..3 at each face for the game to draw on), phones, chairs, mugs, an alarm with IGNORE stuck on it.", "floor, centre; operators sit at +Z", False),
    # the building
    "dc.slab-portacabin": (dc_slab_portacabin, "Slab and site office", "A 34 x 26 m concrete slab with shuttering still on one edge, a portacabin office on blocks, a COMING SOON board, steel and a skip.", "slab centre, ground", False),
    "dc.hall-shell": (dc_hall_shell, "The hall (shell)", "The 32 x 24 x 7 m clad steel hall on its slab: roller door to the loading bay, people door, THE CORE on the band, roof fans, parapet and a ladder up. Hollow inside for the five rooms. Empties door-people, door-roller.", "slab centre, ground; front +Z", False),
    # people
    "person.dana": (person_dana, "Dana Wilke (DC technician)", "Navy polo, lanyard, a yellow fibre lead over one shoulder, a fibre pen in the pocket.", "feet, facing +Z", False),
    "person.moss": (person_moss, "Moss Achebe (planner)", "Green cardigan, glasses, a pencil, a rolled network plan under one arm.", "feet, facing +Z", False),
    "person.noc-a": (person_noc("fleece", "skin-1", "hair-red"), "Night shift (A)", "Grey fleece, headset, a mug of tea.", "feet, facing +Z", False),
    "person.noc-b": (person_noc("hoodie", "skin-3", "hair-blond", extras=("glasses",)), "Night shift (B)", "Hoodie, glasses, headset, a mug of coffee.", "feet, facing +Z", False),
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
    print(f"DC OK {len(results)} props")
