#!/usr/bin/env python3
"""Bind the restyled orc and goblin walk, run and idle clips into as_orc_warrior and as_goblin_warrior in the LIVE
LOTRLOME_Armory action_sets.xml (#791, docs/features/orc-goblin-locomotion.md).

Both sets have base_set="as_human_warrior", so every act_* code they do not override plays the human clip. This
tool writes the override body of each set. It reads Native's as_human_warrior with ElementTree (a commented-out row
is never read) and binds every act_walk_* and act_run_* code there whose vanilla clip has a restyled package in the
set's folder, <Assets>/Race Test/Mordor/<Orc|Goblin>/animations/<prefix><clip>_anm.tpac, the name
tools/gen_troll_anim_clips.ps1 -CloneByName gives the restyled clip. So one restyled clip serves every code that
plays its vanilla clip: walk_forward_1h also serves the thrown and dagger walks, stand_1h_with_shield the running
idles and the hand-shield idles. 42 codes per set on 2026-10-14, from 16 clips. A clip with no package on disk binds
nothing: for a phantom animation= the engine logs "Could not find animation" and keeps the inherited human clip, so
--apply before the Kit import and the clip generation writes nothing.

Why not the Armory's action_sets.xslt: MBObjectManager.CreateMergedXmlFile applies a module's XSLT BEFORE it merges
that module's own XML (v1.5.5, lines 962 to 978), so that stylesheet sees only Native's sets. A module loading after
the Armory would see these two, but TAOM declares no load order against the Armory and registers no action-set file,
so the rows live beside the clips and the race sets, as bind_troll_action_set.py's do.

Derived sets (as_orc_hideout_bandit, as_orc_villager and the rest) copy as_orc_warrior through base_set when the
engine parses them, so they inherit these rows; --check proves every link of each chain is parsed after its parent
and lists the codes a derived set overrides itself (the villager sets keep stand_2 for act_walk_idle_unarmed).

Byte-faithful (tools/README.md XML I/O convention): binary read, BOM and line endings kept, backup to
action_sets.xml.bak-orc-goblin-bind-<stamp> (never a .xml extension: the folder is globbed), the result parsed
with ElementTree before it is written, the body between each set's open and close tags REPLACED, so a re-run is
idempotent. Dry run by default. --apply refuses (exit 2) while the game or the Kit runs
(_gamedir.game_or_kit_running), and when it would drop a row the live sets carry (a clip package gone missing)
unless --allow-shrink. --check exits 1 when the live sets differ from the plan, and also when a set has no restyled
clip on disk at all, which is what a reinstall leaves (no clips and no rows), unless --allow-no-clips. Bad input (a
missing file, a missing or self-closing set when planning or applying, XML that does not parse or is not UTF-8, a
Native row with no type) exits 2; under --check a missing or self-closing set is a PROBLEM (exit 1).

    python tools/bind_orc_goblin_locomotion.py            # plan
    python tools/bind_orc_goblin_locomotion.py --apply
    python tools/bind_orc_goblin_locomotion.py --check
After --apply run  python tools/audit_action_set_parity.py  (a root-level <action> kills a dedicated server).
"""
import argparse
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir, game_or_kit_running  # noqa: E402

GAME = os.path.join(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"), "Modules")
LIVE = os.path.join(GAME, "LOTRLOME_Armory", "ModuleData", "action_sets.xml")
NATIVE = os.path.join(GAME, "Native", "ModuleData", "action_sets.xml")
ASSETS = os.path.join(GAME, "LOTRLOME_Armory", "Assets")

# set id -> (clip prefix, the clips' folder under Assets); the prefix is the variant spec's "clip_prefix"
# (tools/blender/locomotion_variants/<variant>.json), the folders are where Mike keeps each race's art
SETS = {"as_orc_warrior": ("taom_orc_", os.path.join("Race Test", "Mordor", "Orc", "animations")),
        "as_goblin_warrior": ("taom_goblin_", os.path.join("Race Test", "Mordor", "Goblin", "animations"))}
CODE_RE = re.compile(r"act_(walk|run)_")   # the locomotion codes; a stand clip also plays inventory and other idles
PACKAGE_TAIL = "_anm.tpac"
COMMENT_RE = re.compile(r"<!--.*?-->", re.S)


class InputError(Exception):
    """Input the tool cannot work from: main() prints it and exits 2."""


def human_rows(native_text):
    """[(code, clip)] of Native's as_human_warrior in document order."""
    root = ET.fromstring(native_text.encode("utf-8"))
    human = next((s for s in root.iter("action_set") if s.get("id") == "as_human_warrior"), None)
    if human is None:
        raise InputError("as_human_warrior not found in Native's action_sets.xml")
    rows = [(a.get("type"), a.get("animation")) for a in human.findall("action")]
    if any(code is None for code, _ in rows):
        raise InputError("an <action> in Native's as_human_warrior has no type")
    return rows


def packages(folder, prefix):
    """The vanilla clip names whose restyled package <prefix><clip>_anm.tpac is in folder."""
    if not os.path.isdir(folder):
        return set()
    return {f[len(prefix):-len(PACKAGE_TAIL)] for f in os.listdir(folder)
            if f.startswith(prefix) and f.endswith(PACKAGE_TAIL)}


def plan(human, assets):
    """set id -> ([(code, clip)] in Native order, [packages no code plays])."""
    out = {}
    for set_id, (prefix, sub) in SETS.items():
        have = packages(os.path.join(assets, sub), prefix)
        hits = [(code, clip) for code, clip in human if CODE_RE.match(code) and clip in have]
        out[set_id] = ([(code, prefix + clip) for code, clip in hits],
                       sorted(prefix + p for p in have - {clip for _, clip in hits}))
    return out


def find_set(text, set_id):
    """(start, end) of set_id's body, between its open and close tags; a commented-out copy is skipped."""
    plain = COMMENT_RE.sub(lambda m: " " * len(m.group(0)), text)   # same length, so the offsets hold in text
    m = re.search(r'<action_set\s+id="%s"[^>]*>' % re.escape(set_id), plain)
    if not m:
        raise InputError("%s not found in the live action_sets.xml" % set_id)
    if m.group(0).endswith("/>"):
        raise InputError("%s is self-closing; this tool needs its open and close tags" % set_id)
    return m.end(), plain.index("</action_set>", m.end())


def build_body(rows, nl):
    if not rows:
        return nl + "\t"
    head = ("%s\t\t<!-- Restyled orc and goblin locomotion (#791); GENERATED by tools/bind_orc_goblin_locomotion.py,"
            "%s\t\t     which owns this body: edit the tool, not these rows. Every other code inherits as_human_warrior. -->"
            % (nl, nl))
    return head + nl + nl.join('\t\t<action type="%s" animation="%s" />' % r for r in rows) + nl + "\t"


def check(text, planned, allow_no_clips=False):
    """Compare the live sets with the plan and prove each base_set chain. Returns the number of problems."""
    root = ET.fromstring(text.encode("utf-8"))
    sets = list(root.iter("action_set"))
    pos = {s.get("id"): i for i, s in enumerate(sets)}
    by_id = {s.get("id"): s for s in sets}
    problems = len(root.findall("action"))
    if problems:
        print("PROBLEM root-level <action> elements: %d (run tools/audit_action_set_parity.py)" % problems)
    bad_links = set()
    for set_id, (rows, unbound) in planned.items():
        if set_id not in by_id:
            problems += 1
            print("PROBLEM %s is not in the file" % set_id)
            continue
        if not rows and not allow_no_clips:
            problems += 1
            print("PROBLEM %s: no restyled clip on disk (%s); a reinstall removes the clips and the rows together"
                  % (set_id, SETS[set_id][1]))
        live = [(a.get("type"), a.get("animation")) for a in by_id[set_id].findall("action")]
        if live != rows:
            problems += 1
            print("PROBLEM %s: live rows %d, planned %d" % (set_id, len(live), len(rows)))
            for r in sorted(set(rows) - set(live)):
                print("    missing %s -> %s" % r)
            for r in sorted(set(live) - set(rows)):
                print("    extra   %s -> %s" % r)
        else:
            print("ok %s: %d rows as planned" % (set_id, len(rows)))
        for clip in unbound:
            print("    note: no code plays %s" % clip)
        # base_set copies at parse time, so each set in a chain that reaches set_id must come after its own parent
        codes = {c for c, _ in rows}
        groups, derived = {}, 0
        for sid, s in by_id.items():
            links, child, cur, seen = [], sid, s.get("base_set"), set()
            while cur in by_id and cur != set_id and cur not in seen:
                seen.add(cur)
                links.append((child, cur))
                child, cur = cur, by_id[cur].get("base_set")
            if cur != set_id or sid == set_id:
                continue
            links.append((child, set_id))
            derived += 1
            for c, p in links:
                if pos[c] < pos[p] and (c, p) not in bad_links:
                    bad_links.add((c, p))
                    problems += 1
                    print("PROBLEM %s inherits %s but is defined before it" % (c, p))
            own = set()
            for mid in [sid] + [p for _, p in links[:-1]]:   # an intermediate set can override a code too
                own |= {c for c in (a.get("type") for a in by_id[mid].findall("action")) if c in codes}
            groups.setdefault(tuple(sorted(own)), []).append(sid)
        print("   %d derived sets inherit it" % derived)
        for own, ids in sorted(groups.items(), key=lambda kv: -len(kv[1])):
            what = ("keep every bound code" if not own else "override " + ", ".join(own))
            print("   %3d %s (e.g. %s)" % (len(ids), what, ", ".join(ids[:3])))
    return problems


def run(args):
    planned = plan(human_rows(open(args.native, "rb").read().decode("utf-8-sig")), args.assets)
    raw = open(args.live, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    crlf = text.count("\r\n")
    nl = "\r\n" if crlf > text.count("\n") - crlf else "\n"   # the MAJORITY ending (tools/README.md XML I/O)

    if args.check:
        n = check(text, planned, args.allow_no_clips)
        print("check: %d problem(s)" % n)
        return 1 if n else 0

    live_sets = {s.get("id"): s for s in ET.fromstring(text.encode("utf-8")).iter("action_set")}
    new_text, drops = text, 0
    for set_id, (rows, unbound) in planned.items():
        print("%s: %d codes bound" % (set_id, len(rows)))
        for clip in unbound:
            print("    note: no code plays %s" % clip)
        a, b = find_set(new_text, set_id)
        kept = {code for code, _ in rows}
        for code in sorted({x.get("type") for x in live_sets[set_id].findall("action")} - kept):
            print("    DROPS %s (bound now; the plan no longer binds it)" % code)
            drops += 1
        new_text = new_text[:a] + build_body(rows, nl) + new_text[b:]
    if new_text == text:
        print("no change: the sets already carry exactly this body")
        return 0
    ET.fromstring(new_text.encode("utf-8"))   # must still parse
    if not args.apply:
        print("DRY RUN: nothing written.")
        return 0
    if drops and not args.allow_shrink:
        print("REFUSED: %d bound row(s) would go; restore their clip packages, or pass --allow-shrink" % drops)
        return 2
    if game_or_kit_running():   # the shared guard; it also refuses when the process list cannot be read
        print("REFUSED: close the game and the Modding Kit first")
        return 2
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    bak = args.live + ".bak-orc-goblin-bind-" + stamp
    open(bak, "wb").write(raw)
    open(args.live, "wb").write((b"\xef\xbb\xbf" if bom else b"") + new_text.encode("utf-8"))
    after = open(args.live, "rb").read().decode("utf-8-sig")
    n = check(after, planned, args.allow_no_clips)
    print("written; backup %s; check: %d problem(s)" % (os.path.basename(bak), n))
    return 1 if n else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--allow-no-clips", action="store_true",
                    help="--check passes a set with no restyled clip on disk (an install before the Kit import)")
    ap.add_argument("--allow-shrink", action="store_true",
                    help="--apply may drop rows the live sets carry (a clip removed on purpose)")
    ap.add_argument("--live", default=LIVE)
    ap.add_argument("--native", default=NATIVE)
    ap.add_argument("--assets", default=ASSETS, help="the Armory's Assets folder; each set's clips sit in its SETS folder")
    args = ap.parse_args(argv)
    try:
        return run(args)
    except (InputError, OSError, ET.ParseError, UnicodeDecodeError) as e:
        print("ERROR: %s" % e)
        return 2


if __name__ == "__main__":
    sys.exit(main())
