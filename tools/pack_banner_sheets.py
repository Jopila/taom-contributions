#!/usr/bin/env python3
"""Pack the partly used banner icon sheets into as few sheets as they fit, and audit the result (#1047).

A banner key names an icon id. Main/_Module/ModuleData/banner_icons.xml maps each id to a material
(one 4096 x 4096 green-on-black sheet of 16 cells, source in Main/_Module/AssetSources/BannerIcons)
and a texture_index. Keys, saved banners and the UI sprites (named by icon id) never name a sheet,
so moving an icon to another sheet changes only its <Icon> row. All TAOM banner materials share one
shader (their _mtl.tpac files differ only in GUIDs, name and checksum, measured 2026-10-28).

Full sheets stay. The partly used ones are packed whole, first fit decreasing, into
taom_banners_packed_alpha_NN; cells are copied verbatim (two-colour cells keep their red ink) and
free cells get the red and green placeholder checkerboard of the existing sheets.

Usage:
    python tools/pack_banner_sheets.py            # dry run: the plan
    python tools/pack_banner_sheets.py --apply    # write the packed sources (repo and install) and the <Icon> rows
    python tools/pack_banner_sheets.py --audit    # the gate: exit 1 on any finding
    python tools/pack_banner_sheets.py --prune --backup-dir <dir>
        # after the Modding Kit saved the packed materials: delete the sheets no icon names, in the repo
        # (source, _mtl/_tex.tpac) and the install (the same, plus the texture's RuntimeDataCache .rdc),
        # each copied to <dir> first

Kit steps between --apply and --prune: import each packed sheet (Albedo DXT1, Do Not Generate Mips),
make a banner material of the same name, save, copy both .tpac into Main/_Module/Assets/BannerIcons
(docs/reference/banner-icon-generation.md).
"""
import argparse
import os
import re
import shutil
import sys
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parent.parent
CELLS, GRID = 16, 4
PACKED = "taom_banners_packed_alpha_{:02d}"
ICON = re.compile(r'(<Icon id=")(\d+)(" material_name=")([^"]+)(" texture_index=")(\d+)(" />)')
RED, GREEN = (255, 0, 0), (0, 255, 0)


def module(repo):
    return Path(repo) / "Main" / "_Module"


def read_icons(text):
    """[(id, material, texture_index)] in file order."""
    return [(m.group(2), m.group(4), int(m.group(6))) for m in ICON.finditer(text)]


def plan(counts):
    """{material: (packed sheet, first cell)} for every sheet using fewer than 16 cells.

    First fit decreasing over whole sheets, ties by name, so the plan does not depend on input order."""
    bins, out = [], {}
    for mat, n in sorted(((m, n) for m, n in counts.items() if n < CELLS), key=lambda x: (-x[1], x[0])):
        for i, used in enumerate(bins):
            if used + n <= CELLS:
                out[mat] = (PACKED.format(i + 1), used)
                bins[i] += n
                break
        else:
            out[mat] = (PACKED.format(len(bins) + 1), 0)
            bins.append(n)
    return out


def icon_moves(icons, sheet_plan):
    """{icon id: (packed sheet, new index, old index)}: a moved sheet's icons keep their order."""
    moves = {}
    for mat, (packed, start) in sheet_plan.items():
        own = sorted((t, i) for i, m, t in icons if m == mat)
        for k, (t, i) in enumerate(own):
            moves[i] = (packed, start + k, t)
    return moves


def rewrite_icons(text, moves):
    """The XML text with each moved <Icon> row repointed; every other byte unchanged."""
    seen = set()

    def sub(m):
        if m.group(2) not in moves:
            return m.group(0)
        seen.add(m.group(2))
        packed, idx, _ = moves[m.group(2)]
        return f"{m.group(1)}{m.group(2)}{m.group(3)}{packed}{m.group(5)}{idx}{m.group(7)}"

    out = ICON.sub(sub, text)
    missing = set(moves) - seen
    if missing:
        raise KeyError(f"no <Icon> row for {sorted(missing)}")
    ET.fromstring(out.encode("utf-8"))
    return out


def checker(i):
    r, c = divmod(i, GRID)
    return RED if (r + c) % 2 == 0 else GREEN


def assemble(cells, size=4096):
    """A sheet holding `cells` in order from cell 0, the rest the placeholder checkerboard."""
    c = size // GRID
    sheet = np.zeros((size, size, 3), dtype=np.uint8)
    for i in range(CELLS):
        r, k = divmod(i, GRID)
        if i < len(cells):
            sheet[r * c:(r + 1) * c, k * c:(k + 1) * c] = cells[i]
        else:
            sheet[r * c:(r + 1) * c, k * c:(k + 1) * c] = checker(i)
    return sheet


def is_art(cell):
    """False for a placeholder (red, green or black) cell: art is at least 32 pixels more than 16 levels
    off the cell's corner colour, so a placeholder's stray near-colour pixels (Rohan cell 11) do not count."""
    flat = cell.reshape(-1, 3).astype(np.int16)
    off = (np.abs(flat - flat[0]) > 16).any(axis=1)
    return int(off.sum()) >= 32


def load_cells(path):
    a = np.asarray(Image.open(path).convert("RGB"))
    if a.shape[:2] != (4096, 4096):
        raise ValueError(f"{path}: {a.shape[1]}x{a.shape[0]}, expected 4096x4096")
    c = 4096 // GRID
    return [a[r * c:(r + 1) * c, k * c:(k + 1) * c] for r in range(GRID) for k in range(GRID)]


def sources(mod):
    return {p.stem: p for p in (mod / "AssetSources" / "BannerIcons").glob("taom_banners_*.*")
            if p.suffix.lower() in (".psd", ".png")}


def audit(repo):
    """Findings for the gate, keyed by kind; partial_counts maps each partly used material to its icon count."""
    mod = module(repo)
    icons = read_icons((mod / "ModuleData" / "banner_icons.xml").read_bytes().decode("utf-8-sig"))
    srcs = sources(mod)
    rep = {k: [] for k in ("missing_sheet", "empty_cell", "shared_cell", "unnamed_art", "missing_sprite",
                           "missing_package", "unused_sheet")}
    by_mat = {}
    for i, m, t in icons:
        by_mat.setdefault(m, {}).setdefault(t, []).append(i)
        if not (mod / "GUI" / "SpriteParts" / "ui_taom_bannericons" / f"{i}.png").exists():
            rep["missing_sprite"].append(i)
    for mat, cells_used in sorted(by_mat.items()):
        for t, ids in cells_used.items():
            if len(ids) > 1:
                rep["shared_cell"].append((mat, t, ids))
        for kind in ("mtl", "tex"):
            if not (mod / "Assets" / "BannerIcons" / f"{mat}_{kind}.tpac").exists():
                rep["missing_package"].append(f"{mat}_{kind}.tpac")
        if mat not in srcs:
            rep["missing_sheet"].append(mat)
            continue
        cells = load_cells(srcs[mat])
        for t, ids in cells_used.items():
            if not 0 <= t < CELLS or not is_art(cells[t]):
                rep["empty_cell"].append((mat, t, ids))
        for t, cell in enumerate(cells):
            if t not in cells_used and is_art(cell):
                rep["unnamed_art"].append((mat, t))
    rep["unused_sheet"] = sorted(set(srcs) - set(by_mat))
    rep["partial_counts"] = {m: len(c) for m, c in by_mat.items() if len(c) < CELLS}
    return rep


def game_module():
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from _gamedir import game_dir
    return Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")) / "Modules" / "TAOM"


def apply(repo, write):
    mod = module(repo)
    xml_path = mod / "ModuleData" / "banner_icons.xml"
    text = xml_path.read_bytes().decode("utf-8")  # binary round trip keeps BOM and CRLF
    icons = read_icons(text)
    counts = {}
    for _, m, _ in icons:
        counts[m] = counts.get(m, 0) + 1
    sheet_plan = plan(counts)
    moves = icon_moves(icons, sheet_plan)
    packed = sorted({p for p, _ in sheet_plan.values()})
    for p in packed:
        members = sorted(((s, m) for m, (q, s) in sheet_plan.items() if q == p))
        print(f"{p}: " + ", ".join(f"{m} ({counts[m]}) at {s}" for s, m in members))
    print(f"{len(sheet_plan)} partly used sheets -> {len(packed)} packed; {len(moves)} icons move")
    if not write:
        print("dry run: nothing written (--apply to write)")
        return 0
    srcs = sources(mod)
    new_sheets = {}
    for p in packed:
        cells = []
        for s, m in sorted((s, m) for m, (q, s) in sheet_plan.items() if q == p):
            src_cells = load_cells(srcs[m])
            cells += [src_cells[t] for t in sorted(t for _, mm, t in icons if mm == m)]
        new_sheets[p] = assemble(cells)
    for i, (p, idx, old) in moves.items():  # verify every moved cell before anything is written
        mat = next(m for ii, m, _ in icons if ii == i)
        if not np.array_equal(new_sheets[p][(idx // GRID) * 1024:(idx // GRID + 1) * 1024,
                                            (idx % GRID) * 1024:(idx % GRID + 1) * 1024],
                              load_cells(srcs[mat])[old]):
            raise RuntimeError(f"icon {i}: packed cell differs from its source")
    out_text = rewrite_icons(text, moves)
    targets = [mod / "AssetSources" / "BannerIcons"]
    gm = game_module()
    if (gm / "AssetSources" / "BannerIcons").is_dir():
        targets.append(gm / "AssetSources" / "BannerIcons")
    for p, a in new_sheets.items():
        for d in targets:
            Image.fromarray(a, "RGB").save(d / f"{p}.png")
    xml_path.write_bytes(out_text.encode("utf-8"))
    print(f"wrote {len(new_sheets)} sheets to {', '.join(map(str, targets))} and {xml_path}")
    return 0


def prune(repo, backup_dir):
    mod = module(repo)
    icons = read_icons((mod / "ModuleData" / "banner_icons.xml").read_bytes().decode("utf-8-sig"))
    named = {m for _, m, _ in icons}
    missing = [f"{m}_{k}.tpac" for m in sorted(named) for k in ("mtl", "tex")
               if not (mod / "Assets" / "BannerIcons" / f"{m}_{k}.tpac").exists()]
    if missing:
        print("refusing: the Kit has not saved every named material into the repo: " + ", ".join(missing))
        return 1
    gm = game_module()
    roots = [mod] + ([gm] if gm.is_dir() else [])
    backup = Path(backup_dir)
    doomed = []
    for root in roots:
        for p in sorted((root / "AssetSources" / "BannerIcons").glob("taom_banners_*.*")):
            if p.stem not in named:
                doomed.append(p)
        for p in sorted((root / "Assets" / "BannerIcons").glob("taom_banners_*_???.tpac")):
            if p.stem[:-4] not in named:
                doomed.append(p)
                if root != mod and p.stem.endswith("_tex"):
                    rdc = root / "RuntimeDataCache" / f"{str(uuid.UUID(bytes_le=p.read_bytes()[8:24])).upper()}.rdc"
                    if rdc.exists():
                        doomed.append(rdc)
    for p in doomed:
        dest = backup / ("repo" if p.is_relative_to(mod) else "install") / p.relative_to(mod if p.is_relative_to(mod) else gm)
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(p, dest)
    for p in doomed:
        os.remove(p)
        print(f"deleted {p}")
    print(f"{len(doomed)} files deleted; backups in {backup}")
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--audit", action="store_true")
    mode.add_argument("--prune", action="store_true")
    ap.add_argument("--backup-dir")
    args = ap.parse_args(argv)
    if args.audit:
        rep = audit(REPO)
        bad = {k: v for k, v in rep.items() if k != "partial_counts" and v}
        partial = rep["partial_counts"]
        if len(partial) > -(-sum(partial.values()) // CELLS):
            bad["mergeable"] = partial
        for k, v in bad.items():
            print(f"{k}: {v}")
        print("clean" if not bad else f"{len(bad)} finding kinds")
        return 1 if bad else 0
    if args.prune:
        if not args.backup_dir:
            ap.error("--prune needs --backup-dir")
        return prune(REPO, args.backup_dir)
    return apply(REPO, args.apply)


if __name__ == "__main__":
    sys.exit(main())
