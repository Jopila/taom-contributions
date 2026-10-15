"""
Test the Erebor kit join rules (Phase 2 of
docs/kitbash/erebor/blender-build-prompt.md) by placing each claimed join in
headless Blender and measuring it.

Every join is written in the BLENDER IMPORT FRAME. The engine sees each kit
FBX rotated 180 deg about Z relative to that frame, so an offset mined from a
prefab converts as (x, y, z) -> (-x, -y, z) with yaw unchanged (verified by
rebuilding dwarf_house_a/b, dwarf_barracks: blender_render_erebor_prefab.py).

Per join:
    zfight_m2       area where two different pieces share a surface within 3 mm
                    with normals pointing the same way (visible z-fighting)
    butt_m2         the same with opposite normals (back-to-back butt joint,
                    hidden, only wasted triangles)
    intersect_pairs triangle pairs of different pieces that intersect
                    (designed overlaps such as butted wall ends show here)
    size            bbox of the assembly
    render          <out>/<join>.png

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_test_erebor_joins.py -- --out E:\\LOTRAOMAssets\\_export\\erebor\\joins
"""

import argparse
import colorsys
import hashlib
import json
import math
import os
import random
import re
import sys
import traceback

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

SRC = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\erebor"
LOD_RE = re.compile(r"[._]lod\d*$", re.IGNORECASE)

# name: (description, evidence, [(piece, (x, y, z), yaw_deg), ...])
JOINS = {
    "wall_run": ("plain walls continue every 3 m along X", "dwarf_house_a",
                 [("sm_dw_wall_3m_c", (0, 0, 0), 0), ("sm_dw_wall_3m_c", (3, 0, 0), 0)]),
    "wall_corner_90": ("90 deg corner: ends meet at the corner point, column on the corner",
                       "dwarf_house_a",
                       [("sm_dw_wall_3m_c", (0, 0, 0), 0), ("sm_dw_wall_3m_c", (1.5, 1.5, 0), 90),
                        ("sm_dw_wall_clmn_3m_c", (1.5, 0, 0), 0),
                        ("sm_dw_wall_clmn_top_d", (1.5, 0, 3), 0)]),
    "wall_chamfer": ("chamfered corner: Corn pivot on the inner corner of the 3 m cell",
                     "dwarf_house_b",
                     [("sm_dw_wall_3m_a", (0, 0, 0), 0), ("sm_dw_wall_3m_corn_a", (1.5, 3, 0), 270),
                      ("sm_dw_wall_3m_a", (4.5, 4.5, 0), 90),
                      ("sm_dw_ground_3m_a1_corner", (3, 1.5, 0), 0)]),
    "wall_stack": ("second storey wall sits on the first at z 3", "dwarf_barracks",
                   [("sm_dw_wall_3m_a", (0, 0, 0), 0), ("sm_dw_wall_3m_a", (0, 0, 3), 0)]),
    "wall_beams": ("base beam at z 0, eave beam at z 3, both centred on the wall",
                   "dwarf_house_a",
                   [("sm_dw_wall_3m_c", (0, 0, 0), 0), ("sm_dw_wall_beam_3m_b", (0, 0, 0), 0),
                    ("sm_dw_wall_beam_3m_c", (0, 0, 3), 0)]),
    "wall_pilaster": ("pilaster: Clmn_3m_A x1.5 on a wall seam with Clmn_Top_F base and cap",
                      "dwarf_house_a",
                      [("sm_dw_wall_3m_door_c1", (0, 0, 0), 0), ("sm_dw_wall_3m_c", (3, 0, 0), 0),
                       ("sm_dw_wall_clmn_3m_a", (1.5, 0, 0), 0, (1.5, 1.5, 1.0)),
                       ("sm_dw_wall_clmn_top_f", (1.5, 0, 0), 0, (1.5, 1.5, 1.2)),
                       ("sm_dw_wall_clmn_top_f", (1.5, 0, 3), 0, (1.5, 1.5, 1.2))]),
    "eave_on_wall": ("roof panel eave pivot on the wall centreline at wall top", "dwarf_house_a",
                     [("sm_dw_wall_3m_c", (0, 0, 0), 0), ("sm_dw_roof_str_b1", (0, 0, 3), 0)]),
    "roof_row": ("roof panels continue every 3 m along the eave", "dwarf_house_a",
                 [("sm_dw_roof_str_b1", (0, 0, 3), 0), ("sm_dw_roof_str_b1", (3, 0, 3), 0)]),
    "gable_6m": ("6 m gable: two panels meet at the ridge, Side_A1 wedges close the end",
                 "dwarf_house_a",
                 [("sm_dw_roof_str_b1", (0, 0, 3), 0), ("sm_dw_roof_str_b1", (0, 6, 3), 180),
                  ("sm_dw_roof_side_str_a1", (0, 3, 4.5), 90),
                  ("sm_dw_wall_3m_c", (-1.5, 1.5, 0), 90), ("sm_dw_wall_3m_c", (-1.5, 4.5, 0), 90),
                  ("sm_dw_roof_side_a1", (-1.5, 1.5, 3), 0), ("sm_dw_roof_side_a1", (-1.5, 4.5, 3), 180)]),
    "hip_flat_deck": ("hip roof: panels rise 1.5 m to a flat Top deck framed by Side_Str",
                      "dwarf_house_b",
                      [("sm_dw_roof_str_a1", (0, 0, 3), 0), ("sm_dw_roof_top_a1", (0, 4.5, 4.5), 0),
                       ("sm_dw_roof_side_str_a1", (0, 3, 4.5), 90),
                       ("sm_dw_roof_side_cor_out_a1", (1.5, 0, 3), 0)]),
    "chamfer_roof": ("roof over a chamfered corner: Cor_Tri on the Corn pivot, yaw +90",
                     "dwarf_house_b",
                     [("sm_dw_wall_3m_corn_a", (1.5, 3, 0), 270), ("sm_dw_roof_cor_tri_a1", (1.5, 3, 3), 0),
                      ("sm_dw_roof_str_a1", (0, 0, 3), 0), ("sm_dw_roof_str_a1", (4.5, 4.5, 3), 90)]),
    "hip_corner_1_5m": ("1.5 m roof: Cor_Out_B1 beside the last Str_A2 panel",
                        "Soisson/shogun housing (1692 pairs)",
                        [("sm_dw_roof_str_a2", (0, 0, 0), 0), ("sm_dw_roof_cor_out_b1", (1.5, 0, 0), 0),
                         ("sm_dw_roof_str_a2", (1.5, 3.0, 0), 90)]),
    "floor_tiles": ("3 m floor tiles with a trim strip on the seam", "dwarf_house_b",
                    [("sm_dw_ground_3m_a2", (0, 0, 0), 0), ("sm_dw_ground_3m_a2", (3, 0, 0), 0),
                     ("sm_dw_ground_trim_3m_a1", (1.5, 0, 0), 0)]),
    "plinth_6m": ("Ground_6m plinth under a wall line", "dwarf_house_a",
                  [("sm_dw_ground_6m_a1", (0, 0, 0), 0), ("sm_dw_ground_6m_a1", (6, 0, 0), 0),
                   ("sm_dw_wall_3m_c", (1.5, -3, 0), 0)]),
    "castle_run": ("castle straight walls continue every 20 m", "module length 20 m",
                   [("sm_dw_castle_wall_a1_str", (0, 0, 0), 0), ("sm_dw_castle_wall_a1_str", (20, 0, 0), 0)]),
    "castle_gate": ("gate section in a straight run", "module length 20 m",
                    [("sm_dw_castle_wall_a1_str", (0, 0, 0), 0), ("sm_dw_castle_wall_a1_gate", (20, 0, 0), 0)]),
    "castle_ramp": ("ramp continues a straight wall at its low end", "module length 20 m",
                    [("sm_dw_castle_wall_a1_str", (-20, 0, 0), 0), ("sm_dw_castle_wall_a1_ramp", (0, 0, 0), 0)]),
    "castle_bastion": ("Tower_B1 bastion on the merlon (-Y) face", "dwarf_castle_wall_c",
                       [("sm_dw_castle_wall_a1_str", (0, 0, 0), 0), ("sm_dw_castle_tower_b1", (1, -3, 0), 0)]),
    "castle_stairs": ("wall stairs on the inner (+Y) face, head at the wall walk", "dwarf_castle_wall_c",
                      [("sm_dw_castle_wall_a1_str", (0, 0, 0), 0),
                       ("sm_dw_castle_wall_stairs_a1", (8.5, 4.5, 0), 180)]),
    "tower_ramps": ("ramps rise to a Tower_A1, centres 14 m out", "dwarf_tower_ramp_a",
                    [("sm_dw_castle_tower_a1", (0, 0, 0), 0), ("sm_dw_castle_wall_a1_ramp", (-14, 0.5, 0), 0),
                     ("sm_dw_castle_wall_a1_ramp", (14, 0.5, 0), 180)]),
    "props_scale": ("unit props at x3 (screen wall x3.06) beside a 3 m wall and door; statues "
                    "pivot at their centre, so z = 0.48 x 3", "scale choice 2026-10-15",
                    [("sm_dw_wall_3m_c", (0, 0, 0), 0), ("sm_dw_wall_3m_door_c1", (3, 0, 0), 0),
                     ("dw_pillar_moria_01", (6.5, -1.5, 0), 0, (3, 3, 3)),
                     ("kys_dw_pillar_broken_01", (9, -1.5, 0), 0, (3, 3, 3)),
                     ("kys_dw_wall_001", (12.5, 0, 0), 0, (3.06, 3.06, 3.06)),
                     ("sd_dw_ih_statue_a", (1.5, -1.6, 1.44), 270, (3, 3, 3)),
                     ("sd_dw_ih_statue_b", (-1.5, -1.6, 1.47), 0, (3, 3, 3))]),
    "platform_row": ("6 m terrace platforms continue every 6 m", "dwarf_platform_b",
                     [("sm_dw_platform_6m_a2", (0, 0, 0), 90), ("sm_dw_platform_6m_a2", (0, 6, 0), 90)]),
}

LOG = []
OUT = None


def log(m):
    LOG.append(str(m))
    print(m)
    if OUT:
        with open(os.path.join(OUT, "log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG))


def mat_color(name):
    h = int(hashlib.md5(name.encode()).hexdigest()[:6], 16) / 0xFFFFFF
    r, g, b = colorsys.hls_to_rgb(h, 0.62, 0.45)
    return (r, g, b, 1.0)


def load_kit():
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
                if o.type == "MESH" and not o.name.lower().startswith("bo_") and not LOD_RE.search(o.name):
                    me = o.data.copy()
                    me.transform(o.matrix_world)
                    meshes[o.name.lower()] = me
            for o in new:
                bpy.data.objects.remove(o, do_unlink=True)
    for m in bpy.data.materials:
        m.diffuse_color = mat_color(m.name)
    return meshes


def world_tris(obj):
    mw = obj.matrix_world
    me = obj.data
    me.calc_loop_triangles()
    verts = [mw @ v.co for v in me.vertices]
    tris = [tuple(t.vertices) for t in me.loop_triangles]
    return verts, tris


def coplanar_area(va, ta, vb, tb, bvh_a, rng):
    """-> (same, opposite): shared-surface area with normals pointing the same
    way (visible z-fighting) and opposite (back-to-back butt joint, hidden)."""
    same = opp = 0.0
    for t in tb:
        a, b, c = vb[t[0]], vb[t[1]], vb[t[2]]
        cr = (b - a).cross(c - a)
        ar = cr.length / 2
        if ar < 1e-6:
            continue
        n = cr.normalized()
        hs = ho = 0
        samples = 6
        for _ in range(samples):
            r1, r2 = rng.random(), rng.random()
            if r1 + r2 > 1:
                r1, r2 = 1 - r1, 1 - r2
            p = a + (b - a) * r1 + (c - a) * r2
            loc, nrm, idx, dist = bvh_a.find_nearest(p, 0.003)
            if loc is not None:
                dot = nrm.dot(n)
                hs += dot > 0.999
                ho += dot < -0.999
        same += ar * hs / samples
        opp += ar * ho / samples
    return same, opp


def main():
    global OUT
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--only", default="")
    args = ap.parse_args(argv)
    OUT = args.out
    os.makedirs(OUT, exist_ok=True)
    done = os.path.join(OUT, "DONE.txt")
    if os.path.exists(done):
        os.remove(done)
    status = "ok"
    results = {}
    try:
        meshes = load_kit()
        scene = bpy.context.scene
        scene.render.engine = "BLENDER_WORKBENCH"
        sh = scene.display.shading
        sh.light, sh.color_type = "STUDIO", "MATERIAL"
        sh.show_cavity = sh.show_object_outline = True
        sh.background_type = "VIEWPORT"
        sh.background_color = (0.93, 0.93, 0.93)
        scene.render.resolution_x, scene.render.resolution_y = 900, 650
        try:
            scene.view_settings.view_transform = "Standard"
        except Exception:
            pass
        cd = bpy.data.cameras.new("cam")
        cd.type = "ORTHO"
        cd.clip_end = 10000.0
        cam = bpy.data.objects.new("cam", cd)
        scene.collection.objects.link(cam)
        scene.camera = cam
        rng = random.Random(7)
        for jname, (desc, evidence, parts) in JOINS.items():
            if args.only and args.only not in jname:
                continue
            objs = []
            missing = []
            for part in parts:
                piece, loc, yaw = part[0], part[1], part[2]
                scl = part[3] if len(part) > 3 else (1, 1, 1)
                me = meshes.get(piece)
                if me is None:
                    missing.append(piece)
                    continue
                o = bpy.data.objects.new(piece, me)
                o.matrix_world = (Matrix.Translation(Vector(loc)) @ Matrix.Rotation(math.radians(yaw), 4, "Z")
                                  @ Matrix.Diagonal(Vector((scl[0], scl[1], scl[2], 1.0))))
                scene.collection.objects.link(o)
                objs.append(o)
            bpy.context.view_layer.update()
            geo = [world_tris(o) for o in objs]
            bvhs = [BVHTree.FromPolygons(v, t) for v, t in geo]
            cop = opp = 0.0
            inter = 0
            pair_detail = []
            for i in range(len(objs)):
                for j in range(i + 1, len(objs)):
                    c_ij, o_ij = coplanar_area(*geo[i], *geo[j], bvhs[i], rng)
                    n_ij = len(bvhs[i].overlap(bvhs[j]))
                    cop += c_ij
                    opp += o_ij
                    inter += n_ij
                    if c_ij > 0.01 or o_ij > 0.01 or n_ij:
                        pair_detail.append({"a": objs[i].name, "b": objs[j].name,
                                            "zfight_m2": round(c_ij, 3), "butt_m2": round(o_ij, 3),
                                            "intersect_pairs": n_ij})
            mn = Vector((1e9,) * 3)
            mx = Vector((-1e9,) * 3)
            for v, _ in geo:
                for p in v:
                    mn = Vector(map(min, mn, p))
                    mx = Vector(map(max, mx, p))
            centre = (mn + mx) / 2
            span = max((mx - mn).length, 1.0)
            d = Vector((-0.55, -0.75, 0.55)).normalized()
            cam.location = centre + d * span * 2
            cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
            cd.ortho_scale = span * 1.1
            scene.render.filepath = os.path.join(OUT, f"{jname}.png")
            bpy.ops.render.render(write_still=True)
            results[jname] = {"desc": desc, "evidence": evidence, "parts": [list(map(str, p)) for p in parts],
                              "zfight_m2": round(cop, 3), "butt_m2": round(opp, 3),
                              "intersect_pairs": inter,
                              "size": [round(v, 3) for v in (mx - mn)], "min": [round(v, 3) for v in mn],
                              "max": [round(v, 3) for v in mx], "pairs": pair_detail, "missing": missing}
            log(f"[join] {jname}: zfight {cop:.3f} m2, butt {opp:.3f} m2, intersect {inter}, "
                f"size {results[jname]['size']}"
                f"{' MISSING ' + str(missing) if missing else ''}")
            for o in objs:
                bpy.data.objects.remove(o, do_unlink=True)
    except Exception:
        log(traceback.format_exc())
        status = "failed"
    with open(os.path.join(OUT, "joins.json"), "w", encoding="utf-8") as f:
        json.dump(results, f, indent=1)
    with open(done, "w", encoding="utf-8") as f:
        json.dump({"status": status, "joins": len(results)}, f)
    log(f"[done] {status}")


main()
