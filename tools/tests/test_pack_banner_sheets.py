"""Banner icon sheets: the packing tool and the shipped-sheet gate (#1047).

Run:  python -m pytest tools/tests/test_pack_banner_sheets.py -q

A banner key names an icon id; banner_icons.xml maps the id to a material (a 4096 x 4096 sheet of
16 cells) and a texture_index. The gate reads the repo's source sheets (AssetSources/BannerIcons)
and the icon rows: each icon names a sheet that exists and a cell that holds art, no two icons share
a cell, no art cell is unnamed, each icon has its UI sprite, each material has its Kit packages,
and the partly used sheets cannot be merged into fewer.
"""
import sys
import unittest
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parent.parent.parent
TOOLS = REPO / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import pack_banner_sheets as pbs  # noqa: E402

XML = """<?xml version="1.0" encoding="utf-8"?>\r
<base>\r
    <BannerIconGroup id="1" name="{=!}A" is_pattern="false">\r
      <Icon id="100" material_name="taom_banners_a_alpha_01" texture_index="0" />\r
      <Icon id="101" material_name="taom_banners_a_alpha_01" texture_index="3" />\r
    </BannerIconGroup>\r
    <BannerIconGroup id="2" name="{=!}B" is_pattern="false">\r
      <Icon id="200" material_name="taom_banners_b_alpha_01" texture_index="1" />\r
    </BannerIconGroup>\r
</base>\r
"""


class PlanTests(unittest.TestCase):
    def test_full_sheets_are_never_moved(self):
        plan = pbs.plan({"full": 16, "a": 3, "b": 2})
        self.assertNotIn("full", plan)

    def test_partial_sheets_share_one_packed_sheet_when_they_fit(self):
        plan = pbs.plan({"a": 3, "b": 2})
        self.assertEqual({plan["a"][0], plan["b"][0]}, {"taom_banners_packed_alpha_01"})
        self.assertEqual(sorted([plan["a"][1], plan["b"][1]]), [0, 3])

    def test_a_sheet_that_does_not_fit_opens_the_next_packed_sheet(self):
        plan = pbs.plan({"a": 10, "b": 9})
        self.assertNotEqual(plan["a"][0], plan["b"][0])

    def test_first_fit_decreasing_fills_the_gap_with_a_smaller_sheet(self):
        plan = pbs.plan({"a": 10, "b": 9, "c": 6})
        self.assertEqual(plan["a"][0], plan["c"][0])
        self.assertEqual(len({p for p, _ in plan.values()}), 2)

    def test_the_plan_is_the_same_whatever_the_input_order(self):
        counts = {"x": 7, "y": 7, "z": 2, "w": 15, "v": 1}
        self.assertEqual(pbs.plan(counts), pbs.plan(dict(reversed(list(counts.items())))))

    def test_the_shipped_counts_pack_twenty_sheets_into_ten(self):
        counts = dict(zip("abcdefghijklmnopqrst", [7, 7, 2, 1, 4, 11, 15, 11, 15, 7, 4, 5, 4, 6, 10, 4, 11, 9, 10, 7]))
        self.assertEqual(len({p for p, _ in pbs.plan(counts).values()}), 10)


class MappingTests(unittest.TestCase):
    def test_icons_keep_their_order_inside_a_moved_sheet(self):
        icons = pbs.read_icons(XML)
        moves = pbs.icon_moves(icons, {"taom_banners_a_alpha_01": ("P", 4)})
        self.assertEqual(moves, {"100": ("P", 4, 0), "101": ("P", 5, 3)})

    def test_rewrite_changes_only_the_moved_rows_and_keeps_crlf(self):
        out = pbs.rewrite_icons(XML, {"200": ("taom_banners_packed_alpha_01", 7, 1)})
        self.assertIn('<Icon id="200" material_name="taom_banners_packed_alpha_01" texture_index="7" />\r\n', out)
        self.assertEqual(out.replace('taom_banners_packed_alpha_01" texture_index="7', 'taom_banners_b_alpha_01" texture_index="1'), XML)

    def test_rewrite_refuses_an_id_it_cannot_find(self):
        with self.assertRaises(KeyError):
            pbs.rewrite_icons(XML, {"999": ("P", 0, 0)})


class SheetTests(unittest.TestCase):
    def test_free_cells_get_the_red_green_checkerboard(self):
        sheet = pbs.assemble([], size=64)
        c = 16
        self.assertEqual(tuple(sheet[0, 0]), (255, 0, 0))            # cell 0: row 0 + col 0 even
        self.assertEqual(tuple(sheet[0, c]), (0, 255, 0))            # cell 1: odd
        self.assertEqual(tuple(sheet[c, 0]), (0, 255, 0))            # cell 4: row 1 + col 0 odd

    def test_cells_are_copied_verbatim_in_order(self):
        cells = [np.full((16, 16, 3), v, dtype=np.uint8) for v in (7, 9)]
        sheet = pbs.assemble(cells, size=64)
        self.assertEqual(tuple(sheet[0, 0]), (7, 7, 7))
        self.assertEqual(tuple(sheet[0, 16]), (9, 9, 9))
        self.assertEqual(tuple(sheet[0, 32]), (255, 0, 0))           # cell 2 free

    def test_a_placeholder_cell_is_not_art(self):
        self.assertFalse(pbs.is_art(np.full((32, 32, 3), (255, 0, 0), dtype=np.uint8)))
        cell = np.zeros((32, 32, 3), dtype=np.uint8)
        cell[10:20, 10:20, 1] = 255
        self.assertTrue(pbs.is_art(cell))

    def test_a_placeholder_with_a_few_off_by_one_pixels_is_not_art(self):
        # Rohan cell 11 ships as green with 8 pixels at (1, 254, 0)
        cell = np.full((64, 64, 3), (0, 255, 0), dtype=np.uint8)
        cell[0, :8] = (1, 254, 0)
        self.assertFalse(pbs.is_art(cell))

    def test_a_small_mark_is_still_art(self):
        cell = np.zeros((64, 64, 3), dtype=np.uint8)
        cell[30:38, 30:38, 1] = 255
        self.assertTrue(pbs.is_art(cell))


class ShippedSheetGateTests(unittest.TestCase):
    """The repo's sheets and icon rows as they ship."""

    @classmethod
    def setUpClass(cls):
        cls.report = pbs.audit(REPO)

    def test_every_icon_names_a_source_sheet(self):
        self.assertEqual(self.report["missing_sheet"], [])

    def test_every_icon_names_a_cell_that_holds_art(self):
        self.assertEqual(self.report["empty_cell"], [], "a placeholder cell renders as a red or green square")

    def test_no_two_icons_share_a_cell(self):
        self.assertEqual(self.report["shared_cell"], [])

    def test_no_art_cell_is_unnamed(self):
        self.assertEqual(self.report["unnamed_art"], [])

    def test_every_icon_has_its_ui_sprite(self):
        self.assertEqual(self.report["missing_sprite"], [])

    def test_every_material_has_both_kit_packages(self):
        self.assertEqual(self.report["missing_package"], [], "_tex alone renders a blank flag")

    def test_no_source_sheet_is_left_unused(self):
        self.assertEqual(self.report["unused_sheet"], [])

    def test_the_partly_used_sheets_cannot_be_merged_into_fewer(self):
        partial = self.report["partial_counts"]
        self.assertTrue(partial, "no partly used sheet found: the audit read nothing")
        self.assertEqual(len(partial), -(-sum(partial.values()) // 16), f"partly used sheets: {partial}")


if __name__ == "__main__":
    unittest.main()
