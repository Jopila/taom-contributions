"""
Assemble Erebor buildings from the kit pieces in headless Blender (Phases 3 and 4
of docs/kitbash/erebor/blender-build-prompt.md, issue #797).

Each building is a list of placements written in the BLENDER IMPORT FRAME with
the join rules of docs/kitbash/erebor/design-patterns.md. The engine sees the
source FBX turned 180 deg about Z; an export from this frame keeps every join
and only turns the whole building.

--preview   render blockout views and write the brief numbers (pieces, triangles
            per LOD tier, bounds, collision coverage); exports nothing
--export    merge each LOD tier and the collision body into one object and export
            AssetSources/Scenes/erebor/buildings/<name>.fbx (Lond Cirion export
            settings); also writes <out>/frame_probe.fbx (two untouched kit pieces)
            for the raw-FBX frame compare

LOD tiers (Lond Cirion naming): base = each piece's LOD0; lod3 = each piece's
.lod2, else .lod4, else LOD0; lod6 = .lod5, else .lod4, else .lod2, else LOD0.
Unit-size props use their _lod2 as the base tier (their LOD0 is 28k to 52k tris).

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
}
# Thin trims with no body and nothing to stand on: no collision by design.
BO_NONE = {"sm_dw_roof_side_str_a1", "sm_dw_roof_side_str_a2",
           "sm_dw_roof_side_cor_out_a1", "sm_dw_roof_side_cor_out_a2"}


# --------------------------------------------------------------- building specs

def P(piece, loc, yaw=0, scale=(1, 1, 1), role=""):
    return {"piece": piece.lower(), "loc": tuple(float(v) for v in loc), "yaw": float(yaw),
            "scale": tuple(float(v) for v in scale), "role": role}


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
    # corners: column + capital (dwarf_house_a)
    for x in (-4.5, 4.5):
        for y in (-3, 3):
            s.append(P("sm_dw_wall_clmn_3m_c", (x, y, 0), role="corner"))
            s.append(P("sm_dw_wall_clmn_top_d", (x, y, 3), scale=(1.1, 1.1, 1.2), role="corner"))
    # door pilasters on the front wall seams
    for x in (-1.5, 1.5):
        s.append(P("sm_dw_wall_clmn_3m_a", (x, -3, 0), scale=(1.5, 1.5, 1.0), role="pilaster"))
        s.append(P("sm_dw_wall_clmn_top_f", (x, -3, 0), scale=(1.5, 1.5, 1.2), role="pilaster"))
        s.append(P("sm_dw_wall_clmn_top_f", (x, -3, 3), scale=(1.5, 1.5, 1.2), role="pilaster"))
    # base and eave courses on every wall bay
    for w in [p for p in s if p["role"] == "wall"]:
        s.append(P("sm_dw_wall_beam_3m_b", (w["loc"][0], w["loc"][1], 0), w["yaw"], role="course"))
        s.append(P("sm_dw_wall_beam_3m_c", (w["loc"][0], w["loc"][1], 3), w["yaw"], role="course"))
    # gable roof, ridge along X at y 0, z 4.5
    for x in (-3, 0, 3):
        s.append(P("sm_dw_roof_str_a1", (x, -3, 3), 0, role="roof"))
        s.append(P("sm_dw_roof_str_a1", (x, 3, 3), 180, role="roof"))
        s.append(P("sm_dw_roof_side_str_a1", (x, 0, 4.5), 90, role="ridge"))
    for x in (-4.5, 4.5):
        s.append(P("sm_dw_roof_side_a1", (x, -1.5, 3), 0, role="gable"))
        s.append(P("sm_dw_roof_side_a1", (x, 1.5, 3), 180, role="gable"))
    # floor
    for x in (-3, 0, 3):
        for y in (-1.5, 1.5):
            s.append(P("sm_dw_ground_3m_c1", (x, y, 0), role="floor"))
    return s


BUILDINGS = {"erebor_dwarf_house_01": dwarf_house_01}


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


def load_kit():
    """-> {piece: {"base": mesh, "lods": {n: mesh}, "bo": mesh|None}}, world-space meshes
    (node transform baked), keyed by lower-case base name."""
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
                name = o.name.lower()
                if name.startswith("bo_"):
                    kit.setdefault(name[3:], {"base": None, "lods": {}, "bo": None})["bo"] = me
                    continue
                m = LOD_RE.match(name)
                if m:
                    kit.setdefault(m.group("base"), {"base": None, "lods": {}, "bo": None})["lods"][m.group("n")] = me
                else:
                    kit.setdefault(name, {"base": None, "lods": {}, "bo": None})["base"] = me
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


def tier_mesh(entry, piece, tier):
    if piece.startswith(PROPS):
        ladder = {"base": ["2"], "lod3": ["3", "2"], "lod6": ["4", "3", "2"]}[tier]
    else:
        ladder = TIER_LADDER[tier]
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
        up = "Y" if name != "top" else "Y"
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", up).to_euler()
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
            me, src = tier_mesh(e, p["piece"], tier)
            tier_tris[tier] += tris(me)
            tier_src[(tier, src)] += 1
        bme, how = bo_mesh(kit, p["piece"])
        bo_status[how.split(" from ")[0]] += 1
        if bme is not None:
            bo_tris += tris(bme)
        me, _ = tier_mesh(e, p["piece"], "base")
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
    """One merged object for a tier ('base', 'lod3', 'lod6' or 'bo'); -> (object, expected tris)."""
    scene = bpy.context.scene
    parts = []
    expect = 0
    for p in spec:
        if tier == "bo":
            me, _ = bo_mesh(kit, p["piece"])
            if me is None:
                continue
        else:
            me, _ = tier_mesh(kit[p["piece"]], p["piece"], tier)
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
    return joined, expect


def export_fbx(objs, path):
    select_only(objs)
    # Same settings as blender_assemble_lond_cirion_buildings.py, whose output imports
    # correctly in the Modding Kit; do not change bake_space_transform (lond-cirion-buildings.md).
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"},
                             use_mesh_modifiers=True, bake_space_transform=True,
                             add_leaf_bones=False, path_mode="AUTO")


def export(name, spec, kit, out):
    os.makedirs(EXPORT_DIR, exist_ok=True)
    path = os.path.join(EXPORT_DIR, f"{name}.fbx")
    if os.path.exists(path):
        bak = path + ".bak-" + str(int(os.path.getmtime(path)))
        os.replace(path, bak)
        log(f"[export] previous file kept as {bak}")
    stats, objs, warnings = {}, [], []
    for tier in ("base", "lod3", "lod6", "bo"):
        o, expect = join_tier(name, spec, kit, tier)
        got = tris(o.data)
        mn = Vector(map(min, *[o.matrix_world @ Vector(c) for c in o.bound_box]))
        mx = Vector(map(max, *[o.matrix_world @ Vector(c) for c in o.bound_box]))
        stats[o.name] = {"tris": got, "expected_sum": expect,
                         "materials": [m.name for m in o.data.materials if m],
                         "min": [round(v, 3) for v in mn], "max": [round(v, 3) for v in mx]}
        if got != expect:
            warnings.append(f"{o.name}: tris {got} != part sum {expect}")
        dup = [m.name for m in o.data.materials if m and re.search(r"\.\d{3}$", m.name)]
        if dup:
            warnings.append(f"{o.name}: duplicate-suffixed materials {dup}")
        objs.append(o)
        log(f"[export] {o.name}: {stats[o.name]}")
    export_fbx(objs, path)
    log(f"[export] wrote {path}")
    # frame probe: two asymmetric kit pieces exported untouched with the same settings,
    # so a raw-FBX compare against their source files shows how the engine will read us
    probe = []
    for piece in ("sm_dw_roof_str_a1", "sm_dw_wall_3m_corn_a"):
        o = bpy.data.objects.new(piece, kit[piece]["base"].copy())
        bpy.context.scene.collection.objects.link(o)
        probe.append(o)
    export_fbx(probe, os.path.join(out, "frame_probe.fbx"))
    expected = planned_openings(spec)
    with open(os.path.join(out, f"{name}_expected.json"), "w", encoding="utf-8") as f:
        json.dump(expected, f, indent=1)
    write_import_note(name, path, stats, spec)
    report = {"building": name, "fbx": path, "objects": stats, "warnings": warnings}
    with open(os.path.join(out, f"{name}_export.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
    for w in warnings:
        log(f"[warn] {w}")
    return report


CATALOG = r"E:\LOTRAOMAssets\_export\erebor\catalog\erebor_catalog.json"


def planned_openings(spec):
    """World-space rectangles of every see-through opening the plan places in a wall
    (catalog apertures through local Y, moved by the placement), for the probe."""
    pieces = {p["name"].lower(): p for p in json.load(open(CATALOG, encoding="utf-8"))["pieces"]}
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


def write_import_note(name, fbx, stats, spec):
    base = stats[name]
    mats = base["materials"]
    lines = [
        f"# {name}: Modding Kit import note",
        "",
        f"Built by `tools/oneoff/blender_assemble_erebor_buildings.py` (issue #797) from the",
        "Erebor kit pieces. Brief and evidence: `docs/kitbash/erebor/design-patterns.md`.",
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
        "## Materials to bind (exact names, all existing Erebor materials)",
        "",
    ] + [f"- `{m}`" for m in mats] + [
        "- `bo_` body: `stone` (the physics material the kit bodies use)",
        "",
        "## Placement",
        "",
        "- Origin: the centre of the footprint at grade. Wall bases and the underside of the",
        "  floor slab sit at z 0, so the floor sits on the terrain (top of slab 0.10 to 0.12 m).",
        f"  Lowest point z {base['min'][2]} (base beam and pilaster bases sink into the terrain).",
        "- The engine reads Erebor FBX turned 180 degrees about Z from Blender, so the front",
        "  (door side) faces +Y in the editor at rotation 0.",
        f"- {len(spec)} kit pieces merged into one entity: one prefab entity instead of {len(spec)}.",
        "",
        "## LOD tiers",
        "",
        "`.lod3` uses each piece's `.lod2` (else `.lod4`, else LOD0); `.lod6` uses `.lod5`",
        "(else `.lod4`, `.lod2`, LOD0). Roof pieces and plain walls have no authored LODs,",
        "so they stay at full detail in every tier.",
    ]
    with open(os.path.splitext(fbx)[0] + "_import.md", "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def main():
    global OUT
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--building", required=True, choices=sorted(BUILDINGS))
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--export", action="store_true")
    ap.add_argument("--out", required=True)
    args = ap.parse_args(argv)
    OUT = args.out
    os.makedirs(OUT, exist_ok=True)
    done = os.path.join(OUT, "DONE.txt")
    if os.path.exists(done):
        os.remove(done)
    status = "ok"
    try:
        spec = BUILDINGS[args.building]()
        kit = load_kit()
        if args.export:
            export(args.building, spec, kit, OUT)
        else:
            preview(args.building, spec, kit, OUT)
    except Exception:
        log(traceback.format_exc())
        status = "failed"
    with open(done, "w", encoding="utf-8") as f:
        json.dump({"status": status}, f)
    log(f"[done] {status}")


main()
