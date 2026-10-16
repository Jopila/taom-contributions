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
        [--kit gondor] [--only <join substring>]

The join tables are per kit (JOINS[kit]); the Gondor table holds the 15 joins mined in Phase 2.
A Gondor part is several meshes (one per sub-object), and every one is placed.
"""

import argparse
import colorsys
import hashlib
import json
import math
import os
import random
import sys
import traceback

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kitbash_kits as kk  # noqa: E402

# name: (description, evidence, [(piece, (x, y, z), yaw_deg), ...])
JOINS = {
    "erebor": {
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
    },
    # Gondor: offsets from gondor_prefab_joins.json (engine frame of the first part), converted
    # (x, y, z) -> (-x, -y, z), yaw unchanged; "n" is the mined count of the top offset.
    "gondor": {
        "wall_run": ("3 m panels continue every 3 m along X", "n 915, 88 prefabs",
                     [("gondor_wall_3m_a_clean", (0, 0, 0), 0), ("gondor_wall_3m_a_clean", (-3, 0, 0), 0)]),
        "wall_stack": ("second storey panel on the first at z 3", "n 1057, 88 prefabs",
                       [("gondor_wall_3m_a_clean", (0, 0, 0), 0), ("gondor_wall_3m_a_clean", (0, 0, 3), 0)]),
        "wall_corner_90": ("corner: perpendicular panel at (1.25, 1.25), yaw -90", "n 102, 55 prefabs",
                           [("gondor_wall_3m_a_clean", (0, 0, 0), 0),
                            ("gondor_wall_3m_window_a_clean", (1.25, 1.25, 0), -90)]),
        "wall_door_window_run": ("door panel then window panel 3 m along X", "n 37, 23 prefabs",
                                 [("gondor_wall_3m_a_door_a_clean", (0, 0, 0), 0),
                                  ("gondor_wall_3m_window_a_clean", (-3, 0, 0), 0)]),
        "trim_on_wall": ("string course trim on the wall line at z 3", "n 175, 37 prefabs",
                         [("gondor_wall_3m_a_dirty", (0, 0, 0), 0), ("gondor_wall_trim_3m_c_clean", (0, 0, 3), 0)]),
        "trim_corner": ("trim run, corner trim at 1.75, second run at yaw 90", "n 271 and 228, 44 prefabs",
                        [("gondor_wall_trim_3m_c_clean", (0, 0, 0), 0),
                         ("gondor_wall_trim_corner_c_clean", (1.75, 0, 0), 0),
                         ("gondor_wall_trim_3m_c_clean", (1.75, 1.5, 0), 90)]),
        "ground_grid": ("3 m paving tiles on a 3 m grid", "n 1242 and 958, 69 prefabs",
                        [("gondor_ground_3m_a_dirty", (0, 0, 0), 0), ("gondor_ground_3m_a_dirty", (0, -3, 0), 0),
                         ("gondor_ground_3m_a_dirty", (-3, 0, 0), 0)]),
        "ground_to_wall": ("wall stands on the paving edge", "n 54, 41 prefabs",
                           [("gondor_ground_3m_a_dirty", (0, 0, 0), 0), ("gondor_wall_3m_a_dirty", (-1.5, 0.25, 0), 90)]),
        "platform_grid": ("3 m raised platform blocks on a 3 m grid", "n 216, 27 prefabs",
                          [("gondor_ground_platform_3m_a_normal", (0, 0, 0), 0),
                           ("gondor_ground_platform_3m_a_normal", (-3, 0, 0), 0)]),
        "roof30_run": ("26.57 deg roof strips every 1.5 m", "n 419, 61 prefabs",
                       [("gondor_roof_a_30_1-5m_straight", (0, 0, 0), 0),
                        ("gondor_roof_a_30_1-5m_straight", (-1.5, 0, 0), 0)]),
        "roof30_corner_out": ("outer hip corner 2.45 m past a roof strip", "n 131, 45 prefabs",
                              [("gondor_roof_a_30_1-5m_straight", (0, 0, 0), 0),
                               ("gondor_roof_a_30_1-5m_corner_out", (2.45, 0, 0), 0)]),
        "roof30_on_wall": ("roof strip on a wall top at z 3.25, yaw 180", "n 7, 29 prefabs",
                           [("gondor_wall_3m_a_clean", (0, 0, 0), 0),
                            ("gondor_roof_a_30_1-5m_straight", (0.75, 0, 3.25), 180)]),
        "roof45_run": ("45 deg roof panels every 3 m", "n 393, 24 prefabs",
                       [("gondor_roof_a_45_3m_straight", (0, 0, 0), 0),
                        ("gondor_roof_a_45_3m_straight", (-3, 0, 0), 0)]),
        "gable45_halves": ("45 deg gable: two mirrored halves at one origin", "n 66, 25 prefabs",
                           [("gondor_roof_a_45_3m_side_a_clean", (0, 0, 0), 0),
                            ("gondor_roof_a_45_3m_side_a_clean", (0, 0, 0), 180)]),
        "pillar_stack": ("pillar drums stack every 1 m", "n 137, 24 prefabs",
                         [("gondor_pillar_mid_a1", (0, 0, 0), 0), ("gondor_pillar_mid_a1", (0, 0, 1), 0)]),
    },
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


def load_kit(cfg):
    """Import every kit FBX; return {part key: [world-space meshes]}: each part's base objects
    (kk.base_names, so a bare .lod or LOD-only sub-object counts; decals, bo_ bodies, LODs and .NNN
    copies do not). An object kit has one mesh per key, a prefix kit one per sub-object."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    meshes = {}
    for folder in cfg["kit_folders"]:
        d = os.path.join(cfg["src"], folder)
        for fn in sorted(os.listdir(d)):
            if not fn.lower().endswith(".fbx") or kk.is_excluded(cfg, folder, fn):
                continue
            before = set(bpy.data.objects)
            try:
                bpy.ops.wm.fbx_import(filepath=os.path.join(d, fn))
            except Exception:
                bpy.ops.import_scene.fbx(filepath=os.path.join(d, fn))
            new = [o for o in bpy.data.objects if o not in before]
            # one rule set with the catalog: a bare .lod or LOD-only sub-object is a base too
            keep = kk.base_names([o.name for o in new if o.type == "MESH"], cfg["group_by"])
            for o in new:
                if o.type != "MESH" or o.name not in keep:
                    continue
                me = o.data.copy()
                me.transform(o.matrix_world)
                meshes.setdefault(kk.part_key(o.name, cfg["group_by"]), []).append(me)
            bpy.data.batch_remove(new)
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
    ap.add_argument("--kit", default="erebor", choices=sorted(kk.KITS))
    args = ap.parse_args(argv)
    cfg = kk.KITS[args.kit]
    OUT = args.out
    os.makedirs(OUT, exist_ok=True)
    done = os.path.join(OUT, "DONE.txt")
    if os.path.exists(done):
        os.remove(done)
    status = "ok"
    results = {}
    try:
        meshes = load_kit(cfg)
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
        for jname, (desc, evidence, parts) in JOINS[args.kit].items():
            if args.only and args.only not in jname:
                continue
            objs = []
            owner = []  # placement index per object: a Gondor part places one object per sub-object
            missing = []
            for k, part in enumerate(parts):
                piece, loc, yaw = part[0], part[1], part[2]
                scl = part[3] if len(part) > 3 else (1, 1, 1)
                part_meshes = meshes.get(piece)
                if not part_meshes:
                    missing.append(piece)
                    continue
                for me in part_meshes:
                    owner.append(k)
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
            intra = [0.0, 0.0, 0]  # zfight, butt, intersect between sub-objects of ONE placement (authored)
            pair_detail = []
            for i in range(len(objs)):
                for j in range(i + 1, len(objs)):
                    c_ij, o_ij = coplanar_area(*geo[i], *geo[j], bvhs[i], rng)
                    n_ij = len(bvhs[i].overlap(bvhs[j]))
                    if owner[i] == owner[j]:
                        intra[0] += c_ij
                        intra[1] += o_ij
                        intra[2] += n_ij
                        continue
                    cop += c_ij
                    opp += o_ij
                    inter += n_ij
                    if c_ij > 0.01 or o_ij > 0.01 or n_ij:
                        pair_detail.append({"a": f"{objs[i].name} #{owner[i]}", "b": f"{objs[j].name} #{owner[j]}",
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
            if any(intra):
                results[jname]["intra_part"] = {"zfight_m2": round(intra[0], 3), "butt_m2": round(intra[1], 3),
                                                 "intersect_pairs": intra[2]}
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
