"""Write the WildMesh3D mounts into the LIVE LOTRLOME_Armory (#794): Monsters, action sets, Horse items, English names.

    python tools/apply_wildmesh_armory.py            # dry run: prints what would change, writes nothing
    python tools/apply_wildmesh_armory.py --apply    # Modding Kit and game CLOSED

Sibling of tools/apply_animalia_armory.py (#646), whose I/O helpers it imports, for the five WildMesh3D
animals (docs/features/wildmesh-mounts.md): the War Boar, Bristleback Boar and Iron Hills Goat for the Dwarves,
the Mirkwood Stag and Greenwood Moose for the Elves. The Armory is unversioned, so every edit here is recorded
in docs/reference/lotrlome-wildmesh-changes.md.

The unit is the ANIMAL, not the five-animal batch: every step inserts only the animals its file lacks, so a
sixth animal added to the tables below reaches an already applied Armory as one pure insertion per file. An
animal present in a file is left alone; an animal PARTLY present in one file (a set without its _map twin) is
refused, naming the missing ids; an animal absent from one file and present in another is inserted where absent
(a Horse item deleted by hand is put back).

  1. ModuleData/Monsters/LOTR/lotr_monster_wildmesh.xml: taom_wildmesh_<animal>, base_monster="horse",
     action_set as_wildmesh_<animal>, taom_body_length (Mike, 2026-10-14: 100, the moose 150). The dwarf mounts
     copy the war ram's Monster values (weight, hit points, jump acceleration, charge speed limit); the deer and
     moose copy the Animalia elk's and moose's. Rendered from the MONSTERS table. Absent: written whole. Present:
     each missing Monster goes in before </Monsters>; a file with no </Monsters> (0 bytes, <Monsters />) is
     refused, because the tool created it and has no backup of it: delete it and the tool writes it whole.
  2. SubModule.xml: one Monsters registration block after the Animalia one (exact path token; the Animalia
     anchor must occur exactly once).
  3. ModuleData/action_sets.xml: as_wildmesh_<animal> (base as_horse) with its _town_and_village and _map twins.
     The first run writes the whole block (header, sets, end marker) before the /WARG marker; later runs insert
     the missing animals' sets just before the /WILDMESH3D MOUNTS end marker. Each override copies the vanilla
     as_horse action's attributes and changes only the clip (tools/gen_animalia_anim_clips.ps1 -Pack wildmesh
     wrote them). The warthog's canter and gallop bind the boar's clips and the goat's gallop the deer's (the
     pack's own versions do not loop or curve sideways). Rebound: the forward and in-place gait rows at paces 1
     to 5, the standing pace, the stands, idles and riderless idles, and the two hit reactions. Everything else stays
     the horse's: falls, rear, kick, walking backwards, strafes, turns, quick stops, jumps and the head-turn
     additives. No attack yet (stage 12).
  4. ModuleData/LOTRLOME_items/LOTRAOM_horses.xml: taom_wildmesh_<animal>_a, the body_length="100" placeholder
     Items.xsd requires (TAOM copies the Monster's size in at game init), not merchandise until a campaign troop
     rides them. Missing items go after the last taom_wildmesh_*_a item, the first run after
     taom_animalia_moose_a with a block comment.
  5. ModuleData/Languages/loc_LOTRAOM_horses.xml (the root English file only; the 12 language folders are a
     paid translation run): one <string id="taom_wildmesh_<animal>_a" text="<name>"/> row per animal, the
     item's own {=...} default from ITEM_STATS, after the last taom_wildmesh row or else after the
     taom_animalia_moose_a row.

Byte-faithful (tools/README.md "XML I/O convention"): BOM and line endings kept, every changed file parsed before
anything is written (a parse failure is a "PARSE FAILED: <file>" problem, exit 1, nothing written), and a
write-once backup beside each edited file whose non-.xml suffix carries a per-run stamp
(.bak-wildmesh-<YYYYMMDD-HHMMSS>, computed once per run), so a later run never overwrites an earlier backup.
Idempotent. Every bound clip must exist under Assets/creature/creatures/animations. A missing input file
exits 2 (bad input), a refused data check exits 1. Refuses --apply while the game or the Kit runs. --game-dir
points the whole script at a synthetic tree (tools/tests/test_apply_wildmesh_armory.py).
"""
import argparse
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import ensure_exists, game_dir, game_or_kit_running  # noqa: E402
from apply_animalia_armory import nl_of, read, write  # noqa: E402

_DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
BACKUP_PREFIX = ".bak-wildmesh-"
ANIMALS = ("boar", "warthog", "mountaingoat", "deer", "moose")


def backup_suffix():
    """One stamp per run (main computes it once), so a later run never overwrites an earlier run's backup."""
    return BACKUP_PREFIX + dt.datetime.now().strftime("%Y%m%d-%H%M%S")


def resolve_paths(root):
    armory = os.path.join(str(root), "Modules", "LOTRLOME_Armory")
    return {
        "armory": armory,
        "native_sets": os.path.join(str(root), "Modules", "Native", "ModuleData", "action_sets.xml"),
        "clips_dir": os.path.join(armory, "Assets", "creature", "creatures", "animations"),
        "monsters": os.path.join(armory, "ModuleData", "Monsters", "LOTR", "lotr_monster_wildmesh.xml"),
        "submodule": os.path.join(armory, "SubModule.xml"),
        "action_sets": os.path.join(armory, "ModuleData", "action_sets.xml"),
        "horses": os.path.join(armory, "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml"),
        "loc": os.path.join(armory, "ModuleData", "Languages", "loc_LOTRAOM_horses.xml"),
    }

def _clip(animal, stem):
    return "anim_wildmesh_%s_%s" % (animal, stem)


def _gaits(walk, trot, canter, gallop):
    """Each gait and its _stand twin; both gallop feet play the one gallop clip, as for the Animalia animals."""
    out = {}
    for action, clip in (("act_horse_forward_walk", walk), ("act_horse_forward_trot", trot),
                         ("act_horse_forward_canter", canter), ("act_horse_forward_gallop_right_foot", gallop),
                         ("act_horse_forward_gallop_left_foot", gallop)):
        out[action] = clip
        out[action + "_stand"] = clip + "_stand"
    return out


def _rest(animal, stand, idle, riderless, hit_front=None, hit_back=None):
    out = {"act_horse_stand_for_movement_data": _clip(animal, "idle_breathing_movement")}
    for kind, stems in (("stand", stand), ("idle", idle), ("riderless_idle", riderless)):
        for i, stem in enumerate(stems, 1):
            out["act_horse_%s_%d" % (kind, i)] = _clip(animal, stem)
    if hit_front:
        out["act_horse_strike_front"] = _clip(animal, hit_front)
    if hit_back:
        out["act_horse_strike_back"] = _clip(animal, hit_back)
    return out


_PIG_IDLES = dict(stand=["idle_breathing", "idle", "idle_secondarymovecalm_2", "idle_secondarymovecalm_3"],
                  idle=["idle_breathing", "idle_secondarymovecalm_4", "idle_secondarymovecalm_5", "idle"],
                  riderless=["grazingonspota_1", "grazingonspota_2", "grazingonspotb_1", "grazingonspotb_2"],
                  hit_front="hitlegfl2runslow_f", hit_back="hitlegbl2runslow_f")

# horse action -> clip, per animal (the clip plan approved on 2026-10-14, docs/features/wildmesh-mounts.md)
OVERRIDES = {
    "boar": dict(_gaits(_clip("boar", "walkmedium_f"), _clip("boar", "trotfast_f"),
                        _clip("boar", "runslow_f"), _clip("boar", "runfast_f")), **_rest("boar", **_PIG_IDLES)),
    "warthog": dict(_gaits(_clip("warthog", "walkmedium_f"), _clip("warthog", "trotfast_f_2"),
                           _clip("boar", "runslow_f"), _clip("boar", "runfast_f")), **_rest("warthog", **_PIG_IDLES)),
    "mountaingoat": dict(_gaits(_clip("mountaingoat", "walkmedium_f"), _clip("mountaingoat", "trotslow_f_2"),
                                _clip("mountaingoat", "hoppingslow_f"), _clip("deer", "runfast_f")),
                         **_rest("mountaingoat",
                                 stand=["idle_breathing", "idle_secondarymovecalm_1", "idle_secondarymovecalm_2",
                                        "idle_secondarymovecalm_3"],
                                 idle=["idle_breathing", "idle_secondarymovecalm_4", "idle_secondarymovecalm_5", "cue_bleat"],
                                 riderless=["grazingonspota_1", "grazingonspota_2", "grazingonspotb_1", "grazingonspotb_2"],
                                 hit_front="hitinrunhead_f")),
    "deer": dict(_gaits(_clip("deer", "walkmedium_f"), _clip("deer", "trotfast_f"),
                        _clip("deer", "runslow_f"), _clip("deer", "runfast_f")),
                 **_rest("deer", stand=["idle_breathing", "idle_1", "idle_3", "idle_4"],
                         idle=["idle_breathing", "idle", "idle_2", "idle_1"],
                         riderless=["grazingonspotb_1", "grazingonspotb_2", "grazingonspotb_3", "grazingsteps"],
                         hit_front="hitinrunhead_f", hit_back="hitlegbl2runslow_f")),
    "moose": dict(_gaits(_clip("moose", "walkmedium_f"), _clip("moose", "trotslow_f"),
                         _clip("moose", "runslow_f"), _clip("moose", "runfast_f")),
                  **_rest("moose", stand=["idle_breathing", "idle", "grazingstepslookat", "idle_breathing"],
                          idle=["idle_breathing", "idle", "grazingstepslookat", "idle"],
                          riderless=["grazingonspota_1", "grazingonspota_2", "grazingonspotb_1", "grazingonspotb_2"],
                          hit_front="hitlegfl2runslow_f", hit_back="hitlegbl2runslow_f")),
}


def wanted_clips():
    return {clip for overrides in OVERRIDES.values() for clip in overrides.values()}


MONSTER_HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!--
  TAOM WildMesh3D mounts (issue #794, TAOM docs/features/wildmesh-mounts.md), written by
  tools/apply_wildmesh_armory.py.

  Five WildMesh3D "Realistic Animals" reskinned onto the STOCK VANILLA HORSE SKELETON (keepneck profile), each
  the vanilla horse shape (base_monster="horse": Flags, family_type, monster_usage="horse", bones, reins) with
  its own action set, a child of as_horse, playing the pack's clips. Second variants beside the war ram, the
  great elk and the Animalia elk and moose (Mike, 2026-10-14). The dwarf mounts take the war ram's values, the
  deer and moose the Animalia elk's and moose's. Size lives on taom_body_length (Main/Features/MonsterSize).
-->
"""
_RAM_STATS = (("weight", "320"), ("hit_points", "160"), ("jump_acceleration", "7.5"),
              ("relative_speed_limit_for_charge", "4.0"))
# animal -> (Monster attributes after action_set, taom_body_length)
MONSTERS = {
    "boar": (_RAM_STATS, "100"),
    "warthog": (_RAM_STATS, "100"),
    "mountaingoat": (_RAM_STATS, "100"),
    "deer": ((("weight", "500"), ("hit_points", "250")), "100"),
    "moose": ((("weight", "600"), ("hit_points", "300")), "150"),
}


def monster_xml(animal):
    stats, size = MONSTERS[animal]
    rows = ["\t<Monster", '\t\tid="taom_wildmesh_%s"' % animal, '\t\tbase_monster="horse"',
            '\t\taction_set="as_wildmesh_%s"' % animal]
    rows += ['\t\t%s="%s"' % kv for kv in stats] + ['\t\ttaom_body_length="%s" />' % size]
    return "\n".join(rows) + "\n"


def monster_file(animals=None):
    return MONSTER_HEADER + "<Monsters>\n" + "".join(monster_xml(a) for a in (animals or ANIMALS)) + "</Monsters>\n"


MONSTER_FILE = monster_file()

SUBMODULE_TOKEN = 'path="Monsters/LOTR/lotr_monster_wildmesh"'
SUBMODULE_ANCHOR = '<XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_animalia"/>'
SUBMODULE_BLOCK = """
			<!-- #794 WildMesh3D mounts: horse-skeleton reskins with their own action sets
			     (as_wildmesh_<animal> in action_sets.xml). -->
			<XmlNode>
				<XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_wildmesh"/>
				<IncludedGameTypes>
					<GameType value = "Campaign"/>
					<GameType value = "CampaignStoryMode"/>
					<GameType value = "CustomGame"/>
					<GameType value = "EditorGame"/>
				</IncludedGameTypes>
			</XmlNode>"""

_ITEM = """    <Item
        id="taom_wildmesh_%(animal)s_a"
        name="{=taom_wildmesh_%(animal)s_a}%(name)s"
        mesh="wildmesh_%(animal)s"
        culture="Culture.%(culture)s"
        subtype="horse"
        item_category="war_horse"
        value="%(value)s"
        weight="%(weight)s"
        difficulty="0"
        is_merchandise="false"
        Type="Horse">
        <ItemComponent>
            <Horse
                monster="Monster.taom_wildmesh_%(animal)s"
                maneuver="%(maneuver)s"
                speed="%(speed)s"
                charge_damage="%(charge)s"
                body_length="100"
                is_mountable="true"
                extra_health="%(health)s" />
        </ItemComponent>
        <Flags Civilian="true" />
    </Item>
"""
_DWARF = dict(culture="erebor", value="950", weight="300", maneuver="80", speed="55", charge="40", health="20")
ITEM_STATS = (
    dict(_DWARF, animal="boar", name="War Boar"),
    dict(_DWARF, animal="warthog", name="Bristleback Boar"),
    dict(_DWARF, animal="mountaingoat", name="Iron Hills Goat"),
    dict(animal="deer", name="Mirkwood Stag", culture="mirkwood", value="1400", weight="450", maneuver="74",
         speed="62", charge="50", health="20"),
    dict(animal="moose", name="Greenwood Moose", culture="mirkwood", value="1800", weight="550", maneuver="60",
         speed="56", charge="65", health="40"),
)
ITEMS_COMMENT = """    <!-- #794 WildMesh3D mounts (docs/features/wildmesh-mounts.md): horse-skeleton reskins with their own clips.
         Size lives on their Monsters (lotr_monster_wildmesh.xml); body_length="100" is only the placeholder
         Items.xsd requires. The dwarf mounts take the war ram's stats, the deer and moose the Animalia elk's and
         moose's. Not merchandise until a campaign troop rides them. -->
"""
ITEMS = ITEMS_COMMENT + "".join(_ITEM % s for s in ITEM_STATS)

SETS_HEADER = [
    "\t<!-- ============================== WILDMESH3D MOUNTS (#794, 2026-10-14) ============================== -->",
    "\t<!-- Horse-skeleton reskins of the WildMesh3D animals playing their own clips (anim_wildmesh_*, written",
    "\t     by TAOM tools/gen_animalia_anim_clips.ps1 -Pack wildmesh beside the masters in",
    "\t     Assets/creature/creatures/animations). Each action copies the vanilla as_horse action's attributes",
    "\t     and changes only the clip; the warthog's canter and gallop are the boar's clips, the goat's gallop",
    "\t     the deer's. Rebound: the forward and in-place gait rows at paces 1 to 5, the standing pace, the",
    "\t     stands, idles and riderless idles, and the two hit reactions. Everything else stays the horse's: falls,",
    "\t     rear, kick, walking backwards, strafes, turns, quick stops, jumps and the head-turn additives. The",
    "\t     _map twin is required (MobilePartyVisual looks up ActionSetCode + \"_map\"). Written by",
    "\t     tools/apply_wildmesh_armory.py. -->"]
SETS_END = "\t<!-- ============================== /WILDMESH3D MOUNTS ============================== -->"
SETS_WARG = "\t<!-- ============================== /WARG ============================== -->"
TWINS = ("", "_town_and_village", "_map")


def monster_ids(animal):
    return ["taom_wildmesh_" + animal]


def set_ids(animal):
    return ["as_wildmesh_" + animal + twin for twin in TWINS]


def item_ids(animal):
    return ["taom_wildmesh_%s_a" % animal]


def native_vanilla(native_sets_path, problems):
    """The vanilla as_horse actions by type (attributes), or None with the reason added to `problems`."""
    try:
        native = ET.parse(native_sets_path).getroot()
    except ET.ParseError as e:
        problems.append("PARSE FAILED: action_sets.xml (Native): %s" % e)
        return None
    horse = next((s for s in native.iter("action_set") if s.get("id") == "as_horse"), None)
    if horse is None:
        problems.append("Native action_sets.xml: no as_horse action set")
        return None
    return {a.get("type"): a.attrib for a in horse.iter("action")}


def action_set_lines(animals, vanilla):
    """The lines for these animals' three sets, and the overrides that name no as_horse action."""
    lines, missing = [], []
    for animal in animals:
        sid = "as_wildmesh_" + animal
        lines.append('\t<action_set id="%s" skeleton="horse_skeleton" base_set="as_horse">' % sid)
        for atype, clip in OVERRIDES[animal].items():
            if atype not in vanilla:
                missing.append("%s: %s is not an as_horse action" % (sid, atype))
                continue
            attrs = dict(vanilla[atype])
            attrs["animation"] = clip
            ordered = ["type", "animation"] + [k for k in attrs if k not in ("type", "animation")]
            lines.append("\t\t<action " + " ".join('%s="%s"' % (k, attrs[k]) for k in ordered) + " />")
        lines.append("\t</action_set>")
        lines.append('\t<action_set id="%s_town_and_village" skeleton="horse_skeleton" base_set="as_horse_town_and_village" />' % sid)
        lines.append('\t<action_set id="%s_map" skeleton="horse_skeleton" base_set="as_horse_map" />' % sid)
    return lines, missing


def _absent(text, ids_of, what, problems):
    """The animals with none of their ids in `text` (to insert). One with only some of them is a problem."""
    absent = []
    for animal in ANIMALS:
        ids = ids_of(animal)
        have = [i for i in ids if 'id="%s"' % i in text]
        if not have:
            absent.append(animal)
        elif len(have) < len(ids):
            problems.append("%s: %s is only partly present (missing %s); finish or remove it by hand, the tool "
                            "inserts whole animals only" % (what, animal, ", ".join(i for i in ids if i not in have)))
    return absent


def _names(animals):
    return ", ".join(animals)


def plan_monsters(path, report, problems, changed):
    if not os.path.exists(path):
        changed.append((path, MONSTER_FILE))
        report.append("Monsters: new lotr_monster_wildmesh.xml (%s)" % ", ".join("taom_wildmesh_" + a for a in ANIMALS))
        return
    text = read(path)
    k = text.rfind("</Monsters>")
    if k < 0:
        problems.append("lotr_monster_wildmesh.xml: no </Monsters> end tag (an empty or self-closing file); the tool "
                        "created this file and keeps no backup of it, so delete it and re-run to write it whole")
        return
    new = _absent(text, monster_ids, "lotr_monster_wildmesh.xml", problems)
    if not new:
        report.append("Monsters: lotr_monster_wildmesh.xml already holds every animal, left alone")
        return
    block = "".join(monster_xml(a) for a in new).replace("\n", nl_of(text))
    changed.append((path, text[:k] + block + text[k:]))
    report.append("Monsters: lotr_monster_wildmesh.xml gains %s before </Monsters>" % _names(new))


def plan_submodule(path, report, problems, changed):
    sub = read(path)
    if SUBMODULE_TOKEN in sub:
        report.append("SubModule.xml: already registered")
        return
    if sub.count(SUBMODULE_ANCHOR) != 1:
        problems.append("SubModule.xml: anchor %r (the Animalia registration; see docs/reference/lotrlome-animalia-changes.md) "
                        "must occur exactly once, found %d" % (SUBMODULE_ANCHOR, sub.count(SUBMODULE_ANCHOR)))
        return
    j = sub.index("</XmlNode>", sub.index(SUBMODULE_ANCHOR)) + len("</XmlNode>")
    changed.append((path, sub[:j] + SUBMODULE_BLOCK.replace("\n", nl_of(sub)) + sub[j:]))
    report.append("SubModule.xml: Monsters block inserted after the Animalia one")


def plan_action_sets(path, native_path, report, problems, changed):
    sets = read(path)
    nl = nl_of(sets)
    before = len(problems)
    new = _absent(sets, set_ids, "action_sets.xml", problems)
    if not new:
        if len(problems) == before:
            report.append("action_sets.xml: WildMesh sets already present")
        return
    vanilla = native_vanilla(native_path, problems)
    if vanilla is None:
        return
    lines, bad = action_set_lines(new, vanilla)
    problems.extend("NOT AN ACTION: %s" % b for b in bad)
    detail = ", ".join("as_wildmesh_%s (%d actions)" % (a, len(OVERRIDES[a])) for a in new) + " + twins"
    if sets.count(SETS_END) == 1:
        k = sets.index(SETS_END)
        changed.append((path, sets[:k] + nl.join(lines) + nl + sets[k:]))
        report.append("action_sets.xml: " + detail + ", before the /WILDMESH3D MOUNTS end marker")
    elif sets.count(SETS_END) == 0 and len(new) == len(ANIMALS):
        if sets.count(SETS_WARG) != 1:
            problems.append("action_sets.xml: the /WARG marker not found exactly once")
            return
        k = sets.index(SETS_WARG)
        changed.append((path, sets[:k] + nl.join(SETS_HEADER + lines + [SETS_END]) + nl + sets[k:]))
        report.append("action_sets.xml: " + detail + ", before the /WARG marker")
    else:
        problems.append("action_sets.xml: the /WILDMESH3D MOUNTS end marker occurs %d time(s) but %s are missing; "
                        "it must occur exactly once to insert" % (sets.count(SETS_END), _names(new)))


def plan_items(path, report, problems, changed):
    horses = read(path)
    before = len(problems)
    new = _absent(horses, item_ids, "LOTRAOM_horses.xml", problems)
    if not new:
        if len(problems) == before:
            report.append("LOTRAOM_horses.xml: items already present")
        return
    present = list(re.finditer(r'<Item\s+id="taom_wildmesh_[a-z0-9_]+_a".*?</Item>', horses, re.S))
    anchor = present[-1] if present else re.search(r'<Item\s+id="taom_animalia_moose_a".*?</Item>', horses, re.S)
    if not anchor:
        problems.append('LOTRAOM_horses.xml: anchor "taom_animalia_moose_a" not found (the Animalia moose; '
                        'see docs/reference/lotrlome-animalia-changes.md)')
        return
    by_animal = {s["animal"]: s for s in ITEM_STATS}
    block = ("" if present else ITEMS_COMMENT) + "".join(_ITEM % by_animal[a] for a in new)
    nl = nl_of(horses)
    changed.append((path, horses[:anchor.end()] + nl + block.rstrip("\n").replace("\n", nl) + horses[anchor.end():]))
    report.append("LOTRAOM_horses.xml: %d items (%s) after %s" % (
        len(new), ", ".join(item_ids(a)[0] for a in new), "the last WildMesh item" if present else "taom_animalia_moose_a"))


def plan_loc(path, report, problems, changed):
    loc = read(path)
    before = len(problems)
    new = _absent(loc, item_ids, "loc_LOTRAOM_horses.xml", problems)
    if not new:
        if len(problems) == before:
            report.append("loc_LOTRAOM_horses.xml: English rows already present")
        return
    present = list(re.finditer(r'^([ \t]*)<string id="taom_wildmesh_[a-z0-9_]+_a"[^>]*/>', loc, re.M))
    anchor = present[-1] if present else re.search(r'^([ \t]*)<string id="taom_animalia_moose_a"[^>]*/>', loc, re.M)
    if not anchor:
        problems.append('loc_LOTRAOM_horses.xml: anchor row "taom_animalia_moose_a" not found')
        return
    by_animal = {s["animal"]: s for s in ITEM_STATS}
    nl, indent = nl_of(loc), anchor.group(1)
    rows = nl.join('%s<string id="%s" text="%s"/>' % (indent, item_ids(a)[0], escape(by_animal[a]["name"], {'"': "&quot;"}))
                   for a in new)
    changed.append((path, loc[:anchor.end()] + nl + rows + loc[anchor.end():]))
    report.append("loc_LOTRAOM_horses.xml: %d English rows (%s) after %s" % (
        len(new), ", ".join(item_ids(a)[0] for a in new), "the last WildMesh row" if present else "taom_animalia_moose_a"))


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--game-dir", default=None,
                    help="Bannerlord install root (default: $BANNERLORD_GAME_DIR or the desktop path)")
    args = ap.parse_args(argv)
    if args.game_dir is not None and not args.game_dir.strip():
        print("ERROR: --game-dir is empty; pass the Bannerlord install root or leave --game-dir out")
        return 2
    if args.apply and game_or_kit_running():
        print("REFUSED: Bannerlord or the Modding Kit is running; close it and re-run")
        return 2
    paths = resolve_paths(args.game_dir if args.game_dir is not None else game_dir(_DEFAULT_GAME_DIR))
    ensure_exists(paths["armory"], "the LOTRLOME_Armory module")
    for key, what in (("submodule", "the Armory SubModule.xml"), ("action_sets", "the Armory action_sets.xml"),
                      ("horses", "the Armory LOTRAOM_horses.xml"), ("loc", "the English loc_LOTRAOM_horses.xml"),
                      ("native_sets", "the Native action_sets.xml")):
        ensure_exists(paths[key], what)
    suffix = backup_suffix()

    report, problems, changed = [], [], []
    clips = set()
    for _, _, files in os.walk(paths["clips_dir"]):
        clips.update(f[:-len("_anm.tpac")] for f in files if f.endswith("_anm.tpac"))
    for c in sorted(wanted_clips() - clips):
        problems.append("CLIP MISSING: %s" % c)

    plan_monsters(paths["monsters"], report, problems, changed)
    plan_submodule(paths["submodule"], report, problems, changed)
    plan_action_sets(paths["action_sets"], paths["native_sets"], report, problems, changed)
    plan_items(paths["horses"], report, problems, changed)
    plan_loc(paths["loc"], report, problems, changed)

    for path, text in changed:   # parse every document before writing any
        try:
            write(path, text, apply=False, suffix=suffix)
        except ET.ParseError as e:
            problems.append("PARSE FAILED: %s: %s" % (os.path.basename(path), e))
    for line in report:
        print(line)
    if problems:
        for p in problems:
            print(p)
        print("refusing to write: every anchor must be found, every bound clip must exist, every action must be an "
              "as_horse action, every file must parse and no animal may be partly present")
        return 1
    names = ", ".join(os.path.basename(p) for p, _ in changed)
    if not changed:
        print("nothing to write: every step is already present")
    elif args.apply:
        for path, text in changed:
            write(path, text, apply=True, suffix=suffix)
        print("APPLIED %d file(s): %s (backups: *%s)" % (len(changed), names, suffix))
    else:
        print("dry run: %d file(s) would change (%s), all parse; nothing written (--apply to write, Kit and game closed)"
              % (len(changed), names))
    return 0


if __name__ == "__main__":
    sys.exit(main())
