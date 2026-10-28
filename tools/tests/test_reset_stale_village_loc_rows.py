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


class MainTests(unittest.TestCase):
    """main() on a temp TAOM_Map tree with its own cache folder; never the install or the tracked cache."""

    MASTER = ('<Settlements>\r\n'
              '  <Settlement id="village_E2_1" name="{=Settlements.Settlement.name.village_E2_1}Linenhold" />\r\n'
              '  <Settlement id="town_EN1" name="{=Settlements.Settlement.name.town_EN1}Galtrev" />\r\n'
              '</Settlements>\r\n')

    def _tree(self, td, de_rows=None):
        from pathlib import Path
        root = Path(td)
        live = root / "Modules" / "TAOM_Map" / "ModuleData" / "settlements.xml"
        live.parent.mkdir(parents=True)
        live.write_bytes(self.MASTER.encode("utf-8"))
        for lang in rs.amv.LANGS:
            rows = [("village_E2_1", "Dorf E2_1"), ("town_EN1", "Epicrotea")] if de_rows is None or lang != "DE" else de_rows
            body = "".join(f'    <string id="Settlements.Settlement.name.{sid}" text="{t}" />\r\r\n' for sid, t in rows)
            f = live.parent / "Languages" / lang / "loc_settlements.xml"
            f.parent.mkdir(parents=True)
            f.write_bytes(f'<base>\r\r\n  <strings>\r\r\n{body}  </strings>\r\r\n</base>\r\r\n'.encode("utf-8"))
        cache = root / "cache"
        cache.mkdir()
        for lang in rs.amv.LANGS:
            (cache / f"{lang.lower()}.json").write_bytes(
                b'{\n  "Settlements.Settlement.name.town_EN1": "Epicrotea",\n  "Settlements.Settlement.name.village_E2_1": "Dorf E2_1"\n}')
        return str(root / "Modules" / "TAOM_Map"), str(live), str(cache)

    def _main(self, tree, argv):
        import contextlib
        import io
        from unittest import mock
        mp, live, cache = tree
        with mock.patch.object(rs.amv, "MAP", mp), mock.patch.object(rs.amv, "LIVE", live), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return rs.main(argv, cache_dir=cache)

    def _loc(self, tree, lang):
        return open(f"{tree[0]}/ModuleData/Languages/{lang}/loc_settlements.xml", "rb").read().decode("utf-8")

    def test_without_rerun_retired_it_refuses_before_reading_anything(self):
        # A re-run after the backlog translated these rows would turn them back into English.
        from unittest import mock
        with mock.patch.object(rs.amv, "_read", side_effect=AssertionError("read a file")):
            with self.assertRaises(SystemExit):
                rs.main(["--apply"])

    def test_the_four_dunland_fortifications_are_targets_too(self):
        self.assertTrue({"town_EN1", "town_EN2", "town_EN3", "castle_EN5"} <= set(rs.target_ids(["village_E2_1"])))

    def test_apply_resets_every_language_and_evicts_the_cache(self):
        import tempfile
        with tempfile.TemporaryDirectory() as td:
            tree = self._tree(td)
            self.assertEqual(self._main(tree, ["--rerun-retired", "--apply"]), 0)
            fr = self._loc(tree, "FR")
            cache = open(f"{tree[2]}/fr.json", "rb").read().decode("utf-8")
        self.assertIn('text="Linenhold"', fr)
        self.assertIn('text="Galtrev"', fr)
        self.assertNotIn("Settlements.Settlement", cache)

    def test_a_row_the_count_does_not_match_refuses_and_writes_nothing(self):
        import tempfile
        dup = [("village_E2_1", "Dorf E2_1"), ("village_E2_1", "Dorf E2_1"), ("town_EN1", "Epicrotea")]
        with tempfile.TemporaryDirectory() as td:
            tree = self._tree(td, de_rows=dup)
            self.assertEqual(self._main(tree, ["--rerun-retired", "--apply"]), 1)
            fr = self._loc(tree, "FR")
            cache = open(f"{tree[2]}/fr.json", "rb").read().decode("utf-8")
        self.assertIn('text="Dorf E2_1"', fr)
        self.assertIn("village_E2_1", cache)

    def test_a_write_that_fails_is_finished_by_a_rerun(self):
        # DE decides what is stale, so it is written last: a failed run leaves it stale and the re-run finishes.
        import stat
        import tempfile
        with tempfile.TemporaryDirectory() as td:
            tree = self._tree(td)
            fr = f"{tree[0]}/ModuleData/Languages/FR/loc_settlements.xml"
            os.chmod(fr, stat.S_IREAD)
            try:
                with self.assertRaises(PermissionError):
                    self._main(tree, ["--rerun-retired", "--apply"])
            finally:
                os.chmod(fr, stat.S_IREAD | stat.S_IWRITE)
            self.assertIn('text="Dorf E2_1"', self._loc(tree, "DE"))
            self.assertEqual(self._main(tree, ["--rerun-retired", "--apply"]), 0)
            for lang in rs.amv.LANGS:
                self.assertIn('Linenhold" />', self._loc(tree, lang), lang)  # BR and IT carry a grammar tag
                self.assertNotIn("Dorf E2_1", self._loc(tree, lang), lang)

if __name__ == "__main__":
    unittest.main()
