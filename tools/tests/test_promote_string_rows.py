#!/usr/bin/env python3
"""source_blocks("strings", ...) in promote_borrowed_cultures.py: which of the source culture's string rows a
promoted culture clones.

Run:  python -m unittest tools.tests.test_promote_string_rows

A row that a feature writes by hand for every culture must not be cloned: the clone would define the same id a
second time, inside the generator's region.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import promote_borrowed_cultures as promo  # noqa: E402

TEXT = "\n".join([
    "<strings>",
    '  <string id="str_player_father_name.gondor" text="{=taom_str_player_father_name.gondor}Hurin" />',
    '  <string id="str_taom_round_drink.gondor" text="{=taom_round_drink_gondor}Belfalas wine" />',
    "</strings>",
])


class PromoteStringRowsTests(unittest.TestCase):
    def test_a_culture_row_is_cloned(self):
        self.assertIn("str_player_father_name.gondor", promo.source_blocks("strings", TEXT, "gondor"))

    def test_the_inn_rounds_drink_row_is_not_cloned(self):
        # Inn Rounds (#901) writes one drink row per culture by hand, promoted cultures included.
        self.assertNotIn("str_taom_round_drink", promo.source_blocks("strings", TEXT, "gondor"))


if __name__ == "__main__":
    unittest.main()
