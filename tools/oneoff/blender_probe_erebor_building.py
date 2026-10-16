"""
Independent probe of an exported Erebor building FBX (Phase 5 of
docs/kitbash/erebor/blender-build-prompt.md, issue #797). It imports the
EXPORTED file, not the assembly scene, so it checks what the Kit will receive.

Checks (base tier unless stated):
  tiers      every tier object present; triangles equal the brief's numbers
  facades    horizontal rays through each wall line: a ray that passes the wall
             plane must fall inside a planned opening (<name>_expected.json from
             the assembler), and every planned opening must let rays through
  gables     rays along the ridge axis through each gable triangle must stop at
             the gable wall
  roof       down-rays over the footprint must stop on the roof, above the eave
  floor      down-rays from under the eave must reach the floor at grade
  doubled    triangles lying on another triangle's surface, same facing
             (z-fighting) and opposite facing (buried butt joints)
  grounding  lowest point of every tier at the planned depth (default: the brief's)
  bo         the collision body blocks the facade, roof and floor rays the
             visual mesh blocks
  footprint  outer size against the brief

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_probe_erebor_building.py -- --fbx <exported.fbx> ^
        --expected <name>_expected.json --brief <name>_preview.json --out <dir>
"""

import argparse
import json
import math
import os
import sys
import traceback

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

LOG = []
OUT = None


def log(m):
    LOG.append(str(m))
    print(m)
    if OUT:
        with open(os.path.join(OUT, "probe_log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG))


def geometry(obj):
    me = obj.data
    me.calc_loop_triangles()
    mw = obj.matrix_world
    verts = [mw @ v.co for v in me.vertices]
    tris = [tuple(t.vertices) for t in me.loop_triangles]
    return verts, tris


def bvh_of(obj):
    v, t = geometry(obj)
    return BVHTree.FromPolygons(v, t), v, t


def frange(a, b, step):
    n = max(1, int(round((b - a) / step)))
    return [a + (i + 0.5) * (b - a) / n for i in range(n)]


def facade_rays(bvh, wall, openings, centre, step=0.05):
    """wall = {runs_along: 'x' (a wall at y = plane) or 'y' (at x = plane), plane, u_min,
    u_max, z_min, z_max}; centre = the building centre on the other axis."""
    axis, plane = wall["runs_along"], wall["plane"]
    mine = [o for o in openings if o["runs_along"] == axis and abs(o["plane"] - plane) < 0.01]
    outward = -1.0 if plane < centre else 1.0
    stray, edge, inside, cells, through = [], [], {i: [0, 0] for i in range(len(mine))}, 0, 0
    for u in frange(wall["u_min"] + 0.35, wall["u_max"] - 0.35, step):
        for z in frange(wall["z_min"] + 0.05, wall["z_max"] - 0.05, step):
            if axis == "x":
                o = Vector((u, plane + outward * 6.0, z))
                d = Vector((0, -outward, 0))
            else:
                o = Vector((plane + outward * 6.0, u, z))
                d = Vector((-outward, 0, 0))
            hit = bvh.ray_cast(o, d, 12.0)
            cells += 1
            # passed = no hit before 0.5 m beyond the wall plane
            passed = hit[0] is None or hit[3] > 6.0 + 0.5
            planned = None
            for i, op in enumerate(mine):
                if op["u_min"] + 0.02 <= u <= op["u_max"] - 0.02 and op["z_min"] + 0.02 <= z <= op["z_max"] - 0.02:
                    planned = i
                    break
            if planned is not None:
                inside[planned][0] += 1
                inside[planned][1] += 1 if passed else 0
            elif passed:
                near = any(op["u_min"] - 0.06 <= u <= op["u_max"] + 0.06
                           and op["z_min"] - 0.06 <= z <= op["z_max"] + 0.06 for op in mine)
                (edge if near else stray).append((round(u, 2), round(z, 2)))
            through += 1 if passed else 0
    per_opening = [{"piece": mine[i]["piece"], "u": [mine[i]["u_min"], mine[i]["u_max"]],
                    "z": [mine[i]["z_min"], mine[i]["z_max"]],
                    "open_fraction": round(v[1] / v[0], 3) if v[0] else None}
                   for i, v in inside.items()]
    return {"line": f"{axis}-wall at {plane}, z {wall['z_min']} to {wall['z_max']}", "cells": cells,
            "through": through, "stray_open_cells": len(stray), "stray_sample": stray[:10],
            "edge_open_cells": len(edge), "openings": per_opening}


def chamfer_rays(bvh, ch, step=0.05):
    """Rays along the outward normal of a chamfer wall (Wall_3m_Corn_*: the wall crosses its
    3 m cell from local (0, 3) to (3, 0), pivot on the inner corner). Counts rays that pass."""
    rot = Matrix.Rotation(math.radians(ch["yaw"]), 3, "Z")
    pivot = Vector(ch["pivot"])
    n = rot @ Vector((1, 1, 0)).normalized()
    cells = through = 0
    for t in frange(0.35, 2.65, step):
        base = pivot + rot @ Vector((t, 3.0 - t, 0))
        for z in frange(ch["z"] + 0.05, ch["z"] + 2.95, step):
            o = Vector((base.x, base.y, z)) - n * 1.5
            hit = bvh.ray_cast(o, n, 4.0)
            cells += 1
            through += 1 if (hit[0] is None or hit[3] > 2.0) else 0
    return {"piece": ch["piece"], "pivot": ch["pivot"], "z": ch["z"], "window": ch["window"],
            "cells": cells, "through": through,
            "ok": (through > 0) if ch["window"] else (through == 0)}


def inside_plan(x, y, hx, hy, c, margin):
    """Inside the footprint (a rectangle with c-metre chamfered corners), margin metres in."""
    if abs(x) > hx - margin or abs(y) > hy - margin:
        return False
    if c > 0:
        dx, dy = abs(x) - (hx - c), abs(y) - (hy - c)
        if dx > 0 and dy > 0 and dx + dy > c - margin * 1.42:
            return False
    return True


def coincident(verts, tris):
    """Triangles whose centroid lies on another triangle's surface within 2 mm."""
    bvh = BVHTree.FromPolygons(verts, tris)
    same_area = opp_area = 0.0
    same_n = opp_n = 0
    planes = {}
    for idx, t in enumerate(tris):
        a, b, c = verts[t[0]], verts[t[1]], verts[t[2]]
        cr = (b - a).cross(c - a)
        area = cr.length / 2
        if area < 1e-6:
            continue
        n = cr.normalized()
        cen = (a + b + c) / 3
        for loc, nrm, j, dist in bvh.find_nearest_range(cen, 0.002):
            if j == idx or nrm is None:
                continue
            off = loc - cen
            if (off - n * off.dot(n)).length > 1e-4:  # nearest point off to the side: a neighbour
                continue
            dot = nrm.dot(n)
            if dot > 0.999:
                same_area += area
                same_n += 1
                k = max(range(3), key=lambda i: abs(n[i]))
                key = ("+" if n[k] > 0 else "-") + "XYZ"[k] + f" at {round(cen[k], 2) + 0.0:g}"
                planes[key] = planes.get(key, 0.0) + area
                break
            if dot < -0.999:
                opp_area += area
                opp_n += 1
                break
    top = sorted(planes.items(), key=lambda kv: -kv[1])[:12]
    return {"zfight_tris": same_n, "zfight_m2": round(same_area, 3),
            "zfight_by_plane": {k: round(v, 3) for k, v in top},
            "butt_tris": opp_n, "butt_m2": round(opp_area, 3)}


def main():
    global OUT
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--fbx", required=True)
    ap.add_argument("--expected", required=True)
    ap.add_argument("--brief", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--ground", type=float, default=None,
                    help="planned lowest z; default the brief's bounds_min z")
    ap.add_argument("--eave", type=float, default=3.0)
    ap.add_argument("--ridge-axis", default="x")
    ap.add_argument("--name", default=None,
                    help="building to probe in a multi-building FBX; default the file name")
    args = ap.parse_args(argv)
    OUT = args.out
    os.makedirs(OUT, exist_ok=True)
    name = args.name or os.path.splitext(os.path.basename(args.fbx))[0]
    done = os.path.join(OUT, "probe_DONE.txt")
    if os.path.exists(done):
        os.remove(done)
    status, res = "ok", {"fbx": args.fbx}
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        try:
            bpy.ops.wm.fbx_import(filepath=args.fbx)
        except Exception:
            bpy.ops.import_scene.fbx(filepath=args.fbx)
        objs = {o.name: o for o in bpy.context.scene.objects if o.type == "MESH"}
        brief = json.load(open(args.brief, encoding="utf-8"))
        expected = json.load(open(args.expected, encoding="utf-8"))
        want = {name: brief["tris"]["base"], f"{name}.lod3": brief["tris"]["lod3"],
                f"{name}.lod6": brief["tris"]["lod6"], f"bo_{name}": brief["bo_tris"]}
        tiers = {}
        for oname, t in want.items():
            o = objs.get(oname)
            if o is None:
                tiers[oname] = {"present": False}
                continue
            v, tr = geometry(o)
            tiers[oname] = {"present": True, "tris": len(tr), "brief": t, "match": len(tr) == t,
                            "z_min": round(min(p.z for p in v), 3), "z_max": round(max(p.z for p in v), 3),
                            "materials": [m.name for m in o.data.materials if m]}
        res["tiers"] = tiers
        res["grounding"] = {k: v.get("z_min") for k, v in tiers.items()}
        ground = args.ground if args.ground is not None else brief["bounds_min"][2]
        res["grounding_planned"] = ground
        # every tier reaches the planned lowest point; a LOD may sit up to 5 cm higher
        res["grounding_ok"] = all(ground - 0.01 <= v <= ground + 0.05
                                  for v in res["grounding"].values() if v is not None)

        base = objs[name]
        bvh, verts, tris = bvh_of(base)
        bo_bvh = bvh_of(objs[f"bo_{name}"])[0] if f"bo_{name}" in objs else None
        mn = Vector(map(min, *verts))
        mx = Vector(map(max, *verts))
        res["size"] = [round(v, 3) for v in (mx - mn)]
        res["footprint_ok"] = all(abs(a - b) <= 0.01 for a, b in zip(res["size"][:2], brief["size"][:2]))

        # layout from the assembler; a bare list is the older openings-only file
        layout = expected if isinstance(expected, dict) else {"openings": expected}
        openings = layout["openings"]
        eave = layout.get("eave", args.eave)
        roof_kind = layout.get("roof", "gable")
        ridge_axis = layout.get("ridge_axis", args.ridge_axis)
        chamfer = layout.get("chamfer", 0.0)
        if "half" in layout:
            hx, hy = layout["half"]
        else:
            px = [o["plane"] for o in openings if o["runs_along"] == "y"]
            py = [o["plane"] for o in openings if o["runs_along"] == "x"]
            hx = max(abs(v) for v in px) if px else max(abs(mn.x), abs(mx.x))
            hy = max(abs(v) for v in py) if py else max(abs(mn.y), abs(mx.y))
        walls = layout.get("walls") or (
            [{"runs_along": "x", "plane": p, "u_min": -hx, "u_max": hx, "z_min": 0.0, "z_max": eave}
             for p in (-hy, hy)]
            + [{"runs_along": "y", "plane": p, "u_min": -hy, "u_max": hy, "z_min": 0.0, "z_max": eave}
               for p in (-hx, hx)])
        res["layout"] = {"eave": eave, "roof": roof_kind, "chamfer": chamfer, "half": [hx, hy],
                         "walls": len(walls), "chamfer_walls": len(layout.get("chamfers", []))}
        res["facades"] = [facade_rays(bvh, w, openings, 0.0) for w in walls]
        if bo_bvh is not None:
            res["bo_facades"] = [facade_rays(bo_bvh, w, openings, 0.0, step=0.1) for w in walls]
        res["chamfers"] = [chamfer_rays(bvh, ch) for ch in layout.get("chamfers", [])]

        # gables: rays along the ridge axis through the triangle above the eave
        gable = {"rays": 0, "open": 0, "sample": []}
        if roof_kind == "gable":
            half = hy if ridge_axis == "x" else hx
            # a layout that states its ridge height (the Gondor assembler) sets the pitch, so a
            # 45 degree gable is probed up to its apex, not only to the 26.57 degree line
            pitch = (layout["ridge_z"] - eave) / half if "ridge_z" in layout else None
            gable["pitch"] = pitch
            for end, sign in ((-hx, -1), (hx, 1)) if ridge_axis == "x" else ((-hy, -1), (hy, 1)):
                for w in frange(-half + 0.2, half - 0.2, 0.1):
                    top = eave + 1.5 * (1 - abs(w) / half) - 0.12
                    if pitch is not None:
                        top = eave + pitch * (half - abs(w)) - 0.12
                    for z in frange(eave + 0.05, max(eave + 0.06, top), 0.1):
                        if z > top:
                            continue
                        if ridge_axis == "x":
                            o, d = Vector((end + sign * 6, w, z)), Vector((-sign, 0, 0))
                        else:
                            o, d = Vector((w, end + sign * 6, z)), Vector((0, -sign, 0))
                        hit = bvh.ray_cast(o, d, 12.0)
                        gable["rays"] += 1
                        if hit[0] is None or hit[3] > 6.0 + 0.5:
                            gable["open"] += 1
                            gable["sample"].append((round(w, 2), round(z, 2)))
        gable["sample"] = gable["sample"][:10]
        res["gables"] = gable

        # roof and floor, inside the footprint only
        roof = {"rays": 0, "below_eave": 0, "bo_miss": 0}
        floor = {"rays": 0, "miss": 0, "z": []}
        for x in frange(-hx + 0.1, hx - 0.1, 0.25):
            for y in frange(-hy + 0.1, hy - 0.1, 0.25):
                if not inside_plan(x, y, hx, hy, chamfer, 0.1):
                    continue
                hit = bvh.ray_cast(Vector((x, y, 40)), Vector((0, 0, -1)), 60)
                roof["rays"] += 1
                if hit[0] is None or hit[0].z < eave - 0.05:
                    roof["below_eave"] += 1
                if bo_bvh is not None:
                    bh = bo_bvh.ray_cast(Vector((x, y, 40)), Vector((0, 0, -1)), 60)
                    if bh[0] is None or bh[0].z < eave - 0.05:
                        roof["bo_miss"] += 1
                if inside_plan(x, y, hx, hy, chamfer, 0.7):
                    fh = bvh.ray_cast(Vector((x, y, eave - 0.2)), Vector((0, 0, -1)), eave + 4)
                    floor["rays"] += 1
                    if fh[0] is None or fh[0].z < -0.05:
                        floor["miss"] += 1
                    else:
                        floor["z"].append(round(fh[0].z, 2))
        if floor["z"]:
            zs = floor["z"]
            floor["z"] = {"min": min(zs), "max": max(zs)}
        res["roof"] = roof
        res["floor"] = floor
        res["doubled"] = coincident(verts, tris)
    except Exception:
        log(traceback.format_exc())
        status = "failed"
    with open(os.path.join(OUT, f"{name}_probe.json"), "w", encoding="utf-8") as f:
        json.dump(res, f, indent=1)
    with open(done, "w", encoding="utf-8") as f:
        json.dump({"status": status}, f)
    log(json.dumps(res, indent=1))
    log(f"[done] {status}")


main()
