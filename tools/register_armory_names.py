#!/usr/bin/env python
"""Register the Armory's inline `{=KEY}English` item and crafting-piece names.

Artist drops into the unversioned live LOTRLOME_Armory ship names as `name="{=KEY}English"` with
no `<string id=KEY>` row in the Armory's `Languages/*.xml`, so the translator never sees them.
This tool finds those keys and (on request) appends the English rows.

Scanned: `name=` of every Item / CraftedItem / CraftingPiece under
`ModuleData/LOTRLOME_items/**/*.xml` and in `ModuleData/LOTRLOME_crafting_pieces.xml`.
Registered: any `<string id>` in a TOP-LEVEL `ModuleData/Languages/*.xml` (the same semantics as
HaradWeaponsLiveEditTests.RegisteredKeys; the per-language subfolders do not count).

  python tools/register_armory_names.py --check            # read-only gate, exit 1 on drift
  python tools/register_armory_names.py                    # dry run: what --apply would add
  python tools/register_armory_names.py --apply            # append the rows (backs up each file)
  python tools/register_armory_names.py --apply --prefix "^sm_hr_st_"   # only some ids or keys
  python tools/register_armory_names.py --apply --include-baselined   # also the baselined backlog
  python tools/register_armory_names.py --update-baseline  # rewrite the baseline from the install

--apply and the dry run cover only keys NOT in the baseline (exactly what --check flags);
--include-baselined adds the backlog. Each new English row raises check_external_loc_coverage.py's
untranslated count by one per language, so run its --update-baseline in the same change.
--prefix is a regex matched against the key OR the element id (no match: exit 1; bad regex: exit 2).
Names are read with ElementTree, so a commented-out item is ignored and a file that does not parse
exits 2. Out of scope: generate_starter_kit.py's names (`starter_kit.xml` item files and the
TAOM-STARTER-KIT block of the pieces file); they are read from that generator.

--check fails (exit 1) on an unregistered key missing from `tools/armory-unregistered-names-baseline.txt`
and also on a baseline entry that is no longer unregistered, so the list only shrinks. Exit 2: the
Armory is missing or yielded no names (a gate that checked nothing).

Routing: crafting pieces go to `Languages/loc_LOTRLOME_crafting_pieces.xml`, items in
`LOTRLOME_items/<folder>/` to `Languages/loc_<folder>.xml`, a loose
`LOTRLOME_items/<name>.xml` to `Languages/loc_<name>.xml`. A target that does not exist is
reported and its keys skipped; no file is created. Each written file keeps its BOM and its majority
line ending, gets a backup `<file>.bak-armory-names-<timestamp>` (never an .xml suffix: the engine
globs the folder) and is parsed before it is written. A second --apply adds nothing.

Game dir: BANNERLORD_GAME_DIR via tools/_gamedir.py; `--modules <Modules folder>` overrides it.
"""
import argparse
import datetime
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _gamedir import game_modules  # noqa: E402
import generate_starter_kit  # noqa: E402

DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
BASELINE = Path(__file__).resolve().parent / "armory-unregistered-names-baseline.txt"
PIECES_FILE = "LOTRLOME_crafting_pieces.xml"
PIECES_TARGET = "loc_LOTRLOME_crafting_pieces.xml"
NAMED_TAGS = ("Item", "CraftedItem", "CraftingPiece")
# generate_starter_kit.py owns these names (item twins in starter_kit.xml per culture folder, blade
# pieces inside a marker block): they are out of scope here. Read from the generator, never filtered
# on the `starter_` prefix (generate_starter_armor.py's 78 registered keys share it).
STARTER_ITEMS_FILE = generate_starter_kit.ITEMS_FILE_NAME
NAME_RE = re.compile(r"^\{=([^}]+)\}(.*)$", re.S)
STRING_ID_RE = re.compile(r"""<string\s+id\s*=\s*["']([^"']+)["']""")
BOM = b"\xef\xbb\xbf"


def read_text(path):
    return Path(path).read_bytes().decode("utf-8-sig")


class SourceParseError(Exception):
    pass


def names_in(path, strip_starter_block=False):
    """[(key, english, element id)] for every inline `{=KEY}text` name in one file, in file order.
    Raises SourceParseError when the file is not well-formed XML."""
    text = read_text(path)
    if strip_starter_block:
        text = generate_starter_kit.block_re().sub("", text)
    try:
        root = ET.fromstring(text.encode("utf-8"))
    except ET.ParseError as exc:
        raise SourceParseError("%s: %s" % (path, exc))
    out = []
    for el in root.iter():
        if el.tag not in NAMED_TAGS:
            continue
        n = NAME_RE.match(el.get("name") or "")
        if n:
            out.append((n.group(1), n.group(2), el.get("id") or ""))
    return out


def collect(md):
    """{key: (english, target language file name, {element ids})}; the first definition of a
    key wins its text and target."""
    found = {}

    def add(path, target, strip=False):
        for key, text, eid in names_in(path, strip):
            found.setdefault(key, (text, target, set()))[2].add(eid)

    items = md / "LOTRLOME_items"
    for f in sorted(items.rglob("*.xml")):
        if f.name == STARTER_ITEMS_FILE:
            continue
        rel = f.relative_to(items)
        add(f, "loc_%s.xml" % (rel.parts[0] if len(rel.parts) > 1 else f.stem))
    pieces = md / PIECES_FILE
    if pieces.is_file():
        add(pieces, PIECES_TARGET, strip=True)
    return found


def registered_keys(md):
    keys = set()
    for f in (md / "Languages").glob("*.xml"):
        keys |= set(STRING_ID_RE.findall(read_text(f)))
    return keys


def read_baseline(path):
    if not Path(path).is_file():
        return set()
    lines = (l.strip() for l in Path(path).read_text(encoding="utf-8").splitlines())
    return {l for l in lines if l and not l.startswith("#")}


def esc(text):
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")


def build_new_bytes(raw, rows, stamp):
    """`raw` with a comment and one row per (key, text) inserted before </strings>; None if
    the file does not have exactly one </strings> line."""
    had_bom = raw.startswith(BOM)
    text = raw.decode("utf-8-sig")
    crlf = text.count("\r\n")
    nl = "\r\n" if crlf > text.count("\n") - crlf else "\n"
    closers = list(re.finditer(r"(?m)^[ \t]*</strings>", text))
    if len(closers) != 1:
        return None
    block = ["    <!-- register_armory_names.py %s: English rows for names that shipped inline without one -->" % stamp]
    block += ['    <string id="%s" text="%s"/>' % (k, esc(t)) for k, t in rows]
    new = text[:closers[0].start()] + nl.join(block) + nl + text[closers[0].start():]
    return (BOM if had_bom else b"") + new.encode("utf-8")


def resolve_md(args):
    modules = Path(args.modules) if args.modules else game_modules(DEFAULT_GAME)
    md = modules / "LOTRLOME_Armory" / "ModuleData"
    if not (md / "LOTRLOME_items").is_dir() or not (md / "Languages").is_dir():
        print("ERROR: no LOTRLOME_Armory ModuleData (LOTRLOME_items and Languages) at %s; nothing was checked" % md,
              file=sys.stderr)
        raise SystemExit(2)
    return md


def do_check(unreg, baseline_path):
    baseline = read_baseline(baseline_path)
    new = sorted(unreg - baseline)
    stale = sorted(baseline - unreg)
    if new:
        print("FAIL: %d name key(s) have no row in Languages/*.xml and are not in the baseline:" % len(new))
        for k in new:
            print("  " + k)
        print("Fix: python tools/register_armory_names.py --apply (add --prefix to limit it).")
    if stale:
        print("FAIL: %d baseline entr%s no longer unregistered; remove from %s:"
              % (len(stale), "y is" if len(stale) == 1 else "ies are", Path(baseline_path).name))
        for k in stale:
            print("  " + k)
    if new or stale:
        return 1
    print("OK: %d unregistered name key(s), all baselined." % len(unreg))
    return 0


def do_apply(md, found, unreg, args):
    stamp = datetime.datetime.now()
    plan, skipped = {}, []
    for key in sorted(unreg):
        text, target, _ = found[key]
        if not (md / "Languages" / target).is_file():
            skipped.append((key, target))
        else:
            plan.setdefault(target, []).append((key, text))
    status = 0
    for target, rows in sorted(plan.items()):
        print("%s: %d row(s)" % (target, len(rows)))
        for k, t in rows:
            print("    %s = %s" % (k, t))
    for key, target in skipped:
        print("SKIPPED %s: target %s does not exist (not creating it)" % (key, target))
    if not args.apply:
        print("dry run; pass --apply to write")
        return 0
    written = 0
    for target, rows in sorted(plan.items()):
        path = md / "Languages" / target
        raw = path.read_bytes()
        new = build_new_bytes(raw, rows, stamp.strftime("%Y-%m-%d"))
        try:
            if new is None:
                raise ValueError("expected exactly one </strings> line")
            ET.fromstring(new)
        except (ValueError, ET.ParseError) as exc:
            print("REFUSED %s: %s; file untouched" % (target, exc))
            status = 1
            continue
        bak = path.with_name(path.name + ".bak-armory-names-" + stamp.strftime("%Y%m%d-%H%M%S"))
        if not bak.exists():
            bak.write_bytes(raw)
        path.write_bytes(new)
        written += len(rows)
        print("wrote %s (backup %s)" % (target, bak.name))
    if written:
        print("check_external_loc_coverage.py now counts these %d row(s) as untranslated in each language "
              "until the translation run; confirm that delta, then run it with --update-baseline in the "
              "same change." % written)
    return status


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--modules", help="a Modules folder (default: the dev install's)")
    ap.add_argument("--baseline", default=str(BASELINE), help=argparse.SUPPRESS)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="read-only gate against the baseline")
    mode.add_argument("--apply", action="store_true", help="append the English rows")
    mode.add_argument("--update-baseline", action="store_true", help="write the unregistered set to the baseline")
    ap.add_argument("--prefix", help="regex: only names whose key or element id matches it (dry run and --apply)")
    ap.add_argument("--include-baselined", action="store_true",
                    help="also write the baselined backlog (default: only keys --check would flag)")
    args = ap.parse_args(argv)

    md = resolve_md(args)
    try:
        found = collect(md)
    except SourceParseError as exc:
        print("ERROR: cannot parse %s; nothing was checked" % exc, file=sys.stderr)
        return 2
    if not found:
        print("ERROR: the Armory yielded no {=KEY} names; nothing was checked", file=sys.stderr)
        return 2
    unreg = set(found) - registered_keys(md)

    if args.check:
        return do_check(unreg, args.baseline)
    if args.update_baseline:
        Path(args.baseline).write_text("".join(k + "\n" for k in sorted(unreg)), encoding="utf-8", newline="\n")
        print("wrote %d key(s) to %s" % (len(unreg), args.baseline))
        return 0
    if args.prefix:
        try:
            rx = re.compile(args.prefix)
        except re.error as exc:
            print("ERROR: --prefix is not a valid regex: %s" % exc, file=sys.stderr)
            return 2
        if not any(rx.search(k) or any(rx.search(i) for i in found[k][2]) for k in found):
            print("ERROR: --prefix %r matched no key or element id in the Armory" % args.prefix)
            return 1
        unreg = {k for k in unreg if rx.search(k) or any(rx.search(i) for i in found[k][2])}
    if not args.include_baselined:
        unreg -= read_baseline(args.baseline)
    return do_apply(md, found, unreg, args)


if __name__ == "__main__":
    sys.exit(main())
