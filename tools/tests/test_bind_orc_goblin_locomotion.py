#!/usr/bin/env python3
"""Tests for tools/bind_orc_goblin_locomotion.py, the writer of the orc and goblin locomotion rows in the
LIVE Armory action_sets.xml (#791).

Run:  python -m unittest tools.tests.test_bind_orc_goblin_locomotion

The traps: binding a clip whose package is not on disk (a phantom animation= logs "Could not find
animation"), reading a commented-out Native row as a live one, writing outside the two sets or a root-level
<action> (a dedicated server dies on it), losing the file's BOM or CRLF, a second run that changes something,
a run that silently drops rows when clips go missing, a derived set parsed before its own parent (base_set
copies at parse time, so it would miss the rows), and a check that passes a reinstalled Armory because it
has neither the clips nor the rows. The live class is the reinstall gate: the Armory is unversioned, and a
reinstall drops the rows while audit_action_set_parity.py still passes, because both sets inherit
as_human_warrior.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bind_orc_goblin_locomotion as bo  # noqa: E402

NL = "\r\n"

# Native's as_human_warrior in miniature: two codes share a clip, a thrown walk borrows the 1h clip, a running
# idle plays a stand, a two-handed walk has no restyled clip, a non-locomotion code plays a restyled stand, and a
# commented-out row names another clip for act_walk_forward_1h before the live row does.
NATIVE_ROWS = [
    ("act_walk_forward_unarmed", "walk_forward_unarmed"),
    ("act_walk_forward_unarmed_left_stance", "walk_forward_unarmed"),
    ("act_walk_idle_unarmed", "stand_non_combat"),
    ("act_run_idle_unarmed", "stand_non_combat"),
    ("act_walk_forward_1h", "walk_forward_1h"),
    ("act_walk_forward_1h_left_stance", "walk_forward_1h_left_stance"),
    ("act_walk_forward_thrown", "walk_forward_1h"),
    ("act_walk_forward_2h", "walk_forward_2h"),
    ("act_inventory_idle", "stand_non_combat"),
]
RESTYLED = ["walk_forward_unarmed", "stand_non_combat", "walk_forward_1h", "walk_forward_1h_left_stance"]
BOUND = [r for r in NATIVE_ROWS if r[0] not in ("act_walk_forward_2h", "act_inventory_idle")]


def native_text():
    rows = ['\t\t<action type="%s" animation="%s" />' % r for r in NATIVE_ROWS]
    rows.insert(4, '\t\t<!-- <action type="act_walk_forward_1h" animation="walk_forward_1h_old" /> -->')
    return ('<action_sets>\n\t<action_set id="as_human_warrior" skeleton="human_skeleton" movement_system="bipedal">\n'
            + "\n".join(rows) + "\n\t</action_set>\n</action_sets>\n")


def live_text(goblin_open='<action_set id="as_goblin_warrior"  base_set="as_human_warrior">' + NL + "\t</action_set>"):
    return NL.join([
        '<?xml version="1.0" encoding="utf-8"?>',
        "<action_sets>",
        '\t<!-- <action_set id="as_orc_warrior" base_set="as_human_warrior"> (an old copy) -->',
        '\t<action_set id="as_human_warrior" base_set="as_human_warrior_base">',
        '\t\t<action type="act_howdah_stand_bow" animation="howdah_stand_bow" />',
        "\t</action_set>",
        '\t<action_set id="as_orc_warrior"  base_set="as_human_warrior">',
        "\t</action_set>",
        '\t<action_set id="as_orc_female_warrior" base_set="as_orc_warrior" />',
        '\t<action_set id="as_orc_villager" base_set="as_orc_warrior">',
        '\t\t<action type="act_walk_idle_unarmed" animation="stand_2" />',
        "\t</action_set>",
        "\t" + goblin_open,
        "</action_sets>",
        "",
    ])


class Fixture(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        d = Path(self.tmp.name)
        self.native = d / "native.xml"
        self.native.write_text(native_text(), encoding="utf-8")
        self.live = d / "action_sets.xml"
        self.live.write_bytes(b"\xef\xbb\xbf" + live_text().encode("utf-8"))
        self.assets = d / "Assets"
        self.folder = {}
        for set_id, (prefix, sub) in bo.SETS.items():
            self.folder[prefix] = self.assets / sub
            self.folder[prefix].mkdir(parents=True)
        self.human = bo.human_rows(native_text())

    def tearDown(self):
        self.tmp.cleanup()

    def touch(self, prefix, *clips, folder_of=None):
        """Write <prefix><clip>_anm.tpac into the folder of folder_of (default: the prefix's own set)."""
        for c in clips:
            (self.folder[folder_of or prefix] / f"{prefix}{c}_anm.tpac").write_bytes(b"")

    def touch_all(self):
        for prefix, _ in bo.SETS.values():
            self.touch(prefix, *RESTYLED)

    def plan(self):
        return bo.plan(self.human, str(self.assets))

    def run_main(self, *extra, running=False):
        args = ["--live", str(self.live), "--native", str(self.native), "--assets", str(self.assets), *extra]
        with mock.patch.object(bo, "game_or_kit_running", return_value=running), mock.patch("builtins.print"):
            return bo.main(args)


class TestPlan(Fixture):
    def test_binds_every_walk_and_run_code_whose_clip_is_restyled(self):
        self.touch_all()
        rows, unbound = self.plan()["as_orc_warrior"]
        self.assertEqual(rows, [(c, "taom_orc_" + a) for c, a in BOUND], "Native order, one row per code")
        self.assertEqual(unbound, [])

    def test_a_commented_native_row_is_not_read(self):
        self.touch_all()
        rows, _ = self.plan()["as_goblin_warrior"]
        self.assertEqual([r for r in rows if r[0] == "act_walk_forward_1h"],
                         [("act_walk_forward_1h", "taom_goblin_walk_forward_1h")])

    def test_codes_without_a_restyled_clip_or_outside_locomotion_stay_human(self):
        self.touch_all()
        codes = {c for c, _ in self.plan()["as_orc_warrior"][0]}
        self.assertNotIn("act_walk_forward_2h", codes)
        self.assertNotIn("act_inventory_idle", codes, "plays a restyled stand, but is not a walk or run code")

    def test_binds_only_packages_on_disk(self):
        self.touch("taom_orc_", "walk_forward_unarmed")
        planned = self.plan()
        self.assertEqual(planned["as_orc_warrior"][0],
                         [("act_walk_forward_unarmed", "taom_orc_walk_forward_unarmed"),
                          ("act_walk_forward_unarmed_left_stance", "taom_orc_walk_forward_unarmed")])
        self.assertEqual(planned["as_goblin_warrior"][0], [])

    def test_each_set_reads_only_its_own_folder(self):
        self.touch("taom_orc_", *RESTYLED, folder_of="taom_goblin_")
        self.touch("taom_goblin_", *RESTYLED, folder_of="taom_orc_")
        planned = self.plan()
        self.assertEqual(planned["as_orc_warrior"][0], [], "orc clips in the goblin folder bind nothing")
        self.assertEqual(planned["as_goblin_warrior"][0], [], "goblin clips in the orc folder bind nothing")
        self.touch("taom_goblin_", *RESTYLED)
        planned = self.plan()
        self.assertEqual(planned["as_orc_warrior"][0], [])
        self.assertEqual(len(planned["as_goblin_warrior"][0]), len(BOUND))

    def test_reports_a_package_no_code_plays(self):
        self.touch("taom_orc_", "walk_forward_unarmed", "stand_2h")
        self.assertEqual(self.plan()["as_orc_warrior"][1], ["taom_orc_stand_2h"])


class TestApply(Fixture):
    def test_writes_rows_inside_both_sets_only(self):
        self.touch_all()
        self.assertEqual(self.run_main("--apply"), 0)
        raw = self.live.read_bytes()
        self.assertTrue(raw.startswith(b"\xef\xbb\xbf"), "BOM kept")
        text = raw.decode("utf-8-sig")
        self.assertEqual(text.count("\n"), text.count("\r\n"), "CRLF kept, no bare LF added")
        self.assertEqual(bo.check(text, self.plan()), 0)
        self.assertEqual(text.count('animation="taom_orc_'), len(BOUND))
        self.assertEqual(text.count('animation="taom_goblin_'), len(BOUND))
        self.assertIn('act_walk_idle_unarmed" animation="stand_2"', text, "the villager's own row is untouched")
        self.assertIn('<!-- <action_set id="as_orc_warrior" base_set="as_human_warrior"> (an old copy) -->' + NL
                      + '\t<action_set id="as_human_warrior"', text, "the commented-out copy is not the target")
        self.assertEqual(len(list(self.live.parent.glob("action_sets.xml.bak-orc-goblin-bind-*"))), 1)

    def test_a_second_run_changes_nothing(self):
        self.touch_all()
        self.run_main("--apply")
        once = self.live.read_bytes()
        self.assertEqual(self.run_main("--apply"), 0)
        self.assertEqual(self.live.read_bytes(), once)

    def test_dry_run_writes_nothing(self):
        self.touch_all()
        before = self.live.read_bytes()
        self.assertEqual(self.run_main(), 0)
        self.assertEqual(self.live.read_bytes(), before)

    def test_no_clips_on_disk_leaves_the_stubs_byte_identical(self):
        before = self.live.read_bytes()
        self.run_main("--apply")
        self.assertEqual(self.live.read_bytes(), before)

    def test_refuses_while_the_game_or_kit_runs(self):
        self.touch_all()
        before = self.live.read_bytes()
        self.assertEqual(self.run_main("--apply", running=True), 2)
        self.assertEqual(self.live.read_bytes(), before)

    def test_refuses_to_drop_bound_rows_unless_allowed(self):
        self.touch_all()
        self.run_main("--apply")
        bound = self.live.read_bytes()
        (self.folder["taom_orc_"] / "taom_orc_stand_non_combat_anm.tpac").unlink()
        self.assertEqual(self.run_main("--apply"), 2, "two orc rows would go")
        self.assertEqual(self.live.read_bytes(), bound)
        self.assertEqual(self.run_main("--apply", "--allow-shrink"), 0)
        text = self.live.read_bytes().decode("utf-8-sig")
        self.assertEqual(text.count('animation="taom_orc_'), len(BOUND) - 2)
        self.assertEqual(text.count('animation="taom_goblin_'), len(BOUND))

    def test_a_self_closing_set_is_an_input_error(self):
        self.live.write_bytes(live_text('<action_set id="as_goblin_warrior" base_set="as_human_warrior" />').encode("utf-8"))
        self.touch_all()
        before = self.live.read_bytes()
        self.assertEqual(self.run_main("--apply"), 2)
        self.assertEqual(self.live.read_bytes(), before)

    def test_a_missing_input_file_is_an_input_error(self):
        self.native.unlink()
        self.assertEqual(self.run_main(), 2)

    def test_corrupt_input_is_an_input_error_not_a_traceback(self):
        self.native.write_text(native_text().replace('type="act_walk_forward_2h" ', ""), encoding="utf-8")
        self.assertEqual(self.run_main("--check"), 2, "a Native row with no type")
        self.native.write_text(native_text(), encoding="utf-8")
        self.live.write_bytes(live_text().encode("utf-8") + b"\xff\xfe")
        self.assertEqual(self.run_main("--check"), 2, "a live file that is not UTF-8")


class TestCheck(Fixture):
    def test_counts_a_missing_row_and_a_derived_set_parsed_first(self):
        self.touch_all()
        early = live_text().replace('\t<action_set id="as_orc_warrior"  ',
                                    '\t<action_set id="as_orc_early" base_set="as_orc_warrior" />' + NL
                                    + '\t<action_set id="as_orc_warrior"  ')
        with mock.patch("builtins.print"):
            # both sets lack all their rows (2) and as_orc_early is defined before its base (1)
            self.assertEqual(bo.check(early, self.plan()), 3)

    def test_checks_every_link_of_a_chain(self):
        self.touch_all()
        self.run_main("--apply")
        text = self.live.read_bytes().decode("utf-8-sig")
        # as_orc_deep comes after as_orc_warrior but before its own parent, as_orc_villager
        deep = text.replace('\t<action_set id="as_orc_female_warrior"',
                            '\t<action_set id="as_orc_deep" base_set="as_orc_villager" />' + NL
                            + '\t<action_set id="as_orc_female_warrior"')
        with mock.patch("builtins.print"):
            self.assertEqual(bo.check(deep, self.plan()), 1)

    def test_a_set_with_no_clips_fails_unless_allowed(self):
        text = self.live.read_bytes().decode("utf-8-sig")
        with mock.patch("builtins.print"):
            self.assertEqual(bo.check(text, self.plan()), 2, "a reinstall drops both the clips and the rows")
            self.assertEqual(bo.check(text, self.plan(), allow_no_clips=True), 0)
        self.assertEqual(self.run_main("--check"), 1)
        self.assertEqual(self.run_main("--check", "--allow-no-clips"), 0)

    def test_a_missing_set_is_a_problem(self):
        self.touch_all()
        text = live_text("").replace(NL + "\t" + NL, NL)
        with mock.patch("builtins.print"):
            # as_orc_warrior lacks its rows (1) and as_goblin_warrior is gone (1)
            self.assertEqual(bo.check(text, self.plan()), 2)


@unittest.skipUnless(os.path.isfile(bo.LIVE) and os.path.isdir(bo.ASSETS), "needs the LOTRLOME_Armory install; skipped, never faked")
class TestLiveArmory(unittest.TestCase):
    """The reinstall gate: the live sets carry exactly the rows the clips on disk call for."""

    def test_the_live_sets_match_the_plan(self):
        human = bo.human_rows(open(bo.NATIVE, "rb").read().decode("utf-8-sig"))
        planned = bo.plan(human, bo.ASSETS)
        live = open(bo.LIVE, "rb").read().decode("utf-8-sig")
        with mock.patch("builtins.print"):
            self.assertEqual(bo.check(live, planned), 0, "run python tools/bind_orc_goblin_locomotion.py --apply")


if __name__ == "__main__":
    unittest.main()
