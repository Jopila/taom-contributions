#!/usr/bin/env python3
"""Author the 2026-10-27 Lindon expansion (one-off, #1039): four new Lindon clans
(clan_lindon_3..6) and 13 lords, one fief each on Mike's new map entities.

Party slots follow clan tier (tier 1-2: 1 party, 3-4: 2, 5-6: 3, DefaultClanTierModel): the four
clans add 8 (tiers 5, 3, 3, 2). Brithombar and Eglarest are Cirdan's lost havens of the Falas;
Galdor is the Elf of the Havens Cirdan sent to the Council of Elrond.

Writes, in the shape of tools/oneoff/author_dale_lords.py (marker-delimited, so a re-run replaces
the block in place instead of duplicating it):
  - characters/clans.xml   - four <Faction> rows. Culture.lindon under Kingdom.lindon, keys
                             aom_clan_lindon_N_name, party template reused from clan_lindon_2,
                             an elven banner per house (BANNERS, gold on light blue; it replaced
                             the clan_gundabad_1 crest the generator cloned, 2026-10-28), colours
                             authored here.
  - characters/lords.xml   - 13 NPCCharacter blocks in the shape of lord_LN1_2 (race elf, battania
                             hair and beard tags, BodyProperties age 22.01; a hero renders at its
                             age= attribute, which Hero.Age is seeded from). Inline <skills> are the live
                             SkillSet values (LordInlineSkillParityTests), <Traits> the archetype's,
                             rivendell rosters a..e rotated. Every one carries Item.charger and no
                             bow, and a hero's formation comes from its battle equipment, not
                             default_group (CharacterObject.GetFormationClass), so all 13 field as
                             Cavalry like the older Lindon lords. Face key + weight + build donated from a vanilla lord
                             that lords.xslt replaces and whose key nothing else uses, of the SAME
                             sex nearest in age, no donor twice; women carry no <beard_tags>.
  - characters/heroes.xml  - 13 <Hero> rows (faction, father, mother, spouse); Galdor carries a
                             description key.
  - clan_heraldry/rivendell.json - four colours-only spec entries (no roster, no template_id: the
                             clans reuse an existing template, so the spec must not claim one).

Not done here (separate edits): clan_lindon_2's home (town_LN2), the live TAOM_Map owners
(tools/add_map_fortifications.py), the name strings and clan_registry.py.

Finished one-off: the XML is now the source of truth, and a re-run would replace the blocks wholesale
and pick faces again from whatever vanilla install is present. It refuses to run unless --rerun-retired
is passed, before any read or write. Copy it under a new marker for new work.

Usage:
    python tools/oneoff/author_lindon_clans.py --rerun-retired            # dry-run (default)
    python tools/oneoff/author_lindon_clans.py --rerun-retired --apply
"""
import argparse
import codecs
import json
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
if "--rerun-retired" not in sys.argv:
    raise SystemExit("author_lindon_clans.py is a finished one-off; the XML is the source of truth. "
                     "Pass --rerun-retired to run it anyway.")
MD = REPO / "Main" / "_Module" / "ModuleData"
LORDS = MD / "characters" / "lords.xml"
HEROES = MD / "characters" / "heroes.xml"
CLANS = MD / "characters" / "clans.xml"
LORDS_XSLT = MD / "lords.xslt"
SETS = MD / "taom_lord_skill_sets.xml"
PARTY = MD / "taom_partyTemplates.xml"
SPEC = MD / "clan_heraldry" / "rivendell.json"
sys.path.insert(0, str(REPO / "tools"))
from _gamedir import ensure_exists, game_dir  # noqa: E402
from _marker_region import replace_region  # noqa: E402
from apply_culture_skills_traits import BASE_ARCHETYPES, SKILL_ORDER, TRAIT_ORDER  # noqa: E402

GAME = ensure_exists(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))
VANILLA_MD = [GAME / "Modules" / "SandBox" / "ModuleData", GAME / "Modules" / "SandBoxCore" / "ModuleData"]
TAOM_MAP = GAME / "Modules" / "TAOM_Map" / "ModuleData" / "settlements.xml"

MARKER = "TAOM-LINDON-EXPANSION"
BEGIN = f"<!-- {MARKER}:BEGIN -->"
END = f"<!-- {MARKER}:END -->"
EOL = "\r\n"
TEMPLATE = "kingdom_hero_party_rivendell_lindon_2_template"

# id, English name, tier, owner, home settlement, color, color2, spec theme
CLANS_NEW = [
    ("clan_lindon_3", "Nos Galdor", 5, "lord_LN3_1", "town_LN3", "FF6F8FA8", "FF1E2F4F", "lindon house 3 - Forlond"),
    ("clan_lindon_4", "Nos Brithombar", 3, "lord_LN4_1", "castle_LN1", "FF8A9AB0", "FF2B3A5C", "lindon house 4 - Ost Falas"),
    ("clan_lindon_5", "Wardens of Ered Luin", 3, "lord_LN5_1", "castle_LN2", "FF5E7FA6", "FF1B2A4A", "lindon house 5 - Barad Luin"),
    ("clan_lindon_6", "Nos Eglarest", 2, "lord_LN6_1", "castle_LN3", "FF7C9CB4", "FF23365A", "lindon house 6 - Tirith Aear"),
]
# One sigil per house from the Noldor and Sindar sheets (banner_icons.xml), gold (171) on light blue (286),
# Mike's recommendation of 2026-10-28; clan_lindon_1 and _2 (outside this block) carry 15000 and 15003 in the same form.
BANNER = "11.286.286.1528.1528.764.764.1.0.0.{icon}.171.171.483.483.764.764.0.0.0"
BANNERS = {"clan_lindon_3": BANNER.format(icon=16004), "clan_lindon_4": BANNER.format(icon=15002),
           "clan_lindon_5": BANNER.format(icon=15004), "clan_lindon_6": BANNER.format(icon=15005)}

# id, name, sex, age, voice, archetype, clan, father, mother, spouse
# Spouse is written on the wife's row only. Every parent is at least 14 years older than the child.
L = [
    ("lord_LN3_1", "Galdor", "M", 48, "earnest", "elf_lord", 3, None, None, None),
    ("lord_LN3_2", "Faelwen", "F", 45, "softspoken", "elf_lady", 3, None, None, "lord_LN3_1"),
    ("lord_LN3_3", "Thalanor", "M", 26, "earnest", "elf_archer", 3, "lord_LN3_1", "lord_LN3_2", None),
    ("lord_LN3_4", "Glîrwen", "F", 22, "softspoken", "elf_lady", 3, "lord_LN3_1", "lord_LN3_2", None),
    ("lord_LN4_1", "Ostaron", "M", 44, "curt", "elf_lord", 4, None, None, None),
    ("lord_LN4_2", "Brethilwen", "F", 41, "earnest", "elf_lady", 4, None, None, "lord_LN4_1"),
    ("lord_LN4_3", "Hîrion", "M", 32, "earnest", "elf_warrior", 4, None, None, None),
    ("lord_LN5_1", "Luinor", "M", 50, "curt", "elf_lord", 5, None, None, None),
    ("lord_LN5_2", "Celebwen", "F", 46, "softspoken", "elf_lady", 5, None, None, "lord_LN5_1"),
    ("lord_LN5_3", "Lothron", "M", 30, "earnest", "elf_archer", 5, None, None, None),
    ("lord_LN6_1", "Aearion", "M", 40, "earnest", "elf_lord", 6, None, None, None),
    ("lord_LN6_2", "Gwaeriel", "F", 38, "softspoken", "elf_lady", 6, None, None, "lord_LN6_1"),
    ("lord_LN6_3", "Rínthal", "M", 28, "curt", "elf_warrior", 6, None, None, None),
]
DESCRIPTIONS = {
    "lord_LN3_1": "Galdor of the Havens, who bore Círdan's counsel to the house of Elrond, keeps Forlond on the "
                  "northern arm of the Gulf of Lhûn.",
}
EQ = "abcde"


def read(path):
    """Idiom B: full binary round-trip, so a BOM survives inside the string and so does every line ending."""
    return path.read_bytes().decode("utf-8")


def read_loose(path):
    """Any vanilla XML, for key counting only: a few game files are UTF-16."""
    raw = path.read_bytes()
    if raw.startswith((codecs.BOM_UTF16_LE, codecs.BOM_UTF16_BE)):
        return raw.decode("utf-16")
    return raw.decode("utf-8", errors="replace")


def load_set_values():
    root = ET.parse(SETS).getroot()
    return {ss.get("id"): {s.get("id"): int(s.get("value")) for s in ss.findall("skill")}
            for ss in root.findall(".//SkillSet")}


HEX_KEY = re.compile(r"[0-9A-Fa-f]{100,}")
# Dale took 27 Vlandian and Imperial donors; Battania and Sturgia widen the pool.
DONOR_CULTURES = ("Culture.battania", "Culture.vlandia", "Culture.empire", "Culture.sturgia")


def key_use_counts(texts):
    counts = Counter()
    for text in texts:
        counts.update(k.upper() for k in HEX_KEY.findall(text))
    return counts


def harvest_donors(xslt_text, vanilla_lords_text, taom_keys, vanilla_keys):
    """Adult vanilla lords as face donors: (id, sex, age, weight, build, key). A donor is a vanilla row
    that lords.xslt replaces with its own <face> and whose key no TAOM file and no other vanilla row uses."""
    xslt = re.sub(r"<!--.*?-->", "", xslt_text, flags=re.S)
    replaced = {m.group(1) for m in re.finditer(
        r"<xsl:template match=\"NPCCharacter\[@id='([^']+)'\]\">(?:(?!</xsl:template>).)*?<BodyProperties",
        xslt, re.S)}
    donors = []
    for node in ET.fromstring(vanilla_lords_text.encode("utf-8")).iter("NPCCharacter"):
        lid, bp = node.get("id"), node.find("face/BodyProperties")
        if node.get("culture") not in DONOR_CULTURES or lid not in replaced or bp is None or not bp.get("key"):
            continue
        age = node.get("age")
        if not age or int(age) < 18:
            continue
        key = bp.get("key").upper()
        own = len(HEX_KEY.findall(ET.tostring(node, encoding="unicode")))
        if taom_keys[key] or vanilla_keys[key] != own:
            continue
        sex = "F" if (node.get("is_female") or "").lower() == "true" else "M"
        donors.append((lid, sex, int(age), bp.get("weight", "0.5"), bp.get("build", "0.5"), bp.get("key")))
    return donors


def pick_donors(donors):
    used, picks = set(), {}
    for lid, _name, sex, age, *_ in L:
        pool = [d for d in donors if d[1] == sex and d[0] not in used]
        assert pool, f"ran out of {sex} face donors at {lid}"
        best = min(pool, key=lambda d: abs(d[2] - age))  # min keeps the first of equal ages, in donor order
        used.add(best[0])
        picks[lid] = best
    return picks


def npc_block(row, idx, set_values, donor):
    lid, name, sex, age, voice, arch, _clan, *_ = row
    set_id = f"taom_{arch}_skills"
    skills, traits = set_values[set_id], BASE_ARCHETYPES[arch]["traits"]
    _, _, _, weight, build, key = donor
    eq = EQ[idx % len(EQ)]
    female = sex == "F"
    lines = [
        f'    <NPCCharacter id="{lid}" race="elf" name="{{=aom_{lid}_name}}{name}" age="{age}" voice="{voice}"'
        f' is_hero="true" is_female="{"true" if female else "false"}" culture="Culture.lindon" occupation="Lord"'
        f' default_group="Infantry" face_mesh_cache="true" skill_template="SkillSet.{set_id}">',
        "        <face>",
        f'            <BodyProperties version="4" age="22.01" weight="{weight}" build="{build}" key="{key}" />',
        "            <hair_tags>",
        '                <hair_tag name="battania" />',
        "            </hair_tags>",
    ]
    if not female:
        lines += ["            <beard_tags>", '                <beard_tag name="battania" />', "            </beard_tags>"]
    lines += [
        "            <tattoo_tags>",
        '                <tattoo_tag name="Cleanface" />',
        "            </tattoo_tags>",
        "        </face>",
        "        <skills>",
    ]
    lines += [f'            <skill id="{s}" value="{skills[s]}" />' for s in SKILL_ORDER]
    lines += ["        </skills>", "        <Traits>"]
    lines += [f'            <Trait id="{t}" value="{traits.get(t, 0)}" />' for t in TRAIT_ORDER]
    lines += [
        "        </Traits>",
        "        <Equipments>",
        f'            <EquipmentSet id="rivendell_bat_template_medium_{eq}" />',
        f'            <EquipmentSet id="rivendell_civ_template_default_{eq}" equipmentType="Civilian" />',
        "        </Equipments>",
        "    </NPCCharacter>",
    ]
    return EOL.join(lines)


def hero_row(row):
    lid, _n, _s, _a, _v, _arch, clan, father, mother, spouse = row
    lines = ["\t<Hero", f'\t\tid="{lid}"']
    if father:
        lines.append(f'\t\tfather="Hero.{father}"')
    if mother:
        lines.append(f'\t\tmother="Hero.{mother}"')
    if spouse:
        lines.append(f'\t\tspouse="Hero.{spouse}"')
    if lid in DESCRIPTIONS:
        lines.append(f'\t\tfaction="Faction.clan_lindon_{clan}"')
        lines.append(f'\t\ttext="{{={lid}_description}}{DESCRIPTIONS[lid]}" />')
    else:
        lines.append(f'\t\tfaction="Faction.clan_lindon_{clan}" />')
    return EOL.join(lines)


def clan_row(clan, banner):
    cid, name, tier, owner, home, c1, c2, _theme = clan
    return EOL.join([
        "  <Faction",
        f'\t\tid="{cid}"',
        f'\t\tinitial_home_settlement="Settlement.{home}"',
        f'\t\tname="{{=aom_{cid}_name}}{name}"',
        f'\t\ttier="{tier}"',
        f'\t\towner="Hero.{owner}"',
        '\t\tculture="Culture.lindon"',
        '\t\tsuper_faction="Kingdom.lindon"',
        '\t\tis_noble="true"',
        f'\t\tcolor="{c1}"',
        f'\t\tcolor2="{c2}"',
        f'\t\tdefault_party_template="PartyTemplate.{TEMPLATE}"',
        f'\t\tbanner_key="{banner}" />',
    ])


def with_block(text, items, root_tag):
    """Replace the marker block in place, or insert it before the closing root tag; refuse a damaged file."""
    block = f"  {BEGIN}{EOL}" + EOL.join(items) + f"{EOL}  {END}{EOL}"
    return replace_region(text, MARKER, block, f"</{root_tag}>")


def outside_block(text):
    return re.sub(re.escape(f"  {BEGIN}") + r".*?" + re.escape(END), "", text, flags=re.S)


def spec_text(old_text):
    spec = json.loads(old_text)
    if json.dumps(spec, indent=2).replace("\n", EOL) != old_text:
        raise SystemExit("rivendell.json does not round-trip through json.dumps(indent=2); refusing to rewrite it")
    have = {c["id"]: c for c in spec["clans"]}
    for cid, _n, _t, _o, _h, c1, c2, theme in CLANS_NEW:
        entry = {"id": cid, "source": "xml", "theme": theme, "color": c1, "color2": c2, "roster": []}
        if cid in have:
            have[cid].update(entry)
        else:
            spec["clans"].append(entry)
    return json.dumps(spec, indent=2).replace("\n", EOL)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rerun-retired", action="store_true", help="required: this one-off is finished")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    lords, heroes, clans = read(LORDS), read(HEROES), read(CLANS)
    xslt, party, spec_old = read(LORDS_XSLT), read(PARTY), read(SPEC)
    set_values = load_set_values()

    # --- preconditions: nothing outside our block may already own an id ---
    lo, ho, co = outside_block(lords), outside_block(heroes), outside_block(clans)
    for row in L:
        assert f'id="{row[0]}"' not in lo and f'id="{row[0]}"' not in ho, f"{row[0]} already defined outside the block"
    for clan in CLANS_NEW:
        assert f'id="{clan[0]}"' not in co, f"{clan[0]} already defined outside the block"
        assert clan[3] in [r[0] for r in L], f"owner {clan[3]} is not one of the new lords"
    assert f'<MBPartyTemplate id="{TEMPLATE}">' in party, f"party template {TEMPLATE} missing"
    names = [r[1] for r in L]
    assert len(set(names)) == len(names), "duplicate display name inside the new lords"
    taken = set(re.findall(r'name="\{=[^}]+\}([^"]+)"', lo)) | set(re.findall(r'name">\{=[^}]+\}([^<]+)<', xslt))
    clash = [n for n in names if n in taken]
    assert not clash, f"display names already used by an existing lord: {clash}"
    spclans = read(MD / "spclans.xslt")
    clan_taken = set(re.findall(r'name="\{=[^}]+\}([^"]+)"', co)) | set(re.findall(r'name">\{=[^}]+\}([^<]+)<', spclans))
    clash = [c[1] for c in CLANS_NEW if c[1] in clan_taken]
    assert not clash, f"clan names already used by an existing clan: {clash}"

    live = read(TAOM_MAP)
    for clan in CLANS_NEW:
        m = re.search(r'<Settlement\s+id="%s"[^>]*?culture="Culture\.lindon"' % re.escape(clan[4]), live)
        assert m, f"{clan[4]} is not a Lindon settlement in the live TAOM_Map settlements.xml"

    # --- build ---
    vanilla_lords = read(VANILLA_MD[0] / "lords.xml")
    taom_texts = [lo if f == LORDS else read(f)
                  for f in sorted(MD.rglob("*")) if f.is_file() and f.suffix.lower() in (".xml", ".xslt", ".json", ".txt")]
    vanilla_texts = [read_loose(f) for root in VANILLA_MD for f in sorted(root.rglob("*.xml"))]
    donors = harvest_donors(xslt, vanilla_lords, key_use_counts(taom_texts), key_use_counts(vanilla_texts))
    print(f"face donors: {sum(d[1] == 'M' for d in donors)} male, {sum(d[1] == 'F' for d in donors)} female")
    picks = pick_donors(donors)
    npcs = [npc_block(r, i, set_values, picks[r[0]]) for i, r in enumerate(L)]
    lords_new = with_block(lords, npcs, "NPCCharacters")
    heroes_new = with_block(heroes, [hero_row(r) for r in L], "Heroes")
    clans_new = with_block(clans, [clan_row(c, BANNERS[c[0]]) for c in CLANS_NEW], "Factions")
    spec_new = spec_text(spec_old)

    for label, txt in (("lords", lords_new), ("heroes", heroes_new), ("clans", clans_new)):
        ET.fromstring(txt.encode("utf-8"))
        print(f"{label}.xml: well-formed")
    json.loads(spec_new)

    outputs = [(LORDS, lords, lords_new), (HEROES, heroes, heroes_new), (CLANS, clans, clans_new), (SPEC, spec_old, spec_new)]
    changed = [p for p, old, new in outputs if old != new]
    for p, old, new in outputs:
        print(f"{p.relative_to(REPO)}: {'WOULD CHANGE' if old != new else 'no change'}")
    for r in L:
        print(f"  {r[0]:<11} {r[1]:<11} {r[2]} {r[3]:>2} {r[5]:<12} clan_lindon_{r[6]} <- {picks[r[0]][0]}")
    print(f"lords: {len(L)}; clans: {len(CLANS_NEW)}")
    if not args.apply:
        print("(dry-run, pass --apply to write)" if changed else "(dry-run, nothing to do)")
        return 0
    for p, old, new in outputs:
        if old != new:
            p.write_bytes(new.encode("utf-8"))
    print("WROTE: " + ", ".join(p.name for p in changed) if changed else "nothing to write")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
