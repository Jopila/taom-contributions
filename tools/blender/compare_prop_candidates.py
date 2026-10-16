"""Open several prepped prop candidates side by side in the Blender UI, each
with its own baked maps (basecolor x AO, roughness, metallic, normal where
present), a floating label, Material Preview shading. bo_ meshes are dropped.
Saves <first staging>/../<name>_compare.blend so it can be reopened.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -P tools/blender/compare_prop_candidates.py -- \\
        <name> <label>=<staging dir> [<label>=<staging dir> ...]

<name> is the --name given to blender_prep_tripo_prop.py (the FBX is
<staging>/<name>.fbx). GUI mode: no -b.
"""
import os
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
NAME = argv[0]
CANDIDATES = [a.split("=", 1) for a in argv[1:]]
SPACING = 80.0


def import_fbx(path):
    before = set(bpy.data.objects)
    try:
        bpy.ops.wm.fbx_import(filepath=path)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def build_material(name, staging):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]

    def tex(map_name, space):
        path = os.path.join(staging, f"{map_name}.png")
        if not os.path.exists(path):
            return None
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(path)
        n.image.colorspace_settings.name = space
        return n

    base, ao = tex("basecolor", "sRGB"), tex("ao", "Non-Color")
    if base and ao:
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 0.7
        nt.links.new(base.outputs["Color"], mix.inputs[6])
        nt.links.new(ao.outputs["Color"], mix.inputs[7])
        nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    elif base:
        nt.links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    rough = tex("roughness", "Non-Color")
    if rough:
        nt.links.new(rough.outputs["Color"], bsdf.inputs["Roughness"])
    else:
        bsdf.inputs["Roughness"].default_value = 0.8
    metal = tex("metallic", "Non-Color")
    if metal:
        nt.links.new(metal.outputs["Color"], bsdf.inputs["Metallic"])
    normal = tex("normal", "Non-Color")
    if normal:
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(normal.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def setup():
    # a factory reset inside a GUI startup script would replace the window
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for i, (label, staging) in enumerate(CANDIDATES):
        x = (i - (len(CANDIDATES) - 1) / 2.0) * SPACING
        top = 0.0
        for o in import_fbx(os.path.join(staging, f"{NAME}.fbx")):
            if o.type != "MESH":
                continue
            if o.name.startswith("bo_"):
                bpy.data.objects.remove(o, do_unlink=True)
                continue
            o.name = label
            o.location.x += x
            o.data.materials.clear()
            o.data.materials.append(build_material(f"mat_{label}", staging))
            top = max(top, o.dimensions.z)
        curve = bpy.data.curves.new(f"label_{label}", type="FONT")
        curve.body = label.replace("_", " ")
        curve.size = 6.0
        curve.align_x = "CENTER"
        txt = bpy.data.objects.new(f"label_{label}", curve)
        txt.location = (x, 0.0, top + 8.0)
        txt.rotation_euler = (1.5708, 0.0, 0.0)
        bpy.context.scene.collection.objects.link(txt)
    out = os.path.join(os.path.dirname(os.path.normpath(CANDIDATES[0][1])), f"{NAME}_compare.blend")
    bpy.ops.wm.save_as_mainfile(filepath=out)


def shade_and_frame():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != "VIEW_3D":
                continue
            area.spaces.active.shading.type = "MATERIAL"
            area.spaces.active.clip_end = 2000.0
            region = next(r for r in area.regions if r.type == "WINDOW")
            with bpy.context.temp_override(window=window, area=area, region=region):
                bpy.ops.view3d.view_all()
    return None


setup()
bpy.app.timers.register(shade_and_frame, first_interval=1.0)
