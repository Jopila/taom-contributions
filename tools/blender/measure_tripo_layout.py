"""Measure a Tripo model's layout for a rebuild from solids: a top-down height map, a radial
height profile round the model's centre, tower peaks, and orthographic renders. The numbers go
into a builder's layout table (tools/oneoff/blender_build_carn_dum_icon.py's LAYOUT came from
this on 2026-10-16; docs/kitbash/map-icons.md "Rebuilding an icon from solids", step 1).

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/blender/measure_tripo_layout.py --
        --src <scaled Tripo FBX, e.g. the prep script's _high.fbx> --out <dir> [--step 0.25] [--peak-height 6]

--step is the height-map cell (metres); a peak is a cell that is the highest in a 3 m window
and stands --peak-height above that window's median (towers and spires).

Writes <out>/layout.json (bbox, centre, profile: per radius the median, p90 and max height and
the share of directions that hit; peaks: x, y, z, radius and angle from the centre),
heightmap.npy, heightmap.png, ortho_top/front/side.png and <out>/_report/DONE.txt
(tools/blender/blender_job.py).
"""
import argparse
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blender_job import flag_value, import_fbx, job, report_path, script_args  # noqa: E402


def parse_args(argv):
    p = argparse.ArgumentParser(allow_abbrev=False)
    p.add_argument("--src", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--step", type=float, default=0.25)
    p.add_argument("--peak-height", type=float, default=6.0)
    return p.parse_args(argv)


def measure(a, report):
    os.makedirs(a.out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = import_fbx(a.src)
    if not objs:
        raise RuntimeError(f"no mesh in {a.src}")
    bpy.context.view_layer.update()
    corners = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector([min(c[i] for c in corners) for i in range(3)])
    hi = Vector([max(c[i] for c in corners) for i in range(3)])
    depsgraph = bpy.context.evaluated_depsgraph_get()
    scene = bpy.context.scene
    top_z = hi.z + 5.0

    def height_at(x, y):
        hit, loc, *_ = scene.ray_cast(depsgraph, Vector((x, y, top_z)), Vector((0, 0, -1)))
        return loc.z if hit else None

    xs = np.arange(lo.x, hi.x, a.step)
    ys = np.arange(lo.y, hi.y, a.step)
    hm = np.full((len(ys), len(xs)), np.nan, dtype=np.float32)
    for j, y in enumerate(ys):
        for i, x in enumerate(xs):
            z = height_at(float(x), float(y))
            if z is not None:
                hm[j, i] = z
    np.save(os.path.join(a.out, "heightmap.npy"), hm)

    cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
    prof = []
    for r in np.arange(0.0, max(hi.x - lo.x, hi.y - lo.y) / 2, a.step):
        zs = [height_at(cx + r * math.cos(t), cy + r * math.sin(t)) for t in np.radians(np.arange(0, 360, 2))]
        zs = [z for z in zs if z is not None]
        if zs:
            prof.append({"r": round(float(r), 2), "median": round(float(np.median(zs)), 2),
                         "p90": round(float(np.percentile(zs, 90)), 2), "max": round(float(max(zs)), 2),
                         "cover": round(len(zs) / 180.0, 2)})

    peaks = []
    win = int(3.0 / a.step)
    H = np.nan_to_num(hm, nan=lo.z)
    for j in range(win, H.shape[0] - win):
        for i in range(win, H.shape[1] - win):
            z = H[j, i]
            block = H[j - win:j + win + 1, i - win:i + win + 1]
            if z >= block.max() and z - np.median(block) > a.peak_height:
                peaks.append({"x": round(float(xs[i]), 2), "y": round(float(ys[j]), 2), "z": round(float(z), 2),
                              "r_from_centre": round(math.hypot(xs[i] - cx, ys[j] - cy), 2),
                              "angle_deg": round(math.degrees(math.atan2(ys[j] - cy, xs[i] - cx)), 1)})
    with open(os.path.join(a.out, "layout.json"), "w", encoding="utf-8") as f:
        json.dump({"bbox_lo": list(lo), "bbox_hi": list(hi), "centre": [cx, cy], "profile": prof, "peaks": peaks},
                  f, indent=1)
    report.update({"cells": int(hm.size), "profile_rings": len(prof), "peaks": len(peaks),
                   "bbox_m": [round(v, 2) for v in hi - lo]})

    norm = (H - lo.z) / max(hi.z - lo.z, 1e-6)  # top of the image is +Y
    img = bpy.data.images.new("heightmap", width=H.shape[1], height=H.shape[0])
    img.pixels.foreach_set(np.dstack([norm, norm, norm, np.ones_like(norm)]).astype(np.float32).ravel())
    img.filepath_raw = os.path.join(a.out, "heightmap.png")
    img.file_format = "PNG"
    img.save()

    mat = bpy.data.materials.new("clay")
    mat.use_nodes = True
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    scene.world = world
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", type="SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(40), 0.0, math.radians(-30))
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.type = "ORTHO"
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.ortho_scale = max(hi - lo) * 1.05
    centre = (lo + hi) / 2
    views = {"ortho_top": ((centre.x, centre.y, hi.z + 50), (0, 0, 0)),
             "ortho_front": ((centre.x, lo.y - 100, centre.z), (math.radians(90), 0, 0)),
             "ortho_side": ((hi.x + 100, centre.y, centre.z), (math.radians(90), 0, math.radians(90)))}
    scene.render.resolution_x = scene.render.resolution_y = 1200
    for name, (loc, rot) in views.items():
        cam.location = loc
        cam.rotation_euler = rot
        scene.render.filepath = os.path.join(a.out, name + ".png")
        bpy.ops.render.render(write_still=True)


def main():
    args = script_args()
    with job(report_path(flag_value(args, "--out") or ".")) as report:
        measure(parse_args(args), report)


if __name__ == "__main__":  # Blender runs a -P script as __main__
    main()
