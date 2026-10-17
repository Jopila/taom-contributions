"""Render a mesh with its Painter glTF exports, one texture set per material slot:
<tex dir>/<slot>_baseColor.png, _normal.png and _occlusionRoughnessMetallic.png (Painter names a
texture set after its material). Both routes of the /prep-tripo-prop skill use it: a decimated
Tripo low (one slot, t_<mesh>) and a rebuilt icon (one slot per texture set).

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/blender/render_painter_preview.py -- \\
        <mesh.fbx> <tex dir> <out dir> [--albedo]

Lit views use the Standard view transform: Blender 5's AgX and a strong sun pressed 0.9 snow and
0.3 stone into light and mid grey on Carn Dum (2026-10-16), hiding the painted contrast. The AO
in the ORM map is left out, as the game puts it in _s, not in the diffuse. With --albedo every
material is emission of its base colour only: the painted value with no light and no tone
mapping, the check when a lit view and the exported PNG disagree.

Writes <out dir>/preview_<view>.png (albedo_<view>.png with --albedo) for the front,
three-quarter, close, top and map-distance views, and <out dir>/_report/DONE.txt
(tools/blender/blender_job.py; "wired" lists the slots that found textures).
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blender_job import import_fbx, job, report_path, script_args  # noqa: E402


def wire(mat, tex_dir, albedo):
    """Point one material at its set's textures; False when the set has no baseColor."""
    prefix = os.path.join(tex_dir, mat.name.split(".")[0] + "_")
    if not os.path.exists(prefix + "baseColor.png"):
        return False
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")

    def tex(suffix, space):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(prefix + suffix + ".png")
        n.image.colorspace_settings.name = space
        return n

    base = tex("baseColor", "sRGB")
    if albedo:
        em = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(base.outputs["Color"], em.inputs["Color"])
        nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
        return True
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    nt.links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    split = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(tex("occlusionRoughnessMetallic", "Non-Color").outputs["Color"], split.inputs["Color"])
    nt.links.new(split.outputs["Green"], bsdf.inputs["Roughness"])
    nt.links.new(split.outputs["Blue"], bsdf.inputs["Metallic"])
    if os.path.exists(prefix + "normal.png"):
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tex("normal", "Non-Color").outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return True


def main():
    args = script_args()
    albedo = "--albedo" in args
    fbx, tex_dir, out_dir = [a for a in args if a != "--albedo"][:3]
    with job(report_path(out_dir)) as report:
        os.makedirs(out_dir, exist_ok=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        objs = import_fbx(fbx)
        wired = sorted({m.name.split(".")[0] for o in objs for m in o.data.materials if m and wire(m, tex_dir, albedo)})
        report["wired"] = wired
        if not wired:
            raise RuntimeError(f"no material found its textures in {tex_dir}")
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 8 if albedo else 64
        scene.render.resolution_x, scene.render.resolution_y = 1200, 900
        scene.view_settings.view_transform = "Standard"
        if not albedo:
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
        corners = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
        lo = Vector([min(c[i] for c in corners) for i in range(3)])
        hi = Vector([max(c[i] for c in corners) for i in range(3)])
        size = max(hi - lo)
        mid = (lo + hi) / 2
        shots = {
            "front": (Vector((mid.x, lo.y - size * 1.4, mid.z + size * 0.15)), Vector((mid.x, mid.y, mid.z - size * 0.05))),
            "34": (Vector((mid.x + size * 0.95, lo.y - size * 1.0, hi.z + size * 0.35)), Vector((mid.x, mid.y, mid.z - size * 0.05))),
            "close": (Vector((mid.x + size * 0.15, lo.y - size * 0.35, lo.z + size * 0.32)), Vector((mid.x, lo.y + size * 0.1, lo.z + size * 0.2))),
            "top": (Vector((mid.x + size * 0.18, mid.y - size * 0.45, hi.z - size * 0.05)), Vector((mid.x, mid.y, hi.z - size * 0.28))),
            # about the campaign map camera: far, 55 degrees down
            "map": (Vector((mid.x + size * 1.1, lo.y - size * 1.1, mid.z + size * 2.2)), Vector((mid.x, mid.y, mid.z - size * 0.1))),
        }
        prefix = "albedo_" if albedo else "preview_"
        for name, (loc, target) in shots.items():
            cam.location = loc
            cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(out_dir, prefix + name + ".png")
            bpy.ops.render.render(write_still=True)
        report["views"] = [prefix + n for n in shots]


if __name__ == "__main__":  # Blender runs a -P script as __main__
    main()
