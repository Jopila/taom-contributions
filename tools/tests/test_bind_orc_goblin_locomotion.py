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
import xml.etree.ElementTree as ET
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


# Native rows for the gait variants: every one of the 10 variant clips plays on a forward code, two of them also on a
# second code, a polearm run plays a variant clip, an idle code plays a variant clip (it must stay out), and a
# two-handed run plays a clip no variant covers.
VARIANT_NATIVE = [
    ("act_walk_forward_unarmed", "walk_forward_unarmed"),
    ("act_walk_forward_1h", "walk_forward_1h"),
    ("act_walk_forward_1h_left_stance", "walk_forward_1h_left_stance"),
    ("act_walk_forward_1h_with_shield", "walk_forward_1h_with_shield"),
    ("act_walk_forward_1h_with_shield_left_stance", "walk_forward_1h_with_shield_left_stance"),
    ("act_run_forward_unarmed", "run_forward_unarmed"),
    ("act_run_forward_1h", "run_forward_1h"),
    ("act_run_forward_1h_left_stance", "run_forward_1h_left_stance"),
    ("act_run_forward_1h_with_shield", "run_forward_1h_with_shield"),
    ("act_run_forward_1h_with_shield_left_stance", "run_forward_1h_with_shield_left_stance"),
    ("act_walk_forward_thrown", "walk_forward_1h"),
    ("act_run_forward_polearm_with_shield", "run_forward_1h_with_shield"),
    ("act_walk_idle_odd", "walk_forward_1h"),
    ("act_run_forward_2h", "run_forward_2h"),
    ("act_walk_idle_unarmed", "stand_non_combat"),
]
VARIANT_CODES = [r for r in VARIANT_NATIVE if r[0] not in ("act_walk_idle_odd", "act_run_forward_2h", "act_walk_idle_unarmed")]
B_SUB = os.path.join("Race Test", "Mordor", "Orc", "animations_b")
C_SUB = os.path.join("Race Test", "Mordor", "Orc", "animations_c")


class VariantFixture(Fixture):
    def setUp(self):
        super().setUp()
        rows = ['\t\t<action type="%s" animation="%s" />' % r for r in VARIANT_NATIVE]
        self.native.write_text('<action_sets>\n\t<action_set id="as_human_warrior" skeleton="human_skeleton">\n'
                               + "\n".join(rows) + "\n\t</action_set>\n</action_sets>\n", encoding="utf-8")
        self.human = bo.human_rows(self.native.read_text(encoding="utf-8"))

    def clips(self, sub, prefix, names=None):
        d = self.assets / sub
        d.mkdir(parents=True, exist_ok=True)
        for c in (bo.VARIANT_CLIPS if names is None else names):
            (d / f"{prefix}{c}_anm.tpac").write_bytes(b"")

    def vplan(self):
        return bo.plan_variants(self.human, str(self.assets))

    def text(self):
        return self.live.read_bytes().decode("utf-8-sig")

    def check_out(self, text=None):
        """(problems, printed text) of check_variants."""
        with mock.patch("builtins.print") as pr:
            n = bo.check_variants(text or self.text(), self.vplan(), str(self.assets))
        return n, "\n".join(" ".join(map(str, c.args)) for c in pr.call_args_list)

    def ids(self, text=None):
        return [s.get("id") for s in ET.fromstring((text or self.text()).encode("utf-8")).iter("action_set")]


class TestVariantPlan(VariantFixture):
    def test_binds_the_forward_codes_of_the_ten_clips_with_the_variant_prefix(self):
        self.clips(B_SUB, "taom_orc_b_")
        v, status, _codes, rows, missing = self.vplan()[0]
        self.assertEqual((v.vid, v.fid, status, missing), ("as_orc_warrior_b", None, "ready", []))
        self.assertEqual(rows, [(c, "taom_orc_b_" + a) for c, a in VARIANT_CODES], "Native order")
        self.assertEqual(len(rows), 12)

    def test_the_orc_monster_has_no_female_set_so_no_orc_variant_has_a_twin(self):
        # LOTRLOME_Armory monsters.xml declares no female_action_set on the orc Monster: a female orc plays
        # as_orc_warrior, so an as_orc_female_warrior_b would be unreachable
        self.assertEqual([v.fid for v in bo.VARIANTS], [None, None])
        self.assertEqual(bo.variant_ids(), {"as_orc_warrior_b", "as_orc_warrior_c"})

    def test_never_binds_an_idle_or_a_clip_outside_the_ten(self):
        self.clips(B_SUB, "taom_orc_b_")
        codes = {c for c, _ in self.vplan()[0][3]}
        self.assertNotIn("act_walk_idle_odd", codes, "an idle code that plays a variant clip stays variant A's")
        self.assertNotIn("act_walk_idle_unarmed", codes)
        self.assertNotIn("act_run_forward_2h", codes)
        self.assertFalse([c for c in codes if bo.IDLE_CODE_RE.match(c)])

    def test_each_variant_reads_its_own_folder_and_prefix(self):
        self.clips(B_SUB, "taom_orc_b_")
        self.clips(C_SUB, "taom_orc_b_")   # b package names in the c folder: not c's, and not "no clips yet"
        by = {p[0].vid: p for p in self.vplan()}
        self.assertEqual(by["as_orc_warrior_b"][1], "ready")
        self.assertEqual(by["as_orc_warrior_c"][1], "partial", "a folder of wrong-prefix clips is an error")
        self.assertEqual(len(by["as_orc_warrior_c"][4]), len(bo.VARIANT_CLIPS))

    def test_any_package_in_the_folder_makes_it_not_skipped(self):
        d = self.assets / B_SUB
        d.mkdir(parents=True)
        (d / "something_else_anm.tpac").write_bytes(b"")
        self.assertEqual(self.vplan()[0][1], "partial")
        (d / "something_else_anm.tpac").unlink()
        (d / "notes.txt").write_bytes(b"")
        self.assertEqual(self.vplan()[0][1], "skipped", "only *_anm.tpac counts as a clip")

    def test_skipped_without_clips_and_partial_with_some(self):
        self.assertEqual({p[1] for p in self.vplan()}, {"skipped"})
        self.assertEqual([p[3] for p in self.vplan()], [[], []])
        self.clips(B_SUB, "taom_orc_b_", bo.VARIANT_CLIPS[:3])
        p = self.vplan()[0]
        self.assertEqual((p[1], len(p[4])), ("partial", 7))


class TestVariantApply(VariantFixture):
    def apply(self, *extra, running=False):
        return self.run_main("--apply", *extra, running=running)

    def test_writes_the_variants_after_the_race_set_only(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        self.clips(C_SUB, "taom_orc_c_")
        self.assertEqual(self.apply(), 0)
        text = self.text()
        ids = self.ids(text)
        i = ids.index("as_orc_warrior")
        self.assertEqual(ids[i + 1:i + 3], ["as_orc_warrior_b", "as_orc_warrior_c"])
        self.assertLess(ids.index("as_orc_warrior_c"), ids.index("as_orc_female_warrior"), "block sits right after its base")
        self.assertNotIn("as_orc_female_warrior_", text, "the orc Monster has no female set: no twin")
        self.assertIn('<action_set id="as_orc_warrior_b" base_set="as_orc_warrior">', text)
        self.assertEqual(text.count('animation="taom_orc_b_'), 12)
        self.assertEqual(text.count('animation="taom_orc_c_'), 12)
        block = text.split("BEGIN gait variants")[1].split("END gait variants")[0]
        self.assertNotIn("act_walk_idle", block)
        self.assertNotIn("act_run_idle", block)
        self.assertTrue(self.live.read_bytes().startswith(b"\xef\xbb\xbf"), "BOM kept")
        self.assertEqual(text.count("\n"), text.count("\r\n"), "CRLF kept")
        self.assertEqual(self.run_main("--check"), 0)

    def test_a_second_run_changes_nothing(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        self.apply()
        once = self.live.read_bytes()
        self.assertEqual(self.apply(), 0)
        self.assertEqual(self.live.read_bytes(), once)

    def test_dry_run_writes_nothing_and_lists_the_variant(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        before = self.live.read_bytes()
        with mock.patch("builtins.print") as pr:
            rc = bo.main(["--live", str(self.live), "--native", str(self.native), "--assets", str(self.assets)])
        out = "\n".join(" ".join(map(str, c.args)) for c in pr.call_args_list)
        self.assertEqual(rc, 0)
        self.assertIn("as_orc_warrior_b: 12 codes bound", out)
        self.assertIn("as_orc_warrior_c: SKIPPED", out)
        self.assertEqual(self.live.read_bytes(), before)

    def test_skipped_variants_write_no_block(self):
        self.touch_all()
        self.apply()
        before = self.live.read_bytes()
        self.assertEqual(self.apply(), 0)
        self.assertEqual(self.live.read_bytes(), before)
        self.assertNotIn("gait variants", self.text())

    def test_a_partial_variant_is_an_input_error(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_", bo.VARIANT_CLIPS[:3])
        before = self.live.read_bytes()
        self.assertEqual(self.apply(), 2)
        self.assertEqual(self.live.read_bytes(), before)

    def test_refuses_while_the_game_runs(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        before = self.live.read_bytes()
        self.assertEqual(self.apply(running=True), 2)
        self.assertEqual(self.live.read_bytes(), before)

    def test_a_hand_written_variant_outside_the_block_is_an_input_error(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        hand = live_text().replace('\t<action_set id="as_orc_female_warrior"',
                                   '\t<action_set id="as_orc_warrior_b" base_set="as_orc_warrior" />' + NL
                                   + '\t<action_set id="as_orc_female_warrior"')
        self.live.write_bytes(hand.encode("utf-8"))
        self.assertEqual(self.apply(), 2)

    def test_dropping_variant_clips_needs_allow_shrink_and_restores_the_file(self):
        self.touch_all()
        self.apply()
        base_only = self.live.read_bytes()
        self.clips(B_SUB, "taom_orc_b_")
        self.apply()
        with_b = self.live.read_bytes()
        self.assertNotEqual(with_b, base_only)
        for f in (self.assets / B_SUB).iterdir():
            f.unlink()
        self.assertEqual(self.apply(), 2, "the variant's rows would go")
        self.assertEqual(self.live.read_bytes(), with_b)
        self.assertEqual(self.apply("--allow-shrink"), 0)
        self.assertEqual(self.live.read_bytes(), base_only, "the block goes and nothing else changes")


class TestVariantCheck(VariantFixture):
    def applied(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        self.run_main("--apply")
        return self.text()

    def test_every_variant_skipped_passes_with_the_explicit_line(self):
        n, out = self.check_out(live_text())
        self.assertEqual(n, 0)
        self.assertIn("no variant clips on disk yet", out)
        self.touch_all()
        self.run_main("--apply")
        self.assertEqual(self.run_main("--check"), 0)

    def test_a_variant_in_the_file_whose_clips_are_missing_fails(self):
        text = self.applied()
        for f in (self.assets / B_SUB).iterdir():
            f.unlink()
        n, out = self.check_out(text)
        self.assertGreaterEqual(n, 1)
        self.assertIn("clips missing", out)
        self.assertEqual(self.run_main("--check"), 1)

    def test_a_row_whose_package_vanished_fails(self):
        text = self.applied()
        (self.assets / B_SUB / "taom_orc_b_walk_forward_1h_anm.tpac").unlink()
        n, out = self.check_out(text)
        self.assertGreaterEqual(n, 1)
        self.assertIn("has no package", out)

    def test_an_idle_code_in_a_variant_fails(self):
        text = self.applied()
        bad = text.replace('<action type="act_run_forward_unarmed" animation="taom_orc_b_run_forward_unarmed" />',
                           '<action type="act_walk_idle_unarmed" animation="taom_orc_b_run_forward_unarmed" />', 1)
        self.assertNotEqual(bad, text)
        n, out = self.check_out(bad)
        self.assertGreaterEqual(n, 1)
        self.assertIn("overrides idle code act_walk_idle_unarmed", out)

    def test_a_missing_row_fails_complete_rows(self):
        text = self.applied()
        bad = text.replace('\t\t<action type="act_walk_forward_thrown" animation="taom_orc_b_walk_forward_1h" />' + NL, "", 1)
        self.assertNotEqual(bad, text)
        n, out = self.check_out(bad)
        self.assertEqual(n, 1)
        self.assertIn("missing act_walk_forward_thrown", out)

    def test_a_variant_defined_before_its_base_fails(self):
        text = self.applied()
        a = text.index("\t<!-- BEGIN gait variants")
        b = text.index("-->", text.index("<!-- END gait variants")) + 3
        block = text[a:b]
        moved = text[:a].rstrip() + text[b:]
        moved = moved.replace('\t<action_set id="as_human_warrior"', block + NL + NL + '\t<action_set id="as_human_warrior"', 1)
        n, out = self.check_out(moved)
        self.assertGreaterEqual(n, 1)
        self.assertIn("defined before its base as_orc_warrior", out)

    def test_a_variant_with_a_wrong_base_set_fails(self):
        text = self.applied()
        bad = text.replace('<action_set id="as_orc_warrior_b" base_set="as_orc_warrior">',
                           '<action_set id="as_orc_warrior_b" base_set="as_human_warrior">', 1)
        self.assertNotEqual(bad, text)
        n, out = self.check_out(bad)
        self.assertEqual(n, 1)
        self.assertIn("base_set 'as_human_warrior', expected as_orc_warrior", out)

    def test_a_non_forward_code_in_a_variant_fails(self):
        text = self.applied()
        bad = text.replace('<action type="act_run_forward_unarmed" animation="taom_orc_b_run_forward_unarmed" />',
                           '<action type="act_walk_strafe_left" animation="taom_orc_b_run_forward_unarmed" />', 1)
        self.assertNotEqual(bad, text)
        n, out = self.check_out(bad)
        self.assertGreaterEqual(n, 1)
        self.assertIn("carries act_walk_strafe_left, not a forward walk or run code", out)

    def test_a_typeless_row_is_a_problem_not_a_traceback(self):
        text = self.applied()
        # two extra rows, one with no type: sorting them used to compare None with a str
        bad = text.replace('<action type="act_run_forward_unarmed" animation="taom_orc_b_run_forward_unarmed" />',
                           '<action animation="taom_orc_b_run_forward_unarmed" />'
                           '<action type="act_run_forward_zz" animation="taom_orc_b_run_forward_unarmed" />', 1)
        self.assertNotEqual(bad, text)
        n, out = self.check_out(bad)
        self.assertGreaterEqual(n, 1)
        self.assertIn("missing act_run_forward_unarmed", out)
        # the same in a race set, through check() and through --check
        bare = live_text().replace('<action_set id="as_orc_warrior"  base_set="as_human_warrior">',
                                   '<action_set id="as_orc_warrior"  base_set="as_human_warrior">'
                                   '<action animation="taom_orc_walk_forward_unarmed" />'
                                   '<action type="act_walk_zz" animation="taom_orc_walk_forward_unarmed" />')
        with mock.patch("builtins.print"):
            self.assertGreaterEqual(bo.check(bare, self.plan()), 1)
        self.live.write_bytes(bare.encode("utf-8"))
        self.assertEqual(self.run_main("--check"), 1)

    def test_apply_over_a_typeless_row_does_not_traceback(self):
        self.touch_all()
        bare = live_text().replace('<action_set id="as_orc_warrior"  base_set="as_human_warrior">',
                                   '<action_set id="as_orc_warrior"  base_set="as_human_warrior">'
                                   '<action animation="x" /><action type="act_zz" animation="x" />')
        self.live.write_bytes(bare.encode("utf-8"))
        self.assertEqual(self.run_main("--apply", "--allow-shrink"), 0)

    def test_a_root_level_action_fails(self):
        text = self.applied()
        bad = text.replace("</action_sets>", '<action type="act_x" animation="y" /></action_sets>')
        with mock.patch("builtins.print"):
            self.assertEqual(bo.check(bad, self.plan()), 1)

    def test_a_ready_variant_missing_from_the_file_fails(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        n, out = self.check_out(live_text())
        self.assertEqual(n, 1)
        self.assertIn("is not in the file", out)

    def test_a_folder_of_wrong_prefix_clips_is_an_error_not_no_clips_yet(self):
        self.clips(C_SUB, "taom_orc_b_")
        n, out = self.check_out(live_text())
        self.assertEqual(n, 1)
        self.assertIn("clips missing in", out)
        self.assertNotIn("skip as_orc_warrior_c", out)
        self.touch_all()
        self.assertEqual(self.run_main("--apply"), 2, "--apply refuses a partial variant too")

    def test_require_variants_fails_while_a_variant_is_absent(self):
        self.touch_all()
        self.run_main("--apply")
        self.assertEqual(self.run_main("--check"), 0, "every variant skipped passes by default")
        self.assertEqual(self.run_main("--check", "--require-variants"), 1)
        n, out = self.check_out(self.text())
        self.assertEqual(n, 0)
        with mock.patch("builtins.print") as pr:
            n = bo.check_variants(self.text(), self.vplan(), str(self.assets), require=True)
        self.assertEqual(n, 2, "b and c are both absent")
        self.assertIn("--require-variants", "\n".join(" ".join(map(str, c.args)) for c in pr.call_args_list))

    def test_require_variants_passes_once_every_variant_is_imported_and_bound(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        self.clips(C_SUB, "taom_orc_c_")
        self.run_main("--apply")
        self.assertEqual(self.run_main("--check", "--require-variants"), 0)

    def test_require_variants_needs_check(self):
        with self.assertRaises(SystemExit) as cm, mock.patch("sys.stderr"):
            bo.main(["--live", str(self.live), "--require-variants"])
        self.assertEqual(cm.exception.code, 2)


GOBLIN_SUB = os.path.join("Race Test", "Mordor", "Goblin", "animations_b")
GOBLIN_B = bo.Variant("as_goblin_warrior_b", "as_goblin_female_warrior_b", "as_goblin_warrior", "taom_goblin_b_", GOBLIN_SUB)


class TestTwinMechanism(VariantFixture):
    """Goblins will need the twin (their Monster declares female_action_set, monsters.xml:1392), so the mechanism
    stays in the tool although no orc variant uses it. These run it on a patched goblin variant."""

    def setUp(self):
        super().setUp()
        p = mock.patch.object(bo, "VARIANTS", [GOBLIN_B])
        p.start()
        self.addCleanup(p.stop)
        self.touch_all()
        self.clips(GOBLIN_SUB, GOBLIN_B.prefix)

    def applied(self):
        self.assertEqual(self.run_main("--apply"), 0)
        return self.text()

    def test_writes_variant_then_empty_twin_after_the_race_set(self):
        text = self.applied()
        ids = self.ids(text)
        i = ids.index("as_goblin_warrior")
        self.assertEqual(ids[i + 1:i + 3], ["as_goblin_warrior_b", "as_goblin_female_warrior_b"])
        self.assertIn('<action_set id="as_goblin_female_warrior_b" base_set="as_goblin_warrior_b" />', text)
        self.assertEqual(self.run_main("--check"), 0)
        once = self.live.read_bytes()
        self.assertEqual(self.run_main("--apply"), 0)
        self.assertEqual(self.live.read_bytes(), once)

    def test_a_twin_before_its_variant_fails(self):
        text = self.applied()
        twin = '\t<action_set id="as_goblin_female_warrior_b" base_set="as_goblin_warrior_b" />' + NL
        bad = text.replace(twin, "", 1).replace('\t<action_set id="as_goblin_warrior_b"', twin + '\t<action_set id="as_goblin_warrior_b"', 1)
        self.assertNotEqual(bad, text)
        n, out = self.check_out(bad)
        self.assertEqual(n, 1)
        self.assertIn("defined before its variant", out)

    def test_a_twin_with_its_own_rows_or_wrong_base_fails(self):
        text = self.applied()
        bad = text.replace('<action_set id="as_goblin_female_warrior_b" base_set="as_goblin_warrior_b" />',
                           '<action_set id="as_goblin_female_warrior_b" base_set="as_goblin_warrior">'
                           '<action type="act_run_forward_unarmed" animation="x" /></action_set>', 1)
        n, out = self.check_out(bad)
        self.assertEqual(n, 2)
        self.assertIn("carries its own rows", out)

    def test_a_ready_variant_without_its_twin_fails(self):
        text = self.applied()
        bad = text.replace('\t<action_set id="as_goblin_female_warrior_b" base_set="as_goblin_warrior_b" />' + NL, "", 1)
        self.assertNotEqual(bad, text)
        n, out = self.check_out(bad)
        self.assertEqual(n, 1)
        self.assertIn("as_goblin_female_warrior_b is not in the file", out)


class TestDefinitionGuard(VariantFixture):
    """An id this tool owns must be defined once, inside the generated block. The engine merges a repeated action_set
    into its FIRST definition, so an earlier copy with a wrong base_set survives while the later one passes --check."""

    def written(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        self.run_main("--apply")
        return self.text()

    def problems(self, text):
        with mock.patch("builtins.print") as pr:
            n = bo.check_definitions(text)
        return n, "\n".join(" ".join(map(str, c.args)) for c in pr.call_args_list)

    def refuses(self, text):
        """--apply and --apply --allow-shrink both return 2 and leave the bytes alone; --check returns 1."""
        self.live.write_bytes(text.encode("utf-8"))
        before = self.live.read_bytes()
        self.assertEqual(self.run_main("--apply"), 2)
        self.assertEqual(self.run_main("--apply", "--allow-shrink"), 2)
        self.assertEqual(self.live.read_bytes(), before)
        self.assertEqual(self.run_main("--check"), 1)

    def test_a_clean_file_passes(self):
        self.assertEqual(self.problems(self.written())[0], 0)
        self.assertEqual(self.problems(live_text())[0], 0)

    def test_a_duplicate_race_set_is_refused_with_no_block_in_the_file(self):
        self.touch_all()
        dup = live_text().replace('\t<action_set id="as_orc_female_warrior"',
                                  '\t<action_set id="as_orc_warrior" base_set="as_human_warrior_base" />' + NL
                                  + '\t<action_set id="as_orc_female_warrior"')
        n, out = self.problems(dup)
        self.assertEqual(n, 1)
        self.assertIn("as_orc_warrior is defined 2 times", out)
        self.refuses(dup)

    def test_an_earlier_variant_with_a_wrong_base_is_refused_even_with_the_block_present(self):
        text = self.written()
        early = text.replace("\t<!-- BEGIN gait variants",
                             '\t<action_set id="as_orc_warrior_b" base_set="as_human_warrior" />' + NL
                             + "\t<!-- BEGIN gait variants", 1)
        self.assertNotEqual(early, text)
        n, out = self.problems(early)
        self.assertEqual(n, 2, "defined twice, and one copy is outside the block")
        self.assertIn("as_orc_warrior_b is defined 2 times", out)
        self.assertIn("as_orc_warrior_b is defined outside the generated block", out)
        self.refuses(early)

    def test_a_hand_written_variant_outside_an_existing_block_is_refused(self):
        text = self.written()   # only variant b has clips; c is hand-written below, outside the block
        hand = text.replace('\t<action_set id="as_orc_female_warrior"',
                            '\t<action_set id="as_orc_warrior_c" base_set="as_orc_warrior" />' + NL
                            + '\t<action_set id="as_orc_female_warrior"', 1)
        n, out = self.problems(hand)
        self.assertEqual(n, 1)
        self.assertIn("as_orc_warrior_c is defined outside the generated block", out)
        self.refuses(hand)

    def test_a_second_generated_block_is_refused(self):
        text = self.written()
        a = text.index("\t<!-- BEGIN gait variants")
        b = text.index("-->", text.index("<!-- END gait variants")) + 3
        twice = text[:b] + NL + NL + text[a:b] + text[b:]
        n, out = self.problems(twice)
        self.assertGreaterEqual(n, 1)
        self.assertIn("gait-variant block of as_orc_warrior", out)
        self.refuses(twice)

    def test_a_begin_marker_without_its_end_is_refused(self):
        text = self.written()
        torn = text.replace("<!-- END gait variants of as_orc_warrior -->", "", 1)
        n, out = self.problems(torn)
        self.assertGreaterEqual(n, 1)
        self.assertIn("gait-variant block of as_orc_warrior", out)
        self.refuses(torn)

    def test_a_commented_out_copy_is_not_a_definition(self):
        text = self.written()
        commented = text.replace("\t<!-- BEGIN gait variants",
                                 '\t<!-- <action_set id="as_orc_warrior_b" base_set="as_human_warrior" /> -->' + NL
                                 + "\t<!-- BEGIN gait variants", 1)
        self.assertEqual(self.problems(commented)[0], 0)


class TestForeignSetGuard(VariantFixture):
    def test_a_set_this_tool_does_not_own_between_the_markers_is_never_deleted(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        self.assertEqual(self.run_main("--apply"), 0)
        foreign = self.text().replace("\t<!-- END gait variants",
                                      '\t<action_set id="as_orc_hand_made" base_set="as_orc_warrior" />' + NL
                                      + "\t<!-- END gait variants", 1)
        self.assertIn("as_orc_hand_made", foreign)
        self.live.write_bytes(foreign.encode("utf-8"))
        before = self.live.read_bytes()
        self.assertEqual(self.run_main("--apply"), 2)
        self.assertEqual(self.run_main("--apply", "--allow-shrink"), 2)
        self.assertEqual(self.live.read_bytes(), before)

    def test_it_also_guards_the_removal_path(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        self.run_main("--apply")
        foreign = self.text().replace("\t<!-- END gait variants",
                                      '\t<action_set id="as_orc_hand_made" base_set="as_orc_warrior" />' + NL
                                      + "\t<!-- END gait variants", 1)
        self.live.write_bytes(foreign.encode("utf-8"))
        for f in (self.assets / B_SUB).iterdir():
            f.unlink()
        before = self.live.read_bytes()
        self.assertEqual(self.run_main("--apply", "--allow-shrink"), 2, "dropping the block would take the foreign set too")
        self.assertEqual(self.live.read_bytes(), before)

    def test_a_plain_rewrite_still_works_and_loses_only_variant_ids(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        self.run_main("--apply")
        self.clips(C_SUB, "taom_orc_c_")
        self.assertEqual(self.run_main("--apply"), 0)
        self.assertEqual(self.run_main("--check"), 0)


FOREIGN_INNER = ('\t<action_set id="foreign" base_set="important_base">' + NL
                 + '\t\t<action type="act_keep" animation="keep_me" />' + NL + "\t</action_set>" + NL)
FOREIGN_OUTER = '\t<action_set id="foreign" base_set="other_base" />' + NL


class TestForeignDefinitionGuard(VariantFixture):
    """The guard is about DEFINITIONS, not ids. The engine keeps the FIRST definition of a repeated action_set
    (Module.cs:1467-1490), so a foreign definition inside the replaced region that shares its id with a copy elsewhere
    must still stop the rewrite: the id survives, the definition (and its rows) does not (Codex F3, 2026-10-18)."""

    def written(self):
        self.touch_all()
        self.clips(B_SUB, "taom_orc_b_")
        self.assertEqual(self.run_main("--apply"), 0)
        return self.text()

    def counterexample(self):
        text = self.written()
        bad = text.replace("\t<!-- END gait variants", FOREIGN_INNER + "\t<!-- END gait variants", 1)
        bad = bad.replace("</action_sets>", FOREIGN_OUTER + "</action_sets>", 1)
        self.assertEqual(bad.count('id="foreign"'), 2)
        self.assertEqual(bad.count("keep_me"), 1)
        return bad

    def refused(self, text, *extra):
        self.live.write_bytes(text.encode("utf-8"))
        before = self.live.read_bytes()
        self.assertEqual(self.run_main("--apply", *extra), 2)
        self.assertEqual(self.live.read_bytes(), before, "bytes unchanged")
        self.assertIn(b"keep_me", self.live.read_bytes())

    def test_a_foreign_definition_in_the_block_is_refused_though_its_id_survives_elsewhere(self):
        bad = self.counterexample()
        self.refused(bad)
        self.refused(bad, "--allow-shrink")

    def test_check_fails_on_it_and_names_the_set(self):
        bad = self.counterexample()
        with mock.patch("builtins.print") as pr:
            n = bo.check_definitions(bad)
        out = "\n".join(" ".join(map(str, c.args)) for c in pr.call_args_list)
        self.assertGreaterEqual(n, 1)
        self.assertIn("foreign", out)
        self.assertIn("generated block", out)
        self.live.write_bytes(bad.encode("utf-8"))
        self.assertEqual(self.run_main("--check"), 1)

    def test_the_removal_path_refuses_it_too(self):
        bad = self.counterexample()
        for f in (self.assets / B_SUB).iterdir():
            f.unlink()
        self.refused(bad, "--allow-shrink")

    def test_a_foreign_definition_in_the_block_is_refused_with_no_copy_elsewhere(self):
        text = self.written()
        lone = text.replace("\t<!-- END gait variants", FOREIGN_INNER + "\t<!-- END gait variants", 1)
        self.refused(lone)
        self.live.write_bytes(lone.encode("utf-8"))
        self.assertEqual(self.run_main("--check"), 1)

    def test_the_owned_block_alone_is_not_a_problem(self):
        self.assertEqual(bo.definition_problems(self.written()), [])

    def test_a_lost_copy_of_an_unowned_id_is_refused_by_count(self):
        # two copies of an unowned id outside the block: the id set cannot tell one lost copy from none
        text = self.written()
        twice = text.replace("</action_sets>", FOREIGN_OUTER + FOREIGN_OUTER + "</action_sets>", 1)
        self.assertEqual(bo.definition_problems(twice), [])
        self.live.write_bytes(twice.encode("utf-8"))
        self.clips(C_SUB, "taom_orc_c_")   # makes the rewrite change the file
        before = self.live.read_bytes()

        def eats_a_copy(t, base, ready, nl):
            return t.replace(FOREIGN_OUTER, "", 1)

        with mock.patch.object(bo, "apply_variants", side_effect=eats_a_copy):
            self.assertEqual(self.run_main("--apply"), 2)
        self.assertEqual(self.live.read_bytes(), before)


@unittest.skipUnless(os.path.isfile(bo.LIVE) and os.path.isdir(bo.ASSETS), "needs the LOTRLOME_Armory install; skipped, never faked")
class TestLiveArmory(unittest.TestCase):
    """The reinstall gate: the live sets carry exactly the rows the clips on disk call for."""

    def test_the_live_sets_match_the_plan(self):
        human = bo.human_rows(open(bo.NATIVE, "rb").read().decode("utf-8-sig"))
        planned = bo.plan(human, bo.ASSETS)
        live = open(bo.LIVE, "rb").read().decode("utf-8-sig")
        with mock.patch("builtins.print"):
            n = (bo.check_definitions(live) + bo.check(live, planned)
                 + bo.check_variants(live, bo.plan_variants(human, bo.ASSETS), bo.ASSETS))
            self.assertEqual(n, 0, "run python tools/bind_orc_goblin_locomotion.py --apply")


if __name__ == "__main__":
    unittest.main()
