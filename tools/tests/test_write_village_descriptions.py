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

    def test_an_id_without_a_text_attribute_is_refused(self):
        with self.assertRaises(SystemExit):
            wd.rewrite_master(MASTER, {"village_C_1": "Nowhere."})


class LocTests(unittest.TestCase):
    def test_an_existing_row_gets_the_english(self):
        out, n = wd.rewrite_loc(LOC, NEW)
        self.assertIn(f'<string id="{P}text.village_A_1" text="Aford lies by a ford." />', out)
        self.assertEqual(n, 2)

    def test_a_missing_own_row_is_added_after_the_name_row_and_the_borrowed_row_stays(self):
        out, _ = wd.rewrite_loc(LOC, NEW)
        self.assertIn(f'<string id="{P}name.village_B_1" text="Bham" />\r\r\n'
                      f'    <string id="{P}text.village_B_1" text="Bham sits on a hill &amp; a stream." />\r\r\n', out)
        self.assertIn(f'id="{P}text.village_Z_9" text="Geliehen."', out)
        ET.fromstring(out)

    def test_a_second_run_changes_nothing(self):
        once, _ = wd.rewrite_loc(LOC, NEW)
        twice, n = wd.rewrite_loc(once, NEW)
        self.assertEqual((twice, n), (once, 0))


class TableTests(unittest.TestCase):
    def test_every_description_names_its_village_and_reads_as_house_prose(self):
        import runpy
        names = runpy.run_path(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Apply-MapVillageNames.py"),
                               run_name="names_only")["NAMES"]
        self.assertEqual(len(wd.DESCRIPTIONS), 102)
        for sid, text in wd.DESCRIPTIONS.items():
            self.assertIn(names[sid], text, sid)
            self.assertNotRegex(text, "[–—\"<]", sid)
            self.assertTrue(text.endswith("."), sid)


if __name__ == "__main__":
    unittest.main()
