#!/usr/bin/env python3
"""Build the dedicated server's creature skeleton package from the live Armory's art.

WHY THIS EXISTS. The co-op dedicated server stages the Armory's ModuleData but not its art, so
it lacks the creature skeletons the action sets name (chariot, dwarf, elephant, warg, spider,
troll). The engine then falls back to an arbitrary skeleton and every Monsters bone lookup
asserts with a message box: 150 asserts and an 8-minute stall on a server boot. The server
engine loads `<module>/DsAssetPackages/*.tpac` for every loaded module (Rgl.dll
get_asset_path_for_module) and the game client never reads that folder, so a skeleton-only
package there fixes the server and cannot touch the client. A probe package built the same way
removed all 150 asserts (Monsters load 479,705 ms -> 13.73 ms).

WHAT IT DOES.
  1. Collects every skeleton name the Armory's ModuleData declares: `skeleton=`,
     `owner_skeleton=` and `rein_skeleton=` in action_sets.xml, monsters.xml and Monsters/**.
  2. Drops the names the server's own skeleton package already holds (--server-pack; the
     dedicated server's Native skeletons.tpac adds human_skeleton and sling_skeleton).
  3. Finds each remaining name as a Skeleton item in the packages the CLIENT loads for the
     Armory: the loose `Assets/**/*.tpac` tree when the module has one, else `AssetPackages/`
     (validate_mesh_refs.tpac_paths_for_modules; docs/reference/armory-guide.md "Two asset
     trees"). A name held by two items, or by none, is an error: the tool never picks silently.
  4. Copies each item's TOC entry and data unchanged, sorted by name, into a version-2 package
     whose header holds the REAL TOC size (bytes 28-35). tpac_skeleton_extract.py wrote 0 there
     and the engine reads exactly that many TOC bytes.

NOT THE DEPRECATED EXTRACT. tpac_skeleton_extract.py's standalone skeleton package crashed the
client on 2026-06-14 (docs/tools/spider-skeleton-tpac-tools.md): it reused an item GUID as the
package GUID, wrote a TOC size of 0, and loaded beside the bundled copy of the same skeleton.
This package carries the cooked-package GUID and the real TOC size, and it lives where only the
server looks, which has no other copy.

Dry run by default: prints the plan. `--apply` writes the package. `--check` rebuilds in memory
and compares with the committed file: exit 0 equal, 1 differs (the Armory art drifted, or the
file is absent), 2 inputs unreadable. Exit 2 also covers an ambiguous or missing skeleton.

Usage:
  python tools/build_ds_skeleton_pack.py [--apply | --check]
      [--game-dir <Bannerlord install>] [--server-pack <skeletons.tpac>] [--out <tpac>]
"""
from __future__ import annotations

import argparse
import struct
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import tpac_clone_metamesh as tcm  # noqa: E402
import validate_mesh_refs as vmr  # noqa: E402
from _gamedir import game_dir  # noqa: E402

SKELETON_TYPE_GUID = bytes.fromhex("d5a335c6bbeadd45883eaa57e4196113")
# The header GUID every cooked package carries: the server's own Native skeletons.tpac, the
# client's core.tpac and the Armory's packs all hold it, and the server loads several of them at
# once. Only the Modding Kit's loose per-asset packages differ. Constant, so a rebuild is
# byte-stable.
PACKAGE_GUID = bytes.fromhex("8d3d7e8001e59b49a1d322ae3f4e64f3")
ARMORY = "LOTRLOME_Armory"
NAME_ATTRIBUTES = ("skeleton", "owner_skeleton", "rein_skeleton")
DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
DEFAULT_SERVER_PACK = (r"E:\Steam\steamapps\common\Mount & Blade II Dedicated Server"
                       r"\Modules\Native\DsAssetPackages\skeletons.tpac")
DEFAULT_OUT = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "DsAssetPackages" / "taom_creature_skeletons.tpac"

EXIT_OK, EXIT_DIFFERS, EXIT_INPUT = 0, 1, 2


class PackError(Exception):
    """An input is unreadable, ambiguous or missing: nothing is written, exit 2."""


@dataclass
class Source:
    name: str
    path: Path
    item: tcm.Item

    @property
    def data_bytes(self) -> int:
        return sum(s.storage for s in self.item.segments)


read_package = tcm.parse


def collect_skeleton_names(moduledata: Path) -> set:
    """Every skeleton name the Armory's action sets and monsters declare."""
    moduledata = Path(moduledata)
    files = [moduledata / "action_sets.xml"]
    files.append(moduledata / "monsters.xml")
    if (moduledata / "Monsters").is_dir():
        files.extend(sorted((moduledata / "Monsters").rglob("*.xml")))
    if not files[0].is_file():
        raise PackError(f"action sets not found: {files[0]}")
    names = set()
    for f in files:
        if not f.is_file():
            continue
        try:
            root = ET.parse(f).getroot()
        except (ET.ParseError, OSError) as e:
            raise PackError(f"cannot read {f}: {e}") from e
        for el in root.iter():
            for attr in NAME_ATTRIBUTES:
                value = el.get(attr)
                if value:
                    names.add(value)
    return names


def scan_skeleton_items(path: Path) -> list:
    """The Skeleton items of one tpac, read from its TOC alone (blobs stay on disk)."""
    path = Path(path)
    try:
        with open(path, "rb") as f:
            header = f.read(tcm.HEADER_SIZE)
            if len(header) < tcm.HEADER_SIZE or header[:4] != tcm.MAGIC:
                raise PackError(f"not a tpac: {path}")
            version = struct.unpack_from("<I", header, 4)[0]
            if version != 2:
                raise PackError(f"tpac version {version}, expected 2: {path}")
            count = struct.unpack_from("<I", header, 24)[0]
            toc_size = struct.unpack_from("<Q", header, 28)[0]
            toc = f.read(toc_size)
        if len(toc) != toc_size:
            raise PackError(f"TOC truncated: {path}")
        data = header + toc
        pos = tcm.HEADER_SIZE
        out = []
        for _ in range(count):
            item, pos = tcm._read_item(data, pos, version)
            if item.type_guid == SKELETON_TYPE_GUID:
                out.append(item)
        return out
    except (OSError, struct.error, tcm.CloneError, IndexError) as e:
        raise PackError(f"cannot read {path}: {e}") from e


def userdata_count(item: tcm.Item) -> int:
    pos = item.segments[-1].entry_pos + tcm.SEG_ENTRY_SIZE if item.segments else None
    if pos is None:
        return struct.unpack_from("<i", item.toc, len(item.toc) - 4)[0]
    return struct.unpack_from("<i", item.toc, pos)[0]


def find_sources(wanted: set, packages: list) -> tuple:
    """(sources sorted by name, [unreadable package paths]). Ambiguous or missing names raise."""
    found = {n: [] for n in wanted}
    unreadable = []
    for path in packages:
        try:
            items = scan_skeleton_items(path)
        except PackError as e:
            unreadable.append((path, str(e)))
            continue
        for item in items:
            if item.name in found:
                found[item.name].append(Source(item.name, path, item))
    problems = []
    for name in sorted(found):
        hits = found[name]
        if len(hits) > 1:
            where = "; ".join(str(h.path) for h in hits)
            problems.append(f"skeleton {name!r} is held by {len(hits)} items: {where}")
        elif not hits:
            problems.append(f"skeleton {name!r} is declared but no package holds it")
    if problems:
        if unreadable:
            problems.append("unreadable packages (any of them might hold a missing name): "
                            + "; ".join(str(p) for p, _ in unreadable))
        raise PackError("\n".join(problems))
    return [found[n][0] for n in sorted(found)], unreadable


def load_blobs(src: Source) -> tcm.Item:
    item = src.item
    if userdata_count(item) != 0:
        raise PackError(f"skeleton {src.name!r} in {src.path} carries user-data dependencies; "
                        "a skeleton-only package cannot hold them")
    if tcm.expected_checksum(item) != item.checksum:
        raise PackError(f"skeleton {src.name!r} in {src.path} fails its item checksum "
                        "(tools/tpac_fix_item_checksums.py); refusing to copy it")
    blobs = []
    try:
        with open(src.path, "rb") as f:
            for seg in item.segments:
                f.seek(seg.offset)
                blob = f.read(seg.storage)
                if len(blob) != seg.storage:
                    raise PackError(f"segment of {src.name!r} is truncated in {src.path}")
                blobs.append(blob)
    except OSError as e:
        raise PackError(f"cannot read {src.path}: {e}") from e
    item.blobs = blobs
    return item


def build_package(sources: list) -> bytes:
    items = [load_blobs(s) for s in sorted(sources, key=lambda s: s.name)]
    return tcm.serialize(PACKAGE_GUID, items)


def server_names(server_pack: Path) -> set:
    if not Path(server_pack).is_file():
        raise PackError(f"server skeleton package not found: {server_pack}")
    return {i.name for i in scan_skeleton_items(server_pack)}


def plan(game: Path, server_pack: Path) -> tuple:
    armory_data = Path(game) / "Modules" / ARMORY / "ModuleData"
    declared = collect_skeleton_names(armory_data)
    have = server_names(server_pack)
    needed = declared - have
    packages = vmr.tpac_paths_for_modules(Path(game), [ARMORY])
    sources, unreadable = find_sources(needed, packages) if needed else ([], [])
    return declared, needed, sources, unreadable


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--game-dir", default=game_dir(DEFAULT_GAME), help="Bannerlord install root")
    ap.add_argument("--server-pack", default=DEFAULT_SERVER_PACK,
                    help="the dedicated server's Native skeletons.tpac; names it holds are skipped")
    ap.add_argument("--out", default=str(DEFAULT_OUT), help="the package to write or compare")
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true", help="write the package")
    mode.add_argument("--check", action="store_true", help="exit 1 when the committed file differs")
    args = ap.parse_args(argv)
    out = Path(args.out)
    try:
        declared, needed, sources, unreadable = plan(Path(args.game_dir), Path(args.server_pack))
        data = build_package(sources) if sources else None
    except PackError as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return EXIT_INPUT
    for path, why in unreadable:
        print(f"WARNING: skipped unreadable package: {why}", file=sys.stderr)
    if args.check and unreadable:
        # An unreadable package might hold a second copy of a skeleton, so "up to date" is unproven.
        print("ERROR: cannot prove the package is up to date while a package is unreadable",
              file=sys.stderr)
        return EXIT_INPUT

    if data is None:
        print(f"nothing to build: the server already holds all {len(declared)} declared skeletons")
        if args.check:
            return EXIT_DIFFERS if out.exists() else EXIT_OK
        return EXIT_OK

    if args.check:
        if not out.is_file():
            print(f"{out} is absent; run with --apply", file=sys.stderr)
            return EXIT_DIFFERS
        if out.read_bytes() != data:
            print(f"{out} differs from what the live Armory produces "
                  f"({out.stat().st_size} vs {len(data)} bytes); run with --apply", file=sys.stderr)
            return EXIT_DIFFERS
        print(f"{out} is up to date ({len(sources)} skeletons, {len(data)} bytes)")
        return EXIT_OK

    print(f"{len(sources)} skeletons to package ({len(declared)} declared, {len(declared) - len(needed)} "
          "already on the server):")
    for s in sources:
        print(f"  {s.name:<28}{s.data_bytes:>10} bytes  {s.path}")
    if not args.apply:
        print(f"dry run: {len(data)} bytes would be written to {out}; pass --apply")
        return EXIT_OK
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(data)
    print(f"wrote {out} ({len(data)} bytes)")
    return EXIT_OK


if __name__ == "__main__":
    sys.exit(main())
