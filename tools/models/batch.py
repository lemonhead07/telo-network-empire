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


def site_parts_crate():
    crate("crate", (0, 0, 0))


def site_pole_stack():
    for i, (x, z) in enumerate([(-0.27, 0.13), (0, 0.13), (0.27, 0.13), (-0.135, 0.36), (0.135, 0.36), (0, 0.59)]):
        cone(f"pole{i}", (x, 0, z + 0.12), 0.12, 0.1, 6.0, M("pole-brown"), sides=8, rot=(math.pi / 2, 0, 0))
    for y in (-2.2, 2.2):
        box(f"chock{y}", (0, y, 0.06), (0.9, 0.15, 0.12), M("wood-dark"))
    crate("crate", (1.2, -1.2, 0), 0.3, "INSULATORS")


def cabinet(name, w, d, h, colour, plinth=0.15):
    box(f"{name}-plinth", (0, 0, plinth / 2), (w + 0.1, d + 0.1, plinth), M("concrete"))
    box(f"{name}-body", (0, 0, plinth + h / 2), (w, d, h), M(colour), bevel=0.02)
    box(f"{name}-roof", (0, 0, plinth + h + 0.02), (w + 0.06, d + 0.06, 0.04), M(colour), bevel=0.01)
    box(f"{name}-seam", (0, -d / 2 - 0.002, plinth + h / 2), (0.01, 0.004, h - 0.1), M("ink"))
    for sx in (-1, 1):
        box(f"{name}-handle{sx}", (sx * 0.06, -d / 2 - 0.01, plinth + h * 0.55), (0.02, 0.02, 0.12), M("steel"))
    return plinth + h


def site_street_cabinet():
    top = cabinet("cab", 1.0, 0.45, 1.25, "cabinet-green")
    for i in range(4):
        box(f"louvre{i}", (0, 0.227, 0.4 + i * 0.08), (0.6, 0.01, 0.03), M("ink"))
    box("plate", (0.3, -0.229, top - 0.2), (0.22, 0.004, 0.08), M("label"))
    T("plate-text", "TELO 01", (0.3, -0.232, top - 0.2), 0.045, "ink")
    box("sticker", (-0.3, -0.229, 0.6), (0.12, 0.004, 0.12), M("sticker"))  # DANGER, somewhat peeled
    beam("cable-in", (0.2, 0.0, 0.0), (0.2, 0.0, -0.4), 0.06, M("rubber"))


def site_modem_bank():
    box("plinth", (0, 0, 0.05), (0.8, 0.8, 0.1), M("concrete"))
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"upright{sx}{sy}", (sx * 0.32, sy * 0.32, 1.05), (0.04, 0.04, 1.9), M("ink"))
    box("top", (0, 0, 2.0), (0.7, 0.7, 0.04), M("ink"))
    for i in range(14):
        z = 0.25 + i * 0.12
        box(f"modem{i}", (0, 0, z), (0.58, 0.55, 0.09), M("beige" if i % 5 else "beige-dark"), bevel=0.008)
        for j in range(3):
            lit = (i * 7 + j * 3) % 4 != 0
            cylinder(f"led{i}-{j}", (-0.2 + j * 0.03, -0.278, z), 0.006, 0.005, M("led-green" if lit else "led-red"), axis="Y", sides=6)
    for i in range(6):  # the spaghetti
        beam(f"lead{i}", (0.25, -0.28, 0.3 + i * 0.25), (0.35, -0.2, 0.15), 0.015, M(["ink", "nez-blue", "phone-red"][i % 3]), round_=True)
    crate("spare", (0.9, 0.2, 0), 0.2, "MODEMS")


def site_leased_line():
    box("pillar", (-0.4, 0, 0.55), (0.35, 0.3, 1.1), M("nez-blue"), bevel=0.02)
    cone("pillar-cap", (-0.4, 0, 1.15), 0.24, 0.05, 0.12, M("nez-blue"), sides=4, rot=(0, 0, math.pi / 4))
    T("pillar-text", "NEZTEL", (-0.4, -0.155, 0.85), 0.06, "label")
    cabinet("tel", 0.55, 0.35, 0.8, "cabinet-green")
    k.bpy.context.active_object  # noqa: B018
    for o in [o for o in bpy.context.scene.objects if o.name.startswith("tel-")]:
        o.location.x += 0.45
    beam("handover", (-0.25, -0.05, 0.25), (0.2, -0.05, 0.25), 0.04, M("orange"), round_=True)
    box("tag", (-0.03, -0.08, 0.27), (0.06, 0.01, 0.04), M("label"))  # "DO NOT UNPLUG. — N."


def site_microwave_tower():
    H, base, top = 12.0, 1.1, 0.35
    for sx in (-1, 1):
        for sy in (-1, 1):
            beam(f"leg{sx}{sy}", (sx * base, sy * base, 0), (sx * top, sy * top, H), 0.09, M("galv"))
            box(f"footing{sx}{sy}", (sx * base, sy * base, 0.1), (0.35, 0.35, 0.2), M("concrete"))
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
    # the dish, pointing over the ridge (+Y) with its feed horn
    dish = empty("dish", (0, 0.55, H - 0.6))
    dish.rotation_euler = (math.radians(-90), 0, 0)
    cone("dish-bowl", (0, 0, 0), 0.75, 0.3, 0.3, M("dish"), sides=16, parent=dish)
    cylinder("dish-rim", (0, 0, -0.15), 0.76, 0.03, M("steel"), sides=16, parent=dish)
    beam("feed", (0, 0.6, H - 0.6), (0, 1.2, H - 0.6), 0.04, M("steel"))
    for i, z in enumerate(range(1, 12)):
        box(f"ladder{i}", (-0.0, -base + (base - top) * z / H, z), (0.3, 0.02, 0.02), M("steel"))
    # equipment hut at the foot
    box("hut", (1.9, -0.6, 0.9), (1.4, 1.2, 1.8), M("cabinet-grey"), bevel=0.02)
    box("hut-door", (1.9, -1.205, 0.8), (0.6, 0.01, 1.5), M("cabinet-green"))
    beam("feeder", (1.6, -0.2, 1.7), (0.3, 0.3, H - 0.6), 0.05, M("rubber"), round_=True)


def site_exchange_hut():
    box("walls", (0, 0, 1.3), (3.0, 2.4, 2.6), M("brick"))
    box("roof", (0, 0, 2.65), (3.2, 2.6, 0.12), M("roof-felt"))
    box("door", (-0.6, -1.205, 1.05), (0.9, 0.02, 2.1), M("cabinet-green"))
    cylinder("knob", (-0.25, -1.22, 1.0), 0.03, 0.03, M("brass"), axis="Y", sides=8)
    box("sign", (0.65, -1.21, 2.1), (1.3, 0.02, 0.35), M("label"))
    T("sign-text", "TELO EXCHANGE", (0.65, -1.225, 2.1), 0.13, "ink")
    for i in range(3):
        box(f"vent{i}", (0.65, -1.205, 0.6 + i * 0.12), (0.8, 0.012, 0.05), M("ink"))
    box("ac", (1.2, 1.35, 1.6), (0.7, 0.3, 0.5), M("white-paint"), bevel=0.02)
    cylinder("ac-fan", (1.2, 1.51, 1.6), 0.18, 0.02, M("ink"), axis="Y", sides=12)
    box("step", (-0.6, -1.35, 0.05), (1.1, 0.3, 0.1), M("concrete"))
    cylinder("lamp", (-0.6, -1.25, 2.3), 0.07, 0.1, M("van-lamp") if "van-lamp" in k.PALETTE else M("label"), axis="Y", sides=8)


def site_cold_store():
    box("shed", (0, 0, 1.5), (3.6, 3.0, 3.0), M("corrugated"))
    for i in range(18):
        box(f"rib{i}", (-1.7 + i * 0.2, -1.505, 1.5), (0.03, 0.02, 3.0), M("galv"))
    box("roof", (0, 0, 3.05), (3.8, 3.2, 0.1), M("galv"))
    box("door", (0.6, -1.53, 1.15), (1.4, 0.06, 2.3), M("white-paint"), bevel=0.03)
    for z in (0.5, 1.8):
        box(f"hinge{z}", (-0.1, -1.57, z), (0.12, 0.04, 0.15), M("steel"))
    box("handle", (1.15, -1.58, 1.15), (0.06, 0.06, 0.5), M("steel"))
    T("ghost", "KESTREL ICE CO.", (-0.9, -1.52, 2.55), 0.16, "galv", extrude=0.001)
    box("new-sign", (-0.9, -1.525, 2.05), (1.2, 0.02, 0.36), M("ink"))
    T("new-sign-text", "TELO DATA", (-0.9, -1.54, 2.05), 0.16, "led-green")
    for x in (-1.0, 0.2, 1.2):
        box(f"ac{x}", (x, 0.6, 3.3), (0.8, 0.8, 0.4), M("white-paint"), bevel=0.02)
        cylinder(f"fan{x}", (x, 0.6, 3.51), 0.3, 0.02, M("ink"), sides=12)
    beam("cable-tray", (1.7, -1.0, 2.6), (1.7, 1.2, 2.6), 0.12, M("galv"))


def site_cable_landing():
    box("pad", (0, 0, 0.08), (3.0, 2.4, 0.16), M("concrete"))
    cylinder("manhole", (0.5, -0.3, 0.165), 0.45, 0.02, M("steel"), sides=16)
    T("manhole-text", "CABLE", (0.5, -0.3, 0.18), 0.12, "ink", face="+Z")
    for x in (-1.3, 1.3):
        cylinder(f"bollard{x}", (x, -1.1, 0.5), 0.08, 0.8, M("yellow"), sides=8)
    box("hut", (-0.8, 0.5, 1.2), (1.2, 1.0, 2.1), M("white-paint"), bevel=0.03)
    box("hut-door", (-0.8, -0.005, 1.0), (0.6, 0.02, 1.6), M("cabinet-green"))
    cylinder("marker", (1.2, 0.8, 0.8), 0.05, 1.3, M("orange"), sides=8)
    box("marker-sign", (1.2, 0.74, 1.35), (0.3, 0.02, 0.2), M("orange"))
    T("marker-text", "!", (1.2, 0.725, 1.35), 0.15, "ink")
    beam("sea-cable", (0.5, -0.3, 0.1), (0.5, -3.0, -0.6), 0.12, M("rubber"), round_=True)


def site_cable_reel():
    r, w = 1.1, 0.9
    for sx in (-1, 1):
        cylinder(f"flange{sx}", (sx * w / 2, 0, r + 0.1), r, 0.06, M("yellow"), axis="X", sides=16)
        beam(f"stand{sx}a", (sx * (w / 2 + 0.1), -0.7, 0), (sx * (w / 2 + 0.1), 0, r + 0.1), 0.1, M("steel"))
        beam(f"stand{sx}b", (sx * (w / 2 + 0.1), 0.7, 0), (sx * (w / 2 + 0.1), 0, r + 0.1), 0.1, M("steel"))
    cylinder("cable", (0, 0, r + 0.1), r - 0.15, w - 0.05, M("rubber"), axis="X", sides=16)
    cylinder("axle", (0, 0, r + 0.1), 0.06, w + 0.4, M("steel"), axis="X", sides=8)
    beam("pay-out", (0, -r + 0.2, r - 0.4), (0, -2.0, 0.05), 0.1, M("rubber"), round_=True)
    T("stencil", "FARLAND 1", (w / 2 + 0.035, 0, r + 0.5), 0.14, "ink", face="+X")
    # a marker buoy waiting to go out with it
    cone("buoy", (-1.5, -0.6, 0.35), 0.3, 0.1, 0.7, M("led-red"), sides=10)
    cylinder("buoy-band", (-1.5, -0.6, 0.35), 0.24, 0.12, M("white-paint"), sides=10)


def site_dslam_cabinet():
    top = cabinet("cab", 1.2, 0.6, 1.6, "cabinet-grey")
    for i in range(6):
        box(f"grille{i}", (0.0, 0.302, 0.5 + i * 0.12), (0.7, 0.01, 0.05), M("ink"))
    for sx in (-1, 1):
        cylinder(f"fan{sx}", (sx * 0.62, 0.0, 1.2), 0.15, 0.02, M("ink"), axis="X", sides=12)
    box("sticker", (-0.35, -0.302, 1.2), (0.3, 0.004, 0.12), M("duct"))
    T("sticker-text", "BROADBAND", (-0.35, -0.305, 1.2), 0.05, "label")
    cylinder("lamp", (0.4, -0.305, top - 0.15), 0.02, 0.01, M("led-green"), axis="Y", sides=8)


def site_fibre_joint():
    box("chamber", (0, 0, 0.06), (1.4, 1.0, 0.12), M("concrete"))
    box("hole", (0, 0, 0.121), (1.0, 0.65, 0.004), M("ink"))
    box("lid", (0.95, 0.2, 0.25), (0.05, 0.7, 0.5), M("steel"), rot=(0, math.radians(-15), 0))
    for i, y in enumerate((-0.2, 0.0, 0.2)):
        beam(f"duct{i}", (-0.4, y, 0.12), (-1.2, y * 1.5, 0.05), 0.07, M("duct"), round_=True)
    for i, (x, y) in enumerate([(-0.5, 0.9), (0.6, 0.9), (0.05, 1.5)]):
        beam(f"tripod{i}", (x, y, 0), (0.05, 1.1, 1.0), 0.03, M("steel"))
    cylinder("closure", (0.05, 1.1, 0.75), 0.11, 0.45, M("ink"), sides=10)
    beam("fibre", (0.05, 1.1, 0.53), (0, 0.2, 0.12), 0.02, M("yellow"), round_=True)
    for i, x in enumerate((-0.9, 0.9)):
        cone(f"cone{i}", (x, -0.75, 0.25), 0.15, 0.03, 0.5, M("orange"), sides=8)
        cylinder(f"cone-band{i}", (x, -0.75, 0.3), 0.1, 0.07, M("white-paint"), sides=8)


def site_cell_mast():
    H = 15.0
    cone("mast", (0, 0.4, H / 2), 0.28, 0.14, H, M("galv"), sides=10)
    cylinder("base", (0, 0.4, 0.15), 0.6, 0.3, M("concrete"), sides=10)
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
    # compound: fence and an equipment cabinet
    box("yard", (0, 0.4, 0.02), (3.4, 3.4, 0.04), M("concrete"))
    for i in range(12):
        t = i / 12 * 4
        side, f = int(t), t - int(t)
        corners = [(-1.7, -1.3), (1.7, -1.3), (1.7, 2.1), (-1.7, 2.1)]
        (ax, ay), (bx, by) = corners[side], corners[(side + 1) % 4]
        x, y = ax + (bx - ax) * f, ay + (by - ay) * f
        cylinder(f"post{i}", (x, y, 0.9), 0.03, 1.8, M("fence"), sides=6)
    for z in (0.5, 1.7):
        for s in range(4):
            corners = [(-1.7, -1.3), (1.7, -1.3), (1.7, 2.1), (-1.7, 2.1)]
            if s == 0:  # the gate side (front) left open in the middle
                beam(f"rail{z}-{s}a", (-1.7, -1.3, z), (-0.5, -1.3, z), 0.03, M("fence"))
                beam(f"rail{z}-{s}b", (0.5, -1.3, z), (1.7, -1.3, z), 0.03, M("fence"))
                continue
            beam(f"rail{z}-{s}", (*corners[s], z), (*corners[(s + 1) % 4], z), 0.03, M("fence"))
    box("cab", (1.0, 1.4, 0.8), (0.8, 0.6, 1.5), M("cabinet-grey"), bevel=0.02)
    beam("feeder", (0.8, 1.2, 1.4), (0.1, 0.4, H - 0.8), 0.05, M("rubber"), round_=True)
    box("sign", (-1.0, -1.32, 1.1), (0.5, 0.02, 0.35), M("label"))
    T("sign-text", "TELO SITE 1", (-1.0, -1.335, 1.1), 0.07, "ink")


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
