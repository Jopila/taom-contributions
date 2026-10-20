#!/usr/bin/env python3
"""Author the 2026-10-20 Dale lord expansion (one-off): five new Dale clans
(clan_sturgia_10..14) and 27 lords. Companion to tools/author_elf_lords.py (#324).

Party slots follow clan tier (tier 1-2: 1 party, 3-4: 2, 5-6: 3, DefaultClanTierModel), so
more lords alone would not field more hosts; hence new clans, not just new lords. The five
clans add 10 party slots (tiers 4, 4, 3, 3, 3).

Writes, style-matched to the Arthedain block (marker-delimited, so a re-run replaces the
block in place instead of duplicating it):
  - characters/clans.xml   - five <Faction> rows. Culture.sturgia under Kingdom.sturgia, name
                             keys aom_clan_sturgia_N_name (the characters/clans.xml convention;
                             clans 1-9 keep their TAOM_clan_sturgia_N keys in spclans.xslt),
                             party template reused from clan_sturgia_1..9, banner key
                             shared by clans 2-9 (spclans.xslt), colours authored here.
  - characters/lords.xml   - 27 NPCCharacter blocks. Inline <skills> are the live SkillSet
                             values (LordInlineSkillParityTests), <Traits> the generator
                             archetype's, equipment rosters a..h rotated (bowmen on a/b, the
                             only rosters with a bow), face key + weight + build donated from
                             a vanilla Vlandian / Imperial lord that lords.xslt replaces and
                             whose key nothing else uses, of the SAME sex nearest in age, no
                             donor twice (is_female, beard tags and key travel as one unit;
                             women carry no <beard_tags>).
  - characters/heroes.xml  - 27 <Hero> rows (faction, father, mother, spouse).
  - clan_heraldry/dale.json - five colours-only spec entries (no roster: the clans reuse
                             existing templates, so the spec must not claim a roster).

Not done here, by design (separate hand edits): the tier bump of clan_sturgia_8/9 in
spclans.xslt, the live TAOM_Map castle owners (tools/Assign-SettlementOwners.py), the
name-string generator and clan_registry.py.

Finished one-off: the XML is now the source of truth; copy it under a new marker for new work.
It refuses to run unless --rerun-retired is passed, before any read or write.

Usage:
    python tools/oneoff/author_dale_lords.py --rerun-retired            # dry-run (default)
    python tools/oneoff/author_dale_lords.py --rerun-retired --apply
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
    raise SystemExit("author_dale_lords.py is a finished one-off; the XML is the source of truth. "
                     "Pass --rerun-retired to run it anyway.")
MD = REPO / "Main" / "_Module" / "ModuleData"
LORDS = MD / "characters" / "lords.xml"
HEROES = MD / "characters" / "heroes.xml"
CLANS = MD / "characters" / "clans.xml"
LORDS_XSLT = MD / "lords.xslt"
SETS = MD / "taom_lord_skill_sets.xml"
PARTY = MD / "taom_partyTemplates.xml"
SPEC = MD / "clan_heraldry" / "dale.json"
sys.path.insert(0, str(REPO / "tools"))
from _gamedir import ensure_exists, game_dir  # noqa: E402
from apply_culture_skills_traits import BASE_ARCHETYPES, SKILL_ORDER, TRAIT_ORDER  # noqa: E402

GAME = ensure_exists(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))
VANILLA_MD = [GAME / "Modules" / "SandBox" / "ModuleData", GAME / "Modules" / "SandBoxCore" / "ModuleData"]
TAOM_MAP = GAME / "Modules" / "TAOM_Map" / "ModuleData" / "settlements.xml"
# The key clans 2-9 share in spclans.xslt.
DALE_BANNER = "11.312.166.1528.1528.764.764.1.0.0.27000.238.171.900.900.765.790.0.0.0"

BEGIN = "<!-- TAOM-DALE-EXPANSION:BEGIN -->"
END = "<!-- TAOM-DALE-EXPANSION:END -->"
EOL = "\r\n"

# id, English name, tier, owner, home settlement, party template (existing), color, color2, spec theme
CLANS_NEW = [
    ("clan_sturgia_10", "House of the Running River", 4, "lord_S10_1", "castle_S4",
     "kingdom_hero_party_dale_sturgia_1_template", "FF1B4A5E", "FFC9A24A", "dale house 10 - balanced"),
    ("clan_sturgia_11", "House of the Carnen", 4, "lord_S11_1", "castle_S1",
     "kingdom_hero_party_dale_sturgia_4_template", "FF2A3F7A", "FFD8B04F", "dale house 11 - cavalry"),
    ("clan_sturgia_12", "House of Ravenhill", 3, "lord_S12_1", "castle_S3",
     "kingdom_hero_party_dale_sturgia_2_template", "FF3A2F6B", "FFD9C25A", "dale house 12 - infantry"),
    ("clan_sturgia_13", "House of the Forest River", 3, "lord_S13_1", "town_S4",
     "kingdom_hero_party_dale_sturgia_3_template", "FF174A66", "FFE0D6A8", "dale house 13 - ranged"),
    ("clan_sturgia_14", "House of the Long Marshes", 3, "lord_S14_1", "town_S5",
     "kingdom_hero_party_dale_sturgia_6_template", "FF2C4A3E", "FFC9B458", "dale house 14 - skirmisher"),
]

# id, name, sex, age, voice, archetype, default_group, clan, father, mother, spouse
# Spouse is written on the wife's row only. Every parent is at least 14 years older than the child.
L = [
    ("lord_S10_1", "Halvard", "M", 46, "curt", "dale_lord", "Infantry", 10, None, None, None),
    ("lord_S10_2", "Ragnhild", "F", 43, "earnest", "dale_lady", "Infantry", 10, None, None, "lord_S10_1"),
    ("lord_S10_3", "Arnkel", "M", 24, "ironic", "dale_knight", "Cavalry", 10, "lord_S10_1", "lord_S10_2", None),
    ("lord_S10_4", "Torgrim", "M", 38, "curt", "dale_bowman", "Ranged", 10, None, None, None),
    ("lord_S10_5", "Eyvind", "M", 29, "earnest", "dale_bowman", "Ranged", 10, None, None, None),
    ("lord_S10_6", "Sunniva", "F", 20, "softspoken", "dale_young_lady", "Infantry", 10, "lord_S10_1", "lord_S10_2", None),
    ("lord_S11_1", "Ketil", "M", 51, "curt", "dale_knight", "Cavalry", 11, None, None, None),
    ("lord_S11_2", "Gudrun", "F", 48, "earnest", "dale_lady", "Infantry", 11, None, None, "lord_S11_1"),
    ("lord_S11_3", "Orm", "M", 27, "ironic", "dale_knight", "Cavalry", 11, "lord_S11_1", "lord_S11_2", None),
    ("lord_S11_4", "Skuli", "M", 33, "curt", "dale_bowman", "Ranged", 11, None, None, None),
    ("lord_S11_5", "Vigdis", "F", 22, "softspoken", "dale_young_lady", "Infantry", 11, "lord_S11_1", "lord_S11_2", None),
    ("lord_S11_6", "Hrafn", "M", 19, "earnest", "dale_young_lord", "Infantry", 11, "lord_S11_1", "lord_S11_2", None),
    ("lord_S12_1", "Ottar", "M", 44, "earnest", "dale_lord", "Infantry", 12, None, None, None),
    ("lord_S12_2", "Asa", "F", 41, "softspoken", "dale_lady", "Infantry", 12, None, None, "lord_S12_1"),
    ("lord_S12_3", "Geirmund", "M", 22, "ironic", "dale_young_lord", "Infantry", 12, "lord_S12_1", "lord_S12_2", None),
    ("lord_S12_4", "Bjarni", "M", 35, "curt", "dale_bowman", "Ranged", 12, None, None, None),
    ("lord_S12_5", "Kolbein", "M", 30, "earnest", "dale_knight", "Cavalry", 12, None, None, None),
    ("lord_S13_1", "Starkad", "M", 49, "curt", "dale_knight", "Cavalry", 13, None, None, None),
    ("lord_S13_2", "Thora", "F", 46, "earnest", "dale_lady", "Infantry", 13, None, None, "lord_S13_1"),
    ("lord_S13_3", "Audun", "M", 26, "ironic", "dale_bowman", "Ranged", 13, "lord_S13_1", "lord_S13_2", None),
    ("lord_S13_4", "Ingvar", "M", 31, "curt", "dale_bowman", "Ranged", 13, None, None, None),
    ("lord_S13_5", "Solveig", "F", 21, "softspoken", "dale_young_lady", "Infantry", 13, "lord_S13_1", "lord_S13_2", None),
    ("lord_S14_1", "Thormod", "M", 53, "curt", "dale_lord", "Infantry", 14, None, None, None),
    ("lord_S14_2", "Helga", "F", 50, "earnest", "dale_lady", "Infantry", 14, None, None, "lord_S14_1"),
    ("lord_S14_3", "Brynjar", "M", 28, "ironic", "dale_knight", "Cavalry", 14, "lord_S14_1", "lord_S14_2", None),
    ("lord_S14_4", "Valgard", "M", 36, "curt", "dale_bowman", "Ranged", 14, None, None, None),
    ("lord_S14_5", "Yrsa", "F", 23, "softspoken", "dale_young_lady", "Infantry", 14, "lord_S14_1", "lord_S14_2", None),
]
EQ = "abcdefgh"
# Only dale_bat_template_medium_a and _b carry a bow (taom_equipment_sets_dale.xml).
BOW_ROSTERS = "ab"


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
DONOR_CULTURES = ("Culture.vlandia", "Culture.empire")


def key_use_counts(texts):
    """How often each face key occurs in the given texts, upper-cased."""
    counts = Counter()
    for text in texts:
        counts.update(k.upper() for k in HEX_KEY.findall(text))
    return counts


def harvest_donors(xslt_text, vanilla_lords_text, taom_keys, vanilla_keys):
    """Adult vanilla Vlandian / Imperial lords as face donors: (id, sex, age, weight, build, key).

    A donor is a vanilla row that lords.xslt replaces with its own <face> (so the vanilla face never
    loads) and whose key no TAOM file and no other vanilla row uses. BodyProperties may span lines and
    omit weight or build, so the attributes are parsed one by one.
    """
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
        best = min(pool, key=lambda d: (abs(d[2] - age), donors.index(d)))
        used.add(best[0])
        picks[lid] = best
    return picks


def npc_block(row, idx, set_values, donor):
    lid, name, sex, age, voice, arch, group, _clan, *_ = row
    set_id = f"taom_{arch}_skills"
    skills, traits = set_values[set_id], BASE_ARCHETYPES[arch]["traits"]
    _, _, _, weight, build, key = donor
    eq = BOW_ROSTERS[idx % len(BOW_ROSTERS)] if arch == "dale_bowman" else EQ[idx % len(EQ)]
    female = sex == "F"
    lines = [
        f'    <NPCCharacter id="{lid}" name="{{=aom_{lid}_name}}{name}" age="{age}" voice="{voice}"'
        f'{" is_female=" + chr(34) + "true" + chr(34) if female else ""} default_group="{group}" is_hero="true"'
        f' culture="Culture.sturgia" occupation="Lord" face_mesh_cache="true" skill_template="SkillSet.{set_id}">',
        "        <face>",
        f'            <BodyProperties version="4" weight="{weight}" build="{build}" key="{key}" />',
        "            <hair_tags>",
        '                <hair_tag name="sturgia" />',
        "            </hair_tags>",
    ]
    if not female:
        lines += ["            <beard_tags>", '                <beard_tag name="sturgia" />', "            </beard_tags>"]
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
        f'            <EquipmentSet id="dale_bat_template_medium_{eq}" />',
        f'            <EquipmentSet id="dale_civ_template_default_{eq}" equipmentType="Civilian" />',
        "        </Equipments>",
        "    </NPCCharacter>",
    ]
    return EOL.join(lines)


def hero_row(row):
    lid, _n, _s, _a, _v, _arch, _g, clan, father, mother, spouse = row
    lines = ["\t<Hero", f'\t\tid="{lid}"']
    if father:
        lines.append(f'\t\tfather="Hero.{father}"')
    if mother:
        lines.append(f'\t\tmother="Hero.{mother}"')
    if spouse:
        lines.append(f'\t\tspouse="Hero.{spouse}"')
    lines.append(f'\t\tfaction="Faction.clan_sturgia_{clan}" />')
    return EOL.join(lines)


def clan_row(clan):
    cid, name, tier, owner, home, template, c1, c2, *_ = clan
    return EOL.join([
        "  <Faction",
        f'\t\tid="{cid}"',
        f'\t\tinitial_home_settlement="Settlement.{home}"',
        f'\t\tname="{{=aom_{cid}_name}}{name}"',
        f'\t\ttier="{tier}"',
        f'\t\towner="Hero.{owner}"',
        '\t\tculture="Culture.sturgia"',
        '\t\tsuper_faction="Kingdom.sturgia"',
        '\t\tis_noble="true"',
        f'\t\tcolor="{c1}"',
        f'\t\tcolor2="{c2}"',
        f'\t\tdefault_party_template="PartyTemplate.{template}"',
        f'\t\tbanner_key="{DALE_BANNER}" />',
    ])


def with_block(text, items, root_tag):
    """Replace the marker block in place, or insert it before the closing root tag."""
    block = f"  {BEGIN}{EOL}" + EOL.join(items) + f"{EOL}  {END}"
    pat = re.compile(re.escape(f"  {BEGIN}") + r".*?" + re.escape(END), re.S)
    if pat.search(text):
        return pat.sub(lambda m: block, text, count=1)
    close = text.rindex(f"</{root_tag}>")
    return text[:close] + block + EOL + text[close:]


def outside_block(text):
    return re.sub(re.escape(f"  {BEGIN}") + r".*?" + re.escape(END), "", text, flags=re.S)


def spec_text(old_text):
    spec = json.loads(old_text)
    if json.dumps(spec, indent=2).replace("\n", EOL) != old_text:
        raise SystemExit("dale.json does not round-trip through json.dumps(indent=2); refusing to rewrite it")
    have = {c["id"]: c for c in spec["clans"]}
    for cid, _n, _t, _o, _h, _tp, c1, c2, theme in CLANS_NEW:
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

    lords, heroes, clans = read(LORDS), read(HEROES), read(CLANS)
    xslt, party, spec_old = read(LORDS_XSLT), read(PARTY), read(SPEC)
    set_values = load_set_values()

    # --- preconditions: nothing outside our block may already own an id ---
    lo, ho, co = outside_block(lords), outside_block(heroes), outside_block(clans)
    lords_outside = lo
    for row in L:
        assert f'id="{row[0]}"' not in lo and f'id="{row[0]}"' not in ho, f"{row[0]} already defined outside the block"
    for clan in CLANS_NEW:
        assert f'id="{clan[0]}"' not in co, f"{clan[0]} already defined outside the block"
        assert f'<MBPartyTemplate id="{clan[5]}">' in party, f"party template {clan[5]} missing"
        assert clan[3] in [r[0] for r in L], f"owner {clan[3]} is not one of the new lords"
    names = [r[1] for r in L]
    assert len(set(names)) == len(names), "duplicate display name inside the new lords"
    taken = set(re.findall(r'name="\{=[^}]+\}([^"]+)"', lo)) | set(re.findall(r'name">\{=[^}]+\}([^<]+)<', xslt))
    clash = [n for n in names if n in taken]
    assert not clash, f"display names already used by an existing lord: {clash}"

    if TAOM_MAP.exists():
        live = read(TAOM_MAP)
        for clan in CLANS_NEW:
            m = re.search(r'<Settlement\s+id="%s"[^>]*?culture="Culture\.sturgia"' % re.escape(clan[4]), live)
            assert m, f"{clan[4]} is not a Dale settlement in the live TAOM_Map settlements.xml"
    else:
        print("WARNING: live TAOM_Map settlements.xml not found; home settlements not verified")

    # --- build ---
    vanilla_lords = read(VANILLA_MD[0] / "lords.xml")
    taom_texts = [lords_outside if f == LORDS else read(f)
                  for f in sorted(MD.rglob("*")) if f.is_file() and f.suffix.lower() in (".xml", ".xslt", ".json", ".txt")]
    vanilla_texts = [read_loose(f) for root in VANILLA_MD for f in sorted(root.rglob("*.xml"))]
    donors = harvest_donors(xslt, vanilla_lords, key_use_counts(taom_texts), key_use_counts(vanilla_texts))
    print(f"face donors: {sum(d[1] == 'M' for d in donors)} male, {sum(d[1] == 'F' for d in donors)} female")
    picks = pick_donors(donors)
    npcs = [npc_block(r, i, set_values, picks[r[0]]) for i, r in enumerate(L)]
    lords_new = with_block(lords, npcs, "NPCCharacters")
    heroes_new = with_block(heroes, [hero_row(r) for r in L], "Heroes")
    clans_new = with_block(clans, [clan_row(c) for c in CLANS_NEW], "Factions")
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
        print(f"  {r[0]:<11} {r[1]:<9} {r[2]} {r[3]:>2} {r[5]:<16} clan_sturgia_{r[7]} <- {picks[r[0]][0]}")
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
