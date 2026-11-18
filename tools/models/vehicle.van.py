"""vehicle.van — the service van (brief: game/content/props.json).

    blender -b --factory-startup --python tools/models/vehicle.van.py

Writes game/assets/models/vehicle.van.glb and docs/img/props/vehicle.van-*.png.
Front faces +Y here (-Z in Godot). Origin: ground under the middle of the body.
Wheels are empties named wheel-fl/fr/rl/rr at the hub, so the game can spin them.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import _kit as k  # noqa: E402

k.reset()

cream = k.mat("van-cream", rough=0.55)
ghost = k.mat("van-cream-ghost", rough=0.7)
stripe = k.mat("van-stripe", rough=0.55)
glass = k.mat("van-glass", rough=0.15)
tyre = k.mat("van-tyre", rough=0.95)
hub = k.mat("van-hub", rough=0.4, metal=0.6)
chrome = k.mat("van-chrome", rough=0.25, metal=0.9)
rack = k.mat("van-rack", rough=0.6, metal=0.5)
lamp = k.mat("van-lamp", rough=0.2)
tail = k.mat("van-tail", rough=0.3)
grille = k.mat("van-grille", rough=0.8)
wood = k.mat("van-wood", rough=0.9)

# --- body: tall cargo box with the cab at its front, and a short bonnet ahead of it
k.box("body", (0, -0.3, 1.15), (1.9, 3.6, 1.5), cream, bevel=0.09, segments=2)
k.box("bonnet", (0, 1.95, 0.95), (1.88, 0.92, 1.1), cream, bevel=0.07, segments=2)
k.box("stripe", (0, -0.3, 0.95), (1.93, 3.63, 0.2), stripe)
k.box("stripe-nose", (0, 1.95, 0.95), (1.91, 0.95, 0.2), stripe)
k.box("sill", (0, 0.2, 0.47), (1.92, 4.3, 0.12), grille)

# --- glass
k.box("windscreen", (0, 1.505, 1.56), (1.66, 0.03, 0.56), glass, bevel=0.03)
for sx in (-1, 1):
    k.box(f"cab-window-{'l' if sx < 0 else 'r'}", (sx * 0.953, 1.12, 1.56), (0.02, 0.62, 0.52), glass)
    k.box(f"door-handle-{'l' if sx < 0 else 'r'}", (sx * 0.962, 0.74, 1.2), (0.02, 0.12, 0.03), chrome)
    k.box(f"door-seam-{'l' if sx < 0 else 'r'}", (sx * 0.952, 0.72, 1.1), (0.01, 0.012, 1.25), grille)
    # wing mirror on a short arm
    k.box(f"mirror-arm-{'l' if sx < 0 else 'r'}", (sx * 1.0, 1.42, 1.42), (0.12, 0.02, 0.02), grille)
    k.box(f"mirror-{'l' if sx < 0 else 'r'}", (sx * 1.07, 1.42, 1.48), (0.04, 0.09, 0.2), grille, bevel=0.01)

# --- nose: grille, round headlamps, chrome bumper
k.box("grille", (0, 2.405, 0.95), (1.0, 0.02, 0.32), grille)
for i in range(5):
    k.box(f"grille-bar-{i}", (0, 2.415, 0.83 + i * 0.06), (0.98, 0.01, 0.015), chrome)
for sx in (-1, 1):
    k.cylinder(f"headlamp-rim-{sx}", (sx * 0.68, 2.405, 0.95), 0.15, 0.03, chrome, axis="Y", sides=16)
    k.cylinder(f"headlamp-{sx}", (sx * 0.68, 2.42, 0.95), 0.12, 0.03, lamp, axis="Y", sides=16)
k.box("bumper-front", (0, 2.46, 0.46), (1.98, 0.1, 0.16), chrome, bevel=0.03)
k.box("number-plate-front", (0, 2.515, 0.46), (0.5, 0.01, 0.11), lamp)

# --- back: two doors, tail lamps, bumper, fold-down tailboard (kit rides here)
k.box("rear-seam", (0, -2.104, 1.15), (0.012, 0.01, 1.3), grille)
for sx in (-1, 1):
    k.box(f"rear-handle-{sx}", (sx * 0.1, -2.11, 1.1), (0.08, 0.02, 0.03), chrome)
    k.box(f"tail-lamp-{sx}", (sx * 0.8, -2.106, 0.82), (0.14, 0.02, 0.26), tail)
    k.box(f"rear-window-{sx}", (sx * 0.45, -2.104, 1.6), (0.6, 0.01, 0.36), glass)
    chain = k.box(f"tailboard-chain-{sx}", (sx * 0.86, -2.41, 0.83), (0.02, 0.84, 0.02), rack)
    chain.rotation_euler = (math.radians(42), 0, 0)  # from the door frame down to the board's far corner
k.box("bumper-rear", (0, -2.16, 0.46), (1.98, 0.1, 0.16), chrome, bevel=0.03)
k.box("tailboard", (0, -2.42, 0.5), (1.8, 0.66, 0.06), wood, bevel=0.01)

# --- roof rack: two rails, three crossbars, six feet (the ladder rides on it)
for sx in (-1, 1):
    k.box(f"rack-rail-{sx}", (sx * 0.6, -0.3, 2.02), (0.05, 2.4, 0.05), rack)
    for y in (-1.4, -0.3, 0.8):
        k.box(f"rack-foot-{sx}-{y}", (sx * 0.6, y, 1.95), (0.05, 0.05, 0.1), rack)
for y in (-1.4, -0.3, 0.8):
    k.box(f"rack-bar-{y}", (0, y, 2.02), (1.25, 0.05, 0.04), rack)

# --- wheels: an empty at each hub (what the game spins), tyre + hub + one nut bar so the spin shows
for name, (x, y) in {"wheel-fl": (-0.95, 1.5), "wheel-fr": (0.95, 1.5), "wheel-rl": (-0.95, -1.4), "wheel-rr": (0.95, -1.4)}.items():
    w = k.empty(name, (x, y, 0.38))
    sx = 1 if x > 0 else -1
    k.cylinder(f"{name}-tyre", (0, 0, 0), 0.38, 0.26, tyre, axis="X", sides=16, parent=w)
    k.cylinder(f"{name}-hub", (sx * 0.01, 0, 0), 0.18, 0.27, hub, axis="X", sides=12, parent=w)
    k.box(f"{name}-nut", (sx * 0.14, 0, 0), (0.02, 0.24, 0.05), grille, parent=w)
    k.box(f"{name}-arch", (x * 0.995, y, 0.62), (0.03, 0.9, 0.1), grille)  # dark lip over the tyre

# --- lettering: TELO hand-painted, slightly uneven, over the ghost of the bakery it used to be
for sx in (-1, 1):
    rot = (math.radians(90), 0, math.radians(90 * sx))   # facing outwards, reading front-to-back on the left, back-to-front on the right
    read = (0, sx, 0)                                  # the direction the words run along the side
    k.text(f"ghost-{sx}", "PENROSE BAKERY", (sx * 0.951, -0.5, 1.72), 0.2, ghost, rotation=rot, extrude=0.001)
    for i, (ch, wob) in enumerate(zip("TELO", (0.02, -0.015, 0.01, -0.025))):
        off = (i - 1.5) * 0.36
        tilt = (math.radians(90), math.radians(wob * 60 * sx), math.radians(90 * sx))
        k.text(f"telo-{sx}-{ch}", ch, (sx * 0.958, -0.5 + read[1] * off, 1.36 + wob), 0.46, stripe, rotation=tilt, extrude=0.006)
    k.text(f"tagline-{sx}", "LINES  FITTED  DAY  OR  NIGHT", (sx * 0.956, -0.5, 1.12), 0.1, grille, rotation=rot, extrude=0.002)

k.preview("vehicle.van", distance=8.5, height=3.2, target=(0, 0, 1.0))
k.export("vehicle.van")
