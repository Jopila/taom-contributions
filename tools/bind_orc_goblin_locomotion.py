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
unless --allow-shrink. --check exits 1 when the live sets differ from the plan, and also when a set or a gait variant
has no restyled clip on disk at all, which is what a reinstall leaves (no clips and no rows), unless --allow-no-clips. Bad input (a
missing file, a missing or self-closing set when planning or applying, XML that does not parse or is not UTF-8, a
Native row with no type) exits 2; under --check a missing or self-closing set is a PROBLEM (exit 1).

    python tools/bind_orc_goblin_locomotion.py            # plan
    python tools/bind_orc_goblin_locomotion.py --apply
    python tools/bind_orc_goblin_locomotion.py --check
After --apply run  python tools/audit_action_set_parity.py  (a root-level <action> kills a dedicated server).

Gait VARIANTS (per soldier; the C# AnimationVariety feature swaps an agent's set at spawn). VARIANTS lists, per race,
as_orc_warrior_b / _c. A variant set has base_set = the race's as_orc_warrior (copied at parse time, so it sits AFTER
that set and its rows) and holds ONLY the forward walk and run codes whose Native clip is one of the 10 VARIANT_CLIPS,
bound to <prefix><clip> (taom_orc_b_<clip>); it never overrides an idle code, so a variant inherits variant A's idles.
All variant sets sit in ONE generated block right after the race's base set, between BEGIN/END gait-variant comments,
which this tool owns.

No orc variant has a female twin: the Armory's orc Monster declares no female_action_set (monsters.xml), so a female orc
plays as_orc_warrior and an as_orc_female_warrior_b would be unreachable. The goblin Monster does declare one, so the
twin MECHANISM stays: a Variant with an fid is followed by a self-closing set whose base_set is its variant, exactly
like as_orc_female_warrior (self-closing, base_set = as_orc_warrior).

A variant folder holding no *_anm.tpac at all is SKIPPED with a note and never written. Any package in it makes it a
real input: all 10 of <prefix><clip> must be there, else it is PARTIAL (exit 2 on --apply, a PROBLEM on --check), so a
folder of wrong-prefix clips is an error and never "no clips yet". --check fails a variant that is skipped and absent (no
clips and no set: what a reinstall leaves, since it drops both together), like a race set with no clips, unless
--allow-no-clips (an install before the Kit import; it then prints "no variant clips on disk yet"). It also fails when
a variant set is in the file but its clips are missing, its rows differ from the plan, it carries an idle or
non-forward code, it is defined before its base or its twin before it, or a row names a package that is not on disk.
--apply's own re-check allows an absent variant, because it writes only what is on disk. (Until 2026-10-18 an absent
variant passed plain --check and needed --require-variants to fail; that flag is gone.)

An id this tool owns (the race sets and the variant sets) must be defined once, and a variant only inside the generated
block. The engine merges a repeated action_set into its FIRST definition, so an earlier copy with a wrong base_set
survives while the later one passes --check; a repeat, a variant outside the block or a second block is refused on
every mode (exit 2, bytes unchanged) and fails --check. So does ANY action_set between the markers that the tool does
not own, whether or not another definition of its id sits elsewhere (a rewrite replaces the whole region, and the engine
keeps the FIRST definition, so the surviving copy would change the set's base_set): move it out by hand. A backstop
behind that compares id COUNTS and refuses (exit 2, even with --allow-shrink) when an unowned id loses a copy.
"""
import argparse
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, namedtuple

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
# per-soldier gait variants: (variant set, its female twin or None, the race set both inherit, clip prefix, folder under
# Assets). The prefix is the variant JSON's "clip_prefix". The orc Monster has no female_action_set, so its variants
# have no twin; goblins join later with one more row each (as_goblin_warrior_b ..., with a twin: their Monster has one).
Variant = namedtuple("Variant", "vid fid base prefix sub")
VARIANTS = [Variant("as_orc_warrior_b", None, "as_orc_warrior", "taom_orc_b_",
                    os.path.join("Race Test", "Mordor", "Orc", "animations_b")),
            Variant("as_orc_warrior_c", None, "as_orc_warrior", "taom_orc_c_",
                    os.path.join("Race Test", "Mordor", "Orc", "animations_c"))]
VARIANT_CLIPS = ["walk_forward_unarmed", "walk_forward_1h", "walk_forward_1h_left_stance",
                 "walk_forward_1h_with_shield", "walk_forward_1h_with_shield_left_stance",
                 "run_forward_unarmed", "run_forward_1h", "run_forward_1h_left_stance",
                 "run_forward_1h_with_shield", "run_forward_1h_with_shield_left_stance"]
VARIANT_CODE_RE = re.compile(r"act_(walk|run)_forward_")   # forward gaits only; idles stay variant A's
IDLE_CODE_RE = re.compile(r"act_(walk|run)_idle_")
BLOCK_RE = r"<!-- BEGIN gait variants of %s .*?-->.*?<!-- END gait variants of %s -->"
BEGIN_RE = r"<!-- BEGIN gait variants of %s "
END_RE = r"<!-- END gait variants of %s -->"
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


def variant_ids():
    """Every set id the variant block owns: each variant and its twin, if it has one."""
    return {x for v in VARIANTS for x in (v.vid, v.fid) if x}


def set_ids(text):
    """The id of every action_set in document order (a commented-out copy is not one)."""
    return [s.get("id") for s in ET.fromstring(text.encode("utf-8")).iter("action_set")]


def has_any_package(folder):
    """True when folder holds any *_anm.tpac, whatever its prefix."""
    return os.path.isdir(folder) and any(f.endswith(PACKAGE_TAIL) for f in os.listdir(folder))


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


def plan_variants(human, assets):
    """[(Variant, status, codes, rows, missing)]: status "ready" (all 10 packages on disk), "skipped" (the folder holds
    no *_anm.tpac at all) or "partial" (anything else, wrong-prefix packages included). codes = [(code, clip)] the
    variant would bind in Native order, whatever is on disk; rows = those with the prefix applied, only when ready."""
    codes = [(c, clip) for c, clip in human if VARIANT_CODE_RE.match(c) and clip in VARIANT_CLIPS]
    out = []
    for v in VARIANTS:
        folder = os.path.join(assets, v.sub)
        have = packages(folder, v.prefix)
        missing = [c for c in VARIANT_CLIPS if c not in have]
        status = "ready" if not missing else "partial" if has_any_package(folder) else "skipped"
        rows = [(c, v.prefix + clip) for c, clip in codes] if status == "ready" else []
        out.append((v, status, codes, rows, missing))
    return out


def variant_block(base, ready, nl):
    """The generated block that follows base's own set: each ready variant, then its female twin if it has one."""
    parts = []
    for v, rows in ready:
        parts.append('\t<action_set id="%s" base_set="%s">' % (v.vid, base))
        parts += ['\t\t<action type="%s" animation="%s" />' % r for r in rows]
        parts.append("\t</action_set>")
        if v.fid:
            parts.append('\t<action_set id="%s" base_set="%s" />' % (v.fid, v.vid))
    return nl.join(["<!-- BEGIN gait variants of %s (#791); GENERATED by tools/bind_orc_goblin_locomotion.py, which "
                    "owns this block: edit the tool, not these sets. Walk and run forward codes only; idles inherit "
                    "%s. -->" % (base, base)] + parts + ["\t<!-- END gait variants of %s -->" % base])


def apply_variants(text, base, ready, nl):
    """text with base's gait-variant block written, replaced or (with nothing ready) removed. Idempotent. The caller has
    already refused a file that defines a variant id outside the block (definition_problems)."""
    m = re.search(BLOCK_RE % (re.escape(base), re.escape(base)), text, re.S)
    if not ready:
        if not m:
            return text
        start = m.start()
        while text[start - 1] == "\t":
            start -= 1
        head = text[:start]
        return (head[:-2 * len(nl)] if head.endswith(nl + nl) else head) + text[m.end():]
    block = variant_block(base, ready, nl)
    if m:
        return text[:m.start()] + block + text[m.end():]
    _, end = find_set(text, base)
    end += len("</action_set>")
    return text[:end] + nl + nl + "\t" + block + text[end:]


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


def definition_problems(text):
    """[message] for what makes the file ambiguous to this tool: an id it owns (a race set or a variant) defined more
    than once, a variant outside the generated block, or a block that is repeated or torn. The engine merges a repeated
    action_set into its FIRST definition, so a later copy passing the check proves nothing."""
    ids = set_ids(text)
    out = ["%s is defined %d times; the engine merges a repeat into the first definition, so keep one" % (i, ids.count(i))
           for i in sorted(set(SETS) | variant_ids()) if ids.count(i) > 1]
    inside = []
    for base in sorted({v.base for v in VARIANTS}):
        blocks = list(re.finditer(BLOCK_RE % (re.escape(base), re.escape(base)), text, re.S))
        begins, ends = len(re.findall(BEGIN_RE % re.escape(base), text)), len(re.findall(END_RE % re.escape(base), text))
        if len(blocks) > 1 or begins != len(blocks) or ends != len(blocks):
            out.append("the gait-variant block of %s is repeated or torn (%d complete, %d BEGIN, %d END markers); keep one"
                       % (base, len(blocks), begins, ends))
        for m in blocks:
            here = set_ids("<r>" + m.group(0) + "</r>")
            inside += here
            # a rewrite replaces the whole region, so ANY definition in it that the tool does not own goes with it, whether
            # or not a copy of its id survives elsewhere (the engine keeps the FIRST copy, so that would change its base_set)
            out += ["%s sits inside the generated block of %s but this tool does not own it; move it out by hand first"
                    % (i, base) for i in sorted(set(here) - variant_ids())]
    out += ["%s is defined outside the generated block; remove it by hand first" % i
            for i in sorted(variant_ids()) if ids.count(i) > inside.count(i)]
    return out


def check_definitions(text):
    """Print each definition problem; returns their number."""
    msgs = definition_problems(text)
    for m in msgs:
        print("PROBLEM " + m)
    return len(msgs)


def compare_rows(set_id, live, rows):
    """Print how a set's live (type, animation) rows compare with the plan; returns 1 when they differ, else 0."""
    key = lambda r: tuple(x or "" for x in r)   # a row with no type must report, not raise TypeError
    if live == rows:
        print("ok %s: %d rows as planned" % (set_id, len(rows)))
        return 0
    print("PROBLEM %s: live rows %d, planned %d" % (set_id, len(live), len(rows)))
    for r in sorted(set(rows) - set(live), key=key):
        print("    missing %s -> %s" % r)
    for r in sorted(set(live) - set(rows), key=key):
        print("    extra   %s -> %s" % r)
    return 1


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
        problems += compare_rows(set_id, live, rows)
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
            if cur != set_id or sid == set_id or sid in variant_ids():   # variants: check_variants proves them
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


def check_variants(text, vplans, assets, allow_absent=False):
    """Prove the gait-variant sets. Returns the number of problems. A variant that is skipped and absent (no clips
    and no set: what a reinstall leaves) is a problem like a race set with no clips, unless allow_absent
    (--allow-no-clips under --check, an install before the Kit import; --apply's own re-check, which writes only
    what is on disk)."""
    root = ET.fromstring(text.encode("utf-8"))
    sets = list(root.iter("action_set"))
    pos = {s.get("id"): i for i, s in enumerate(sets)}
    by_id = {s.get("id"): s for s in sets}
    problems, skipped = 0, 0
    for v, status, _codes, rows, missing in vplans:
        folder = os.path.join(assets, v.sub)
        here = [x for x in (v.vid, v.fid) if x and x in by_id]
        if status == "skipped" and not here:
            if not allow_absent:
                problems += 1
                print("PROBLEM %s: no variant clips in %s and no set in the file, which is what a reinstall leaves; "
                      "import and generate the clips, then --apply (before the Kit import: --allow-no-clips)"
                      % (v.vid, v.sub))
            else:
                skipped += 1
                print("skip %s: no variant clips in %s" % (v.vid, v.sub))
            continue
        if status != "ready":
            problems += 1
            print("PROBLEM %s: clips missing in %s (%d of %d: %s)%s" % (
                v.vid, v.sub, len(missing), len(VARIANT_CLIPS), ", ".join(v.prefix + m for m in missing),
                "; the set is in the file" if here else ""))
        if v.vid not in by_id:
            if status == "ready":
                problems += 1
                print("PROBLEM %s is not in the file (run --apply)" % v.vid)
        else:
            s = by_id[v.vid]
            if s.get("base_set") != v.base:
                problems += 1
                print("PROBLEM %s: base_set %r, expected %s" % (v.vid, s.get("base_set"), v.base))
            if v.base in pos and pos[v.vid] < pos[v.base]:
                problems += 1
                print("PROBLEM %s is defined before its base %s (base_set copies at parse time)" % (v.vid, v.base))
            live = [(a.get("type"), a.get("animation")) for a in s.findall("action")]
            for code, anim in live:
                if IDLE_CODE_RE.match(code or ""):
                    problems += 1
                    print("PROBLEM %s overrides idle code %s (variants inherit variant A's idles)" % (v.vid, code))
                elif not VARIANT_CODE_RE.match(code or ""):
                    problems += 1
                    print("PROBLEM %s carries %s, not a forward walk or run code" % (v.vid, code))
                if not (anim or "").startswith(v.prefix) or not os.path.isfile(
                        os.path.join(folder, anim + PACKAGE_TAIL)):
                    problems += 1
                    print("PROBLEM %s: %s -> %s has no package in %s" % (v.vid, code, anim, v.sub))
            if status == "ready":
                problems += compare_rows(v.vid, live, rows)
        if not v.fid:
            continue
        if v.fid not in by_id:
            if status == "ready":
                problems += 1
                print("PROBLEM %s is not in the file (run --apply)" % v.fid)
        else:
            f = by_id[v.fid]
            if f.get("base_set") != v.vid:
                problems += 1
                print("PROBLEM %s: base_set %r, expected %s" % (v.fid, f.get("base_set"), v.vid))
            if v.vid in pos and pos[v.fid] < pos[v.vid]:
                problems += 1
                print("PROBLEM %s is defined before its variant %s" % (v.fid, v.vid))
            if f.findall("action"):
                problems += 1
                print("PROBLEM %s carries its own rows; the twin is empty like as_orc_female_warrior" % v.fid)
    if skipped and skipped == len(vplans):
        print("no variant clips on disk yet: %d variant set(s) skipped, nothing to check" % skipped)
    return problems


def print_variant_plan(vplans):
    for v, status, codes, rows, missing in vplans:
        if status == "ready":
            print("%s%s: %d codes bound" % (v.vid, " (+ %s)" % v.fid if v.fid else "", len(rows)))
            for r in rows:
                print("    %s -> %s" % r)
        elif status == "skipped":
            print("%s%s: SKIPPED, no variant clips in %s (would bind %d codes from %d clips)"
                  % (v.vid, " (+ %s)" % v.fid if v.fid else "", v.sub, len(codes), len(VARIANT_CLIPS)))
        else:
            print("%s: PARTIAL, %d of %d clips missing in %s: %s" % (
                v.vid, len(missing), len(VARIANT_CLIPS), v.sub, ", ".join(v.prefix + m for m in missing)))


def run(args):
    human = human_rows(open(args.native, "rb").read().decode("utf-8-sig"))
    planned = plan(human, args.assets)
    vplans = plan_variants(human, args.assets)
    raw = open(args.live, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    crlf = text.count("\r\n")
    nl = "\r\n" if crlf > text.count("\n") - crlf else "\n"   # the MAJORITY ending (tools/README.md XML I/O)

    if args.check:
        n = (check_definitions(text) + check(text, planned, args.allow_no_clips)
             + check_variants(text, vplans, args.assets, args.allow_no_clips))
        print("check: %d problem(s)" % n)
        return 1 if n else 0

    found = definition_problems(text)
    if found:   # before any edit: find_set and the checks read different copies of a repeated id
        raise InputError("; ".join(found))
    live_sets = {s.get("id"): s for s in ET.fromstring(text.encode("utf-8")).iter("action_set")}
    new_text, drops = text, 0
    for set_id, (rows, unbound) in planned.items():
        print("%s: %d codes bound" % (set_id, len(rows)))
        for clip in unbound:
            print("    note: no code plays %s" % clip)
        a, b = find_set(new_text, set_id)
        kept = {code for code, _ in rows}
        for code in sorted({x.get("type") for x in live_sets[set_id].findall("action")} - kept, key=lambda c: c or ""):
            print("    DROPS %s (bound now; the plan no longer binds it)" % code)
            drops += 1
        new_text = new_text[:a] + build_body(rows, nl) + new_text[b:]
    print_variant_plan(vplans)
    if any(p[1] == "partial" for p in vplans):
        raise InputError("a variant has only some of its %d clips on disk; finish the Kit import first"
                         % len(VARIANT_CLIPS))
    for base in sorted({v.base for v in VARIANTS}):
        ready = [(p[0], p[3]) for p in vplans if p[0].base == base and p[1] == "ready"]
        keep = {(v.vid, c) for v, rows in ready for c, _ in rows}
        for p in vplans:
            if p[0].base != base:
                continue
            for code in sorted({x.get("type") for x in live_sets[p[0].vid].findall("action")} if p[0].vid in live_sets else (),
                               key=lambda c: c or ""):
                if (p[0].vid, code) not in keep:
                    print("    DROPS %s from %s (bound now; the plan no longer binds it)" % (code, p[0].vid))
                    drops += 1
        new_text = apply_variants(new_text, base, ready, nl)
    if new_text == text:
        print("no change: the sets already carry exactly this body")
        return 0
    # a backstop behind definition_problems: compare COUNTS, so an unowned id that loses a copy is caught even when
    # another copy keeps the id in the set. Also proves new_text still parses.
    before, after = Counter(set_ids(text)), Counter(set_ids(new_text))
    lost = [i for i in before if i not in variant_ids() and after[i] < before[i]]
    if lost:
        print("REFUSED: the rewrite would delete a definition of %s, which this tool does not own (it sits between the "
              "gait-variant markers); move it out of the block by hand" % ", ".join(sorted(lost)))
        return 2
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
    n = (check_definitions(after) + check(after, planned, args.allow_no_clips)
         + check_variants(after, vplans, args.assets, allow_absent=True))
    print("written; backup %s; check: %d problem(s)" % (os.path.basename(bak), n))
    return 1 if n else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--allow-no-clips", action="store_true",
                    help="--check passes a race set or a gait variant with no restyled clip on disk (an install "
                         "before the Kit import)")
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
