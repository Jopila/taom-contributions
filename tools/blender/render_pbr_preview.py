"""Render a low mesh with a Painter glTF export (baseColor, normal,
occlusionRoughnessMetallic): a three-quarter view and a high, map-like view.
The check between a Painter export and the Kit import.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/blender/render_pbr_preview.py -- \\
        <low.fbx> <texture prefix, e.g. E:/.../t_carn_dum_town_> <out dir>

Writes <out dir>/preview_34.png, preview_map.png, then preview_done.txt (the
launcher detaches: wait for that file).
"""
import math
import os
import sys

import bpy
from mathutils import Vector

low, prefix, out_dir = sys.argv[sys.argv.index("--") + 1:][:3]
bpy.ops.wm.read_factory_settings(use_empty=True)
try:
    bpy.ops.wm.fbx_import(filepath=low)
except Exception:
    bpy.ops.import_scene.fbx(filepath=low)
obj = [o for o in bpy.data.objects if o.type == "MESH"][0]

mat = bpy.data.materials.new("painter_preview")
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes["Principled BSDF"]


def tex(suffix, space):
    n = nt.nodes.new("ShaderNodeTexImage")
    n.image = bpy.data.images.load(prefix + suffix + ".png")
    n.image.colorspace_settings.name = space
    return n


base = tex("baseColor", "sRGB")
orm = tex("occlusionRoughnessMetallic", "Non-Color")
split = nt.nodes.new("ShaderNodeSeparateColor")
nt.links.new(orm.outputs["Color"], split.inputs["Color"])
ao_rgb = nt.nodes.new("ShaderNodeCombineColor")
for ch in ("Red", "Green", "Blue"):
    nt.links.new(split.outputs["Red"], ao_rgb.inputs[ch])
mix = nt.nodes.new("ShaderNodeMix")
mix.data_type = "RGBA"
mix.blend_type = "MULTIPLY"
mix.inputs[0].default_value = 1.0
# the Mix node's sockets share names across data types: RGBA A/B/Result are 6/7/2
nt.links.new(base.outputs["Color"], mix.inputs[6])
nt.links.new(ao_rgb.outputs["Color"], mix.inputs[7])
nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
nt.links.new(split.outputs["Green"], bsdf.inputs["Roughness"])
nt.links.new(split.outputs["Blue"], bsdf.inputs["Metallic"])
nrm = tex("normal", "Non-Color")
nmap = nt.nodes.new("ShaderNodeNormalMap")
nt.links.new(nrm.outputs["Color"], nmap.inputs["Color"])
nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
obj.data.materials.clear()
obj.data.materials.append(mat)

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 64
scene.render.resolution_x, scene.render.resolution_y = 1200, 900
world = bpy.data.worlds.new("w")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.38, 0.42, 1.0)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
scene.world = world
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", type="SUN"))
sun.data.energy = 3.5
sun.rotation_euler = (math.radians(50), 0.0, math.radians(-35))
scene.collection.objects.link(sun)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scene.collection.objects.link(cam)
scene.camera = cam

bpy.context.view_layer.update()
d = max(obj.dimensions)
h = obj.dimensions[2]
center = Vector((0.0, 0.0, h * 0.35))
views = {"preview_34": Vector((d * 0.95, -d * 1.15, h * 0.55 + d * 0.1)),
         "preview_map": Vector((d * 0.55, -d * 0.75, d * 1.45))}
for name, loc in views.items():
    cam.location = loc
    cam.rotation_euler = (center - loc).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(out_dir, name + ".png")
    bpy.ops.render.render(write_still=True)
with open(os.path.join(out_dir, "preview_done.txt"), "w") as f:
    f.write("ok")
