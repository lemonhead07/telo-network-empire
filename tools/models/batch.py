"""Every model the player handles through Act 5, one builder per prop id.

    blender -b --factory-startup --python tools/models/batch.py -- all
    blender -b --factory-startup --python tools/models/batch.py -- kit.ladder site.cell-mast

Each prop: game/assets/models/<id>.glb, a preview in docs/img/props/<id>.png, and its brief in
game/content/props.json (added if missing; size, tris and status kept up to date).

Axes (see _kit.py): Blender X right, Z up, -Y is the side that faces Godot +Z.
  * Carried kit: the pivot is the bottom centre, as the old boxes in Cargo.cs were.
  * Install parts: the pivot is the centre, as the old boxes in InstallSite.cs were. Things on the outside wall
    face -Y (out of the wall); the indoor socket faces +X (out of the left wall).
  * Project sites: the pivot is ground centre; the side you work at faces -Y.
"""
import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(__file__))
import _kit as k  # noqa: E402
from _kit import box, cylinder, cone, beam, text, empty  # noqa: E402

k.PALETTE.update({
    "wood": (0.72, 0.58, 0.36), "wood-dark": (0.45, 0.33, 0.2), "pole-brown": (0.33, 0.24, 0.16), "creosote": (0.16, 0.12, 0.08),
    "rubber": (0.1, 0.1, 0.11), "steel": (0.62, 0.64, 0.66), "galv": (0.7, 0.72, 0.72), "brass": (0.78, 0.62, 0.25),
    "toolbox-red": (0.7, 0.16, 0.12), "toolbox-red-dark": (0.55, 0.12, 0.09), "cardboard": (0.72, 0.57, 0.38), "tape": (0.82, 0.72, 0.5),
    "label": (0.95, 0.94, 0.88), "ink": (0.12, 0.12, 0.14), "beige": (0.82, 0.74, 0.56), "beige-dark": (0.62, 0.56, 0.42),
    "pcb": (0.1, 0.36, 0.2), "chip": (0.08, 0.08, 0.09), "gold": (0.85, 0.7, 0.3), "led-red": (0.95, 0.2, 0.15), "led-green": (0.3, 0.9, 0.35),
    "wall-grey": (0.55, 0.56, 0.55), "cream": (0.92, 0.88, 0.76), "phone-red": (0.55, 0.13, 0.1), "phone-dark": (0.42, 0.1, 0.08),
    "dial": (0.93, 0.92, 0.86), "paper": (0.95, 0.93, 0.86), "line": (0.6, 0.66, 0.78), "clip-brown": (0.45, 0.3, 0.18),
    "insulator": (0.55, 0.78, 0.7), "cabinet-green": (0.22, 0.38, 0.28), "cabinet-grey": (0.6, 0.62, 0.6), "concrete": (0.68, 0.67, 0.64),
    "brick": (0.6, 0.32, 0.24), "roof-felt": (0.2, 0.2, 0.22), "white-paint": (0.9, 0.9, 0.88), "corrugated": (0.78, 0.8, 0.8),
    "orange": (0.95, 0.5, 0.1), "yellow": (0.95, 0.8, 0.15), "nez-blue": (0.12, 0.2, 0.5), "dish": (0.88, 0.88, 0.86),
    "fence": (0.5, 0.52, 0.5), "duct": (0.95, 0.45, 0.12), "glass-dark": (0.2, 0.29, 0.35), "sticker": (0.95, 0.85, 0.2),
    "earth": (0.36, 0.26, 0.16),
})

M = k.mat  # material by palette name


def T(name, body, center, size, colour, face="-Y", extrude=0.002):
    """Lettering facing out of a side: -Y (front), +Y (back), +X, -X, or up (+Z)."""
    rot = {"-Y": (math.pi / 2, 0, 0), "+Y": (math.pi / 2, 0, math.pi), "+X": (math.pi / 2, 0, math.pi / 2),
           "-X": (math.pi / 2, 0, -math.pi / 2), "+Z": (0, 0, 0)}[face]
    return text(name, body, center, size, M(colour), rotation=rot, extrude=extrude)


# ======================================================================== carried kit (bottom-centre pivot)

def kit_ladder():
    for sx in (-1, 1):
        box(f"rail{sx}", (sx * 0.2, 0, 1.5), (0.05, 0.07, 3.0), M("wood"), bevel=0.008)
        box(f"foot{sx}", (sx * 0.2, 0, 0.03), (0.07, 0.09, 0.06), M("rubber"))
        box(f"bracket{sx}", (sx * 0.2, 0, 1.52), (0.065, 0.085, 0.1), M("galv"))
    for i, z in enumerate([0.3 + 0.3 * j for j in range(9)]):
        cylinder(f"rung{i}", (0, 0, z), 0.018, 0.4, M("wood-dark" if i == 4 else "wood"), axis="X", sides=6)
    box("paint-splash", (0.05, -0.02, 1.52), (0.06, 0.01, 0.02), M("white-paint"))  # someone else's decorating


def kit_pole():
    cone("pole", (0, 0, 3.0), 0.12, 0.09, 6.0, M("pole-brown"), sides=8)
    cone("creosote", (0, 0, 0.25), 0.125, 0.12, 0.5, M("creosote"), sides=8)
    cone("cap", (0, 0, 6.03), 0.095, 0.0, 0.08, M("galv"), sides=8)
    for i, z in enumerate([2.2 + 0.4 * j for j in range(9)]):
        sx = 1 if i % 2 else -1
        box(f"step{i}", (sx * 0.15, 0, z), (0.12, 0.02, 0.02), M("galv"))
    box("tag", (0, -0.112, 1.6), (0.07, 0.01, 0.05), M("brass"))


def kit_cable_drum():
    for z in (0.02, 0.32):
        cylinder(f"flange{z}", (0, 0, z), 0.24, 0.04, M("wood"), sides=12)
    cylinder("cable", (0, 0, 0.17), 0.18, 0.26, M("rubber"), sides=12)
    cylinder("hub", (0, 0, 0.17), 0.05, 0.345, M("wood-dark"), sides=8)
    for i in range(4):
        a = i * math.pi / 2 + 0.4
        cylinder(f"bolt{i}", (math.cos(a) * 0.2, math.sin(a) * 0.2, 0.17), 0.008, 0.36, M("galv"), sides=6)
    beam("tail", (0.18, -0.02, 0.28), (0.3, -0.12, 0.02), 0.02, M("rubber"))
    T("stencil", "TELO", (0, 0, 0.342), 0.07, "ink", face="+Z")


def kit_toolbox():
    box("body", (0, 0, 0.09), (0.46, 0.2, 0.18), M("toolbox-red"), bevel=0.01)
    box("lid", (0, 0, 0.2), (0.47, 0.21, 0.04), M("toolbox-red-dark"), bevel=0.01)
    for sx in (-1, 1):
        box(f"upright{sx}", (sx * 0.09, 0, 0.245), (0.02, 0.02, 0.05), M("steel"))
        box(f"latch{sx}", (sx * 0.15, -0.106, 0.18), (0.04, 0.012, 0.05), M("steel"))
    box("handle", (0, 0, 0.27), (0.22, 0.03, 0.025), M("rubber"), bevel=0.006)
    box("sticker", (0.12, -0.101, 0.08), (0.08, 0.004, 0.05), M("sticker"))


def kit_modem_box():
    box("carton", (0, 0, 0.07), (0.36, 0.28, 0.14), M("cardboard"))
    box("tape", (0, 0, 0.1415), (0.06, 0.285, 0.003), M("tape"))
    box("label", (0.09, 0.02, 0.1415), (0.14, 0.11, 0.003), M("label"))
    T("label-text", "MODEM 300", (0.09, 0.04, 0.144), 0.022, "ink", face="+Z")
    box("picture", (0, -0.141, 0.075), (0.18, 0.003, 0.06), M("beige"))
    box("picture-lights", (0.05, -0.143, 0.075), (0.05, 0.003, 0.01), M("led-red"))
    T("arrow", "^ ^", (-0.12, -0.142, 0.1), 0.03, "ink", face="-Y")


def kit_line_card():
    box("pcb", (0, 0, 0.012), (0.3, 0.2, 0.012), M("pcb"))
    for i, (x, y) in enumerate([(-0.06, 0.04), (-0.06, -0.04), (0.04, 0.02)]):
        box(f"chip{i}", (x, y, 0.025), (0.06, 0.025, 0.012), M("chip"))
    for i in range(6):
        cylinder(f"cap{i}", (0.06 + (i % 3) * 0.02, -0.06 + (i // 3) * 0.02, 0.028), 0.006, 0.02, M("nez-blue"), sides=6)
    box("edge", (0.142, 0, 0.012), (0.02, 0.17, 0.016), M("gold"))
    box("faceplate", (-0.152, 0, 0.035), (0.01, 0.2, 0.06), M("steel"))
    cylinder("lamp", (-0.158, 0.06, 0.04), 0.008, 0.006, M("led-red"), axis="X", sides=8)
    box("pull", (-0.163, -0.05, 0.035), (0.012, 0.04, 0.03), M("rubber"))


# ======================================================================== install parts (centre pivot)

def install_wall_clip():
    box("plate", (0, 0.022, 0), (0.06, 0.012, 0.06), M("galv"), bevel=0.004)
    cylinder("screw", (0, 0.014, 0.012), 0.008, 0.006, M("steel"), axis="Y", sides=6)
    box("arm", (0, -0.005, -0.01), (0.012, 0.045, 0.012), M("galv"))
    beam("hook", (0, -0.026, -0.01), (0, -0.034, 0.025), 0.012, M("galv"))


def install_junction_box():
    box("base", (0, 0, 0), (0.2, 0.06, 0.26), M("wall-grey"), bevel=0.006)
    for i, z in enumerate((0.06, 0.0, -0.06)):
        for sx in (-1, 1):
            cylinder(f"post{i}{sx}", (sx * 0.04, -0.035, z), 0.009, 0.015, M("brass"), axis="Y", sides=8)
    cylinder("gland", (0, 0, -0.14), 0.015, 0.03, M("rubber"), sides=8)


def install_junction_cover():
    box("cover", (0, 0, 0), (0.22, 0.03, 0.28), M("cream"), bevel=0.008)
    T("emboss", "TELO", (0, -0.016, 0.06), 0.045, "wall-grey", face="-Y")
    for z in (0.11, -0.11):
        cylinder(f"screw{z}", (0, -0.016, z), 0.008, 0.004, M("steel"), axis="Y", sides=6)


def install_socket_back():
    box("back", (0, 0, 0), (0.03, 0.1, 0.1), M("wall-grey"))
    for y in (-0.025, 0.025):
        cylinder(f"term{y}", (0.017, y, 0), 0.006, 0.006, M("brass"), axis="X", sides=6)


def install_socket_face():
    box("face", (0, 0, 0), (0.02, 0.12, 0.12), M("cream"), bevel=0.006)
    box("slot", (0.011, 0, -0.01), (0.004, 0.05, 0.03), M("ink"))
    for z in (0.04, -0.045):
        cylinder(f"screw{z}", (0.011, 0, z), 0.006, 0.003, M("steel"), axis="X", sides=6)


def install_modem():
    box("body", (0, 0, -0.005), (0.3, 0.22, 0.06), M("beige"), bevel=0.01)
    box("panel", (0, -0.111, -0.005), (0.24, 0.004, 0.03), M("beige-dark"))
    for i, c in enumerate(("led-red", "led-green", "led-green", "led-green")):
        cylinder(f"led{i}", (-0.09 + i * 0.03, -0.113, -0.005), 0.005, 0.004, M(c), axis="Y", sides=6)
    for sx in (-1, 1):  # acoustic coupler cups: the handset sits in these
        cylinder(f"cup{sx}", (sx * 0.08, 0.02, 0.03), 0.04, 0.03, M("rubber"), sides=10)
        cylinder(f"cup-hole{sx}", (sx * 0.08, 0.02, 0.0451), 0.025, 0.002, M("chip"), sides=10)
    for i in range(5):
        box(f"vent{i}", (-0.02 + i * 0.02, 0.07, 0.0255), (0.01, 0.05, 0.002), M("beige-dark"))


def install_telephone():
    box("base", (0, 0.01, -0.025), (0.2, 0.2, 0.05), M("phone-red"), bevel=0.012)
    box("body", (0, 0.03, 0.02), (0.17, 0.15, 0.06), M("phone-red"), bevel=0.02, segments=2,
        rot=(math.radians(-12), 0, 0))
    dial = empty("dial", (0, -0.055, 0.035))
    dial.rotation_euler = (math.radians(50), 0, 0)
    cylinder("dial-plate", (0, 0, 0), 0.055, 0.012, M("dial"), sides=16, parent=dial)
    for i in range(10):
        a = math.radians(-60 + i * 28)
        cylinder(f"hole{i}", (math.cos(a) * 0.038, math.sin(a) * 0.038, 0.007), 0.008, 0.004, M("ink"), sides=6, parent=dial)
    cylinder("dial-centre", (0, 0, 0.008), 0.018, 0.004, M("label"), sides=10, parent=dial)
    # handset across the cradle
    box("handset", (0, 0.035, 0.07), (0.24, 0.045, 0.035), M("phone-dark"), bevel=0.012)
    for sx in (-1, 1):
        cylinder(f"cup{sx}", (sx * 0.1, 0.035, 0.055), 0.03, 0.03, M("phone-dark"), sides=10)
        box(f"prong{sx}", (sx * 0.06, 0.035, 0.05), (0.02, 0.03, 0.025), M("phone-red"))
    beam("cord", (0.1, 0.09, 0.0), (0.14, 0.12, -0.04), 0.012, M("phone-dark"), round_=True)


def install_job_sheet():
    box("board", (0, 0, 0), (0.3, 0.03, 0.4), M("clip-brown"), bevel=0.006)
    box("paper", (0, 0.02, -0.02), (0.26, 0.01, 0.33), M("paper"))
    box("clip", (0, 0.025, 0.18), (0.1, 0.02, 0.04), M("steel"), bevel=0.005)
    for i in range(6):
        box(f"line{i}", (0, 0.0255, -0.08 - i * 0.025), (0.21, 0.002, 0.004), M("line"))
    box("tick", (-0.09, 0.0256, -0.08), (0.015, 0.002, 0.015), M("led-green"))


# ======================================================================== the network in the world

def world_line_pole():
    cone("pole", (0, 0, 3.6), 0.13, 0.09, 7.2, M("pole-brown"), sides=8)
    cone("creosote", (0, 0, 0.3), 0.135, 0.13, 0.6, M("creosote"), sides=8)
    box("crossarm", (0, 0, 6.6), (0.12, 1.8, 0.12), M("wood-dark"))
    for sy in (-1, 1):
        beam(f"brace{sy}", (0, 0, 6.1), (0, sy * 0.5, 6.55), 0.03, M("galv"))
        for y in (0.7, 0.35):
            cylinder(f"pin{sy}{y}", (0, sy * y, 6.7), 0.012, 0.08, M("galv"), sides=6)
            cone(f"insulator{sy}{y}", (0, sy * y, 6.78), 0.045, 0.025, 0.1, M("insulator"), sides=8)
    for i, z in enumerate([2.4 + 0.4 * j for j in range(9)]):
        box(f"step{i}", ((1 if i % 2 else -1) * 0.16, 0, z), (0.14, 0.02, 0.02), M("galv"))
    box("number", (0, -0.12, 2.0), (0.08, 0.01, 0.12), M("label"))


# ======================================================================== project sites (ground-centre pivot, work side -Y)
#
# Geometry and gameplay as one system (EA5, docs/WORK_POINTS.md). Each site model is built in stages:
#   * anything made outside a `with stage(...)` block is the site as you find it (pegs, the old building, a crate);
#   * `with stage(n, "name"):` groups parts under an empty "stage-<n>-<name>": the game shows them once project
#     step n (0-based, the order in projects.json) is done, and as a ghost while that step is under way;
#   * `wp(n, i, motion, at)` puts an empty "wp-s<n>-<i>-<motion>": where a hand works during step n. Points sit on
#     the surface, on the -Y side (the side the player works from), 0.3-2.2 m up, or on the ground for dig/tamp.
# Two projects can share a model (exchange-hut, fibre-joint, cell-mast): stage n then means "step n" of either.

class stage:
    """Everything made inside `with stage(n, name):` appears in the game once project step n is done."""

    def __init__(self, n, name):
        self.n, self.name = n, name

    def __enter__(self):
        self.before = {o.name for o in bpy.context.scene.objects}
        return self

    def __exit__(self, *exc):
        root = empty(f"stage-{self.n}-{self.name}")
        for o in list(bpy.context.scene.objects):
            if o.name not in self.before and o is not root and o.parent is None:
                o.parent = root


def wp(step, i, motion, at):
    """A work point for project step `step`: the i-th place a hand works, doing `motion` (see WorkPoints.Motions)."""
    return empty(f"wp-s{step}-{i:02d}-{motion}", at)


def crate(name, at, rot=0.0, label="TELO PARTS"):
    x, y, z = at
    root = empty(name, (x, y, z))
    root.rotation_euler = (0, 0, rot)
    box(f"{name}-body", (0, 0, 0.3), (0.8, 0.6, 0.6), M("wood"), parent=root)
    for i, zz in enumerate((0.1, 0.3, 0.5)):
        box(f"{name}-slat{i}", (0, -0.305, zz), (0.82, 0.01, 0.06), M("wood-dark"), parent=root)
    for sx in (-1, 1):
        box(f"{name}-post{sx}", (sx * 0.39, -0.305, 0.3), (0.05, 0.02, 0.6), M("wood-dark"), parent=root)
    t = T(f"{name}-stencil", label, (0, -0.317, 0.38), 0.07, "ink")
    t.parent = root
    return root


def pegs(name, x0, y0, x1, y1, h=0.35):
    """Four marking-out pegs with orange string between them: where something is going to be built."""
    corners = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    for i, (x, y) in enumerate(corners):
        box(f"{name}-peg{i}", (x, y, h / 2), (0.04, 0.04, h), M("wood"))
        (ax, ay), (bx, by) = corners[i], corners[(i + 1) % 4]
        beam(f"{name}-string{i}", (ax, ay, h - 0.05), (bx, by, h - 0.05), 0.008, M("orange"))


def site_parts_crate():
    crate("crate", (0, 0, 0))


def site_pole_stack():
    """Church Lane: plant a pole (0), string the wire (1), fit the lane's terminal box (2)."""
    for i, (x, z) in enumerate([(-0.27, 0.13), (0, 0.13), (0.27, 0.13), (-0.135, 0.36), (0.135, 0.36), (0, 0.59)]):
        cone(f"pole{i}", (0.6, 0.6 + x, z + 0.12), 0.12, 0.1, 5.0, M("pole-brown"), sides=8, rot=(0, math.pi / 2, 0))
    for x in (-1.2, 2.4):
        box(f"chock{x}", (x, 0.6, 0.06), (0.15, 0.9, 0.12), M("wood-dark"))
    crate("crate", (2.6, -0.5, 0), 0.3, "INSULATORS")
    P = (-1.2, -0.3)  # where the pole goes in
    box("pole-mark", (P[0], P[1], 0.01), (0.5, 0.5, 0.02), M("earth"))
    with stage(0, "pole"):
        cone("standing-pole", (P[0], P[1], 3.0), 0.13, 0.1, 6.0, M("pole-brown"), sides=8)
        cylinder("tamped", (P[0], P[1], 0.03), 0.38, 0.06, M("earth"), sides=10)
        for i, z in enumerate((2.2, 2.6, 3.0, 3.4, 3.8, 4.2, 4.6)):
            box(f"step{i}", (P[0] + (0.1 if i % 2 else -0.1), P[1], z), (0.18, 0.03, 0.03), M("steel"))
    with stage(1, "wire"):
        box("crossarm", (P[0], P[1], 5.6), (1.0, 0.09, 0.09), M("wood-dark"))
        for sx in (-1, 1):
            cylinder(f"insulator{sx}", (P[0] + sx * 0.4, P[1], 5.7), 0.04, 0.1, M("insulator"), sides=8)
            beam(f"wire{sx}", (P[0] + sx * 0.4, P[1], 5.74), (P[0] + 4.5, P[1] + 2.5 + sx * 0.3, 5.3), 0.015, M("ink"))
    with stage(2, "terminal-box"):
        box("terminal", (P[0], P[1] - 0.16, 1.5), (0.26, 0.12, 0.32), M("cabinet-grey"), bevel=0.01)
        box("terminal-plate", (P[0], P[1] - 0.222, 1.58), (0.18, 0.004, 0.06), M("label"))
        T("terminal-text", "CHURCH LN", (P[0], P[1] - 0.225, 1.58), 0.03, "ink")
        beam("drop-up", (P[0], P[1] - 0.12, 1.66), (P[0], P[1] - 0.11, 5.55), 0.02, M("ink"))
    x, y = P
    wp(0, 1, "dig", (x + 0.32, y - 0.32, 0.08)); wp(0, 2, "dig", (x - 0.32, y - 0.32, 0.08))
    wp(0, 3, "lift", (x, y - 0.18, 1.3)); wp(0, 4, "tamp", (x, y - 0.42, 0.04))
    wp(1, 1, "clip", (x + 0.12, y - 0.16, 2.1)); wp(1, 2, "pull", (x + 0.6, y - 0.3, 1.4)); wp(1, 3, "tape", (x - 0.12, y - 0.16, 1.8))
    wp(2, 1, "place", (x, y - 0.25, 1.5)); wp(2, 2, "screw", (x - 0.08, y - 0.23, 1.62)); wp(2, 3, "screw", (x + 0.08, y - 0.23, 1.38))
    wp(2, 4, "check", (x + 0.2, y - 0.2, 1.2))


def cabinet(name, w, d, h, colour, plinth=0.15, with_plinth=True):
    if with_plinth:
        box(f"{name}-plinth", (0, 0, plinth / 2), (w + 0.1, d + 0.1, plinth), M("concrete"))
    box(f"{name}-body", (0, 0, plinth + h / 2), (w, d, h), M(colour), bevel=0.02)
    box(f"{name}-roof", (0, 0, plinth + h + 0.02), (w + 0.06, d + 0.06, 0.04), M(colour), bevel=0.01)
    box(f"{name}-seam", (0, -d / 2 - 0.002, plinth + h / 2), (0.01, 0.004, h - 0.1), M("ink"))
    for sx in (-1, 1):
        box(f"{name}-handle{sx}", (sx * 0.06, -d / 2 - 0.01, plinth + h * 0.55), (0.02, 0.02, 0.12), M("steel"))
    return plinth + h


def site_street_cabinet():
    """Eastgate: dig the base (0), stand the cabinet (1), run trunk pairs (2), wire it (3), test (4)."""
    pegs("mark", -0.6, -0.35, 0.6, 0.35)
    with stage(0, "base"):
        box("cab-plinth", (0, 0, 0.075), (1.1, 0.55, 0.15), M("concrete"))
        cone("spoil", (-1.0, 0.4, 0.15), 0.45, 0.15, 0.3, M("earth"), sides=7)
    with stage(1, "cabinet"):
        top = cabinet("cab", 1.0, 0.45, 1.25, "cabinet-green", with_plinth=False)
        for i in range(4):
            box(f"louvre{i}", (0, 0.227, 0.4 + i * 0.08), (0.6, 0.01, 0.03), M("ink"))
        box("sticker", (-0.3, -0.229, 0.6), (0.12, 0.004, 0.12), M("sticker"))  # DANGER, somewhat peeled
    with stage(2, "trunk"):
        box("trench", (1.5, 0.05, 0.012), (1.9, 0.32, 0.024), M("earth"))
        for i, dy in enumerate((-0.05, 0.05, 0.15)):
            beam(f"trunk{i}", (0.5, dy, 0.1), (2.4, dy, 0.03), 0.04, M(["rubber", "ink", "rubber"][i]))
    with stage(3, "wired"):
        box("plate", (0.3, -0.229, top - 0.2), (0.22, 0.004, 0.08), M("label"))
        T("plate-text", "TELO 01", (0.3, -0.232, top - 0.2), 0.045, "ink")
        box("gland", (0.52, -0.05, 0.3), (0.06, 0.2, 0.15), M("rubber"))
    with stage(4, "tested"):
        cylinder("lamp", (-0.3, -0.232, top - 0.15), 0.025, 0.012, M("led-green"), axis="Y", sides=8)
        box("tag", (0.0, -0.24, 0.85), (0.1, 0.004, 0.14), M("label"))
        T("tag-text", "OK", (0.0, -0.243, 0.85), 0.05, "led-green")
    wp(0, 1, "dig", (-0.3, -0.45, 0.08)); wp(0, 2, "dig", (0.3, -0.45, 0.08)); wp(0, 3, "tamp", (0.0, -0.35, 0.06))
    wp(1, 1, "lift", (-0.35, -0.3, 0.9)); wp(1, 2, "place", (0.35, -0.3, 0.9)); wp(1, 3, "bolt", (0.0, -0.3, 0.22))
    wp(2, 1, "pull", (0.8, -0.2, 0.12)); wp(2, 2, "clip", (1.4, -0.15, 0.08)); wp(2, 3, "clip", (0.56, -0.2, 0.3))
    wp(3, 1, "plug", (-0.2, -0.26, 1.0)); wp(3, 2, "screw", (0.2, -0.24, 1.0)); wp(3, 3, "tape", (0.0, -0.26, 0.7))
    wp(4, 1, "check", (0.0, -0.28, 1.1)); wp(4, 2, "check", (-0.3, -0.27, 1.25))


def site_modem_bank():
    """The depot: carry the rack in (0), bolt in the modems (1), cable it to the hunt group (2), watch the lamps (3)."""
    box("plinth", (0, 0, 0.05), (0.8, 0.8, 0.1), M("concrete"))
    crate("spare", (0.95, 0.2, 0), 0.2, "MODEMS")
    with stage(0, "rack"):
        for sx in (-1, 1):
            for sy in (-1, 1):
                box(f"upright{sx}{sy}", (sx * 0.32, sy * 0.32, 1.05), (0.04, 0.04, 1.9), M("ink"))
        box("top", (0, 0, 2.0), (0.7, 0.7, 0.04), M("ink"))
    with stage(1, "modems"):
        for i in range(14):
            z = 0.25 + i * 0.12
            box(f"modem{i}", (0, 0, z), (0.58, 0.55, 0.09), M("beige" if i % 5 else "beige-dark"), bevel=0.008)
    with stage(2, "leads"):
        for i in range(6):  # the spaghetti
            beam(f"lead{i}", (0.25, -0.28, 0.3 + i * 0.25), (0.35, -0.2, 0.15), 0.015, M(["ink", "nez-blue", "phone-red"][i % 3]), round_=True)
    with stage(3, "lamps"):
        for i in range(14):
            z = 0.25 + i * 0.12
            for j in range(3):
                lit = (i * 7 + j * 3) % 4 != 0
                cylinder(f"led{i}-{j}", (-0.2 + j * 0.03, -0.278, z), 0.006, 0.005, M("led-green" if lit else "led-red"), axis="Y", sides=6)
    wp(0, 1, "lift", (-0.32, -0.4, 1.0)); wp(0, 2, "lift", (0.32, -0.4, 1.0))
    wp(0, 3, "bolt", (-0.32, -0.36, 1.8)); wp(0, 4, "bolt", (0.32, -0.36, 0.4))
    wp(1, 1, "bolt", (-0.27, -0.31, 0.7)); wp(1, 2, "bolt", (0.27, -0.31, 0.7))
    wp(1, 3, "bolt", (-0.27, -0.31, 1.5)); wp(1, 4, "bolt", (0.27, -0.31, 1.5))
    wp(2, 1, "plug", (0.22, -0.31, 0.5)); wp(2, 2, "plug", (0.22, -0.31, 1.2)); wp(2, 3, "pull", (0.4, -0.3, 0.9))
    wp(3, 1, "check", (-0.17, -0.31, 1.0)); wp(3, 2, "check", (-0.17, -0.31, 1.6))


def site_leased_line():
    """NezTel's box: fill in the form (0), [ten days for their engineer], test the line at handover (1)."""
    box("pillar", (-0.4, 0, 0.55), (0.35, 0.3, 1.1), M("nez-blue"), bevel=0.02)
    cone("pillar-cap", (-0.4, 0, 1.15), 0.24, 0.05, 0.12, M("nez-blue"), sides=4, rot=(0, 0, math.pi / 4))
    T("pillar-text", "NEZTEL", (-0.4, -0.155, 0.85), 0.06, "label")
    cabinet("tel", 0.55, 0.35, 0.8, "cabinet-green")
    for o in [o for o in bpy.context.scene.objects if o.name.startswith("tel-")]:
        o.location.x += 0.45
    with stage(0, "form"):
        box("form", (-0.4, -0.153, 0.55), (0.2, 0.004, 0.27), M("paper"))
        for i in range(5):
            box(f"form-line{i}", (-0.42, -0.156, 0.64 - i * 0.045), (0.13, 0.002, 0.008), M("ink"))
        box("form-tape", (-0.4, -0.157, 0.69), (0.1, 0.003, 0.025), M("tape"))
    with stage(1, "handover"):
        beam("handover", (-0.25, -0.05, 0.25), (0.2, -0.05, 0.25), 0.04, M("orange"), round_=True)
        box("tag", (-0.03, -0.08, 0.27), (0.06, 0.01, 0.04), M("label"))  # "DO NOT UNPLUG. — N."
        cylinder("lamp", (0.6, -0.177, 0.85), 0.02, 0.01, M("led-green"), axis="Y", sides=8)
    wp(0, 1, "place", (-0.4, -0.2, 0.6)); wp(0, 2, "check", (-0.4, -0.2, 0.95))
    wp(1, 1, "plug", (-0.25, -0.2, 0.3)); wp(1, 2, "plug", (0.25, -0.22, 0.3)); wp(1, 3, "check", (0.6, -0.22, 0.85))


def site_microwave_tower():
    """Farm hill: carry the mast sections up (0), bolt on the dish (1), aim it (2), test the hop (3)."""
    H, base, top = 12.0, 1.1, 0.35
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"footing{sx}{sy}", (sx * base, sy * base, 0.1), (0.35, 0.35, 0.2), M("concrete"))
    box("hut", (1.9, -0.6, 0.9), (1.4, 1.2, 1.8), M("cabinet-grey"), bevel=0.02)
    box("hut-door", (1.9, -1.205, 0.8), (0.6, 0.01, 1.5), M("cabinet-green"))
    with stage(0, "mast"):
        for sx in (-1, 1):
            for sy in (-1, 1):
                beam(f"leg{sx}{sy}", (sx * base, sy * base, 0), (sx * top, sy * top, H), 0.09, M("galv"))
        levels = [0.0, 2.5, 5.0, 7.5, 10.0, 12.0]
        for i in range(len(levels) - 1):
            z0, z1 = levels[i], levels[i + 1]
            w0 = base + (top - base) * z0 / H
            w1 = base + (top - base) * z1 / H
            for side in range(4):
                a = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
                (ax, ay), (bx, by) = a[side], a[(side + 1) % 4]
                beam(f"x{i}{side}a", (ax * w0, ay * w0, z0), (bx * w1, by * w1, z1), 0.035, M("galv"))
                beam(f"x{i}{side}b", (bx * w0, by * w0, z0), (ax * w1, ay * w1, z1), 0.035, M("galv"))
                beam(f"r{i}{side}", (ax * w1, ay * w1, z1), (bx * w1, by * w1, z1), 0.04, M("galv"))
        box("platform", (0, 0, H), (1.1, 1.1, 0.05), M("steel"))
        for i, z in enumerate(range(1, 12)):
            box(f"ladder{i}", (-0.0, -base + (base - top) * z / H, z), (0.3, 0.02, 0.02), M("steel"))
    with stage(1, "dish"):
        # the dish, pointing over the ridge (+Y) with its feed horn
        dish = empty("dish", (0, 0.55, H - 0.6))
        dish.rotation_euler = (math.radians(-90), 0, 0)
        cone("dish-bowl", (0, 0, 0), 0.75, 0.3, 0.3, M("dish"), sides=16, parent=dish)
        cylinder("dish-rim", (0, 0, -0.15), 0.76, 0.03, M("steel"), sides=16, parent=dish)
        beam("feed", (0, 0.6, H - 0.6), (0, 1.2, H - 0.6), 0.04, M("steel"))
    with stage(2, "aimed"):
        beam("feeder", (1.6, -0.2, 1.7), (0.3, 0.3, H - 0.6), 0.05, M("rubber"), round_=True)
        box("meter", (1.45, -1.215, 1.3), (0.24, 0.03, 0.18), M("cream"))
        box("meter-face", (1.45, -1.232, 1.32), (0.18, 0.004, 0.1), M("label"))
        box("needle", (1.47, -1.236, 1.33), (0.006, 0.004, 0.08), M("led-red"), rot=(0, math.radians(-30), 0))
    with stage(3, "tested"):
        cylinder("lamp", (2.3, -1.215, 1.55), 0.03, 0.015, M("led-green"), axis="Y", sides=8)
        box("plate", (2.3, -1.212, 1.35), (0.26, 0.006, 0.1), M("label"))
        T("plate-text", "HOP 1", (2.3, -1.217, 1.35), 0.05, "ink")
    wp(0, 1, "lift", (-0.55, -1.3, 0.9)); wp(0, 2, "bolt", (-1.0, -1.12, 1.4)); wp(0, 3, "bolt", (1.0, -1.12, 1.4))
    wp(1, 1, "lift", (0.0, -1.12, 1.2)); wp(1, 2, "bolt", (0.12, -1.07, 1.9)); wp(1, 3, "bolt", (-0.12, -1.07, 1.9))
    wp(2, 1, "check", (1.45, -1.27, 1.3)); wp(2, 2, "bolt", (0.25, -1.06, 1.6)); wp(2, 3, "check", (1.45, -1.27, 1.12))
    wp(3, 1, "check", (2.3, -1.26, 1.55)); wp(3, 2, "check", (2.0, -1.26, 1.0))


def site_exchange_hut():
    """Millbrook / Farland: the delivery and the route (0), the mast (1), the racks fitted (2), the lines cut over (3)."""
    box("walls", (0, 0, 1.3), (3.0, 2.4, 2.6), M("brick"))
    box("roof", (0, 0, 2.65), (3.2, 2.6, 0.12), M("roof-felt"))
    box("door", (-0.6, -1.205, 1.05), (0.9, 0.02, 2.1), M("cabinet-green"))
    cylinder("knob", (-0.25, -1.22, 1.0), 0.03, 0.03, M("brass"), axis="Y", sides=8)
    for i in range(3):
        box(f"vent{i}", (0.65, -1.205, 0.6 + i * 0.12), (0.8, 0.012, 0.05), M("ink"))
    box("step", (-0.6, -1.35, 0.05), (1.1, 0.3, 0.1), M("concrete"))
    with stage(0, "delivery"):
        box("pallet", (0.9, -1.85, 0.06), (1.0, 0.8, 0.12), M("wood-dark"))
        box("rack-wrapped", (0.9, -1.85, 0.75), (0.6, 0.6, 1.25), M("white-paint"), bevel=0.02)
        T("rack-label", "TELO RACK", (0.9, -2.153, 1.0), 0.06, "ink")
        cylinder("survey-pole", (-1.9, -1.5, 0.75), 0.025, 1.5, M("white-paint"), sides=6)
        for i in range(3):
            cylinder(f"survey-band{i}", (-1.9, -1.5, 0.3 + i * 0.5), 0.028, 0.2, M("led-red"), sides=6)
        box("survey-flag", (-1.8, -1.5, 1.4), (0.18, 0.005, 0.12), M("orange"))
    with stage(1, "mast"):
        cylinder("mast", (1.75, -0.7, 3.2), 0.07, 6.4, M("galv"), sides=8)
        for z in (1.0, 2.4):
            box(f"mast-bracket{z}", (1.62, -0.7, z), (0.26, 0.05, 0.05), M("steel"))
        box("antenna", (1.75, -0.7, 6.1), (0.08, 0.08, 0.8), M("white-paint"))
        cone("mast-dish", (1.75, -0.95, 5.3), 0.28, 0.1, 0.12, M("dish"), sides=12, rot=(math.pi / 2, 0, 0))
    with stage(2, "fitted"):
        box("sign", (0.65, -1.21, 2.1), (1.3, 0.02, 0.35), M("label"))
        T("sign-text", "TELO EXCHANGE", (0.65, -1.225, 2.1), 0.13, "ink")
        box("ac", (1.2, 1.35, 1.6), (0.7, 0.3, 0.5), M("white-paint"), bevel=0.02)
        cylinder("ac-fan", (1.2, 1.51, 1.6), 0.18, 0.02, M("ink"), axis="Y", sides=12)
        cylinder("lamp", (-0.6, -1.25, 2.3), 0.07, 0.1, M("van-lamp"), axis="Y", sides=8)
    with stage(3, "cutover"):
        cone("cut-pole", (-2.4, 0.2, 2.2), 0.12, 0.09, 4.4, M("pole-brown"), sides=8)
        beam("cut-cable", (-2.4, 0.2, 4.1), (-1.5, -0.6, 2.2), 0.05, M("rubber"), round_=True)
        box("cut-gland", (-1.4, -1.22, 1.5), (0.18, 0.04, 0.24), M("cabinet-grey"))
        beam("cut-tail", (-1.5, -0.6, 2.2), (-1.4, -1.22, 1.62), 0.04, M("rubber"), round_=True)
    wp(0, 1, "place", (0.9, -2.2, 0.6)); wp(0, 2, "check", (-1.9, -1.56, 1.2))
    wp(1, 1, "lift", (1.75, -0.8, 1.0)); wp(1, 2, "bolt", (1.68, -0.78, 1.8)); wp(1, 3, "bolt", (1.68, -0.78, 1.0 + 0.25))
    wp(2, 1, "place", (-0.6, -1.32, 1.1)); wp(2, 2, "plug", (0.2, -1.24, 1.4)); wp(2, 3, "screw", (0.9, -1.24, 1.6))
    wp(3, 1, "cut", (-1.38, -1.27, 1.75)); wp(3, 2, "solder", (-1.4, -1.27, 1.5)); wp(3, 3, "tape", (-1.38, -1.27, 1.25))


def site_cold_store():
    """Eastgate ice works: clear the cold room (0), rack the servers (1), power and cooling (2), test the generator (3)."""
    W, D, Hh = 3.6, 3.0, 3.0
    # the shell, with its front doorway open (x 0..1.2)
    box("back", (0, D / 2 - 0.05, Hh / 2), (W, 0.1, Hh), M("corrugated"))
    for sx in (-1, 1):
        box(f"side{sx}", (sx * (W / 2 - 0.05), 0, Hh / 2), (0.1, D, Hh), M("corrugated"))
    box("front-left", (-0.9, -D / 2 + 0.05, Hh / 2), (1.8, 0.1, Hh), M("corrugated"))
    box("front-right", (1.5, -D / 2 + 0.05, Hh / 2), (0.6, 0.1, Hh), M("corrugated"))
    box("lintel", (0.6, -D / 2 + 0.05, 2.65), (1.2, 0.1, 0.7), M("corrugated"))
    box("floor", (0, 0, 0.02), (W - 0.2, D - 0.2, 0.04), M("concrete"))
    for i in range(18):
        x = -1.7 + i * 0.2
        if -0.05 < x < 1.25:
            box(f"rib{i}", (x, -1.505, 2.65), (0.03, 0.02, 0.7), M("galv"))
            continue
        box(f"rib{i}", (x, -1.505, 1.5), (0.03, 0.02, 3.0), M("galv"))
    box("roof", (0, 0, 3.05), (3.8, 3.2, 0.1), M("galv"))
    box("door", (1.8, -1.59, 1.15), (1.2, 0.06, 2.3), M("white-paint"), bevel=0.03)  # swung open against the wall
    box("handle", (2.3, -1.63, 1.15), (0.06, 0.06, 0.5), M("steel"))
    T("ghost", "KESTREL ICE CO.", (-0.9, -1.52, 2.55), 0.16, "galv", extrude=0.001)
    box("ice-crate", (0.6, 0.85, 0.35), (0.8, 0.6, 0.7), M("wood"))
    with stage(0, "cleared"):
        box("skip", (-2.55, 0.5, 0.45), (0.9, 1.8, 0.9), M("yellow"))
        for i, (dx, dy, dz) in enumerate([(-0.15, -0.4, 0.95), (0.1, 0.2, 1.0), (-0.1, 0.55, 0.92)]):
            box(f"junk{i}", (-2.55 + dx, 0.5 + dy, dz), (0.45, 0.4, 0.3), M(["wood", "galv", "wood-dark"][i]), rot=(0, 0, 0.3 * i))
    with stage(1, "racks"):
        for i, x in enumerate((0.3, 0.9)):
            box(f"rack{i}", (x, -0.3, 1.0), (0.5, 0.6, 2.0), M("ink"))
            for j in range(8):
                cylinder(f"rack{i}-led{j}", (x - 0.15 + (j % 2) * 0.05, -0.605, 0.4 + j * 0.18), 0.008, 0.006, M("led-green"), axis="Y", sides=6)
        box("new-sign", (-0.9, -1.525, 2.05), (1.2, 0.02, 0.36), M("ink"))
        T("new-sign-text", "TELO DATA", (-0.9, -1.54, 2.05), 0.16, "led-green")
    with stage(2, "power"):
        for x in (-1.0, 0.2, 1.2):
            box(f"ac{x}", (x, 0.6, 3.3), (0.8, 0.8, 0.4), M("white-paint"), bevel=0.02)
            cylinder(f"fan{x}", (x, 0.6, 3.51), 0.3, 0.02, M("ink"), sides=12)
        beam("cable-tray", (-1.7, -1.56, 1.65), (-0.05, -1.56, 1.65), 0.1, M("galv"))
        beam("power-drop", (-1.6, -1.56, 1.65), (-1.6, -1.56, 0.05), 0.04, M("rubber"))
        box("isolator", (-1.3, -1.57, 1.15), (0.22, 0.06, 0.3), M("cabinet-grey"))
    with stage(3, "generator"):
        box("generator", (2.2, 0.75, 0.55), (0.7, 1.2, 1.1), M("cabinet-green"), bevel=0.03)
        cylinder("exhaust", (2.2, 1.1, 1.4), 0.06, 0.6, M("steel"), sides=8)
        cylinder("gen-lamp", (2.2, 0.142, 0.9), 0.025, 0.012, M("led-green"), axis="Y", sides=8)
    wp(0, 1, "lift", (0.6, 0.5, 0.75)); wp(0, 2, "lift", (0.6, -1.65, 0.9)); wp(0, 3, "place", (-2.55, -0.45, 0.95))
    wp(1, 1, "lift", (0.3, -0.65, 1.0)); wp(1, 2, "bolt", (0.9, -0.65, 1.5)); wp(1, 3, "bolt", (0.9, -0.65, 0.6))
    wp(2, 1, "pull", (-1.2, -1.66, 1.65)); wp(2, 2, "clip", (-0.4, -1.66, 1.65)); wp(2, 3, "screw", (-1.3, -1.63, 1.15))
    wp(3, 1, "pull", (-1.3, -1.64, 1.22)); wp(3, 2, "check", (2.2, 0.1, 0.9))


def site_cable_landing():
    """Eastgate beach: pour the floor (0), [the concrete sets], it passes (1), fit out the hut (2)."""
    for i, (c, s) in enumerate([((0, -1.25, 0.1), (3.1, 0.04, 0.2)), ((0, 1.25, 0.1), (3.1, 0.04, 0.2)),
                                ((-1.55, 0, 0.1), (0.04, 2.5, 0.2)), ((1.55, 0, 0.1), (0.04, 2.5, 0.2))]):
        box(f"form{i}", c, s, M("wood"))
    cylinder("marker", (1.2, 0.8, 0.8), 0.05, 1.3, M("orange"), sides=8)
    box("marker-sign", (1.2, 0.74, 1.35), (0.3, 0.02, 0.2), M("orange"))
    T("marker-text", "!", (1.2, 0.725, 1.35), 0.15, "ink")
    with stage(0, "floor"):
        box("pad", (0, 0, 0.08), (3.0, 2.4, 0.16), M("concrete"))
        cylinder("manhole", (0.5, -0.3, 0.165), 0.45, 0.02, M("steel"), sides=16)
    with stage(1, "set"):
        T("manhole-text", "CABLE", (0.5, -0.3, 0.18), 0.12, "ink", face="+Z")
        for x in (-1.3, 1.3):
            cylinder(f"bollard{x}", (x, -1.1, 0.5), 0.08, 0.8, M("yellow"), sides=8)
    with stage(2, "fitted"):
        box("hut", (-0.8, 0.5, 1.2), (1.2, 1.0, 2.1), M("white-paint"), bevel=0.03)
        box("hut-door", (-0.8, -0.005, 1.0), (0.6, 0.02, 1.6), M("cabinet-green"))
        beam("sea-cable", (0.5, -0.3, 0.1), (0.9, 3.0, -0.6), 0.12, M("rubber"), round_=True)
        box("power", (-0.15, 0.1, 0.55), (0.3, 0.2, 0.8), M("cabinet-grey"))
    wp(0, 1, "place", (-0.6, -0.8, 0.2)); wp(0, 2, "tamp", (0.6, -0.85, 0.18)); wp(0, 3, "tamp", (-0.1, -0.45, 0.18))
    wp(1, 1, "check", (0.5, -0.65, 0.22)); wp(1, 2, "check", (-1.3, -1.2, 0.8))
    wp(2, 1, "lift", (-0.8, -0.1, 1.2)); wp(2, 2, "bolt", (-1.25, -0.06, 1.6)); wp(2, 3, "plug", (-0.15, -0.05, 0.75))


def site_cable_reel():
    """The ocean cable: load the drums (0), [the ship lays it] (1), splice on the beach (2), light it (3)."""
    r, w = 1.1, 0.9
    for sx in (-1, 1):
        cylinder(f"flange{sx}", (sx * w / 2, 0, r + 0.1), r, 0.06, M("yellow"), axis="X", sides=16)
        beam(f"stand{sx}a", (sx * (w / 2 + 0.1), -0.7, 0), (sx * (w / 2 + 0.1), 0, r + 0.1), 0.1, M("steel"))
        beam(f"stand{sx}b", (sx * (w / 2 + 0.1), 0.7, 0), (sx * (w / 2 + 0.1), 0, r + 0.1), 0.1, M("steel"))
    cylinder("cable", (0, 0, r + 0.1), r - 0.15, w - 0.05, M("rubber"), axis="X", sides=16)
    cylinder("axle", (0, 0, r + 0.1), 0.06, w + 0.4, M("steel"), axis="X", sides=8)
    T("stencil", "FARLAND 1", (w / 2 + 0.035, 0, r + 0.5), 0.14, "ink", face="+X")
    cone("buoy", (-1.5, -0.3, 0.35), 0.3, 0.1, 0.7, M("led-red"), sides=10)
    cylinder("buoy-band", (-1.5, -0.3, 0.35), 0.24, 0.12, M("white-paint"), sides=10)
    with stage(0, "loaded"):
        for i, x in enumerate((-0.2, 0.2)):
            cylinder(f"strap{i}", (x, 0, r + 0.1), r - 0.1, 0.05, M("orange"), axis="X", sides=16)
        box("ship-tag", (0.0, -0.98, 1.0), (0.2, 0.01, 0.14), M("label"))
        T("ship-tag-text", "SHIP", (0.0, -0.99, 1.0), 0.05, "ink")
    with stage(1, "laid"):
        beam("pay-out", (0, r - 0.2, r - 0.4), (0.3, 3.2, 0.05), 0.1, M("rubber"), round_=True)
    with stage(2, "spliced"):
        for i, (x, y) in enumerate([(0.75, -0.9), (1.05, -0.9), (0.9, -0.65)]):
            beam(f"tripod{i}", (x, y, 0), (0.9, -0.8, 1.0), 0.03, M("steel"))
        cylinder("closure", (0.9, -0.95, 0.78), 0.1, 0.4, M("ink"), sides=10)
        beam("fibre", (0.42, -0.4, 0.5), (0.9, -0.95, 0.58), 0.02, M("yellow"), round_=True)
    with stage(3, "lit"):
        box("test-set", (1.45, -1.05, 0.15), (0.32, 0.22, 0.3), M("cream"), bevel=0.01)
        cylinder("test-lamp", (1.45, -1.165, 0.22), 0.025, 0.012, M("led-green"), axis="Y", sides=8)
    wp(0, 1, "pull", (0.2, -1.12, 1.2)); wp(0, 2, "clip", (-0.2, -1.1, 0.75))
    wp(1, 1, "check", (0.0, -1.08, 1.0))
    wp(2, 1, "cut", (0.9, -1.07, 0.92)); wp(2, 2, "solder", (0.9, -1.07, 0.78)); wp(2, 3, "tape", (0.9, -1.07, 0.64))
    wp(3, 1, "check", (1.45, -1.2, 0.32))


def site_dslam_cabinet():
    """Broadband: install the racks (0), re-jumper the lines through them (1), test at every distance (2)."""
    box("cab-plinth", (0, 0, 0.075), (1.3, 0.7, 0.15), M("concrete"))
    # the old copper cabinet the lines come from
    box("old-plinth", (-1.3, 0.1, 0.06), (0.8, 0.5, 0.12), M("concrete"))
    box("old-cab", (-1.3, 0.1, 0.67), (0.7, 0.4, 1.1), M("cabinet-green"), bevel=0.02)
    with stage(0, "racks"):
        top = cabinet("cab", 1.2, 0.6, 1.6, "cabinet-grey", with_plinth=False)
        for i in range(6):
            box(f"grille{i}", (0.0, 0.302, 0.5 + i * 0.12), (0.7, 0.01, 0.05), M("ink"))
        for sx in (-1, 1):
            cylinder(f"fan{sx}", (sx * 0.62, 0.0, 1.2), 0.15, 0.02, M("ink"), axis="X", sides=12)
    with stage(1, "jumpers"):
        for i in range(4):
            beam(f"jumper{i}", (-0.95, -0.02 + i * 0.03, 0.35 + i * 0.05), (-0.6, -0.05 + i * 0.03, 0.3 + i * 0.06), 0.025,
                 M(["ink", "rubber", "nez-blue", "ink"][i]), round_=True)
    with stage(2, "tested"):
        box("sticker", (-0.35, -0.302, 1.2), (0.3, 0.004, 0.12), M("duct"))
        T("sticker-text", "BROADBAND", (-0.35, -0.305, 1.2), 0.05, "label")
        cylinder("lamp", (0.4, -0.305, top - 0.15), 0.02, 0.01, M("led-green"), axis="Y", sides=8)
    wp(0, 1, "lift", (-0.3, -0.4, 0.9)); wp(0, 2, "bolt", (0.3, -0.35, 1.4)); wp(0, 3, "bolt", (0.3, -0.35, 0.5))
    wp(1, 1, "plug", (-1.3, -0.16, 0.85)); wp(1, 2, "pull", (-0.78, -0.2, 0.4)); wp(1, 3, "plug", (-0.45, -0.35, 0.6))
    wp(2, 1, "check", (0.4, -0.36, 1.6)); wp(2, 2, "check", (-0.35, -0.36, 1.05))


def site_fibre_joint():
    """Ring / backhaul: the chamber and ducts (0), fibre blown through (1), [the crews] the tripod (2), spliced (3)."""
    for i, x in enumerate((-1.6, 1.8)):
        cone(f"cone{i}", (x, -0.9, 0.25), 0.15, 0.03, 0.5, M("orange"), sides=8)
        cylinder(f"cone-band{i}", (x, -0.9, 0.3), 0.1, 0.07, M("white-paint"), sides=8)
    cylinder("route-post", (-2.0, 0.6, 0.45), 0.03, 0.9, M("duct"), sides=6)
    box("route-plate", (-2.0, 0.56, 0.8), (0.14, 0.01, 0.1), M("label"))
    with stage(0, "chamber"):
        box("chamber", (0, 0, 0.06), (1.4, 1.0, 0.12), M("concrete"))
        box("hole", (0, 0, 0.121), (1.0, 0.65, 0.004), M("ink"))
        box("lid", (0.0, 0.62, 0.3), (0.7, 0.05, 0.5), M("steel"), rot=(math.radians(-15), 0, 0))
        box("trench", (-1.3, 0, 0.012), (1.2, 0.7, 0.024), M("earth"))
        for i, y in enumerate((-0.2, 0.0, 0.2)):
            beam(f"duct{i}", (-0.4, y, 0.12), (-1.6, y * 1.5, 0.05), 0.07, M("duct"), round_=True)
    with stage(1, "fibre"):
        box("blower", (-1.0, -0.65, 0.25), (0.5, 0.35, 0.5), M("yellow"), bevel=0.02)
        cylinder("blower-reel", (-1.0, -0.65, 0.62), 0.18, 0.12, M("ink"), axis="Y", sides=10)
        for i, y in enumerate((-0.2, 0.0, 0.2)):
            beam(f"fibre-in{i}", (-0.4, y, 0.13), (0.3, y * 0.5, 0.08), 0.02, M("yellow"), round_=True)
    with stage(2, "tripod"):
        for i, (x, y) in enumerate([(0.95, -0.45), (1.45, -0.45), (1.2, 0.0)]):
            beam(f"tripod{i}", (x, y, 0), (1.2, -0.3, 1.0), 0.03, M("steel"))
    with stage(3, "spliced"):
        cylinder("closure", (1.2, -0.3, 0.78), 0.11, 0.45, M("ink"), sides=10)
        beam("fibre", (1.2, -0.3, 0.55), (0.3, 0.0, 0.1), 0.02, M("yellow"), round_=True)
    wp(0, 1, "dig", (-0.3, -0.62, 0.08)); wp(0, 2, "dig", (0.3, -0.62, 0.08)); wp(0, 3, "place", (-0.5, -0.4, 0.14))
    wp(1, 1, "pull", (-0.45, -0.2, 0.2)); wp(1, 2, "clip", (-0.85, -0.38, 0.12)); wp(1, 3, "check", (-1.0, -0.86, 0.45))
    wp(2, 1, "lift", (1.2, -0.48, 0.85)); wp(2, 2, "check", (1.0, -0.5, 0.4))
    wp(3, 1, "cut", (1.2, -0.43, 0.92)); wp(3, 2, "solder", (1.2, -0.43, 0.78)); wp(3, 3, "tape", (1.2, -0.43, 0.64))


def site_cell_mast():
    """First mast / coast masts: the footing (0), mast and antennas (1), the base station (2), on air (3)."""
    H = 15.0
    box("yard", (0, 0.4, 0.02), (3.4, 3.4, 0.04), M("concrete"))
    corners = [(-1.7, -1.3), (1.7, -1.3), (1.7, 2.1), (-1.7, 2.1)]
    for i in range(12):
        t = i / 12 * 4
        side, f = int(t), t - int(t)
        (ax, ay), (bx, by) = corners[side], corners[(side + 1) % 4]
        x, y = ax + (bx - ax) * f, ay + (by - ay) * f
        if side == 0 and -0.5 < x < 0.5:
            continue  # the gate
        cylinder(f"post{i}", (x, y, 0.9), 0.03, 1.8, M("fence"), sides=6)
    for z in (0.5, 1.7):
        for s in range(4):
            if s == 0:  # the gate side (front) left open in the middle
                beam(f"rail{z}-{s}a", (-1.7, -1.3, z), (-0.5, -1.3, z), 0.03, M("fence"))
                beam(f"rail{z}-{s}b", (0.5, -1.3, z), (1.7, -1.3, z), 0.03, M("fence"))
                continue
            beam(f"rail{z}-{s}", (*corners[s], z), (*corners[(s + 1) % 4], z), 0.03, M("fence"))
    box("sign", (-1.0, -1.32, 1.1), (0.5, 0.02, 0.35), M("label"))
    T("sign-text", "TELO SITE 1", (-1.0, -1.335, 1.1), 0.07, "ink")
    with stage(0, "footing"):
        cylinder("base", (0, 0.4, 0.15), 0.6, 0.3, M("concrete"), sides=10)
    with stage(1, "mast"):
        cone("mast", (0, 0.4, H / 2), 0.28, 0.14, H, M("galv"), sides=10)
        for i in range(3):  # three sectors
            a = math.radians(i * 120 - 90)
            cx, cy = math.cos(a) * 0.5, 0.4 + math.sin(a) * 0.5
            beam(f"arm{i}", (0, 0.4, H - 0.6), (cx, cy, H - 0.6), 0.06, M("galv"))
            p = box(f"panel{i}", (cx, cy, H - 0.6), (0.3, 0.1, 1.2), M("white-paint"), bevel=0.02)
            p.rotation_euler = (0, 0, a + math.pi / 2)
        dish = empty("link", (0, 0.1, H - 2.2))
        dish.rotation_euler = (math.radians(90), 0, 0)
        cone("link-dish", (0, 0, 0), 0.3, 0.12, 0.12, M("dish"), sides=12, parent=dish)
        cone("light", (0, 0.4, H + 0.1), 0.06, 0.03, 0.12, M("led-red"), sides=8)
        for i in range(10):
            box(f"peg{i}", ((0.12 if i % 2 else -0.12), 0.4 - 0.27, 2.5 + i * 0.35), (0.14, 0.03, 0.03), M("steel"))
    with stage(2, "base-station"):
        box("cab", (1.0, -0.45, 0.8), (0.8, 0.6, 1.5), M("cabinet-grey"), bevel=0.02)
        beam("feeder", (0.8, -0.3, 1.4), (0.1, 0.35, H - 0.8), 0.05, M("rubber"), round_=True)
    with stage(3, "on-air"):
        cylinder("lamp", (1.25, -0.755, 1.35), 0.025, 0.012, M("led-green"), axis="Y", sides=8)
        box("air-plate", (0.9, -0.752, 1.35), (0.3, 0.004, 0.1), M("label"))
        T("air-text", "ON AIR", (0.9, -0.755, 1.35), 0.05, "led-red")
    wp(0, 1, "place", (-0.3, -0.12, 0.33)); wp(0, 2, "tamp", (0.3, -0.12, 0.33))
    wp(1, 1, "lift", (-0.2, 0.08, 1.2)); wp(1, 2, "bolt", (0.0, 0.1, 1.7)); wp(1, 3, "bolt", (0.0, 0.11, 2.1))
    wp(2, 1, "plug", (0.85, -0.79, 1.0)); wp(2, 2, "screw", (1.15, -0.79, 1.25)); wp(2, 3, "pull", (0.55, -0.3, 1.5))
    wp(3, 1, "check", (1.25, -0.8, 1.35))


# ======================================================================== registry: id -> (builder, name, what, pivot, single mesh?)
PROPS = {
    "kit.ladder": (kit_ladder, "Ladder", "Wooden two-section ladder, rubber feet. Rides on the van's roof rack.", "foot, centre", False),
    "kit.pole": (kit_pole, "Line pole", "A 6 m creosoted pole with climbing steps, carried on the rack and planted along a road.", "base, centre", False),
    "kit.cable-drum": (kit_cable_drum, "Cable drum", "Wooden spool of black drop wire, standing on its flange.", "bottom, centre", False),
    "kit.toolbox": (kit_toolbox, "Toolbox", "Red metal toolbox with a cantilever lid.", "bottom, centre", False),
    "kit.modem-box": (kit_modem_box, "Boxed modem", "A carton with a printed label and a picture of the modem inside.", "bottom, centre", False),
    "kit.line-card": (kit_line_card, "Line card", "A circuit board that slots into the line rack: one more line into the Board.", "bottom, centre", False),
    "install.wall-clip": (install_wall_clip, "Wall clip", "Drop-wire fixing screwed to the outside wall.", "centre", False),
    "install.junction-box": (install_junction_box, "Junction box", "Where the drop wire meets the house wiring, by the front door.", "centre", False),
    "install.junction-cover": (install_junction_cover, "Junction box cover", "The cream lid that goes on last.", "centre", False),
    "install.socket-back": (install_socket_back, "Socket backplate", "Screwed to the skirting-height wall indoors; faces +X.", "centre", False),
    "install.socket-face": (install_socket_face, "Socket faceplate", "The cream faceplate with the plug slot; faces +X.", "centre", False),
    "install.modem": (install_modem, "Modem", "Beige 300-baud modem with acoustic coupler cups and four lights.", "centre", False),
    "install.telephone": (install_telephone, "Telephone", "The customer's red rotary phone; lift the handset to test the line.", "centre of the base", False),
    "install.job-sheet": (install_job_sheet, "Job sheet", "Clipboard by the door with the job's ticked steps.", "centre", False),
    "world.line-pole": (world_line_pole, "Telephone pole", "7.2 m pole with crossarm (along Godot Z), insulators and steps. One mesh, shared by every pole.", "base, centre", True),
    "site.parts-crate": (site_parts_crate, "Parts crate", "A slatted crate of project parts; a pile of them marks a site under way.", "ground, centre", False),
    "site.pole-stack": (site_pole_stack, "Stack of poles", "Poles on chocks, ready for Church Lane.", "ground, centre", False),
    "site.street-cabinet": (site_street_cabinet, "Street cabinet", "Green roadside cabinet on a plinth.", "ground, centre", False),
    "site.modem-bank": (site_modem_bank, "Modem bank", "An open rack of beige modems with blinking lights and too many leads.", "ground, centre", False),
    "site.leased-line": (site_leased_line, "Leased line handover", "NezTel's blue pillar, your cabinet, and an orange cord between them.", "ground, centre", False),
    "site.microwave-tower": (site_microwave_tower, "Microwave tower", "12 m lattice tower with a dish pointing over the ridge, hut at its foot.", "ground, centre", False),
    "site.exchange-hut": (site_exchange_hut, "Exchange hut", "A small brick exchange with a green door and a sign.", "ground, centre", False),
    "site.cold-store": (site_cold_store, "Cold store data room", "A corrugated cold store, the ice company's name still showing under TELO DATA.", "ground, centre", False),
    "site.cable-landing": (site_cable_landing, "Cable landing", "Concrete pad, a manhole marked CABLE, a hut, the cable heading into the sea.", "ground, centre", False),
    "site.cable-reel": (site_cable_reel, "Ocean cable reel", "A huge yellow reel of sea cable on a stand, and a marker buoy.", "ground, centre", False),
    "site.dslam-cabinet": (site_dslam_cabinet, "Broadband cabinet", "A tall grey cabinet with fans and a BROADBAND sticker.", "ground, centre", False),
    "site.fibre-joint": (site_fibre_joint, "Fibre joint", "An open chamber, orange ducts, a splice closure on a tripod, cones.", "ground, centre", False),
    "site.cell-mast": (site_cell_mast, "Cell mast", "15 m monopole with three panel antennas, a link dish and a fenced compound.", "ground, centre", False),
}


def bounds():
    from mathutils import Vector
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    dg = bpy.context.evaluated_depsgraph_get()
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        for v in o.evaluated_get(dg).bound_box:
            w = o.matrix_world @ Vector(v)
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi


def build(pid):
    fn, name, what, pivot, single = PROPS[pid]
    k.reset()
    fn()
    bpy.context.view_layer.update()
    if single:
        k.join_all(pid.replace(".", "-"))
    lo, hi = bounds()
    size = hi - lo
    centre = (lo + hi) / 2
    span = max(size.x, size.y, size.z)
    k.preview(pid, distance=span * 1.9 + 0.25, height=centre.z + span * 0.55, target=tuple(centre), views=((35, "front"),))
    k.export(pid)
    os.replace(os.path.join(k.PREVIEWS, pid + "-front.png"), os.path.join(k.PREVIEWS, pid + ".png"))
    # Godot size: x, height, depth
    return {"id": pid, "name": name, "what": what, "size": [round(size.x, 3), round(size.z, 3), round(size.y, 3)],
            "pivot": pivot, "tris": k.triangle_count(), "status": "modelled"}


def update_briefs(results):
    path = os.path.join(k.ROOT, "game", "content", "props.json")
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    by_id = {p["id"]: p for p in data["props"]}
    for r in results:
        if r["id"] in by_id:
            by_id[r["id"]].update({"size": r["size"], "tris": r["tris"], "status": "modelled"})
        else:
            data["props"].append(r)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1, ensure_ascii=False)
        f.write("\n")


want = k.args() or ["all"]
ids = list(PROPS) if want == ["all"] else want
results = []
for pid in ids:
    results.append(build(pid))
    print(f"BUILT {pid}: {results[-1]['tris']} tris, size {results[-1]['size']}")
update_briefs(results)
print(f"BATCH OK {len(results)} props")
