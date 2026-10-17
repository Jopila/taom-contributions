"""
Build the Carn Dum campaign map icon from clean solids (flat faces, hard edges,
separate merlons and spikes), on the layout measured from Mike's Tripo model.

Why a rebuild: the Tripo mesh is a sculpt (rounded corners, dripping walls);
decimated to 25k or 90k triangles it read as melted wax in the Kit, and only
8.5% of its surface was flat enough for a planar decimation at 1 degree. The
hand-made Gondor icons (minas_tirith 63,597 tris) are box-modelled: flat
planes, true cylinders, one sharp edge per face. This builds that kind of mesh.

Every number in LAYOUT comes from the 2026-10-16 measurement of the Tripo
source scaled to 60 m (ray-cast height map, radial profile, peak search,
orthographic views: E:\\LOTRAOMAssets\\_export\\carn_dum_rebuild\\measure\\). Gate
faces -Y. The icon draws LOD0 on the map, so the 90k budget (Mike, 2026-10-16;
gundabad_town is 91,089) is spent on visible detail.

The spikes are sculpted out of the stone, as in Mike's reference: only the
round towers, the central tower and the keep carry them. Each round tower is
one fluted shaft: deep ribs run from the buried base, down the sloped foot and
up the shaft, and every rib ends in a thick horn spike. One curved flange with
a downward thorn joins each pair of neighbouring spikes, so the wings meet as
one piece. The keep drum carries a crown of stone blades. Walls and parapets
keep plain square merlons; the gate carries a row of horn merlons.

Versions, for the record: v1 layout only (12,272 tris); v2 buttresses, framed
windows, tiered towers, gate, houses (19,297); v3 128-sided rings,
machicolations, sloped bases (36,287); v4 banners, flying buttresses, palisade,
rocks (50,233); v5 no banners, plinth, rocks, palisade or gate ramp, iron horns
everywhere (89,319); v6 stone spikes on the towers and keep only, a slate roof
slot, iron left on the portcullis (87,332); v7 fluted towers after Mike's
reference, no centre spires, plain walls, the gatehouse (88,146); v8 one flange
per spike gap instead of two wings that crossed (87,154); v9 ribs run into the
ground, no gate corner horns or sigil, half as many houses (82,123; the shape
Mike approved); v10 (this) terraces as rings, so a top face is its visible step
only, a per-face part attribute for the texture-set rules, and every box wound
outward (84,053). The base sits 1 m below ground so it seats into the terrain.

Texturing: tools/blender/unwrap_texture_sets.py unwraps <out>/carn_dum_town_blockout.blend
with tools/oneoff/carn_dum_texture_sets.json, then Painter
(docs/kitbash/map-icons.md "Rebuilding an icon from solids").

Material slots name surface kinds, not texture sets: carn_dum_stone (walls, towers,
spikes), carn_dum_paving (terrace and wall tops), carn_dum_iron (portcullis),
carn_dum_dark (window and gate openings), carn_dum_roof (house and turret roofs). The
unwrap turns material plus part into the texture sets.

v11 (2026-10-16 review): no face reaches down into the solid that hides it (terrace
risers seated on the step in front, the outer wall's faces started at the talus top and
the terrace 0 step), both gable ends stone, the flying-buttress piers seated in the
wall. The tiling route's box UVs and FBX are gone: the unwrap writes the Kit FBX.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/oneoff/blender_build_carn_dum_icon.py -- <out dir>

Writes <out>/carn_dum_town_blockout.blend (the unwrap's input; open it in Blender to show
the shape), front/three-quarter/close/keep renders and _report/DONE.txt (triangle count
and faces per material; tools/blender/blender_job.py writes it on every exit).
"""

import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "blender"))
from blender_job import job, report_path, script_args  # noqa: E402

UP = Vector((0.0, 0.0, 1.0))
BASE = -1.0  # every wall, tower and terrace starts here, 1 m under the map ground

LAYOUT = {
    # concentric solids: (radius, top z); stacked they form the terraces
    "terraces": [(21.6, 5.4), (19.0, 10.0), (16.3, 12.8), (14.0, 15.7), (10.2, 18.0)],
    "outer_wall": {"r_in": 21.6, "r_out": 24.4, "top": 10.3},
    "inner_wall": {"r_in": 8.0, "r_out": 10.2, "z0": 18.0, "top": 21.5},
    # terrace radii whose edge gets a parapet (10.2 is under the inner wall: it would z-fight)
    "parapets": [19.0, 16.3, 14.0],
    "keep": {"r": 5.0, "top": 27.5},
    "tower": {"r": 2.6, "top": 35.8},
    # round wall towers: (x, y, radius, body top, crown blade length)
    "round_towers": [(20.5, 13.2, 2.0, 25.5, 3.6), (-20.5, 13.2, 2.0, 25.5, 3.6),
                     (24.25, -3.3, 2.0, 18.5, 3.0), (-24.25, -3.3, 2.0, 18.5, 3.0),
                     (16.5, -17.3, 1.8, 12.0, 2.4), (-16.5, -17.3, 1.8, 12.0, 2.4),
                     (10.75, 21.0, 1.5, 10.5, 2.0), (-10.75, 21.0, 1.5, 10.5, 2.0)],
    # square towers: (x, y, width x, depth y, body top); only the rear tower since v7
    "square_towers": [(0.0, 24.0, 3.6, 6.0, 11.0)],
    # the gatehouse (v7: the place the budget goes, Mike): two fluted spiked towers
    # (x, y, radius, top, horn length) either side of a gate block whose pointed arch has
    # three receding archivolt rings (inner and outer half-span offsets, how far proud), a
    # portcullis and a spiked gallery
    "gate_towers": [(5.6, -25.6, 1.9, 15.0, 2.8), (-5.6, -25.6, 1.9, 15.0, 2.8)],
    "gate": {"half_width": 2.4, "springline": 4.5, "front_y": -27.6, "back_y": -23.6,
             "block_half_x": 3.9, "block_top": 13.5, "rings": [(0.0, 0.45, 0.15), (0.45, 0.85, 0.27),
                                                                (0.85, 1.15, 0.4)]},
    # square turrets on terrace 3's parapet
    "turrets": {"r": 13.4, "angles": [0, 40, 140, 180, -20, -160], "size": 1.8, "top": 18.6},
    # houses on the four lower terraces: (radius, floor z, radial depth, width, height, roof,
    # angle step). v9 halves the count and widens each house ("we don't need that many",
    # Mike) and drops v8's row in the keep courtyard, hidden under the flying buttresses
    "houses": [(20.4, 5.4, 1.6, 2.9, 2.4, 1.7, 14), (17.4, 10.0, 1.5, 2.9, 2.2, 1.7, 15),
               (15.1, 12.8, 1.3, 2.4, 1.8, 1.4, 16), (11.9, 15.7, 1.8, 2.7, 2.2, 1.8, 19)],
    "stairs_clear_deg": 9,
    "merlon": {"w": 0.6, "h": 0.7, "gap": 0.55, "d": 0.5},
    "parapet": {"h": 1.0, "t": 0.45},
    "segments": 128,
    "tower_segments": 32,
    "flying_buttresses": {"angles": [45, 112, 165, -15, -68, -127], "z_keep": 25.5,
                          "r_keep": 5.0, "r_wall": 9.1, "z_wall": 21.5},
    # sculpted towers after Mike's reference (fluted shaft, every rib ending in a winged horn
    # spike): rib spacing round the base, top radius as a fraction of the base; the central
    # keep tower's own numbers; the keep drum's blade crown
    "sculpt": {
        "towers": {"rib_pitch_m": 1.05, "taper": 0.9},
        "central": {"ribs": 16, "r_top": 2.3, "crown_len": 4.4, "segs": 24},
        "keep": {"crown": 28, "length": 1.7, "d": 0.45, "sides": 6, "segs": 7},
    },
}

MATS = ["carn_dum_stone", "carn_dum_paving", "carn_dum_iron", "carn_dum_dark", "carn_dum_roof"]
STONE, PAVING, IRON, DARK, ROOF = range(5)
# which part of the fortress a face belongs to, kept as the face attribute "part" (names in
# the object's "parts" property) so tools/blender/unwrap_texture_sets.py can split texture sets
PARTS = ["terraces", "outer_wall", "inner_wall", "houses", "keep", "central_tower", "towers", "gate",
         "rear_tower", "turrets", "stairs"]


class Builder:
    """Accumulates flat-shaded geometry with a material index and a part per face."""

    def __init__(self):
        self.bm = bmesh.new()
        self.part_layer = self.bm.faces.layers.int.new("part")
        self.part = 0

    def set_part(self, name):
        self.part = PARTS.index(name)

    # ---- primitives -------------------------------------------------------------------

    def face(self, pts, mat):
        verts = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(verts)
        f.material_index = mat
        f.smooth = False
        f[self.part_layer] = self.part
        return f

    def face_facing(self, pts, mat, want):
        """A face whose winding is flipped if its normal points away from `want`."""
        pv = [Vector(p) for p in pts]
        n = (pv[1] - pv[0]).cross(pv[2] - pv[0])
        if n.dot(Vector(want)) < 0:
            pts = list(reversed(pts))
        return self.face(pts, mat)

    def prism_ring(self, r_in, r_out, z0, z1, segs, top_mat=PAVING, side_mat=STONE, z0_in=None, outer=True):
        """Annular prism (outer and inner walls, flat top), or a solid cylinder when r_in is 0.
        z0_in starts the inner face at its own height and outer=False leaves the outer face out,
        so no face reaches down into the solid that hides it: one face per segment spans its
        whole height, and the unwrap keeps a face's full density when part of it shows (v11)."""
        zi = z0 if z0_in is None else z0_in
        ring = [(math.cos(2 * math.pi * i / segs), math.sin(2 * math.pi * i / segs)) for i in range(segs)]
        for i in range(segs):
            (c0, s0), (c1, s1) = ring[i], ring[(i + 1) % segs]
            if outer:
                self.face([(r_out * c0, r_out * s0, z0), (r_out * c1, r_out * s1, z0),
                           (r_out * c1, r_out * s1, z1), (r_out * c0, r_out * s0, z1)], side_mat)
            if r_in > 0:
                self.face([(r_in * c1, r_in * s1, zi), (r_in * c0, r_in * s0, zi),
                           (r_in * c0, r_in * s0, z1), (r_in * c1, r_in * s1, z1)], side_mat)
                self.face([(r_in * c0, r_in * s0, z1), (r_out * c0, r_out * s0, z1),
                           (r_out * c1, r_out * s1, z1), (r_in * c1, r_in * s1, z1)], top_mat)
        if r_in <= 0:
            self.face([(r_out * c, r_out * s, z1) for c, s in ring], top_mat)

    def cylinder(self, x, y, r, z0, z1, segs=16, top_mat=PAVING, side_mat=STONE):
        ring = [(x + r * math.cos(2 * math.pi * i / segs), y + r * math.sin(2 * math.pi * i / segs))
                for i in range(segs)]
        for i in range(segs):
            (ax, ay), (bx, by) = ring[i], ring[(i + 1) % segs]
            self.face([(ax, ay, z0), (bx, by, z0), (bx, by, z1), (ax, ay, z1)], side_mat)
        self.face([(px, py, z1) for px, py in ring], top_mat)

    def cone(self, x, y, r, z0, z1, segs=6, mat=STONE):
        ring = [(x + r * math.cos(2 * math.pi * i / segs), y + r * math.sin(2 * math.pi * i / segs))
                for i in range(segs)]
        for i in range(segs):
            (ax, ay), (bx, by) = ring[i], ring[(i + 1) % segs]
            self.face([(ax, ay, z0), (bx, by, z0), (x, y, z1)], mat)

    def frustum(self, x, y, r_bottom, r_top, z0, z1, segs, mat=STONE):
        """Sloped side band only (a talus at a wall or tower foot)."""
        for i in range(segs):
            a0, a1 = 2 * math.pi * i / segs, 2 * math.pi * (i + 1) / segs
            self.face([(x + r_bottom * math.cos(a0), y + r_bottom * math.sin(a0), z0),
                       (x + r_bottom * math.cos(a1), y + r_bottom * math.sin(a1), z0),
                       (x + r_top * math.cos(a1), y + r_top * math.sin(a1), z1),
                       (x + r_top * math.cos(a0), y + r_top * math.sin(a0), z1)], mat)

    def box(self, center, size, rot_z=0.0, mat=STONE, top_mat=None):
        """size is (local x, local y, z); rot_z turns local x to that angle (radial when rot_z = a)."""
        self.obox(center, size, Matrix.Rotation(rot_z, 3, "Z"), mat, top_mat)

    def obox(self, center, size, m, mat=STONE, top_mat=None):
        """Box under any 3x3 rotation m (local x, y, z sizes)."""
        cx, cy, cz = center
        sx, sy, sz = (s / 2 for s in size)
        p = [m @ Vector((dx * sx, dy * sy, dz * sz)) + Vector((cx, cy, cz))
             for dz in (-1, 1) for dy in (-1, 1) for dx in (-1, 1)]
        top = top_mat if top_mat is not None else mat
        # wound outward (v10: up to v9 every box face pointed inward; Cycles draws back faces,
        # so only the visibility pass of tools/blender/unwrap_texture_sets.py caught it)
        for idx, fm in (((2, 3, 1, 0), mat), ((5, 7, 6, 4), top), ((1, 5, 4, 0), mat),
                        ((6, 7, 3, 2), mat), ((4, 6, 2, 0), mat), ((3, 7, 5, 1), mat)):
            self.face([p[i] for i in idx], fm)

    def pyramid(self, center, size, z0, h, rot_z=0.0, mat=STONE):
        """Four-sided roof or cap over a (local x, local y) footprint."""
        cx, cy = center
        sx, sy = size[0] / 2, size[1] / 2
        m = Matrix.Rotation(rot_z, 3, "Z")
        base = [m @ Vector(v) + Vector((cx, cy, z0))
                for v in ((-sx, -sy, 0), (sx, -sy, 0), (sx, sy, 0), (-sx, sy, 0))]
        apex = Vector((cx, cy, z0 + h))
        for i in range(4):
            self.face([base[i], base[(i + 1) % 4], apex], mat)

    def gable(self, center, size, z0, h, rot_z=0.0, mat=ROOF):
        """Gabled roof over a (radial, tangential) footprint, ridge along the tangent."""
        cx, cy = center
        sx, sy = size[0] / 2, size[1] / 2
        m = Matrix.Rotation(rot_z, 3, "Z")
        o = Vector((cx, cy, z0))
        p = [m @ Vector(v) + o for v in ((-sx, -sy, 0), (sx, -sy, 0), (sx, sy, 0), (-sx, sy, 0),
                                         (0, -sy, h), (0, sy, h))]
        self.face([p[0], p[1], p[4]], STONE)  # both gable ends are masonry (to v10 one was roof)
        self.face([p[2], p[3], p[5]], STONE)
        self.face([p[1], p[2], p[5], p[4]], mat)
        self.face([p[3], p[0], p[4], p[5]], mat)

    # ---- sculpted spikes --------------------------------------------------------------

    def blade(self, base, d0, d1, length, w, d, sides=6, segs=8, taper_start=0.12, mat=STONE, taper_pow=0.9):
        """A curved stone blade: a tube along a quadratic curve that starts along d0 and ends
        along d1, its cross-section w across (perpendicular to the bending plane) and d deep
        (in it), full size until taper_start then falling to a sharp tip; taper_pow above 1
        flares the base (concave flanks)."""
        b0 = Vector(base)
        d0, d1 = Vector(d0).normalized(), Vector(d1).normalized()
        p1 = b0 + d0 * (length * 0.45)
        p2 = p1 + d1 * (length * 0.55)
        pn = d0.cross(d1)
        if pn.length < 1e-6:
            pn = d0.cross(Vector((1, 0, 0)) if abs(d0.x) < 0.9 else Vector((0, 1, 0)))
        pn.normalize()
        rings = []
        for i in range(segs):
            s = i / segs
            p = (1 - s) ** 2 * b0 + 2 * (1 - s) * s * p1 + s * s * p2
            t = (2 * (1 - s) * (p1 - b0) + 2 * s * (p2 - p1)).normalized()
            v2 = t.cross(pn).normalized()
            f = 1.0 if s <= taper_start else ((1 - s) / (1 - taper_start)) ** taper_pow
            rings.append([p + f * (w / 2 * math.cos(2 * math.pi * k / sides) * pn
                                   + d / 2 * math.sin(2 * math.pi * k / sides) * v2)
                          for k in range(sides)])
        for i in range(segs - 1):
            ra, rb = rings[i], rings[i + 1]
            for k in range(sides):
                self.face([ra[k], ra[(k + 1) % sides], rb[(k + 1) % sides], rb[k]], mat)
        last = rings[-1]
        for k in range(sides):
            self.face([last[k], last[(k + 1) % sides], p2], mat)

    def tapered_rib(self, x, y, a, stations, sides=6):
        """A stone rib on a tower at angle a through stations (z, face radius, w across, d deep),
        bottom first, its inner part buried in the face; a station on the sloped foot bends the
        rib down the slope. Returns the top centre."""
        u = Vector((math.cos(a), math.sin(a), 0.0))
        tv = Vector((-math.sin(a), math.cos(a), 0.0))

        def ring(z, r, w, d):
            c = Vector((x, y, z)) + u * (r + d / 2 - 0.08)
            return c, [c + tv * (w / 2 * math.cos(2 * math.pi * k / sides)) + u * (d / 2 * math.sin(2 * math.pi * k / sides))
                       for k in range(sides)]

        rings = [ring(*s) for s in stations]
        for (c0, ra), (c1, rb) in zip(rings, rings[1:]):
            for k in range(sides):
                off = (ra[k] + ra[(k + 1) % sides] + rb[k] + rb[(k + 1) % sides]) / 4 - (c0 + c1) / 2
                self.face_facing([ra[k], ra[(k + 1) % sides], rb[(k + 1) % sides], rb[k]], STONE,
                                 off - UP * off.dot(UP))
        return rings[-1][0]

    def horn_spike(self, base, a, length, w, d, lean_deg, sides, segs):
        """The reference tower spike: a thick horn (about 4:1) with a strongly flared, concave
        base, curving outward along angle a. (Its wings are now one arc_flange per gap: two
        per spike crossed their neighbours', Mike's v7 note.)"""
        u = Vector((math.cos(a), math.sin(a), 0.0))
        t = math.radians(lean_deg)
        b0 = Vector(base) - UP * 0.06
        self.blade(b0, (UP + u * 0.05).normalized(), UP * math.cos(t) + u * math.sin(t), length, w, d, sides, segs,
                   taper_start=0.0, taper_pow=1.85)

    def arc_flange(self, p_a, p_b, out, sag, w, d, sides=6, segs=9, thorn=0.0):
        """One continuous curved flange from p_a to p_b (two neighbouring spikes' flanks),
        bowing outward along `out` and dipping by `sag` mid-gap, thickest in the middle, both
        ends buried in the spikes; a small downward thorn at its low point when thorn > 0."""
        pa, pb = Vector(p_a), Vector(p_b)
        p1 = (pa + pb) / 2 + Vector(out) * 0.6 - UP * (2 * sag)
        rings = []
        for i in range(segs + 1):
            s = i / segs
            p = (1 - s) ** 2 * pa + 2 * (1 - s) * s * p1 + s * s * pb
            t = (2 * (1 - s) * (p1 - pa) + 2 * s * (pb - p1)).normalized()
            ax = t.cross(UP)
            ax = ax.normalized() if ax.length > 1e-6 else Vector((1, 0, 0))
            bx = ax.cross(t).normalized()
            f = 0.6 + 0.4 * math.sin(math.pi * s)
            rings.append([p + f * (w / 2 * math.cos(2 * math.pi * k / sides) * ax
                                   + d / 2 * math.sin(2 * math.pi * k / sides) * bx) for k in range(sides)])
        for i in range(segs):
            ra, rb = rings[i], rings[i + 1]
            for k in range(sides):
                mid = (ra[k] + rb[(k + 1) % sides]) / 2
                centre = sum(ra, Vector()) / sides
                self.face_facing([ra[k], ra[(k + 1) % sides], rb[(k + 1) % sides], rb[k]], STONE, mid - centre)
        if thorn > 0:
            low = 0.25 * pa + 0.5 * p1 + 0.25 * pb
            o = Vector(out).normalized()
            self.blade(low + UP * (d * 0.2), (-UP + o * 0.45), (-UP * 0.6 + o * 0.8), thorn, w * 0.8, d * 0.6, 5, 4,
                       taper_start=0.0, taper_pow=1.3)

    # ---- the gate -----------------------------------------------------------------------

    @staticmethod
    def pointed_arch(a, zs, z_bottom, n):
        """Outline (x, z) of an equilateral pointed arch of half-span a springing at zs, from
        the left foot up the jamb, over the apex and down to the right foot."""
        pts = [(-a, z_bottom), (-a, zs)]
        for i in range(1, n + 1):  # left arc, centre (a, zs), radius 2a, 180 -> 120 deg
            th = math.pi - (math.pi / 3) * i / n
            pts.append((a + 2 * a * math.cos(th), zs + 2 * a * math.sin(th)))
        for i in range(1, n + 1):  # right arc, centre (-a, zs), 60 -> 0 deg
            th = (math.pi / 3) * (1 - i / n)
            pts.append((-a + 2 * a * math.cos(th), zs + 2 * a * math.sin(th)))
        pts.append((a, z_bottom))
        return pts

    def arch_frame(self, y_front, depth, a_in, a_out, zs, n=10, mat=STONE):
        """A pointed archivolt ring proud of a facade: the band between the arches of half-span
        a_in and a_out (both springing at zs, feet at 0), from y_front back by depth."""
        inner = self.pointed_arch(a_in, zs, 0.0, n)
        outer = self.pointed_arch(a_out, zs, 0.0, n)
        yb = y_front + depth
        for i in range(len(inner) - 1):
            (xi0, zi0), (xi1, zi1) = inner[i], inner[i + 1]
            (xo0, zo0), (xo1, zo1) = outer[i], outer[i + 1]
            self.face_facing([(xi0, y_front, zi0), (xi1, y_front, zi1), (xo1, y_front, zo1), (xo0, y_front, zo0)],
                             mat, (0, -1, 0))
            self.face_facing([(xi0, y_front, zi0), (xi1, y_front, zi1), (xi1, yb, zi1), (xi0, yb, zi0)], mat,
                             (-(xi0 + xi1) / 2, 0, -((zi0 + zi1) / 2 - zs) if (zi0 + zi1) / 2 > zs else 0.0))
            self.face_facing([(xo0, y_front, zo0), (xo1, y_front, zo1), (xo1, yb, zo1), (xo0, yb, zo0)], mat,
                             ((xo0 + xo1) / 2, 0, max(0.0, (zo0 + zo1) / 2 - zs)))

    def gatehouse(self):
        """The Carn Dum gate: a gate block between the gate towers carrying a pointed arch with
        receding archivolts, a dark opening and an arch-shaped iron portcullis, and a corbelled
        gallery of spike merlons. (v8's two corner horns stuck out into the towers and its
        fan-of-horns sigil looked odd; Mike removed both.)"""
        g = LAYOUT["gate"]
        a, zs = g["half_width"], g["springline"]
        hx, yf, yb, top = g["block_half_x"], g["front_y"], g["back_y"], g["block_top"]
        self.box((0.0, (yf + yb) / 2, (BASE + top) / 2), (2 * hx, yb - yf, top - BASE), top_mat=PAVING)
        # archivolts: each ring steps forward of the one inside it
        for d_in, d_out, proud in g["rings"]:
            self.arch_frame(yf - proud, proud + 0.05, a + d_in, a + d_out, zs)
        # dark opening and the portcullis whose bars follow the arch
        dark = self.pointed_arch(a, zs, 0.0, 10)
        self.face_facing([(x, yf - 0.02, z) for x, z in dark], DARK, (0, -1, 0))

        def arch_top(x):
            return zs + math.sqrt(max(0.0, 4 * a * a - (a + abs(x)) ** 2))

        def half_width_at(z):
            return a if z <= zs else math.sqrt(max(0.0, 4 * a * a - (z - zs) ** 2)) - a

        bar_y = yf - 0.1
        for i in range(9):
            x = -a + 0.35 + i * (2 * a - 0.7) / 8
            h = arch_top(x) - 0.12
            self.box((x, bar_y, h / 2), (0.13, 0.13, h), mat=IRON)
        for z in (1.4, 3.2, 5.0, 6.6):
            hw = half_width_at(z) - 0.15
            if hw > 0.3:
                self.box((0.0, bar_y - 0.07, z), (2 * hw, 0.1, 0.14), mat=IRON)
        # corbelled gallery along the top front with a row of spike merlons
        self.box((0.0, yf - 0.3, top - 0.35), (2 * hx + 0.4, 0.7, 0.7), top_mat=PAVING)
        for i in range(10):
            x = -hx + 0.3 + i * (2 * hx - 0.6) / 9
            self.box((x, yf - 0.25, top - 1.0), (0.25, 0.5, 0.5))
            self.horn_spike((x, yf - 0.45, top), -math.pi / 2, 1.5 if i % 2 == 0 else 1.0, 0.45, 0.45, 10, 6, 8)

    def fluted_tower(self, x, y, r_bottom, r_top, z_foot, top, crown_len, n_ribs, segs, foot=True,
                     windows=True):
        """A round tower sculpted after Mike's reference: one slightly tapering shaft fluted by
        n deep ribs that flare into buttress feet, every rib ending in a winged horn spike
        above a low notched parapet. No tiers, no centre spire."""
        if foot:
            self.frustum(x, y, r_bottom + 0.6, r_bottom, BASE, z_foot, segs)
        self.frustum(x, y, r_bottom, r_top, z_foot, top, segs)
        self.face([(x + r_top * math.cos(2 * math.pi * i / segs), y + r_top * math.sin(2 * math.pi * i / segs), top)
                   for i in range(segs)], PAVING)
        pitch = 2 * math.pi * r_bottom / n_ribs
        w, d = pitch * 0.42, min(0.55, 0.22 + r_bottom * 0.11)
        w_spike = pitch * 0.75
        cs = math.cos(math.pi / segs)  # a flat segment face sits inside the vertex radius
        if foot:
            # v9: the ribs run down the sloped foot into the buried base, flaring as they go;
            # stopped above the foot they looked cut off (Mike, v8)
            stations = [(BASE, (r_bottom + 0.6) * cs, w * 1.5, d * 1.5), (z_foot, r_bottom * cs, w * 1.25, d * 1.15),
                        (top, r_top * cs, w * 0.92, d)]
        else:
            stations = [(z_foot, r_bottom * cs, w * 1.25, d * 1.2), (top, r_top * cs, w * 0.92, d)]
        tops = []
        for i in range(n_ribs):
            a = 2 * math.pi * i / n_ribs
            c1 = self.tapered_rib(x, y, a, stations, sides=6)
            self.horn_spike(c1, a, crown_len * (0.95 if i % 2 == 0 else 0.75), w_spike, max(0.75, d * 1.6), 16, 8, 14)
            tops.append((c1, a))
            # notched parapet piece and a small merlon in the gap to the next rib
            am = a + math.pi / n_ribs
            gap = pitch * 0.55
            rr = r_top - 0.1
            self.box((x + rr * math.cos(am), y + rr * math.sin(am), top + 0.3), (0.3, gap, 0.6), rot_z=am,
                     top_mat=PAVING)
            self.box((x + rr * math.cos(am), y + rr * math.sin(am), top + 0.85), (0.3, gap * 0.45, 0.5), rot_z=am,
                     top_mat=PAVING)
        # one continuous thorned flange per gap, from each spike's flank to the next one's
        h_fl = crown_len * 0.1
        for i, (c_a, a_a) in enumerate(tops):
            c_b, a_b = tops[(i + 1) % n_ribs]
            tv_a = Vector((-math.sin(a_a), math.cos(a_a), 0.0))
            tv_b = Vector((-math.sin(a_b), math.cos(a_b), 0.0))
            am = a_a + math.pi / n_ribs
            out = Vector((math.cos(am), math.sin(am), 0.0))
            self.arc_flange(c_a + tv_a * (w_spike * 0.34) + UP * h_fl, c_b - tv_b * (w_spike * 0.34) + UP * h_fl,
                            out * 0.5, 0.18 + crown_len * 0.05, max(0.12, pitch * 0.13), max(0.26, pitch * 0.3),
                            thorn=0.35 + crown_len * 0.1)
        if windows:
            for i in range(0, n_ribs, max(1, n_ribs // 4)):
                am = 2 * math.pi * (i + 0.5) / n_ribs
                for frac in (0.3, 0.68):
                    zc = z_foot + (top - z_foot) * frac
                    rr = (r_bottom + (r_top - r_bottom) * frac) * math.cos(math.pi / segs)
                    self.window(x + rr * math.cos(am), y + rr * math.sin(am), am, zc, 0.35, 1.0)

    # ---- architecture -----------------------------------------------------------------

    def window(self, px, py, a, zc, w, h, t=0.08, frame=True):
        """Dark opening on a wall face whose outward normal points at angle a: a panel proud
        of the face plus a pointed-arch triangle on top; framed with a sill and a hood."""
        n = Vector((math.cos(a), math.sin(a), 0.0))
        tv = Vector((-math.sin(a), math.cos(a), 0.0))
        c = Vector((px, py, zc)) + n * (t / 2 + 0.01)
        self.box(c, (t, w, h), rot_z=a, mat=DARK)
        front = Vector((px, py, zc + h / 2)) + n * (t + 0.012)
        self.face([front - tv * (w / 2), front + tv * (w / 2), front + Vector((0, 0, w * 0.6))], DARK)
        if frame:
            self.box(Vector((px, py, zc - h / 2 - 0.06)) + n * 0.1, (0.2, w + 0.22, 0.12), rot_z=a)
            self.box(Vector((px, py, zc + h / 2 + w * 0.6 + 0.08)) + n * 0.08, (0.16, w * 0.6, 0.14), rot_z=a)

    def buttress(self, r, a, z0, z1, w=0.9, d=0.8):
        rc = r + d / 2 - 0.05
        x, y = rc * math.cos(a), rc * math.sin(a)
        self.box((x, y, (z0 + z1) / 2), (d, w, z1 - z0), rot_z=a)
        self.pyramid((x, y), (d, w), z1, 0.7, rot_z=a)

    def merlons_ring(self, r, z, skip=None):
        """Merlons round a ring; skip(angle in degrees) leaves out the ones inside a tower."""
        mer = LAYOUT["merlon"]
        n = max(6, int(2 * math.pi * r / (mer["w"] + mer["gap"])))
        for i in range(n):
            a = 2 * math.pi * (i + 0.5) / n
            if skip and skip(math.degrees(a)):
                continue
            self.box((r * math.cos(a), r * math.sin(a), z + mer["h"] / 2), (mer["d"], mer["w"], mer["h"]),
                     rot_z=a, top_mat=PAVING)

    def merlons_line(self, p0, p1, z, rot):
        mer = LAYOUT["merlon"]
        length = (Vector(p1) - Vector(p0)).length
        n = max(2, int(length / (mer["w"] + mer["gap"])))
        for i in range(n):
            f = (i + 0.5) / n
            x, y = p0[0] + (p1[0] - p0[0]) * f, p0[1] + (p1[1] - p0[1]) * f
            self.box((x, y, z + mer["h"] / 2), (mer["d"], mer["w"], mer["h"]), rot_z=rot, top_mat=PAVING)

    def merlons_square(self, cx, cy, wx, wy, z):
        hx, hy = wx / 2, wy / 2
        self.merlons_line((cx - hx, cy - hy), (cx + hx, cy - hy), z, math.pi / 2)
        self.merlons_line((cx + hx, cy - hy), (cx + hx, cy + hy), z, 0.0)
        self.merlons_line((cx + hx, cy + hy), (cx - hx, cy + hy), z, math.pi / 2)
        self.merlons_line((cx - hx, cy + hy), (cx - hx, cy - hy), z, 0.0)

    def brackets_ring(self, x, y, r, z_top, count, depth=0.35, w=0.25, h=0.5):
        """Corbel brackets under an overhanging parapet (machicolations)."""
        for i in range(count):
            a = 2 * math.pi * (i + 0.5) / count
            rc = r + depth / 2
            self.box((x + rc * math.cos(a), y + rc * math.sin(a), z_top - h / 2), (depth, w, h), rot_z=a)

    def sloped_wall(self, x, y0, y1, z_bottom, z_top0, z_top1, t=0.3, mat=STONE):
        """A low wall along y at x whose top rises from z_top0 at y0 to z_top1 at y1."""
        xo, xi = x + t / 2, x - t / 2
        sgn = 1.0 if y1 > y0 else -1.0
        self.face_facing([(xo, y0, z_bottom), (xo, y1, z_bottom), (xo, y1, z_top1), (xo, y0, z_top0)],
                         mat, (1, 0, 0))
        self.face_facing([(xi, y0, z_bottom), (xi, y1, z_bottom), (xi, y1, z_top1), (xi, y0, z_top0)],
                         mat, (-1, 0, 0))
        self.face_facing([(xi, y0, z_top0), (xo, y0, z_top0), (xo, y1, z_top1), (xi, y1, z_top1)],
                         PAVING, (0, 0, 1))
        self.face_facing([(xi, y0, z_bottom), (xo, y0, z_bottom), (xo, y0, z_top0), (xi, y0, z_top0)],
                         mat, (0, -sgn, 0))
        self.face_facing([(xi, y1, z_bottom), (xo, y1, z_bottom), (xo, y1, z_top1), (xi, y1, z_top1)],
                         mat, (0, sgn, 0))

    def flying_buttress(self, a, r0, z0, r1, z1, w=0.5, h=0.6):
        """A slanted beam from (r0, z0) out and down to (r1, z1) along angle a, with a capped
        pier at the outer end."""
        dr, dz = r1 - r0, z1 - z0
        m = Matrix.Rotation(a, 3, "Z") @ Matrix.Rotation(-math.atan2(dz, dr), 3, "Y")
        rc, zc = (r0 + r1) / 2, (z0 + z1) / 2
        self.obox((rc * math.cos(a), rc * math.sin(a), zc), (math.hypot(dr, dz), w, h), m)
        px, py = r1 * math.cos(a), r1 * math.sin(a)
        self.box((px, py, z1 + 0.525), (0.6, 0.6, 1.35), rot_z=a)  # 0.15 m down into the wall it stands on
        self.pyramid((px, py), (0.6, 0.6), z1 + 1.2, 0.9, rot_z=a)

    def blade_crown(self, x, y, r_face, z, n, tall, short, depth, lean_deg, sides, segs, extra_every=0,
                    extra_scale=1.5, width_frac=0.55):
        """The merlons turned into spikes: n blades on a crown ring whose outer face is at
        r_face, each as wide as a merlon (width_frac of the pitch) and flush with that face,
        triangular from the base, alternately tall and short (every extra_every-th taller
        still). Returns the blade angles."""
        pitch = 2 * math.pi * r_face / n
        w = pitch * width_frac
        rc = r_face - depth / 2 + 0.05  # flush with the ribs, which stand 5 cm proud of the crown
        angles = []
        for i in range(n):
            a = 2 * math.pi * (i + 0.5) / n
            ln = tall if i % 2 == 0 else short
            if extra_every and i % extra_every == 0:
                ln *= extra_scale
            u = Vector((math.cos(a), math.sin(a), 0.0))
            t = math.radians(lean_deg)
            self.blade((x + rc * math.cos(a), y + rc * math.sin(a), z - 0.04), (UP + u * 0.06).normalized(),
                       UP * math.cos(t) + u * math.sin(t), ln, w, depth, sides, segs, taper_start=0.04)
            angles.append(a)
        return angles



def blocked(a_deg, centres, half_widths):
    for c, hw in zip(centres, half_widths):
        if abs((a_deg - c + 180) % 360 - 180) < hw:
            return True
    return False



def build():
    L = LAYOUT
    b = Builder()
    segs = L["segments"]
    pt = L["parapet"]
    sc = L["sculpt"]
    random.seed(7)

    # what stands on the outer ring, as (angle, half-width) in degrees, so wall merlons keep
    # out of it
    ow = L["outer_wall"]
    ring_r = ow["r_out"]
    centres, halfs = [], []
    for x, y, r, *_ in L["round_towers"] + L["gate_towers"]:
        if abs(math.hypot(x, y) - ring_r) < 4:
            centres.append(math.degrees(math.atan2(y, x)))
            halfs.append(math.degrees(r / ring_r) + 3)
    for x, y, wx, wy, *_ in L["square_towers"]:
        centres.append(math.degrees(math.atan2(y, x)))
        halfs.append(math.degrees(wx / 2 / ring_r) + 3)
    centres.append(-90.0)
    halfs.append(math.degrees(L["gate"]["block_half_x"] / ring_r) + 1)

    # terraces: stacked rings, each open under the next one so its top face is the visible step
    # only (v10: a solid disc top was one face, mostly hidden). Each riser is seated 0.3 m into
    # the step in front of it, not in the ground (v11: a riser from BASE was 58 to 88% buried
    # and the unwrap kept it at full density, a third of the town set's tile). Terrace 0 has no
    # riser: it stood back to back with the outer wall's inner face.
    b.set_part("terraces")
    terr = L["terraces"]
    for i, (r, top) in enumerate(terr):
        r_in = terr[i + 1][0] - 0.3 if i + 1 < len(terr) else L["keep"]["r"] + 0.4
        z0 = BASE if i == 0 else terr[i - 1][1] - 0.3
        b.prism_ring(r_in, r, z0, top, segs, outer=i > 0)

    # walls are plain since v7 ("no one really cares about the wall", Mike): the outer wall
    # keeps its sloped foot and square merlons; buttresses, windows, string course and
    # machicolations went to the gate. Merlons inside a tower are skipped: on the rear tower
    # their tops (11.0 m) were coplanar with its crown top and rendered black (v6 check).
    # The wall's outer face starts just under the talus top and its inner face just under the
    # terrace 0 step: below those they were hidden bands at full texture density (v11).
    b.set_part("outer_wall")
    talus_top = 2.6
    b.prism_ring(ow["r_in"], ow["r_out"], talus_top - 0.3, ow["top"], segs, z0_in=terr[0][1] - 0.3)
    b.frustum(0, 0, ow["r_out"] + 1.0, ow["r_out"], BASE, talus_top, segs)
    b.merlons_ring(ow["r_out"] - L["merlon"]["d"] / 2, ow["top"],
                   skip=lambda a: blocked(a, centres, [h - 1.5 for h in halfs]))
    b.set_part("inner_wall")
    iw = L["inner_wall"]
    b.prism_ring(iw["r_in"], iw["r_out"], iw["z0"], iw["top"], segs)
    b.merlons_ring(iw["r_out"] - L["merlon"]["d"] / 2, iw["top"])

    # terrace parapets
    b.set_part("terraces")
    terrace_tops = dict(L["terraces"])
    stair_clear = L["stairs_clear_deg"]
    for r in L["parapets"]:
        z = terrace_tops[r]
        b.prism_ring(r - pt["t"], r, z, z + pt["h"], segs)
        b.merlons_ring(r - pt["t"] / 2, z + pt["h"])

    # houses on the lower terraces, three kinds, clear of the stairs and the terrace-3 turrets: a
    # pyramid-roofed house, a two-storey gabled house with a chimney, a narrow tower-house
    b.set_part("houses")
    tu = L["turrets"]
    for r, ztop, depth, width, height, roof, step in L["houses"]:
        for i in range(int(360 / step)):
            a = i * step + (step / 2 if r < 16 else 0)
            if blocked(a, [90.0, -90.0], [stair_clear + 3, stair_clear + 3]):
                continue
            if 11 < r < 13 and blocked(a, tu["angles"], [8] * len(tu["angles"])):
                continue
            if random.random() < 0.1:
                continue
            ar = math.radians(a)
            x, y = r * math.cos(ar), r * math.sin(ar)
            kind = random.choice((0, 0, 1, 1, 2))
            if kind == 2:  # tower-house
                w2 = min(depth, width) * 0.8
                h = height * random.uniform(1.6, 2.0)
                b.box((x, y, ztop + h / 2), (w2, w2, h), rot_z=ar)
                b.pyramid((x, y), (w2 + 0.2, w2 + 0.2), ztop + h, roof * 1.8, rot_z=ar, mat=ROOF)
            elif kind == 1:  # gabled, two storeys, chimney
                h = height * random.uniform(1.25, 1.5)
                b.box((x, y, ztop + h / 2), (depth, width * 1.2, h), rot_z=ar)
                b.box((x, y, ztop + h * 0.5), (depth + 0.12, width * 1.2 + 0.12, 0.15), rot_z=ar)
                b.gable((x, y), (depth + 0.2, width * 1.2 + 0.2), ztop + h, roof, rot_z=ar)
                off = Matrix.Rotation(ar, 3, "Z") @ Vector((0.0, width * 0.4, 0.0))
                b.box((x + off.x, y + off.y, ztop + h + roof * 0.6), (0.35, 0.35, roof * 1.0), rot_z=ar)
            else:  # pyramid roof
                h = height * random.uniform(0.85, 1.2)
                b.box((x, y, ztop + h / 2), (depth, width, h), rot_z=ar)
                b.pyramid((x, y), (depth + 0.15, width + 0.15), ztop + h, roof * random.uniform(0.9, 1.3),
                          rot_z=ar, mat=ROOF)
            b.window(x + (depth / 2) * math.cos(ar), y + (depth / 2) * math.sin(ar), ar,
                     ztop + 1.2, 0.35, 0.8)
            if kind == 1:
                b.window(x + (depth / 2) * math.cos(ar), y + (depth / 2) * math.sin(ar), ar,
                         ztop + h - 0.9, 0.3, 0.7)

    # keep: sloped foot, drum with buttresses and windows, machicolated crown whose merlons
    # are stone blades, six pinnacle turrets, flying buttresses to the inner wall
    b.set_part("keep")
    k = L["keep"]
    ks = 48
    kc = sc["keep"]
    b.frustum(0, 0, k["r"] + 0.7, k["r"], 18.0, 19.5, ks)
    b.cylinder(0, 0, k["r"], 19.5, k["top"] - 0.6, segs=ks)
    b.cylinder(0, 0, k["r"] + 0.35, k["top"] - 0.6, k["top"], segs=ks)
    b.brackets_ring(0, 0, k["r"], k["top"] - 0.6, 30, depth=0.35)
    b.blade_crown(0, 0, k["r"] + 0.35, k["top"], kc["crown"], kc["length"] * 1.2, kc["length"] * 0.75, kc["d"], 10,
                  kc["sides"], kc["segs"])
    rf = k["r"] * math.cos(math.pi / ks)
    for i in range(12):
        a = math.radians(i * 30.0)
        b.buttress(rf, a, 19.5, k["top"] - 1.4, w=0.7, d=0.6)
        aw = math.radians(i * 30.0 + 15.0)
        b.window(rf * math.cos(aw), rf * math.sin(aw), aw, 22.6, 0.5, 1.5)
        b.window(rf * math.cos(aw), rf * math.sin(aw), aw, 20.4, 0.3, 0.8)
    for i in range(6):
        a = math.radians(30 + 60 * i)
        x, y = 4.35 * math.cos(a), 4.35 * math.sin(a)
        b.cylinder(x, y, 0.75, k["top"] - 1.0, k["top"] + 2.5, segs=12)
        b.cylinder(x, y, 0.9, k["top"] + 2.2, k["top"] + 2.5, segs=12)
        b.cone(x, y, 0.95, k["top"] + 2.5, k["top"] + 5.6, segs=12)
        b.blade_crown(x, y, 0.92, k["top"] + 2.5, 6, 1.1, 0.7, 0.26, 16, 5, 5)
    fb = L["flying_buttresses"]
    for ang in fb["angles"]:
        b.flying_buttress(math.radians(ang), fb["r_keep"] + 0.3, fb["z_keep"], fb["r_wall"],
                          fb["z_wall"] + 0.1)

    # central tower and round wall towers: fluted shafts whose ribs end in winged horn spikes
    b.set_part("central_tower")
    t = L["tower"]
    cc = sc["central"]
    b.fluted_tower(0, 0, t["r"], cc["r_top"], k["top"], t["top"], cc["crown_len"], cc["ribs"], cc["segs"],
                   foot=False)
    b.set_part("towers")
    tw = sc["towers"]
    for x, y, r, top, crown_len in L["round_towers"] + L["gate_towers"]:
        n_ribs = max(8, round(2 * math.pi * r / tw["rib_pitch_m"]))
        b.fluted_tower(x, y, r, r * tw["taper"], 2.4, top, crown_len, n_ribs, L["tower_segments"])

    # the gate between the gate towers
    b.set_part("gate")
    b.gatehouse()

    # the rear square tower: machicolated top, square merlons, windows
    b.set_part("rear_tower")
    for x, y, wx, wy, top in L["square_towers"]:
        b.box((x, y, (BASE + top - 0.6) / 2), (wx, wy, top - 0.6 - BASE), top_mat=PAVING)
        b.box((x, y, top - 0.3), (wx + 0.5, wy + 0.5, 0.6), top_mat=PAVING)
        nbx, nby = max(2, int(wx / 0.8)), max(2, int(wy / 0.8))
        for j in range(nbx):  # brackets under the overhang, front and back
            px = x + ((j + 0.5) / nbx - 0.5) * wx
            for py in (y - wy / 2 - 0.12, y + wy / 2 + 0.12):
                b.box((px, py, top - 0.85), (0.25, 0.25, 0.5))
        for j in range(nby):  # and the two sides
            py = y + ((j + 0.5) / nby - 0.5) * wy
            for px in (x - wx / 2 - 0.12, x + wx / 2 + 0.12):
                b.box((px, py, top - 0.85), (0.25, 0.25, 0.5))
        b.merlons_square(x, y, wx, wy, top)
        face_y = y - wy / 2 if y < 0 else y + wy / 2
        a = -math.pi / 2 if y < 0 else math.pi / 2
        for zc in (5.0, 8.4):
            b.window(x, face_y, a, zc, 0.5, 1.3)

    # terrace-3 turrets with pyramid caps
    b.set_part("turrets")
    for a in tu["angles"]:
        ar = math.radians(a)
        x, y = tu["r"] * math.cos(ar), tu["r"] * math.sin(ar)
        b.box((x, y, (15.7 + tu["top"]) / 2), (tu["size"], tu["size"], tu["top"] - 15.7), rot_z=ar,
              top_mat=PAVING)
        b.pyramid((x, y), (tu["size"] + 0.2, tu["size"] + 0.2), tu["top"], 1.4, rot_z=ar, mat=ROOF)
        b.window(x + (tu["size"] / 2) * math.cos(ar), y + (tu["size"] / 2) * math.sin(ar), ar,
                 17.0, 0.35, 0.9)

    # front and rear stairs up the terraces, with side walls rising with each flight
    b.set_part("stairs")
    steps = [(21.6, 19.0, 5.4, 10.0), (19.0, 16.3, 10.0, 12.8), (16.3, 14.0, 12.8, 15.7),
             (14.0, 10.2, 15.7, 18.0)]
    for sign in (-1, 1):
        for r_out, r_in, z_lo, z_hi in steps:
            count = 6
            for i in range(count):
                f = (i + 1) / count
                rr = r_out - (r_out - r_in) * (i + 0.5) / count
                b.box((0.0, sign * rr, (z_lo + (z_hi - z_lo) * f + z_lo) / 2),
                      (2.2, (r_out - r_in) / count + 0.02, (z_hi - z_lo) * f), top_mat=PAVING)
            for sx in (-1.25, 1.25):
                b.sloped_wall(sx, sign * r_out, sign * r_in, z_lo, z_lo + 0.9, z_hi + 0.9)

    bmesh.ops.remove_doubles(b.bm, verts=b.bm.verts, dist=1e-4)

    mesh = bpy.data.meshes.new("carn_dum_town")
    b.bm.to_mesh(mesh)
    b.bm.free()
    obj = bpy.data.objects.new("carn_dum_town", mesh)
    obj["parts"] = ",".join(PARTS)
    bpy.context.scene.collection.objects.link(obj)
    for name in MATS:
        mesh.materials.append(bpy.data.materials.get(name) or bpy.data.materials.new(name))
    return obj


RENDER_COLORS = {"carn_dum_stone": (0.42, 0.43, 0.46), "carn_dum_paving": (0.6, 0.6, 0.6),
                 "carn_dum_iron": (0.12, 0.12, 0.13), "carn_dum_dark": (0.03, 0.03, 0.035),
                 "carn_dum_roof": (0.2, 0.21, 0.24)}


def render(obj, out_dir):
    scene = bpy.context.scene
    for m in obj.data.materials:
        m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*RENDER_COLORS[m.name], 1)
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.render.resolution_x, scene.render.resolution_y = 1200, 900
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.65, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    scene.world = world
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", type="SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(55), 0.0, math.radians(-30))
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    shots = {"blockout_front": (Vector((0, -105, 30)), Vector((0, 0, 15))),
             "blockout_34": (Vector((70, -80, 55)), Vector((0, 0, 15))),
             "blockout_tower": (Vector((33, -14, 24)), Vector((24.25, -3.3, 18))),
             "blockout_gate": (Vector((7, -50, 9)), Vector((0, -26, 7.5))),
             "blockout_gate_34": (Vector((22, -44, 20)), Vector((0, -26, 8))),
             "blockout_tall": (Vector((32, 0, 30)), Vector((20.5, 13.2, 24))),
             "blockout_keep": (Vector((10, -24, 40)), Vector((0, 0, 32))),
             "blockout_foot": (Vector((14, -40, 4)), Vector((5.6, -25.6, 2.5)))}
    for name, (loc, target) in shots.items():
        cam.location = loc
        cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(out_dir, name + ".png")
        bpy.ops.render.render(write_still=True)


def main():
    out_dir = script_args()[0]
    with job(report_path(out_dir)) as report:
        os.makedirs(out_dir, exist_ok=True)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        obj = build()
        obj.data.calc_loop_triangles()
        bpy.context.view_layer.update()
        report.update({"tris": len(obj.data.loop_triangles), "faces": len(obj.data.polygons),
                       "dims_m": [round(v, 2) for v in obj.dimensions],
                       "faces_per_material": {m.name: sum(1 for p in obj.data.polygons if p.material_index == i)
                                              for i, m in enumerate(obj.data.materials)}})
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, "carn_dum_town_blockout.blend"))
        render(obj, out_dir)


if __name__ == "__main__":  # Blender runs a -P script as __main__
    main()
