"""Render a multi-material mesh with tiling Painter exports: each material slot <slot> gets
<tex dir>/<slot>_baseColor.png, _normal.png and _occlusionRoughnessMetallic.png (glTF
preset), repeating over the mesh's world-scale UVs. Front, three-quarter, close and keep
views; the check between the tiling export and the Kit import.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/blender/render_tiled_preview.py -- \\
        <mesh.fbx> <tex dir> <out dir>

Writes <out dir>/tiled_*.png, then tiled_done.txt (the launcher detaches).
"""
import math
import os
import sys

import bpy
from mathutils import Vector

fbx, tex_dir, out_dir = sys.argv[sys.argv.index("--") + 1:][:3]
os.makedirs(out_dir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
try:
    bpy.ops.wm.fbx_import(filepath=fbx)
except Exception:
    bpy.ops.import_scene.fbx(filepath=fbx)
obj = [o for o in bpy.data.objects if o.type == "MESH"][0]


def wire(mat):
    slot = mat.name.split(".")[0]
    prefix = os.path.join(tex_dir, slot + "_")
    if not os.path.exists(prefix + "baseColor.png"):
        return False
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    def tex(suffix, space):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(prefix + suffix + ".png")
        n.image.colorspace_settings.name = space
        return n

    nt.links.new(tex("baseColor", "sRGB").outputs["Color"], bsdf.inputs["Base Color"])
    split = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(tex("occlusionRoughnessMetallic", "Non-Color").outputs["Color"], split.inputs["Color"])
    nt.links.new(split.outputs["Green"], bsdf.inputs["Roughness"])
    nt.links.new(split.outputs["Blue"], bsdf.inputs["Metallic"])
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tex("normal", "Non-Color").outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return True


wired = [m.name for m in obj.data.materials if m and wire(m)]
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 64
scene.render.resolution_x, scene.render.resolution_y = 1200, 900
# Standard, not Blender 5's AgX: AgX and a strong sun pressed 0.9 snow and 0.3 stone into
# light and mid grey (Carn Dum, 2026-10-16), so the preview hid the painted contrast
scene.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("w")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.55, 0.62, 1.0)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.45
scene.world = world
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", type="SUN"))
sun.data.energy = 2.5
sun.rotation_euler = (math.radians(55), 0.0, math.radians(-30))
scene.collection.objects.link(sun)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scene.collection.objects.link(cam)
scene.camera = cam
bpy.context.view_layer.update()
corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
lo = Vector([min(c[i] for c in corners) for i in range(3)])
hi = Vector([max(c[i] for c in corners) for i in range(3)])
size = max(hi - lo)
mid = (lo + hi) / 2
shots = {"tiled_front": (Vector((mid.x, lo.y - size * 1.4, mid.z + size * 0.15)), Vector((mid.x, mid.y, mid.z - size * 0.05))),
         "tiled_34": (Vector((mid.x + size * 0.95, lo.y - size * 1.0, hi.z + size * 0.35)), Vector((mid.x, mid.y, mid.z - size * 0.05))),
         "tiled_close": (Vector((mid.x + size * 0.15, lo.y - size * 0.35, lo.z + size * 0.32)), Vector((mid.x, lo.y + size * 0.1, lo.z + size * 0.2))),
         "tiled_keep": (Vector((mid.x + size * 0.18, mid.y - size * 0.45, hi.z - size * 0.05)), Vector((mid.x, mid.y, hi.z - size * 0.28))),
         # about the campaign map camera: far, 55 degrees down
         "tiled_map": (Vector((mid.x + size * 1.1, lo.y - size * 1.1, mid.z + size * 2.2)), Vector((mid.x, mid.y, mid.z - size * 0.1)))}
for name, (loc, target) in shots.items():
    cam.location = loc
    cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(out_dir, name + ".png")
    bpy.ops.render.render(write_still=True)
with open(os.path.join(out_dir, "tiled_done.txt"), "w") as f:
    f.write(",".join(wired))
