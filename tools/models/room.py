"""Your room's working wall: the switchboard and its parts, the line rack, and the boards on the walls.

    blender -b --factory-startup --python tools/models/room.py -- all
    blender -b --factory-startup --python tools/models/room.py -- switch.port room.line-rack
    blender -b --factory-startup --python tools/models/room.py -- takes     # docs/img/props/takes-switchboard.png

Same conventions as town.py: builders are written in GODOT coordinates (x right, y up, z out of the wall towards
you) through G() and S(), because SwitchboardStation.cs, LineRack.cs and DeskWall.cs place wires, lamps, labels and
click areas in those coordinates and the models must line up with them. A model replaces only the LOOK: every click
area, label and the wires themselves stay code-built.

Pivots (each matches where the code puts the prop):
  * room.switchboard: the station's origin, the centre of the board. Back of the battens on the wall at z -0.02;
    the plywood face at z 0.034, just behind the code's labels (z 0.04, SwitchboardStation.Face).
  * Parts on the board (terminal, port, lamp, tag, hook, throw switch, panel sign): back-centre, placed at z = Face.
  * Tools and the jumper plug: their centre; tools lie along X with the business end at +X.
  * room.line-rack and the wall boards: the centre of the node the code makes (the old placeholder box's centre).

Named nodes the code looks for: "lens…" (its material is set at run time: lamps), "collar" (nickel on spare
ports), "sleeve" (the jumper's colour), "blade" (the throw switch swings it), "card-NN…" (a fitted line card),
"next-slot" (moved to the next empty card slot; holds the wp-01-place / wp-02-plug / wp-03-screw work points).
"""
import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(__file__))
import _kit as k  # noqa: E402

k.PALETTE.update({
    # the switchboard (names from docs/props/switchboard.json)
    "switch-ply": (0.74, 0.60, 0.42), "switch-ply-shelf": (0.62, 0.49, 0.33), "switch-ply-old": (0.56, 0.47, 0.36),
    "switch-batten": (0.46, 0.35, 0.23), "switch-brass": (0.80, 0.66, 0.30), "switch-brass-old": (0.52, 0.46, 0.30),
    "switch-spare": (0.72, 0.74, 0.76), "switch-label": (0.95, 0.93, 0.85), "switch-tag": (0.95, 0.93, 0.85),
    "switch-tag-ink": (0.12, 0.10, 0.30), "switch-bakelite": (0.22, 0.14, 0.10), "switch-black": (0.10, 0.10, 0.11),
    "switch-knife": (0.86, 0.80, 0.62), "switch-slate": (0.20, 0.21, 0.23), "switch-panel": (0.32, 0.33, 0.35),
    "verdigris": (0.36, 0.58, 0.48), "scorch": (0.12, 0.09, 0.07), "gaffer": (0.24, 0.25, 0.27),
    "pencil": (0.36, 0.36, 0.38), "coffee": (0.42, 0.28, 0.16), "stencil": (0.55, 0.42, 0.30), "screw": (0.70, 0.70, 0.66),
    "cable-grey": (0.55, 0.56, 0.55), "lamp-glass": (0.30, 0.10, 0.06), "lamp-bezel": (0.62, 0.63, 0.64),
    "wire-red": (0.80, 0.15, 0.12), "wire-blue": (0.15, 0.30, 0.80), "wire-yellow": (0.90, 0.80, 0.20),
    "wire-green": (0.15, 0.60, 0.25), "wire-white": (0.90, 0.90, 0.88), "wire-orange": (0.85, 0.45, 0.10),
    "wire-dirty": (0.36, 0.33, 0.29),
    "tool-probe": (0.95, 0.80, 0.15), "tool-probe-dark": (0.70, 0.56, 0.08), "tool-steel": (0.70, 0.70, 0.72),
    "tool-test-set": (0.12, 0.12, 0.13), "tool-test-orange": (0.85, 0.42, 0.10), "led-red": (0.95, 0.2, 0.15),
    # the line rack
    "rack-body": (0.20, 0.21, 0.22), "rack-face": (0.26, 0.27, 0.29), "rack-slot": (0.06, 0.06, 0.07),
    "rack-tally": (0.93, 0.90, 0.80), "rack-label": (0.85, 0.83, 0.75), "rack-card": (0.16, 0.34, 0.22),
    # wall boards
    "cork": (0.66, 0.50, 0.33), "cork-dark": (0.55, 0.41, 0.26), "pine": (0.82, 0.66, 0.44), "pine-dark": (0.64, 0.49, 0.30),
    "rail-alu": (0.72, 0.73, 0.74), "paper": (0.95, 0.93, 0.86), "paper-yellow": (0.95, 0.90, 0.55),
    "paper-white": (0.92, 0.92, 0.88), "paper-pink": (0.95, 0.78, 0.76), "paper-blue": (0.80, 0.88, 0.96),
    "pin-red": (0.85, 0.15, 0.12), "pin-blue": (0.15, 0.35, 0.80), "pin-green": (0.15, 0.6, 0.3), "pin-yellow": (0.95, 0.8, 0.15),
    "calendar-red": (0.60, 0.14, 0.12), "calendar-sky": (0.55, 0.72, 0.86), "calendar-hill": (0.35, 0.55, 0.30),
    "calendar-hill-2": (0.45, 0.65, 0.35), "card-back": (0.62, 0.55, 0.44), "ring": (0.55, 0.56, 0.58), "string": (0.85, 0.80, 0.68),
    "masonite": (0.45, 0.32, 0.20), "clip-steel": (0.66, 0.67, 0.68), "navy": (0.16, 0.20, 0.36), "margin-red": (0.85, 0.40, 0.38),
    "ink": (0.12, 0.12, 0.14),
})
M = k.mat
TAKE = "plain"   # plain / old / bodge (the takes sheet builds all three; the game uses plain)


# ---------------------------------------------------------------------------------------- Godot-space helpers
def G(x, y, z):
    """A Godot position as a Blender one."""
    return (x, -z, y)


def S(x, y, z):
    """A Godot size as a Blender one."""
    return (x, z, y)


def gbox(name, c, s, colour, bevel=0.0, rot_z=0.0, parent=None):
    """A box; rot_z turns it in the board's plane (about Godot Z), for crooked things."""
    o = k.box(name, G(*c), S(*s), M(colour), bevel=bevel)
    if rot_z:
        o.rotation_euler = (0, -rot_z, 0)  # Godot Z is Blender -Y
    if parent is not None:
        under(o, parent)
    return o


def gcyl(name, c, r, length, colour, axis="z", sides=10, parent=None, r2=None):
    """A cylinder (or a cone if r2 is given) centred on c, along a Godot axis: "x", "y" or "z" (out of the wall)."""
    if r2 is None:
        o = k.cylinder(name, G(*c), r, length, M(colour), axis={"x": "X", "y": "Z", "z": "Y"}[axis], sides=sides)
    else:
        rot = {"x": (0, math.pi / 2, 0), "y": (0, 0, 0), "z": (math.pi / 2, 0, 0)}[axis]  # cone tip towards +axis
        o = k.cone(name, G(*c), r, r2, length, M(colour), sides=sides, rot=rot)
    if parent is not None:
        under(o, parent)
    return o


def ring(name, c, major, minor, colour, axis="z", parent=None, segs=10):
    """A torus round a Godot axis: wire coils, calendar rings."""
    rot = {"x": (0, math.pi / 2, 0), "y": (0, 0, 0), "z": (math.pi / 2, 0, 0)}[axis]
    bpy.ops.mesh.primitive_torus_add(major_segments=segs, minor_segments=4, major_radius=major, minor_radius=minor,
                                     location=G(*c), rotation=rot)
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    o = k._finish(o, name, M(colour))
    if parent is not None:
        under(o, parent)
    return o


def gbeam(name, a, b, thick, colour, round_=False, parent=None):
    o = k.beam(name, G(*a), G(*b), thick, M(colour), round_=round_)
    if parent is not None:
        under(o, parent)
    return o


def node(name, at, parent=None):
    """An empty in Godot space (a pivot, a group, a work point)."""
    o = k.empty(name, G(*at))
    if parent is not None:
        under(o, parent)
    return o


def under(o, parent):
    """Parent o to parent, keeping where o is now."""
    bpy.context.view_layer.update()
    o.parent = parent
    o.matrix_parent_inverse = parent.matrix_world.inverted()


def label(name, body, c, size, colour, extrude=0.001):
    """Lettering facing the room (+Z)."""
    return k.text(name, body, G(*c), size, M(colour), rotation=(math.pi / 2, 0, 0), extrude=extrude)


def screw(name, c, r=0.004, colour="screw", slot_angle=0.0):
    gcyl(name, c, r, 0.003, colour, axis="z", sides=8)
    gbox(name + "-slot", (c[0], c[1], c[2] + 0.0016), (r * 1.8, r * 0.35, 0.001), "ink", rot_z=slot_angle)


# ======================================================================================== the switchboard
# SwitchboardStation.cs: W 1.6, H 1.36, Face 0.04. Rows from y 0.5 down, pitch 0.95 / (rows + 1). Terminals at x -0.4,
# tags at x -0.62 (0.34 wide), ports at x 0.3, lamps at x 0.49, port numbers at x 0.39. Shelf at y -0.64, z 0.1, 0.22 deep.
# Shelf spots: probe (-0.64, -0.58), test set (-0.38, -0.58), hooks (-0.12 and 0.06, -0.53), switch (0.62, -0.52),
# panel sign (0.64, 0.625). The board model is the backing, shelf, stencils and hardware only: rows are separate props.
W, H, PLY_FRONT = 1.6, 1.36, 0.034


def room_switchboard():
    ply = {"plain": "switch-ply", "old": "switch-ply-old", "bodge": "switch-ply"}[TAKE]
    # battens on the wall, plywood on the battens
    for sx in (-1, 1):
        gbox(f"batten-v{sx}", (sx * 0.72, 0, -0.002), (0.06, H - 0.04, 0.036), "switch-batten")
    for sy in (-1, 1):
        gbox(f"batten-h{sy}", (0, sy * 0.6, -0.002), (W - 0.2, 0.06, 0.036), "switch-batten")
    gbox("ply", (0, 0, PLY_FRONT - 0.009), (W, H, 0.018), ply, bevel=0.004)
    for sx in (-1, 1):
        for sy in (-1, 0, 1):
            screw(f"ply-screw{sx}{sy}", (sx * 0.72, sy * 0.6, PLY_FRONT + 0.0015), r=0.006, colour="switch-brass", slot_angle=0.3 * sx + sy)
    # the customers' pairs come through the wall just above the board to a lacing bar over the subscriber column
    gbox("lacing-bar", (-0.55, 0.655, PLY_FRONT + 0.008), (0.5, 0.016, 0.016), "switch-batten")
    gcyl("wall-hole", (-0.705, 0.75, -0.019), 0.03, 0.004, "ink", axis="z", sides=8)
    for i, x in enumerate((-0.74, -0.71, -0.68)):
        gbeam(f"pair-cable{i}", (x, 0.66, PLY_FRONT + 0.012 + i * 0.006), (-0.72 + i * 0.015, 0.75, -0.02), 0.012, "cable-grey", round_=True)
        gbeam(f"pair-run{i}", (x, 0.66, PLY_FRONT + 0.012 + i * 0.006), (-0.32 - i * 0.04, 0.655, PLY_FRONT + 0.018), 0.008, "cable-grey", round_=True)
    gbox("cleat", (-0.71, 0.655, PLY_FRONT + 0.02), (0.06, 0.03, 0.014), "switch-black")
    # charm: pencil on the plywood (names, sums, crossings-out) and a coffee ring near the TEST switch
    for i, (x, y, w, a) in enumerate([(-0.12, 0.64, 0.16, 0.02), (-0.1, 0.615, 0.12, -0.01), (-0.13, 0.59, 0.18, 0.0),
                                      (0.02, 0.6, 0.08, 0.5), (0.0, 0.62, 0.06, -0.45)]):
        gbox(f"pencil{i}", (x, y, PLY_FRONT + 0.0004), (w, 0.0035, 0.001), "pencil", rot_z=a)
    for i, y in enumerate((-0.25, -0.27, -0.29)):
        gbox(f"sum{i}", (-0.02, y, PLY_FRONT + 0.0004), (0.07 - i * 0.015, 0.003, 0.001), "pencil")
    gbox("sum-line", (-0.02, -0.305, PLY_FRONT + 0.0004), (0.09, 0.002, 0.001), "pencil")
    ring("coffee-ring", (0.68, -0.33, PLY_FRONT + 0.0002), 0.032, 0.004, "coffee", segs=16)
    # the shelf from an offcut, on three brackets
    gbox("shelf", (0, -0.64, 0.1), (W, 0.04, 0.22), "switch-ply-shelf", bevel=0.004)
    gbox("shelf-lip", (0, -0.612, 0.205), (W, 0.016, 0.012), "switch-ply-shelf")
    for x in (-0.72, 0.0, 0.72):
        gbox(f"bracket{x}", (x, -0.72, PLY_FRONT + 0.004), (0.03, 0.12, 0.008), "switch-batten")
        gbeam(f"brace{x}", (x, -0.78, PLY_FRONT + 0.006), (x, -0.664, 0.18), 0.02, "switch-batten")
    if TAKE == "old":
        # a telex repair shop's spare board: old screw holes and a faded stencil
        label("stencil", "TELEX SVC 4", (0.0, -0.1, PLY_FRONT + 0.0002), 0.11, "stencil", extrude=0.0003)
        for i in range(10):
            gcyl(f"hole{i}", (-0.7 + i * 0.155, 0.32 - (i % 3) * 0.2, PLY_FRONT + 0.0003), 0.004, 0.001, "ink", sides=6)
    if TAKE == "bodge":
        # the upgrade panel bolted on crooked with odd screws, gaffer tape holding a tag strip
        gbox("panel-bodge", (0.62, 0.4, PLY_FRONT + 0.004), (0.3, 0.5, 0.008), "switch-panel", rot_z=0.04)
        for i, (x, y) in enumerate([(0.49, 0.62), (0.75, 0.63), (0.48, 0.16), (0.76, 0.18)]):
            screw(f"odd-screw{i}", (x, y, PLY_FRONT + 0.0095), r=0.005 + 0.002 * (i % 2), colour="switch-brass" if i % 2 else "screw", slot_angle=i)
        gbox("gaffer", (-0.62, 0.2, PLY_FRONT + 0.012), (0.08, 0.24, 0.002), "gaffer", rot_z=0.08)


def switch_terminal():
    """Two brass screw posts on a bakelite block; the jumper's near end lands on the front at z 0.03."""
    post = {"plain": "switch-brass", "old": "verdigris", "bodge": "tool-steel"}[TAKE]
    gbox("block", (0, 0, 0.008), (0.045, 0.04, 0.016), "switch-bakelite", bevel=0.002)
    if TAKE == "bodge":  # a bent nail with the wire twisted round it
        gbeam("nail", (0, -0.006, 0.016), (0.004, 0.006, 0.032), 0.004, "tool-steel", round_=True)
        ring("twist", (0.001, 0.0, 0.024), 0.006, 0.0015, "wire-dirty", axis="y", segs=8)
        return
    for i, sx in enumerate((-1, 1)):
        gcyl(f"post{sx}", (sx * 0.011, 0, 0.021), 0.0055, 0.011, post, sides=8)
        gcyl(f"head{sx}", (sx * 0.011, 0, 0.0275), 0.0075, 0.004, post, sides=8)
        gbox(f"slot{sx}", (sx * 0.011, 0, 0.0298), (0.012, 0.0018, 0.001), "ink", rot_z=0.5 if sx > 0 else 0.0)  # one slot cut crooked


def switch_port():
    """A jack on a bakelite block: brass collar ("collar", nickel on spares), dark hole for the plug."""
    gbox("block", (0, 0, 0.008), (0.045, 0.045, 0.016), "switch-black", bevel=0.002)
    collar = {"plain": "switch-brass", "old": "switch-brass-old", "bodge": "switch-brass"}[TAKE]
    gcyl("collar", (0, 0, 0.021), 0.0135, 0.01, collar, sides=12)
    gcyl("hole", (0, 0, 0.0262), 0.0075, 0.001, "ink", sides=10)
    screw("fix-top", (-0.016, 0.016, 0.0175), r=0.003)
    screw("fix-bottom", (0.016, -0.016, 0.0175), r=0.003)
    if TAKE == "bodge":  # known faulty: a scorch mark
        gcyl("scorch", (0.006, 0.008, 0.0164), 0.018, 0.001, "scorch", sides=7)


def switch_port_lamp():
    """A jewelled lamp: chrome bezel and a faceted lens ("lens": coloured by the game)."""
    gcyl("bezel", (0, 0, 0.007), 0.019, 0.014, "lamp-bezel", sides=10)
    gcyl("lens-base", (0, 0, 0.017), 0.0145, 0.006, "lamp-glass", sides=8)
    gcyl("lens-jewel", (0, 0, 0.025), 0.0145, 0.01, "lamp-glass", axis="z", sides=8, r2=0.006)
    if TAKE == "bodge":
        gbox("tape", (0, 0.014, 0.02), (0.04, 0.012, 0.002), "gaffer", rot_z=0.2)


def switch_tag():
    """A card in a brass holder. The game stretches it to the row height and writes the name on it."""
    card = "switch-tag" if TAKE != "old" else "paper-yellow"
    gbox("card", (0, 0, 0.002), (0.32, 0.034, 0.004), card)
    for sy in (-1, 1):
        gbox(f"rail{sy}", (0, sy * 0.0185, 0.003), (0.34, 0.004, 0.006), "switch-brass")
    for sx in (-1, 1):
        gbox(f"end{sx}", (sx * 0.168, 0, 0.003), (0.004, 0.04, 0.006), "switch-brass")
    if TAKE == "old":  # water-stained
        gcyl("stain", (0.06, -0.004, 0.0042), 0.016, 0.001, "card-back", sides=7)
    if TAKE == "bodge":  # rewritten on a strip of tape
        gbox("tape", (-0.03, 0, 0.0045), (0.2, 0.03, 0.001), "paper-white", rot_z=0.03)


def switch_jumper_plug():
    """A jumper's end, along Z: brass tip into the jack at -Z, the wire leaves at +Z. "sleeve" takes the wire's colour."""
    sleeve = {"plain": "wire-red", "old": "wire-dirty", "bodge": "wire-dirty"}[TAKE]
    gcyl("tip", (0, 0, -0.01), 0.0035, 0.01, "switch-brass", sides=8)
    gcyl("body", (0, 0, 0.001), 0.0072, 0.012, "switch-black", sides=8)
    gcyl("sleeve", (0, 0, 0.0105), 0.0078, 0.007, sleeve, sides=8)
    gcyl("boot", (0, 0, 0.0155), 0.0045, 0.004, sleeve, axis="z", sides=8, r2=0.003)
    if TAKE == "bodge":
        gbox("tape", (0, 0, 0.004), (0.017, 0.006, 0.017), "gaffer")


COILS = ("wire-red", "wire-blue", "wire-yellow", "wire-green", "wire-white")


def switch_jumper_hook():
    """Pivot: the new-jumper hook's spot (HookAt). New coloured coils hang at x 0, salvaged old wire at x +0.18."""
    left = 5 if TAKE != "bodge" else 2
    gbox("bar-new", (0, 0.06, 0.008), (0.16, 0.018, 0.016), "switch-brass", bevel=0.002)
    gbox("bar-old", (0.18, 0.06, 0.008), (0.1, 0.018, 0.016), "switch-brass-old", bevel=0.002)
    for x in (-0.07, 0.07, 0.14, 0.22):
        screw(f"bar-screw{x}", (x, 0.06, 0.0175), r=0.003)
    for i in range(left):
        x = -0.06 + i * 0.03
        gbeam(f"hook{i}", (x, 0.06, 0.016), (x, 0.06, 0.03), 0.004, "switch-brass", round_=True)
        ring(f"coil{i}", (x, 0.03, 0.026 + i * 0.002), 0.028, 0.0045, COILS[i], axis="z", segs=10)
        gbeam(f"tail{i}", (x + 0.02, 0.01, 0.03), (x + 0.012, -0.045 - (i % 2) * 0.015, 0.034), 0.006, COILS[i], round_=True)
        gcyl(f"tail-plug{i}", (x + 0.012, -0.052 - (i % 2) * 0.015, 0.034), 0.006, 0.014, "switch-black", axis="y", sides=6)
    for i in range(3):
        x = 0.15 + i * 0.03
        gbeam(f"old-hook{i}", (x, 0.06, 0.016), (x, 0.06, 0.03), 0.004, "switch-brass-old", round_=True)
        ring(f"old-coil{i}", (x, 0.028 - i * 0.004, 0.026 + i * 0.002), 0.026 + i * 0.003, 0.0045, "wire-dirty", axis="z", segs=8)
        gbeam(f"old-tail{i}", (x - 0.015, 0.0, 0.03), (x - 0.03 + i * 0.02, -0.05, 0.036), 0.006, "wire-dirty", round_=True)


def tool_tone_probe():
    """A yellow probe lying along X; the steel tip at +X (anchor tip 0.13, 0, 0)."""
    body = "tool-probe" if TAKE != "old" else "tool-probe-dark"
    gbox("body", (-0.02, 0, 0), (0.14, 0.04, 0.04), body, bevel=0.008)
    gcyl("nose", (0.07, 0, 0), 0.018, 0.04, body, axis="x", sides=8, r2=0.006)
    gcyl("tip", (0.11, 0, 0), 0.003, 0.04, "tool-steel", axis="x", sides=6)
    gbox("grip", (-0.05, 0, 0), (0.06, 0.042, 0.042), "switch-black", bevel=0.004)
    gcyl("led", (0.02, 0.021, 0), 0.004, 0.004, "led-red", axis="y", sides=6)
    gbox("speaker", (-0.005, 0.0205, 0), (0.025, 0.002, 0.02), "ink")
    gcyl("lanyard", (-0.095, 0, 0), 0.008, 0.002, "ink", axis="z", sides=8)
    if TAKE == "bodge":
        gbox("tape", (-0.02, 0, 0), (0.025, 0.044, 0.044), "gaffer")


def tool_test_set():
    """A chunky butt-set lying along X, earpiece and mouthpiece facing down; leads and clips at +X."""
    body = "tool-test-set" if TAKE != "old" else "tool-test-orange"
    gbox("handle", (-0.01, 0.008, 0), (0.13, 0.03, 0.05), body, bevel=0.008)
    for sx, name in ((-1, "ear"), (1, "mouth")):
        gcyl(name, (sx * 0.065 - 0.01, -0.012, 0), 0.03, 0.03, body, axis="y", sides=10)
        gcyl(name + "-grille", (sx * 0.065 - 0.01, -0.028, 0), 0.022, 0.002, "ink", axis="y", sides=10)
    gbox("keypad", (-0.01, 0.024, 0), (0.05, 0.003, 0.032), "ink")
    gbox("switch", (-0.035, 0.026, 0), (0.012, 0.006, 0.01), "switch-label")
    for i, col in enumerate(("wire-red", "switch-black")):
        z = -0.01 + i * 0.02
        gbeam(f"lead{i}", (0.06, 0.0, z), (0.095, -0.02, z * 1.5), 0.005, col, round_=True)
        gbox(f"clip{i}", (0.103, -0.022, z * 1.5), (0.02, 0.008, 0.008), col if TAKE != "bodge" else "tool-steel")
        gbox(f"jaw{i}", (0.112, -0.022, z * 1.5), (0.012, 0.006, 0.01), "tool-steel")


def switch_throw_switch():
    """A knife switch on a slate. "blade" pivots at the hinge (y -0.035); the game swings it out when thrown."""
    gbox("slate", (0, 0, 0.01), (0.15, 0.19, 0.02), "switch-slate", bevel=0.003)
    for sx in (-1, 1):
        for sy in (-1, 1):
            screw(f"slate-screw{sx}{sy}", (sx * 0.06, sy * 0.08, 0.0215), r=0.004)
    knife = "switch-knife" if TAKE != "old" else "switch-brass-old"
    for sx in (-1, 1):
        gbox(f"jaw{sx}", (sx * 0.012, 0.075, 0.032), (0.006, 0.03, 0.024), "switch-brass")
        gbox(f"hinge{sx}", (sx * 0.012, -0.035, 0.032), (0.006, 0.026, 0.024), "switch-brass")
    gbox("jaw-base", (0, 0.075, 0.022), (0.04, 0.03, 0.004), "switch-brass")
    gbox("hinge-base", (0, -0.035, 0.022), (0.04, 0.026, 0.004), "switch-brass")
    pivot = node("blade", (0, -0.035, 0.036))
    gbox("blade-bar", (0, 0.02, 0.036), (0.012, 0.11, 0.004), knife, parent=pivot)
    gcyl("blade-pin", (0, -0.035, 0.036), 0.004, 0.034, "switch-brass", axis="x", sides=6, parent=pivot)
    gbeam("handle-stem", (0, 0.05, 0.036), (0, 0.05, 0.075), 0.008, "switch-brass", round_=True, parent=pivot)
    gcyl("handle-grip", (0, 0.05, 0.095), 0.011, 0.045, "switch-black" if TAKE != "bodge" else "gaffer", axis="z", sides=8, parent=pivot)
    gcyl("handle-knob", (0, 0.05, 0.119), 0.013, 0.006, "switch-black", axis="z", sides=8, parent=pivot)


def switch_upgrade_panel():
    """The price sign where the $120 panel goes: a steel plate with four screws (the price is a label in the game)."""
    gbox("plate", (0, 0, 0.004), (0.28, 0.06, 0.008), "switch-panel", bevel=0.002, rot_z=0.0 if TAKE != "bodge" else 0.05)
    for sx in (-1, 1):
        for sy in (-1, 1):
            screw(f"screw{sx}{sy}", (sx * 0.128, sy * 0.019, 0.0095), r=0.0035)
    gbeam("string", (-0.1, 0.03, 0.006), (0.0, 0.07, 0.004), 0.002, "string")
    gbeam("string2", (0.1, 0.03, 0.006), (0.0, 0.07, 0.004), 0.002, "string")
    gcyl("nail", (0.0, 0.07, 0.006), 0.003, 0.012, "tool-steel", axis="z", sides=6)


# ======================================================================================== the line rack
# LineRack.cs: 0.62 x 1.0, centred; slots from (-0.225, 0.24) every 0.09 across and 0.1 down, 6 x 2; lamps at z 0.035;
# title at (0, 0.4, 0.03); tally paper (0.5 x 0.36) at (0, -0.27, 0.03), its text at z 0.04.
def slot_at(i):
    return -0.225 + (i % 6) * 0.09, 0.24 - (i // 6) * 0.1


def room_line_rack():
    gbox("back", (0, 0, -0.02), (0.62, 1.0, 0.01), "rack-body")
    for sx in (-1, 1):
        gbox(f"side{sx}", (sx * 0.305, 0, 0.0), (0.01, 1.0, 0.05), "rack-body")
    for sy in (-1, 1):
        gbox(f"cap{sy}", (0, sy * 0.495, 0.0), (0.62, 0.01, 0.05), "rack-body")
    gbox("face", (0, 0, 0.02), (0.6, 0.98, 0.006), "rack-face")
    for sx in (-1, 1):
        for sy in (-1, 1):
            screw(f"face-screw{sx}{sy}", (sx * 0.28, sy * 0.47, 0.0245), r=0.005)
    # the card cage: a lamp on top of each slot, the slot for the card below it
    for i in range(12):
        x, y = slot_at(i)
        n = f"{i + 1:02d}"
        gcyl(f"bezel-{n}", (x, y + 0.018, 0.026), 0.016, 0.006, "lamp-bezel", sides=10)
        gcyl(f"lens-{n}", (x, y + 0.018, 0.031), 0.012, 0.008, "lamp-glass", axis="z", sides=8, r2=0.006)
        gbox(f"slot-{n}", (x, y - 0.025, 0.0232), (0.06, 0.04, 0.001), "rack-slot")
        card = gbox(f"card-{n}", (x, y - 0.025, 0.027), (0.058, 0.038, 0.008), "rack-card", bevel=0.001)
        gbox(f"card-{n}-tab", (x, y - 0.025, 0.033), (0.03, 0.008, 0.006), "switch-label", parent=card)
        gcyl(f"card-{n}-screw", (x + 0.022, y - 0.04, 0.032), 0.0035, 0.004, "screw", axis="z", sides=8, parent=card)
    # separators between the rows of slots
    for row in range(3):
        gbox(f"rule{row}", (0, 0.295 - row * 0.1, 0.0235), (0.56, 0.004, 0.002), "rack-label")
    # the tally card on a clip, and the pencil hanging off it
    gbox("tally", (0, -0.27, 0.0245), (0.5, 0.36, 0.003), "rack-tally")
    gbox("tally-clip", (0, -0.095, 0.03), (0.08, 0.025, 0.01), "clip-steel", bevel=0.002)
    gbeam("tally-clip-loop", (-0.025, -0.08, 0.03), (0.025, -0.08, 0.03), 0.003, "clip-steel", round_=True)
    gbeam("pencil-string", (0.245, -0.12, 0.028), (0.27, -0.4, 0.03), 0.002, "string")
    gbeam("pencil", (0.27, -0.4, 0.032), (0.262, -0.48, 0.032), 0.007, "pin-yellow", round_=True)
    # where the next card goes: the code moves this to the first empty slot
    x, y = slot_at(0)
    nxt = node("next-slot", (x, y, 0.0))
    node("wp-01-place", (x, y - 0.025, 0.05), parent=nxt)
    node("wp-02-plug", (x, y - 0.02, 0.035), parent=nxt)
    node("wp-03-screw", (x + 0.022, y - 0.04, 0.036), parent=nxt)


# ======================================================================================== the wall boards (DeskWall.cs)
def pin(name, c, colour):
    gcyl(name, c, 0.007, 0.006, colour, axis="z", sides=8)
    gcyl(name + "-top", (c[0], c[1], c[2] + 0.005), 0.004, 0.004, colour, axis="z", sides=6)


def frame(prefix, w, h, z_front, header=0.06, colour="pine"):
    """A pine frame round a board w x h (outside size), the top rail `header` tall for the painted title."""
    d = z_front + 0.015
    side = 0.04
    gbox(f"{prefix}-top", (0, h / 2 - header / 2, z_front - d / 2), (w, header, d), colour, bevel=0.003)
    gbox(f"{prefix}-bottom", (0, -h / 2 + side / 2, z_front - d / 2), (w, side, d), colour, bevel=0.003)
    for sx in (-1, 1):
        gbox(f"{prefix}-side{sx}", (sx * (w / 2 - side / 2), -header / 2 + side / 2 + 0.0, z_front - d / 2), (side, h - header - side + 0.002, d), colour, bevel=0.003)


def room_ticket_rail():
    """Cork in a pine frame, four ticket rails (a steel ball holds each slip), done tickets on a nail."""
    frame("frame", 1.36, 1.0, 0.016, header=0.1)
    gbox("cork", (0, -0.03, 0.002), (1.28, 0.86, 0.02), "cork")
    for r in range(4):
        y = 0.24 - r * 0.21 + 0.098
        gbox(f"rail{r}", (0, y, 0.022), (1.28, 0.016, 0.02), "rail-alu", bevel=0.002)
        gbox(f"rail{r}-lip", (0, y - 0.006, 0.033), (1.28, 0.004, 0.004), "rail-alu")
        for c in (-1, 1):
            gcyl(f"ball{r}{c}", (c * 0.33, y - 0.008, 0.034), 0.006, 0.006, "tool-steel", axis="z", sides=8)
        for sx in (-1, 1):
            screw(f"rail{r}-screw{sx}", (sx * 0.62, y, 0.0325), r=0.004)
    # charm: done tickets stuck on a nail in the header, the way they pile up
    gcyl("done-nail", (0.56, 0.45, 0.03), 0.003, 0.03, "tool-steel", axis="z", sides=6)
    for i, col in enumerate(("paper", "paper-pink", "paper-blue")):
        gbox(f"done{i}", (0.56 + 0.006 * i, 0.44 - 0.004 * i, 0.019 + i * 0.002), (0.1, 0.05, 0.002), col, rot_z=0.25 * i - 0.2)


def room_wall_calendar():
    """A tear-off calendar on a string from a nail: a picture of a pole line on the hill, wire rings, the month pad."""
    gbeam("string-l", (-0.3, 0.43, 0.0), (0, 0.56, -0.004), 0.003, "string")
    gbeam("string-r", (0.3, 0.43, 0.0), (0, 0.56, -0.004), 0.003, "string")
    gcyl("nail", (0, 0.56, 0.0), 0.004, 0.02, "tool-steel", axis="z", sides=6)
    gbox("card", (0, 0, -0.006), (0.7, 0.86, 0.004), "card-back")
    # the picture: sky, hills and a little pole line (it's a telephone company calendar)
    gbox("sky", (0, 0.37, -0.001), (0.7, 0.12, 0.004), "calendar-sky")
    gbox("hill", (-0.12, 0.33, 0.002), (0.46, 0.04, 0.002), "calendar-hill", rot_z=0.06)
    gbox("hill2", (0.2, 0.335, 0.0025), (0.32, 0.05, 0.002), "calendar-hill-2", rot_z=-0.08)
    for i, x in enumerate((-0.2, -0.05, 0.1, 0.25)):
        gbox(f"pole{i}", (x, 0.37 - i * 0.004, 0.004), (0.006, 0.07, 0.001), "ink")
        gbox(f"arm{i}", (x, 0.395 - i * 0.004, 0.004), (0.03, 0.004, 0.001), "ink")
    gbeam("wire-a", (-0.2, 0.395, 0.0045), (0.25, 0.383, 0.0045), 0.0015, "ink")
    gbox("band", (0, 0.302, 0.0), (0.7, 0.016, 0.004), "calendar-red")
    gbox("pad", (0, -0.06, 0.0), (0.68, 0.72, 0.006), "paper")
    for i in range(7):
        ring(f"ring{i}", (-0.27 + i * 0.09, 0.43, 0.0), 0.012, 0.0025, "ring", axis="x", segs=8)
    gbox("torn-stub", (0, 0.296, 0.0035), (0.66, 0.008, 0.001), "paper")


def room_timesheet():
    """A clipboard: masonite board, steel clip at the top, the sheet with its navy header band, pencil on a string."""
    gbox("board", (0, 0.02, -0.007), (0.66, 0.92, 0.006), "masonite", bevel=0.004)
    gbox("sheet", (0, 0.0, -0.003), (0.62, 0.82, 0.002), "paper-white")
    gbox("header", (0, 0.37, -0.0015), (0.62, 0.08, 0.002), "navy")
    gbox("margin", (-0.294, -0.04, -0.0015), (0.003, 0.74, 0.001), "margin-red")
    gbox("clip-plate", (0, 0.44, 0.0), (0.18, 0.05, 0.006), "clip-steel", bevel=0.002)
    gbox("clip-lever", (0, 0.462, 0.008), (0.12, 0.012, 0.012), "clip-steel", bevel=0.003)
    for sx in (-1, 1):
        gcyl(f"rivet{sx}", (sx * 0.07, 0.44, 0.004), 0.005, 0.004, "tool-steel", axis="z", sides=6)
    gbeam("pencil-string", (0.09, 0.44, 0.002), (0.33, 0.1, 0.004), 0.002, "string")
    gbeam("pencil", (0.335, 0.1, 0.006), (0.335, -0.06, 0.006), 0.008, "pin-yellow", round_=True)
    gcyl("pencil-tip", (0.335, -0.068, 0.006), 0.004, 0.012, "ink", axis="y", sides=6)


def room_ad_board():
    """A pinboard: cork in pine, two leaflets with tear-off phone strips (two taken), the card the game writes on."""
    frame("frame", 0.8, 0.6, 0.015, header=0.08)
    gbox("cork", (0, -0.02, 0.0), (0.74, 0.48, 0.02), "cork")
    gbox("leaflet-a", (-0.22, 0.08, 0.0115), (0.22, 0.22, 0.002), "paper-yellow", rot_z=0.03)
    gbox("leaflet-a-band", (-0.22, 0.16, 0.0128), (0.2, 0.03, 0.001), "calendar-red", rot_z=0.03)
    gbox("leaflet-b", (0.2, 0.08, 0.0115), (0.28, 0.2, 0.002), "paper-white", rot_z=-0.02)
    gbox("leaflet-b-band", (0.2, 0.155, 0.0128), (0.26, 0.025, 0.001), "navy", rot_z=-0.02)
    for i in range(7):
        if i in (2, 5):
            continue  # two strips torn off: someone rang
        gbox(f"strip{i}", (0.088 + i * 0.037, -0.04, 0.0128), (0.03, 0.05, 0.001), "paper-white", rot_z=-0.02)
    gbox("card", (0, -0.2, 0.0125), (0.72, 0.16, 0.002), "paper")
    for i, (x, y, col) in enumerate([(-0.31, 0.18, "pin-red"), (-0.13, 0.18, "pin-blue"), (0.08, 0.17, "pin-green"),
                                     (0.32, 0.17, "pin-yellow"), (-0.34, -0.13, "pin-red"), (0.34, -0.13, "pin-blue")]):
        pin(f"pin{i}", (x, y, 0.016), col)


# ======================================================================================== registry
PROPS = {
    "room.switchboard": (room_switchboard, "station origin: centre of the board; battens on the wall at z -0.02, plywood face at z 0.034"),
    "switch.terminal": (switch_terminal, "back-centre"),
    "switch.port": (switch_port, "back-centre"),
    "switch.port-lamp": (switch_port_lamp, "back-centre"),
    "switch.tag": (switch_tag, "back-centre"),
    "switch.jumper-plug": (switch_jumper_plug, "centre; tip into the jack at -Z, wire out at +Z"),
    "switch.jumper-hook": (switch_jumper_hook, "the new-jumper hook's spot (HookAt), back of the bar"),
    "tool.tone-probe": (tool_tone_probe, "centre"),
    "tool.test-set": (tool_test_set, "centre"),
    "switch.throw-switch": (switch_throw_switch, "back-centre"),
    "switch.upgrade-panel": (switch_upgrade_panel, "back-centre"),
    "room.line-rack": (room_line_rack, "centre of the rack (LineRack's origin)"),
    "room.ticket-rail": (room_ticket_rail, "centre of the board (TicketRail's origin)"),
    "room.wall-calendar": (room_wall_calendar, "centre of the card (WallCalendar's origin)"),
    "room.timesheet": (room_timesheet, "centre of the sheet (Timesheet's origin)"),
    "room.ad-board": (room_ad_board, "centre of the board (AdBoard's origin)"),
}

NEW_BRIEFS = {   # props not in the switchboard batch: name, what
    "room.ticket-rail": ("Ticket rail", "The job tickets board on the left wall: cork in a pine frame, four steel ticket rails with a ball holding each slip, done tickets on a nail in the header. The slips are drawn by the game."),
    "room.wall-calendar": ("Wall calendar", "A telephone company's tear-off calendar on a string from a nail: a picture of a pole line on the hills, wire rings, the month pad the game writes on."),
    "room.timesheet": ("Timesheet clipboard", "Tomorrow's plan on a masonite clipboard: steel clip, a sheet with a navy header band, a pencil on a string. The game writes the hours."),
    "room.ad-board": ("Ad board", "A pinboard: cork in pine, two leaflets with tear-off phone strips (two taken), coloured pins, and the card the game writes what's running on."),
}


def bounds():
    from mathutils import Vector
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    dg = bpy.context.evaluated_depsgraph_get()
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        for vv in o.evaluated_get(dg).bound_box:
            w = o.matrix_world @ Vector(vv)
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi


def build(pid):
    fn, pivot = PROPS[pid]
    k.reset()
    fn()
    bpy.context.view_layer.update()
    lo, hi = bounds()
    size = hi - lo
    centre = (lo + hi) / 2
    span = max(size.x, size.y, size.z)
    # these all hang on a wall: look from in front (Blender -Y), a little to the right and above
    k.preview(pid, distance=span * 1.6 + 0.05, height=centre.z + span * 0.35, target=tuple(centre), views=((200, "front"),))
    k.export(pid)
    os.replace(os.path.join(k.PREVIEWS, pid + "-front.png"), os.path.join(k.PREVIEWS, pid + ".png"))
    out = {"id": pid, "size": [round(size.x, 3), round(size.z, 3), round(size.y, 3)], "pivot": pivot, "tris": k.triangle_count(), "status": "modelled"}
    if pid in NEW_BRIEFS:
        out["name"], out["what"] = NEW_BRIEFS[pid]
    return out


def update_briefs(results):
    path = os.path.join(k.ROOT, "game", "content", "props.json")
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    by_id = {p["id"]: p for p in data["props"]}
    for r in results:
        if r["id"] in by_id:
            by_id[r["id"]].update({"size": r["size"], "pivot": r["pivot"], "tris": r["tris"], "status": "modelled"})
            if "what" in r:
                by_id[r["id"]].update({"name": r["name"], "what": r["what"]})
        else:
            data["props"].append({"id": r["id"], "name": r["name"], "what": r["what"], "size": r["size"], "pivot": r["pivot"],
                                  "tris": r["tris"], "usedBy": ["desk"], "status": "modelled"})
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1, ensure_ascii=False)
        f.write("\n")


def takes_sheet():
    """Every switchboard part in its three takes (plain / old / bodge), side by side: one picture for the owner to pick from."""
    global TAKE
    from mathutils import Vector
    ids = [p for p in PROPS if p.startswith(("switch.", "tool."))] + ["room.switchboard"]
    k.reset()
    k.PALETTE["label-ink"] = (0.1, 0.1, 0.12)
    cell = 1.4
    for row, pid in enumerate(ids):
        for col, take in enumerate(("plain", "old", "bodge")):
            TAKE = take
            before = set(bpy.context.scene.objects)
            PROPS[pid][0]()
            bpy.context.view_layer.update()
            new = [o for o in bpy.context.scene.objects if o not in before]
            holder = bpy.data.objects.new(f"{pid}-{take}", None)
            bpy.context.scene.collection.objects.link(holder)
            for o in new:
                if o.parent is None:
                    o.parent = holder
            bpy.context.view_layer.update()
            lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
            for o in new:
                if o.type == "MESH":
                    for v in o.bound_box:
                        w = o.matrix_world @ Vector(v)
                        lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
            s = 1.0 / max(hi.x - lo.x, hi.z - lo.z)
            holder.scale = (s, s, s)
            holder.location = (col * cell - (lo.x + hi.x) / 2 * s, 0, -row * cell - (lo.z + hi.z) / 2 * s)
            if row == 0:
                k.text(f"head{col}", take.upper(), (col * cell, -0.1, 0.75), 0.16, k.mat("label-ink"), rotation=(math.pi / 2, 0, 0))
        k.text(f"lbl{row}", pid, (-1.75, -0.1, -row * cell), 0.13, k.mat("label-ink"), rotation=(math.pi / 2, 0, 0))
    TAKE = "plain"
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_cavity = True
    rows = len(ids)
    width, height = 3 * cell + 2.6, rows * cell + 1.0
    scene.render.resolution_x, scene.render.resolution_y = int(2600 * width / height), 2600
    w = bpy.data.worlds.new("w"); w.color = (0.86, 0.88, 0.9); scene.world = w
    scene.display.shading.background_type = "WORLD"
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(width, height)
    cam.location = (cell - 1.3 + 0.0, -10, -(rows - 1) * cell / 2 + 0.25)
    cam.rotation_euler = (math.radians(90), 0, 0)
    scene.render.filepath = os.path.join(k.PREVIEWS, "takes-switchboard.png")
    bpy.ops.render.render(write_still=True)
    print("TAKES", scene.render.filepath)


if __name__ == "__main__":
    want = k.args() or ["all"]
    if want == ["takes"]:
        takes_sheet()
        sys.exit(0)
    ids = list(PROPS) if want == ["all"] else [i for w in want for i in PROPS if i == w or (w.endswith("*") and i.startswith(w[:-1]))]
    results = []
    for pid in ids:
        results.append(build(pid))
        print(f"BUILT {pid}: {results[-1]['tris']} tris, size {results[-1]['size']}")
    update_briefs(results)
    print(f"ROOM OK {len(results)} props")
