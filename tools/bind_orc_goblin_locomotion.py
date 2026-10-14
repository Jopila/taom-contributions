#!/usr/bin/env python3
"""Bind the restyled orc and goblin walk, run and idle clips into as_orc_warrior and as_goblin_warrior in the LIVE
LOTRLOME_Armory action_sets.xml (#791, docs/features/orc-goblin-locomotion.md).

Both sets have base_set="as_human_warrior", so every act_* code they do not override plays the human clip. This
tool writes the override body of each set: for every code in CODES it reads the vanilla clip that Native's
as_human_warrior binds (walk_forward_1h_left_stance and the other left-stance codes have their own vanilla clips;
the unarmed walk and run share one) and binds <prefix><that clip>, the name tools/gen_troll_anim_clips.ps1
-CloneByName gives the restyled clip. A code binds ONLY when its clip package
<Assets>/Race Test/Mordor/<Orc|Goblin>/animations/<clip>_anm.tpac exists: for a phantom animation= the engine logs
"Could not find animation" and keeps the inherited human clip. So --apply before the Kit import and the clip
generation writes nothing.

Why not the Armory's action_sets.xslt: MBObjectManager.CreateMergedXmlFile applies a module's XSLT BEFORE it merges
that module's own XML (v1.5.5, lines 962 to 978), so that stylesheet sees only Native's sets. A module loading after
the Armory would see these two, but TAOM declares no load order against the Armory and registers no action-set file,
so the rows live beside the clips and the race sets, as bind_troll_action_set.py's do.

Derived sets (as_orc_hideout_bandit, as_orc_villager and the rest) copy as_orc_warrior through base_set when the
engine parses them, so they inherit these rows; --check proves the chain and lists the codes a derived set
overrides itself (the villager sets keep stand_2 for act_walk_idle_unarmed).

Byte-faithful (tools/README.md XML I/O convention): binary read, BOM and line endings kept, backup to
action_sets.xml.bak-orc-goblin-bind-<stamp> (never a .xml extension: the folder is globbed), the result parsed
with ElementTree before it is written, the body between each set's open and close tags REPLACED, so a re-run is
idempotent. Dry run by default; --apply refuses (exit 2) while the game or the Kit runs (_gamedir.game_or_kit_running).

    python tools/bind_orc_goblin_locomotion.py            # plan
    python tools/bind_orc_goblin_locomotion.py --apply
    python tools/bind_orc_goblin_locomotion.py --check    # exit 1 when the live sets differ from the plan
After --apply run  python tools/audit_action_set_parity.py  (a root-level <action> kills a dedicated server).
"""
import argparse
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET

GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
LIVE = os.path.join(GAME, "LOTRLOME_Armory", "ModuleData", "action_sets.xml")
NATIVE = os.path.join(GAME, "Native", "ModuleData", "action_sets.xml")
ASSETS = os.path.join(GAME, "LOTRLOME_Armory", "Assets")

# set id -> (clip prefix, the clips' folder under Assets); the prefix is the variant spec's "clip_prefix"
# (tools/blender/locomotion_variants/<variant>.json), the folders are where Mike keeps each race's art
SETS = {"as_orc_warrior": ("taom_orc_", os.path.join("Race Test", "Mordor", "Orc", "animations")),
        "as_goblin_warrior": ("taom_goblin_", os.path.join("Race Test", "Mordor", "Goblin", "animations"))}
STANCES = ("unarmed", "1h", "1h_with_shield")
KINDS = ("walk_forward", "run_forward", "walk_idle")
CODES = ["act_%s_%s%s" % (k, s, ls) for s in STANCES for k in KINDS for ls in ("", "_left_stance")]

ACTION_RE = re.compile(r"<action\b([^>]*?)/>", re.S)
ATTR_RE = re.compile(r'(\w+)\s*=\s*"([^"]*)"')


def vanilla_clips(native_text):
    """code -> clip for CODES, read from Native's full as_human_warrior."""
    m = re.search(r'<action_set\s+id="as_human_warrior"[^>]*>', native_text)
    if not m:
        raise SystemExit("as_human_warrior not found in " + NATIVE)
    end = native_text.index("</action_set>", m.end())
    found = {}
    for am in ACTION_RE.finditer(native_text, m.end(), end):
        a = dict(ATTR_RE.findall(am.group(1)))
        if a.get("type") in CODES and a["type"] not in found:
            found[a["type"]] = a["animation"]
    missing = [c for c in CODES if c not in found]
    if missing:
        raise SystemExit("codes not in Native as_human_warrior: " + ", ".join(missing))
    return found


def plan(vanilla, assets, clips_dir=None):
    """set id -> ([(code, clip)], [clips not on disk]). clips_dir, when given, replaces every set's own folder."""
    out = {}
    for set_id, (prefix, sub) in SETS.items():
        folder = clips_dir or os.path.join(assets, sub)
        rows, absent = [], []
        for code in CODES:
            clip = prefix + vanilla[code]
            if os.path.isfile(os.path.join(folder, clip + "_anm.tpac")):
                rows.append((code, clip))
            elif clip not in absent:
                absent.append(clip)
        out[set_id] = (rows, absent)
    return out


def find_set(text, set_id):
    m = re.search(r'<action_set\s+id="%s"[^>]*>' % re.escape(set_id), text)
    if not m:
        raise SystemExit("%s not found" % set_id)
    if m.group(0).endswith("/>"):
        raise SystemExit("%s is self-closing; this tool needs its open and close tags" % set_id)
    return m.end(), text.index("</action_set>", m.end())


def build_body(rows, nl):
    if not rows:
        return nl + "\t"
    head = ("%s\t\t<!-- Restyled orc and goblin locomotion (#791); GENERATED by tools/bind_orc_goblin_locomotion.py,"
            "%s\t\t     which owns this body: edit the tool, not these rows. Every other code inherits as_human_warrior. -->"
            % (nl, nl))
    return head + nl + nl.join('\t\t<action type="%s" animation="%s" />' % r for r in rows) + nl + "\t"


def check(text, planned):
    """Compare the live sets with the plan and prove the base_set chain. Returns the number of problems."""
    root = ET.fromstring(text.encode("utf-8"))
    sets = list(root.iter("action_set"))
    pos = {s.get("id"): i for i, s in enumerate(sets)}
    by_id = {s.get("id"): s for s in sets}
    if root.findall("action"):
        print("PROBLEM root-level <action> elements: %d (run tools/audit_action_set_parity.py)" % len(root.findall("action")))
    problems = len(root.findall("action"))
    for set_id, (rows, absent) in planned.items():
        live = [(a.get("type"), a.get("animation")) for a in by_id[set_id].findall("action")]
        if live != rows:
            problems += 1
            print("PROBLEM %s: live rows %d, planned %d" % (set_id, len(live), len(rows)))
            for r in sorted(set(rows) - set(live)):
                print("    missing %s -> %s" % r)
            for r in sorted(set(live) - set(rows)):
                print("    extra   %s -> %s" % r)
        else:
            print("ok %s: %d rows as planned (%d clips not on disk yet)" % (set_id, len(rows), len(absent)))
        # every set whose base_set chain reaches set_id must be parsed after it (base_set copies at parse time)
        codes = {c for c, _ in rows}
        groups, derived = {}, 0
        for sid, s in by_id.items():
            chain, cur, seen = [], s.get("base_set"), set()
            while cur and cur not in seen and cur != set_id:
                seen.add(cur)
                chain.append(cur)
                cur = by_id[cur].get("base_set") if cur in by_id else None
            if cur != set_id or sid == set_id:
                continue
            derived += 1
            if pos[sid] < pos[set_id]:
                problems += 1
                print("PROBLEM %s inherits %s but is defined before it" % (sid, set_id))
            own = {c for c in (a.get("type") for a in s.findall("action")) if c in codes}
            for mid in chain:   # an intermediate set can override a code too
                own |= {c for c in (a.get("type") for a in by_id[mid].findall("action")) if c in codes}
            groups.setdefault(tuple(sorted(own)), []).append(sid)
        print("   %d derived sets inherit it, all defined after it" % derived)
        for own, ids in sorted(groups.items(), key=lambda kv: -len(kv[1])):
            what = ("keep every bound code" if not own else "override " + ", ".join(own))
            print("   %3d %s (e.g. %s)" % (len(ids), what, ", ".join(ids[:3])))
    return problems


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--live", default=LIVE)
    ap.add_argument("--native", default=NATIVE)
    ap.add_argument("--assets", default=ASSETS, help="the Armory's Assets folder; each set's clips sit in its SETS folder")
    ap.add_argument("--clips-dir", default=None, help="one folder for every set's clips instead (tests)")
    args = ap.parse_args(argv)

    vanilla = vanilla_clips(open(args.native, "rb").read().decode("utf-8-sig"))
    planned = plan(vanilla, args.assets, args.clips_dir)
    raw = open(args.live, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    crlf = text.count("\r\n")
    nl = "\r\n" if crlf > text.count("\n") - crlf else "\n"   # the MAJORITY ending (tools/README.md XML I/O)

    if args.check:
        n = check(text, planned)
        print("check: %d problem(s)" % n)
        return 1 if n else 0

    new_text = text
    for set_id, (rows, absent) in planned.items():
        print("%s: %d of %d codes bound; clips not on disk: %d" % (set_id, len(rows), len(CODES), len(absent)))
        for clip in absent:
            print("    absent %s_anm.tpac" % clip)
        a, b = find_set(new_text, set_id)
        new_text = new_text[:a] + build_body(rows, nl) + new_text[b:]
    if new_text == text:
        print("no change: the sets already carry exactly this body")
        return 0
    ET.fromstring(new_text.encode("utf-8"))   # must still parse
    if not args.apply:
        print("DRY RUN: nothing written.")
        return 0
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from _gamedir import game_or_kit_running
    if game_or_kit_running():   # the shared guard; it also refuses when the process list cannot be read
        print("REFUSED: close the game and the Modding Kit first")
        return 2
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    bak = args.live + ".bak-orc-goblin-bind-" + stamp
    open(bak, "wb").write(raw)
    open(args.live, "wb").write((b"\xef\xbb\xbf" if bom else b"") + new_text.encode("utf-8"))
    after = open(args.live, "rb").read().decode("utf-8-sig")
    n = check(after, planned)
    print("written; backup %s; check: %d problem(s)" % (os.path.basename(bak), n))
    return 1 if n else 0


if __name__ == "__main__":
    sys.exit(main())
