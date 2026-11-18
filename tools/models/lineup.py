"""Contact sheet of every model in game/assets/models: each scaled to fit one cell, labelled, one render.

    blender -b --factory-startup --python tools/models/lineup.py -- [prefix ...] [--out name]

Writes docs/img/props/lineup[-name].png.
"""
import glob
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
import _kit as k  # noqa: E402

argv = k.args()
out = "lineup"
if "--out" in argv:
    i = argv.index("--out"); out = "lineup-" + argv[i + 1]; argv = argv[:i] + argv[i + 2:]
prefixes = argv or [""]

k.reset()
files = sorted(f for f in glob.glob(os.path.join(k.MODELS, "*.glb")) if any(os.path.basename(f).startswith(p) for p in prefixes))
cols = math.ceil(math.sqrt(len(files) * 1.5))
cell = 1.5
k.PALETTE["label-ink"] = (0.1, 0.1, 0.12)
for n, f in enumerate(files):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=f)
    new = [o for o in bpy.context.scene.objects if o not in before]
    roots = [o for o in new if o.parent is None]
    holder = bpy.data.objects.new(os.path.basename(f), None)
    bpy.context.scene.collection.objects.link(holder)
    for r in roots:
        r.parent = holder
    bpy.context.view_layer.update()
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    for o in new:
        if o.type == "MESH":
            for v in o.bound_box:
                w = o.matrix_world @ Vector(v)
                lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    size = hi - lo
    s = 1.0 / max(size.x, size.y, size.z)
    col, row = n % cols, n // cols
    holder.scale = (s, s, s)
    holder.location = (col * cell - ((lo.x + hi.x) / 2) * s, -row * cell * 1.8 - ((lo.y + hi.y) / 2) * s, -lo.z * s)
    k.text(f"lbl{n}", os.path.basename(f)[:-4], (col * cell, -row * cell * 1.8 - 0.62, 0.01), 0.11, k.mat("label-ink"))

rows = math.ceil(len(files) / cols)
centre = ((cols - 1) * cell / 2, -(rows - 1) * cell * 1.8 / 2, 0.4)
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x, scene.render.resolution_y = 2400, int(2400 * rows * 0.95 / cols) + 200
w = bpy.data.worlds.new("w"); w.color = (0.86, 0.88, 0.9); scene.world = w
scene.display.shading.background_type = "WORLD"
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scene.collection.objects.link(cam)
scene.camera = cam
cam.data.type = "ORTHO"
cam.data.ortho_scale = cols * cell + 0.6
cam.location = (centre[0], centre[1] - 9, 5.2)
cam.rotation_euler = (math.radians(62), 0, 0)
os.makedirs(k.PREVIEWS, exist_ok=True)
scene.render.filepath = os.path.join(k.PREVIEWS, out + ".png")
bpy.ops.render.render(write_still=True)
print("LINEUP", scene.render.filepath, len(files))
