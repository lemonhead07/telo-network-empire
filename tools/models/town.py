"""The places people live and work, the street, and what grows round them. One builder per prop id.

    blender -b --factory-startup --python tools/models/town.py -- all
    blender -b --factory-startup --python tools/models/town.py -- house.a shop.sols

Same conventions as batch.py (see _kit.py for axes). Builders here are written in GODOT coordinates
(x right, y up, z towards the street/front) through G() and S(), because the game's code that places
doors, windows, install points and collision is in Godot coordinates and these models must match it.

  * Buildings: pivot is the lot origin (ground level, centre of the footprint); the front faces Godot +Z.
  * Street things and plants: pivot is the foot; the front faces Godot +Z.
"""
import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(__file__))
import _kit as k  # noqa: E402

k.PALETTE.update({
    # houses
    "wall-cream": (0.86, 0.80, 0.62), "wall-blue": (0.62, 0.70, 0.76), "wall-pink": (0.84, 0.62, 0.54),
    "wall-sage": (0.68, 0.74, 0.58), "wall-white": (0.90, 0.89, 0.84), "wall-tan": (0.74, 0.60, 0.46),
    "roof-red": (0.52, 0.22, 0.17), "roof-grey": (0.30, 0.31, 0.34), "roof-brown": (0.40, 0.29, 0.20), "roof-charcoal": (0.20, 0.20, 0.23),
    "trim": (0.94, 0.92, 0.86), "plinth": (0.48, 0.46, 0.43), "glass": (0.24, 0.34, 0.44), "glass-in": (0.62, 0.78, 0.90),
    "floorboard": (0.50, 0.36, 0.24), "ceiling": (0.92, 0.89, 0.82), "skirting": (0.36, 0.25, 0.16), "gutter": (0.32, 0.32, 0.34),
    "shutter-green": (0.20, 0.36, 0.26), "shutter-blue": (0.20, 0.30, 0.48), "shutter-red": (0.55, 0.18, 0.14),
    "brick": (0.60, 0.32, 0.24), "brick-dark": (0.48, 0.25, 0.19), "chimney-pot": (0.62, 0.38, 0.26),
    "paper-1": (0.86, 0.80, 0.64), "paper-2": (0.74, 0.80, 0.70), "paper-3": (0.88, 0.74, 0.70), "paper-4": (0.80, 0.82, 0.86),
    "fabric-1": (0.36, 0.42, 0.50), "fabric-2": (0.55, 0.30, 0.22), "fabric-3": (0.42, 0.50, 0.30), "fabric-4": (0.60, 0.50, 0.32),
    "rug": (0.55, 0.25, 0.22), "rug-edge": (0.80, 0.70, 0.45), "wood": (0.42, 0.28, 0.16), "wood-light": (0.66, 0.50, 0.32),
    "tv": (0.15, 0.15, 0.16), "tv-screen": (0.30, 0.40, 0.42), "lampshade": (0.95, 0.86, 0.62), "brass": (0.78, 0.62, 0.25),
    "book-1": (0.60, 0.15, 0.12), "book-2": (0.18, 0.30, 0.50), "book-3": (0.25, 0.42, 0.25), "book-4": (0.85, 0.75, 0.40),
    "ink": (0.12, 0.12, 0.14), "label": (0.95, 0.94, 0.88), "steel": (0.62, 0.64, 0.66), "concrete": (0.68, 0.67, 0.64),
    "door-green": (0.24, 0.34, 0.22),
})
M = k.mat


# ---------------------------------------------------------------------------------------- Godot-space helpers
def G(x, y, z):
    """A Godot position as a Blender one."""
    return (x, -z, y)


def S(x, y, z):
    """A Godot size as a Blender one."""
    return (x, z, y)


def gbox(name, c, s, colour, bevel=0.0, rot_y=0.0):
    o = k.box(name, G(*c), S(*s), M(colour), bevel=bevel)
    if rot_y:
        o.rotation_euler = (0, 0, rot_y)
    return o


def gcyl(name, base, r, h, colour, sides=10, r2=None):
    """Upright (Godot +Y) cylinder or cone standing on `base`."""
    x, y, z = base
    if r2 is None:
        return k.cylinder(name, G(x, y + h / 2, z), r, h, M(colour), sides=sides)
    return k.cone(name, G(x, y + h / 2, z), r, r2, h, M(colour), sides=sides)


def poly(name, verts, faces, colour):
    """A mesh from Godot-space vertices and faces (any winding: normals are recalculated outward)."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([G(*v) for v in verts], [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(M(colour))
    for p in me.polygons:
        p.use_smooth = False
    return o


def slab(name, a, b, c, d, thick, colour):
    """A thick quad (roof plane, awning): corners a-b-c-d in Godot space, extruded `thick` along its normal."""
    from mathutils import Vector
    A, B, C, D = (Vector(p) for p in (a, b, c, d))
    n = (B - A).cross(D - A).normalized() * thick
    top = [A, B, C, D]
    bot = [p - n for p in top]
    verts = [tuple(p) for p in top + bot]
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    return poly(name, verts, faces, colour)


def label(name, body, c, size, colour, face="+Z", extrude=0.003):
    rot = {"+Z": (math.pi / 2, 0, 0), "-Z": (math.pi / 2, 0, math.pi), "+X": (math.pi / 2, 0, math.pi / 2),
           "-X": (math.pi / 2, 0, -math.pi / 2), "up": (0, 0, 0)}[face]
    return k.text(name, body, G(*c), size, M(colour), rotation=rot, extrude=extrude)


# ======================================================================================== houses
# Footprint, walls, door and windows match TownBuilder.HouseMesh exactly (collision is still built there):
# 8 x 7 m, walls 0.2 thick, floor 0.15 up, door gap 1.1 wide in the middle of the front (+Z) wall,
# windows 1.3 x 1.0 centred at x = +-2.5, y = 1.7 front and back. The junction box and drop-wire clips go on the
# front wall at x = -3.6, so nothing stands proud there. Furniture solids: hall table, sofa, television stand.
F, T, W, D = 0.15, 0.2, 8.0, 7.0

HOUSES = {
    #        wall          roof            shutters         wall h  paper     fabric      extras
    "a": ("wall-cream", "roof-red", "shutter-green", 3.0, "paper-1", "fabric-1", {"shutters", "canopy"}),
    "b": ("wall-blue", "roof-grey", None, 3.5, "paper-2", "fabric-2", {"clapboard", "porch"}),
    "c": ("wall-pink", "roof-brown", "shutter-blue", 3.0, "paper-3", "fabric-3", {"shutters", "canopy", "side-chimney"}),
    "d": ("wall-sage", "roof-red", None, 3.5, "paper-4", "fabric-4", {"brick-dado", "canopy"}),
    "e": ("wall-white", "roof-charcoal", "shutter-red", 3.0, "paper-2", "fabric-2", {"clapboard", "dormer", "shutters"}),
    "f": ("brick", "roof-grey", None, 3.5, "paper-1", "fabric-1", {"porch", "white-trim"}),
}


def window(name, x, z, out, wall_colour, shutters=None, w=1.3, h=1.0, y=1.7, side=False):
    """A window in a front/back wall (out = +1 front, -1 back) or a side wall (side=True, x is the wall, z the centre)."""
    if side:
        sx = out
        gbox(f"{name}-trim", (x + sx * 0.02, y, z), (0.06, h + 0.2, w + 0.2), "trim")
        gbox(f"{name}-glass", (x + sx * 0.05, y, z), (0.03, h, w), "glass")
        gbox(f"{name}-bar", (x + sx * 0.07, y, z), (0.02, h, 0.06), "trim")
        gbox(f"{name}-sill", (x + sx * 0.08, y - h / 2 - 0.08, z), (0.16, 0.07, w + 0.3), "trim")
        gbox(f"{name}-in", (x - sx * (T + 0.03), y, z), (0.02, h, w), "glass-in")
        return
    zo = out * (D / 2)
    gbox(f"{name}-trim", (x, y, zo + out * 0.02), (w + 0.2, h + 0.2, 0.06), "trim")
    gbox(f"{name}-glass", (x, y, zo + out * 0.05), (w, h, 0.03), "glass")
    gbox(f"{name}-mullion", (x, y, zo + out * 0.07), (0.06, h, 0.02), "trim")
    gbox(f"{name}-transom", (x, y + 0.2, zo + out * 0.07), (w, 0.05, 0.02), "trim")
    gbox(f"{name}-sill", (x, y - h / 2 - 0.08, zo + out * 0.09), (w + 0.3, 0.07, 0.18), "trim")
    if shutters:
        for s in (-1, 1):
            gbox(f"{name}-shutter{s}", (x + s * (w / 2 + 0.2), y, zo + out * 0.04), (0.34, h + 0.15, 0.04), shutters)
            for i in range(4):
                gbox(f"{name}-slat{s}{i}", (x + s * (w / 2 + 0.2), y - 0.38 + i * 0.25, zo + out * 0.065), (0.3, 0.03, 0.012), "ink")
    # seen from indoors: a pale pane on the inside of the wall
    gbox(f"{name}-in-trim", (x, y, zo - out * (T + 0.025)), (w + 0.2, h + 0.2, 0.02), "trim")
    gbox(f"{name}-in", (x, y, zo - out * (T + 0.035)), (w, h, 0.02), "glass-in")


def house(v):
    wall, roof, shut, wh, paper, fabric, extra = HOUSES[v]
    trim_col = "trim"
    # --- shell -----------------------------------------------------------------------------------
    gbox("foundation", (0, -0.3, 0), (W + 0.1, 0.6, D + 0.1), "plinth")
    gbox("floor", (0, F / 2, 0), (W - 2 * T, F, D - 2 * T), "floorboard")
    gbox("wall-back", (0, wh / 2, -D / 2 + T / 2), (W, wh, T), wall)
    gbox("wall-left", (-W / 2 + T / 2, wh / 2, 0), (T, wh, D - 2 * T), wall)
    gbox("wall-right", (W / 2 - T / 2, wh / 2, 0), (T, wh, D - 2 * T), wall)
    for s in (-1, 1):
        gbox(f"wall-front{s}", (s * 2.275, wh / 2, D / 2 - T / 2), (3.45, wh, T), wall)
    lintel = F + 2.05
    gbox("lintel", (0, (lintel + wh) / 2, D / 2 - T / 2), (1.1, wh - lintel, T), wall)
    gbox("ceiling", (0, wh - 0.05, 0), (W - 2 * T, 0.1, D - 2 * T), "ceiling")
    if "clapboard" in extra:  # horizontal boards on every outside wall
        for i in range(int(wh / 0.3)):
            y = 0.25 + i * 0.3
            if y > lintel + 0.05:
                gbox(f"board-f{i}", (0, y, D / 2 + 0.012), (W, 0.04, 0.025), "trim")
            else:
                for s in (-1, 1):
                    gbox(f"board-f{i}{s}", (s * 2.3, y, D / 2 + 0.012), (3.4, 0.04, 0.025), "trim")
            gbox(f"board-b{i}", (0, y, -D / 2 - 0.012), (W, 0.04, 0.025), "trim")
            for s in (-1, 1):
                gbox(f"board-s{i}{s}", (s * (W / 2 + 0.012), y, 0), (0.025, 0.04, D), "trim")
    if "brick-dado" in extra:  # brick up to the window sills
        h = 1.05
        gbox("dado-b", (0, h / 2, -D / 2 - 0.02), (W + 0.04, h, 0.04), "brick")
        for s in (-1, 1):
            gbox(f"dado-s{s}", (s * (W / 2 + 0.02), h / 2, 0), (0.04, h, D + 0.04), "brick")
            gbox(f"dado-f{s}", (s * 2.3, h / 2, D / 2 + 0.02), (3.4, h, 0.04), "brick")
        for s in (-1, 1):
            gbox(f"dado-cap{s}", (s * 2.3, h, D / 2 + 0.03), (3.4, 0.06, 0.06), "concrete")
    if wall == "brick":  # brick courses picked out
        for i in range(int(wh / 0.45)):
            y = 0.2 + i * 0.45
            gbox(f"course-b{i}", (0, y, -D / 2 - 0.006), (W, 0.03, 0.012), "brick-dark")
            for s in (-1, 1):
                gbox(f"course-s{i}{s}", (s * (W / 2 + 0.006), y, 0), (0.012, 0.03, D), "brick-dark")
                if y > lintel:
                    gbox(f"course-f{i}{s}", (s * 2.0, y, D / 2 + 0.006), (4.0, 0.03, 0.012), "brick-dark")
                else:
                    gbox(f"course-f{i}{s}", (s * 2.3, y, D / 2 + 0.006), (3.4, 0.03, 0.012), "brick-dark")
    # corner boards
    for sx in (-1, 1):
        for sz in (-1, 1):
            gbox(f"corner{sx}{sz}", (sx * (W / 2 + 0.02), wh / 2, sz * (D / 2 + 0.02)), (0.1, wh, 0.1), trim_col)

    # --- door surround, step, number ---------------------------------------------------------------
    for s in (-1, 1):
        gbox(f"doorpost{s}", (s * 0.62, (F + lintel) / 2 + 0.05, D / 2 + 0.03), (0.12, lintel - F + 0.1, 0.06), trim_col)
    gbox("doorhead", (0, lintel + 0.08, D / 2 + 0.03), (1.4, 0.14, 0.08), trim_col)
    gbox("number-plate", (1.0, 1.55, D / 2 + 0.02), (0.22, 0.16, 0.02), "brass")
    label("number", str(ord(v) - 96 + 10), (1.0, 1.55, D / 2 + 0.035), 0.1, "ink")
    if "canopy" in extra:
        slab("canopy", (-0.9, lintel + 0.45, D / 2), (0.9, lintel + 0.45, D / 2), (0.9, lintel + 0.3, D / 2 + 0.75), (-0.9, lintel + 0.3, D / 2 + 0.75), 0.06, roof)
        for s in (-1, 1):
            k.beam(f"bracket{s}", G(s * 0.8, lintel + 0.05, D / 2), G(s * 0.8, lintel + 0.33, D / 2 + 0.6), 0.05, M(trim_col))
    if "porch" in extra:
        for s in (-1, 1):
            gbox(f"porch-post{s}", (s * 1.1, (lintel + 0.35) / 2, D / 2 + 1.35), (0.14, lintel + 0.35, 0.14), trim_col)
        slab("porch-roof", (-1.4, lintel + 0.75, D / 2), (1.4, lintel + 0.75, D / 2), (1.4, lintel + 0.4, D / 2 + 1.6), (-1.4, lintel + 0.4, D / 2 + 1.6), 0.08, roof)
        gbox("porch-beam", (0, lintel + 0.32, D / 2 + 1.35), (2.4, 0.12, 0.12), trim_col)

    # --- windows: front and back (match the night-light quads), and one each side --------------------
    for x in (-2.5, 2.5):
        window(f"wf{x}", x, 0, 1, wall, shut if "shutters" in extra else None)
        window(f"wb{x}", x, 0, -1, wall, None)
    for s in (-1, 1):
        window(f"ws{s}", s * W / 2, -1.6, s, wall, side=True, w=0.9, h=0.9)

    # --- roof: two slopes with thickness, gable ends closed, ridge, gutters, chimney --------------------
    rw, rd, rise, eave = W + 0.8, D + 0.8, 2.3, 0.0
    hw, hd = rw / 2, rd / 2
    slab("roof-front", (-hw, wh + eave, hd), (hw, wh + eave, hd), (hw, wh + rise, 0), (-hw, wh + rise, 0), 0.12, roof)
    slab("roof-back", (hw, wh + eave, -hd), (-hw, wh + eave, -hd), (-hw, wh + rise, 0), (hw, wh + rise, 0), 0.12, roof)
    # tile courses
    for i in range(1, 6):
        t = i / 6
        y, z = wh + rise * t + 0.02, hd * (1 - t)
        gbox(f"tiles-f{i}", (0, y, z + 0.0), (rw, 0.03, 0.05), "roof-charcoal" if roof != "roof-charcoal" else "roof-grey")
        gbox(f"tiles-b{i}", (0, y, -z), (rw, 0.03, 0.05), "roof-charcoal" if roof != "roof-charcoal" else "roof-grey")
    gbox("ridge", (0, wh + rise + 0.03, 0), (rw + 0.05, 0.12, 0.22), "roof-charcoal" if roof != "roof-charcoal" else "roof-grey")
    for s in (-1, 1):  # gable triangles in the wall colour, with a little vent
        x = s * (W / 2)
        eaves_y = wh + rise * (1 - (D / 2) / hd) - 0.1
        poly(f"gable{s}", [(x, wh, -D / 2), (x, wh, D / 2), (x, eaves_y, D / 2), (x, wh + rise - 0.1, 0), (x, eaves_y, -D / 2)],
             [(0, 1, 2, 3, 4)], wall)
        gbox(f"gable-vent{s}", (x + s * 0.03, wh + 0.9, 0), (0.04, 0.35, 0.5), "ink")
        for sz in (-1, 1):
            k.beam(f"barge{s}{sz}", G(s * (hw + 0.02), wh - 0.05, sz * hd), G(s * (hw + 0.02), wh + rise + 0.02, 0), 0.08, M(trim_col))
    for sz in (-1, 1):  # the strip of wall between the wall top and the roof's underside at the eaves
        fh = rise * (1 - (D / 2) / hd) - 0.02
        gbox(f"frieze{sz}", (0, wh + fh / 2, sz * (D / 2 - T / 2)), (W, fh, T), wall)
    for s in (-1, 1):
        gbox(f"gutter{s}", (0, wh - 0.05, s * (hd + 0.04)), (rw, 0.1, 0.1), "gutter")
        gbox(f"downpipe{s}", (s * (W / 2 + 0.15), (wh - 0.05) / 2, s * (hd - 0.1)), (0.08, wh, 0.08), "gutter")
    cx, cz = (2.6, -1.2) if "side-chimney" not in extra else (-W / 2 - 0.25, -0.8)
    ch_base = wh + 0.6 if "side-chimney" not in extra else 0.0
    ch_h = (wh + 1.6 + 0.9) - ch_base
    gbox("chimney", (cx, ch_base + ch_h / 2, cz), (0.7, ch_h, 0.7), "brick")
    gbox("chimney-cap", (cx, ch_base + ch_h + 0.04, cz), (0.82, 0.08, 0.82), "concrete")
    for i, dx in enumerate((-0.15, 0.15)):
        gcyl(f"pot{i}", (cx + dx, ch_base + ch_h + 0.08, cz), 0.09, 0.3, "chimney-pot", sides=8)
    if "dormer" in extra:  # a small dormer on the front slope
        y0, z0 = wh + rise * 0.35, hd * 0.65
        gbox("dormer", (1.6, y0 + 0.45, z0 - 0.2), (1.4, 0.9, 1.2), wall)
        gbox("dormer-glass", (1.6, y0 + 0.45, z0 + 0.41), (0.9, 0.6, 0.03), "glass")
        gbox("dormer-trim", (1.6, y0 + 0.45, z0 + 0.4), (1.05, 0.75, 0.03), trim_col)
        slab("dormer-roof-l", (0.8, y0 + 0.9, z0 + 0.45), (1.6, y0 + 1.25, z0 + 0.45), (1.6, y0 + 1.25, z0 - 0.8), (0.8, y0 + 0.9, z0 - 0.8), 0.06, roof)
        slab("dormer-roof-r", (1.6, y0 + 1.25, z0 + 0.45), (2.4, y0 + 0.9, z0 + 0.45), (2.4, y0 + 0.9, z0 - 0.8), (1.6, y0 + 1.25, z0 - 0.8), 0.06, roof)

    # --- indoors ----------------------------------------------------------------------------------------
    li = 0.006
    gbox("paper-back", (0, wh / 2, -D / 2 + T + li), (W - 2 * T, wh - 0.1, 0.01), paper)
    for s in (-1, 1):
        gbox(f"paper-side{s}", (s * (W / 2 - T - li), wh / 2, 0), (0.01, wh - 0.1, D - 2 * T), paper)
        gbox(f"paper-front{s}", (s * 2.3, wh / 2, D / 2 - T - li), (3.4 - 0.05, wh - 0.1, 0.01), paper)
    gbox("paper-lintel", (0, (lintel + wh) / 2, D / 2 - T - li), (1.1, wh - lintel - 0.1, 0.01), paper)
    sk = 0.1
    gbox("skirt-back", (0, F + sk / 2, -D / 2 + T + 0.015), (W - 2 * T, sk, 0.02), "skirting")
    for s in (-1, 1):
        gbox(f"skirt-side{s}", (s * (W / 2 - T - 0.015), F + sk / 2, 0), (0.02, sk, D - 2 * T), "skirting")
        gbox(f"skirt-front{s}", (s * 2.3, F + sk / 2, D / 2 - T - 0.015), (3.3, sk, 0.02), "skirting")
    # hall table (the telephone lives on it), with a drawer
    gbox("hall-table-top", (-3.35, F + 0.68, -0.5), (0.7, 0.05, 0.9), "wood")
    for dx in (-0.3, 0.3):
        for dz in (-0.4, 0.4):
            gbox(f"hall-leg{dx}{dz}", (-3.35 + dx, F + 0.33, -0.5 + dz), (0.05, 0.66, 0.05), "wood")
    gbox("hall-drawer", (-3.35, F + 0.58, -0.5), (0.6, 0.12, 0.8), "wood-light")
    gbox("hall-knob", (-3.0, F + 0.58, -0.5), (0.03, 0.03, 0.06), "brass")
    # sofa: seat, back, arms, cushions
    gbox("sofa-base", (1.6, F + 0.22, -2.7), (2.2, 0.44, 0.9), fabric)
    gbox("sofa-back", (1.6, F + 0.6, -3.05), (2.2, 0.7, 0.2), fabric)
    for s in (-1, 1):
        gbox(f"sofa-arm{s}", (1.6 + s * 1.05, F + 0.42, -2.7), (0.18, 0.4, 0.9), fabric)
        gbox(f"cushion{s}", (1.6 + s * 0.48, F + 0.49, -2.65), (0.9, 0.1, 0.72), fabric, bevel=0.03)
    # rug where the bench jobs are laid out
    gbox("rug", (1.5, F + 0.008, -0.9), (2.6, 0.016, 1.8), "rug")
    gbox("rug-border", (1.5, F + 0.006, -0.9), (2.8, 0.012, 2.0), "rug-edge")
    # television on its stand
    gbox("tv-stand", (3.4, F + 0.3, 0.8), (0.6, 0.6, 1.0), "wood")
    gbox("tv", (3.35, F + 0.85, 0.8), (0.5, 0.5, 0.6), "tv", bevel=0.02)
    gbox("tv-screen", (3.09, F + 0.87, 0.8), (0.02, 0.36, 0.46), "tv-screen")
    k.beam("aerial-a", G(3.35, F + 1.1, 0.8), G(3.35, F + 1.4, 0.6), 0.012, M("steel"))
    k.beam("aerial-b", G(3.35, F + 1.1, 0.8), G(3.35, F + 1.4, 1.0), 0.012, M("steel"))
    # standard lamp, bookcase under the back window, a picture, the ceiling light
    gcyl("lamp-base", (-3.3, F, -2.9), 0.16, 0.04, "brass", sides=8)
    gcyl("lamp-pole", (-3.3, F + 0.04, -2.9), 0.02, 1.4, "brass", sides=6)
    gcyl("lamp-shade", (-3.3, F + 1.35, -2.9), 0.25, 0.3, "lampshade", sides=8, r2=0.15)
    gbox("bookcase", (-2.5, F + 0.45, -3.12), (1.3, 0.9, 0.32), "wood")
    for row in range(2):
        for i in range(8):
            gbox(f"book{row}-{i}", (-3.05 + i * 0.14, F + 0.22 + row * 0.42, -3.04), (0.08, 0.3 - (i % 3) * 0.03, 0.22),
                 ["book-1", "book-2", "book-3", "book-4"][(i + row) % 4])
    gbox("picture-frame", (W / 2 - T - 0.02, 1.8, 2.0), (0.04, 0.6, 0.8), "wood-light")
    gbox("picture", (W / 2 - T - 0.045, 1.8, 2.0), (0.01, 0.48, 0.68), "wall-blue")
    gcyl("ceiling-rose", (0, wh - 0.14, 0), 0.08, 0.04, "ceiling", sides=8)
    gcyl("ceiling-shade", (0, wh - 0.45, 0), 0.25, 0.25, "lampshade", sides=8, r2=0.08)
    k.beam("flex", G(0, wh - 0.1, 0), G(0, wh - 0.22, 0), 0.01, M("ink"))


def _house(v):
    return lambda: house(v)


# ======================================================================================== registry
PROPS = {}
for v in HOUSES:
    PROPS[f"house.{v}"] = (_house(v), f"House ({v})", f"A small 1980s house, style {v}: hollow, with a furnished front room. "
                           "Walls, door gap and windows match TownBuilder.HouseMesh.", "ground, centre of footprint", False)


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
    # front view from the street side (Blender -Y), a little to the right and above
    k.preview(pid, distance=span * 1.7 + 0.25, height=centre.z + span * 0.45, target=tuple(centre), views=((200, "front"),))
    k.export(pid)
    os.replace(os.path.join(k.PREVIEWS, pid + "-front.png"), os.path.join(k.PREVIEWS, pid + ".png"))
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


if __name__ == "__main__":
    want = k.args() or ["all"]
    ids = list(PROPS) if want == ["all"] else [i for w in want for i in PROPS if i == w or (w.endswith("*") and i.startswith(w[:-1]))]
    results = []
    for pid in ids:
        results.append(build(pid))
        print(f"BUILT {pid}: {results[-1]['tris']} tris, size {results[-1]['size']}")
    update_briefs(results)
    print(f"TOWN OK {len(results)} props")
