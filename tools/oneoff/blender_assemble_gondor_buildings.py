"""
Assemble six Gondor buildings from the kit's part families and export them in ONE FBX
(docs/kitbash/gondor/blender-build-prompt.md Phases 3 to 5; brief and evidence in
docs/kitbash/gondor/design-patterns.md and docs/kitbash/gondor-buildings.md).

The part-family helpers (grip a panel by its structural body, per-sub-object LOD ladder, bo_
per prefix, decal and bare-.lod traps, gable seating, flush trims) are imported from
blender_assemble_lond_cirion_buildings.py. Its join loop, world-bake and material fold live inline in
lc.main, so join_section and main repeat them (moving them into shared functions is a follow-up).
The run stops before writing anything if the catalog is missing, if the Lond Cirion selection and
kitbash_kits.base_names disagree on a placed part, or if a tier's triangles differ from its part
sum (a dropped sub-object). Differences from Lond
Cirion, from Mike's Erebor review: no below-grade skirt (the floor slab sits on the terrain at
z 0), corner columns on every corner (they also cover the authored corner's coplanar end caps),
round pillars get a base and a capital.

Every building is centred on the origin with its front at -Y in Blender (+Y in the editor at
rotation 0, by the frame rule), and is four objects: <name>, <name>.lod3, <name>.lod6,
bo_<name>. All six share the file, overlapping at the origin, as the Lond Cirion sections did.

Writes:
    AssetSources/Scenes/Gondor/buildings_taom/gondor_buildings_a.fbx (+ _import.md beside it)
    <out>/<name>_preview.json   brief numbers for blender_probe_erebor_building.py --brief
    <out>/<name>_expected.json  layout (eave, roof, walls, openings) for the probe --expected
    <out>/<name>__<view>.png    workbench renders
    <out>/gondor_buildings_a_inspect.blend  re-imported export, one collection per tier
    <out>/log.txt, <out>/DONE.txt

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_assemble_gondor_buildings.py -- --out E:\\LOTRAOMAssets\\_export\\gondor\\buildings
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

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blender_assemble_lond_cirion_buildings as lc  # noqa: E402
from kitbash_kits import FBX_EXPORT, base_names, get_kit  # noqa: E402

KIT = get_kit("gondor")
FILE_NAME = "gondor_buildings_a"
FAMILIES = ["gondor_wall_3m_straight_a.fbx", "gondor_wall_6m_straight_a.fbx",
            "gondor_trims_straight_a.fbx", "gondor_roofs_a.fbx", "gondor_ground_straight_a.fbx",
            "gondor_buttres_a.fbx", "gondor_stairs_3m_straight_a.fbx", "gondor_pillars_a.fbx"]
CATALOG = os.path.join(KIT["catalog_dir"], "gondor_catalog_meshes.json")
ASSETS = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets"

PLAIN = "gondor_wall_3m_a_clean"
DOOR = "gondor_wall_3m_a_door_a_clean"
WIN = "gondor_wall_3m_window_a_clean"
WIN2 = "gondor_wall_3m_window2_a_clean"
PLAIN6 = "gondor_wall_6m_a_clean"
ARCH6 = "gondor_wall_6m_arch_b_clean"      # open arch, 4.56 x 5.24 m
DOOR6 = "gondor_wall_6m_arch_c_clean"      # arched door, 1.21 x 2.63 m
WIN6 = "gondor_wall_6m_window3_a_clean"
COLUMN = "gondor_column_3m_a_1x1"
TRIM3 = "gondor_wall_trim_3m_b_clean"
TRIM6 = "gondor_wall_trim_6m_b"            # _6m_b: the _6m_a profile without the decal
BUTTRESS = "gondor_buttress_a_clean"
STAIR = "gondor_stairs_3m_a_clean"
FLOOR = "gondor_ground_3m_a_normal.floor"
ROOF30 = ("gondor_roof_a_30_3m_straight", "gondor_roof_a_30_3m_edge_straight", "gondor_roof_a_30_3m_side_a_clean")
ROOF45 = ("gondor_roof_a_45_3m_straight", "gondor_roof_a_45_3m_edge_straight", "gondor_roof_a_45_3m_side_a_clean")
HIP45 = "gondor_roof_a_45_3m_corner_out"
PILLAR = ["gondor_pillar_bot_a"] + ["gondor_pillar_mid_a1"] * 4 + ["gondor_pillar_mid_a2", "gondor_pillar_top_b"]
# the hip quadrant's origin is its eave corner, so it is placed by origin (Lond Cirion rule 4)
lc.ANCHOR_ORIGIN.add(HIP45)

WALLS = []    # one dict per wall panel, recorded where it is placed (expected_layout reads them)
LAYOUT = {}


def log(m):
    lc.log(m)


# --------------------------------------------------------------- recipe helpers

def wall_at(S, prefix, cx, cy, out, z):
    """Place a panel on a footprint line and record that line for the probe: axis, plane, centre and
    half width along it, z range, and the part-to-world matrix that moves its catalog apertures."""
    lc.wall(S, prefix, cx, cy, out, z)
    lo, hi = lc.group_bbox([lc.by_name.get(prefix + ".wall") or lc.by_name[prefix]])
    axis = "x" if out[0] == 0 else "y"
    WALLS.append({"section": S, "prefix": prefix, "axis": axis, "plane": round(cy if axis == "x" else cx, 3),
                  "u": cx if axis == "x" else cy, "half_w": round(hi.x - lo.x) / 2, "z": z,
                  "height": round(hi.z - lo.z),
                  "M": lc.PLACEMENTS[-1][2] @ Matrix.Translation(-lc.anchor(prefix))})


def face(S, row, along, plane, out, z, centres):
    """One storey of panels on a footprint line: along 'x' (wall at y = plane) or 'y'."""
    for prefix, c in zip(row, centres):
        cx, cy = (c, plane) if along == "x" else (plane, c)
        wall_at(S, prefix, cx, cy, out, z)


# the 1.04 m column's cap faces sit at about 0.51 m, the same plane as the 6 m trim's faces
# (+-0.51 m): 2 % wider in plan, the column encloses them (0.48 m2 exposed per corner, measured)
COLUMN_SCALE = Matrix.Diagonal((1.02, 1.02, 1.0, 1.0))


def corner_columns(S, hx, hy, zs):
    for sx in (-hx, hx):
        for sy in (-hy, hy):
            for z in zs:
                lc.place(S, COLUMN, Matrix.Translation((sx, sy, z)) @ COLUMN_SCALE)


WALL_HALF = 0.27  # half thickness of the panels' top with their stonebrick (measured +-0.267)


def trim_lift(trim):
    """How far to raise a trim above "top flush with the storey line" so its top surface covers the
    whole wall top. A trim's top is a moulding, not a flat lid: over the wall band it can sit well
    below its bbox top (the 6 m trim slopes to 2 cm below it, measured), which left the wall top
    level with the trim and open to the sky. Measured per trim by down-rays over the wall band in
    the trim's own frame (long axis X, the wall across Y), plus 5 mm."""
    objs = lc.base_set(trim)
    verts, tris, base = [], [], 0
    for o in objs:
        o.data.calc_loop_triangles()
        verts += [v.co.copy() for v in o.data.vertices]
        tris += [tuple(base + i for i in t.vertices) for t in o.data.loop_triangles]
        base = len(verts)
    bvh = BVHTree.FromPolygons(verts, tris)
    lo, hi = lc.group_bbox(objs)
    lowest = hi.z
    # every 2 cm along the whole length: the carved blocks have V-grooves between them (the 3 m
    # trim's dips 3.5 mm below the wall top at one joint, measured), which sparse samples miss
    nx = max(2, int((hi.x - lo.x - 0.04) / 0.02) + 1)
    for i in range(nx):
        x = lo.x + 0.02 + (hi.x - lo.x - 0.04) * i / (nx - 1)
        for j in range(9):
            y = -WALL_HALF + 2 * WALL_HALF * j / 8
            hit = bvh.ray_cast(Vector((x, y, hi.z + 1.0)), Vector((0, 0, -1)), 10.0)
            if hit[0] is None:
                raise RuntimeError(f"{trim}: no top surface over the wall band at ({x:.2f}, {y:.2f})")
            lowest = min(lowest, hit[0].z)
    return round(hi.z - lowest + 0.005, 4)


def courses(S, trim, tops, xs, ys, hx, hy):
    """String courses centred on the wall line (Lond Cirion rule 6), raised by trim_lift() so the
    trim's top covers the wall top instead of sharing its plane."""
    d = lc.detail_side(trim)
    th = lc.part_height(trim)
    lift = trim_lift(trim)
    log(f"[trim] {trim}: lift {lift} m")
    for top in tops:
        z = top - th + lift
        for cx in xs:
            for cy, out in ((-hy, (0, -1)), (hy, (0, 1))):
                lc.place(S, trim, Matrix.Translation((cx, cy, z)) @ lc.rot_to(d, out))
        for cy in ys:
            for cx, out in ((-hx, (-1, 0)), (hx, (1, 0))):
                lc.place(S, trim, Matrix.Translation((cx, cy, z)) @ lc.rot_to(d, out))


def buttress(S, cx, cy, out):
    """The buttress is origin-anchored and its body lies wholly on one side of its mounting plane
    (measured: y -0.55 to 0.01), so turn that side outward, read from its bounds. lc.wall's
    detail_side() counts vertices instead and turned every buttress into its wall."""
    lo, hi = lc.group_bbox(lc.base_set(BUTTRESS))
    side = -1.0 if lo.y + hi.y < 0 else 1.0
    lc.place(S, BUTTRESS, Matrix.Translation((cx, cy, 0.0)) @ lc.rot_to(side, out))


def floor_grid(S, xs, ys, z):
    for cx in xs:
        for cy in ys:
            lc.place(S, FLOOR, Matrix.Translation((cx, cy, z)))  # gripped by its bottom: slab on z


def pillar(S, x, y):
    """Base, drums and capital, 6.0 m: bot 0.5 + 4 x 1.0 + 0.5 + top 1.0, each gripped by its bottom."""
    z = 0.0
    for prefix in PILLAR:
        lc.place(S, prefix, Matrix.Translation((x, y, z)))
        z += lc.part_height(prefix)
    return z


def pillar_half():
    """Half width of the pillar base, so an engaged pillar touches the wall face (0.25 m off the line)."""
    lo, hi = lc.group_bbox(lc.base_set(PILLAR[0]))
    return round((hi.x - lo.x) / 2, 3)


def high_point(prefix):
    pts = [v.co for o in lc.base_set(prefix) for v in o.data.vertices]
    zmax = max(p.z for p in pts)
    top = [p for p in pts if p.z > zmax - 0.02]
    return Vector((sum(p.x for p in top) / len(top), sum(p.y for p in top) / len(top), zmax))


def pyramid_roof(S, h, z):
    """Four 45 degree outer-corner quadrants, origin on the footprint corner at the eave, each
    turned so its high point is the building centre. The high point is measured, not assumed."""
    hp = high_point(HIP45)
    if abs(abs(hp.x) - h) > 0.1 or abs(abs(hp.y) - h) > 0.1:
        raise RuntimeError(f"{HIP45}: high point {tuple(round(v, 3) for v in hp)} is not a {h} m quadrant apex")
    for sx in (-1, 1):
        for sy in (-1, 1):
            ang = math.atan2(-sy, -sx) - math.atan2(hp.y, hp.x)
            lc.place(S, HIP45, Matrix.Translation((sx * h, sy * h, z)) @ Matrix.Rotation(ang, 4, "Z"))
    return z + hp.z


def bays(n, width=3.0):
    return [round(-(n - 1) * width / 2 + i * width, 3) for i in range(n)]


# --------------------------------------------------------------- the six buildings

def town_house_01():
    """9 x 6 m, one 3 m storey, 26.57 degree gable along X."""
    S, hx, hy = "gondor_town_house_01", 4.5, 3.0
    xs, ys = bays(3), bays(2)
    face(S, [WIN, DOOR, WIN], "x", -hy, (0, -1), 0, xs)
    face(S, [PLAIN, WIN, PLAIN], "x", hy, (0, 1), 0, xs)
    face(S, [WIN, PLAIN], "y", hx, (1, 0), 0, ys)
    face(S, [PLAIN, WIN], "y", -hx, (-1, 0), 0, ys)
    corner_columns(S, hx, hy, (0,))
    courses(S, TRIM3, (3,), xs, ys, hx, hy)
    floor_grid(S, xs, ys, 0.0)
    ridge = lc.gabled_roof(S, *ROOF30, xs, hx, 3.0)
    LAYOUT[S] = {"eave": 3.0, "roof": "gable", "ridge_axis": "x", "half": [hx, hy], "chamfer": 0.0,
                 "ridge_z": ridge}


def townhouse_01():
    """12 x 6 m, two 3 m storeys, 45 degree gable along X."""
    S, hx, hy = "gondor_townhouse_01", 6.0, 3.0
    xs, ys = bays(4), bays(2)
    face(S, [WIN, DOOR, WIN, WIN], "x", -hy, (0, -1), 0, xs)
    face(S, [WIN2] * 4, "x", -hy, (0, -1), 3, xs)
    face(S, [PLAIN, WIN, WIN, PLAIN], "x", hy, (0, 1), 0, xs)
    face(S, [WIN] * 4, "x", hy, (0, 1), 3, xs)
    for x, out, low in ((hx, (1, 0), [WIN, PLAIN]), (-hx, (-1, 0), [PLAIN, WIN])):
        face(S, low, "y", x, out, 0, ys)
        face(S, [WIN, WIN], "y", x, out, 3, ys)
    corner_columns(S, hx, hy, (0, 3))
    courses(S, TRIM3, (3, 6), xs, ys, hx, hy)
    floor_grid(S, xs, ys, 0.0)
    floor_grid(S, xs, ys, 3.0)
    ridge = lc.gabled_roof(S, *ROOF45, xs, hx, 6.0)
    LAYOUT[S] = {"eave": 6.0, "roof": "gable", "ridge_axis": "x", "half": [hx, hy], "chamfer": 0.0,
                 "ridge_z": ridge}


def watch_tower_01():
    """6 x 6 m, four 3 m storeys, buttressed, 45 degree pyramid roof."""
    S, h = "gondor_watch_tower_01", 3.0
    xs = bays(2)
    rows = {0: ([DOOR, PLAIN], [PLAIN, PLAIN], [PLAIN, PLAIN], [PLAIN, PLAIN]),
            3: ([WIN, WIN], [WIN, PLAIN], [PLAIN, WIN], [WIN, PLAIN]),
            6: ([WIN, WIN], [PLAIN, WIN], [WIN, PLAIN], [PLAIN, WIN]),
            9: ([WIN2, WIN2], [WIN2, WIN2], [WIN2, WIN2], [WIN2, WIN2])}
    for z, (front, back, east, west) in rows.items():
        face(S, front, "x", -h, (0, -1), z, xs)
        face(S, back, "x", h, (0, 1), z, xs)
        face(S, east, "y", h, (1, 0), z, xs)
        face(S, west, "y", -h, (-1, 0), z, xs)
    corner_columns(S, h, h, (0, 3, 6, 9))
    # buttresses at the middle of each face, on the outer face (origin-anchored, 6 m tall)
    for cx, cy, out in ((0, -h - 0.25, (0, -1)), (0, h + 0.25, (0, 1)),
                        (h + 0.25, 0, (1, 0)), (-h - 0.25, 0, (-1, 0))):
        buttress(S, cx, cy, out)
    courses(S, TRIM3, (3, 6, 9), xs, xs, h, h)
    courses(S, TRIM6, (12,), [0.0], [0.0], h, h)
    for z in (0.0, 3.0, 6.0, 9.0):
        floor_grid(S, xs, xs, z)
    apex = pyramid_roof(S, h, 12.0)
    LAYOUT[S] = {"eave": 12.0, "roof": "hip", "ridge_axis": "x", "half": [h, h], "chamfer": 0.0,
                 "ridge_z": apex}


def barracks_01():
    """24 x 6 m, two 3 m storeys, buttressed, external stair to an upper door, 26.57 degree gable."""
    S, hx, hy = "gondor_barracks_01", 12.0, 3.0
    xs, ys = bays(8), bays(2)
    stair_x = 1.5
    face(S, [WIN, DOOR, WIN, WIN, PLAIN, WIN, DOOR, WIN], "x", -hy, (0, -1), 0, xs)
    face(S, [WIN2, WIN2, WIN2, WIN2, DOOR, WIN2, WIN2, WIN2], "x", -hy, (0, -1), 3, xs)
    face(S, [PLAIN] * 8, "x", hy, (0, 1), 0, xs)
    face(S, [WIN] * 8, "x", hy, (0, 1), 3, xs)
    for x, out in ((hx, (1, 0)), (-hx, (-1, 0))):
        face(S, [PLAIN, PLAIN], "y", x, out, 0, ys)
        face(S, [WIN, WIN], "y", x, out, 3, ys)
    corner_columns(S, hx, hy, (0, 3))
    for cx in (-9.0, -6.0, -3.0, 0.0, 3.0, 6.0, 9.0):
        buttress(S, cx, hy + 0.25, (0, 1))
        if abs(cx - stair_x) > 2.0:  # the stair occupies x 0 to 3 in front of the wall
            buttress(S, cx, -hy - 0.25, (0, -1))
    # stair: high tread at local -Y, so Rz(180) puts the head at the upper door (Lond Cirion rule 4)
    lc.place(S, STAIR, Matrix.Translation((stair_x, -(hy + 0.25) - 3.0, 0.0)) @ Matrix.Rotation(math.pi, 4, "Z"))
    courses(S, TRIM3, (3, 6), xs, ys, hx, hy)
    floor_grid(S, xs, ys, 0.0)
    floor_grid(S, xs, ys, 3.0)
    ridge = lc.gabled_roof(S, *ROOF30, xs, hx, 6.0)
    LAYOUT[S] = {"eave": 6.0, "roof": "gable", "ridge_axis": "x", "half": [hx, hy], "chamfer": 0.0,
                 "ridge_z": ridge}


def hall_01():
    """18 x 6 m hall, one 6 m storey, engaged pillars with base and capital, 45 degree gable."""
    S, hx, hy = "gondor_hall_01", 9.0, 3.0
    xs6, xs3 = bays(3, 6.0), bays(6)
    face(S, [WIN6, DOOR6, WIN6], "x", -hy, (0, -1), 0, xs6)
    face(S, [WIN6, PLAIN6, WIN6], "x", hy, (0, 1), 0, xs6)
    wall_at(S, WIN6, hx, 0.0, (1, 0), 0)
    wall_at(S, PLAIN6, -hx, 0.0, (-1, 0), 0)
    corner_columns(S, hx, hy, (0, 3))
    r = pillar_half()
    for x in (-3.0, 3.0):
        for y in (-hy - 0.25 - r, hy + 0.25 + r):
            pillar(S, x, y)
    courses(S, TRIM6, (6,), xs6, [0.0], hx, hy)
    floor_grid(S, xs3, bays(2), 0.0)
    ridge = lc.gabled_roof(S, *ROOF45, xs3, hx, 6.0)
    LAYOUT[S] = {"eave": 6.0, "roof": "gable", "ridge_axis": "x", "half": [hx, hy], "chamfer": 0.0,
                 "ridge_z": ridge}


def market_arcade_01():
    """12 x 6 m market hall, one 6 m storey, open arches on both long sides, 26.57 degree gable."""
    S, hx, hy = "gondor_market_arcade_01", 6.0, 3.0
    xs6, xs3 = bays(2, 6.0), bays(4)
    face(S, [ARCH6, ARCH6], "x", -hy, (0, -1), 0, xs6)
    face(S, [ARCH6, ARCH6], "x", hy, (0, 1), 0, xs6)
    wall_at(S, DOOR6, hx, 0.0, (1, 0), 0)
    wall_at(S, PLAIN6, -hx, 0.0, (-1, 0), 0)
    corner_columns(S, hx, hy, (0, 3))
    r = pillar_half()
    for y in (-hy - 0.25 - r, hy + 0.25 + r):
        pillar(S, 0.0, y)
    courses(S, TRIM6, (6,), xs6, [0.0], hx, hy)
    floor_grid(S, xs3, bays(2), 0.0)
    ridge = lc.gabled_roof(S, *ROOF30, xs3, hx, 6.0)
    LAYOUT[S] = {"eave": 6.0, "roof": "gable", "ridge_axis": "x", "half": [hx, hy], "chamfer": 0.0,
                 "ridge_z": ridge}


BUILDINGS = [town_house_01, townhouse_01, watch_tower_01, barracks_01, hall_01, market_arcade_01]
# the door side in Blender where it is not the front (-Y); the editor sees (x, y) as (-x, -y)
DOOR_SIDE = {"gondor_market_arcade_01": "+X"}
EDITOR_SIDE = {"-Y": "+Y", "+Y": "-Y", "+X": "-X", "-X": "+X"}


# --------------------------------------------------------------- probe inputs

def expected_layout(S, apertures):
    """LAYOUT plus every wall line and planned opening, for blender_probe_erebor_building.py.
    apertures: {lowercase part name: catalog apertures}; a placed panel missing from it is an error,
    since a silent gap would only show later as stray see-through rays."""
    out = dict(LAYOUT[S])
    lines, openings = {}, []
    for wl in (w for w in WALLS if w["section"] == S):
        prefix, axis, plane, u, M = wl["prefix"], wl["axis"], wl["plane"], wl["u"], wl["M"]
        w = lines.setdefault((axis, plane), {"runs_along": axis, "plane": plane, "u_min": 1e9, "u_max": -1e9,
                                              "z_min": 1e9, "z_max": -1e9})
        w["u_min"], w["u_max"] = min(w["u_min"], u - wl["half_w"]), max(w["u_max"], u + wl["half_w"])
        w["z_min"], w["z_max"] = min(w["z_min"], wl["z"]), max(w["z_max"], wl["z"] + wl["height"])
        if prefix.lower() not in apertures:
            raise RuntimeError(f"{prefix} is placed as a wall but missing from {CATALOG}")
        for a in apertures[prefix.lower()]:
            if a["through"] != "Y":
                continue
            pts = [M @ Vector((uu, 0.0, zz)) for uu in (a["u_min"], a["u_max"]) for zz in (a["z_min"], a["z_max"])]
            us = [p.x if axis == "x" else p.y for p in pts]
            openings.append({"piece": prefix, "runs_along": axis, "plane": plane,
                             "u_min": round(min(us), 3), "u_max": round(max(us), 3),
                             "z_min": round(min(p.z for p in pts), 3), "z_max": round(max(p.z for p in pts), 3),
                             "area": a["area"]})
    out["walls"] = sorted(({k: (round(v, 3) if isinstance(v, float) else v) for k, v in w.items()}
                           for w in lines.values()), key=lambda w: (w["runs_along"], w["plane"]))
    out["openings"] = openings
    out["chamfers"] = []
    return out


# --------------------------------------------------------------- render and export

def mat_color(name):
    h = int(hashlib.md5(name.encode()).hexdigest()[:6], 16)
    r, g, b = colorsys.hsv_to_rgb((h % 360) / 360.0, 0.35, 0.75)
    return (r, g, b, 1.0)


def setup_workbench():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light, sh.color_type = "STUDIO", "MATERIAL"
    sh.show_cavity = sh.show_object_outline = sh.show_shadows = True
    sh.background_type = "VIEWPORT"
    sh.background_color = (0.93, 0.93, 0.93)
    scene.render.resolution_x, scene.render.resolution_y = 1400, 1000
    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass
    for m in bpy.data.materials:
        m.diffuse_color = mat_color(m.name)
    cam_data = bpy.data.cameras.new("cam")
    cam_data.lens = 50.0
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam


def join_section(section, tier):
    """lc.main's join loop for one section and tier; -> (object, expected tris)."""
    dups, expect = [], 0
    for sec, prefix, M in lc.PLACEMENTS:
        if sec != section:
            continue
        objs = lc.tier_objs(prefix, tier)
        if not objs:
            lc.WARNINGS.append(f"{section}: {prefix}: no {tier} objects")
            continue
        pm = M @ Matrix.Translation(-lc.anchor(prefix))
        for src in objs:
            expect += lc.tris(src)
            dup = src.copy()
            dup.data = src.data.copy()
            dup.matrix_world = pm
            if pm.determinant() < 0:
                dup.data.flip_normals()
            bpy.context.scene.collection.objects.link(dup)
            dups.append(dup)
    target = bpy.data.objects.new("join_target", bpy.data.meshes.new("join_target"))
    bpy.context.scene.collection.objects.link(target)
    bpy.context.view_layer.update()
    lc.select_only(dups + [target])
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    name = f"bo_{section}" if tier == "bo" else section + lc.TIER_SUFFIX[tier]
    joined.name = joined.data.name = name
    if tier == "bo":
        joined.data.materials.clear()
        joined.data.materials.append(bpy.data.materials.get("stone") or bpy.data.materials.new("stone"))
    bpy.context.view_layer.update()
    return joined, expect


def unpackaged_materials(names):
    have = set()
    for _, _, files in os.walk(ASSETS):
        have.update(f.lower()[:-len("_mtl.tpac")] for f in files if f.lower().endswith("_mtl.tpac"))
    return [n for n in names if n.lower() not in have]


def write_import_note(path, stats, sections):
    mats = sorted({m for s in sections for m in stats[s]["materials"]})
    missing = unpackaged_materials(mats)
    lines = [f"# {FILE_NAME}: Modding Kit import note", "",
             "Built by `tools/oneoff/blender_assemble_gondor_buildings.py` from the Gondor kit part families.",
             "Brief and evidence: `docs/kitbash/gondor-buildings.md`, `docs/kitbash/gondor/design-patterns.md`.", "",
             "## Buildings in the FBX", "",
             "Six buildings, four objects each, all centred on the origin (they overlap in the file).", "",
             "| Object | Triangles | Bounds min (m) | Bounds max (m) |", "|---|---|---|---|"]
    for s in sections:
        for suffix in ("", ".lod3", ".lod6"):
            st = stats[s + suffix]
            lines.append(f"| `{s + suffix}` | {st['tris']} | {st['min']} | {st['max']} |")
        st = stats["bo_" + s]
        lines.append(f"| `bo_{s}` | {st['tris']} | {st['min']} | {st['max']} |")
    lines += ["", "Bounds are in the Blender frame (Z up, front of each building facing -Y).", "",
              "## Materials to bind (exact names)", ""]
    lines += [f"- `{m}`" + (" **(no `_mtl.tpac` found in TAOM_Map)**" if m in missing else "") for m in mats]
    lines += ["- `bo_` bodies: `stone` (the physics material the kit bodies use)", ""]
    if missing:
        lines += ["**Unpackaged materials.** The names marked above have no material package under",
                  "`TAOM_Map/Assets`. In the Kit, bind those faces to the material the shipped Gondor part uses.", ""]
    lines += ["## Placement", "",
              "- Origin: the centre of the footprint at grade. Wall bases and the underside of every floor",
              "  slab sit at z 0; no below-grade skirt. Corner columns, pillar bases and buttresses also start at z 0.",
              "- The engine reads the Gondor kit FBX turned 180 degrees about Z from Blender: a side at",
              "  (x, y) in Blender is at (-x, -y) in the editor at rotation 0. Door side per building:",
              ] + [f"  - `{s}`: {DOOR_SIDE.get(s, '-Y')} in Blender, {EDITOR_SIDE[DOOR_SIDE.get(s, '-Y')]} in the editor"
                   for s in sections] + [
              "", "## LOD tiers", "",
              "Per sub-object (Lond Cirion ladder): `.lod3` takes `.lod3`, else `.lod2`, else `.lod4`;",
              "`.lod6` takes `.lod6`, else `.lod5`, `.lod4`, `.lod2`. A sub-object with none keeps its base."]
    with open(os.path.splitext(path)[0] + "_import.md", "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def render_section(section, out):
    scene = bpy.context.scene
    cam = scene.camera
    for o in scene.objects:
        if o.type == "MESH":
            o.hide_render = o.name != section
    lo, hi = lc.group_bbox([bpy.data.objects[section]])
    lo.z = max(lo.z, -0.5)
    ctr, span = (lo + hi) / 2, (hi - lo).length
    shots = {"q1": Vector((-0.6, -0.8, 0.55)), "q2": Vector((0.6, 0.8, 0.55)), "front": Vector((0, -1, 0)),
             "side": Vector((1, 0, 0)), "top": Vector((0, 0, 1))}
    cam.data.type = "ORTHO"
    for nm, d in shots.items():
        d = d.normalized()
        cam.location = ctr + d * span * 3
        cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.ortho_scale = span * 1.45  # landscape frame: the scale is the width, so a tower needs margin
        cam.data.clip_end = span * 10
        scene.render.filepath = os.path.join(out, f"{section}__{nm}.png")
        bpy.ops.render.render(write_still=True)


def inspect_blend(fbx, out, sections):
    """Re-import the EXPORTED file, one collection per tier, flat colours, saved for Mike. Each
    building is moved 35 m along X from the last (in this .blend only) so the six do not overlap."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    cols = {}
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH":
            continue
        building = o.name[3:] if o.name.startswith("bo_") else o.name.split(".")[0]
        if building in sections:
            o.location.x += 35.0 * sections.index(building)
        tier = "bo" if o.name.startswith("bo_") else (o.name.rsplit(".", 1)[1] if "." in o.name else "base")
        if tier not in cols:
            cols[tier] = bpy.data.collections.new(f"tier_{tier}")
            bpy.context.scene.collection.children.link(cols[tier])
        for c in o.users_collection:
            c.objects.unlink(o)
        cols[tier].objects.link(o)
    for m in bpy.data.materials:
        m.diffuse_color = mat_color(m.name)
    for name, c in cols.items():
        if name != "base":
            c.hide_viewport = True
    path = os.path.join(out, f"{FILE_NAME}_inspect.blend")
    bpy.ops.wm.save_as_mainfile(filepath=path)
    return path, {k: sorted(o.name for o in c.objects) for k, c in cols.items()}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    args = ap.parse_args(argv)
    out = args.out
    os.makedirs(out, exist_ok=True)
    lc.REPORT_DIR = out
    done = os.path.join(out, "DONE.txt")
    if os.path.exists(done):
        os.remove(done)
    summary = {"status": "error"}
    try:
        # read before anything is built or written: a missing catalog must not cost the live export
        apertures = {p["name"].lower(): p["apertures"]
                     for p in json.load(open(CATALOG, encoding="utf-8"))["pieces"]}
        bpy.ops.wm.read_factory_settings(use_empty=True)
        for fname in FAMILIES:
            # the legacy importer produced the verified export; the catalog and probe try
            # wm.fbx_import first (their names and apertures agree with this import, measured)
            bpy.ops.import_scene.fbx(filepath=os.path.join(KIT["src"], "meshes", fname))
        for o in list(bpy.context.scene.objects):  # world-bake, as lc.main does
            if o.type != "MESH":
                continue
            if o.data.users > 1:
                o.data = o.data.copy()
            mw = o.matrix_world.copy()
            o.data.transform(mw)
            if mw.determinant() < 0:
                o.data.flip_normals()
            o.matrix_world = Matrix.Identity(4)
        for mat in list(bpy.data.materials):  # .NNN duplicates: the editor binds by exact name
            m = re.match(r"^(.*)\.\d{3}$", mat.name)
            if not m:
                continue
            base = bpy.data.materials.get(m.group(1))
            if base is not None:
                mat.user_remap(base)
                bpy.data.materials.remove(mat)
            else:
                mat.name = m.group(1)
        bpy.context.view_layer.update()
        lc.by_name.update({o.name: o for o in bpy.context.scene.objects if o.type == "MESH"})

        for build in BUILDINGS:
            build()
        sections = list(LAYOUT)
        # the assembler selects geometry with the Lond Cirion rules; the catalog with kitbash_kits.
        # They must agree on every part placed, or the probe's apertures describe other geometry.
        keep = base_names(list(lc.by_name), "prefix")
        for prefix in sorted({p for _, p, _ in lc.PLACEMENTS}):
            got = {o.name for o in lc.base_set(prefix)}
            want = {n for n in keep if n == prefix or n.startswith(prefix + ".")}
            if got != want:
                raise RuntimeError(f"{prefix}: Lond Cirion selects {sorted(got)}, kitbash_kits {sorted(want)}")
        missing = sorted({w["prefix"] for w in WALLS if w["prefix"].lower() not in apertures})
        if missing:  # before the export, so a stale catalog never leaves a half-written run
            raise RuntimeError(f"walls missing from {CATALOG}: {missing}")
        for s in sections:
            log(f"[plan] {s}: {sum(1 for p in lc.PLACEMENTS if p[0] == s)} placements, top z {LAYOUT[s]['ridge_z']:.3f}")

        stats, outputs = {}, []
        for section in sections:
            for tier in ("base", "lod3", "lod6", "bo"):
                o, expect = join_section(section, tier)
                got = lc.tris(o)
                lo, hi = lc.group_bbox([o])
                stats[o.name] = {"tris": got, "expected_sum": expect,
                                 "materials": [m.name for m in o.data.materials if m],
                                 "min": [round(v, 3) for v in lo], "max": [round(v, 3) for v in hi]}
                if got != expect:  # a dropped sub-object: never export it
                    raise RuntimeError(f"{o.name}: tris {got} != part sum {expect}")
                dup = [m.name for m in o.data.materials if m and re.search(r"\.\d{3}$", m.name)]
                if dup:
                    lc.WARNINGS.append(f"{o.name}: duplicate-suffixed materials {dup}")
                log(f"[join] {o.name}: tris {got} (sum {expect}), min {stats[o.name]['min']}, max {stats[o.name]['max']}")
                outputs.append(o)
        for w in sorted(set(lc.WARNINGS)):
            log(f"[warn] {w}")

        os.makedirs(KIT["export_dir"], exist_ok=True)
        fbx = os.path.join(KIT["export_dir"], f"{FILE_NAME}.fbx")
        if os.path.exists(fbx):
            bak = fbx + ".bak-" + str(int(os.path.getmtime(fbx)))
            os.replace(fbx, bak)
            log(f"[export] previous file kept as {bak}")
        lc.select_only(outputs)
        bpy.ops.export_scene.fbx(filepath=fbx, **FBX_EXPORT)
        log(f"[export] wrote {fbx}")
        write_import_note(fbx, stats, sections)

        for s in sections:
            brief = {"building": s, "placements": sum(1 for p in lc.PLACEMENTS if p[0] == s),
                     "tris": {"base": stats[s]["tris"], "lod3": stats[s + ".lod3"]["tris"],
                              "lod6": stats[s + ".lod6"]["tris"]},
                     "bo_tris": stats["bo_" + s]["tris"],
                     "bounds_min": stats[s]["min"], "bounds_max": stats[s]["max"],
                     "size": [round(b - a, 3) for a, b in zip(stats[s]["min"], stats[s]["max"])],
                     "pieces": {}}
            for sec, prefix, _ in lc.PLACEMENTS:
                if sec == s:
                    brief["pieces"][prefix] = brief["pieces"].get(prefix, 0) + 1
            with open(os.path.join(out, f"{s}_preview.json"), "w", encoding="utf-8") as f:
                json.dump(brief, f, indent=1)
            with open(os.path.join(out, f"{s}_expected.json"), "w", encoding="utf-8") as f:
                json.dump(expected_layout(s, apertures), f, indent=1)

        setup_workbench()
        for s in sections:
            render_section(s, out)
        blend, cols = inspect_blend(fbx, out, sections)
        log(f"[inspect] {blend}: {json.dumps(cols)}")
        summary = {"status": "ok", "fbx": fbx, "stats": stats, "warnings": sorted(set(lc.WARNINGS))}
    except Exception:
        log(traceback.format_exc())
        summary["trace"] = traceback.format_exc(limit=3)
    with open(os.path.join(out, "summary.json"), "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    with open(done, "w", encoding="utf-8") as f:
        json.dump({"status": summary["status"]}, f)
    log(f"[done] {summary['status']}")


if __name__ == "__main__":
    main()
