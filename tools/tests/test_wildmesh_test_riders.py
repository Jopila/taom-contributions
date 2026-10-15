"""The five WildMesh3D test riders (#794, docs/features/wildmesh-mounts.md) stay what they claim to be.

Reads the shipped Main/_Module/ModuleData/test_riders/wildmesh.xml. Nothing else pins each rider to
its own animal and its own harness (RCA rca-wildmesh-mounts-2026-10-15.md, row 22): a swapped Horse id
would still pass every validator, because every WildMesh mount is a legal dwarf mount or a legal elf
mount, and the ram barding or elk saddle is only required to be present.

The battle sets are modelled the way BasicCharacterObject.Deserialize builds them: each battle
EquipmentRoster is one set, and an <equipment> written directly under <Equipments> is laid over that
slot in every set (an empty id clears it).
"""
import json
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
MODULE = REPO / "Main" / "_Module"
RIDER_REL = "test_riders/wildmesh"
RIDER_FILE = MODULE / "ModuleData" / "test_riders" / "wildmesh.xml"
SUBMODULE = MODULE / "SubModule.xml"
NPC_SCHEMA = REPO / "tools" / "schemas" / "taom_npccharacter.json"

ANIMALS = ("boar", "warthog", "mountaingoat", "deer", "moose")
DWARF_ANIMALS = ("boar", "warthog", "mountaingoat")
RAM_BARDINGS = {"Item.taom_ram_barding_light_a", "Item.taom_ram_barding_heavy_a"}
ELK_SADDLE = "Item.taom_elk_saddle_a"


def battle_sets(npc: ET.Element) -> list:
    """Each battle set of a character as {slot: item id}, troop-level overrides applied."""
    equipments = npc.find("Equipments")
    if equipments is None:
        return []
    overrides = {}
    for eq in equipments.findall("equipment"):
        overrides[eq.get("slot")] = eq.get("id")
    sets = []
    for roster in equipments.findall("EquipmentRoster"):
        if (roster.get("civilian") or "").lower() == "true":
            continue
        slots = {eq.get("slot"): eq.get("id") for eq in roster.findall("equipment")}
        slots.update(overrides)
        sets.append({slot: item for slot, item in slots.items() if item})
    return sets


def load_riders() -> dict:
    root = ET.parse(RIDER_FILE).getroot()
    return {npc.get("id"): npc for npc in root.findall("NPCCharacter")}


class BattleSetModelTests(unittest.TestCase):
    """The deserializer model the data tests below rely on."""

    def test_a_troop_level_equipment_overrides_the_roster_slot_in_every_set(self):
        npc = ET.fromstring(
            '<NPCCharacter id="x"><Equipments>'
            '<EquipmentRoster><equipment slot="Horse" id="Item.a"/><equipment slot="Item0" id="Item.s"/></EquipmentRoster>'
            '<EquipmentRoster><equipment slot="Item0" id="Item.s2"/></EquipmentRoster>'
            '<equipment slot="Horse" id="Item.b"/>'
            '</Equipments></NPCCharacter>')
        self.assertEqual(battle_sets(npc), [{"Horse": "Item.b", "Item0": "Item.s"},
                                            {"Horse": "Item.b", "Item0": "Item.s2"}])

    def test_a_civilian_roster_is_not_a_battle_set(self):
        npc = ET.fromstring(
            '<NPCCharacter id="x"><Equipments>'
            '<EquipmentRoster civilian="true"><equipment slot="Item0" id="Item.c"/></EquipmentRoster>'
            '<EquipmentRoster><equipment slot="Item0" id="Item.s"/></EquipmentRoster>'
            '</Equipments></NPCCharacter>')
        self.assertEqual(battle_sets(npc), [{"Item0": "Item.s"}])


class WildMeshTestRiderDataTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.riders = load_riders()

    def _rider(self, animal):
        return self.riders["taom_test_wildmesh_%s_rider" % animal]

    def test_there_is_one_rider_per_animal_and_no_other(self):
        self.assertEqual(set(self.riders), {"taom_test_wildmesh_%s_rider" % a for a in ANIMALS})

    def test_every_rider_id_starts_with_taom_test(self):
        # The /release step 5 grep (git grep -l taom_test_ -- Main/_Module) finds the file by this prefix.
        for rider_id in self.riders:
            self.assertTrue(rider_id.startswith("taom_test_"), rider_id)

    def test_every_rider_rides_its_own_animal_in_every_battle_set(self):
        for animal in ANIMALS:
            sets = battle_sets(self._rider(animal))
            self.assertTrue(sets, animal)
            for i, s in enumerate(sets):
                self.assertEqual(s.get("Horse"), "Item.taom_wildmesh_%s_a" % animal,
                                 "%s rider, battle set %d" % (animal, i + 1))

    def test_every_rider_carries_a_harness_in_every_battle_set(self):
        for animal in ANIMALS:
            for i, s in enumerate(battle_sets(self._rider(animal))):
                self.assertIn("HorseHarness", s, "%s rider, battle set %d" % (animal, i + 1))

    def test_the_dwarf_riders_wear_a_ram_barding_and_use_both(self):
        for animal in DWARF_ANIMALS:
            harnesses = [s["HorseHarness"] for s in battle_sets(self._rider(animal))]
            self.assertTrue(set(harnesses) <= RAM_BARDINGS, (animal, harnesses))
            self.assertEqual(set(harnesses), RAM_BARDINGS, (animal, harnesses))

    def test_the_deer_and_moose_riders_wear_the_elk_saddle(self):
        for animal in ("deer", "moose"):
            harnesses = {s["HorseHarness"] for s in battle_sets(self._rider(animal))}
            self.assertEqual(harnesses, {ELK_SADDLE}, animal)

    def test_no_rider_has_an_upgrade_target(self):
        # The picker lists them, the upgrade graph must not (RCA row 6).
        for rider_id, npc in self.riders.items():
            self.assertIsNone(npc.find("upgrade_targets"), rider_id)

    def test_no_dwarf_rider_repeats_a_roster(self):
        # RCA row 21: rosters 3 and 4 were byte copies of 1 and 2.
        for animal in DWARF_ANIMALS:
            sets = [tuple(sorted(s.items())) for s in battle_sets(self._rider(animal))]
            self.assertEqual(len(sets), len(set(sets)), animal)


class WildMeshTestRiderRegistrationTests(unittest.TestCase):
    def test_the_file_is_outside_the_production_troops_glob(self):
        self.assertTrue(RIDER_FILE.is_file())
        self.assertFalse((MODULE / "ModuleData" / "troops" / "troops_wildmesh_test.xml").exists())

    def test_submodule_registers_the_file_for_custom_game_only(self):
        root = ET.parse(SUBMODULE).getroot()
        nodes = [n for n in root.iter("XmlNode")
                 if n.find("XmlName") is not None and n.find("XmlName").get("path") == RIDER_REL]
        self.assertEqual(len(nodes), 1)
        self.assertEqual(nodes[0].find("XmlName").get("id"), "NPCCharacters")
        games = [g.get("value").strip() for g in nodes[0].find("IncludedGameTypes").findall("GameType")]
        self.assertEqual(games, ["CustomGame"])

    def test_submodule_names_no_stale_path(self):
        self.assertNotIn("troops_wildmesh_test", SUBMODULE.read_text(encoding="utf-8-sig"))

    def test_the_npc_schema_covers_the_folder(self):
        # Without it the duplicate-id and enum checks skip the riders silently.
        spec = json.loads(NPC_SCHEMA.read_text(encoding="utf-8-sig"))
        self.assertIn("test_riders/*.xml", spec["applies_to"])


if __name__ == "__main__":
    unittest.main()
