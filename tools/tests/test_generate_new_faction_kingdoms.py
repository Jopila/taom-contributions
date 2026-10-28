#!/usr/bin/env python3
"""upsert() in generate_new_faction_kingdoms.py: a re-run keeps its marker block where it stands.

Run:  python -m unittest tools.tests.test_generate_new_faction_kingdoms

#947: the Dale expansion (64345bf3a) appended its own marker block after the Arthedain block in
clans.xml, lords.xml and heroes.xml. upsert() deleted the Arthedain block and re-appended it before
the closing tag, so --check reported drift for a change of position alone, and --apply would have
moved Arthedain below Dale.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_new_faction_kingdoms as gen  # noqa: E402

MARKER = "TAOM-NEWFACTIONS:arthedain"


def doc(arthedain_rows, after=""):
    return ("<Factions>\n"
            "  <Faction id=\"vanilla\" />\n"
            f"  <!-- {MARKER}:BEGIN -->\n{arthedain_rows}  <!-- {MARKER}:END -->\n"
            f"{after}"
            "</Factions>\n")


DALE = ("  <!-- TAOM-DALE-EXPANSION:BEGIN -->\n"
        "  <Faction id=\"clan_sturgia_10\" />\n"
        "  <!-- TAOM-DALE-EXPANSION:END -->\n")


class UpsertTests(unittest.TestCase):
    def test_same_payload_with_a_block_after_it_changes_nothing(self):
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        text = doc(rows, after=DALE)
        self.assertEqual(gen.upsert(text, "</Factions>", rows, MARKER), text)

    def test_new_payload_replaces_the_block_in_place(self):
        old = "  <Faction id=\"clan_arthedain_1\" />\n"
        new = "  <Faction id=\"clan_arthedain_1\" tier=\"5\" />\n"
        self.assertEqual(gen.upsert(doc(old, after=DALE), "</Factions>", new, MARKER), doc(new, after=DALE))

    def test_absent_block_is_appended_before_the_closing_tag(self):
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        text = "<Factions>\n  <Faction id=\"vanilla\" />\n</Factions>\n"
        self.assertEqual(gen.upsert(text, "</Factions>", rows, MARKER), doc(rows))

    def test_a_second_region_is_refused(self):
        # The old strip-all upsert made --check report a stale second copy; replacing only the first would hide it,
        # and the engine loads the later row of a repeated id.
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        stale = f"  <!-- {MARKER}:BEGIN -->\n  <Faction id=\"clan_arthedain_1\" tier=\"6\" />\n  <!-- {MARKER}:END -->\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(doc(rows, after=DALE + stale), "</Factions>", rows, MARKER)

    def test_a_begin_without_its_end_is_refused(self):
        text = f"<Factions>\n  <!-- {MARKER}:BEGIN -->\n  <Faction id=\"clan_arthedain_1\" />\n</Factions>\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(text, "</Factions>", "  <Faction id=\"clan_arthedain_1\" />\n", MARKER)

    def test_an_end_without_its_begin_is_refused(self):
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        orphan = f"  <Faction id=\"clan_arthedain_1\" tier=\"6\" />\n  <!-- {MARKER}:END -->\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(doc(rows, after=orphan), "</Factions>", rows, MARKER)

    def test_a_missing_closing_tag_is_refused(self):
        with self.assertRaises(RuntimeError):
            gen.upsert("<Factions>\n", "</Factions>", "  <Faction id=\"x\" />\n", MARKER)

    def test_a_missing_closing_tag_is_refused_when_the_region_exists(self):
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        with self.assertRaises(RuntimeError):
            gen.upsert(doc(rows).replace("</Factions>\n", ""), "</Factions>", rows, MARKER)

    def test_an_end_marker_straight_before_the_closing_tag_is_a_region(self):
        rows = "  <Faction id=\"clan_arthedain_1\" />\n"
        text = doc(rows).replace(f":END -->\n</Factions>", ":END --></Factions>")
        self.assertEqual(gen.upsert(text, "</Factions>", rows, MARKER), doc(rows))

    def test_payload_backslashes_are_written_verbatim(self):
        rows = "  <Faction id=\"x\" name=\"a\\1b\" />\n"
        self.assertIn("a\\1b", gen.upsert(doc("", after=DALE), "</Factions>", rows, MARKER))


class BannerTests(unittest.TestCase):
    """A cloned clan keeps clan_gundabad_1's orc crest unless its realm names a banner (Lindon did not, 2026-10-28)."""

    def test_every_regeneratable_realm_names_a_banner_for_its_kingdom_and_each_clan(self):
        for rid in gen.REGENERATABLE:
            k = gen.KINGDOMS[rid]
            self.assertTrue(k.get("banner"), rid)
            self.assertIn("kingdom_icon", k, rid)
            self.assertEqual(len(k["clans"]), len(k.get("clan_icons", [])), rid)

if __name__ == "__main__":
    unittest.main()
