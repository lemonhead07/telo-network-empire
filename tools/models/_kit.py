"""Shared helpers for model scripts (D15). Run a model with:

    blender -b --factory-startup --python tools/models/<name>.py

Blender axes: X right, Y forward (the model's front), Z up. The glTF export turns that into Godot's
X right, Y up, front facing -Z, which is what the game's scripts expect.
"""
import math
import os
import sys

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS = os.path.join(ROOT, "game", "assets", "models")
PREVIEWS = os.path.join(ROOT, "docs", "img", "props")

# Named colours (sRGB 0-1). Briefs in game/content/props.json refer to these names.
PALETTE = {
    "van-cream": (0.86, 0.82, 0.68),
    "van-cream-ghost": (0.80, 0.76, 0.62),
    "van-stripe": (0.72, 0.28, 0.16),
    "van-glass": (0.20, 0.29, 0.35),
    "van-tyre": (0.10, 0.10, 0.11),
    "van-hub": (0.70, 0.70, 0.68),
    "van-chrome": (0.78, 0.79, 0.80),
    "van-rack": (0.42, 0.42, 0.44),
    "van-lamp": (1.00, 0.95, 0.72),
    "van-tail": (0.70, 0.08, 0.06),
    "van-grille": (0.16, 0.16, 0.17),
    "van-wood": (0.55, 0.40, 0.25),
}


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()


_mats = {}


def mat(name, rough=0.85, metal=0.0):
    """One material per palette colour, reused across the model."""
    if name in _mats:
        return _mats[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    r, g, b = PALETTE[name]
    bsdf.inputs["Base Color"].default_value = (srgb_to_linear(r), srgb_to_linear(g), srgb_to_linear(b), 1)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    m.diffuse_color = (srgb_to_linear(r), srgb_to_linear(g), srgb_to_linear(b), 1)  # what the preview render shows
    _mats[name] = m
    return m


def _finish(obj, name, material, parent=None, bevel=0.0, segments=1):
    obj.name = name
    obj.data.name = name
    obj.data.materials.append(material)
    if bevel > 0:
        mod = obj.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier="bevel")
    for p in obj.data.polygons:
        p.use_smooth = False
    if parent is not None:
        obj.parent = parent
    return obj


def box(name, center, size, material, parent=None, bevel=0.0, segments=1, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o = _finish(o, name, material, parent, bevel, segments)
    o.rotation_euler = rot
    return o


def cylinder(name, center, radius, depth, material, axis="Z", sides=12, parent=None, bevel=0.0, rot=None):
    rot = rot or {"Z": (0, 0, 0), "X": (0, math.pi / 2, 0), "Y": (math.pi / 2, 0, 0)}[axis]
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth, location=center, rotation=rot)
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return _finish(o, name, material, parent, bevel)


def cone(name, center, r1, r2, depth, material, sides=8, parent=None, rot=(0, 0, 0)):
    """Tapered cylinder along Z (r2 = 0 makes a point), centred on `center`."""
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r1, radius2=r2, depth=depth, location=center, rotation=rot)
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return _finish(o, name, material, parent)


def beam(name, a, b, thick, material, parent=None, round_=False):
    """A square (or 6-sided) bar from point a to point b: lattice legs, braces, straps, cables."""
    from mathutils import Vector
    a, b = Vector(a), Vector(b)
    d = b - a
    if round_:
        bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=thick / 2, depth=d.length, location=(a + b) / 2)
    else:
        bpy.ops.mesh.primitive_cube_add(size=1, location=(a + b) / 2)
        bpy.context.active_object.scale = (thick, thick, d.length)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o = bpy.context.active_object
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = d.to_track_quat("Z", "Y")
    return _finish(o, name, material, parent)


def join_all(name):
    """Merge every mesh into one object (for props the game uses as a single shared mesh, like poles)."""
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    return o


def text(name, body, center, size, material, rotation=(0, 0, 0), extrude=0.004, parent=None):
    """Raised lettering, converted to a mesh so the export doesn't depend on fonts."""
    bpy.ops.object.text_add(location=center, rotation=rotation)
    o = bpy.context.active_object
    o.data.body = body
    o.data.size = size
    o.data.extrude = extrude
    o.data.align_x = "CENTER"
    o.data.align_y = "CENTER"
    o.data.resolution_u = 2  # few curve steps: low-poly letters, not 10k triangles of them
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.active_object
    return _finish(o, name, material, parent)


def empty(name, location=(0, 0, 0), parent=None):
    o = bpy.data.objects.new(name, None)
    o.location = location
    bpy.context.scene.collection.objects.link(o)
    if parent is not None:
        o.parent = parent
    return o


def triangle_count():
    dg = bpy.context.evaluated_depsgraph_get()
    n = 0
    for o in bpy.context.scene.objects:
        if o.type == "MESH":
            m = o.evaluated_get(dg).to_mesh()
            n += sum(len(p.vertices) - 2 for p in m.polygons)
            o.evaluated_get(dg).to_mesh_clear()
    return n


def export(prop_id):
    os.makedirs(MODELS, exist_ok=True)
    path = os.path.join(MODELS, prop_id + ".glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_apply=True, export_yup=True)
    print(f"MODEL {prop_id}: {triangle_count()} tris -> {path}")
    return path


def preview(prop_id, distance=8.0, height=3.0, target=(0, 0, 1.0), views=((35, "front"), (215, "back"))):
    """Contact sheet frames for the owner: one PNG per view, flat colours, no textures."""
    os.makedirs(PREVIEWS, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 960, 640
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("w")
    world.color = (0.55, 0.62, 0.70)
    scene.world = world
    scene.display.shading.background_type = "WORLD"

    tgt = empty("preview-target", target)
    cam = bpy.data.objects.new("preview-cam", bpy.data.cameras.new("preview-cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    track = cam.constraints.new("TRACK_TO")
    track.target = tgt
    out = []
    for deg, label in views:
        a = math.radians(deg)
        cam.location = (math.sin(a) * distance, math.cos(a) * distance, height)
        path = os.path.join(PREVIEWS, f"{prop_id}-{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        out.append(path)
    # keep preview helpers out of the exported model
    bpy.data.objects.remove(cam)
    bpy.data.objects.remove(tgt)
    print("PREVIEW", " ".join(out))
    return out


def args():
    """Arguments after `--` on the blender command line."""
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
