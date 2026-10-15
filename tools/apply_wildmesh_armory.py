"""Write the WildMesh3D mounts into the LIVE LOTRLOME_Armory (#794): Monsters, action sets, Horse items.

    python tools/apply_wildmesh_armory.py            # dry run: prints what would change, writes nothing
    python tools/apply_wildmesh_armory.py --apply    # Modding Kit and game CLOSED

Sibling of tools/apply_animalia_armory.py (#646), whose I/O helpers it imports, for the five WildMesh3D
animals (docs/features/wildmesh-mounts.md): the War Boar, Bristleback Boar and Iron Hills Goat for the Dwarves,
the Mirkwood Stag and Greenwood Moose for the Elves. The Armory is unversioned, so every edit here is recorded
in docs/reference/lotrlome-wildmesh-changes.md.

  1. ModuleData/Monsters/LOTR/lotr_monster_wildmesh.xml (new): taom_wildmesh_<animal>, base_monster="horse",
     action_set as_wildmesh_<animal>, taom_body_length (Mike, 2026-10-14: 100, the moose 150). The dwarf mounts
     copy the war ram's Monster values (weight, hit points, jump acceleration, charge speed limit); the deer and
     moose copy the Animalia elk's and moose's.
  2. SubModule.xml: one Monsters registration block after the Animalia one.
  3. ModuleData/action_sets.xml: as_wildmesh_<animal> (base as_horse) with its _town_and_village and _map twins,
     before the /WARG marker. Each override copies the vanilla as_horse action's attributes and changes only the
     clip (tools/gen_animalia_anim_clips.ps1 -Pack wildmesh wrote them). The warthog's canter and gallop bind the
     boar's clips and the goat's gallop the deer's (the pack's own versions do not loop or curve sideways).
     Falls, rear, kick, walking backwards, turns and jumps stay the horse's; no attack yet (stage 12).
  4. ModuleData/LOTRLOME_items/LOTRAOM_horses.xml: taom_wildmesh_<animal>_a after taom_animalia_moose_a, the
     body_length="100" placeholder Items.xsd requires (TAOM copies the Monster's size in at game init), not
     merchandise while only the Custom Battle test riders ride them.

Byte-faithful (tools/README.md "XML I/O convention"): BOM and line endings kept, every changed file parsed before
anything is written, write-once backups (non-.xml suffix) beside each edited file. Idempotent; a step whose output
is partly present is refused, never merged. Every bound clip must exist under Assets/creature/creatures/animations.
Refuses --apply while the game or the Kit runs. --game-dir points the whole script at a synthetic tree
(tools/tests/test_apply_wildmesh_armory.py).
"""
import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import ensure_exists, game_dir, game_or_kit_running  # noqa: E402
from apply_animalia_armory import nl_of, read, write  # noqa: E402

_DEFAULT_GAME_DIR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
BACKUP_SUFFIX = ".bak-wildmesh-20261014"
ANIMALS = ("boar", "warthog", "mountaingoat", "deer", "moose")


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


_RAM = 'weight="320"\n\t\thit_points="160"\n\t\tjump_acceleration="7.5"\n\t\trelative_speed_limit_for_charge="4.0"'
MONSTER_FILE = """<?xml version="1.0" encoding="utf-8"?>
<!--
  TAOM WildMesh3D mounts (issue #794, TAOM docs/features/wildmesh-mounts.md), written by
  tools/apply_wildmesh_armory.py.

  Five WildMesh3D "Realistic Animals" reskinned onto the STOCK VANILLA HORSE SKELETON (keepneck profile), each
  the vanilla horse shape (base_monster="horse": Flags, family_type, monster_usage="horse", bones, reins) with
  its own action set, a child of as_horse, playing the pack's clips. Second variants beside the war ram, the
  great elk and the Animalia elk and moose (Mike, 2026-10-14). The dwarf mounts take the war ram's values, the
  deer and moose the Animalia elk's and moose's. Size lives on taom_body_length (Main/Features/MonsterSize).
-->
<Monsters>
	<Monster
		id="taom_wildmesh_boar"
		base_monster="horse"
		action_set="as_wildmesh_boar"
		%(ram)s
		taom_body_length="100" />
	<Monster
		id="taom_wildmesh_warthog"
		base_monster="horse"
		action_set="as_wildmesh_warthog"
		%(ram)s
		taom_body_length="100" />
	<Monster
		id="taom_wildmesh_mountaingoat"
		base_monster="horse"
		action_set="as_wildmesh_mountaingoat"
		%(ram)s
		taom_body_length="100" />
	<Monster
		id="taom_wildmesh_deer"
		base_monster="horse"
		action_set="as_wildmesh_deer"
		weight="500"
		hit_points="250"
		taom_body_length="100" />
	<Monster
		id="taom_wildmesh_moose"
		base_monster="horse"
		action_set="as_wildmesh_moose"
		weight="600"
		hit_points="300"
		taom_body_length="150" />
</Monsters>
""" % {"ram": _RAM}

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
ITEMS = ("""    <!-- #794 WildMesh3D mounts (docs/features/wildmesh-mounts.md): horse-skeleton reskins with their own clips.
         Size lives on their Monsters (lotr_monster_wildmesh.xml); body_length="100" is only the placeholder
         Items.xsd requires. The dwarf mounts take the war ram's stats, the deer and moose the Animalia elk's and
         moose's. Not merchandise while only the Custom Battle test riders ride them. -->
""" + "".join(_ITEM % s for s in ITEM_STATS))

MONSTER_IDS = tuple("taom_wildmesh_" + a for a in ANIMALS)
ITEM_IDS = tuple("taom_wildmesh_%s_a" % a for a in ANIMALS)
SET_IDS = tuple("as_wildmesh_%s%s" % (a, twin) for a in ANIMALS for twin in ("", "_town_and_village", "_map"))


def action_sets_block(nl, native_sets_path):
    native = ET.parse(native_sets_path).getroot()
    horse = next(s for s in native.iter("action_set") if s.get("id") == "as_horse")
    vanilla = {a.get("type"): a.attrib for a in horse.iter("action")}
    lines = ["\t<!-- ============================== WILDMESH3D MOUNTS (#794, 2026-10-14) ============================== -->",
             "\t<!-- Horse-skeleton reskins of the WildMesh3D animals playing their own clips (anim_wildmesh_*, written",
             "\t     by TAOM tools/gen_animalia_anim_clips.ps1 -Pack wildmesh beside the masters in",
             "\t     Assets/creature/creatures/animations). Each action copies the vanilla as_horse action's attributes",
             "\t     and changes only the clip; the warthog's canter and gallop are the boar's clips, the goat's gallop",
             "\t     the deer's. Falls, rear, kick, walking backwards, turns and jumps stay the horse's. The _map twin",
             "\t     is required (MobilePartyVisual looks up ActionSetCode + \"_map\"). Written by",
             "\t     tools/apply_wildmesh_armory.py. -->"]
    missing = []
    for animal in ANIMALS:
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
    lines.append("\t<!-- ============================== /WILDMESH3D MOUNTS ============================== -->")
    return nl.join(lines), missing


def _presence(text, ids, what, problems):
    """'all', 'none', or a problem naming the missing ids when only some are present."""
    present = [i for i in ids if 'id="%s"' % i in text]
    if len(present) == len(ids):
        return "all"
    if present:
        problems.append("%s: only part of the WildMesh block is present (missing %s); restore the file from its "
                        "backup rather than inserting a second block" % (what, ", ".join(i for i in ids if i not in present)))
        return "part"
    return "none"


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

    report, problems, changed = [], [], []
    clips = set()
    for _, _, files in os.walk(paths["clips_dir"]):
        clips.update(f[:-len("_anm.tpac")] for f in files if f.endswith("_anm.tpac"))
    for c in sorted(wanted_clips() - clips):
        problems.append("CLIP MISSING: %s" % c)

    # 1. Monsters file
    mon = paths["monsters"]
    if os.path.exists(mon):
        if _presence(read(mon), MONSTER_IDS, "lotr_monster_wildmesh.xml", problems) == "all":
            report.append("Monsters: lotr_monster_wildmesh.xml already present, left alone")
    else:
        changed.append((mon, MONSTER_FILE))
        report.append("Monsters: new lotr_monster_wildmesh.xml (%s)" % ", ".join(MONSTER_IDS))

    # 2. SubModule.xml
    sub = read(paths["submodule"])
    if "Monsters/LOTR/lotr_monster_wildmesh" in sub:
        report.append("SubModule.xml: already registered")
    else:
        anchor = '<XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_animalia"/>'
        i = sub.find(anchor)
        if i < 0:
            problems.append("SubModule.xml: anchor %r not found (the Animalia registration; "
                            "see docs/reference/lotrlome-animalia-changes.md)" % anchor)
        else:
            j = sub.index("</XmlNode>", i) + len("</XmlNode>")
            changed.append((paths["submodule"], sub[:j] + SUBMODULE_BLOCK.replace("\n", nl_of(sub)) + sub[j:]))
            report.append("SubModule.xml: Monsters block inserted after the Animalia one")

    # 3. action_sets.xml
    sets = read(paths["action_sets"])
    state = _presence(sets, SET_IDS, "action_sets.xml", problems)
    if state == "all":
        report.append("action_sets.xml: WildMesh sets already present")
    elif state == "none":
        block, bad = action_sets_block(nl_of(sets), paths["native_sets"])
        problems.extend("NOT AN ACTION: %s" % b for b in bad)
        anchor = "\t<!-- ============================== /WARG ============================== -->"
        if sets.count(anchor) != 1:
            problems.append("action_sets.xml: the /WARG marker not found exactly once")
        else:
            k = sets.index(anchor)
            changed.append((paths["action_sets"], sets[:k] + block + nl_of(sets) + sets[k:]))
            report.append("action_sets.xml: " + ", ".join("as_wildmesh_%s (%d actions)" % (a, len(OVERRIDES[a]))
                                                          for a in ANIMALS) + " + twins, before the /WARG marker")

    # 4. horses
    horses = read(paths["horses"])
    state = _presence(horses, ITEM_IDS, "LOTRAOM_horses.xml", problems)
    if state == "all":
        report.append("LOTRAOM_horses.xml: items already present")
    elif state == "none":
        m = re.search(r'<Item\s+id="taom_animalia_moose_a".*?</Item>', horses, re.S)
        if not m:
            problems.append('LOTRAOM_horses.xml: anchor "taom_animalia_moose_a" not found (the Animalia moose; '
                            'see docs/reference/lotrlome-animalia-changes.md)')
        else:
            nl = nl_of(horses)
            changed.append((paths["horses"], horses[:m.end()] + nl + ITEMS.rstrip("\n").replace("\n", nl) + horses[m.end():]))
            report.append("LOTRAOM_horses.xml: 5 items (%s) after taom_animalia_moose_a" % ", ".join(ITEM_IDS))

    for line in report:
        print(line)
    if problems:
        for p in problems:
            print(p)
        print("refusing to write: every anchor must be found, every bound clip must exist, and every action must be an as_horse action")
        return 1
    for path, text in changed:
        write(path, text, apply=False, suffix=BACKUP_SUFFIX)   # parse every document before writing any
    names = ", ".join(os.path.basename(p) for p, _ in changed)
    if not changed:
        print("nothing to write: every step is already present")
    elif args.apply:
        for path, text in changed:
            write(path, text, apply=True, suffix=BACKUP_SUFFIX)
        print("APPLIED %d file(s): %s (backups: *%s)" % (len(changed), names, BACKUP_SUFFIX))
    else:
        print("dry run: %d file(s) would change (%s), all parse; nothing written (--apply to write, Kit and game closed)"
              % (len(changed), names))
    return 0


if __name__ == "__main__":
    sys.exit(main())
