"""
Measure every piece of the Erebor dwarven kitbash (meshes/, walls/, statues/)
and write a machine catalog plus per-piece view sheets. Phase 1 of
docs/kitbash/erebor/blender-build-prompt.md.

Per base piece (LOD and bo_ objects are folded into it):
    names, LOD ladder, bo_ twin or orphan, node transform, world and local
    bbox, pivot class per axis, tris, materials, normal-area buckets, slopes,
    triangle split per side (detail side), walkable top from down-rays,
    climb profile, see-through apertures from ray grids, 3 m grid fit.

Geometry is measured in WORLD space (node transform applied), because the
Bannerlord importer bakes the node transform (lond-cirion-buildings.md,
"The export's non-identity node transform is not a bug").

Output: E:\\LOTRAOMAssets\\_export\\erebor\\catalog\\
    erebor_catalog.json
    sheets\\<file>_p<n>.png      numbered contact sheets (3/4 view)
    views\\<piece>.png           front(-Y) / back(+Y) / side(+X) / top, 1 m grid,
                                 red dot = pivot
    _report\\DONE.txt + log.txt  (MS-Store launcher detaches; log(), not print)

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_catalog_erebor.py [-- --only <substring>] [--no-render]
"""

import colorsys
import hashlib
import json
import math
import os
import re
import sys
import time
import traceback
from collections import Counter, defaultdict, deque

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

SRC = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\erebor"
FOLDERS = ["meshes", "walls", "statues"]
OUT = r"E:\LOTRAOMAssets\_export\erebor\catalog"
PAGE = 36  # pieces per contact sheet (6x6)

LOD_RE = re.compile(r"^(?P<base>.+?)[._]lod(?P<n>\d*)$", re.IGNORECASE)

LOG_LINES = []
REPORT_DIR = None


def log(msg):
    LOG_LINES.append(str(msg))
    print(msg)
    if REPORT_DIR:
        with open(os.path.join(REPORT_DIR, "log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG_LINES))


def r3(v):
    return round(float(v), 3)


def vec3(v):
    return [r3(x) for x in v]


# --------------------------------------------------------------- naming

def split_name(name):
    """-> (kind, base, lod) with kind in base|lod|bo."""
    if name.lower().startswith("bo_"):
        return "bo", name[3:], None
    m = LOD_RE.match(name)
    if m:
        return "lod", m.group("base"), m.group("n")  # "" = bare .lod
    return "base", name, None


# --------------------------------------------------------------- geometry

def world_geometry(obj):
    """World-space verts and polygons of the evaluated mesh."""
    deps = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(deps)
    me = ev.to_mesh()
    mw = obj.matrix_world
    verts = [mw @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    me.calc_loop_triangles()
    tris = [tuple(t.vertices) for t in me.loop_triangles]
    mats = [m.name for m in obj.data.materials if m]
    ev.to_mesh_clear()
    return verts, polys, tris, mats


def bbox(verts):
    mn = Vector((min(v.x for v in verts), min(v.y for v in verts), min(v.z for v in verts)))
    mx = Vector((max(v.x for v in verts), max(v.y for v in verts), max(v.z for v in verts)))
    return mn, mx


def pivot_class(mn, mx, origin):
    out = []
    for k in range(3):
        lo, hi, o = mn[k], mx[k], origin[k]
        span = hi - lo
        eps = max(0.01, span * 0.005)
        if abs(o - lo) <= eps:
            out.append("min")
        elif abs(o - hi) <= eps:
            out.append("max")
        elif abs(o - (lo + hi) / 2) <= eps:
            out.append("mid")
        elif lo < o < hi:
            out.append(f"{(o - lo) / span:.2f}")
        else:
            out.append("outside")
    return out


def normal_stats(verts, tris):
    buckets = Counter()
    slopes = Counter()
    split = Counter()
    total = 0.0
    mn, mx = bbox(verts)
    c = (mn + mx) / 2
    for t in tris:
        a, b, cc = verts[t[0]], verts[t[1]], verts[t[2]]
        cr = (b - a).cross(cc - a)
        area = cr.length / 2
        if area < 1e-9:
            continue
        n = cr.normalized()
        total += area
        cen = (a + b + cc) / 3
        split["+X" if cen.x > c.x else "-X"] += 1
        split["+Y" if cen.y > c.y else "-Y"] += 1
        axis = None
        for name, vec in (("+X", (1, 0, 0)), ("-X", (-1, 0, 0)), ("+Y", (0, 1, 0)),
                          ("-Y", (0, -1, 0)), ("+Z", (0, 0, 1)), ("-Z", (0, 0, -1))):
            if n.dot(Vector(vec)) > 0.97:
                axis = name
                break
        if axis:
            buckets[axis] += area
            continue
        buckets["other"] += area
        if 0.2 < n.z < 0.97:  # upward-facing slope
            ang = round(math.degrees(math.acos(max(-1.0, min(1.0, n.z)))))
            az = round(math.degrees(math.atan2(n.y, n.x)) / 15) * 15
            slopes[(ang, az)] += area
    top_slopes = [{"deg": k[0], "facing_az_deg": k[1], "area": r3(v)}
                  for k, v in slopes.most_common(4) if v > 0.05 * max(total, 1e-6)]
    return ({k: r3(v) for k, v in buckets.items()}, top_slopes, dict(split), r3(total))


def down_rays(bvh, mn, mx):
    sx, sy = mx.x - mn.x, mx.y - mn.y
    step = max(0.1, max(sx, sy) / 40.0)
    nx, ny = max(1, int(sx / step)), max(1, int(sy / step))
    zs = []
    grid = []
    for i in range(nx):
        x = mn.x + (i + 0.5) * sx / nx
        row = []
        for j in range(ny):
            y = mn.y + (j + 0.5) * sy / ny
            hit = bvh.ray_cast(Vector((x, y, mx.z + 1.0)), Vector((0, 0, -1)), (mx.z - mn.z) + 2.0)
            z = hit[0].z if hit[0] is not None else None
            row.append(z)
            if z is not None:
                zs.append(round(z, 2))
        grid.append(row)
    if not zs:
        return {"coverage": 0.0}
    mode = Counter(zs).most_common(1)[0][0]

    def profile(axis_rows):
        out = []
        for row in axis_rows:
            vals = [z for z in row if z is not None]
            out.append(r3(sum(vals) / len(vals)) if vals else None)
        return out

    prof_x = profile(grid)  # slices along X
    prof_y = profile([[grid[i][j] for i in range(nx)] for j in range(ny)])  # along Y
    return {"coverage": r3(len(zs) / (nx * ny)), "z_mode": mode, "z_max": max(zs),
            "z_min_hit": min(zs), "profile_x": compress(prof_x), "profile_y": compress(prof_y)}


def compress(prof, n=10):
    if len(prof) <= n:
        return prof
    out = []
    for k in range(n):
        seg = [v for v in prof[k * len(prof) // n:(k + 1) * len(prof) // n] if v is not None]
        out.append(r3(sum(seg) / len(seg)) if seg else None)
    return out


def apertures(bvh, mn, mx, along):
    """Rays along `along` ('X' or 'Y') over the perpendicular plane. Misses
    not connected to the top/left/right border are apertures (the bottom
    border counts as closed, so a door open at grade still counts)."""
    if along == "Y":
        u_lo, u_hi, ax_u = mn.x, mx.x, 0
    else:
        u_lo, u_hi, ax_u = mn.y, mx.y, 1
    v_lo, v_hi = mn.z, mx.z
    su, sv = u_hi - u_lo, v_hi - v_lo
    if su < 0.3 or sv < 0.3:
        return []
    step = max(0.05, max(su, sv) / 120.0)
    nu, nv = max(2, int(su / step)), max(2, int(sv / step))
    d = Vector((0, 1, 0)) if along == "Y" else Vector((1, 0, 0))
    depth_lo = mn.y if along == "Y" else mn.x
    depth = (mx.y - mn.y) if along == "Y" else (mx.x - mn.x)
    miss = [[False] * nv for _ in range(nu)]
    for i in range(nu):
        u = u_lo + (i + 0.5) * su / nu
        for j in range(nv):
            v = v_lo + (j + 0.5) * sv / nv
            o = Vector((0, 0, v))
            o[ax_u] = u
            o[1 if along == "Y" else 0] = depth_lo - 1.0
            hit = bvh.ray_cast(o, d, depth + 2.0)
            miss[i][j] = hit[0] is None
    seen = [[False] * nv for _ in range(nu)]
    q = deque()
    for i in range(nu):
        for j in range(nv):
            border = i in (0, nu - 1) or j == nv - 1
            if border and miss[i][j]:
                seen[i][j] = True
                q.append((i, j))
    while q:
        i, j = q.popleft()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            a, b = i + di, j + dj
            if 0 <= a < nu and 0 <= b < nv and miss[a][b] and not seen[a][b]:
                seen[a][b] = True
                q.append((a, b))
    holes = []
    cu, cv = su / nu, sv / nv
    for i in range(nu):
        for j in range(nv):
            if miss[i][j] and not seen[i][j]:
                comp = []
                q.append((i, j))
                seen[i][j] = True
                while q:
                    a, b = q.popleft()
                    comp.append((a, b))
                    for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        x, y = a + di, b + dj
                        if 0 <= x < nu and 0 <= y < nv and miss[x][y] and not seen[x][y]:
                            seen[x][y] = True
                            q.append((x, y))
                area = len(comp) * cu * cv
                if area < 0.04:
                    continue
                i0 = min(c[0] for c in comp); i1 = max(c[0] for c in comp)
                j0 = min(c[1] for c in comp); j1 = max(c[1] for c in comp)
                holes.append({"through": along,
                              "u_min": r3(u_lo + i0 * cu), "u_max": r3(u_lo + (i1 + 1) * cu),
                              "z_min": r3(v_lo + j0 * cv), "z_max": r3(v_lo + (j1 + 1) * cv),
                              "width": r3((i1 - i0 + 1) * cu), "height": r3((j1 - j0 + 1) * cv),
                              "area": r3(area)})
    holes.sort(key=lambda h: -h["area"])
    return holes[:8]


def grid_fit(size):
    """Distance of the X and Y footprint from the nearest multiple of 3 m and 1.5 m."""
    def off(a, m):
        return r3(min(a % m, m - a % m))
    return {"x_mod3": off(size[0], 3.0), "y_mod3": off(size[1], 3.0),
            "x_mod1_5": off(size[0], 1.5), "y_mod1_5": off(size[1], 1.5)}


# --------------------------------------------------------------- render

def mat_color(name):
    h = int(hashlib.md5(name.encode()).hexdigest()[:6], 16) / 0xFFFFFF
    r, g, b = colorsys.hls_to_rgb(h, 0.62, 0.45)
    return (r, g, b, 1.0)


def setup_render(scene, res):
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_cavity = True
    sh.show_object_outline = True
    sh.object_outline_color = (0.05, 0.05, 0.05)
    sh.background_type = "VIEWPORT"
    sh.background_color = (0.93, 0.93, 0.93)
    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.film_transparent = False
    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass


def flat_mat(name, rgba):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = rgba
    return m


def text_obj(body, loc, size, rot, rgba=(0.1, 0.1, 0.1, 1.0)):
    cu = bpy.data.curves.new("txt", type="FONT")
    cu.body = body
    cu.size = size
    ob = bpy.data.objects.new("txt", cu)
    ob.location = loc
    ob.rotation_euler = rot
    ob.data.materials.append(flat_mat(f"_txt{rgba}", rgba))
    bpy.context.scene.collection.objects.link(ob)
    return ob


def grid_obj(size_u, size_v, spacing, loc):
    """A wire grid in the XZ plane (faces the -Y camera)."""
    import bmesh
    bm = bmesh.new()
    nu = int(size_u / spacing) + 1
    nv = int(size_v / spacing) + 1
    w = spacing * 0.025
    for i in range(nu):
        x = -size_u / 2 + i * spacing
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.LocRotScale(
            Vector((x, 0, 0)), None, Vector((w, w, size_v))))
    for j in range(nv):
        z = -size_v / 2 + j * spacing
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.LocRotScale(
            Vector((0, 0, z)), None, Vector((size_u, w, w))))
    me = bpy.data.meshes.new("grid")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("grid", me)
    ob.location = loc
    ob.data.materials.append(flat_mat("_grid", (0.72, 0.78, 0.86, 1.0)))
    bpy.context.scene.collection.objects.link(ob)
    return ob


def make_camera(scene):
    cd = bpy.data.cameras.new("cam")
    cd.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cd)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def render_views(piece, me_world, out_path):
    """2x2: front (camera at -Y), back (+Y), side (+X), top (+Z)."""
    scene = bpy.context.scene
    temp = []
    mn, mx = piece["_mn"], piece["_mx"]
    size = mx - mn
    cell = max(size.x, size.y, size.z) * 1.25 + 1.0
    spacing = 1.0 if cell <= 16 else 5.0
    # Rotations that turn the requested face toward the -Y camera.
    views = [("front -Y", Matrix.Identity(4), (0, 1)),
             ("back +Y", Matrix.Rotation(math.pi, 4, "Z"), (1, 1)),
             ("side +X", Matrix.Rotation(-math.pi / 2, 4, "Z"), (0, 0)),
             ("top +Z", Matrix.Rotation(math.pi / 2, 4, "X"), (1, 0))]
    for label, rot, (cx, cz) in views:
        centre_w = (mn + mx) / 2
        centre_r = rot @ centre_w
        off = Vector((cx * cell, 0, cz * cell)) - Vector((centre_r.x, 0, centre_r.z))
        ob = bpy.data.objects.new("v", me_world)
        ob.matrix_world = Matrix.Translation(off) @ rot
        scene.collection.objects.link(ob)
        temp.append(ob)
        # pivot marker in front of everything (ortho: depth does not move it)
        piv = rot @ Vector((0, 0, 0)) + off
        dot = bpy.data.objects.new("dot", bpy.data.meshes.get("_dot"))
        dot.location = (piv.x, -1000.0, piv.z)
        dot.scale = (cell * 0.012,) * 3
        scene.collection.objects.link(dot)
        temp.append(dot)
        g = grid_obj(cell, cell, spacing, (cx * cell, 1000.0, cz * cell))
        temp.append(g)
        temp.append(text_obj(label, (cx * cell - cell * 0.47, -900.0, cz * cell + cell * 0.42),
                             cell * 0.05, (math.pi / 2, 0, 0)))
    title = (f"{piece['name']}   {size.x:.2f} x {size.y:.2f} x {size.z:.2f} m   "
             f"grid {spacing:g} m   red = pivot")
    temp.append(text_obj(title, (-cell * 0.5, -900.0, cell * 1.58), cell * 0.055,
                         (math.pi / 2, 0, 0)))
    cam = scene.camera
    cam.data.ortho_scale = cell * 2.25
    cam.location = (cell * 0.5, -2000.0, cell * 0.62)
    cam.rotation_euler = (math.pi / 2, 0, 0)
    cam.data.clip_end = 5000.0
    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True)
    for ob in temp:
        data = ob.data
        bpy.data.objects.remove(ob, do_unlink=True)
        if data is not None and data.users == 0 and data.name.startswith(("grid", "txt")):
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            else:
                bpy.data.curves.remove(data)


def render_sheet(pieces, meshes, out_path, start_idx):
    scene = bpy.context.scene
    cols = max(1, math.ceil(math.sqrt(len(pieces))))
    cell = max(max(p["_mx"] - p["_mn"]) for p in pieces) * 1.3 + 1.0
    cell = min(cell, 60.0)
    temp = []
    for i, p in enumerate(pieces):
        r, c = divmod(i, cols)
        mn, mx = p["_mn"], p["_mx"]
        size = mx - mn
        k = min(1.0, (cell / 1.3) / max(size))  # shrink giants to fit the cell
        centre = (mn + mx) / 2
        ob = bpy.data.objects.new("s", meshes[p["name"]])
        ob.matrix_world = (Matrix.Translation(Vector((c * cell, -r * cell, 0)))
                           @ Matrix.Scale(k, 4)
                           @ Matrix.Translation(Vector((-centre.x, -centre.y, -mn.z))))
        scene.collection.objects.link(ob)
        temp.append(ob)
        tag = f"{start_idx + i}" + (f" (x{k:.2f})" if k < 1.0 else "")
        temp.append(text_obj(tag, (c * cell - cell * 0.45, -r * cell - cell * 0.45, 0.0),
                             cell * 0.12, (0, 0, 0), (0.75, 0.05, 0.05, 1.0)))
    span = cols * cell
    centre = Vector((span / 2 - cell / 2, -span / 2 + cell / 2, 0))
    cam = scene.camera
    cam.data.ortho_scale = span * 1.12
    cam.data.clip_end = 100000.0
    cam.location = centre + Vector((-span * 0.45, -span * 0.75, span * 0.8))
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True)
    for ob in temp:
        bpy.data.objects.remove(ob, do_unlink=True)


# --------------------------------------------------------------- main

def import_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=path)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=path)
    bpy.context.view_layer.update()


def measure_file(folder, fname, args, catalog, sheet_meta):
    path = os.path.join(SRC, folder, fname)
    family = os.path.splitext(fname)[0]
    import_fbx(path)
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    non_mesh = [f"{o.type}:{o.name}" for o in bpy.context.scene.objects if o.type != "MESH"]
    groups = defaultdict(lambda: {"base": None, "lods": [], "bo": None})
    lower_map = {}
    for o in objs:
        kind, base, n = split_name(o.name)
        key = base.lower()
        lower_map.setdefault(key, base)
        g = groups[key]
        if kind == "base":
            g["base"] = o
        elif kind == "lod":
            g["lods"].append((n, o))
        else:
            g["bo"] = o
    for o_mat in bpy.data.materials:
        o_mat.diffuse_color = mat_color(o_mat.name)
    dot_me = bpy.data.meshes.get("_dot")
    if dot_me is None:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1.0, segments=12, ring_count=8)
        s = bpy.context.active_object
        s.data.name = "_dot"
        s.data.materials.append(flat_mat("_dotmat", (0.95, 0.05, 0.05, 1.0)))
        bpy.data.objects.remove(s, do_unlink=True)
    scene = bpy.context.scene
    setup_render(scene, 1100)
    make_camera(scene)

    pieces = []
    world_meshes = {}
    orphan_bo = []
    for key, g in sorted(groups.items()):
        base = g["base"]
        if base is None:
            if g["bo"] is not None and not g["lods"]:
                orphan_bo.append(g["bo"].name)
                continue
            if g["lods"]:
                # bare .lod only, or LODs with no base: promote the first LOD
                g["lods"].sort(key=lambda t: t[0] or "0")
                n0, base = g["lods"].pop(0)
                log(f"[warn] {family}: {key} has no base object; using {base.name}")
        if args.get("only") and args["only"].lower() not in base.name.lower():
            continue
        verts, polys, tris, mats = world_geometry(base)
        if not verts:
            continue
        mn, mx = bbox(verts)
        size = mx - mn
        mw = base.matrix_world
        loc, rot, scl = mw.decompose()
        identity = (loc.length < 1e-4 and rot.angle < 1e-4
                    and all(abs(s - 1.0) < 1e-4 for s in scl))
        lmn, lmx = bbox([v.co for v in base.data.vertices])
        bvh = BVHTree.FromPolygons(verts, polys)
        buckets, slopes, split, area = normal_stats(verts, tris)
        top = down_rays(bvh, mn, mx)
        holes = apertures(bvh, mn, mx, "Y") + apertures(bvh, mn, mx, "X")
        lods = []
        for n, lo in sorted(g["lods"], key=lambda t: int(t[0]) if t[0] else -1):
            lo.data.calc_loop_triangles()
            lmn2, lmx2 = bbox([lo.matrix_world @ v.co for v in lo.data.vertices]) \
                if lo.data.vertices else (mn, mx)
            lods.append({"name": lo.name, "n": n, "tris": len(lo.data.loop_triangles),
                         "size": vec3(lmx2 - lmn2),
                         "bbox_delta": r3(max((lmn2 - mn).length, (lmx2 - mx).length))})
        bo = None
        if g["bo"] is not None:
            bo_v, bo_p, bo_t, bo_m = world_geometry(g["bo"])
            bmn, bmx = bbox(bo_v) if bo_v else (mn, mx)
            bo = {"name": g["bo"].name, "tris": len(bo_t), "min": vec3(bmn), "max": vec3(bmx),
                  "materials": bo_m}
            if bo_v and holes:
                bo_bvh = BVHTree.FromPolygons(bo_v, bo_p)
                bo["apertures"] = apertures(bo_bvh, mn, mx, "Y") + apertures(bo_bvh, mn, mx, "X")
        piece = {
            "name": base.name, "file": family, "folder": folder,
            "node": {"identity": identity, "loc": vec3(loc),
                     "rot_deg": vec3([math.degrees(a) for a in rot.to_euler("XYZ")]),
                     "scale": vec3(scl)},
            "min": vec3(mn), "max": vec3(mx), "size": vec3(size),
            "local_min": vec3(lmn), "local_max": vec3(lmx),
            "pivot": pivot_class(mn, mx, loc),
            "tris": len(tris), "materials": mats,
            "normal_area": buckets, "surface_area": area, "slopes": slopes,
            "tri_split": split, "top": top, "apertures": holes,
            "grid_fit": grid_fit(size),
            "lods": lods, "bo": bo,
        }
        pieces.append(piece)
        me = bpy.data.meshes.new(f"w_{base.name}")
        me.from_pydata([tuple(v) for v in verts], [], [list(p) for p in polys])
        me.update()
        for m in base.data.materials:
            me.materials.append(m)
        for i, p in enumerate(me.polygons):
            p.material_index = base.data.polygons[i].material_index
        world_meshes[base.name] = me
        piece["_mn"], piece["_mx"] = mn, mx

    for o in objs:
        o.hide_render = True

    if not args.get("no_render"):
        os.makedirs(os.path.join(OUT, "views"), exist_ok=True)
        for p in pieces:
            render_views(p, world_meshes[p["name"]], os.path.join(OUT, "views", f"{p['name']}.png"))
        os.makedirs(os.path.join(OUT, "sheets"), exist_ok=True)
        setup_render(scene, 2000)
        start = len(catalog)
        for pi in range(0, len(pieces), PAGE):
            page = pieces[pi:pi + PAGE]
            out = os.path.join(OUT, "sheets", f"{family}_p{pi // PAGE}.png")
            render_sheet(page, world_meshes, out, start + pi)
            sheet_meta.append({"sheet": os.path.basename(out), "first": start + pi,
                               "last": start + pi + len(page) - 1})

    for i, p in enumerate(pieces):
        p["idx"] = len(catalog)
        p.pop("_mn", None)
        p.pop("_mx", None)
        catalog.append(p)
    log(f"[catalog] {family}: {len(pieces)} pieces, {len(objs)} mesh objects, "
        f"orphan bo {orphan_bo}, non-mesh {non_mesh}")
    return {"file": family, "folder": folder, "mesh_objects": len(objs),
            "pieces": len(pieces), "orphan_bo": orphan_bo, "non_mesh": non_mesh}


def main():
    global REPORT_DIR
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = {"only": None, "no_render": "--no-render" in argv}
    if "--only" in argv:
        args["only"] = argv[argv.index("--only") + 1]
    os.makedirs(OUT, exist_ok=True)
    REPORT_DIR = os.path.join(OUT, "_report")
    os.makedirs(REPORT_DIR, exist_ok=True)
    done = os.path.join(REPORT_DIR, "DONE.txt")
    if os.path.exists(done):
        os.remove(done)
    t0 = time.time()
    catalog, files, sheets = [], [], []
    status = "ok"
    for folder in FOLDERS:
        d = os.path.join(SRC, folder)
        for fname in sorted(os.listdir(d)):
            if not fname.lower().endswith(".fbx"):
                continue
            try:
                files.append(measure_file(folder, fname, args, catalog, sheets))
            except Exception:
                log(traceback.format_exc())
                status = "partial"
    name = "erebor_catalog.json" if not args["only"] else f"erebor_catalog_only_{args['only']}.json"
    with open(os.path.join(OUT, name), "w", encoding="utf-8") as f:
        json.dump({"source": SRC, "files": files, "sheets": sheets, "pieces": catalog}, f, indent=1)
    with open(done, "w", encoding="utf-8") as f:
        json.dump({"status": status, "pieces": len(catalog), "seconds": round(time.time() - t0)}, f)
    log(f"[done] {status}: {len(catalog)} pieces in {time.time() - t0:.0f}s")


main()
