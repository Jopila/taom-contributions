#!/usr/bin/env python3
"""register_own_region() in promote_borrowed_cultures.py: a culture's SubModule.xml registration region is
replaced where it stands, added once, or refused.

Run:  python -m unittest tools.tests.test_promote_register_region

A doubled region used to pass --check, because only the first copy was replaced and the stale second one
(which the engine loads last) was never looked at.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import promote_borrowed_cultures as promo  # noqa: E402

MARK = f"{promo.MARKER}-REG:cardolan"
SHARED_END = f"    <!-- {promo.MARKER}-REG:END -->\n"


def region(body):
    return f"    <!-- {MARK}:BEGIN -->\n{body}    <!-- {MARK}:END -->\n"


def doc(*regions):
    return "<Module>\n" + SHARED_END + "".join(regions) + "</Module>\n"


class RegisterOwnRegionTests(unittest.TestCase):
    def test_an_existing_region_is_replaced_in_place(self):
        new = region("    <new/>\n")
        self.assertEqual(promo.register_own_region(doc(region("    <old/>\n")), MARK, new), doc(new))

    def test_an_absent_region_is_added_after_the_shared_block(self):
        new = region("    <new/>\n")
        self.assertEqual(promo.register_own_region(doc(), MARK, new), doc(new))

    def test_a_doubled_region_is_refused(self):
        stale = region("    <old/>\n")
        with self.assertRaises(SystemExit) as ctx:
            promo.register_own_region(doc(stale, stale), MARK, region("    <new/>\n"))
        self.assertIn("nothing written", str(ctx.exception))

    def test_a_lone_begin_is_refused(self):
        text = doc(f"    <!-- {MARK}:BEGIN -->\n    <old/>\n")
        with self.assertRaises(SystemExit):
            promo.register_own_region(text, MARK, region("    <new/>\n"))

    def test_a_lone_end_is_refused(self):
        text = doc(f"    <old/>\n    <!-- {MARK}:END -->\n")
        with self.assertRaises(SystemExit):
            promo.register_own_region(text, MARK, region("    <new/>\n"))


if __name__ == "__main__":
    unittest.main()
