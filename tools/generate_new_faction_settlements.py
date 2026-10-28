#!/usr/bin/env python3
"""Generate settlements.xml <Settlement> blocks for the three new TAOM kingdoms.

Source of truth: tools/taom_new_factions_layout.json (built from the live map scene
positions the user placed in TAOM_Map/SceneObj/Main_map/scene.xscene).

The new settlements reuse vanilla interior/menu scenes by culture family:
  * orc cultures (mistymountainorcs, goblin)  -> sturgia_* scenes (same as gundabad)
  * rivendell (Lindon)                          -> battania_* scenes (same as Rivendell)

Appends ONLY the layout ids the LIVE external file lacks, at the end of the file (the save-order rule
in tools/add_map_villages.append_settlements):
  E:/Steam/.../Modules/TAOM_Map/ModuleData/settlements.xml
A row already on the map is never stripped or rewritten. All 49 layout ids have shipped since June
2026 and have changed since (the Lindon batch, #1039; renames; the economy floors), so until
2026-10-27 (#1042) this tool's strip-and-reinsert would have reverted them to the values below. A run on
today's map does nothing. A row it does add carries the first-pass prosperity (3000 town, 600
castle) and hearth (300), so run python tools/validate_moduledata.py afterwards
(SETTLEMENT_ECONOMY_FLOOR) and raise it to the floor. Byte discipline as in add_map_villages.py:
binary read, BOM and newline kept, the output parsed before the write, a non-.xml backup first.
New settlements for these kingdoms go through tools/add_map_fortifications.py and add_map_villages.py.

Usage:
  python tools/generate_new_faction_settlements.py            # dry run: lists the ids it would add
  python tools/generate_new_faction_settlements.py --apply    # appends them to the live file (+ backup)
"""
import argparse, datetime, json, os, re, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from assign_orc_village_types import fief_types, ORC_CULTURES  # per-fief orc village-type rule
from _gamedir import game_dir
import add_map_villages as amv  # noqa: E402  _read, _write, detect_newline, append_settlements

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LAYOUT = os.path.join(ROOT, "tools", "taom_new_factions_layout.json")
# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME = game_dir(r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord")
LIVE = GAME + r"/Modules/TAOM_Map/ModuleData/settlements.xml"

# ---- Curated display names (placeholders, lore-flavoured; user can rename freely) ----
NAMES = {
    # Misty Mountain Orcs — Moria + Misty Mountains lore (user-specified towns; castles = peaks/gates)
    "town_MM1": "Western Moria", "town_MM2": "Eastern Moria", "town_MM3": "Nanduhirion",
    "castle_MM1": "Caradhras", "castle_MM2": "Celebdil", "castle_MM3": "Fanuidhol",
    "castle_MM4": "Redhorn Gate", "castle_MM5": "Methedras", "castle_MM6": "Hollin Gate", "castle_MM7": "Sirannon",
    # Goblins (Goblin Town)
    "town_GT1": "Goblin Town",
    # Goblins of Blue Craig (capital; scene entity not yet placed — user will place + relocate)
    "town_GBC1": "Blue Craig",
    # Lindon (Grey Havens, Sindarin)
    "town_LN1": "Mithlond",
}
# Sindarin-ish names for Lindon villages
LN_VILLAGE_NAMES = {"village_LN1_1": "Eryn Lhûn", "village_LN1_2": "Harlindon",
                    "village_LN1_3": "Ninglas", "village_LN1_4": "Lhûnbar"}

# Mountain/orc village types cycle; elven village types for Lindon.
ORC_VTYPES = ["iron_mine", "silver_mine", "lumberjack", "swine_farm", "cattle_farm", "clay_mine"]  # unused; per-fief rule drives types
ELF_VTYPES = ["lumberjack", "fisherman", "wheat_farm", "vineyard"]  # order matches placed Lindon villages

# Generated orc village names (stable, index-seeded so re-runs are identical).
ORC_SYL_A = ["Grish", "Skar", "Mau", "Dush", "Krimp", "Nar", "Var", "Mor", "Ush", "Gak", "Bag", "Snag", "Lug", "Rad", "Throk", "Gor", "Hrak", "Mok", "Zog", "Brak", "Gund", "Shôrd", "Düglar", "Mazûg"]
ORC_SYL_B = ["bosh", "gûl", "nakh", "goth", "bûr", "tang", "mauz", "zâr", "mîr", "bash", "krish", "glob", "salth", "thrak", "snar", "dum", "gash", "rod", "ghâsh", "morn"]


def orc_village_name(seed):
    a = ORC_SYL_A[seed % len(ORC_SYL_A)]
    b = ORC_SYL_B[(seed * 7 + 3) % len(ORC_SYL_B)]
    return f"{a}-{b}"


def disp_name(sid, idx):
    if sid in NAMES:
        return NAMES[sid]
    if sid in LN_VILLAGE_NAMES:
        return LN_VILLAGE_NAMES[sid]
    # generated orc village name
    return orc_village_name(idx)


def scene_family(culture):
    return "battania" if culture == "rivendell" else "sturgia"


def shipped_culture(s):
    """The culture the row's kingdom has now: the layout predates promote_borrowed_cultures.py."""
    if s["culture"] == "rivendell":
        return "lindon"
    if "_GBC" in s["id"]:
        return "bluecraig"
    return s["culture"]


def town_block(s, idx, is_castle):
    cul = s["culture"]; fam = scene_family(cul); sid = s["id"]
    name = disp_name(sid, idx)
    gx = round(s["posX"], 4); gy = round(s["posY"], 4)
    owner = s["owner"]
    if is_castle:
        comp_id = f"castle_comp_{sid.split('_',1)[1]}"
        bg = f"gui_bg_castle_{fam}"; prosperity = 600; grot = "0.908"
        if fam == "battania":
            center = "battania_castle_002"
        else:
            center = "sturgia_castle_siege_001"
        buildings = [
            ("building_castle_fortifications", 1), ("building_castle_barracks", 1),
            ("building_castle_training_fields", 1), ("building_castle_guard_house", 0),
            ("building_castle_siege_workshop", 0), ("building_castle_castallans_office", 1),
            ("building_castle_granary", 1), ("building_castle_craftmans_quarters", 0),
            ("building_castle_farmlands", 1), ("building_castle_mason", 0),
            ("building_castle_roads_and_paths", 1),
        ]
        locations = (
            f'      <Location id="center" scene_name="{center}" scene_name_1="{center}" scene_name_2="{center}" scene_name_3="{center}" />\n'
            f'      <Location id="lordshall" scene_name_1="{fam}_castle_keep_a_l1_interior" scene_name_2="{fam}_castle_keep_a_l2_interior" scene_name_3="{fam}_castle_keep_a_l3_interior" />\n'
            f'      <Location id="prison" scene_name="{fam}_dungeon_stealth" />\n'
        )
        loc_template = "LocationComplexTemplate.castle_complex"
        common = ""
    else:
        comp_id = f"town_comp_{sid.split('_',1)[1]}"
        bg = f"gui_bg_town_{fam}"; prosperity = 3000; grot = "0.378"
        center = "battania_town_d" if fam == "battania" else "sturgia_town_f"
        arena = f"arena_{fam}_a"
        tavern = "battania_town_house_a_interior_a_tavern" if fam == "battania" else "sturgia_house_b_interior_tavern"
        house = "battania_town_house_b_interior_b_house" if fam == "battania" else "sturgia_town_house_d1_interior_b_house"
        buildings = [
            ("building_settlement_fortifications", 1), ("building_settlement_barracks", 1),
            ("building_settlement_training_fields", 1), ("building_settlement_guard_house", 0),
            ("building_settlement_siege_workshop", 0), ("building_settlement_tax_office", 1),
            ("building_settlement_marketplace", 1), ("building_settlement_warehouse", 0),
            ("building_settlement_mason", 1), ("building_settlement_waterworks", 0),
            ("building_settlement_courthouse", 0), ("building_settlement_roads_and_paths", 1),
        ]
        locations = (
            f'      <Location id="center" scene_name="{center}" scene_name_1="{center}" scene_name_2="{center}" scene_name_3="{center}" />\n'
            f'      <Location id="arena" scene_name="{arena}" />\n'
            f'      <Location id="tavern" scene_name="{tavern}" />\n'
            f'      <Location id="lordshall" scene_name_1="{fam}_castle_keep_a_l1_interior" scene_name_2="{fam}_castle_keep_a_l2_interior" scene_name_3="{fam}_castle_keep_a_l3_interior" />\n'
            f'      <Location id="prison" scene_name="{fam}_dungeon_stealth" />\n'
            f'      <Location id="house_1" scene_name="{house}" />\n'
            f'      <Location id="house_2" scene_name="{house}" />\n'
            f'      <Location id="house_3" scene_name="{house}" />\n'
            f'      <Location id="alley" />\n'
        )
        loc_template = "LocationComplexTemplate.town_complex"
        common = (
            '    <CommonAreas>\n'
            '      <Area type="Backstreet" name="{=a0MVffcN}Backstreet" />\n'
            '      <Area type="Clearing" name="{=LWHIVshb}Clearing" />\n'
            '      <Area type="Waterfront" name="{=Rr1cy5Sk}Waterfront" />\n'
            '    </CommonAreas>\n'
        )
    bld = "\n".join(f'          <Building id="{b}" level="{lv}" />' for b, lv in buildings)
    is_castle_attr = "true" if is_castle else "false"
    return (
        f'  <Settlement id="{sid}" name="{{=Settlements.Settlement.name.{sid}}}{name}" owner="Faction.{owner}" '
        f'posX="{s["posX"]}" posY="{s["posY"]}" culture="Culture.{shipped_culture(s)}" gate_posX="{gx}" gate_posY="{gy}">\n'
        f'    <Components>\n'
        f'      <Town id="{comp_id}" is_castle="{is_castle_attr}" background_crop_position="0.0" background_mesh="{bg}" wait_mesh="wait_{fam}_town" gate_rotation="{grot}" prosperity="{prosperity}">\n'
        f'        <Buildings>\n{bld}\n        </Buildings>\n'
        f'      </Town>\n'
        f'    </Components>\n'
        f'    <Locations complex_template="{loc_template}">\n{locations}    </Locations>\n'
        f'{common}'
        f'  </Settlement>\n'
    )


def village_block(s, idx, vtype):
    cul = s["culture"]; fam = scene_family(cul); sid = s["id"]
    name = disp_name(sid, idx)
    comp_id = f"village_comp_{sid.split('_',1)[1]}"
    center = f"{fam}_village_m" if fam == "battania" else f"{fam}_village_f"
    return (
        f'  <Settlement id="{sid}" name="{{=Settlements.Settlement.name.{sid}}}{name}" '
        f'posX="{s["posX"]}" posY="{s["posY"]}" culture="Culture.{shipped_culture(s)}">\n'
        f'    <Components>\n'
        f'      <Village id="{comp_id}" village_type="VillageType.{vtype}" hearth="300" bound="Settlement.{s["bound"]}" '
        f'background_crop_position="0.0" background_mesh="gui_bg_village_{fam}" wait_mesh="wait_{fam}_village" castle_background_mesh="gui_bg_castle_{fam}" />\n'
        f'    </Components>\n'
        f'    <Locations complex_template="LocationComplexTemplate.village_complex">\n'
        f'      <Location id="village_center" scene_name="{center}" />\n'
        f'    </Locations>\n'
        f'    <CommonAreas>\n'
        f'      <Area type="Pasture" name="{{=fOUsLdZR}}Pasture" />\n'
        f'      <Area type="Thicket" name="{{=66Mzk0NZ}}Thicket" />\n'
        f'      <Area type="Bog" name="{{=iXA5SttU}}Bog" />\n'
        f'    </CommonAreas>\n'
        f'  </Settlement>\n'
    )


def _village_type_map(setts):
    """Orc/goblin villages: per-fief rule (mostly animal + 1 iron/silver mine; 4+ adds lumberjack),
    matching tools/assign_orc_village_types.py. Rivendell (Lindon) villages: elven cycle."""
    from collections import defaultdict
    vmap = {}
    orc_fiefs = defaultdict(list)
    elf_idx = 0
    for s in setts:
        if s["type"] != "village":
            continue
        if s["culture"] in ORC_CULTURES:
            orc_fiefs[s["bound"]].append(s["id"])
        else:
            vmap[s["id"]] = ELF_VTYPES[elf_idx % len(ELF_VTYPES)]; elf_idx += 1
    for fi, fief in enumerate(sorted(orc_fiefs)):
        vids = sorted(orc_fiefs[fief], key=lambda s: [int(x) if x.isdigit() else x for x in re.split(r'(\d+)', s)])
        mine = "iron_mine" if fi % 2 == 0 else "silver_mine"
        for vid, vt in zip(vids, fief_types(len(vids), mine)):
            vmap[vid] = vt
    return vmap


def build_blocks(layout):
    blocks = []
    vmap = _village_type_map(layout["settlements"])
    order = {"town": 0, "castle": 1, "village": 2}
    setts = sorted(layout["settlements"], key=lambda s: (order[s["type"]], s["id"]))
    for idx, s in enumerate(setts):
        if s["type"] == "town":
            blocks.append((s["id"], town_block(s, idx, is_castle=False)))
        elif s["type"] == "castle":
            blocks.append((s["id"], town_block(s, idx, is_castle=True)))
        else:
            blocks.append((s["id"], village_block(s, idx, vmap[s["id"]])))
    return blocks


def missing_blocks(layout, master_text):
    """[(id, block)] for the layout ids with no <Settlement> row in `master_text`, in build order."""
    return [(sid, block) for sid, block in build_blocks(layout)
            if not re.search(r'<Settlement id="' + re.escape(sid) + r'"', master_text)]


def append_blocks(master_text, blocks):
    """`master_text` with the blocks appended at the end, in the file's own newline."""
    nl = amv.detect_newline(master_text)
    return amv.append_settlements(master_text, [block.replace("\n", nl) for _, block in blocks])


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    layout = json.load(open(LAYOUT, encoding="utf-8"))
    if not os.path.exists(LIVE):
        sys.exit(f"LIVE settlements.xml not found: {LIVE}")
    had_bom, src = amv._read(LIVE)
    blocks = missing_blocks(layout, src)
    print(f"{len(layout['settlements'])} layout ids, {len(layout['settlements']) - len(blocks)} already on the map "
          f"(never rewritten), {len(blocks)} missing.")
    if not blocks:
        print("Nothing to do.")
        return
    for sid, _ in blocks:
        print(f"  + {sid}")
    out = append_blocks(src, blocks)
    if not args.apply:
        print("\nDRY RUN: re-run with --apply to append them to the live file.")
        return
    amv._write(LIVE, had_bom, out, "newfactions_" + datetime.datetime.now().strftime("%Y%m%d_%H%M%S"))
    print(f"Appended {len(blocks)} settlement(s); the file parses. Run python tools/validate_moduledata.py next "
          "(SETTLEMENT_ECONOMY_FLOOR). This tool writes no loc rows: add the 12 as add_map_villages.py does.")


if __name__ == "__main__":
    main()
