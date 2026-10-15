"""
Render the measured Erebor catalog (blender_catalog_erebor.py output) as the
per-family markdown tables used in docs/kitbash/erebor/meshes.md. Prints to
stdout; paste the tables over the old ones when the kit is re-measured.

    python tools/oneoff/erebor_catalog_to_md.py [--json E:\\LOTRAOMAssets\\_export\\erebor\\catalog\\erebor_catalog.json]
"""

import argparse
import json
import re

FAMILIES = [
    ("Ground tiles and floor trim", r"^SM_DW_Ground_"),
    ("Pillars", r"^SM_DW_Pillar_"),
    ("Platforms (terrace blocks)", r"^SM_DW_Platform_"),
    ("Roof", r"^SM_DW_Roof_"),
    ("Wall overlays (beams, columns, capitals)", r"^SM_DW_Wall_(Beam|Clmn)"),
    ("Walls (3 m modules)", r"^SM_DW_Wall_3m_"),
    ("Decoration", r"^SM_DW_Pile_"),
    ("Castle", r"^SM_DW_Castle_(?!.*Mrln)"),
    ("Castle merlons", r"^SM_DW_Castle_.*Mrln"),
    ("Props (unit size, unplaced)", r"^(kys_|dw_pillar|SD_DW)"),
]

AXIS = {"min": "min", "max": "max", "mid": "mid"}


def rise_dir(az):
    """Slope normal azimuth (deg) -> the direction the surface rises toward."""
    a = (az + 180) % 360
    names = {0: "+X", 45: "+X+Y", 90: "+Y", 135: "-X+Y", 180: "-X", 225: "-X-Y", 270: "-Y", 315: "+X-Y"}
    return names.get(int(round(a / 45.0) * 45) % 360, f"{a:.0f} deg")


def notes(p):
    out = []
    for h in p["apertures"][:2]:
        out.append(f"opening {h['width']:.2f} x {h['height']:.2f} at z {round(h['z_min'], 2) + 0.0:.2f} "
                   f"(through {h['through']})")
    if len(p["apertures"]) > 2:
        out.append(f"+{len(p['apertures']) - 2} more openings")
    for s in p["slopes"][:2]:
        out.append(f"{s['deg']} deg slope rising toward {rise_dir(s['facing_az_deg'])}")
    if not p["node"]["identity"]:
        n = p["node"]
        bits = []
        if any(abs(a) > 0.01 for a in n["rot_deg"]):
            bits.append("rot " + "/".join(f"{a:g}" for a in n["rot_deg"]))
        if any(abs(s - 1) > 0.001 for s in n["scale"]):
            bits.append("scale " + "/".join(f"{s:g}" for s in n["scale"]))
        out.append("node " + ", ".join(bits) + " (baked)")
    if p["bo"] is None:
        out.append("**no bo_**")
    elif p["bo"]["name"][3:].lower() != p["name"].lower():
        out.append(f"bo_ is {p['bo']['name']}")
    return "; ".join(out)


def row(p):
    # round first, then + 0.0, so -0.002 prints as 0.00, not -0.00
    s, mn, mx = ([round(v, 2) + 0.0 for v in p[k]] for k in ("size", "min", "max"))
    piv = "/".join(AXIS.get(c, c) for c in p["pivot"])
    lods = ", ".join(f"lod{l['n']}" if l["n"] else "lod" for l in p["lods"]) or "none"
    top = p["top"].get("z_mode")
    top = None if top is None else round(top, 2) + 0.0
    return (f"| {p['idx']} | `{p['name']}` | {s[0]:.2f} x {s[1]:.2f} x {s[2]:.2f} | "
            f"{mn[2]:.2f} to {mx[2]:.2f} | {piv} | {'' if top is None else f'{top:.2f}'} | "
            f"{p['tris']} | {lods} | {notes(p)} |")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", default=r"E:\LOTRAOMAssets\_export\erebor\catalog\erebor_catalog.json")
    args = ap.parse_args()
    pieces = json.load(open(args.json, encoding="utf-8"))["pieces"]
    seen = set()
    for title, pat in FAMILIES:
        rx = re.compile(pat, re.IGNORECASE)
        rows = [p for p in pieces if rx.search(p["name"]) and p["idx"] not in seen]
        if not rows:
            continue
        print(f"### {title}\n")
        print("| # | Piece | Size X x Y x Z (m) | Z range | Pivot X/Y/Z | Top z | Tris | LODs | Measured notes |")
        print("|---|---|---|---|---|---|---|---|---|")
        for p in rows:
            seen.add(p["idx"])
            print(row(p))
        print()
    left = [p["name"] for p in pieces if p["idx"] not in seen]
    if left:
        print("UNGROUPED: " + ", ".join(left))


if __name__ == "__main__":
    main()
