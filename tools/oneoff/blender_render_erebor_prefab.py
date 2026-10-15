"""
Rebuild authored TAOM_Map prefabs from the Erebor kit FBX sources in headless
Blender and render them. Used to test how the engine frame relates to the
Blender FBX-import frame (Phase 2 of docs/kitbash/erebor/blender-build-prompt.md)
and to show Mike what the authored buildings are.

--mode conj      piece frame = C @ engine frame @ C, C = Rz(180): the hypothesis
                 that the engine sees every source FBX rotated 180 deg about Z
                 relative to Blender's import
--mode identity  the engine frame used as-is
--mode both      render both, side by side files

Only kit pieces (sm_dw_*, kys_dw_*, dw_pillar_*, sd_dw_*) are drawn; other
meshes in the prefab are counted and skipped.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_render_erebor_prefab.py -- ^
        --file taom_erebor_kitbash.xml --entities "dwarf_house_a;dwarf_house_b" ^
        --mode both --out E:\\LOTRAOMAssets\\_export\\erebor\\prefab_renders
"""

import argparse
import colorsys
import hashlib
import json
import math
import os
import re
import sys
import traceback
import xml.etree.ElementTree as ET

import bpy
from mathutils import Matrix, Vector

SRC = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\erebor"
PREFABS = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs"
KIT_RE = re.compile(r"^(sm_dw_|kys_dw_|dw_pillar_|sd_dw_)", re.IGNORECASE)
LOD_RE = re.compile(r"[._]lod\d*$", re.IGNORECASE)
C = Matrix.Rotation(math.pi, 4, "Z")

LOG = []
OUT = None


def log(msg):
    LOG.append(str(msg))
    print(msg)
    if OUT:
        with open(os.path.join(OUT, "log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG))


def mat_color(name):
    h = int(hashlib.md5(name.encode()).hexdigest()[:6], 16) / 0xFFFFFF
    r, g, b = colorsys.hls_to_rgb(h, 0.62, 0.45)
    return (r, g, b, 1.0)


def load_kit():
    """Import every kit FBX; return {lower piece name: world-space mesh}."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    meshes = {}
    for folder in ("meshes", "walls", "statues"):
        d = os.path.join(SRC, folder)
        for fn in sorted(os.listdir(d)):
            if not fn.lower().endswith(".fbx"):
                continue
            before = set(bpy.data.objects)
            try:
                bpy.ops.wm.fbx_import(filepath=os.path.join(d, fn))
            except Exception:
                bpy.ops.import_scene.fbx(filepath=os.path.join(d, fn))
            new = [o for o in bpy.data.objects if o not in before]
            for o in new:
                if o.type != "MESH" or o.name.lower().startswith("bo_") or LOD_RE.search(o.name):
                    continue
                me = o.data.copy()
                me.transform(o.matrix_world)
                me.name = "k_" + o.name
                meshes[o.name.lower()] = me
            for o in new:
                bpy.data.objects.remove(o, do_unlink=True)
    for m in bpy.data.materials:
        m.diffuse_color = mat_color(m.name)
    return meshes


def euler_matrix(e):
    # engine ApplyEulerAngles: RotateAboutUp(z), RotateAboutSide(x), RotateAboutForward(y)
    return (Matrix.Rotation(e[2], 4, "Z") @ Matrix.Rotation(e[0], 4, "X")
            @ Matrix.Rotation(e[1], 4, "Y"))


def vec(s, d):
    return [float(x) for x in s.split(",")] if s else list(d)


def walk(el, parent, out, skipped):
    tr = el.find("transform")
    m = Matrix.Identity(4)
    if tr is not None:
        pos = vec(tr.get("position"), (0, 0, 0))
        eul = vec(tr.get("rotation_euler"), (0, 0, 0))
        scl = vec(tr.get("scale"), (1, 1, 1))
        m = (Matrix.Translation(Vector(pos)) @ euler_matrix(eul)
             @ Matrix.Diagonal(Vector((scl[0], scl[1], scl[2], 1.0))))
    world = parent @ m
    comps = el.find("components")
    if comps is not None:
        for mm in comps.findall("meta_mesh_component"):
            name = (mm.get("name") or "").lower()
            if KIT_RE.match(name):
                out.append((name, world.copy()))
            elif name:
                skipped[name] = skipped.get(name, 0) + 1
    ch = el.find("children")
    if ch is not None:
        for c in ch.findall("game_entity"):
            walk(c, world, out, skipped)


def find_entity(root, name):
    for el in root.iter("game_entity"):
        if el.get("name") == name:
            return el
    return None


def setup_scene():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_cavity = True
    sh.show_shadows = True
    sh.show_object_outline = True
    sh.background_type = "VIEWPORT"
    sh.background_color = (0.93, 0.93, 0.93)
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1000
    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass
    cd = bpy.data.cameras.new("cam")
    cd.type = "ORTHO"
    cd.clip_end = 100000.0
    cam = bpy.data.objects.new("cam", cd)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return scene, cam


def render(objs, cam, path, view):
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            mn = Vector(map(min, mn, w))
            mx = Vector(map(max, mx, w))
    centre = (mn + mx) / 2
    span = max((mx - mn).length, 1.0)
    if view == "top":
        cam.location = centre + Vector((0, 0, span * 2))
        cam.rotation_euler = (0, 0, 0)
        cam.data.ortho_scale = max(mx.x - mn.x, (mx.y - mn.y) * 1.4) * 1.1
    else:
        sx, sy = {"q1": (-1, -1), "q2": (1, 1)}[view]
        d = Vector((sx * 0.62, sy * 0.62, 0.48)).normalized()
        cam.location = centre + d * span * 2
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.ortho_scale = span * 1.05
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return [round(v, 2) for v in (mx - mn)]


def main():
    global OUT
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--file", required=True)
    ap.add_argument("--entities", required=True)
    ap.add_argument("--mode", default="both", choices=["conj", "identity", "both"])
    ap.add_argument("--out", required=True)
    args = ap.parse_args(argv)
    OUT = args.out
    os.makedirs(OUT, exist_ok=True)
    done = os.path.join(OUT, "DONE.txt")
    if os.path.exists(done):
        os.remove(done)
    status = "ok"
    summary = []
    try:
        meshes = load_kit()
        log(f"[kit] {len(meshes)} pieces loaded")
        scene, cam = setup_scene()
        root = ET.parse(os.path.join(PREFABS, args.file)).getroot()
        modes = ["conj", "identity"] if args.mode == "both" else [args.mode]
        for ent_name in [e for e in args.entities.split(";") if e]:
            ent = find_entity(root, ent_name)
            if ent is None:
                log(f"[miss] {ent_name}")
                status = "partial"
                continue
            placements, skipped = [], {}
            walk(ent, Matrix.Identity(4), placements, skipped)
            missing = sorted({n for n, _ in placements if n not in meshes})
            for mode in modes:
                objs = []
                for name, m in placements:
                    me = meshes.get(name)
                    if me is None:
                        continue
                    o = bpy.data.objects.new(name, me)
                    o.matrix_world = (C @ m @ C) if mode == "conj" else m
                    scene.collection.objects.link(o)
                    objs.append(o)
                if not objs:
                    continue
                safe = re.sub(r"[^A-Za-z0-9_]+", "_", ent_name)
                dims = None
                for view in ("q1", "q2", "top"):
                    dims = render(objs, cam, os.path.join(OUT, f"{safe}_{mode}_{view}.png"), view)
                for o in objs:
                    bpy.data.objects.remove(o, do_unlink=True)
                summary.append({"entity": ent_name, "mode": mode, "pieces": len(objs),
                                "dims": dims, "missing_meshes": missing,
                                "skipped_non_kit": sum(skipped.values())})
                log(f"[render] {ent_name} {mode}: {len(objs)} pieces, dims {dims}, "
                    f"non-kit skipped {sum(skipped.values())}, missing {missing}")
    except Exception:
        log(traceback.format_exc())
        status = "failed"
    with open(os.path.join(OUT, "summary.json"), "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    with open(done, "w", encoding="utf-8") as f:
        json.dump({"status": status}, f)
    log(f"[done] {status}")


main()
