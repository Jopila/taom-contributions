"""Tests for tools/oneoff/reset_stale_village_loc_rows.py: which language rows still hold a pre-rename name.

Synthetic rows only; the one-off's live run is checked by its own dry run and the loc coverage gate.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import reset_stale_village_loc_rows as rs  # noqa: E402

MASTER = {"village_E2_1": "Linenhold", "village_I_b": "Talath Curunír", "castle_village_E1_1": "Hammerstand"}


class StaleRowsTests(unittest.TestCase):
    def test_a_row_from_before_the_rename_is_listed(self):
        rows = {"DE": {"village_E2_1": "Dorf E2_1"}, "RU": {"village_E2_1": "Деревня E2_1"}}
        self.assertEqual(rs.stale_rows(["village_E2_1"], MASTER, rows), {"DE": ["village_E2_1"], "RU": ["village_E2_1"]})

    def test_an_id_the_witness_already_carries_keeps_its_translations(self):
        # DE equal to the English means the id was translated after its rename: RU's Cyrillic row is good.
        rows = {"DE": {"village_I_b": "Talath Curunír"}, "RU": {"village_I_b": "Талат Курунир"}}
        self.assertEqual(rs.stale_rows(["village_I_b"], MASTER, rows), {"DE": [], "RU": []})

    def test_a_row_already_equal_to_the_english_is_not_listed(self):
        rows = {"DE": {"village_E2_1": "Dorf E2_1"}, "BR": {"village_E2_1": "{.FS}Linenhold"}}
        self.assertEqual(rs.stale_rows(["village_E2_1"], MASTER, rows)["BR"], [])

    def test_a_kept_translation_of_the_current_name_is_not_listed(self):
        rows = {"DE": {"castle_village_E1_1": "Burgdorf E1_1"}, "PL": {"castle_village_E1_1": "Młotowisko"}}
        self.assertEqual(rs.stale_rows(["castle_village_E1_1"], MASTER, rows)["PL"], [])

    def test_a_second_run_finds_nothing(self):
        rows = {"DE": {"village_E2_1": "Linenhold"}, "RU": {"village_E2_1": "Linenhold"}}
        self.assertEqual(rs.stale_rows(["village_E2_1"], MASTER, rows), {"DE": [], "RU": []})

    def test_a_language_file_missing_the_id_lists_nothing(self):
        rows = {"DE": {"village_E2_1": "Dorf E2_1"}, "FR": {}}
        self.assertEqual(rs.stale_rows(["village_E2_1"], MASTER, rows)["FR"], [])


class ResetTextTests(unittest.TestCase):
    TABLE = {"languages": {"BR": {"plain": "{.NS}", "allowed": ["{.NS}", "{.FS}"], "head_nouns": {}, "pins": {}, "tags": {}}}}

    def test_the_row_gets_the_english_name_and_keeps_its_grammar_tag(self):
        loc = '    <string id="Settlements.Settlement.name.village_E2_1" text="{.FS}Aldeia E2_1" />\r\r\n'
        out, n = rs.reset_rows(loc, "BR", {"village_E2_1": "Linenhold"}, self.TABLE)
        self.assertEqual(n, 1)
        self.assertIn('text="{.FS}Linenhold"', out)
        self.assertTrue(out.endswith("\r\r\n"))


if __name__ == "__main__":
    unittest.main()
