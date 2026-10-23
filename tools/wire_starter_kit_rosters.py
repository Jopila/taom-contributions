#!/usr/bin/env python3
"""Point the culture-default player-start rosters at the `starter_` twins, make the ones whose ids vanilla
also defines replace vanilla's, and override the other vanilla cultures' starts with TAOM rosters.

TWO STEPS
---------
1. Rewire `taom_char_creation_equipment.xml` (the 256 `player_char_creation_{culture}_{title}_{m|f}`
   rosters, battle AND civilian sets): every Item0-3 / Body / Leg / Cape / Head / Gloves id becomes
   `Item.starter_<donor>` (a trailing `_starter` on the donor is stripped first); Horse and
   HorseHarness are left alone. The substitution is attribute-in-place, because the culture
   file puts every attribute on its own line and a re-emitted set would rewrite thousands of
   lines the change did not ask for. The id rule and the roster filter are imported from
   generate_starter_kit.py, so the two tools cannot disagree about which items exist. The
   career file is NOT rewired (#629): its `player_career_*` rosters name the gear each
   culture's lowest troops carry, and `REWIRE_FILES` pins that.
   The same pass gives `_replaceWhileMerging="true"` to every roster of that file whose id
   vanilla also defines (`mark_vanilla_replacements`; aserai's 37 since Erkam's 2026-10-10
   rosters), and refuses the flag on any other (`stray_replace_ids`).

2. Write `taom_player_start_vanilla_override.xml`: vlandia, empire, sturgia, aserai, battania
   and khuzait had no TAOM culture-default rosters, so their careerless start read vanilla's
   `SandBox/ModuleData/sandbox_equipment_sets.xml` and handed a Rohan player a Calradian sword
   in a hemp tunic. A roster the culture-default file already defines is left out
   (`roster_ids(REWIRE_FILES)`; all of aserai's since Erkam's 2026-10-10 rosters). The override
   carries a roster with the same id as vanilla's for every title the culture's youth menu
   offers, built from the mapped culture's career kit (copied as the career file names it) by
   title: hunter/skirmisher/bard take the ranged kit, guard/infantry the infantry kit,
   retainer the cavalry kit with its mount. Khand (battania) borrows Rhun's (khuzait) kit
   until it has its own (#571). The civilian set is the kit's one-handed weapon, body and legs.

   The roster element carries `_replaceWhileMerging="true"`. MBObjectManager.MergeElements
   (1.4.8, lines 799-875) merges a later module's same-id element INTO the earlier one, and
   the only unique key EquipmentRosters.xsd declares is `EquipmentRoster/@id`: a set has no
   key, so a plain same-id roster would APPEND its sets and the adapter would keep taking
   vanilla's first one. With the attribute on the roster the engine drops every vanilla
   attribute and child first (lines 804-808, 829-832), so ours is the whole roster. The
   attribute is legal everywhere because the engine injects it into every schema complex
   type before validating (line 1092).

USAGE
-----
    python tools/wire_starter_kit_rosters.py            # dry run: counts + a sample
    python tools/wire_starter_kit_rosters.py --apply
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import OrderedDict
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import generate_starter_kit as gk  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULEDATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
CC_FILE = MODULEDATA / "equipmentsets" / "taom_char_creation_equipment.xml"
CAREER_FILE = MODULEDATA / "equipmentsets" / "taom_career_starting_equipment.xml"
OVERRIDE_FILE = MODULEDATA / "equipmentsets" / "taom_player_start_vanilla_override.xml"
MENUS_DIR = MODULEDATA / "charactercreation"
# The files step 1 rewires. Not the career file: since #629 its rosters name the culture's lowest
# troop items, and a rewire would point them at starter_ twins that do not exist.
REWIRE_FILES = (CC_FILE,)

VANILLA_CULTURES = ("vlandia", "empire", "sturgia", "aserai", "battania", "khuzait")
BORROW = {"battania": "khuzait"}   # Khand has no gear of its own (#571); generate_career_kits KIT_SOURCE agrees
TITLE_TO_ARCHETYPE = {
    "hunter": "ranged", "skirmisher": "ranged", "bard": "ranged",
    "guard": "infantry", "infantry": "infantry", "mercenary": "infantry",
    "retainer": "cavalry",
}
# where each career archetype keeps its one-handed weapon (taom_career_starting_equipment.xml header)
SIDEARM_SLOT = {"ranged": "Item2", "cavalry": "Item2", "infantry": "Item0"}
VANILLA_SETS_FILE = gk.DEFAULT_MODULES / "SandBox" / "ModuleData" / "sandbox_equipment_sets.xml"
# every vanilla file that defines an id a culture-default roster may reuse (SandBoxCore: the aserai
# parents' `*_none_aserai` rosters)
VANILLA_ROSTER_FILES = (VANILLA_SETS_FILE,
                        gk.DEFAULT_MODULES / "SandBoxCore" / "ModuleData" / "sandboxcore_equipment_sets.xml")
REPLACE_ATTR = '_replaceWhileMerging="true"'

ROSTER_RE = re.compile(r"<EquipmentRoster\b([^>]*)>(.*?)</EquipmentRoster>", re.S)
ROSTER_OPEN_RE = re.compile(r"<EquipmentRoster\b([^>]*)>", re.S)
EQUIPMENT_RE = re.compile(r"<Equipment\b[^>]*?/?>", re.S)
ID_ATTR_RE = re.compile(r'\bid="([^"]*)"')
SLOT_ATTR_RE = re.compile(r'\bslot="([^"]*)"')


class WireError(Exception):
    pass


def _is_target(roster_id: str) -> bool:
    return roster_id.startswith(gk.TARGET_PREFIXES) and not any(m in roster_id for m in gk.EXCLUDED_ROSTER_MARKERS)


def roster_ids(paths) -> frozenset:
    """Every `EquipmentRoster` id the given files define: vanilla's (VANILLA_ROSTER_FILES) decide which
    rosters replace, and the culture-default files' (REWIRE_FILES) are left out of the override, because
    the same id twice in one module is DUPLICATE_ROSTER_ID and the override, which loads after the
    culture-default file with _replaceWhileMerging, would replace the culture's own start (aserai,
    2026-10-14)."""
    ids = set()
    for path in paths:
        ids.update(r.get("id") for r in ET.fromstring(gk.read_xml(path)[0].encode("utf-8")).iter("EquipmentRoster"))
    return frozenset(ids)


def _comment_spans(text: str) -> list[tuple[int, int]]:
    return [m.span() for m in re.finditer(r"<!--.*?-->", text, re.S)]


def _in_spans(pos: int, spans: list[tuple[int, int]]) -> bool:
    return any(start <= pos < end for start, end in spans)


def mark_vanilla_replacements(text: str, vanilla: frozenset) -> tuple[str, list[str]]:
    """Give `_replaceWhileMerging="true"` to every roster whose id vanilla also defines, on a new
    attribute line indented like its `id=` (inline when the tag is one line). Without it
    MergeElements APPENDS the roster's sets after vanilla's and the adapter takes vanilla's first
    battle and civilian set: the aserai start fell back to Calradian gear (2026-10-14). Only those
    ids: on a roster vanilla lacks, the flag merges it into the document's first roster."""
    marked: list[str] = []
    comments = _comment_spans(text)

    def one(m: re.Match) -> str:
        if _in_spans(m.start(), comments):
            return m.group(0)
        attrs = m.group(1)
        idm = ID_ATTR_RE.search(attrs)
        if attrs.endswith("/") or not idm or idm.group(1) not in vanilla or "_replaceWhileMerging" in attrs:
            return m.group(0)
        marked.append(idm.group(1))
        ws = re.search(r'(\s+)id="', attrs).group(1)
        return f"<EquipmentRoster{attrs}{ws}{REPLACE_ATTR}>"

    return ROSTER_OPEN_RE.sub(one, text), marked


def stray_replace_ids(text: str, vanilla: frozenset) -> list[str]:
    """Rosters carrying `_replaceWhileMerging` whose id vanilla does not define: MergeElements would
    merge each one into the FIRST roster of the accumulated document (1.5.3 MBObjectManager.MergeElements,
    lines 846 and 857-859: it loses its own id and content). The override file is checked through this too,
    since every override roster carries the flag. ElementTree drops comments, so a commented-out roster is
    never reported."""
    return [r.get("id") for r in ET.fromstring(text.encode("utf-8")).iter("EquipmentRoster")
            if r.get("_replaceWhileMerging") == "true" and r.get("id") not in vanilla]


def rewire_text(text: str) -> tuple[str, int, list[str]]:
    """(text, ids changed, roster ids touched). In place: only `id="Item.x"` values move."""
    changed = 0
    rosters: list[str] = []

    def roster_sub(match: re.Match) -> str:
        nonlocal changed
        attrs, body = match.group(1), match.group(2)
        id_match = ID_ATTR_RE.search(attrs)
        roster_id = id_match.group(1) if id_match else ""
        if not _is_target(roster_id):
            return match.group(0)
        count = 0

        def equipment_sub(em: re.Match) -> str:
            nonlocal count
            tag = em.group(0)
            slot = SLOT_ATTR_RE.search(tag)
            iid = ID_ATTR_RE.search(tag)
            if not slot or not iid or slot.group(1) not in gk.PLAYER_SLOTS:
                return tag
            item = iid.group(1)
            if not item.startswith("Item."):
                return tag
            bare = item[len("Item."):]
            if bare.startswith(gk.STARTER_PREFIX):
                return tag
            count += 1
            return tag[:iid.start(1)] + "Item." + gk.starter_id(bare) + tag[iid.end(1):]

        new_body = EQUIPMENT_RE.sub(equipment_sub, body)
        if count:
            changed += count
            rosters.append(roster_id)
        return f"<EquipmentRoster{attrs}>{new_body}</EquipmentRoster>"

    return ROSTER_RE.sub(roster_sub, text), changed, rosters


def leftovers(text: str) -> list[tuple[str, str, str]]:
    """(roster, slot, id) for every player-slot id in a target roster that is not a starter."""
    out = []
    root = ET.fromstring(text.encode("utf-8"))
    wanted = set(gk.target_roster_ids(root))
    for roster in root.iter("EquipmentRoster"):
        if roster.get("id") not in wanted:
            continue
        for eq in roster.iter("Equipment"):
            slot = eq.get("slot") or ""
            iid = (eq.get("id") or "").replace("Item.", "", 1)
            if slot in gk.PLAYER_SLOTS and not iid.startswith(gk.STARTER_PREFIX):
                out.append((roster.get("id"), slot, iid))
    return out


def titles_from_menus(menus_dir: Path, cultures=VANILLA_CULTURES) -> "OrderedDict[str, list[str]]":
    youth = json.loads((menus_dir / "youth_menu.json").read_text(encoding="utf-8"))
    titles: "OrderedDict[str, list[str]]" = OrderedDict((c, []) for c in cultures)
    for opt in youth:
        culture, title = opt.get("culture_id"), opt.get("title_type")
        if culture in titles and title and title not in titles[culture]:
            titles[culture].append(title)
    missing = [c for c, t in titles.items() if not t]
    if missing:
        raise WireError(f"youth_menu.json offers no title for {missing}")
    return titles


def build_vanilla_override(career_text: str, titles_by_culture, borrow: dict[str, str], eol: str = "\r\n",
                           owned: frozenset = frozenset()) -> str:
    """`owned`: roster ids the culture-default file already defines (roster_ids(REWIRE_FILES)); they are
    left out, and a culture with nothing left gets no block."""
    root = ET.fromstring(career_text.encode("utf-8"))
    career = {r.get("id"): r for r in root.iter("EquipmentRoster")}
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        "<!--",
        "  TAOM player-start rosters for the vanilla-mapped cultures. GENERATED by",
        "  tools/wire_starter_kit_rosters.py (do not hand-edit; re-run the tool).",
        "",
        "  vlandia (Rohan), empire (Dunland), sturgia (Dale), aserai (Harad), khuzait (Rhun) and",
        "  battania (Khand) had no TAOM culture-default rosters, so their careerless start read",
        "  vanilla SandBox's rosters and handed out Calradian gear. A roster that",
        "  taom_char_creation_equipment.xml now defines is left out (all of aserai's since",
        "  2026-10-10): this file loads after that one and would replace it. Each roster here carries the",
        "  SAME id as vanilla's and _replaceWhileMerging=\"true\": MBObjectManager.MergeElements",
        "  (1.4.8, lines 799-875) then drops every vanilla attribute and child before adding ours,",
        "  which is the only shape that replaces rather than appends (EquipmentSet has no unique",
        "  key in EquipmentRosters.xsd). Battle set = the mapped culture's career kit by title",
        "  (hunter/skirmisher/bard ranged, guard/infantry infantry, retainer cavalry), which is the",
        "  gear the culture's lowest troops carry (#629); civilian set = its one-handed weapon,",
        "  body and legs. Khand borrows Rhun's kit until it has its own (#571).",
        "-->",
        "<EquipmentRosters>",
    ]
    for culture, titles in titles_by_culture.items():
        source = borrow.get(culture, culture)
        if all(f"player_char_creation_{culture}_{t}_{s}" in owned for t in titles for s in ("m", "f")):
            continue
        lines.append("")
        lines.append(f"    <!-- {culture}{' (borrows ' + source + ')' if source != culture else ''} -->")
        for title in titles:
            archetype = TITLE_TO_ARCHETYPE.get(title)
            if archetype is None:
                raise WireError(f"{culture}: youth title {title!r} has no archetype mapping")
            for sex in ("m", "f"):
                if f"player_char_creation_{culture}_{title}_{sex}" in owned:
                    continue
                source_id = f"player_career_{source}_{archetype}_{sex}"
                roster = career.get(source_id)
                if roster is None:
                    raise WireError(f"{culture}/{title}/{sex}: career roster {source_id} is missing")
                battle = [s for s in roster.findall("EquipmentSet") if s.get("equipmentType") is None]
                if len(battle) != 1:
                    raise WireError(f"{source_id}: expected one battle EquipmentSet, found {len(battle)}")
                rows = [(e.get("slot"), e.get("id")) for e in battle[0].findall("Equipment")]
                by_slot = dict(rows)
                sword = by_slot.get(SIDEARM_SLOT[archetype])
                if not sword or not by_slot.get("Body") or not by_slot.get("Leg"):
                    raise WireError(f"{source_id}: needs a one-handed weapon in {SIDEARM_SLOT[archetype]}, "
                                    "a Body and a Leg")
                rid = f"player_char_creation_{culture}_{title}_{sex}"
                lines.append(f'    <EquipmentRoster id="{rid}" culture="Culture.{culture}" _replaceWhileMerging="true">')
                lines.append("        <EquipmentSet>")
                for slot, iid in rows:
                    lines.append(f'            <Equipment slot="{slot}" id="{iid}" />')
                lines.append("        </EquipmentSet>")
                lines.append('        <EquipmentSet equipmentType="Civilian">')
                lines.append(f'            <Equipment slot="Item0" id="{sword}" />')
                lines.append(f'            <Equipment slot="Body" id="{by_slot["Body"]}" />')
                lines.append(f'            <Equipment slot="Leg" id="{by_slot["Leg"]}" />')
                lines.append("        </EquipmentSet>")
                lines.append("    </EquipmentRoster>")
    lines.append("")
    lines.append("</EquipmentRosters>")
    return eol.join(lines) + eol


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write (default: dry run)")
    ap.add_argument("--skip-override", action="store_true", help="rewire only; do not write the vanilla override")
    args = ap.parse_args()

    rc = 0
    missing = [p for p in VANILLA_ROSTER_FILES if not p.exists()]
    if missing:
        print(f"ERROR: {', '.join(map(str, missing))} not found; the vanilla roster ids decide which rosters replace")
        return 2
    vanilla = roster_ids(VANILLA_ROSTER_FILES)
    for path in REWIRE_FILES:
        text, had_bom = gk.read_xml(path)
        new_text, changed, rosters = rewire_text(text)
        new_text, marked = mark_vanilla_replacements(new_text, vanilla)
        left = leftovers(new_text)
        print(f"{path.name}: {changed} id(s) in {len(rosters)} roster(s) -> starter_; leftovers after: {len(left)}")
        print(f"   {len(marked)} roster(s) that vanilla also defines now replace it ({REPLACE_ATTR})")
        for roster, slot, iid in left[:10]:
            print(f"   leftover {roster} {slot} {iid}")
        stray = stray_replace_ids(new_text, vanilla)
        if stray:
            print(f"ERROR: {len(stray)} roster(s) carry {REPLACE_ATTR} but vanilla does not define them; the engine "
                  f"would merge each into the first roster: {', '.join(stray)}")
            return 2
        if (changed or marked) and args.apply:
            err = gk.checked_write(path, new_text, had_bom, gk.BACKUP_TAG, backup=False)
            if err:
                print("ERROR:", err)
                return 2
            print(f"   wrote {path}")

    if not args.skip_override:
        try:
            titles = titles_from_menus(MENUS_DIR)
            # the career kit exactly as the career file names it, never rewired (#629); a roster the
            # culture-default file already defines is left out
            override = build_vanilla_override(gk.read_xml(CAREER_FILE)[0], titles, BORROW, owned=roster_ids(REWIRE_FILES))
        except WireError as exc:
            print("ERROR:", exc)
            return 2
        missing = stray_replace_ids(override, vanilla)
        if missing:
            print(f"ERROR: {len(missing)} override roster id(s) vanilla does not define, which the engine "
                  f"would merge into another roster: {', '.join(missing)}")
            return 2
        count = override.count("<EquipmentRoster ")
        if OVERRIDE_FILE.exists() and OVERRIDE_FILE.read_bytes() == override.encode("utf-8"):
            print(f"{OVERRIDE_FILE.name}: {count} rosters, unchanged")
        elif args.apply:
            err = gk.checked_write(OVERRIDE_FILE, override, False, gk.BACKUP_TAG, backup=False)
            if err:
                print("ERROR:", err)
                return 2
            print(f"{OVERRIDE_FILE.name}: {count} rosters, wrote {OVERRIDE_FILE}")
        else:
            print(f"{OVERRIDE_FILE.name}: {count} rosters (dry run)")
            print("\n".join(override.splitlines()[17:33]))
    if not args.apply:
        print("\n(dry run; pass --apply to write)")
    else:
        print("\nRegister the override in Main/_Module/SubModule.xml if it is new, run validate_moduledata.py,")
        print("then RESTART Bannerlord and start a NEW campaign before believing anything.")
    return rc


if __name__ == "__main__":
    sys.exit(main())
