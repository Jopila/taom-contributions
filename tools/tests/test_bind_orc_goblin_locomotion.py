#!/usr/bin/env python3
"""Tests for tools/bind_orc_goblin_locomotion.py, the writer of the orc and goblin locomotion rows in the
LIVE Armory action_sets.xml (#791).

Run:  python -m unittest tools.tests.test_bind_orc_goblin_locomotion

The traps: binding a clip whose package is not on disk (a phantom animation= logs "Could not find
animation"), writing outside the two sets or a root-level <action> (a dedicated server dies on it),
losing the file's BOM or CRLF, a second run that changes something, and a derived set parsed before its
warrior set (base_set copies at parse time, so it would miss the rows). The live class is the reinstall
gate: the Armory is unversioned, and a reinstall drops the rows while audit_action_set_parity.py still
passes, because both sets inherit as_human_warrior.
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


def native_text():
    """as_human_warrior with every bound code: left-stance 1h/shield codes get their own clips."""
    rows = []
    for code in bo.CODES:
        clip = code.replace("act_", "").replace("walk_idle_unarmed", "stand_non_combat").replace("walk_idle_", "stand_")
        if code in ("act_walk_forward_unarmed_left_stance", "act_run_forward_unarmed_left_stance"):
            clip = clip.replace("_left_stance", "")
        rows.append(f'\t\t<action type="{code}" animation="{clip}" />')
    return ('<action_sets>\n\t<action_set id="as_human_warrior" skeleton="human_skeleton" movement_system="bipedal">\n'
            + "\n".join(rows) + '\n\t\t<action type="act_strike_bent_over" animation="strike_bent" />\n\t</action_set>\n</action_sets>\n')


def live_text(goblin_open='<action_set id="as_goblin_warrior"  base_set="as_human_warrior">' + NL + "\t</action_set>"):
    return NL.join([
        '<?xml version="1.0" encoding="utf-8"?>',
        "<action_sets>",
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
        self.clips = d / "clips"
        self.clips.mkdir()
        self.vanilla = bo.vanilla_clips(native_text())

    def tearDown(self):
        self.tmp.cleanup()

    def touch(self, *names):
        for n in names:
            (self.clips / f"{n}_anm.tpac").write_bytes(b"")

    def touch_all(self):
        for prefix, _ in bo.SETS.values():
            self.touch(*{prefix + c for c in self.vanilla.values()})

    def run_main(self, *extra):
        args = ["--live", str(self.live), "--native", str(self.native), "--clips-dir", str(self.clips), *extra]
        with mock.patch("_gamedir.game_or_kit_running", return_value=False), mock.patch("builtins.print"):
            return bo.main(args)


class TestPlan(Fixture):
    def test_left_stance_codes_take_their_own_vanilla_clip(self):
        self.assertEqual(self.vanilla["act_walk_forward_1h_left_stance"], "walk_forward_1h_left_stance")
        self.assertEqual(self.vanilla["act_walk_forward_unarmed_left_stance"], "walk_forward_unarmed")

    def test_binds_only_clips_on_disk(self):
        self.touch("taom_orc_walk_forward_unarmed")
        planned = bo.plan(self.vanilla, "unused", str(self.clips))
        orc_rows, orc_absent = planned["as_orc_warrior"]
        self.assertEqual(orc_rows, [("act_walk_forward_unarmed", "taom_orc_walk_forward_unarmed"),
                                    ("act_walk_forward_unarmed_left_stance", "taom_orc_walk_forward_unarmed")])
        self.assertNotIn("taom_orc_walk_forward_unarmed", orc_absent)
        self.assertEqual(planned["as_goblin_warrior"][0], [])

    def test_each_set_reads_its_own_folder(self):
        planned = bo.plan(self.vanilla, str(self.clips))
        self.assertEqual(len(planned["as_orc_warrior"][1]), 16, "nothing in <assets>/Race Test/Mordor/Orc/animations")


class TestApply(Fixture):
    def test_writes_rows_inside_both_sets_only(self):
        self.touch_all()
        self.assertEqual(self.run_main("--apply"), 0)
        raw = self.live.read_bytes()
        self.assertTrue(raw.startswith(b"\xef\xbb\xbf"), "BOM kept")
        text = raw.decode("utf-8-sig")
        self.assertEqual(text.count("\n"), text.count("\r\n"), "CRLF kept, no bare LF added")
        self.assertEqual(bo.check(text, bo.plan(self.vanilla, "unused", str(self.clips))), 0)
        self.assertEqual(text.count('animation="taom_orc_'), 18)
        self.assertEqual(text.count('animation="taom_goblin_'), 18)
        self.assertIn('act_walk_idle_unarmed" animation="stand_2"', text, "the villager's own row is untouched")
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
        args = ["--live", str(self.live), "--native", str(self.native), "--clips-dir", str(self.clips), "--apply"]
        with mock.patch("_gamedir.game_or_kit_running", return_value=True), mock.patch("builtins.print"):
            self.assertEqual(bo.main(args), 2)
        self.assertEqual(self.live.read_bytes(), before)

    def test_a_self_closing_set_is_refused(self):
        self.live.write_bytes(live_text('<action_set id="as_goblin_warrior" base_set="as_human_warrior" />').encode("utf-8"))
        self.touch_all()
        with self.assertRaises(SystemExit):
            self.run_main("--apply")


class TestCheck(Fixture):
    def test_counts_a_missing_row_and_a_derived_set_parsed_first(self):
        self.touch_all()
        planned = bo.plan(self.vanilla, "unused", str(self.clips))
        early = live_text().replace('\t<action_set id="as_orc_warrior"',
                                    '\t<action_set id="as_orc_early" base_set="as_orc_warrior" />' + NL
                                    + '\t<action_set id="as_orc_warrior"')
        with mock.patch("builtins.print"):
            # both sets lack all 18 rows (2) and as_orc_early is defined before its base (1)
            self.assertEqual(bo.check(early, planned), 3)


@unittest.skipUnless(os.path.isfile(bo.LIVE) and os.path.isdir(bo.ASSETS), "needs the LOTRLOME_Armory install; skipped, never faked")
class TestLiveArmory(unittest.TestCase):
    """The reinstall gate: the live sets carry exactly the rows the clips on disk call for."""

    def test_the_live_sets_match_the_plan(self):
        native = open(bo.NATIVE, "rb").read().decode("utf-8-sig")
        planned = bo.plan(bo.vanilla_clips(native), bo.ASSETS)
        self.assertTrue(all(rows for rows, _ in planned.values()), "no restyled clip on disk")
        live = open(bo.LIVE, "rb").read().decode("utf-8-sig")
        with mock.patch("builtins.print"):
            self.assertEqual(bo.check(live, planned), 0, "run python tools/bind_orc_goblin_locomotion.py --apply")


if __name__ == "__main__":
    unittest.main()
