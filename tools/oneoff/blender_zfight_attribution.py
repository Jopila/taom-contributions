"""
Which materials share each z-fighting plane of an exported building, and where. The probe's
`doubled` check reports area per plane only; this names the two materials of every same-facing
coincident triangle pair within 2 mm and the bounds of those triangles, so each plane can be judged
authored (one part's sub-objects), a join, or hidden.

Classification differs from the probe's `coincident()` on purpose: the probe stops at the first
neighbour of either facing, so a triangle sandwiched between an opposite-facing and a same-facing
surface can count as a butt joint there; this tool skips opposite-facing neighbours and counts any
same-facing one. Its plane areas are therefore equal to or larger than the probe's. Quote one tool
per number (docs/kitbash/gondor-buildings.md labels them).

    blender-launcher.exe -b -P tools/oneoff/blender_zfight_attribution.py -- --fbx <file> --name <object> --out <dir>

Each coincident triangle is also tested for visibility from outside: a ray from it along its normal
that leaves the building makes it `exposed`; a downward face at grade is `below_grade` (the terrain
hides it); the rest is `covered` (inside a trim or column, under a roof, or facing the interior).

Writes <out>/<name>_zfight_materials.json (every plane, largest first), <name>_zfight_log.txt and
<name>_zfight_DONE.txt (removed before the run, so a poller never reads an older result).
"""

import argparse
import json
import os
import sys
import traceback

import bpy
from mathutils.bvhtree import BVHTree

LOG = []


def log(m):
    LOG.append(str(m))
    print(m)


def main(a):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=a.fbx)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=a.fbx)
    o = bpy.data.objects[a.name]
    me = o.data
    me.calc_loop_triangles()
    mw = o.matrix_world
    verts = [mw @ v.co for v in me.vertices]
    tris = [tuple(t.vertices) for t in me.loop_triangles]
    mats = [me.materials[t.material_index].name if me.materials else "" for t in me.loop_triangles]
    log(f"[zfight] {a.name}: {len(tris)} tris, {len(me.materials)} materials")
    bvh = BVHTree.FromPolygons(verts, tris)
    planes = {}
    for idx, t in enumerate(tris):
        p0, p1, p2 = verts[t[0]], verts[t[1]], verts[t[2]]
        cr = (p1 - p0).cross(p2 - p0)
        area = cr.length / 2
        if area < 1e-6:
            continue
        n = cr.normalized()
        cen = (p0 + p1 + p2) / 3
        for loc, nrm, j, _ in bvh.find_nearest_range(cen, 0.002):
            if j == idx or nrm is None:
                continue
            off = loc - cen
            if (off - n * off.dot(n)).length > 1e-4 or nrm.dot(n) <= 0.999:
                continue
            k = max(range(3), key=lambda i: abs(n[i]))
            key = ("+" if n[k] > 0 else "-") + "XYZ"[k] + f" at {round(cen[k], 2) + 0.0:g}"
            pair = " | ".join(sorted((mats[idx], mats[j])))
            d = planes.setdefault(key, {"m2": 0.0, "pairs": {}, "min": list(cen), "max": list(cen),
                                        "exposed": 0.0, "below_grade": 0.0})
            d["m2"] += area
            # visible from outside? a ray along the normal that leaves the building: exposed; one
            # that hits other geometry: covered (inside a trim or column, under a roof, or interior)
            if n.z < -0.999 and cen.z < 0.02:
                d["below_grade"] += area
            elif bvh.ray_cast(cen + n * 0.004, n, 100.0)[0] is None:
                d["exposed"] += area
            d["pairs"][pair] = d["pairs"].get(pair, 0.0) + area
            d["min"] = [min(d["min"][i], cen[i]) for i in range(3)]
            d["max"] = [max(d["max"][i], cen[i]) for i in range(3)]
            break
    out = {k: {"m2": round(v["m2"], 3), "exposed_m2": round(v["exposed"], 3),
               "below_grade_m2": round(v["below_grade"], 3),
               "pairs": {p: round(x, 3) for p, x in sorted(v["pairs"].items(), key=lambda kv: -kv[1])},
               "centroid_min": [round(x, 2) for x in v["min"]], "centroid_max": [round(x, 2) for x in v["max"]]}
           for k, v in sorted(planes.items(), key=lambda kv: -kv[1]["m2"])}
    total = round(sum(v["m2"] for v in planes.values()), 3)
    exposed = round(sum(v["exposed"] for v in planes.values()), 3)
    below = round(sum(v["below_grade"] for v in planes.values()), 3)
    with open(os.path.join(a.out, f"{a.name}_zfight_materials.json"), "w", encoding="utf-8") as f:
        json.dump({"name": a.name, "total_m2": total, "exposed_m2": exposed, "below_grade_m2": below,
                   "covered_m2": round(total - exposed - below, 3), "planes": out}, f, indent=1)
    log(f"[zfight] {a.name}: {len(out)} planes, {total} m2: exposed {exposed}, below grade {below}")


def run():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    # clear the old marker before argparse can reject anything (lessons: "Clear a detached tool's
    # DONE marker before parsing arguments"); callers also delete it before launch
    raw = dict(zip(argv, argv[1:]))
    if raw.get("--out") and raw.get("--name"):
        stale = os.path.join(raw["--out"], f"{raw['--name']}_zfight_DONE.txt")
        if os.path.exists(stale):
            os.remove(stale)
    ap = argparse.ArgumentParser()
    ap.add_argument("--fbx", required=True)
    ap.add_argument("--name", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args(argv)
    os.makedirs(a.out, exist_ok=True)
    done = os.path.join(a.out, f"{a.name}_zfight_DONE.txt")
    status = "ok"
    try:
        main(a)
    except Exception:
        log(traceback.format_exc())
        status = "failed"
    with open(os.path.join(a.out, f"{a.name}_zfight_log.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(LOG))
    with open(done, "w", encoding="utf-8") as f:
        json.dump({"status": status}, f)


if __name__ == "__main__":
    run()
