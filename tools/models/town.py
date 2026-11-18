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
    "door-green": (0.24, 0.34, 0.22), "corrugated": (0.70, 0.72, 0.72), "beige-box": (0.82, 0.74, 0.56),
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


# ======================================================================================== shared pieces
def gable_roof(n, cx, cz, y, w, d, rise, colour, end_colour=None, thick=0.12, ends_inset=0.0, wall_w=None, wall_d=None):
    """A gable roof with its ridge along X over a w x d rectangle centred at (cx, cz), eaves at height y.
    With end_colour, the triangles under it are closed in that colour at the wall line (wall_w)."""
    hw, hd = w / 2, d / 2
    slab(f"{n}-f", (cx - hw, y, cz + hd), (cx + hw, y, cz + hd), (cx + hw, y + rise, cz), (cx - hw, y + rise, cz), thick, colour)
    slab(f"{n}-b", (cx + hw, y, cz - hd), (cx - hw, y, cz - hd), (cx - hw, y + rise, cz), (cx + hw, y + rise, cz), thick, colour)
    gbox(f"{n}-ridge", (cx, y + rise + 0.03, cz), (w + 0.05, 0.1, 0.2), "roof-charcoal")
    if end_colour:
        ww = wall_w if wall_w else w
        wd = wall_d if wall_d else d
        for s in (-1, 1):
            x = cx + s * ww / 2
            ey = y + rise * (1 - (wd / 2) / hd) - thick
            poly(f"{n}-gable{s}", [(x, y - 0.3, cz - wd / 2), (x, y - 0.3, cz + wd / 2), (x, ey, cz + wd / 2), (x, y + rise - thick, cz), (x, ey, cz - wd / 2)],
                 [(0, 1, 2, 3, 4)], end_colour)


def framed_window(n, x, y, z, w, h, out=1, glass="glass", frame="trim"):
    """A window on a wall facing +Z (out=1) or -Z (out=-1)."""
    gbox(f"{n}-frame", (x, y, z + out * 0.02), (w + 0.16, h + 0.16, 0.05), frame)
    gbox(f"{n}-glass", (x, y, z + out * 0.045), (w, h, 0.02), glass)
    gbox(f"{n}-bar", (x, y, z + out * 0.06), (0.05, h, 0.02), frame)
    gbox(f"{n}-sill", (x, y - h / 2 - 0.06, z + out * 0.08), (w + 0.25, 0.06, 0.15), frame)


def crt(n, x, y, z, size, screen, case="tv", face=1):
    """A television of the period, facing +Z (face=1) or -Z."""
    gbox(f"{n}-case", (x, y + size * 0.4, z), (size, size * 0.8, size * 0.85), case, bevel=0.02)
    gbox(f"{n}-screen", (x - size * 0.08, y + size * 0.42, z + face * size * 0.43), (size * 0.7, size * 0.55, 0.01), screen)
    for i in range(2):
        gcyl(f"{n}-knob{i}", (x + size * 0.38, y + size * 0.25 + i * 0.12 * size, z + face * size * 0.43), 0.02 * size * 2, 0.01, "steel", sides=6)


# ======================================================================================== Sol's Electronics
# Footprint 10 x 8, collision box 10 x 4 x 8 (TownBuilder.Building). The sign text is a Label3D at (0, 3.25, 4.12)
# over the navy board; the counter hotspot is the hatch at x = 3, the door at x = 0.
k.PALETTE.update({
    "shop-wall": (0.80, 0.74, 0.62), "shop-navy": (0.14, 0.18, 0.34), "awning-red": (0.78, 0.18, 0.15), "awning-cream": (0.95, 0.92, 0.82),
    "shop-floor": (0.55, 0.52, 0.48), "screen-blue": (0.35, 0.55, 0.75), "screen-green": (0.35, 0.75, 0.45), "screen-orange": (0.90, 0.60, 0.30),
    "poster-yellow": (0.98, 0.85, 0.25), "radio-wood": (0.52, 0.34, 0.20), "cardboard": (0.72, 0.57, 0.38), "pipe-grey": (0.45, 0.46, 0.48),
})


def shop_sols():
    w, d, h = 10.0, 8.0, 3.7
    gbox("plinth", (0, -0.15, 0), (w + 0.1, 0.3, d + 0.1), "plinth")
    # walls with openings in the front: window (x -3), door (x 0), counter hatch (x 3)
    gbox("wall-back", (0, h / 2, -d / 2 + 0.1), (w, h, 0.2), "shop-wall")
    for s in (-1, 1):
        gbox(f"wall-side{s}", (s * (w / 2 - 0.1), h / 2, 0), (0.2, h, d - 0.4), "shop-wall")
    zf = d / 2 - 0.1
    for x0, x1 in ((-5.0, -4.3), (-1.7, -0.55), (0.55, 1.7), (4.3, 5.0)):  # piers between the openings
        gbox(f"pier{x0}", ((x0 + x1) / 2, h / 2, zf), (x1 - x0, h, 0.2), "shop-wall")
    gbox("stallriser-l", (-3.0, 0.375, zf), (2.6, 0.75, 0.2), "brick")
    gbox("stallriser-r", (3.0, 0.375, zf), (2.6, 0.75, 0.2), "brick")
    gbox("fascia", (0, 2.9, zf), (w, 1.6, 0.2), "shop-wall")
    gbox("over-door", (0, 2.6, zf), (1.1, 0.6, 0.2), "shop-wall")
    gbox("floor", (0, 0.02, 0), (w - 0.4, 0.04, d - 0.4), "shop-floor")
    gbox("ceiling", (0, h - 0.05, 0), (w - 0.4, 0.1, d - 0.4), "ceiling")
    gbox("roof", (0, h + 0.05, 0), (w + 0.4, 0.15, d + 0.4), "roof-charcoal")
    for s in (-1, 1):
        gbox(f"parapet-s{s}", (s * (w / 2 + 0.1), h + 0.3, 0), (0.2, 0.4, d + 0.4), "shop-wall")
    gbox("parapet-b", (0, h + 0.3, -d / 2 - 0.1), (w + 0.4, 0.4, 0.2), "shop-wall")
    gbox("parapet-f", (0, h + 0.3, d / 2 + 0.1), (w + 0.4, 0.4, 0.2), "shop-wall")
    # the sign board (text is drawn by the game) with a trim
    gbox("sign", (0, 3.25, d / 2 + 0.03), (9.0, 0.8, 0.08), "shop-navy")
    gbox("sign-trim", (0, 3.25, d / 2 + 0.01), (9.2, 0.95, 0.06), "awning-cream")
    # striped awning over the windows
    n = 14
    for i in range(n):
        x0 = -4.8 + i * (9.6 / n)
        slab(f"awning{i}", (x0, 2.45, d / 2 + 0.1), (x0 + 9.6 / n, 2.45, d / 2 + 0.1), (x0 + 9.6 / n, 2.05, d / 2 + 1.1), (x0, 2.05, d / 2 + 1.1), 0.03,
             "awning-red" if i % 2 == 0 else "awning-cream")
        gbox(f"valance{i}", (x0 + 4.8 / n, 1.97, d / 2 + 1.1), (9.6 / n, 0.16, 0.02), "awning-red" if i % 2 == 0 else "awning-cream")
    # shop window: frame, glass, and the goods on show
    framed_window("display", -3.0, 1.5, d / 2, 2.6, 1.5, frame="shop-navy")
    gbox("display-shelf", (-3.0, 0.78, d / 2 - 0.45), (2.6, 0.06, 0.6), "wood")
    crt("tv1", -3.7, 0.81, d / 2 - 0.45, 0.55, "screen-blue")
    crt("tv2", -2.95, 0.81, d / 2 - 0.45, 0.45, "screen-green")
    gbox("radio", (-2.25, 0.96, d / 2 - 0.4), (0.5, 0.3, 0.2), "radio-wood", bevel=0.02)
    gbox("radio-grille", (-2.35, 0.96, d / 2 - 0.29), (0.22, 0.2, 0.01), "ink")
    gbox("radio-dial", (-2.1, 0.99, d / 2 - 0.29), (0.14, 0.08, 0.01), "poster-yellow")
    gbox("poster", (-3.9, 1.95, d / 2 - 0.06), (0.5, 0.65, 0.01), "poster-yellow")
    label("poster-text", "SALE", (-3.9, 2.05, d / 2 - 0.05), 0.14, "awning-red")
    label("poster-text2", "MODEMS\nIN STOCK", (-3.9, 1.8, d / 2 - 0.05), 0.07, "ink")
    # the door: glazed, with a sign on it
    gbox("door", (0, 1.05, d / 2 - 0.05), (1.06, 2.1, 0.06), "shop-navy")
    gbox("door-glass", (0, 1.35, d / 2 - 0.015), (0.8, 1.2, 0.02), "glass")
    gbox("open-sign", (0, 1.4, d / 2 + 0.0), (0.4, 0.16, 0.01), "label")
    label("open-text", "OPEN", (0, 1.4, d / 2 + 0.008), 0.08, "awning-red")
    gbox("door-handle", (0.4, 1.0, d / 2 + 0.02), (0.04, 0.3, 0.04), "steel")
    gbox("doorstep", (0, 0.05, d / 2 + 0.2), (1.4, 0.1, 0.4), "concrete")
    # counter hatch: open window with a shelf and a bell, the counter inside
    gbox("hatch-frame-t", (3.0, 2.27, d / 2 + 0.02), (2.8, 0.1, 0.1), "shop-navy")
    for s in (-1, 1):
        gbox(f"hatch-frame{s}", (3.0 + s * 1.35, 1.5, d / 2 + 0.02), (0.1, 1.5, 0.1), "shop-navy")
    gbox("counter-shelf", (3.0, 0.72, d / 2 + 0.25), (2.8, 0.08, 0.5), "wood")
    gcyl("bell-base", (3.7, 0.76, d / 2 + 0.3), 0.07, 0.02, "steel", sides=8)
    gcyl("bell", (3.7, 0.78, d / 2 + 0.3), 0.06, 0.06, "brass", sides=8, r2=0.01)
    gbox("counter", (3.0, 0.5, d / 2 - 0.6), (2.6, 1.0, 0.6), "wood")
    gbox("till", (2.5, 1.1, d / 2 - 0.65), (0.4, 0.2, 0.35), "steel", bevel=0.02)
    gbox("shelves", (3.0, 1.2, -d / 2 + 0.5), (4.0, 2.4, 0.6), "wood")
    for i in range(10):
        gbox(f"stock{i}", (1.3 + (i % 5) * 0.8, 0.6 + (i // 5) * 0.9, -d / 2 + 0.55), (0.5, 0.35, 0.4), "cardboard" if i % 3 else "tv")
    # behind and above: back door, drainpipe, a big roof aerial, a stack of empty boxes by the side wall
    gbox("back-door", (-3.0, 1.05, -d / 2 - 0.02), (0.95, 2.1, 0.05), "door-green")
    gbox("drainpipe", (w / 2 + 0.08, h / 2, d / 2 - 0.3), (0.1, h, 0.1), "pipe-grey")
    k.beam("mast", G(-3.5, h + 0.1, -2.0), G(-3.5, h + 3.2, -2.0), 0.06, M("steel"), round_=True)
    for i, y in enumerate((h + 2.4, h + 2.9)):
        k.beam(f"boom{i}", G(-3.5, y, -2.0 - 0.9), G(-3.5, y, -2.0 + 0.9), 0.03, M("steel"), round_=True)
        for j in range(6):
            zz = -2.0 - 0.75 + j * 0.3
            k.beam(f"el{i}{j}", G(-3.5 - 0.5 + j * 0.04, y, zz), G(-3.5 + 0.5 - j * 0.04, y, zz), 0.015, M("steel"))
    for i, (dx, dy, dz) in enumerate(((0, 0, 0), (0.1, 0.45, 0.05), (-0.05, 0.9, -0.05), (0.7, 0, 0.1))):
        gbox(f"empties{i}", (-w / 2 - 0.5 + dx, 0.22 + dy, -1.5 + dz), (0.6, 0.45, 0.5), "cardboard")


def street_phone_box():
    red, glass = "awning-red", "glass-in"
    gbox("base", (0, 0.06, 0), (1.05, 0.12, 1.05), "concrete")
    for sx in (-1, 1):
        for sz in (-1, 1):
            gbox(f"corner{sx}{sz}", (sx * 0.45, 1.3, sz * 0.45), (0.1, 2.4, 0.1), red)
    for side, (ox, oz, ax) in enumerate(((0, 0.47, "x"), (0, -0.47, "x"), (0.47, 0, "z"), (-0.47, 0, "z"))):
        for r in range(4):
            y = 0.55 + r * 0.45
            size = (0.8, 0.4, 0.02) if ax == "x" else (0.02, 0.4, 0.8)
            gbox(f"pane{side}{r}", (ox, y + 0.22, oz), size, glass)
            bar = (0.8, 0.05, 0.05) if ax == "x" else (0.05, 0.05, 0.8)
            gbox(f"bar{side}{r}", (ox, y, oz), bar, red)
        gbox(f"kick{side}", (ox, 0.3, oz), (0.8, 0.4, 0.04) if ax == "x" else (0.04, 0.4, 0.8), red)
        gbox(f"head{side}", (ox, 2.38, oz), (0.85, 0.25, 0.05) if ax == "x" else (0.05, 0.25, 0.85), red)
    gbox("sign-f", (0, 2.38, 0.5), (0.7, 0.14, 0.01), "label")
    label("sign-text", "TELEPHONE", (0, 2.38, 0.507), 0.07, "ink")
    gbox("roof", (0, 2.58, 0), (1.05, 0.12, 1.05), red)
    gbox("roof2", (0, 2.68, 0), (0.85, 0.1, 0.85), red)
    gbox("phone", (0, 1.45, -0.38), (0.25, 0.35, 0.12), "ink")
    gbox("handset", (-0.08, 1.48, -0.3), (0.06, 0.25, 0.06), "ink")
    gbox("directory", (0.15, 1.05, -0.35), (0.3, 0.05, 0.2), "poster-yellow")


# ======================================================================================== the depot (1 Main Street)
# Footprint 8 x 7, collision box 8 x 4 x 7. Roller door left of the people's door (hotspot at x = 0); the name is a
# Label3D at (0, 2.95, 3.62) on the sign board.
def depot_yard():
    w, d, h = 8.0, 7.0, 3.5
    gbox("plinth", (0, -0.15, 0), (w + 0.1, 0.3, d + 0.1), "plinth")
    gbox("walls-back", (0, h / 2, -d / 2 + 0.1), (w, h, 0.2), "brick")
    for s in (-1, 1):
        gbox(f"walls-side{s}", (s * (w / 2 - 0.1), h / 2, 0), (0.2, h, d - 0.4), "brick")
    zf = d / 2 - 0.1
    gbox("pier-l", (-3.75, h / 2, zf), (0.5, h, 0.2), "brick")
    gbox("pier-m", (-0.45, h / 2, zf), (0.3, h, 0.2), "brick")
    gbox("pier-r", (2.3, h / 2, zf), (3.4, h, 0.2), "brick")
    gbox("over-roller", (-2.0, 3.05, zf), (3.1, 0.9, 0.2), "brick")
    gbox("over-door", (0.15, 2.8, zf), (0.9, 1.4, 0.2), "brick")
    for i in range(int(h / 0.45)):
        y = 0.2 + i * 0.45
        gbox(f"course-b{i}", (0, y, -d / 2 - 0.006), (w, 0.03, 0.012), "brick-dark")
        for s in (-1, 1):
            gbox(f"course-s{i}{s}", (s * (w / 2 + 0.006), y, 0), (0.012, 0.03, d), "brick-dark")
    # roller door: ribbed, slightly raised, with its drum box
    for i in range(13):
        gbox(f"slat{i}", (-2.0, 0.12 + i * 0.2, zf + 0.12), (3.0, 0.17, 0.04), "corrugated")
    gbox("roller-drum", (-2.0, 2.75, zf + 0.2), (3.2, 0.35, 0.3), "steel")
    gbox("roller-handle", (-2.0, 0.35, zf + 0.15), (0.4, 0.05, 0.05), "ink")
    # people's door and window
    gbox("door", (0.15, 1.05, zf + 0.06), (0.9, 2.1, 0.06), "door-green")
    gbox("door-knob", (0.5, 1.0, zf + 0.11), (0.05, 0.05, 0.05), "brass")
    framed_window("window", 2.6, 1.7, d / 2, 1.4, 1.0)
    # sign board, lamp, roof (monopitch, falling to the back)
    gbox("sign", (0, 2.95, d / 2 + 0.05), (5.0, 0.55, 0.06), "label")
    gbox("sign-trim", (0, 2.95, d / 2 + 0.03), (5.15, 0.65, 0.05), "ink")
    k.beam("lamp-arm", G(2.8, 3.2, d / 2), G(2.8, 3.3, d / 2 + 0.5), 0.04, M("ink"))
    gcyl("lamp", (2.8, 3.1, d / 2 + 0.55), 0.18, 0.2, "ink", sides=8, r2=0.05)
    slab("roof", (-w / 2 - 0.2, h + 0.5, d / 2 + 0.3), (w / 2 + 0.2, h + 0.5, d / 2 + 0.3), (w / 2 + 0.2, h - 0.05, -d / 2 - 0.3), (-w / 2 - 0.2, h - 0.05, -d / 2 - 0.3), 0.1, "corrugated")
    for s in (-1, 1):
        poly(f"roof-end{s}", [(s * w / 2, h, d / 2), (s * w / 2, h + 0.45, d / 2), (s * w / 2, h, -d / 2)], [(0, 1, 2)], "brick")
    gbox("gutter", (0, h - 0.1, -d / 2 - 0.35), (w + 0.4, 0.12, 0.12), "gutter")
    gbox("downpipe", (w / 2 - 0.1, h / 2, -d / 2 - 0.35), (0.09, h, 0.09), "gutter")
    # yard clutter beside it: a drum of cable, a pallet, an oil drum
    gbox("pallet", (w / 2 + 1.0, 0.07, 1.5), (1.2, 0.14, 1.0), "wood-light")
    gcyl("oil-drum", (w / 2 + 0.9, 0, -1.5), 0.3, 0.9, "shutter-blue", sides=10)
    gcyl("reel-l", (w / 2 + 1.1, 0.6, 1.5), 0.45, 0.06, "wood-light", sides=10)


# ======================================================================================== Hollow Farm
# Farmhouse at the origin (8 x 7, collision 8 x 4 x 7); the barn's solid is 11 x 5.2 x 15 at (20, 2.6, -6) and the
# silo's 4.2 x 9 x 4.2 at (29, 4.5, -10) (TownBuilder.FarmMesh). The van parks in the yard at (-7, 8).
k.PALETTE.update({
    "farm-white": (0.90, 0.88, 0.80), "barn-red": (0.60, 0.20, 0.16), "barn-trim": (0.92, 0.90, 0.84), "silo": (0.72, 0.74, 0.76),
    "silo-roof": (0.52, 0.54, 0.57), "hay": (0.85, 0.74, 0.42), "tractor-green": (0.25, 0.48, 0.22), "tractor-yellow": (0.95, 0.78, 0.18),
    "tyre": (0.10, 0.10, 0.11), "fence": (0.45, 0.33, 0.21), "water": (0.30, 0.45, 0.55),
})


def tractor(n, x, z, rot=0.0):
    root = k.empty(n, G(x, 0, z))
    root.rotation_euler = (0, 0, rot)
    parts = []
    def P(o):
        o.parent = root
        parts.append(o)
    P(gbox(f"{n}-body", (0, 1.05, 0.2), (0.8, 0.6, 1.8), "tractor-green", bevel=0.03))
    P(gbox(f"{n}-bonnet", (0, 1.25, 0.8), (0.7, 0.35, 1.0), "tractor-green", bevel=0.03))
    P(gbox(f"{n}-grille", (0, 1.1, 1.31), (0.6, 0.4, 0.04), "ink"))
    P(gbox(f"{n}-seat", (0, 1.55, -0.4), (0.45, 0.12, 0.4), "ink"))
    P(gbox(f"{n}-seat-back", (0, 1.8, -0.6), (0.45, 0.45, 0.08), "ink"))
    P(k.beam(f"{n}-wheel-col", G(0, 1.3, 0.1), G(0, 1.75, -0.1), 0.04, M("ink")))
    P(gcyl(f"{n}-steer", (0, 1.73, -0.12), 0.18, 0.03, "ink", sides=10))
    P(k.beam(f"{n}-exhaust", G(0.25, 1.4, 1.0), G(0.25, 2.3, 1.0), 0.06, M("ink"), round_=True))
    for s in (-1, 1):
        P(k.cylinder(f"{n}-rear{s}", G(s * 0.65, 0.75, -0.45), 0.75, 0.4, M("tyre"), axis="X", sides=14))
        P(k.cylinder(f"{n}-rear-hub{s}", G(s * 0.86, 0.75, -0.45), 0.35, 0.03, M("tractor-yellow"), axis="X", sides=10))
        P(k.cylinder(f"{n}-front{s}", G(s * 0.5, 0.42, 1.0), 0.42, 0.25, M("tyre"), axis="X", sides=12))
        P(k.cylinder(f"{n}-front-hub{s}", G(s * 0.63, 0.42, 1.0), 0.2, 0.03, M("tractor-yellow"), axis="X", sides=8))
        P(gbox(f"{n}-mudguard{s}", (s * 0.65, 1.55, -0.45), (0.45, 0.06, 1.2), "tractor-green"))
    return root


def farm_hollow():
    # --- farmhouse
    fw, fd, fh = 8.0, 7.0, 3.1
    gbox("fh-plinth", (0, -0.15, 0), (fw + 0.1, 0.3, fd + 0.1), "plinth")
    gbox("fh-walls", (0, fh / 2, 0), (fw, fh, fd), "farm-white")
    gable_roof("fh-roof", 0, 0, fh, fw + 0.8, fd + 0.8, 2.4, "roof-grey", end_colour="farm-white", wall_w=fw, wall_d=fd)
    gbox("fh-door", (0, 1.0, fd / 2 + 0.03), (1.0, 2.0, 0.06), "door-green")
    gbox("fh-door-frame", (0, 1.05, fd / 2 + 0.01), (1.25, 2.2, 0.04), "trim")
    for x in (-2.5, 2.5):
        framed_window(f"fh-w{x}", x, 1.7, fd / 2, 1.3, 1.0)
        framed_window(f"fh-wb{x}", x, 1.7, -fd / 2, 1.3, 1.0, out=-1)
    # porch on posts
    slab("fh-porch", (-2.0, 2.6, fd / 2), (2.0, 2.6, fd / 2), (2.0, 2.25, fd / 2 + 1.8), (-2.0, 2.25, fd / 2 + 1.8), 0.08, "roof-grey")
    for s in (-1, 1):
        gbox(f"fh-post{s}", (s * 1.8, 1.12, fd / 2 + 1.65), (0.14, 2.25, 0.14), "trim")
    gbox("fh-deck", (0, 0.08, fd / 2 + 0.9), (4.0, 0.16, 1.8), "wood-light")
    gbox("fh-chimney", (-2.6, fh + 1.5, -1.0), (0.7, 2.6, 0.7), "brick")
    for i, dx in enumerate((-0.15, 0.15)):
        gcyl(f"fh-pot{i}", (-2.6 + dx, fh + 2.8, -1.0), 0.09, 0.3, "chimney-pot", sides=8)
    # --- barn (red, white trim, big doors with X-bracing, hay loft)
    bx, bz, bw, bd, bh = 20.0, -6.0, 11.0, 15.0, 5.2
    gbox("barn-walls", (bx, bh / 2, bz), (bw, bh, bd), "barn-red")
    for i in range(int(bw / 0.4)):  # vertical boards on the front
        gbox(f"barn-board{i}", (bx - bw / 2 + 0.2 + i * 0.4, bh / 2, bz + bd / 2 + 0.01), (0.04, bh, 0.02), "brick-dark")
    gable_roof("barn-roof", bx, bz, bh, bw + 0.8, bd + 0.8, 3.2, "roof-charcoal", end_colour="barn-red", wall_w=bw, wall_d=bd)
    zf = bz + bd / 2 + 0.03
    gbox("barn-door-frame", (bx, 1.85, zf), (4.4, 3.8, 0.05), "barn-trim")
    for s in (-1, 1):
        dxc = bx + s * 1.05
        gbox(f"barn-door{s}", (dxc, 1.8, zf + 0.03), (2.0, 3.5, 0.05), "barn-red")
        k.beam(f"barn-x{s}a", G(dxc - 0.95, 0.1, zf + 0.07), G(dxc + 0.95, 3.5, zf + 0.07), 0.12, M("barn-trim"))
        k.beam(f"barn-x{s}b", G(dxc + 0.95, 0.1, zf + 0.07), G(dxc - 0.95, 3.5, zf + 0.07), 0.12, M("barn-trim"))
    gbox("barn-loft", (bx, bh + 1.0, zf), (1.6, 1.4, 0.05), "barn-trim")
    gbox("barn-loft-door", (bx, bh + 1.0, zf + 0.03), (1.3, 1.1, 0.05), "barn-red")
    gbox("barn-hoist", (bx, bh + 2.1, zf + 0.6), (0.15, 0.15, 1.3), "wood")
    for s in (-1, 1):
        for zz in (bz - 4, bz + 1):
            xw = bx + s * bw / 2
            gbox(f"barn-sw{s}{zz}-frame", (xw + s * 0.02, 2.6, zz), (0.05, 0.96, 1.16), "barn-trim")
            gbox(f"barn-sw{s}{zz}-glass", (xw + s * 0.045, 2.6, zz), (0.02, 0.8, 1.0), "glass")
            gbox(f"barn-sw{s}{zz}-bar", (xw + s * 0.06, 2.6, zz), (0.02, 0.8, 0.05), "barn-trim")
    # --- silo
    sx, sz = 29.0, -10.0
    gcyl("silo", (sx, 0, sz), 2.2, 9.0, "silo", sides=16)
    for i in range(6):
        gcyl(f"silo-band{i}", (sx, 1.0 + i * 1.5, sz), 2.23, 0.08, "silo-roof", sides=16)
    gcyl("silo-dome", (sx, 9.0, sz), 2.3, 1.8, "silo-roof", sides=16, r2=0.25)
    for i in range(20):
        gbox(f"silo-rung{i}", (sx, 0.5 + i * 0.42, sz + 2.28), (0.4, 0.04, 0.04), "steel")
    for s in (-1, 1):
        gbox(f"silo-rail{s}", (sx + s * 0.2, 4.6, sz + 2.28), (0.04, 8.4, 0.04), "steel")
    k.beam("silo-chute", G(sx - 1.8, 7.5, sz + 1.0), G(bx + bw / 2, 4.8, sz + 2.0), 0.3, M("silo-roof"), round_=True)
    # --- yard: hay bales, a trough, the tractor, and the old fence toward the road
    for i, (x, z, r) in enumerate(((12, 4, 0.2), (13.6, 4.3, -0.3), (12.8, 4.2, 0.0))):
        y = 0.65 if i < 2 else 1.95
        o = k.cylinder(f"bale{i}", G(x, y, z), 0.65, 1.2, M("hay"), axis="X", sides=12)
        o.rotation_euler.z += r
    gbox("trough", (6.5, 0.3, -5.0), (2.0, 0.6, 0.7), "steel")
    gbox("trough-water", (6.5, 0.55, -5.0), (1.85, 0.04, 0.55), "water")
    tractor("tractor", 9.0, 7.0, math.radians(-25))
    for i, x in enumerate((-8, -4, 4, 8)):
        gbox(f"fence-post{i}", (x, 0.6, 12), (0.15, 1.2, 0.15), "fence")
    for y in (0.5, 0.95):
        gbox(f"fence-rail{y}", (0, y, 12), (16.2, 0.1, 0.08), "fence")
    gbox("sign-post", (-9.5, 0.9, 12.3), (0.12, 1.8, 0.12), "fence")
    gbox("sign", (-9.5, 1.6, 12.38), (1.4, 0.5, 0.05), "farm-white")
    label("sign-text", "HOLLOW FARM\nEGGS", (-9.5, 1.6, 12.42), 0.13, "barn-red")


# ======================================================================================== nature (single meshes, drawn
# thousands of times by PlanetBuilder.Scatter and TownDressing: kept within ~1.5x the old code-built triangle counts)
k.PALETTE.update({
    "bark": (0.38, 0.27, 0.17), "bark-dark": (0.30, 0.21, 0.13), "leaf-1": (0.34, 0.56, 0.23), "leaf-2": (0.29, 0.50, 0.20),
    "leaf-3": (0.40, 0.62, 0.27), "needle-1": (0.19, 0.38, 0.21), "needle-2": (0.23, 0.44, 0.25), "needle-3": (0.27, 0.50, 0.29),
    "rock-1": (0.52, 0.50, 0.48), "rock-2": (0.44, 0.42, 0.41), "moss": (0.36, 0.48, 0.26), "cactus": (0.33, 0.52, 0.30),
    "cactus-dark": (0.27, 0.45, 0.25), "apple": (0.80, 0.16, 0.12), "blossom": (0.95, 0.85, 0.88),
})


def blob(n, c, rx, ry_up, ry_down, colour, sides=7, rot=0.0):
    """A faceted canopy lump: two cones base to base (like LowPoly.Gem), centred at c (Godot)."""
    x, y, z = c
    a = k.cone(f"{n}-u", G(x, y + ry_up / 2, z), rx, 0.0, ry_up, M(colour), sides=sides, rot=(0, 0, rot))
    b = k.cone(f"{n}-d", G(x, y - ry_down / 2, z), 0.0, rx, ry_down, M(colour), sides=sides, rot=(0, 0, rot))
    return a, b


def nature_oak():
    gcyl("trunk", (0, 0, 0), 0.24, 2.0, "bark", sides=6, r2=0.15)
    k.beam("branch-a", G(0, 1.6, 0), G(0.7, 2.5, 0.3), 0.14, M("bark"))
    k.beam("branch-b", G(0, 1.7, 0), G(-0.6, 2.6, -0.3), 0.12, M("bark"))
    blob("crown", (0, 3.1, 0), 1.7, 1.5, 1.1, "leaf-1", sides=7)
    blob("crown-r", (0.85, 2.7, 0.4), 1.05, 0.9, 0.6, "leaf-2", sides=6, rot=0.4)
    blob("crown-l", (-0.75, 2.85, -0.35), 1.0, 0.9, 0.6, "leaf-3", sides=6, rot=0.9)


def nature_pine():
    gcyl("trunk", (0, 0, 0), 0.2, 1.4, "bark-dark", sides=5, r2=0.13)
    for i, (y, r, h, c) in enumerate(((0.9, 1.6, 2.4, "needle-1"), (2.0, 1.25, 2.1, "needle-2"), (3.1, 0.9, 1.8, "needle-3"), (4.1, 0.5, 1.3, "needle-3"))):
        k.cone(f"tier{i}", G(0, y + h / 2, 0), r, 0.0, h, M(c), sides=7, rot=(0, 0, i * 0.45))


def nature_rock():
    blob("rock", (0, 0.25, 0), 0.9, 0.55, 0.4, "rock-1", sides=5)
    blob("rock-b", (0.6, 0.12, 0.35), 0.5, 0.35, 0.3, "rock-2", sides=4, rot=0.7)
    blob("moss", (-0.15, 0.62, -0.1), 0.45, 0.12, 0.05, "moss", sides=5)


def nature_cactus():
    gcyl("stem", (0, 0, 0), 0.28, 2.3, "cactus", sides=7, r2=0.22)
    gcyl("stem-top", (0, 2.3, 0), 0.22, 0.2, "cactus", sides=7, r2=0.1)
    k.beam("arm-r", G(0.15, 1.2, 0), G(0.65, 1.3, 0), 0.22, M("cactus-dark"), round_=True)
    gcyl("arm-r-up", (0.68, 1.2, 0), 0.12, 0.75, "cactus", sides=6, r2=0.09)
    k.beam("arm-l", G(-0.15, 0.9, 0), G(-0.5, 0.95, 0.1), 0.18, M("cactus-dark"), round_=True)
    gcyl("arm-l-up", (-0.52, 0.9, 0.1), 0.1, 0.5, "cactus", sides=6, r2=0.07)
    gcyl("flower", (0, 2.45, 0), 0.08, 0.06, "apple", sides=5)


def nature_fruit_tree():
    gcyl("trunk", (0, 0, 0), 0.13, 1.3, "bark", sides=6, r2=0.1)
    k.beam("fork-a", G(0, 1.1, 0), G(0.45, 1.8, 0.2), 0.09, M("bark"))
    k.beam("fork-b", G(0, 1.15, 0), G(-0.4, 1.85, -0.15), 0.08, M("bark"))
    blob("crown", (0, 2.2, 0), 1.25, 0.9, 0.7, "leaf-3", sides=8)
    blob("crown-b", (0.5, 2.0, 0.35), 0.75, 0.6, 0.4, "leaf-1", sides=6, rot=0.5)
    for i in range(9):
        a = i * 2.39
        r = 0.9 + (i % 3) * 0.12
        gcyl(f"apple{i}", (math.cos(a) * r, 1.75 + (i % 4) * 0.18, math.sin(a) * r), 0.07, 0.12, "apple", sides=5)
    gbox("prop", (0.5, 0.6, 0.0), (0.05, 1.2, 0.05), "wood-light")  # a stick holding up the heavy branch


# ======================================================================================== street things
def street_lamp():
    """Concrete-grey column, arm reaching over the road (-Z), the head lit warm."""
    gcyl("base", (0, 0, 0), 0.16, 0.5, "steel", sides=8, r2=0.11)
    gcyl("column", (0, 0.5, 0), 0.09, 4.6, "steel", sides=8, r2=0.06)
    k.beam("arm", G(0, 5.0, 0), G(0, 5.25, -1.3), 0.07, M("steel"), round_=True)
    gbox("head", (0, 5.18, -1.45), (0.32, 0.16, 0.6), "steel", bevel=0.03)
    gbox("lens", (0, 5.08, -1.45), (0.24, 0.04, 0.48), "lampshade")
    gbox("door", (0, 0.9, 0.095), (0.1, 0.3, 0.02), "ink")


# ======================================================================================== cars and vans (front faces -Z)
k.PALETTE.update({
    "car-teal": (0.24, 0.50, 0.52), "car-mustard": (0.80, 0.62, 0.20), "car-maroon": (0.50, 0.16, 0.18), "car-cream": (0.88, 0.84, 0.70),
    "car-glass": (0.28, 0.38, 0.46), "chrome": (0.80, 0.81, 0.82), "headlamp": (1.0, 0.95, 0.75), "tail": (0.70, 0.08, 0.06),
    "nez-blue": (0.12, 0.20, 0.50), "nez-letter": (0.95, 0.88, 0.45), "plate": (0.95, 0.94, 0.88), "wood-panel": (0.55, 0.38, 0.22),
})


def car(body, length=3.9, roof_len=2.0, roof_shift=0.15, estate=False, wood=False, hatch=False):
    w = 1.7
    gbox("body", (0, 0.58, 0), (w, 0.55, length), body, bevel=0.06)
    gbox("sill", (0, 0.32, 0), (w + 0.02, 0.06, length - 0.6), "ink")
    if estate:
        roof_len, roof_shift = length * 0.62, 0.45
    if hatch:
        roof_len, roof_shift = length * 0.5, 0.35
    rz = roof_shift
    # cabin: glass band then roof
    gbox("glass", (0, 1.07, rz), (w - 0.12, 0.42, roof_len), "car-glass")
    gbox("roof", (0, 1.31, rz), (w - 0.1, 0.07, roof_len - 0.05), body, bevel=0.02)
    for sx in (-1, 1):
        gbox(f"pillar-a{sx}", (sx * (w / 2 - 0.08), 1.07, rz - roof_len / 2 + 0.05), (0.07, 0.42, 0.08), body)
        gbox(f"pillar-b{sx}", (sx * (w / 2 - 0.07), 1.07, rz), (0.07, 0.42, 0.08), body)
        gbox(f"pillar-c{sx}", (sx * (w / 2 - 0.08), 1.07, rz + roof_len / 2 - 0.05), (0.07, 0.42, 0.1), body)
        if wood:
            gbox(f"wood{sx}", (sx * (w / 2 + 0.005), 0.62, 0.2), (0.01, 0.3, length - 1.2), "wood-panel")
        gbox(f"handle{sx}", (sx * (w / 2 + 0.01), 0.75, -0.2), (0.02, 0.04, 0.16), "chrome")
        gbox(f"mirror{sx}", (sx * (w / 2 + 0.1), 0.95, rz - roof_len / 2 + 0.2), (0.12, 0.1, 0.06), "ink")
    for zz, name in ((-length / 2 - 0.02, "front"), (length / 2 + 0.02, "back")):
        gbox(f"bumper-{name}", (0, 0.42, zz), (w + 0.06, 0.12, 0.08), "chrome")
        gbox(f"plate-{name}", (0, 0.55, zz - 0.02 * (1 if zz < 0 else -1)), (0.5, 0.12, 0.02), "plate")
    for sx in (-1, 1):
        gbox(f"headlamp{sx}", (sx * 0.58, 0.7, -length / 2 - 0.005), (0.3, 0.16, 0.02), "headlamp")
        gbox(f"tail{sx}", (sx * 0.62, 0.7, length / 2 + 0.005), (0.25, 0.14, 0.02), "tail")
    gbox("grille", (0, 0.68, -length / 2 - 0.006), (0.7, 0.16, 0.02), "ink")
    for sx in (-1, 1):
        for sz in (-1, 1):
            zz = sz * (length / 2 - 0.7)
            k.cylinder(f"wheel{sx}{sz}", G(sx * 0.78, 0.31, zz), 0.31, 0.2, M("tyre"), axis="X", sides=12)
            k.cylinder(f"hub{sx}{sz}", G(sx * 0.89, 0.31, zz), 0.16, 0.02, M("chrome"), axis="X", sides=8)
            gbox(f"arch{sx}{sz}", (sx * (w / 2 + 0.005), 0.62, zz), (0.02, 0.1, 0.75), "ink")


def nez_van():
    """The national phone company's van: same size as yours (2.0 x 1.9 x 4.6), blue, lettered both sides."""
    L, Wd = 4.6, 2.0
    gbox("body", (0, 1.15, 0.25), (Wd, 1.7, L - 0.5), "nez-blue", bevel=0.05)
    gbox("cab", (0, 0.95, -L / 2 + 0.35), (Wd, 1.3, 0.7), "nez-blue", bevel=0.08)
    gbox("windscreen", (0, 1.5, -L / 2 + 0.34), (1.7, 0.5, 0.65), "car-glass")
    gbox("stripe", (0, 0.85, 0.1), (Wd + 0.02, 0.18, L - 0.2), "nez-letter")
    for sx in (-1, 1):
        gbox(f"side-window{sx}", (sx * (Wd / 2 + 0.005), 1.5, -L / 2 + 0.95), (0.01, 0.45, 0.6), "car-glass")
        label(f"letter{sx}", "NATIONAL TELEPHONE", (sx * (Wd / 2 + 0.012), 1.45, 0.6), 0.2, "nez-letter", face="+X" if sx > 0 else "-X")
        gbox(f"mirror{sx}", (sx * (Wd / 2 + 0.12), 1.4, -L / 2 + 0.6), (0.1, 0.2, 0.06), "ink")
    gbox("bumper-f", (0, 0.4, -L / 2 - 0.02), (Wd + 0.06, 0.16, 0.1), "ink")
    gbox("bumper-b", (0, 0.4, L / 2 + 0.02), (Wd + 0.06, 0.16, 0.1), "ink")
    for sx in (-1, 1):
        gbox(f"headlamp{sx}", (sx * 0.7, 0.72, -L / 2 - 0.005), (0.3, 0.18, 0.02), "headlamp")
        gbox(f"tail{sx}", (sx * 0.85, 0.8, L / 2 + 0.005), (0.15, 0.3, 0.02), "tail")
        for sz in (-1, 1):
            zz = sz * (L / 2 - 0.8)
            k.cylinder(f"wheel{sx}{sz}", G(sx * 0.9, 0.36, zz), 0.36, 0.24, M("tyre"), axis="X", sides=12)
            k.cylinder(f"hub{sx}{sz}", G(sx * 1.03, 0.36, zz), 0.18, 0.02, M("chrome"), axis="X", sides=8)
    gbox("back-door-seam", (0, 1.15, L / 2 + 0.005), (0.02, 1.5, 0.01), "ink")
    gbox("roof-rack", (0, 2.05, 0.5), (1.6, 0.06, 2.6), "steel")
    gbox("ladder", (0.3, 2.13, 0.5), (0.45, 0.08, 2.8), "steel")
    gcyl("beacon", (-0.5, 2.0, -1.2), 0.1, 0.15, "car-mustard", sides=8)
    gbox("plate-f", (0, 0.55, -L / 2 - 0.07), (0.5, 0.12, 0.02), "plate")


# ======================================================================================== registry
PROPS = {}
for v in HOUSES:
    PROPS[f"house.{v}"] = (_house(v), f"House ({v})", f"A small 1980s house, style {v}: hollow, with a furnished front room. "
                           "Walls, door gap and windows match TownBuilder.HouseMesh.", "ground, centre of footprint", False)

PROPS["shop.sols"] = (shop_sols, "Sol's Electronics", "The town's electronics shop: display window of televisions, striped awning, the counter hatch where modems are sold, a roof aerial.", "ground, centre of footprint", False)
PROPS["depot.yard"] = (depot_yard, "1 Main Street (the depot)", "A brick workshop with a roller door and a sign board; the yard you lease for your staff.", "ground, centre of footprint", False)
PROPS["farm.hollow"] = (farm_hollow, "Hollow Farm", "Farmhouse with a porch, red barn with X-braced doors and hay loft, silo, tractor, hay bales, the fence toward the road.", "farmhouse ground centre", False)
PROPS["street.phone-box"] = (street_phone_box, "Phone box", "The national phone company's red kiosk outside Sol's.", "foot, centre", False)

PROPS["nature.oak"] = (nature_oak, "Oak", "A broad faceted oak, three canopy lumps. Scattered on grassland and in hedgerows.", "foot of the trunk", True)
PROPS["nature.pine"] = (nature_pine, "Pine", "Four tiers of needles on a dark trunk. Forests and cold places.", "foot of the trunk", True)
PROPS["nature.rock"] = (nature_rock, "Rock", "A boulder with a smaller one beside it and a cap of moss.", "ground, centre", True)
PROPS["nature.cactus"] = (nature_cactus, "Cactus", "A desert cactus with two arms and one red flower.", "foot", True)
PROPS["nature.fruit-tree"] = (nature_fruit_tree, "Apple tree", "A small garden apple tree with fruit on it and a stick propping up a branch.", "foot of the trunk", True)
PROPS["street.lamp"] = (street_lamp, "Street lamp", "Grey column with an arm reaching over the road (towards -Z), a warm lens.", "foot, centre", True)
PROPS["car.saloon"] = (lambda: car("car-teal"), "Saloon car", "A household's four-door saloon, teal.", "ground, centre", False)
PROPS["car.estate"] = (lambda: car("car-cream", length=4.2, estate=True, wood=True), "Estate car", "A long estate with wood panels down the sides.", "ground, centre", False)
PROPS["car.hatch"] = (lambda: car("car-mustard", length=3.5, hatch=True), "Hatchback", "A small mustard hatchback.", "ground, centre", False)
PROPS["car.coupe"] = (lambda: car("car-maroon", length=4.0, roof_len=1.5, roof_shift=0.3), "Coupe", "A maroon two-door with a short roof.", "ground, centre", False)
PROPS["vehicle.nez-van"] = (nez_van, "National Telephone van", "The monopoly's blue van, lettered on both sides, ladder on the roof.", "ground, centre", False)


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
