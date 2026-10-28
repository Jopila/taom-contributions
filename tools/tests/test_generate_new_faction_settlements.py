"""Tests for tools/generate_new_faction_settlements.py: it only ever adds rows the live map lacks.

Until 2026-10-27 an --apply stripped every layout id from the live settlements.xml and wrote it
again from the June 2026 values, which would have undone the Lindon batch, the renames and the
economy floors (RCA docs/reviews/rca-lindon-expansion-2026-10-27.md, finding 19).
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import generate_new_faction_settlements as g  # noqa: E402

LAYOUT = {"settlements": [
    {"id": "town_LN1", "type": "town", "culture": "rivendell", "owner": "clan_lindon_1", "posX": 1.0, "posY": 2.0},
    {"id": "village_LN1_1", "type": "village", "culture": "rivendell", "bound": "town_LN1", "posX": 1.5, "posY": 2.5},
    {"id": "town_GBC1", "type": "town", "culture": "goblin", "owner": "clan_bluecraig_1", "posX": 3.0, "posY": 4.0},
    {"id": "town_GT1", "type": "town", "culture": "goblin", "owner": "clan_goblin_1", "posX": 5.0, "posY": 6.0},
]}


def master(*ids):
    rows = "".join(f'  <Settlement id="{sid}" name="x" />\r\n' for sid in ids)
    return f'<Settlements>\r\n{rows}</Settlements>\r\n'


class MissingBlocksTests(unittest.TestCase):
    def test_an_id_already_on_the_map_is_never_rewritten(self):
        blocks = g.missing_blocks(LAYOUT, master("town_LN1", "village_LN1_1", "town_GT1"))
        self.assertEqual([sid for sid, _ in blocks], ["town_GBC1"])

    def test_every_id_present_means_nothing_to_do(self):
        self.assertEqual(g.missing_blocks(LAYOUT, master("town_LN1", "village_LN1_1", "town_GBC1", "town_GT1")), [])

    def test_a_similar_id_does_not_count_as_present(self):
        blocks = g.missing_blocks(LAYOUT, master("town_LN10", "village_LN1_1", "town_GBC1", "town_GT1"))
        self.assertEqual([sid for sid, _ in blocks], ["town_LN1"])

    def test_rows_carry_the_culture_each_kingdom_has_now(self):
        blocks = dict(g.missing_blocks(LAYOUT, master()))
        self.assertIn('culture="Culture.lindon"', blocks["town_LN1"])
        self.assertIn('culture="Culture.lindon"', blocks["village_LN1_1"])
        self.assertIn('culture="Culture.bluecraig"', blocks["town_GBC1"])
        self.assertIn('culture="Culture.goblin"', blocks["town_GT1"])

    def test_lindon_keeps_its_battania_scenes(self):
        self.assertIn("gui_bg_town_battania", dict(g.missing_blocks(LAYOUT, master()))["town_LN1"])

    def test_appended_rows_parse_and_use_the_files_newline(self):
        out = g.append_blocks(master("town_GT1"), g.missing_blocks(LAYOUT, master("town_GT1")))
        ET.fromstring(out)
        self.assertNotIn("\n", out.replace("\r\n", ""))
        self.assertTrue(out.rstrip().endswith("</Settlements>"))


if __name__ == "__main__":
    unittest.main()
