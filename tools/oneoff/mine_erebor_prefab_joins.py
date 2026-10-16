"""
Mine the authored TAOM_Map prefabs for how the Erebor kit pieces are joined
(Phase 2 of docs/kitbash/erebor/blender-build-prompt.md). Read-only.

For every prefab that places a kit piece (sm_dw_*, kys_dw_*, dw_pillar_*,
sd_dw_*), resolve each entity's world transform through the <children>
nesting, then report:

  usage   per piece: placements, prefabs, Z rotations, non-Z rotations,
          scales, material overrides
  pairs   per (A, B) piece pair inside one prefab, B's pivot in A's local
          frame (rounded to 5 cm) and the Z-rotation difference, when the
          pivots are within --radius metres. The most frequent offsets are
          the de-facto join rules.

Rotation: rotation_euler is radians. The engine applies it as
MatrixFrame.Rotation.ApplyEulerAngles = RotateAboutUp(z), then
RotateAboutSide(x), then RotateAboutForward(y), which is R = Rz @ Rx @ Ry.
Placements with a non-Z rotation are counted and flagged, because almost
every kit placement is Z-only.

    python tools/oneoff/mine_erebor_prefab_joins.py --out E:\\LOTRAOMAssets\\_export\\erebor\\prefab_joins.json

--kit gondor mines the Gondor part families instead: a meta_mesh_component counts as a kit piece
when its name is the prefix of a part in the kit's FBX files (kitbash_kits.kit_part_names).
--prefabs <glob> limits the run to prefab files whose name matches (case-insensitive).
"""

import argparse
import fnmatch
import json
import math
import os
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kitbash_kits as kk  # noqa: E402

PREFABS = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\Prefabs"
KIT_RE = kk.KITS["erebor"]["kit_re"]


def mat_mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def rot_x(t):
    c, s = math.cos(t), math.sin(t)
    return [[1, 0, 0], [0, c, -s], [0, s, c]]


def rot_y(t):
    c, s = math.cos(t), math.sin(t)
    return [[c, 0, s], [0, 1, 0], [-s, 0, c]]


def rot_z(t):
    c, s = math.cos(t), math.sin(t)
    return [[c, -s, 0], [s, c, 0], [0, 0, 1]]


def parse_vec(s, default):
    if not s:
        return list(default)
    return [float(x) for x in s.split(",")]


class Frame:
    """Rotation-scale matrix M (3x3) and translation t: p_world = M p + t."""

    def __init__(self, m=None, t=None):
        self.m = m or [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
        self.t = t or [0.0, 0.0, 0.0]

    def compose(self, child):
        m = mat_mul(self.m, child.m)
        t = [sum(self.m[i][k] * child.t[k] for k in range(3)) + self.t[i] for i in range(3)]
        return Frame(m, t)


def local_frame(el):
    tr = el.find("transform")
    if tr is None:
        return Frame(), (0.0, 0.0, 0.0), (1.0, 1.0, 1.0)
    pos = parse_vec(tr.get("position"), (0, 0, 0))
    eul = parse_vec(tr.get("rotation_euler"), (0, 0, 0))
    scl = parse_vec(tr.get("scale"), (1, 1, 1))
    r = mat_mul(mat_mul(rot_z(eul[2]), rot_x(eul[0])), rot_y(eul[1]))
    m = [[r[i][j] * scl[j] for j in range(3)] for i in range(3)]
    return Frame(m, pos), tuple(eul), tuple(scl)


def walk(el, parent, prefab, out, path, is_kit=None):
    """Collect kit placements under el. is_kit: predicate on the lowercase component name
    (default: the Erebor KIT_RE)."""
    if is_kit is None:
        is_kit = KIT_RE.match
    frame_l, eul, scl = local_frame(el)
    frame = parent.compose(frame_l)
    comps = el.find("components")
    if comps is not None:
        for mm in comps.findall("meta_mesh_component"):
            name = (mm.get("name") or "").lower()
            if not is_kit(name):
                continue
            mats = [m.get("material") for m in mm.findall("mesh") if m.get("material")]
            # world Z rotation of the piece's +X axis
            yaw = math.degrees(math.atan2(frame.m[1][0], frame.m[0][0]))
            tilt = abs(frame.m[2][2]) < 0.999 * math.sqrt(sum(frame.m[i][2] ** 2 for i in range(3)))
            wscale = [math.sqrt(sum(frame.m[i][j] ** 2 for i in range(3))) for j in range(3)]
            out.append({"prefab": prefab, "piece": name, "pos": [round(v, 3) for v in frame.t],
                        "yaw": round(yaw, 1), "tilt": tilt,
                        "scale": [round(v, 3) for v in wscale], "local_euler": eul,
                        "mats": mats, "m": frame.m, "path": path})
    ch = el.find("children")
    if ch is not None:
        for c in ch.findall("game_entity"):
            walk(c, frame, prefab, out, path + "/" + (c.get("name") or "?"), is_kit)


def to_local(a, b):
    """b's pivot in a's frame (rotation only; scale divided out) and yaw delta."""
    d = [b["pos"][i] - a["pos"][i] for i in range(3)]
    yaw = math.radians(a["yaw"])
    c, s = math.cos(-yaw), math.sin(-yaw)
    lx, ly = c * d[0] - s * d[1], s * d[0] + c * d[1]
    dyaw = (b["yaw"] - a["yaw"] + 180) % 360 - 180
    return (round(lx / 0.05) * 0.05, round(ly / 0.05) * 0.05, round(d[2] / 0.05) * 0.05), round(dyaw)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--radius", type=float, default=4.5)
    ap.add_argument("--kit", default="erebor", choices=sorted(kk.KITS))
    ap.add_argument("--prefabs", default=None, help="glob on prefab file names")
    args = ap.parse_args()
    is_kit = kk.kit_matcher(args.kit)

    placements = []
    for fn in sorted(os.listdir(PREFABS)):
        if not fn.lower().endswith(".xml"):
            continue
        if args.prefabs and not fnmatch.fnmatch(fn.lower(), args.prefabs.lower()):
            continue
        try:
            root = ET.parse(os.path.join(PREFABS, fn)).getroot()
        except ET.ParseError as e:
            print(f"[skip] {fn}: {e}", file=sys.stderr)
            continue
        tops = [root] if root.tag == "game_entity" else root.findall("game_entity")
        for top in tops:
            before = len(placements)
            walk(top, Frame(), f"{fn}:{top.get('name')}", placements, top.get("name") or "?", is_kit)
            if len(placements) == before:
                continue

    usage = defaultdict(lambda: {"count": 0, "prefabs": set(), "yaw": Counter(), "tilted": 0,
                                 "scale": Counter(), "mats": Counter()})
    by_prefab = defaultdict(list)
    for p in placements:
        u = usage[p["piece"]]
        u["count"] += 1
        u["prefabs"].add(p["prefab"])
        u["yaw"][round(p["yaw"]) % 360] += 1
        u["tilted"] += 1 if p["tilt"] else 0
        u["scale"][tuple(round(v, 2) for v in p["scale"])] += 1
        for m in p["mats"]:
            u["mats"][m] += 1
        by_prefab[p["prefab"]].append(p)

    pairs = defaultdict(Counter)
    pair_prefabs = defaultdict(set)
    r2 = args.radius ** 2
    for prefab, ps in by_prefab.items():
        if len(ps) > 4000:
            print(f"[note] {prefab}: {len(ps)} placements (large scene)", file=sys.stderr)
        # bucket by 5 m cells to keep this linear-ish
        grid = defaultdict(list)
        for i, p in enumerate(ps):
            grid[(int(p["pos"][0] // 5), int(p["pos"][1] // 5))].append(i)
        for i, a in enumerate(ps):
            if a["tilt"]:
                continue
            gx, gy = int(a["pos"][0] // 5), int(a["pos"][1] // 5)
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    for j in grid.get((gx + dx, gy + dy), []):
                        if j == i:
                            continue
                        b = ps[j]
                        if b["tilt"]:
                            continue
                        d2 = sum((b["pos"][k] - a["pos"][k]) ** 2 for k in range(3))
                        if d2 > r2:
                            continue
                        off, dyaw = to_local(a, b)
                        key = (a["piece"], b["piece"])
                        pairs[key][(off, dyaw)] += 1
                        pair_prefabs[key].add(prefab)

    usage_out = {}
    for k, u in sorted(usage.items(), key=lambda kv: -kv[1]["count"]):
        usage_out[k] = {"count": u["count"], "prefab_count": len(u["prefabs"]),
                        "prefabs_sample": sorted(u["prefabs"])[:6],
                        "yaw_top": u["yaw"].most_common(6), "tilted": u["tilted"],
                        "scale_top": [[list(s), n] for s, n in u["scale"].most_common(4)],
                        "mat_overrides": u["mats"].most_common(4)}
    pairs_out = []
    for key, cnt in pairs.items():
        total = sum(cnt.values())
        if total < 3:
            continue
        pairs_out.append({"a": key[0], "b": key[1], "total": total,
                          "prefabs": len(pair_prefabs[key]),
                          "top": [{"offset": list(o), "dyaw": dy, "n": n}
                                  for (o, dy), n in cnt.most_common(6)]})
    pairs_out.sort(key=lambda p: -p["total"])
    prefab_summary = []
    for prefab, ps in sorted(by_prefab.items(), key=lambda kv: -len(kv[1])):
        xs = [p["pos"][0] for p in ps]
        ys = [p["pos"][1] for p in ps]
        prefab_summary.append({"prefab": prefab, "pieces": len(ps),
                               "distinct": len({p["piece"] for p in ps}),
                               "extent": [round(max(xs) - min(xs), 1), round(max(ys) - min(ys), 1)]})
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump({"placements": len(placements), "prefabs": prefab_summary,
                   "usage": usage_out, "pairs": pairs_out}, f, indent=1)
    print(f"placements {len(placements)} in {len(by_prefab)} prefabs; "
          f"{len(usage_out)} kit pieces used; {len(pairs_out)} pair kinds")


if __name__ == "__main__":
    main()
