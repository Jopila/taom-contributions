"""Tests for tools/oneoff/write_village_descriptions.py: new descriptions for the renamed map villages.

Synthetic text only; the live run is checked by the one-off's own second dry run (0 changes).
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import write_village_descriptions as wd  # noqa: E402

P = "Settlements.Settlement."
MASTER = ('<Settlements>\r\n'
          f'  <Settlement id="village_A_1" name="{{={P}name.village_A_1}}Aford" text="{{={P}text.village_A_1}}Old Calradian text." posX="1" />\r\n'
          f'  <Settlement id="village_B_1" name="{{={P}name.village_B_1}}Bham" text="{{={P}text.village_Z_9}}Borrowed text." posX="2" />\r\n'
          '</Settlements>\r\n')
LOC = ('<base>\r\r\n  <strings>\r\r\n'
       f'    <string id="{P}name.village_A_1" text="Aford" />\r\r\n'
       f'    <string id="{P}text.village_A_1" text="Alter Text." />\r\r\n'
       f'    <string id="{P}name.village_B_1" text="Bham" />\r\r\n'
       f'    <string id="{P}text.village_Z_9" text="Geliehen." />\r\r\n'
       '  </strings>\r\r\n</base>\r\r\n')
NEW = {"village_A_1": "Aford lies by a ford.", "village_B_1": "Bham sits on a hill & a stream."}


class MasterTests(unittest.TestCase):
    def test_text_is_replaced_and_a_borrowed_key_becomes_the_villages_own(self):
        out, n = wd.rewrite_master(MASTER, NEW)
        self.assertEqual(n, 2)
        self.assertIn(f'text="{{={P}text.village_A_1}}Aford lies by a ford."', out)
        self.assertIn(f'text="{{={P}text.village_B_1}}Bham sits on a hill &amp; a stream."', out)
        self.assertNotIn("village_Z_9", out)
        ET.fromstring(out)

    def test_a_second_run_changes_nothing(self):
        once, _ = wd.rewrite_master(MASTER, NEW)
        twice, n = wd.rewrite_master(once, NEW)
        self.assertEqual((twice, n), (once, 0))

    def test_a_row_without_text_gets_one_after_its_name(self):
        master = MASTER.replace('</Settlements>', f'  <Settlement id="village_C_1" name="{{={P}name.village_C_1}}Cby" posX="3" />\r\n</Settlements>')
        out, n = wd.rewrite_master(master, {"village_C_1": "Cby lies low."})
        self.assertEqual(n, 1)
        self.assertIn(f'name="{{={P}name.village_C_1}}Cby" text="{{={P}text.village_C_1}}Cby lies low." posX="3"', out)
        ET.fromstring(out)

    def test_an_id_with_no_row_is_refused(self):
        with self.assertRaises(SystemExit):
            wd.rewrite_master(MASTER, {"village_C_1": "Nowhere."})


class LocTests(unittest.TestCase):
    def test_an_existing_row_gets_the_english(self):
        out, n = wd.rewrite_loc(LOC, NEW)
        self.assertIn(f'<string id="{P}text.village_A_1" text="Aford lies by a ford." />', out)
        self.assertEqual(n, 2)

    def test_a_missing_own_row_is_added_after_the_name_row(self):
        out, _ = wd.rewrite_loc(LOC, NEW)
        self.assertIn(f'<string id="{P}name.village_B_1" text="Bham" />\r\r\n'
                      f'    <string id="{P}text.village_B_1" text="Bham sits on a hill &amp; a stream." />\r\r\n', out)
        ET.fromstring(out)

    def test_a_second_run_changes_nothing(self):
        once, _ = wd.rewrite_loc(LOC, NEW)
        twice, n = wd.rewrite_loc(once, NEW)
        self.assertEqual((twice, n), (once, 0))


class OrphanTests(unittest.TestCase):
    """Rows of a borrowed key whose owner village is not on the map: no settlement reads them."""

    def test_the_rows_are_removed_whole_and_the_rest_stays(self):
        out, n = wd.remove_rows(LOC, [f"{P}text.village_Z_9"])
        self.assertEqual(n, 1)
        self.assertNotIn("village_Z_9", out)
        self.assertIn(f'    <string id="{P}name.village_B_1" text="Bham" />\r\r\n  </strings>', out)
        ET.fromstring(out)

    def test_a_second_run_removes_nothing(self):
        once, _ = wd.remove_rows(LOC, [f"{P}text.village_Z_9"])
        self.assertEqual(wd.remove_rows(once, [f"{P}text.village_Z_9"]), (once, 0))

    def test_a_key_the_master_still_uses_is_named(self):
        self.assertEqual(wd.orphans_in_use(MASTER, [f"{P}text.village_Z_9", f"{P}text.village_Y_8"]), [f"{P}text.village_Z_9"])


class CheckTests(unittest.TestCase):
    """--check, the drift gate: a reinstall of an older TAOM_Map package brings the Calradian text back."""

    def setUp(self):
        self.master, _ = wd.rewrite_master(MASTER, NEW)
        loc, _ = wd.rewrite_loc(LOC, NEW)
        self.loc, _ = wd.remove_rows(loc, [f"{P}text.village_Z_9"])

    def test_the_written_state_passes(self):
        self.assertEqual(wd.check(self.master, {"DE": self.loc}, NEW, [f"{P}text.village_Z_9"]), [])

    def test_a_translated_row_still_passes(self):
        translated = self.loc.replace('text="Aford lies by a ford."', 'text="Aford liegt an einer Furt."')
        self.assertEqual(wd.check(self.master, {"DE": translated}, NEW, []), [])

    def test_a_reverted_master_text_fails(self):
        found = wd.check(MASTER, {"DE": self.loc}, NEW, [])
        self.assertTrue(any("village_A_1" in f and "settlements.xml" in f for f in found))

    def test_a_missing_own_row_fails(self):
        found = wd.check(self.master, {"DE": LOC}, NEW, [])
        self.assertTrue(any("village_B_1" in f and "DE" in f for f in found))

    def test_an_orphan_row_that_came_back_fails(self):
        found = wd.check(self.master, {"DE": LOC}, {}, [f"{P}text.village_Z_9"])
        self.assertTrue(any("village_Z_9" in f for f in found))


class RetiredTests(unittest.TestCase):
    def test_without_rerun_retired_it_refuses_before_reading_anything(self):
        # After #763 every row differs from the English, so a re-run would turn the translations back.
        from unittest import mock
        with mock.patch.object(wd.amv, "_read", side_effect=AssertionError("read a file")):
            with self.assertRaises(SystemExit):
                wd.main(["--apply"])

    def _main(self, argv, orphaned, descriptions=NEW):
        """main() on a temp TAOM_Map tree holding MASTER and LOC; returns (code, master after, DE loc after)."""
        import contextlib
        import io
        import tempfile
        from pathlib import Path
        from unittest import mock
        with tempfile.TemporaryDirectory() as td:
            mp = Path(td) / "TAOM_Map"
            live = mp / "ModuleData" / "settlements.xml"
            live.parent.mkdir(parents=True)
            live.write_bytes(MASTER.encode("utf-8"))
            for lang in wd.amv.LANGS:
                f = mp / "ModuleData" / "Languages" / lang / "loc_settlements.xml"
                f.parent.mkdir(parents=True)
                f.write_bytes(LOC.encode("utf-8"))
            (Path(td) / "cache").mkdir()
            with mock.patch.object(wd.amv, "MAP", str(mp)), mock.patch.object(wd.amv, "LIVE", str(live)), \
                    mock.patch.object(wd, "DESCRIPTIONS", descriptions), mock.patch.object(wd, "ORPHANED_KEYS", orphaned), \
                    contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                code = wd.main(argv, cache_dir=str(Path(td) / "cache"))
            de = mp / "ModuleData" / "Languages" / "DE" / "loc_settlements.xml"
            return code, live.read_bytes().decode("utf-8"), de.read_bytes().decode("utf-8")

    def test_check_runs_without_the_flag(self):
        self.assertEqual(self._main(["--check"], [])[0], 1)

    def test_an_orphaned_key_a_settlement_still_reads_after_the_run_refuses_and_writes_nothing(self):
        # village_B_1 reads the borrowed text.village_Z_9 and this run does not re-key it, so the rows must stay.
        code, master, de = self._main(["--rerun-retired", "--apply"], [f"{P}text.village_Z_9"],
                                      {"village_A_1": NEW["village_A_1"]})
        self.assertEqual(code, 1)
        self.assertEqual((master, de), (MASTER, LOC))

    def test_an_orphaned_key_whose_only_reader_this_run_re_keys_is_deleted(self):
        # A reinstalled package still has village_B_1 on the borrowed key; the repair must not refuse it.
        code, master, de = self._main(["--rerun-retired", "--apply"], [f"{P}text.village_Z_9"])
        self.assertEqual(code, 0)
        self.assertNotIn("village_Z_9", master)
        self.assertNotIn("village_Z_9", de)


class TableTests(unittest.TestCase):
    def test_every_description_names_its_village_and_reads_as_house_prose(self):
        import runpy
        names = runpy.run_path(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Apply-MapVillageNames.py"),
                               run_name="names_only")["NAMES"]
        self.assertEqual(len(wd.DESCRIPTIONS), 109)
        for sid, text in wd.DESCRIPTIONS.items():
            self.assertIn(names[sid], text, sid)
            self.assertNotRegex(text, "[–—\"<{}]", sid)
            self.assertTrue(text.endswith("."), sid)

    def test_no_orphaned_key_belongs_to_a_described_village(self):
        for key in wd.ORPHANED_KEYS:
            self.assertNotIn(key[len(wd.KEY):], wd.DESCRIPTIONS)


@unittest.skipUnless(os.path.isfile(wd.amv.LIVE), "needs the live TAOM_Map install")
class LiveDriftTests(unittest.TestCase):
    """The live module is unversioned: a reinstall of an older package reverts these rows silently."""

    def setUp(self):
        _, self.master = wd.amv._read(wd.amv.LIVE)
        self.locs = {lang: wd.amv._read(wd.amv._loc_path(lang))[1] for lang in wd.amv.LANGS}

    def test_the_live_module_holds_every_description_and_no_orphan_row(self):
        findings = wd.check(self.master, self.locs, wd.DESCRIPTIONS, wd.ORPHANED_KEYS)
        self.assertEqual(findings, [], "Repair: python tools/oneoff/write_village_descriptions.py --rerun-retired --apply")

    def test_each_description_names_its_fortification_as_the_map_does(self):
        import re
        names = wd.amv.master_names(self.master)
        for sid, text in wd.DESCRIPTIONS.items():
            row = re.search(r'<Settlement id="' + re.escape(sid) + r'"[\s>].*?</Settlement>', self.master, re.S)
            bound = re.search(r'bound="Settlement\.([^"]+)"', row.group(0)).group(1)
            self.assertIn(names[bound].casefold(), text.casefold(), sid)  # "the Morannon" mid-sentence


if __name__ == "__main__":
    unittest.main()
