"""
Assemble Erebor buildings from the kit pieces in headless Blender (Phases 3 and 4
of docs/kitbash/erebor/blender-build-prompt.md, issue #797).

Each building is a list of placements written in the BLENDER IMPORT FRAME with
the join rules of docs/kitbash/erebor/design-patterns.md. The engine sees the
source FBX turned 180 deg about Z; an export from this frame keeps every join
and only turns the whole building.

--preview      render blockout views and write the brief numbers (pieces, triangles
               per LOD tier, bounds, collision coverage); the default mode
--export       merge each LOD tier and the collision body into one object and export
               AssetSources/Scenes/erebor/buildings/<name>.fbx (Lond Cirion export
               settings), its import note and <out>/<name>_expected.json (probe layout).
               Refuses before any live write when a piece or a collision body is
               missing, a tier's triangles differ from its parts, or a material keeps
               a Blender .NNN suffix. The FBX is staged in --out first; the previous
               live FBX is kept as <name>.fbx.bak-<mtime> (a copy).
               --preview --export runs both.
--frame-probe  export two untouched kit pieces to <out>/frame_probe.fbx with the same
               settings, for fbx_frame_compare.py against their source files
--inspect      build <out>/<name>_inspect.blend from the EXPORTED FBX: one collection
               per tier, only the base visible, flat viewport colours per material;
               runs alone (beside --export it would read the previous live FBX)

LOD tiers (Lond Cirion naming): base = each piece's LOD0; lod3 = each piece's
.lod2, else .lod4, else LOD0; lod6 = .lod5, else .lod4, else .lod2, else LOD0.
Unit-size props use their _lod2 as the base tier (their LOD0 is 28k to 52k tris);
a placement's base_lod moves its base tier down (a budget cut).

The MS-Store launcher detaches. Delete <out>\\DONE.txt before launching: the script
removes it only once it runs, so until then the file there is the previous verdict.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_assemble_erebor_buildings.py -- ^
        --building erebor_dwarf_house_01 --preview --out E:\\LOTRAOMAssets\\_export\\erebor\\buildings
"""

import argparse
import colorsys
import hashlib
import json
import math
import os
import re
import shutil
import sys
import traceback
from collections import Counter

import bpy
from mathutils import Matrix, Vector

SRC = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\erebor"
LOD_RE = re.compile(r"^(?P<base>.+?)[._]lod(?P<n>\d*)$", re.IGNORECASE)
PROPS = ("kys_dw_", "dw_pillar_moria", "sd_dw_")
TIER_LADDER = {"base": [], "lod3": ["2", "4"], "lod6": ["5", "4", "2"]}
# Pieces with no bo_ twin borrow the body of the same-shape twin (meshes.md "Collision bodies").
BO_STANDIN = {
    "sm_dw_roof_str_b1": "sm_dw_roof_str_a1", "sm_dw_roof_str_b2": "sm_dw_roof_str_a2",
    "sm_dw_roof_top_a2": "sm_dw_roof_top_a1", "sm_dw_roof_top_b2": "sm_dw_roof_top_b1",
    "sm_dw_roof_cor_in_a2": "sm_dw_roof_cor_in_a1", "sm_dw_roof_cor_in_b2": "sm_dw_roof_cor_in_b1",
    "sm_dw_roof_cor_out_a2": "sm_dw_roof_cor_out_a1", "sm_dw_roof_cor_out_b2": "sm_dw_roof_cor_out_b1",
    "sm_dw_roof_cor_tri_a2": "sm_dw_roof_cor_tri_a1", "sm_dw_roof_cor_tri_b2": "sm_dw_roof_cor_tri_b1",
    "sm_dw_roof_cor_tri_c1": "sm_dw_roof_cor_tri_c2",
    # its own body, under the truncated name bo_SM_DW_Castle_Wall_A1_Ramp_Mrln_0
    "sm_dw_castle_wall_a1_ramp_mrln_01": "sm_dw_castle_wall_a1_ramp_mrln_0",
}
# Thin trims with no body and nothing to stand on: no collision by design.
BO_NONE = {"sm_dw_roof_side_str_a1", "sm_dw_roof_side_str_a2",
           "sm_dw_roof_side_cor_out_a1", "sm_dw_roof_side_cor_out_a2"}


# --------------------------------------------------------------- building specs

def P(piece, loc, yaw=0, scale=(1, 1, 1), role="", base_lod=None):
    """One placement. base_lod (e.g. "2") makes that authored LOD the piece's base tier,
    and no lower tier uses a more detailed one: a triangle-budget cut per placement."""
    return {"piece": piece.lower(), "loc": tuple(float(v) for v in loc), "yaw": float(yaw),
            "scale": tuple(float(v) for v in scale), "role": role, "base_lod": base_lod}


def wall_line(piece_by_bay, start, axis, z=0.0, role="wall"):
    """Walls along X (axis 'x', yaw 0) or Y (axis 'y', yaw 90), one per 3 m bay;
    start = pivot of the first bay."""
    out = []
    for i, piece in enumerate(piece_by_bay):
        x, y = start
        if axis == "x":
            out.append(P(piece, (x + 3 * i, y, z), 0, role=role))
        else:
            out.append(P(piece, (x, y + 3 * i, z), 90, role=role))
    return out


def column(x, y, storeys, role="column"):
    """Clmn_3m_C on every storey with the square Clmn_Top_D at its foot and its head
    (the foot after Mike's review of the first Kit import, 2026-10-15)."""
    out = [P("sm_dw_wall_clmn_3m_c", (x, y, 3 * k), role=role) for k in range(storeys)]
    out += [P("sm_dw_wall_clmn_top_d", (x, y, z), scale=(1.1, 1.1, 1.2), role=role) for z in (0, 3 * storeys)]
    return out


def pilaster(x, y, storeys):
    """Clmn_3m_A (x1.5) on every storey with Clmn_Top_F on every storey line."""
    out = [P("sm_dw_wall_clmn_3m_a", (x, y, 3 * k), scale=(1.5, 1.5, 1.0), role="pilaster")
           for k in range(storeys)]
    out += [P("sm_dw_wall_clmn_top_f", (x, y, 3 * k), scale=(1.5, 1.5, 1.2), role="pilaster")
            for k in range(storeys + 1)]
    return out


# beam courses, one per storey line from the ground up: (piece, depth scale)
COURSES_ONE_STOREY = [("sm_dw_wall_beam_3m_b", 1.0), ("sm_dw_wall_beam_3m_c", 1.0)]
COURSES_TWO_STOREYS = [("sm_dw_wall_beam_3m_b", 1.0), ("sm_dw_wall_beam_3m_a", 1.1),
                       ("sm_dw_wall_beam_3m_c", 1.0)]


def courses(s, beams):
    """A beam course on every storey line (z 0, 3, 6 ...) over every ground-floor wall bay."""
    bays = [p for p in s if p["role"] == "wall" and p["loc"][2] == 0]
    return [P(beam, (w["loc"][0], w["loc"][1], 3 * k), w["yaw"], (1, depth, 1), role="course")
            for k, (beam, depth) in enumerate(beams) for w in bays]


def dwarf_house_01():
    """9 x 6 m one-storey gable house, dressed block (_C), slate roof. The floor slab
    sits on the terrain (no below-grade skirt, Mike 2026-10-15). Footprint centred on
    the origin at grade; front = -Y."""
    s = []
    front = ["sm_dw_wall_3m_win_c3", "sm_dw_wall_3m_door_c1", "sm_dw_wall_3m_win_c3"]
    back = ["sm_dw_wall_3m_win_c1", "sm_dw_wall_3m_c", "sm_dw_wall_3m_win_c1"]
    west = ["sm_dw_wall_3m_win_c2", "sm_dw_wall_3m_win_c2"]
    east = ["sm_dw_wall_3m_c", "sm_dw_wall_3m_win_c2"]
    s += wall_line(front, (-3, -3), "x")
    s += wall_line(back, (-3, 3), "x")
    s += wall_line(west, (-4.5, -1.5), "y")
    s += wall_line(east, (4.5, -1.5), "y")
    for x in (-4.5, 4.5):
        for y in (-3, 3):
            s += column(x, y, 1, role="corner")
    for x in (-1.5, 1.5):  # door pilasters on the front wall seams
        s += pilaster(x, -3, 1)
    s += courses(s, COURSES_ONE_STOREY)
    # gable roof, ridge along X at y 0, z 4.5
    for x in (-3, 0, 3):
        s.append(P("sm_dw_roof_str_a1", (x, -3, 3), 0, role="roof"))
        s.append(P("sm_dw_roof_str_a1", (x, 3, 3), 180, role="roof"))
        s.append(P("sm_dw_roof_side_str_a1", (x, 0, 4.5), 90, role="ridge"))
    for x in (-4.5, 4.5):
        s.append(P("sm_dw_roof_side_a1", (x, -1.5, 3), 0, role="gable"))
        s.append(P("sm_dw_roof_side_a1", (x, 1.5, 3), 180, role="gable"))
    # floor: carved stone (Ground_3m_A2, as dwarf_house_b); the C1 pavement reads as roof
    # tiles in game (Mike, 2026-10-15)
    for x in (-3, 0, 3):
        for y in (-1.5, 1.5):
            s.append(P("sm_dw_ground_3m_a2", (x, y, 0), role="floor"))
    return s


CORN_YAW = {(-1, -1): 180, (1, -1): 270, (-1, 1): 90, (1, 1): 0}  # chamfer wall per corner (dwarf_house_b)


def great_hall_01():
    """21 x 12 m two-storey hall (front 5 bays, 3 m chamfered corners), dressed block (_C),
    hipped slate roof rising 1.5 m to a flat deck, door flanked by double-height pilasters
    and two plain bays. No statues (Mike, 2026-10-15). Floor on the terrain. Centred on the
    origin; front = -Y."""
    hx, hy, c = 10.5, 6.0, 3.0           # half length, half width, chamfer
    ix, iy = hx - c, hy - c              # inner corner of each chamfer cell (7.5, 3)
    xs = [-6.0, -3.0, 0.0, 3.0, 6.0]     # long-wall bay pivots, span -7.5..7.5
    ys = [-1.5, 1.5]                     # end-wall bay pivots, span -3..3
    s = []
    walls = {
        (-hy, 0): ["win_c3", "c", "door_c1", "c", "win_c3"],
        (-hy, 3): ["win_c2", "win2_c1", "win_c2", "win2_c1", "win_c2"],
        (hy, 0): ["c", "win_c1", "c", "win_c1", "c"],
        (hy, 3): ["c", "win_c2", "c", "win_c2", "c"],
    }
    for (y, z), row in walls.items():
        s += wall_line([f"sm_dw_wall_3m_{w}" for w in row], (xs[0], y), "x", z=z)
    for x in (-hx, hx):
        for z, w in ((0, "win_c1"), (3, "win_c2")):
            s += wall_line([f"sm_dw_wall_3m_{w}"] * 2, (x, ys[0]), "y", z=z)
    corners = [(sx, sy) for sx in (-1, 1) for sy in (-1, 1)]
    for sx, sy in corners:
        yaw = CORN_YAW[(sx, sy)]
        s.append(P("sm_dw_wall_3m_corn_c", (sx * ix, sy * iy, 0), yaw, role="chamfer"))
        s.append(P("sm_dw_wall_3m_corn_win_c", (sx * ix, sy * iy, 3), yaw, role="chamfer"))
        # junction columns where the chamfer meets the long wall and the end wall
        for jx, jy in ((sx * ix, sy * hy), (sx * hx, sy * iy)):
            s += column(jx, jy, 2)
        # floor half tile in the chamfer cell, roof facet over it
        s.append(P("sm_dw_ground_3m_a1_corner", (sx * (hx - 1.5), sy * (hy - 1.5), 0), yaw + 90, role="floor"))
        s.append(P("sm_dw_roof_cor_tri_a1", (sx * ix, sy * iy, 6), yaw + 90, role="roof"))
    for x in (-1.5, 1.5):  # door pilasters, double height
        s += pilaster(x, -hy, 2)
    # courses: base, string (between storeys) and eave, on every wall bay and across the
    # chamfers (scaled 1.5 along the beam, turned 45 degrees, as in dwarf_house_b)
    s += courses(s, COURSES_TWO_STOREYS)
    for k, (beam, depth) in enumerate(COURSES_TWO_STOREYS):
        for sx, sy in corners:
            yaw = math.degrees(math.atan2(-sy, sx))
            s.append(P(beam, (sx * (hx - 1.5), sy * (hy - 1.5), 3 * k), yaw, (1.5, depth, 1), role="course"))
    # hipped roof: panels on every straight wall rise 1.5 m to the deck edge
    for x in xs:
        s.append(P("sm_dw_roof_str_a1", (x, -hy, 6), 0, role="roof"))
        s.append(P("sm_dw_roof_str_a1", (x, hy, 6), 180, role="roof"))
    for y in ys:
        s.append(P("sm_dw_roof_str_a1", (-hx, y, 6), 270, role="roof"))
        s.append(P("sm_dw_roof_str_a1", (hx, y, 6), 90, role="roof"))
    # flat deck at eave + 1.5, framed by strips on every tile edge, studs at the rib heads
    deck_z = 7.5
    for x in xs:
        for y in ys:
            s.append(P("sm_dw_roof_top_a1", (x, y, deck_z), role="deck"))
        for y in (-iy, 0.0, iy):
            s.append(P("sm_dw_roof_side_str_a1", (x, y, deck_z), 90, role="deck"))
    for x in (-7.5, -4.5, -1.5, 1.5, 4.5, 7.5):
        for y in ys:
            s.append(P("sm_dw_roof_side_str_a1", (x, y, deck_z), 0, role="deck"))
        # ribs on the panel seams, eave to deck
        s.append(P("sm_dw_roof_side_cor_out_a1", (x, -hy, 6), 0, role="rib"))
        s.append(P("sm_dw_roof_side_cor_out_a1", (x, hy, 6), 180, role="rib"))
        for y in (-iy, iy):
            s.append(P("sm_dw_ground_trim_corner_a1", (x, y, deck_z - 0.1), role="stud"))
    for y in (-iy, 0.0, iy):
        s.append(P("sm_dw_roof_side_cor_out_a1", (-hx, y, 6), 270, role="rib"))
        s.append(P("sm_dw_roof_side_cor_out_a1", (hx, y, 6), 90, role="rib"))
    for x in (-ix, ix):
        s.append(P("sm_dw_ground_trim_corner_a1", (x, 0.0, deck_z - 0.1), role="stud"))
    # floor: carved stone A1 (its chamfer half tile only exists in A1), at its flat 12-tri
    # .lod2: seen only through the door and windows (budget cut, 2026-10-15)
    for x in xs:
        for y in (-4.5, -1.5, 1.5, 4.5):
            s.append(P("sm_dw_ground_3m_a1", (x, y, 0), role="floor", base_lod="2"))
    for x in (-(hx - 1.5), hx - 1.5):
        for y in ys:
            s.append(P("sm_dw_ground_3m_a1", (x, y, 0), role="floor", base_lod="2"))
    return s


def barracks_01():
    """24 x 12 m two-storey barracks (8 x 4 bays), dressed block (_C), 12 m gable roof in stone
    (two rows of 3 m panels a side, ridge along X at z 9), two pilastered doors, dormitory
    windows on the upper storey. Floor on the terrain. Centred on the origin; front = -Y."""
    hx, hy = 12.0, 6.0
    xs = [-10.5, -7.5, -4.5, -1.5, 1.5, 4.5, 7.5, 10.5]
    ys = [-4.5, -1.5, 1.5, 4.5]
    s = []
    walls = {
        (-hy, 0): ["win_c3", "door_c1", "win_c3", "win_c1", "win_c1", "win_c3", "door_c1", "win_c3"],
        (-hy, 3): ["win2_c1"] * 8,
        (hy, 0): ["c", "win_c1", "c", "win_c1", "win_c1", "c", "win_c1", "c"],
        (hy, 3): ["win_c1", "c", "win_c1", "c", "c", "win_c1", "c", "win_c1"],
    }
    for (y, z), row in walls.items():
        s += wall_line([f"sm_dw_wall_3m_{w}" for w in row], (xs[0], y), "x", z=z)
    for x in (-hx, hx):
        s += wall_line([f"sm_dw_wall_3m_{w}" for w in ("win_c1", "c", "c", "win_c1")], (x, ys[0]), "y", z=0)
        s += wall_line([f"sm_dw_wall_3m_{w}" for w in ("win_c2", "win_c1", "win_c1", "win_c2")], (x, ys[0]), "y", z=3)
    # corner columns and the centre pilaster of each long wall, two high
    for cx, cy in [(sx * hx, sy * hy) for sx in (-1, 1) for sy in (-1, 1)] + [(0.0, -hy), (0.0, hy)]:
        s += column(cx, cy, 2)
    for x in (-9.0, -6.0, 6.0, 9.0):  # pilasters beside each door (doors at x -7.5 and 7.5)
        s += pilaster(x, -hy, 1)
    s += courses(s, COURSES_TWO_STOREYS)  # base, string between the storeys, eave
    # 12 m gable: two rows of stone panels a side, ridge strip at z 9, A1 + A2 gable wedges
    for x in xs:
        for y, z, yaw in ((-hy, 6, 0), (-3.0, 7.5, 0), (hy, 6, 180), (3.0, 7.5, 180)):
            s.append(P("sm_dw_roof_str_b1", (x, y, z), yaw, role="roof"))
        s.append(P("sm_dw_roof_side_str_a1", (x, 0.0, 9.0), 90, role="ridge"))
    for x in (-hx, hx):
        s.append(P("sm_dw_roof_side_a1", (x, -4.5, 6), 0, role="gable"))
        s.append(P("sm_dw_roof_side_a2", (x, -1.5, 6), 0, role="gable"))
        s.append(P("sm_dw_roof_side_a2", (x, 1.5, 6), 180, role="gable"))
        s.append(P("sm_dw_roof_side_a1", (x, 4.5, 6), 180, role="gable"))
    # floor: carved stone A2 at its flat .lod2 (seen only through doors and windows)
    for x in xs:
        for y in ys:
            s.append(P("sm_dw_ground_3m_a2", (x, y, 0), role="floor", base_lod="2"))
    return s


BUILDINGS = {"erebor_dwarf_house_01": dwarf_house_01, "erebor_great_hall_01": great_hall_01,
             "erebor_barracks_01": barracks_01}

# What the probe cannot read from the placements: the roof form, and for a gable its ridge
# axis and its rise above the eave. Eave, half sizes and chamfer come from the walls.
ROOF = {
    "erebor_dwarf_house_01": {"roof": "gable", "ridge_axis": "x", "rise": 1.5},
    "erebor_great_hall_01": {"roof": "hip"},
    "erebor_barracks_01": {"roof": "gable", "ridge_axis": "x", "rise": 3.0},
}


# --------------------------------------------------------------- kit loading

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


def split_kit_name(name):
    """A kit object name -> (kind, piece, lod): ("bo", piece, None) for bo_<piece>,
    ("lod", piece, "N") for <piece>.lodN or <piece>_lodN, else ("base", piece, None).
    A bare trailing .lod is a base object: an artist typo can make it the only copy of a
    sub-object (lond-cirion-buildings.md rule 3: a gable plate named .wall.lod)."""
    if name.startswith("bo_"):
        return "bo", name[3:], None
    m = LOD_RE.match(name)
    if m and m.group("n"):
        return "lod", m.group("base"), m.group("n")
    if m:
        return "base", m.group("base"), None
    return "base", name, None


def load_kit():
    """-> {piece: {"base": mesh, "lods": {n: mesh}, "bo": mesh|None}}, world-space meshes
    (node transform baked; normals flipped when a node mirrors), keyed by lower-case name."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    kit = {}
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
                if o.type != "MESH":
                    continue
                me = o.data.copy()
                me.transform(o.matrix_world)
                if o.matrix_world.determinant() < 0:  # a mirrored node turns the faces inside out
                    me.flip_normals()
                kind, piece, n = split_kit_name(o.name.lower())
                e = kit.setdefault(piece, {"base": None, "lods": {}, "bo": None})
                if kind == "bo":
                    e["bo"] = me
                elif kind == "lod":
                    e["lods"][n] = me
                else:
                    e["base"] = me
            for o in new:
                bpy.data.objects.remove(o, do_unlink=True)
    # Blender renames a material met again in a later file to "<name>.NNN"; the
    # editor binds slots by exact name, so fold every duplicate into its original.
    for mat in list(bpy.data.materials):
        m = re.match(r"^(.*)\.\d{3}$", mat.name)
        if not m:
            continue
        base = bpy.data.materials.get(m.group(1))
        if base is not None:
            mat.user_remap(base)
            bpy.data.materials.remove(mat)
        else:
            mat.name = m.group(1)
    return kit


def tier_mesh(entry, piece, tier, base_lod=None):
    if piece.startswith(PROPS):
        ladder = {"base": ["2"], "lod3": ["3", "2"], "lod6": ["4", "3", "2"]}[tier]
    else:
        ladder = TIER_LADDER[tier]
    if base_lod is not None:
        if base_lod not in entry["lods"]:
            raise ValueError(f"{piece}: no .lod{base_lod} for base_lod")
        ladder = [base_lod] if tier == "base" else [n for n in ladder if int(n) >= int(base_lod)] or [base_lod]
    for n in ladder:
        if n in entry["lods"]:
            return entry["lods"][n], f"lod{n}"
    return entry["base"], "lod0"


def bo_mesh(kit, piece):
    if piece in BO_NONE:
        return None, "none by design"
    e = kit.get(piece)
    if e and e["bo"] is not None:
        return e["bo"], "own"
    alt = BO_STANDIN.get(piece)
    if alt and kit.get(alt, {}).get("bo") is not None:
        return kit[alt]["bo"], f"borrowed from {alt}"
    return None, "MISSING"


def matrix(p):
    return (Matrix.Translation(Vector(p["loc"])) @ Matrix.Rotation(math.radians(p["yaw"]), 4, "Z")
            @ Matrix.Diagonal(Vector((*p["scale"], 1.0))))


def tris(me):
    me.calc_loop_triangles()
    return len(me.loop_triangles)


# --------------------------------------------------------------- preview

def setup_render():
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
    cd = bpy.data.cameras.new("cam")
    cd.type = "ORTHO"
    cd.clip_end = 10000.0
    cam = bpy.data.objects.new("cam", cd)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return scene, cam


def render_views(objs, cam, prefix, ground_z=0.0):
    mn = Vector((1e9,) * 3)
    mx = Vector((-1e9,) * 3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            mn, mx = Vector(map(min, mn, w)), Vector(map(max, mx, w))
    vis_mn = Vector((mn.x, mn.y, max(mn.z, ground_z - 0.5)))  # frame the part above grade
    centre = (vis_mn + mx) / 2
    span = (mx - vis_mn).length
    views = {
        "q1": (Vector((-0.6, -0.8, 0.55)), span * 1.05),
        "q2": (Vector((0.6, 0.8, 0.55)), span * 1.05),
        "front": (Vector((0, -1, 0)), max(mx.x - mn.x, (mx.z - vis_mn.z) * 1.4) * 1.15),
        "side": (Vector((1, 0, 0)), max(mx.y - mn.y, (mx.z - vis_mn.z) * 1.4) * 1.15),
        "top": (Vector((0, 0, 1)), max(mx.x - mn.x, (mx.y - mn.y) * 1.4) * 1.15),
    }
    for name, (d, scale) in views.items():
        d = d.normalized()
        cam.location = centre + d * span * 3
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.ortho_scale = scale
        bpy.context.scene.render.filepath = f"{prefix}_{name}.png"
        bpy.ops.render.render(write_still=True)
    return mn, mx


def preview(name, spec, kit, out):
    scene, cam = setup_render()
    missing = sorted({p["piece"] for p in spec if p["piece"] not in kit or kit[p["piece"]]["base"] is None})
    if missing:
        raise RuntimeError(f"unknown pieces: {missing}")
    tier_tris = Counter()
    tier_src = Counter()
    bo_status = Counter()
    bo_tris = 0
    objs = []
    for p in spec:
        e = kit[p["piece"]]
        for tier in TIER_LADDER:
            me, src = tier_mesh(e, p["piece"], tier, p["base_lod"])
            tier_tris[tier] += tris(me)
            tier_src[(tier, src)] += 1
        bme, how = bo_mesh(kit, p["piece"])
        bo_status[how.split(" from ")[0]] += 1
        if bme is not None:
            bo_tris += tris(bme)
        me, _ = tier_mesh(e, p["piece"], "base", p["base_lod"])
        o = bpy.data.objects.new(p["piece"], me)
        o.matrix_world = matrix(p)
        scene.collection.objects.link(o)
        objs.append(o)
    bpy.context.view_layer.update()
    mn, mx = render_views(objs, cam, os.path.join(out, name))
    report = {
        "building": name, "placements": len(spec),
        "pieces": dict(Counter(p["piece"] for p in spec).most_common()),
        "roles": dict(Counter(p["role"] for p in spec)),
        "tris": dict(tier_tris), "bo_tris": bo_tris,
        "tier_sources": {f"{t}:{s}": n for (t, s), n in sorted(tier_src.items())},
        "bo_coverage": dict(bo_status),
        "bounds_min": [round(v, 3) for v in mn], "bounds_max": [round(v, 3) for v in mx],
        "size": [round(v, 3) for v in (mx - mn)],
    }
    with open(os.path.join(out, f"{name}_preview.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
    log(f"[preview] {name}: {len(spec)} placements, tris {dict(tier_tris)}, bo {bo_tris}, "
        f"size {report['size']}, bo coverage {dict(bo_status)}")
    return report


# --------------------------------------------------------------- export

TIER_SUFFIX = {"base": "", "lod3": ".lod3", "lod6": ".lod6"}
EXPORT_DIR = os.path.join(SRC, "buildings")


def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)


def join_tier(name, spec, kit, tier):
    """One merged object for a tier ('base', 'lod3', 'lod6' or 'bo');
    -> (object, expected tris, {source: placements})."""
    scene = bpy.context.scene
    parts = []
    expect = 0
    sources = Counter()
    for p in spec:
        if tier == "bo":
            me, src = bo_mesh(kit, p["piece"])
            if me is None:
                continue
        else:
            me, src = tier_mesh(kit[p["piece"]], p["piece"], tier, p["base_lod"])
        sources[src.split(" from ")[0]] += 1
        expect += tris(me)
        o = bpy.data.objects.new("part", me.copy())
        o.matrix_world = matrix(p)
        if o.matrix_world.determinant() < 0:
            o.data.flip_normals()
        scene.collection.objects.link(o)
        parts.append(o)
    target = bpy.data.objects.new("join_target", bpy.data.meshes.new("join_target"))
    scene.collection.objects.link(target)
    bpy.context.view_layer.update()
    select_only(parts + [target])
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    out_name = f"bo_{name}" if tier == "bo" else name + TIER_SUFFIX[tier]
    joined.name = joined.data.name = out_name
    if tier == "bo":
        joined.data.materials.clear()
        joined.data.materials.append(bpy.data.materials.get("stone") or bpy.data.materials.new("stone"))
    return joined, expect, dict(sources)


def export_fbx(objs, path):
    select_only(objs)
    # Same settings as blender_assemble_lond_cirion_buildings.py, whose output imports
    # correctly in the Modding Kit; do not change bake_space_transform (lond-cirion-buildings.md).
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"},
                             use_mesh_modifiers=True, bake_space_transform=True,
                             add_leaf_bones=False, path_mode="AUTO")


def export_problems(stats, missing_bodies):
    """What must stop an export: a tier whose triangles differ from the sum of its parts
    (a dropped part), a material with a Blender .NNN suffix (the Kit binds by exact name),
    a piece whose collision body is missing."""
    problems = []
    for oname, s in stats.items():
        if s["tris"] != s["expected_sum"]:
            problems.append(f"{oname}: tris {s['tris']} != part sum {s['expected_sum']}")
        dup = [m for m in s["materials"] if re.search(r"\.\d{3}$", m)]
        if dup:
            problems.append(f"{oname}: duplicate-suffixed materials {dup}")
    if missing_bodies:
        problems.append(f"no collision body for {sorted(set(missing_bodies))}")
    return problems


def export(name, spec, kit, out):
    """Build every tier, refuse on any problem, then replace the live FBX. Nothing in the live
    folder changes until the new FBX exists in full (staged in --out)."""
    if os.path.normcase(os.path.abspath(out)) == os.path.normcase(os.path.abspath(EXPORT_DIR)):
        raise RuntimeError(f"--out is the live folder {EXPORT_DIR}: stage the export elsewhere")
    missing = sorted({p["piece"] for p in spec if p["piece"] not in kit or kit[p["piece"]]["base"] is None})
    if missing:
        raise RuntimeError(f"unknown pieces: {missing}")
    expected = probe_layout(name, spec)  # ROOF and catalog errors surface before anything is built
    missing_bodies = [p["piece"] for p in spec if bo_mesh(kit, p["piece"])[1] == "MISSING"]
    stats, objs, tier_sources = {}, [], {}
    for tier in ("base", "lod3", "lod6", "bo"):
        o, expect, sources = join_tier(name, spec, kit, tier)
        got = tris(o.data)
        mn = Vector(map(min, *[o.matrix_world @ Vector(c) for c in o.bound_box]))
        mx = Vector(map(max, *[o.matrix_world @ Vector(c) for c in o.bound_box]))
        stats[o.name] = {"tris": got, "expected_sum": expect,
                         "materials": [m.name for m in o.data.materials if m],
                         "min": [round(v, 3) for v in mn], "max": [round(v, 3) for v in mx]}
        tier_sources[o.name] = sources
        objs.append(o)
        log(f"[export] {o.name}: {stats[o.name]}")
    problems = export_problems(stats, missing_bodies)
    if problems:
        raise RuntimeError("export refused: " + "; ".join(problems))
    staged = os.path.join(out, f"{name}.fbx")
    export_fbx(objs, staged)
    os.makedirs(EXPORT_DIR, exist_ok=True)
    path = os.path.join(EXPORT_DIR, f"{name}.fbx")
    if os.path.exists(path):
        bak = path + ".bak-" + str(int(os.path.getmtime(path)))
        shutil.copy2(path, bak)
        log(f"[export] previous file kept as {bak}")
    shutil.copy2(staged, path)
    log(f"[export] wrote {path}")
    with open(os.path.join(out, f"{name}_expected.json"), "w", encoding="utf-8") as f:
        json.dump(expected, f, indent=1)
    write_import_note(name, path, stats, spec, tier_sources)
    report = {"building": name, "fbx": path, "objects": stats, "tier_sources": tier_sources, "problems": []}
    with open(os.path.join(out, f"{name}_export.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
    return report


FRAME_PROBE_PIECES = ("sm_dw_roof_str_a1", "sm_dw_wall_3m_corn_a")  # asymmetric in X and in Y


def frame_probe(kit, out):
    """Two untouched kit pieces exported with the building settings: fbx_frame_compare.py of
    this file against their source FBX shows whether the engine reads an export like the kit."""
    probe = []
    for piece in FRAME_PROBE_PIECES:
        o = bpy.data.objects.new(piece, kit[piece]["base"].copy())
        bpy.context.scene.collection.objects.link(o)
        probe.append(o)
    path = os.path.join(out, "frame_probe.fbx")
    export_fbx(probe, path)
    log(f"[frame-probe] wrote {path}")


def inspect(name, out):
    """<out>/<name>_inspect.blend from the EXPORTED FBX: one collection per tier, only the
    base tier visible, flat viewport colours per material (the textures do not resolve)."""
    path = os.path.join(EXPORT_DIR, f"{name}.fbx")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=path)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=path)
    scene = bpy.context.scene
    for label, oname in (("base", name), ("lod3", name + ".lod3"), ("lod6", name + ".lod6"),
                         ("bo", "bo_" + name)):
        obj = bpy.data.objects.get(oname)
        if obj is None:
            raise RuntimeError(f"{path}: no object {oname}")
        col = bpy.data.collections.new(f"{label}  ({oname})")
        scene.collection.children.link(col)
        for c in list(obj.users_collection):
            c.objects.unlink(obj)
        col.objects.link(obj)
        if label != "base":
            bpy.context.view_layer.layer_collection.children[col.name].hide_viewport = True
    for m in bpy.data.materials:
        m.diffuse_color = mat_color(m.name)
    blend = os.path.join(out, f"{name}_inspect.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    log(f"[inspect] wrote {blend}")


CATALOG = r"E:\LOTRAOMAssets\_export\erebor\catalog\erebor_catalog.json"


def planned_openings(spec):
    """World-space rectangles of every see-through opening the plan places in a wall
    (catalog apertures through local Y, moved by the placement), for the probe."""
    with open(CATALOG, encoding="utf-8") as f:
        pieces = {p["name"].lower(): p for p in json.load(f)["pieces"]}
    out = []
    for p in spec:
        if p["role"] != "wall":
            continue
        for h in pieces[p["piece"]]["apertures"]:
            if h["through"] != "Y":
                continue
            yaw = round(p["yaw"]) % 360
            if yaw not in (0, 90, 180, 270):
                raise ValueError(f"{p['piece']}: wall yaw {yaw} not handled")
            sign = 1 if yaw in (0, 90) else -1
            u0, u1 = sorted((sign * h["u_min"], sign * h["u_max"]))
            axis = "x" if yaw in (0, 180) else "y"  # the wall runs along this world axis
            along = p["loc"][0] if axis == "x" else p["loc"][1]
            plane = p["loc"][1] if axis == "x" else p["loc"][0]
            out.append({"piece": p["piece"], "runs_along": axis, "plane": plane,
                        "u_min": round(along + u0, 3), "u_max": round(along + u1, 3),
                        "z_min": round(p["loc"][2] + h["z_min"], 3),
                        "z_max": round(p["loc"][2] + h["z_max"], 3), "area": h["area"]})
    return out


def probe_layout(name, spec):
    """The probe's layout for blender_probe_erebor_building.py: the roof form (ROOF), the
    planned openings, every straight wall line (span and height), every chamfer wall, and the
    eave, half sizes, chamfer and gable ridge height derived from them."""
    roof = dict(ROOF[name])
    rise = roof.pop("rise", None)
    out = dict(roof)
    out["openings"] = planned_openings(spec)
    lines = {}
    for p in spec:
        if p["role"] != "wall":
            continue
        yaw = round(p["yaw"]) % 180
        axis, plane, u = ("x", p["loc"][1], p["loc"][0]) if yaw == 0 else ("y", p["loc"][0], p["loc"][1])
        w = lines.setdefault((axis, plane), {"runs_along": axis, "plane": plane, "u_min": u - 1.5,
                                             "u_max": u + 1.5, "z_min": p["loc"][2], "z_max": p["loc"][2] + 3})
        w["u_min"], w["u_max"] = min(w["u_min"], u - 1.5), max(w["u_max"], u + 1.5)
        w["z_min"], w["z_max"] = min(w["z_min"], p["loc"][2]), max(w["z_max"], p["loc"][2] + 3)
    out["walls"] = sorted(lines.values(), key=lambda w: (w["runs_along"], w["plane"]))
    out["chamfers"] = [{"piece": p["piece"], "pivot": list(p["loc"]), "yaw": p["yaw"], "z": p["loc"][2],
                        "window": "_win" in p["piece"]} for p in spec if p["role"] == "chamfer"]
    out["eave"] = max(w["z_max"] for w in out["walls"])
    out["half"] = [max(abs(w["plane"]) for w in out["walls"] if w["runs_along"] == axis) for axis in ("y", "x")]
    out["chamfer"] = (out["half"][0] - abs(out["chamfers"][0]["pivot"][0])) if out["chamfers"] else 0.0
    if rise is not None:
        out["ridge_z"] = out["eave"] + rise
    return out


ASSETS = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Assets"


def unpackaged_materials(names):
    """Materials with no <name>_mtl.tpac anywhere under TAOM_Map/Assets: the Kit may not bind them."""
    have = set()
    for _, _, files in os.walk(ASSETS):
        have.update(f.lower()[:-len("_mtl.tpac")] for f in files if f.lower().endswith("_mtl.tpac"))
    return [n for n in names if n.lower() not in have]


def write_import_note(name, fbx, stats, spec, tier_sources):
    base = stats[name]
    mats = base["materials"]
    missing = unpackaged_materials(mats)
    lines = [
        f"# {name}: Modding Kit import note",
        "",
        "Built by `tools/oneoff/blender_assemble_erebor_buildings.py` (issue #797) from the",
        "Erebor kit pieces. Recipe, brief and probe results: `docs/kitbash/erebor-buildings.md`.",
        "",
        "## Objects in the FBX",
        "",
        "| Object | Triangles | Bounds min (m) | Bounds max (m) |",
        "|---|---|---|---|",
    ]
    for oname, s in stats.items():
        lines.append(f"| `{oname}` | {s['tris']} | {s['min']} | {s['max']} |")
    lines += [
        "",
        "Bounds are in the Blender frame (Z up, front of the building facing -Y).",
        "",
        "## Materials to bind (exact names)",
        "",
    ] + [f"- `{m}`" + (" **(no `_mtl.tpac` found in TAOM_Map)**" if m in missing else "") for m in mats] + [
        "- `bo_` body: `stone` (the physics material the kit bodies use)",
        "",
    ] + ([
        "**Unpackaged materials.** The names marked above come from a source FBX and no material",
        "package with that name exists under `TAOM_Map/Assets`. In the Kit, bind those faces to the",
        "material the shipped piece uses (open its metamesh, for example `sd_dw_ih_statue_a`), or",
        "report the name so the export can carry it.",
        "",
    ] if missing else []) + [
        "## Placement",
        "",
        "- Origin: the centre of the footprint at grade. Wall bases and the underside of the",
        "  floor slab sit at z 0, so the floor sits on the terrain (top of slab 0.10 to 0.12 m).",
        f"  Lowest point z {base['min'][2]} (base beam and pilaster bases sink into the terrain).",
        "- Expected heading: the engine reads Erebor FBX turned 180 degrees about Z from Blender,",
        "  so the front (door side) should face +Y in the editor at rotation 0. The frame rule",
        "  predicts this; the editor has not yet confirmed it.",
        f"- {len(spec)} kit pieces merged into one entity: one prefab entity instead of {len(spec)}.",
        "",
        "## LOD tiers",
        "",
        "Which authored level each tier takes, counted over the placements (`lod0` is a piece's",
        "full-detail mesh; a piece with no lower level keeps it in every tier):",
        "",
    ] + [f"- `{oname}`: " + ", ".join(f"{n} {src}" for src, n in sorted(src_counts.items()))
         for oname, src_counts in tier_sources.items()]
    with open(os.path.splitext(fbx)[0] + "_import.md", "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def main():
    global OUT
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    # clear the previous verdict before anything can fail, argument errors included
    if "--out" in argv and argv.index("--out") + 1 < len(argv):
        stale = os.path.join(argv[argv.index("--out") + 1], "DONE.txt")
        if os.path.exists(stale):
            os.remove(stale)
    ap = argparse.ArgumentParser()
    ap.add_argument("--building", required=True, choices=sorted(BUILDINGS))
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--export", action="store_true")
    ap.add_argument("--frame-probe", action="store_true")
    ap.add_argument("--inspect", action="store_true")
    ap.add_argument("--out", required=True)
    args = ap.parse_args(argv)
    OUT = args.out
    os.makedirs(OUT, exist_ok=True)
    done = os.path.join(OUT, "DONE.txt")
    if os.path.exists(done):  # --out=<dir> is not caught by the early clear
        os.remove(done)
    status = "ok"
    try:
        if args.inspect and (args.preview or args.export or args.frame_probe):
            raise RuntimeError("--inspect reads the live FBX and runs alone")
        if args.inspect:
            inspect(args.building, OUT)
        else:
            spec = BUILDINGS[args.building]()
            kit = load_kit()
            if args.preview or not (args.export or args.frame_probe):
                preview(args.building, spec, kit, OUT)
            if args.export:
                export(args.building, spec, kit, OUT)
            if args.frame_probe:
                frame_probe(kit, OUT)
    except Exception:
        log(traceback.format_exc())
        status = "failed"
    with open(done, "w", encoding="utf-8") as f:
        json.dump({"status": status}, f)
    log(f"[done] {status}")


if __name__ == "__main__":
    main()
