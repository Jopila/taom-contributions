"""
Unique UV layout in several texture sets for a hard-surface mesh (a map icon, a static
prop), ready for a Substance Painter bake: every face goes to a texture set by its material
and part, faces no camera can see get almost no UV space, and each set is unwrapped and
packed on its own 0..1 tile at one texel density. Exports the Kit FBX with one material per
texture set (Painter makes one texture set per material).

Why: a tiling material repeats the same pixels everywhere, so it cannot carry baked
occlusion. That needs a unique layout and Painter's mesh maps. Measured on the Carn Dum
rebuild (2026-10-16).

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/blender/unwrap_texture_sets.py --
        --blend <source .blend, one mesh> --rules <rules.json> --out <dir> [--name <fbx stem>]
        [--resolution 1024] [--margin-px 2] [--soft-deg 75] [--anchor-deg 20]
        [--max-island-frac 0.5] [--probe]

rules.json: {"sets": [{"name": "carn_dum_towers", "materials": ["carn_dum_stone"],
"parts": ["towers"], "resolution": 1024}, ...]}. The first matching rule wins; a rule with
neither key takes every face left. "parts" reads the INT face attribute "part" and the
object's "parts" property (comma-separated names), as tools/oneoff/blender_build_carn_dum_icon.py
writes them. --probe writes only the area per material and part (total and visible, m2)
and, with --rules, an upper bound of the texel density per set (partly visible faces cost
full area, so the measured density comes out lower).

Visibility: up to 19 sample points per face (the centre, and 40%, 80% and 95% of the way to
up to 6 corners). From each, rays go out along 64 directions 10 to 90 degrees above the
horizon (MapCameraView, read in the decompile dump: untilted, the camera looks down about 26
degrees; the lowest tilt a player can reach is about 10.5 degrees at the default distance and
5.2 at full zoom, so the 10 degree floor covers the default view, and a face seen only from
lower, at full zoom, counts as hidden). A sample below the ground (z < 0) or whose rays all hit the mesh is hidden.
A UV island less than 30% visible shrinks with the square root of its visible share; islands
under 2% visible (buried bases, box bottoms, faces inside other solids) are stacked in one
small square that packs as one island. A face visible in part keeps its full area, so a
builder should not run one face down into the solid that hides it (each set reports its
hidden_kept_m2 by part, the area that still costs texels unseen).

Inverted faces: a face no ray sees along its normal but rays see along the reversed one is
reported in suspect_inverted_faces (count, area, samples), and the run fails when their area
passes 0.1% of the mesh. A Bannerlord material culls back faces unless it carries
two_sided (the Gundabad icon materials do not), while Blender's and Cycles' views draw them:
this caught every box of the Carn Dum builder wound inward (5,906 faces). Tiny faces in
crevices between overlapping open shells also land there (175 faces, 0.7 m2 on Carn Dum
v10).

Unwrap ("nets"): within each set, a spanning tree over each part's visible faces joins the
flattest neighbours first; edges outside it are seams, and soft seams that dangle are joined
back, so a box unfolds as a cross and a tube as one strip with one seam. Long rings are cut
into sectors by perimeter. Then an angle-based unwrap, one texel density per set, and a
concave pack with rotation and a margin of --margin-px at the set's resolution. Margins
dominate when islands are a few pixels wide: Smart UV Project at 45 degrees and 4 px put
7,109 tower islands in 5% of their tile, nets put about 900 in 51%. The mesh is split into
one object per set, because the UV operators act on every selected face, and joined again.

Writes <out>/<name>.fbx (the Kit settings, kitbash_kits.FBX_EXPORT), <out>/<name>_uv.blend,
<out>/uv_<set>.png layouts (white: visible faces, red: hidden ones) and
<out>/_report/DONE.txt (tools/blender/blender_job.py: written on every exit; per set: faces,
islands, UV coverage, visible share, hidden_kept_m2, visible texel density).
"""

import argparse
import json
import math
import os
import sys
import time

import bmesh
import bpy
import numpy as np
from bpy_extras.bmesh_utils import bmesh_linked_uv_islands
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blender_job import export_fbx, flag_value, job, report_path, script_args  # noqa: E402

MIN_ELEV_DEG = 10.0
N_DIRS = 64
VISIBLE_SHARE = 0.3
HIDDEN_SHARE = 0.02
INVERTED_LIMIT = 0.001  # inverted faces above this share of the mesh area fail the run


def parse_args(argv):
    p = argparse.ArgumentParser(allow_abbrev=False)
    p.add_argument("--blend", required=True)
    p.add_argument("--rules")
    p.add_argument("--out", required=True)
    p.add_argument("--name")
    p.add_argument("--resolution", type=int, default=1024)
    p.add_argument("--margin-px", type=float, default=2.0)
    p.add_argument("--soft-deg", type=float, default=75.0)
    p.add_argument("--anchor-deg", type=float, default=20.0)
    p.add_argument("--max-island-frac", type=float, default=0.5,
                   help="longest chart, as a share of the tile at the estimated texel density")
    p.add_argument("--probe", action="store_true")
    return p.parse_args(argv)


def hemisphere_dirs():
    """N_DIRS directions spread evenly over the sky above MIN_ELEV_DEG (uniform in z is
    uniform in solid angle), on a golden-angle spiral."""
    zmin = math.sin(math.radians(MIN_ELEV_DEG))
    golden = math.pi * (3 - math.sqrt(5))
    out = []
    for i in range(N_DIRS):
        z = zmin + (1 - zmin) * (i + 0.5) / N_DIRS
        r = math.sqrt(1 - z * z)
        out.append(Vector((r * math.cos(golden * i), r * math.sin(golden * i), z)))
    return out


def face_samples(f):
    c = f.calc_center_median()
    corners = [v.co for v in f.verts]
    step = max(1, len(corners) // 6)
    pts = [c]
    for co in corners[::step][:6]:
        pts += [c.lerp(co, 0.4), c.lerp(co, 0.8), c.lerp(co, 0.95)]  # 0.95: a face whose top edge shows
    return pts


def face_visibility(bvh, dirs, f, n):
    valid = [d for d in dirs if d.dot(n) > 0.02]
    if not valid:
        return 0.0
    pts = face_samples(f)
    seen = 0
    for s in pts:
        p = s + n * 0.01
        if p.z < 0.0:
            continue
        for d in valid:
            if bvh.ray_cast(p, d, 1000.0)[0] is None:
                seen += 1
                break
    return seen / len(pts)


def visibility(bm):
    """Visible share (0..1) of every face, in bm.faces order, and the indices of faces that
    look inverted: never seen along their normal, seen along the reversed one (a box wound
    inward shows up here; renderers that draw back faces hide it, the game culls it)."""
    bm.normal_update()
    bvh = BVHTree.FromBMesh(bm)
    dirs = hemisphere_dirs()
    out, inverted = [], []
    for f in bm.faces:
        v = face_visibility(bvh, dirs, f, f.normal)
        if v == 0.0 and face_visibility(bvh, dirs, f, -f.normal) >= 0.5:
            inverted.append(f.index)
        out.append(v)
    return out, inverted


def match_rule(rules, mat, part):
    for i, r in enumerate(rules):
        if "materials" in r and mat not in r["materials"]:
            continue
        if "parts" in r and part not in r["parts"]:
            continue
        return i
    return -1


def select_only(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def net_seams(obj, soft_deg, anchor_deg, max_len):
    """Seams that unfold each part as one net: a spanning tree over its visible faces joins the
    flattest neighbours first, every edge outside the tree is a cut, then a soft cut (under
    soft_deg) is removed again where it dangles, unless it ends on a boundary or at a corner
    whose angles miss 360 degrees by more than anchor_deg. A box then unfolds as a cross (its
    90-degree cuts are never soft), a tube as one strip whose single seam runs from its open
    base to its tip. Hidden faces are cut off as faces of their own. A chart longer than
    max_len (metres) is cut into sectors (a ring) or slabs, because one island must fit its
    tile at the set's texel density. Returns (cuts, extra cuts, hidden faces)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    bm.faces.ensure_lookup_table()
    vis = bm.faces.layers.float.get("vis")
    hidden = [f[vis] < HIDDEN_SHARE for f in bm.faces]

    def interior(e):
        lf = e.link_faces
        return len(lf) == 2 and not hidden[lf[0].index] and not hidden[lf[1].index]

    inter = {e for e in bm.edges if interior(e)}
    ang = {e: e.calc_face_angle(math.pi) for e in inter}
    parent = list(range(len(bm.faces)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    tree = set()
    for e in sorted(inter, key=lambda e: (ang[e], e.index)):  # ties (coplanar, 90 degrees) in a fixed order
        a, b = find(e.link_faces[0].index), find(e.link_faces[1].index)
        if a != b:
            parent[a] = b
            tree.add(e)
    cut = inter - tree
    fixed = {v for e in bm.edges if e not in inter for v in e.verts}
    lim = math.radians(anchor_deg)
    for v in bm.verts:
        if v not in fixed and abs(2 * math.pi - sum(l.calc_angle() for l in v.link_loops)) > lim:
            fixed.add(v)
    soft = math.radians(soft_deg)
    deg = {}
    for e in cut:
        for v in e.verts:
            deg[v] = deg.get(v, 0) + 1
    stack = [v for v, d in deg.items() if d == 1 and v not in fixed]
    while stack:
        v = stack.pop()
        if deg.get(v) != 1 or v in fixed:
            continue
        e = next(e for e in v.link_edges if e in cut)
        if ang[e] >= soft:
            continue
        cut.discard(e)
        for w in e.verts:
            deg[w] -= 1
            if deg[w] == 1 and w not in fixed:
                stack.append(w)

    seam = cut | (set(bm.edges) - inter)
    extra = set()
    seen = [False] * len(bm.faces)
    for f0 in bm.faces:
        if hidden[f0.index] or seen[f0.index]:
            continue
        chart, todo = [], [f0]
        seen[f0.index] = True
        while todo:
            f = todo.pop()
            chart.append(f)
            for e in f.edges:
                if e in seam:
                    continue
                for g in e.link_faces:
                    if not seen[g.index]:
                        seen[g.index] = True
                        todo.append(g)
        pts = [v.co for f in chart for v in f.verts]
        mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        size = mx - mn
        if max(size) * math.pi <= max_len:  # even unrolled as a ring it fits
            continue
        c = (mn + mx) / 2
        cs = [f.calc_center_median() for f in chart]
        angs = [math.atan2(p.y - c.y, p.x - c.x) for p in cs]
        # a ring (it wraps 270+ degrees round its centre) unrolls to its perimeter, not its
        # bounding box: v10's terrace walls, 38 m across, became 119 m strips
        if len({int((a + math.pi) / (math.pi / 6)) for a in angs}) >= 9:
            radius = sum(math.hypot(p.x - c.x, p.y - c.y) for p in cs) / len(cs)
            n = math.ceil(2 * math.pi * radius / max_len)
            if n < 2:
                continue
            key = [int((a + math.pi) / (2 * math.pi) * n) % n for a in angs]
        else:
            axis = max(range(3), key=lambda i: size[i])
            n = math.ceil(size[axis] / max_len)
            if n < 2:
                continue
            key = [min(n - 1, int((p[axis] - mn[axis]) / size[axis] * n)) for p in cs]
        kf = {f.index: k for f, k in zip(chart, key)}
        for f in chart:
            for e in f.edges:
                if e not in seam and len({kf.get(g.index) for g in e.link_faces}) > 1:
                    extra.add(e)
    for e in bm.edges:
        e.seam = e in seam or e in extra
    n_cut, n_hidden = len(cut), sum(hidden)
    bm.to_mesh(obj.data)
    bm.free()
    return n_cut, len(extra), n_hidden


def unwrap_set(obj, margin, soft_deg, anchor_deg, max_len):
    """Islands from net_seams, an angle-based unwrap along them, one texel density. Islands
    under VISIBLE_SHARE shrink; fully hidden ones (under 2%) are stacked in one small square,
    packed as one island (merge_overlap), so they cost no margins."""
    # UV operators act on selected UVs; with sync on, the selected faces are the selected UVs
    bpy.context.scene.tool_settings.use_uv_select_sync = True
    seams = net_seams(obj, soft_deg, anchor_deg, max_len)
    select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.unwrap(method="ANGLE_BASED", fill_holes=True, correct_aspect=True, margin=0.001)
    bpy.ops.uv.average_islands_scale()
    # apart again before the shrink below; by bounding box, so an island shrunk toward its
    # centre stays inside its own box and merge_overlap cannot freeze two kept islands together
    bpy.ops.uv.pack_islands(rotate=False, margin=0.001, shape_method="AABB")
    bpy.ops.object.mode_set(mode="OBJECT")

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    uv = bm.loops.layers.uv.active
    vis = bm.faces.layers.float.get("vis")
    islands = bmesh_linked_uv_islands(bm, uv)
    shrunk, hidden, kept_uv_area = 0, [], 0.0
    for isl in islands:
        faces = [bm.faces[i] if isinstance(i, int) else i for i in isl]
        area = sum(f.calc_area() for f in faces)
        share = sum(f.calc_area() * f[vis] for f in faces) / max(area, 1e-12)
        loops = [l for f in faces for l in f.loops]
        if share < HIDDEN_SHARE:
            hidden.append(loops)
            continue
        if share < VISIBLE_SHARE:
            s = math.sqrt(share / VISIBLE_SHARE)
            c = sum((l[uv].uv for l in loops), Vector((0.0, 0.0))) / len(loops)
            for l in loops:
                l[uv].uv = c + (l[uv].uv - c) * s
            shrunk += 1
        us = [l[uv].uv.x for l in loops]
        vs = [l[uv].uv.y for l in loops]
        kept_uv_area += (max(us) - min(us)) * (max(vs) - min(vs))
    side = 0.05 * math.sqrt(kept_uv_area)
    for loops in hidden:  # outside 0..1, where no kept island is, so only they overlap
        us = [l[uv].uv.x for l in loops]
        vs = [l[uv].uv.y for l in loops]
        u0, v0 = min(us), min(vs)
        k = side / max(max(us) - u0, max(vs) - v0, 1e-9)
        for l in loops:
            l[uv].uv = Vector((2.0 + (l[uv].uv.x - u0) * k, 2.0 + (l[uv].uv.y - v0) * k))
    bm.to_mesh(obj.data)
    bm.free()

    select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin_method="FRACTION", margin=margin, shape_method="CONCAVE",
                            merge_overlap=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    return {"islands": len(islands), "islands_shrunk": shrunk, "islands_hidden_stacked": len(hidden),
            "cuts": seams[0], "length_cuts": seams[1]}


def set_stats(obj, res, parts):
    """UV coverage (kept islands plus the one square of stacked hidden ones), the share of the
    kept UV area that faces show, the hidden area kept at full density by part (what still
    costs texels unseen), folded faces (the minority winding inside an island) and the
    visible texel density."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    uv = bm.loops.layers.uv.active
    vis = bm.faces.layers.float.get("vis")
    part = bm.faces.layers.int.get("part")
    cover, cover_vis, dens, weights, folded = 0.0, 0.0, [], [], 0
    stacked_uv, hidden_kept = [], {}

    def signed(f):
        uvs = [l[uv].uv for l in f.loops]
        s = 0.0
        for i in range(1, len(uvs) - 1):
            e1, e2 = uvs[i] - uvs[0], uvs[i + 1] - uvs[0]
            s += (e1.x * e2.y - e1.y * e2.x) / 2
        return s

    for isl in bmesh_linked_uv_islands(bm, uv):
        faces = [bm.faces[i] if isinstance(i, int) else i for i in isl]
        area3 = sum(f.calc_area() for f in faces)
        share = sum(f.calc_area() * f[vis] for f in faces) / max(area3, 1e-12)
        if share < HIDDEN_SHARE:
            stacked_uv.extend(l[uv].uv.copy() for f in faces for l in f.loops)
            continue
        s_areas = [signed(f) for f in faces]
        pos = sum(1 for s in s_areas if s >= 0)
        folded += min(pos, len(s_areas) - pos)
        for f, s in zip(faces, s_areas):
            cover += abs(s)
            cover_vis += abs(s) * f[vis]
            a3 = f.calc_area()
            if part is not None and f[vis] < 1.0:
                name = parts[f[part]] if f[part] < len(parts) else str(f[part])
                hidden_kept[name] = hidden_kept.get(name, 0.0) + a3 * (1.0 - f[vis])
            if f[vis] >= 0.5 and a3 > 1e-6:
                dens.append(math.sqrt(abs(s) / a3) * res)
                weights.append(a3)
    visible_share = cover_vis / max(cover, 1e-12)  # of the kept islands, before the hidden square
    if stacked_uv:
        cover += (max(p.x for p in stacked_uv) - min(p.x for p in stacked_uv)) * \
                 (max(p.y for p in stacked_uv) - min(p.y for p in stacked_uv))
    bm.free()
    if dens:
        order = np.argsort(dens)
        d = np.array(dens)[order]
        cum = np.cumsum(np.array(weights)[order])
        cum /= cum[-1]
        pct = {f"p{q}": round(float(d[np.searchsorted(cum, q / 100)]), 1) for q in (10, 50, 90)}
    else:
        pct = {}
    return {"uv_coverage": round(cover, 3), "visible_share": round(visible_share, 3),
            "hidden_kept_m2": {k: round(v, 1) for k, v in sorted(hidden_kept.items(), key=lambda kv: -kv[1])},
            "folded_faces": folded, "visible_px_per_m": pct}


def write_layout(obj, path, res):
    """UV edges as a PNG: white for visible faces, red for hidden ones."""
    me = obj.data
    n_loops = len(me.loops)
    uvs = np.zeros(n_loops * 2, dtype=np.float32)
    me.uv_layers.active.data.foreach_get("uv", uvs)
    uvs = uvs.reshape(-1, 2)
    starts = np.zeros(len(me.polygons), dtype=np.int32)
    totals = np.zeros(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get("loop_start", starts)
    me.polygons.foreach_get("loop_total", totals)
    vis = np.zeros(len(me.polygons), dtype=np.float32)
    me.attributes["vis"].data.foreach_get("value", vis)
    a_idx, b_idx, face_vis = [], [], []
    for s, t, v in zip(starts, totals, vis):
        idx = np.arange(s, s + t)
        a_idx.append(idx)
        b_idx.append(np.roll(idx, -1))
        face_vis.append(np.full(t, v))
    a, b, fv = uvs[np.concatenate(a_idx)], uvs[np.concatenate(b_idx)], np.concatenate(face_vis)
    ts = np.linspace(0.0, 1.0, 24, dtype=np.float32)[None, :, None]
    pts = a[:, None, :] + (b - a)[:, None, :] * ts
    px = np.clip((pts * (res - 1)).astype(np.int32), 0, res - 1)
    img = np.zeros((res, res, 4), dtype=np.float32)
    img[..., 3] = 1.0
    col = np.where((fv >= 0.5)[:, None], np.array([[0.9, 0.9, 0.9]]), np.array([[0.9, 0.15, 0.1]]))
    col = np.repeat(col[:, None, :], ts.shape[1], axis=1)
    img[px[..., 1].ravel(), px[..., 0].ravel(), :3] = col.reshape(-1, 3)
    im = bpy.data.images.new(os.path.basename(path), res, res, alpha=True)
    im.pixels.foreach_set(img.ravel())
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()


def main():
    args = script_args()
    out = flag_value(args, "--out") or "."  # before argparse, so a bad argument still gets a fresh report
    with job(report_path(out)) as report:
        run(parse_args(args), report)


def run(a, report):
    bpy.ops.wm.open_mainfile(filepath=a.blend)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"the .blend must hold one mesh, not {[o.name for o in meshes]}")
    obj = meshes[0]
    name = a.name or obj.name
    me = obj.data
    if obj.matrix_world != obj.matrix_world.Identity(4):
        mirrored = obj.matrix_world.determinant() < 0
        me.transform(obj.matrix_world)
        obj.matrix_world.identity()
        if mirrored:  # a negative scale flips the winding when it is baked in
            me.flip_normals()
    mats = [s.material.name if s.material else "" for s in obj.material_slots]
    parts = [p for p in obj.get("parts", "").split(",") if p]
    part_attr = me.attributes.get("part")
    if parts and part_attr is None:
        raise RuntimeError('the object names parts but its mesh has no INT face attribute "part"')
    part_idx = np.zeros(len(me.polygons), dtype=np.int32)
    if part_attr:
        part_attr.data.foreach_get("value", part_idx)

    def part_name(i):
        pi = int(part_idx[i])
        return parts[pi] if pi < len(parts) else str(pi)

    t0 = time.time()
    bm = bmesh.new()
    bm.from_mesh(me)
    vis, inverted = visibility(bm)
    areas = [f.calc_area() for f in bm.faces]
    bm.free()
    inv_by_key, inv_samples = {}, []
    for i in inverted:
        p = me.polygons[i]
        key = f"{mats[p.material_index]}/{part_name(i)}"
        inv_by_key[key] = inv_by_key.get(key, 0) + 1
        if len(inv_samples) < 12:
            inv_samples.append({"face": i, "centre": [round(c, 2) for c in p.center],
                                "normal": [round(c, 2) for c in p.normal], "area": round(p.area, 4),
                                "verts": len(p.vertices)})
    if "vis" in me.attributes:
        me.attributes.remove(me.attributes["vis"])
    me.attributes.new("vis", "FLOAT", "FACE").data.foreach_set("value", vis)

    by_mat_part = {}
    for p, ar, v in zip(me.polygons, areas, vis):
        e = by_mat_part.setdefault(f"{mats[p.material_index]}/{part_name(p.index)}", [0, 0.0, 0.0])
        e[0] += 1
        e[1] += ar
        e[2] += ar * v
    inv_area = sum(areas[i] for i in inverted)
    report.update({"visibility_seconds": round(time.time() - t0, 1),
                   "suspect_inverted_faces": {"total": len(inverted), "area_m2": round(inv_area, 2),
                                              "by_material_part": inv_by_key, "samples": inv_samples},
                   "area_m2_total_visible": {k: [v[0], round(v[1], 1), round(v[2], 1)]
                                             for k, v in sorted(by_mat_part.items())}})
    if inv_area > INVERTED_LIMIT * sum(areas):
        raise RuntimeError(f"{inv_area:.1f} m2 of faces look inverted (over {INVERTED_LIMIT:.1%} of the mesh): "
                           "fix the builder's winding; samples in suspect_inverted_faces")

    rules = json.load(open(a.rules, encoding="utf-8"))["sets"] if a.rules else []
    if rules:
        assign = [match_rule(rules, mats[p.material_index], part_name(p.index) if parts else "")
                  for p in me.polygons]
        if min(assign) < 0:
            raise RuntimeError(f"{assign.count(-1)} faces match no rule")
        est = {}
        for r_i, r in enumerate(rules):
            vis_a = sum(ar * v for ar, v, s in zip(areas, vis, assign) if s == r_i)
            res = r.get("resolution", a.resolution)
            est[r["name"]] = {"faces": assign.count(r_i), "visible_m2": round(vis_a, 1),
                              "upper_px_per_m_at_70pct": round(res * math.sqrt(0.7 / max(vis_a, 1e-6)), 1)}
        report["sets_estimate"] = est

    if a.probe or not rules:
        return

    # one material per texture set, then one object per set for the UV operators
    set_mats = [bpy.data.materials.get(r["name"]) or bpy.data.materials.new(r["name"]) for r in rules]
    me.materials.clear()
    for m in set_mats:
        me.materials.append(m)
    me.polygons.foreach_set("material_index", np.array(assign, dtype=np.int32))
    for layer in list(me.uv_layers):
        me.uv_layers.remove(layer)
    me.uv_layers.new(name="UVMap")
    before = set(bpy.data.objects)
    select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="MATERIAL")
    bpy.ops.object.mode_set(mode="OBJECT")
    pieces = [obj] + [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    sets_out = {}
    for o in pieces:
        used = {p.material_index for p in o.data.polygons}
        mat = o.material_slots[used.pop()].material.name
        r = next(r for r in rules if r["name"] == mat)
        res = r.get("resolution", a.resolution)
        max_len = a.max_island_frac * res / est[mat]["upper_px_per_m_at_70pct"]
        t1 = time.time()
        info = unwrap_set(o, a.margin_px / res, a.soft_deg, a.anchor_deg, max_len)
        st = set_stats(o, res, parts)
        write_layout(o, os.path.join(a.out, f"uv_{mat}.png"), res)
        sets_out[mat] = {"faces": len(o.data.polygons), "resolution": res, "max_island_m": round(max_len, 1),
                         **info, "seconds": round(time.time() - t1, 1), **st}
    report["sets"] = sets_out

    bpy.ops.object.select_all(action="DESELECT")
    for o in pieces:
        o.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = joined.data.name = name
    export_fbx(os.path.join(a.out, name + ".fbx"), [joined])
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(a.out, name + "_uv.blend"))
    joined.data.calc_loop_triangles()
    report["tris"] = len(joined.data.loop_triangles)
    report["materials"] = [m.name for m in joined.data.materials]


if __name__ == "__main__":  # Blender runs a -P script as __main__
    main()
