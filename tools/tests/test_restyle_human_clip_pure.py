"""Pure parts of tools/blender/restyle_human_clip.py, with bpy and mathutils stubbed (no Blender).

Reachable without real mathutils: load_spec, identity_spec, cap_violations, clip_kind, error_out_dir and
clear_stale_verdict, and the limp's pure parts: the limp and run spec blocks (merge, caps, refusals), contact
detection, the time warp and its inverse, step point mapping and the ankle forward path rebuild. The pose math (IK,
quaternion means, the source sampler, the fractional frame sampling) needs mathutils and is proven by the headless
parity run, not here.
"""
import json
import math
import os
import sys
import tempfile
import unittest
from unittest import mock

BLENDER_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "blender")
VARIANTS_DIR = os.path.join(BLENDER_DIR, "locomotion_variants")


def _import_restyle():
    stubs = {"bpy": mock.MagicMock(), "mathutils": mock.MagicMock()}
    with mock.patch.dict(sys.modules, stubs), mock.patch.object(sys, "path", [BLENDER_DIR] + sys.path):
        for name in ("restyle_human_clip", "retarget_mannequin_to_human"):
            sys.modules.pop(name, None)
        module = __import__("restyle_human_clip")
        for name in ("restyle_human_clip", "retarget_mannequin_to_human"):
            sys.modules.pop(name, None)
    return module


rs = _import_restyle()


class SpecFileCase(unittest.TestCase):
    def setUp(self):
        self._dir = tempfile.TemporaryDirectory()
        self.addCleanup(self._dir.cleanup)

    def write(self, data):
        path = os.path.join(self._dir.name, "spec.json")
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(data, fh)
        return path


class LoadSpecTests(SpecFileCase):
    def test_defaults_merge_under_the_file(self):
        spec = rs.load_spec(self.write({"clip_prefix": "x_", "stride_scale": 1.2, "idle": {"head_yaw_deg": 4.0}}))
        self.assertEqual(spec["clip_prefix"], "x_")
        self.assertEqual(spec["stride_scale"], 1.2)
        self.assertEqual(spec["bob_scale"], rs.DEFAULTS["bob_scale"])
        self.assertEqual(spec["idle"]["head_yaw_deg"], 4.0)
        self.assertEqual(spec["idle"]["breath_cycles"], rs.DEFAULTS["idle"]["breath_cycles"])

    def test_load_does_not_mutate_defaults(self):
        before = json.dumps(rs.DEFAULTS, sort_keys=True)
        rs.load_spec(self.write({"clip_prefix": "x_", "trunk_flex_deg": {"spine": 5.0}, "idle": {"head_cycles": 3}}))
        self.assertEqual(json.dumps(rs.DEFAULTS, sort_keys=True), before)

    def test_underscore_keys_are_notes(self):
        spec = rs.load_spec(self.write({"clip_prefix": "x_", "_note": "hi", "idle": {"_why": "n", "head_cycles": 2}}))
        self.assertNotIn("_note", spec)
        self.assertNotIn("_why", spec["idle"])
        self.assertEqual(spec["idle"]["head_cycles"], 2)

    def test_unknown_top_level_key_is_refused_by_name_and_file(self):
        path = self.write({"clip_prefix": "x_", "stride_scal": 1.3})
        with self.assertRaises(SystemExit) as ctx:
            rs.load_spec(path)
        self.assertIn("stride_scal", str(ctx.exception))
        self.assertIn(path, str(ctx.exception))

    def test_unknown_idle_key_is_refused(self):
        path = self.write({"clip_prefix": "x_", "idle": {"head_yaw": 4.0}})
        with self.assertRaises(SystemExit) as ctx:
            rs.load_spec(path)
        self.assertIn("head_yaw", str(ctx.exception))

    def test_missing_clip_prefix_is_refused(self):
        with self.assertRaises(SystemExit) as ctx:
            rs.load_spec(self.write({"stride_scale": 1.1}))
        self.assertIn("clip_prefix", str(ctx.exception))

    def test_shipped_variants_load(self):
        for name in ("orc.json", "goblin.json"):
            with self.subTest(name):
                spec = rs.load_spec(os.path.join(VARIANTS_DIR, name))
                self.assertTrue(spec["clip_prefix"])
                if name != "orc.json":   # the main orc is over the combat caps on purpose (restyled with --allow-over-cap)
                    self.assertEqual(rs.cap_violations(spec), [])
                else:
                    self.assertEqual(len(rs.cap_violations(spec)), 2)

    def test_identity_spec_keeps_only_the_prefix(self):
        spec = rs.load_spec(self.write({"clip_prefix": "x_", "stride_scale": 1.4}))
        ident = rs.identity_spec(spec)
        self.assertEqual(ident["clip_prefix"], "x_")
        self.assertEqual(ident["stride_scale"], rs.DEFAULTS["stride_scale"])


class CapViolationsTests(unittest.TestCase):
    def spec(self, **kw):
        base = json.loads(json.dumps(rs.DEFAULTS))
        base.update(kw)
        return base

    def test_defaults_are_under_the_caps(self):
        self.assertEqual(rs.cap_violations(self.spec()), [])

    def test_at_the_caps_is_allowed(self):
        spec = self.spec(trunk_flex_deg={"spine": rs.CAPS["trunk_flex_total_deg"]},
                         clavicle_drop_deg=-rs.CAPS["clavicle_deg"], clavicle_protract_deg=rs.CAPS["clavicle_deg"])
        self.assertEqual(rs.cap_violations(spec), [])

    def test_trunk_flex_sums_absolute_values(self):
        spec = self.spec(trunk_flex_deg={"spine": 20.0, "spine1": -8.0, "spine2": 3.0})
        out = rs.cap_violations(spec)
        self.assertEqual(len(out), 1)
        self.assertIn("trunk flex 31.0", out[0])

    def test_clavicle_caps_are_per_key_and_signed_either_way(self):
        out = rs.cap_violations(self.spec(clavicle_drop_deg=-10.5, clavicle_protract_deg=11.0))
        self.assertEqual(len(out), 2)
        self.assertTrue(out[0].startswith("clavicle_drop_deg"))
        self.assertTrue(out[1].startswith("clavicle_protract_deg"))


class ClipKindTests(unittest.TestCase):
    def test_gaits(self):
        for name in ("walk_forward_1h", "run_forward_unarmed", "walk_", "run_x"):
            self.assertEqual(rs.clip_kind(name), "gait", name)

    def test_idles(self):
        for name in ("stand_1h", "stand_unarmed"):
            self.assertEqual(rs.clip_kind(name), "idle", name)

    def test_other_names_are_refused(self):
        for name in ("jump_loop", "walking", "running_fast", "standing", "xwalk_a", "Walk_a", ""):
            self.assertIsNone(rs.clip_kind(name), name)


class ErrorHandlerHelperTests(unittest.TestCase):
    def test_out_forms(self):
        self.assertEqual(rs.error_out_dir(["--out", "a"]), "a")
        self.assertEqual(rs.error_out_dir(["--out=b", "--only", "x"]), "b")
        self.assertEqual(rs.error_out_dir(["--variant", "v", "--out", "c", "--only", "x"]), "c")

    def test_absent_or_trailing_out_falls_back_to_cwd(self):
        self.assertEqual(rs.error_out_dir([]), os.getcwd())
        self.assertEqual(rs.error_out_dir(["--variant", "v"]), os.getcwd())
        self.assertEqual(rs.error_out_dir(["--variant", "v", "--out"]), os.getcwd())
        self.assertEqual(rs.error_out_dir(["--out="]), os.getcwd())

    def test_clear_stale_verdict_removes_only_the_verdict_files(self):
        with tempfile.TemporaryDirectory() as d:
            keep = os.path.join(d, "restyle_report.json")
            for name in ("restyle_report.json", "restyle_report.json.DONE", "restyle_error.log"):
                open(os.path.join(d, name), "w").close()
            rs.clear_stale_verdict(d)
            self.assertEqual(os.listdir(d), [os.path.basename(keep)])
            rs.clear_stale_verdict(d)   # nothing left to remove: no error


class LimpSpecTests(SpecFileCase):
    def load(self, **kw):
        data = {"clip_prefix": "x_"}
        data.update(kw)
        return rs.load_spec(self.write(data))

    def test_no_limp_block_is_neutral_and_no_run_block_is_empty(self):
        spec = self.load()
        self.assertEqual(spec["limp"], rs.LIMP_DEFAULTS)
        self.assertEqual(spec["run"], {})
        self.assertFalse(rs.limp_cycle_active(spec["limp"]))
        self.assertFalse(rs.limp_static_active(spec["limp"]))

    def test_limp_keys_merge_over_the_defaults(self):
        spec = self.load(limp={"strong_side": "l", "timing_split": 0.56, "_note": "n"})
        self.assertEqual(spec["limp"]["strong_side"], "l")
        self.assertEqual(spec["limp"]["timing_split"], 0.56)
        self.assertEqual(spec["limp"]["knee_bow_deg"], rs.LIMP_DEFAULTS["knee_bow_deg"])
        self.assertNotIn("_note", spec["limp"])
        self.assertTrue(rs.limp_cycle_active(spec["limp"]))

    def test_load_does_not_mutate_the_limp_defaults(self):
        before = json.dumps(rs.LIMP_DEFAULTS, sort_keys=True)
        self.load(limp={"timing_split": 0.6}, run={"timing_split": 0.58})
        self.assertEqual(json.dumps(rs.LIMP_DEFAULTS, sort_keys=True), before)
        self.assertEqual(rs.DEFAULTS["run"], {})
        self.assertEqual(rs.DEFAULTS["limp"], rs.LIMP_DEFAULTS)

    def test_static_only_limp_needs_no_contacts(self):
        spec = self.load(limp={"low_shoulder_deg": 3.0, "knee_bow_deg": 10.0})
        self.assertTrue(rs.limp_static_active(spec["limp"]))
        self.assertFalse(rs.limp_cycle_active(spec["limp"]))

    def test_every_cycle_key_alone_activates_the_warp_path(self):
        for key in rs.LIMP_CYCLE_KEYS:
            with self.subTest(key):
                limp = dict(rs.LIMP_DEFAULTS)
                limp[key] = 0.56 if key == "timing_split" else limp[key] + 1.0
                self.assertTrue(rs.limp_cycle_active(limp))

    def test_unknown_limp_key_is_refused_by_name_and_file(self):
        path = self.write({"clip_prefix": "x_", "limp": {"weak_lift_scal": 0.4}})
        with self.assertRaises(SystemExit) as ctx:
            rs.load_spec(path)
        self.assertIn("weak_lift_scal", str(ctx.exception))
        self.assertIn(path, str(ctx.exception))

    def test_unknown_run_key_is_refused(self):
        with self.assertRaises(SystemExit) as ctx:
            rs.load_spec(self.write({"clip_prefix": "x_", "run": {"pelvis_tilt": 16.0}}))
        self.assertIn("pelvis_tilt", str(ctx.exception))

    def test_run_may_not_hold_idle_or_nested_blocks(self):
        for key in ("idle", "limp", "run", "clip_prefix"):
            with self.subTest(key):
                with self.assertRaises(SystemExit):
                    rs.load_spec(self.write({"clip_prefix": "x_", "run": {key: {}}}))

    def test_run_accepts_top_level_and_limp_keys(self):
        spec = self.load(run={"pelvis_tilt_deg": 16.0, "timing_split": 0.58})
        self.assertEqual(spec["run"], {"pelvis_tilt_deg": 16.0, "timing_split": 0.58})

    def test_bad_side_split_and_scale_are_refused(self):
        bad = ({"strong_side": "x"}, {"lead_shoulder_side": "left"}, {"timing_split": 0.9}, {"timing_split": 0.2},
               {"weak_lift_scale": -0.1}, {"weak_swing_out_m": "a lot"}, {"low_arm_swing_scale": "big"})
        for limp in bad:
            with self.subTest(limp):
                with self.assertRaises(SystemExit):
                    rs.load_spec(self.write({"clip_prefix": "x_", "limp": limp}))
        with self.assertRaises(SystemExit):
            rs.load_spec(self.write({"clip_prefix": "x_", "run": {"timing_split": 0.9}}))

    def test_weak_trail_is_a_cycle_key_with_a_zero_default(self):
        self.assertEqual(rs.LIMP_DEFAULTS["weak_trail_m"], 0.0)
        spec = self.load(limp={"weak_trail_m": 0.15}, run={"weak_trail_m": 0.2})
        self.assertTrue(rs.limp_cycle_active(spec["limp"]))
        self.assertEqual(rs.spec_for_clip(spec, "run_forward_1h")["limp"]["weak_trail_m"], 0.2)
        self.assertEqual(rs.spec_for_clip(spec, "walk_forward_1h")["limp"]["weak_trail_m"], 0.15)

    def test_mirror_sides_must_be_a_bool(self):
        with self.assertRaises(SystemExit):
            rs.load_spec(self.write({"clip_prefix": "x_", "mirror_sides": "yes"}))
        self.assertIs(self.load()["mirror_sides"], False)
        self.assertIs(self.load(mirror_sides=True)["mirror_sides"], True)

    def test_the_lead_shoulder_keys_default_to_neutral_and_activate_the_static_posture(self):
        self.assertEqual(rs.LIMP_DEFAULTS["lead_shoulder_yaw_deg"], 0.0)
        self.assertEqual(rs.LIMP_DEFAULTS["lead_shoulder_protract_deg"], 0.0)
        for key in ("lead_shoulder_yaw_deg", "lead_shoulder_protract_deg"):
            limp = dict(rs.LIMP_DEFAULTS)
            limp[key] = 5.0
            self.assertTrue(rs.limp_static_active(limp), key)
            self.assertFalse(rs.limp_cycle_active(limp), key)

    def test_the_left_shoulder_can_be_the_low_one(self):
        spec = self.load(limp={"lead_shoulder_side": "l", "low_shoulder_deg": 3.0, "low_arm_forward_deg": 6.0})
        self.assertEqual(spec["limp"]["lead_shoulder_side"], "l")
        self.assertTrue(rs.limp_static_active(spec["limp"]))
        self.assertEqual(self.load(run={"lead_shoulder_side": "l"})["run"], {"lead_shoulder_side": "l"})

    def test_a_null_low_arm_swing_scale_follows_arm_swing_scale(self):
        self.assertIsNone(self.load(limp={"low_arm_swing_scale": None})["limp"]["low_arm_swing_scale"])
        self.assertTrue(rs.limp_static_active(self.load(limp={"low_arm_swing_scale": 1.6})["limp"]))


class RunStrideTests(SpecFileCase):
    def test_a_run_block_that_changes_the_stride_is_refused(self):
        path = self.write({"clip_prefix": "x_", "stride_scale": 0.85, "run": {"stride_scale": 0.95}})
        with self.assertRaises(SystemExit) as ctx:
            rs.load_spec(path)
        self.assertIn("stride_scale", str(ctx.exception))
        self.assertIn(path, str(ctx.exception))

    def test_a_run_block_restating_the_same_stride_is_fine(self):
        spec = rs.load_spec(self.write({"clip_prefix": "x_", "stride_scale": 0.85, "run": {"stride_scale": 0.85}}))
        self.assertEqual(rs.spec_for_clip(spec, "run_forward_1h")["stride_scale"], 0.85)

    def test_a_run_stride_over_the_default_base_is_refused_too(self):
        with self.assertRaises(SystemExit):
            rs.load_spec(self.write({"clip_prefix": "x_", "run": {"stride_scale": 0.9}}))


class SpecForClipTests(SpecFileCase):
    def spec(self):
        return rs.load_spec(self.write({"clip_prefix": "x_", "pelvis_tilt_deg": 8.0, "stride_scale": 0.95,
                                        "limp": {"timing_split": 0.56, "strong_side": "r"},
                                        "run": {"pelvis_tilt_deg": 16.0, "timing_split": 0.58}}))

    def test_a_run_clip_gets_the_run_block_over_top_level_and_limp_keys(self):
        merged = rs.spec_for_clip(self.spec(), "run_forward_1h")
        self.assertEqual(merged["pelvis_tilt_deg"], 16.0)
        self.assertEqual(merged["limp"]["timing_split"], 0.58)
        self.assertEqual(merged["limp"]["strong_side"], "r")
        self.assertEqual(merged["stride_scale"], 0.95)

    def test_walk_and_stand_clips_keep_the_base_spec(self):
        spec = self.spec()
        for clip in ("walk_forward_1h", "stand_1h"):
            self.assertIs(rs.spec_for_clip(spec, clip), spec)
            self.assertEqual(spec["pelvis_tilt_deg"], 8.0)
            self.assertEqual(spec["limp"]["timing_split"], 0.56)

    def test_merging_does_not_mutate_the_spec_and_is_idempotent(self):
        spec = self.spec()
        before = json.dumps(spec, sort_keys=True)
        merged = rs.spec_for_clip(spec, "run_forward_unarmed")
        self.assertEqual(json.dumps(spec, sort_keys=True), before)
        self.assertEqual(merged["run"], {})
        self.assertEqual(rs.spec_for_clip(merged, "run_forward_unarmed"), merged)

    def test_a_spec_without_a_run_block_is_returned_as_is(self):
        spec = rs.load_spec(self.write({"clip_prefix": "x_"}))
        self.assertIs(rs.spec_for_clip(spec, "run_forward_1h"), spec)

    def test_a_run_trunk_dict_replaces_the_base_dict_whole(self):
        spec = rs.load_spec(self.write({"clip_prefix": "x_", "trunk_flex_deg": {"spine": 5.0, "spine1": 5.0},
                                        "run": {"trunk_flex_deg": {"spine2": 9.0}}}))
        self.assertEqual(rs.spec_for_clip(spec, "run_x")["trunk_flex_deg"], {"spine2": 9.0})


class MetricKeyTests(SpecFileCase):
    def load(self, **kw):
        data = {"clip_prefix": "x_"}
        data.update(kw)
        return rs.load_spec(self.write(data))

    def test_the_lead_shoulder_follows_the_strong_side_unless_named(self):
        self.assertEqual(rs.limp_sides(self.load(limp={"strong_side": "l"})["limp"]), ("l", "l"))
        self.assertEqual(rs.limp_sides(self.load(limp={"strong_side": "r"})["limp"]), ("r", "r"))
        self.assertEqual(rs.limp_sides(self.load(limp={"strong_side": "r", "lead_shoulder_side": "l"})["limp"]), ("r", "l"))
        self.assertIsNone(rs.LIMP_DEFAULTS["lead_shoulder_side"])

    def test_mirror_swaps_the_strong_side_and_leaves_a_following_lead_following(self):
        merged = rs.spec_for_clip(self.load(limp={"strong_side": "r"}, mirror_sides=True), "walk_forward_1h")
        self.assertIsNone(merged["limp"]["lead_shoulder_side"])
        self.assertEqual(rs.limp_sides(merged["limp"]), ("l", "l"))

    def test_the_metric_keys_default_to_off(self):
        d = rs.LIMP_DEFAULTS
        self.assertEqual((d["knee_bow_m"], d["weak_lift_max_m"], d["weak_toe_down_deg"], d["weak_hip_hike_m"],
                          d["weight_over_foot"], d["foot_outer_edge_deg"], d["arm_forward_deg"], d["min_track_half_m"]),
                         (0.0,) * 8)
        self.assertEqual(d["knee_max_deg"], 180.0)
        self.assertFalse(rs.limp_cycle_active(self.load()["limp"]))
        self.assertFalse(rs.limp_static_active(self.load()["limp"]))

    def test_the_cycle_metrics_need_contacts_and_the_static_ones_do_not(self):
        for key in ("weak_lift_max_m", "weak_toe_down_deg", "weak_hip_hike_m", "weight_over_foot", "foot_outer_edge_deg"):
            limp = dict(rs.LIMP_DEFAULTS, **{key: 0.05})
            self.assertTrue(rs.limp_cycle_active(limp), key)
        for key, val in (("knee_bow_m", 0.07), ("arm_forward_deg", 10.0), ("min_track_half_m", 0.14), ("knee_max_deg", 160.0)):
            limp = dict(rs.LIMP_DEFAULTS, **{key: val})
            self.assertTrue(rs.limp_static_active(limp), key)
            self.assertFalse(rs.limp_cycle_active(limp), key)

    def test_a_run_block_may_carry_metric_keys(self):
        spec = self.load(run={"knee_bow_m": 0.09, "weak_trail_m": 0.15})
        merged = rs.spec_for_clip(spec, "run_forward_1h")
        self.assertEqual(merged["limp"]["knee_bow_m"], 0.09)
        self.assertEqual(spec["limp"]["knee_bow_m"], 0.0)

    def test_out_of_range_metric_values_are_refused(self):
        for limp in ({"knee_max_deg": 30.0}, {"knee_max_deg": 200.0}, {"knee_bow_m": -0.01}, {"weak_lift_max_m": -1.0},
                     {"min_track_half_m": -0.1}, {"weight_over_foot": -0.3}, {"knee_bow_m": True}):
            with self.subTest(limp):
                with self.assertRaises(SystemExit):
                    rs.load_spec(self.write({"clip_prefix": "x_", "limp": limp}))

    def test_hip_rise_adds_the_roll_and_the_raise(self):
        rise = rs.hike_rise_nominal(0.02, 10.0, 0.09)
        self.assertAlmostEqual(rise, 0.09 * math.sin(math.radians(10.0)) + 0.02)
        self.assertEqual(rs.hike_rise_nominal(0.0, 0.0, 0.09), 0.0)
        self.assertAlmostEqual(rs.hike_rise_nominal(0.0, -10.0, 0.09), 0.09 * math.sin(math.radians(10.0)))

    def test_the_achieved_block_names_only_what_was_asked_for(self):
        track = {"edge": 6.0, "toe_down": 15.0, "knee_max": 150.0, "bow": [0.05, 0.09], "splay": [10.0, 20.0],
                 "half": 0.141, "weak_lift": 0.03, "weak_lift_toe": 0.02, "pelvis_x": [0.0, 0.05, -0.04],
                 "hip_rise": 0.04, "drop": 0.04}
        knee = {"lowered": 0.01, "pulled": 0.0}
        out = rs._achieved(track, knee, dict(rs.LIMP_DEFAULTS))
        self.assertEqual(out["knee_bow_m"], {"min": 0.05, "mean": 0.07})
        self.assertEqual(out["thigh_splay_deg"], {"mean": 15.0, "max": 20.0})
        self.assertEqual(out["pelvis_lateral_travel_m"], 0.09)
        self.assertNotIn("weak_lift_max_m", out)
        self.assertNotIn("foot_outer_edge_deg", out)
        asked = dict(rs.LIMP_DEFAULTS, weak_lift_max_m=0.03, foot_outer_edge_deg=6.0, weak_hip_hike_m=0.04,
                     weak_toe_down_deg=15.0, strong_drop_m=0.04)
        out = rs._achieved(track, knee, asked)
        for key in ("weak_lift_max_m", "weak_toe_height_max_m", "foot_outer_edge_deg", "weak_hip_rise_m",
                    "weak_toe_down_deg", "pelvis_extra_drop_max_m"):
            self.assertIn(key, out)


class MirrorTests(SpecFileCase):
    def spec(self, **kw):
        data = {"clip_prefix": "x_", "limp": {"strong_side": "r", "lead_shoulder_side": "l"}, "mirror_sides": True}
        data.update(kw)
        return rs.load_spec(self.write(data))

    def test_mirror_sides_swaps_the_strong_and_the_lead_side(self):
        merged = rs.spec_for_clip(self.spec(), "walk_forward_1h")
        self.assertEqual((merged["limp"]["strong_side"], merged["limp"]["lead_shoulder_side"]), ("l", "r"))
        self.assertIs(merged["mirror_sides"], False)

    def test_mirroring_twice_returns_the_original_sides_and_does_not_mutate(self):
        spec = self.spec()
        before = json.dumps(spec, sort_keys=True)
        merged = rs.spec_for_clip(spec, "walk_forward_1h")
        self.assertEqual(json.dumps(spec, sort_keys=True), before)
        self.assertEqual(rs.spec_for_clip(merged, "walk_forward_1h"), merged)   # already mirrored: no second swap
        again = rs.spec_for_clip(dict(merged, mirror_sides=True), "walk_forward_1h")
        self.assertEqual((again["limp"]["strong_side"], again["limp"]["lead_shoulder_side"]), ("r", "l"))

    def test_mirror_applies_to_run_clips_after_the_run_block(self):
        spec = self.spec(run={"strong_side": "l"})
        merged = rs.spec_for_clip(spec, "run_forward_1h")
        self.assertEqual((merged["limp"]["strong_side"], merged["limp"]["lead_shoulder_side"]), ("r", "r"))

    def test_without_mirror_sides_the_spec_is_returned_as_is(self):
        spec = self.spec(mirror_sides=False)
        self.assertIs(rs.spec_for_clip(spec, "walk_forward_1h"), spec)

    def test_the_other_limp_values_are_untouched_by_the_mirror(self):
        spec = self.spec(limp={"strong_side": "r", "lead_shoulder_side": "l", "timing_split": 0.56, "low_shoulder_deg": 3.0})
        merged = rs.spec_for_clip(spec, "walk_forward_1h")
        self.assertEqual(merged["limp"]["timing_split"], 0.56)
        self.assertEqual(merged["limp"]["low_shoulder_deg"], 3.0)


class LimpCapTests(SpecFileCase):
    def spec(self, **kw):
        data = {"clip_prefix": "x_"}
        data.update(kw)
        return rs.load_spec(self.write(data))

    def test_low_shoulder_adds_to_the_clavicle_drop_cap(self):
        over = rs.cap_violations(self.spec(clavicle_drop_deg=7.0, limp={"low_shoulder_deg": 4.0}))
        self.assertEqual(len(over), 1)
        self.assertIn("clavicle_drop_deg + low_shoulder_deg 11.0", over[0])

    def test_clavicle_plus_low_shoulder_exactly_at_the_cap_is_allowed(self):
        self.assertEqual(rs.cap_violations(self.spec(clavicle_drop_deg=7.0, limp={"low_shoulder_deg": 3.0})), [])

    def test_lead_protraction_adds_to_the_clavicle_protract_cap(self):
        self.assertEqual(rs.cap_violations(self.spec(clavicle_protract_deg=7.0, limp={"lead_shoulder_protract_deg": 3.0})), [])
        over = rs.cap_violations(self.spec(clavicle_protract_deg=10.0, limp={"lead_shoulder_protract_deg": 3.0}))
        self.assertEqual(len(over), 1)
        self.assertIn("clavicle_protract_deg + lead_shoulder_protract_deg 13.0", over[0])

    def test_lead_yaw_has_its_own_cap_and_is_not_trunk_flexion(self):
        self.assertEqual(rs.cap_violations(self.spec(limp={"lead_shoulder_yaw_deg": 20.0})), [])
        over = rs.cap_violations(self.spec(limp={"lead_shoulder_yaw_deg": -20.5}))
        self.assertEqual(len(over), 1)
        self.assertIn("lead_shoulder_yaw_deg", over[0])
        full = self.spec(trunk_flex_deg={"spine": 10.0, "spine1": 10.0, "spine2": 10.0}, limp={"lead_shoulder_yaw_deg": 15.0})
        self.assertEqual(rs.cap_violations(full), [])

    def test_the_yaw_split_and_the_counter_turn_each_add_up_to_the_whole_yaw(self):
        self.assertAlmostEqual(sum(rs.LEAD_YAW_SPLIT.values()), 1.0)
        self.assertAlmostEqual(sum(rs.LEAD_YAW_COUNTER.values()), 1.0)

    def test_lateral_bend_caps_the_sum_of_the_static_and_the_stomp_lean(self):
        self.assertEqual(rs.cap_violations(self.spec(limp={"low_shoulder_lean_deg": 3.0, "strong_lean_deg": 5.0})), [])
        over = rs.cap_violations(self.spec(limp={"low_shoulder_lean_deg": 3.0, "strong_lean_deg": 5.5}))
        self.assertEqual(len(over), 1)
        self.assertIn("lateral", over[0])
        self.assertIn("8.5", over[0])

    def test_the_run_block_is_checked_merged(self):
        spec = self.spec(clavicle_drop_deg=6.0, limp={"low_shoulder_deg": 3.0}, run={"low_shoulder_deg": 5.0})
        self.assertEqual(rs.cap_violations(spec), [])
        over = rs.all_cap_violations(spec)
        self.assertEqual(len(over), 1)
        self.assertTrue(over[0].startswith("run: clavicle_drop_deg + low_shoulder_deg 11.0"))

    def test_a_run_trunk_flex_over_the_cap_is_flagged(self):
        spec = self.spec(run={"trunk_flex_deg": {"spine": 15.0, "spine1": 10.0, "spine2": 10.0}})
        self.assertEqual(rs.cap_violations(spec), [])
        self.assertIn("run: trunk flex 35.0", rs.all_cap_violations(spec)[0])

    def test_pelvis_tilt_is_not_capped_in_a_run(self):
        spec = self.spec(pelvis_tilt_deg=8.0, run={"pelvis_tilt_deg": 25.0})
        self.assertEqual(rs.all_cap_violations(spec), [])

    def test_shoulder_roll_is_a_pulse_and_not_capped(self):
        self.assertEqual(rs.cap_violations(self.spec(clavicle_protract_deg=9.0, limp={"shoulder_roll_deg": 8.0})), [])


def synthetic_foot(period, a, length, speed, z_lift=0.12, y_mid=0.0):
    """period + 1 samples of an in-place foot: planted (z = 0.09) and moving back at `speed` for `length` frames
    from frame a, then swung forward with a smooth raised arc."""
    y_a = y_mid + speed * length / 2.0
    y_b = y_a - speed * length
    swing = period - length
    ys, zs = [], []
    for k in range(period):
        rel = (k - a) % period
        if rel <= length:
            ys.append(y_a - speed * rel)
            zs.append(0.09)
        else:
            t = (rel - length) / swing
            ys.append(y_b + (y_a - y_b) * (1.0 - math.cos(math.pi * t)) / 2.0)
            zs.append(0.09 + z_lift * math.sin(math.pi * t))
    ys.append(ys[0])
    zs.append(zs[0])
    return ys, zs


class ContactDetectionTests(unittest.TestCase):
    def test_finds_the_planted_run_and_nothing_in_the_swing(self):
        ys, zs = synthetic_foot(36, 2, 14, 0.046)
        idx = rs.contact_indices(zs, ys, True)
        self.assertEqual(idx, list(range(2, 16)))

    def test_a_foot_that_is_planted_but_not_moving_back_is_not_in_contact(self):
        n = 12
        self.assertEqual(rs.contact_indices([0.1] * (n + 1), [0.0] * (n + 1), True), [])

    def test_runs_wrap_past_the_end_of_the_cycle(self):
        self.assertEqual(rs.cyclic_runs([34, 35, 0, 1, 2, 10, 11], 36), [(10, 2), (34, 5)])
        self.assertEqual(rs.cyclic_runs([], 36), [])
        self.assertEqual(rs.cyclic_runs(list(range(8)), 8), [(0, 8)])

    def test_longest_stance_takes_the_longest_run_and_counts_runs(self):
        st, n, runs = rs.longest_stance([34, 35, 0, 1, 2, 10, 11], 36)
        self.assertEqual((st, n, runs), (34, 5, 2))

    def test_a_foot_with_no_stable_contact_is_refused(self):
        with self.assertRaises(ValueError):
            rs.longest_stance([4, 5], 36)
        with self.assertRaises(ValueError):
            rs.longest_stance([], 36)

    def test_plateau_speed_is_the_median_back_rate(self):
        ys, zs = synthetic_foot(36, 20, 14, 0.046)
        st, n, _ = rs.longest_stance(rs.contact_indices(zs, ys, True), 36)
        self.assertEqual(rs.plateau_window(ys, st, n, 36), (20, 14, rs.plateau_window(ys, st, n, 36)[2]))
        self.assertAlmostEqual(rs.plateau_window(ys, st, n, 36)[2], 0.046, places=9)

    def test_a_wrapping_stance_has_the_same_plateau_speed(self):
        ys, zs = synthetic_foot(36, 30, 14, 0.05)
        st, n, _ = rs.longest_stance(rs.contact_indices(zs, ys, True), 36)
        self.assertEqual((st, n), (30, 14))
        self.assertAlmostEqual(rs.plateau_window(ys, st, n, 36)[2], 0.05, places=9)

    def test_slow_landing_and_take_off_frames_are_trimmed_from_the_plateau(self):
        period = 12
        ys = [0.0]
        for v in (0.03, 0.05, 0.05, 0.05, 0.05, 0.03):   # contact indices 0..5, a roll at each end
            ys.append(ys[-1] - v)
        ys += [ys[-1] + 0.05] * 6
        ys = ys[:period] + [ys[0]]
        start, length, speed = rs.plateau_window(ys, 0, 6, period)
        self.assertEqual((start, length), (1, 4))
        self.assertAlmostEqual(speed, 0.05)

    def test_a_plateau_under_three_frames_falls_back_to_the_whole_contact(self):
        ys = [0.0, -0.05, -0.10, -0.12, -0.14, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]
        start, length, _ = rs.plateau_window(ys, 0, 4, 12)
        self.assertEqual((start, length), (0, 4))


class TimeWarpTests(unittest.TestCase):
    def warp(self, strong=2 / 36.0, source=0.5, split=0.56):
        return rs.Warp(strong, source, split)

    def test_the_strong_contact_keeps_its_phase_and_the_weak_one_lands_at_the_split(self):
        w = self.warp()
        s = 2 / 36.0
        self.assertAlmostEqual(w.forward(s), s)
        self.assertAlmostEqual(w.forward(s + 0.56), s + 0.5)
        self.assertAlmostEqual(w.inverse(s), s)
        self.assertAlmostEqual(w.inverse(s + 0.5), s + 0.56)

    def test_inverse_undoes_forward_over_several_cycles(self):
        w = self.warp(strong=0.3, source=0.46, split=0.6)
        for i in range(-30, 80):
            u = i / 37.0
            self.assertAlmostEqual(w.inverse(w.forward(u)), u, places=9)
            self.assertAlmostEqual(w.forward(w.inverse(u)), u, places=9)

    def test_the_map_is_cyclic_and_strictly_monotone(self):
        for split in (0.52, 0.56, 0.6, 0.62, 0.7):
            w = self.warp(split=split)
            prev = w.forward(-1.0)
            for i in range(-359, 1080):
                u = i / 360.0
                cur = w.forward(u)
                self.assertGreater(cur, prev)
                self.assertAlmostEqual(w.forward(u + 1.0), cur + 1.0, places=9)
                self.assertGreater(w.derivative(u), 0.0)
                prev = cur

    def test_the_derivative_is_continuous_across_every_contact_and_the_seam(self):
        # a piecewise-linear map jumped here by 0.2 to 0.3 (a rate of 0.86 then 1.19); this one is smooth
        for strong, source, split in ((2 / 36.0, 0.5, 0.56), (2 / 24.0, 0.5, 0.62), (0.0, 0.5, 0.6), (0.97, 0.47, 0.58)):
            w = self.warp(strong, source, split)
            weak = strong + split
            for u in (strong, weak, 0.0, 1.0, 0.5):
                eps = 1e-6
                left = (w.forward(u) - w.forward(u - eps)) / eps
                right = (w.forward(u + eps) - w.forward(u)) / eps
                self.assertAlmostEqual(left, right, places=4, msg="u=%s" % u)
                self.assertAlmostEqual(left, w.derivative(u), places=4)
            self.assertAlmostEqual(w.derivative(0.0), w.derivative(1.0), places=12)   # across the loop seam
            step = [w.derivative(i / 720.0) for i in range(721)]
            self.assertLess(max(abs(b - a) for a, b in zip(step, step[1:])), 0.02)    # and no kink between

    def test_an_even_split_is_the_identity(self):
        w = self.warp(source=0.5, split=0.5)
        for i in range(0, 36):
            self.assertAlmostEqual(w.forward(i / 36.0), i / 36.0, places=9)
        for i, x in enumerate(rs.output_positions(36, w)):
            self.assertAlmostEqual(x, float(i), places=7)

    def test_a_late_weak_contact_slows_the_first_half_and_speeds_the_second(self):
        w = self.warp(source=0.5, split=0.58)
        s = 2 / 36.0
        self.assertLess(w.derivative(s + 0.29), 1.0)     # between the contacts the source is played slower
        self.assertGreater(w.derivative(s + 0.79), 1.0)  # and faster after the weak one

    def test_output_positions_stay_inside_the_loop(self):
        pos = rs.output_positions(36, self.warp())
        self.assertEqual(len(pos), 36)
        self.assertTrue(all(0.0 <= x < 36.0 for x in pos))
        wraps = sum(1 for a, b in zip(pos, pos[1:] + pos[:1]) if b < a)
        self.assertEqual(wraps, 1)   # the source phase advances through exactly one cycle

    def test_degenerate_splits_are_refused(self):
        for source, split in ((0.0, 0.5), (1.0, 0.5), (0.5, 0.0), (0.5, 1.0)):
            with self.assertRaises(ValueError):
                rs.Warp(0.1, source, split)

    def test_a_split_the_map_cannot_reach_monotonically_is_refused(self):
        with self.assertRaises(ValueError):
            rs.Warp(0.1, 0.5, 0.7 + 0.2)   # a 0.9 split against 0.5: 2 pi |R| is past 1

    def test_the_report_row_rebuilds_the_same_map(self):
        w = self.warp(strong=0.0833333, source=0.5, split=0.62)
        again = rs.Warp.from_report(w.report())
        for u in (0.0, 0.2, 0.7, 1.3):
            self.assertAlmostEqual(w.forward(u), again.forward(u), places=6)

    def test_step_points_map_through_the_warp_and_stay_ordered(self):
        w = self.warp()
        mapped = rs.map_step_points([0.4, 0.898039222], w)
        self.assertAlmostEqual(w.forward(mapped[0]), 0.4, places=9)
        self.assertAlmostEqual(w.forward(mapped[1]), 0.898039222, places=9)
        self.assertLess(mapped[0], mapped[1])
        self.assertGreater(mapped[0], 0.4)   # the first half is played slower, so the plant lands later

    def test_unused_step_points_are_skipped_and_an_even_warp_keeps_the_rest(self):
        w = self.warp(source=0.5, split=0.5)
        self.assertEqual([round(v, 9) for v in rs.map_step_points([0.4, 0.9, -1.0, -1.0], w)], [0.4, 0.9])
        self.assertEqual(rs.map_step_points([-1.0, -1.0, -1.0, -1.0], w), [])

    def test_a_point_that_wraps_past_one_comes_back_into_the_cycle(self):
        w = self.warp(strong=0.9, source=0.5, split=0.6)
        for m in rs.map_step_points([0.05, 0.95], w):
            self.assertTrue(0.0 <= m < 1.0)

    def test_step_points_are_mapped_in_place_never_sorted(self):
        w = rs.Warp(2 / 24.0, 0.5, 0.62)   # the mirrored run's split
        mapped, wrapped = rs.map_step_points_checked([0.4, 0.99], w)
        self.assertTrue(wrapped)                                   # 0.99 plays past 1.0 and wraps to the start
        self.assertAlmostEqual(w.forward(mapped[0]), 0.4, places=9)  # X stays the image of vanilla X
        self.assertLess(mapped[1], mapped[0])                      # the wrapped Y is NOT swapped into first place
        self.assertEqual(rs.map_step_points([0.4, 0.99], w), mapped)

    def test_no_wrap_is_not_flagged(self):
        w = rs.Warp(2 / 36.0, 0.5, 0.56)
        mapped, wrapped = rs.map_step_points_checked([0.4, 0.898039222], w)
        self.assertFalse(wrapped)
        self.assertLess(mapped[0], mapped[1])

    def test_an_unused_point_keeps_the_others_in_place(self):
        w = rs.Warp(2 / 36.0, 0.5, 0.56)
        mapped, _ = rs.map_step_points_checked([-1.0, 0.7, -1.0, 0.3], w)
        self.assertEqual(len(mapped), 2)
        self.assertAlmostEqual(w.forward(mapped[0]), 0.7, places=9)
        self.assertAlmostEqual(w.forward(mapped[1]), 0.3, places=9)


class LoopClosureTests(unittest.TestCase):
    def test_a_seam_under_one_degree_is_already_closed(self):
        self.assertFalse(rs.loop_closure_plan(0.19))
        self.assertFalse(rs.loop_closure_plan(0.999))

    def test_the_three_vanilla_run_deltas_are_closed_by_the_tool(self):
        for delta in (1.1, 1.54, 5.21):
            self.assertTrue(rs.loop_closure_plan(delta), delta)

    def test_a_seam_past_the_threshold_is_refused(self):
        for delta in (rs.LOOP_CLOSE_MAX_DEG, 6.5, 40.0):
            with self.assertRaises(ValueError) as ctx:
                rs.loop_closure_plan(delta)
            self.assertIn("deg", str(ctx.exception))

    def test_the_closing_weights_rise_smoothly_to_exactly_one(self):
        w = rs.closure_weights(rs.CLOSURE_FRAMES)
        self.assertEqual(len(w), rs.CLOSURE_FRAMES)
        self.assertEqual(w[-1], 1.0)
        self.assertTrue(all(b > a for a, b in zip(w, w[1:])))
        self.assertTrue(0.0 < w[0] < 0.1)
        steps = [b - a for a, b in zip([0.0] + w, w)]
        self.assertLess(max(steps) - min(steps), 0.3)   # no single frame takes the whole correction


class LimpPlanTests(unittest.TestCase):
    P = 36
    SPEED = 0.046

    def feet(self, strong_a=2, weak_a=20, length=14):
        ry, rz = synthetic_foot(self.P, strong_a, length, self.SPEED)
        ly, lz = synthetic_foot(self.P, weak_a, length, self.SPEED)
        return {"r_foot": ry, "l_foot": ly}, {"r_foot": rz, "l_foot": lz}

    def plan(self, split=0.56, **kw):
        y, z = self.feet(**kw)
        return rs.plan_contacts(y, z, self.P, "r_foot", "l_foot", split)

    def test_the_source_stances_and_split_are_found(self):
        plan = self.plan()
        self.assertEqual(plan["stance_src"], {"r_foot": (2.0, 16.0), "l_foot": (20.0, 34.0)})
        self.assertEqual(plan["contact_src"], plan["stance_src"])
        self.assertAlmostEqual(plan["source_split"], 0.5)
        self.assertAlmostEqual(plan["planted_speed"], self.SPEED, places=9)
        self.assertEqual(plan["runs"], {"r_foot": 1, "l_foot": 1})

    def test_the_strong_contact_stays_and_the_weak_contact_moves_to_the_split(self):
        plan = self.plan(split=0.56)
        a_s = plan["contact_out"]["r_foot"][0]
        a_w = plan["contact_out"]["l_foot"][0]
        self.assertAlmostEqual(a_s, 2.0, places=9)
        self.assertAlmostEqual((a_w - a_s) % self.P, 0.56 * self.P, places=9)

    def test_the_strong_stance_gets_longer_and_the_weak_one_shorter(self):
        plan = self.plan(split=0.56)
        ls = plan["stance_out"]["r_foot"][1] - plan["stance_out"]["r_foot"][0]
        lw = plan["stance_out"]["l_foot"][1] - plan["stance_out"]["l_foot"][0]
        self.assertGreater(ls, 14.0)
        self.assertLess(lw, 14.0)

    def test_a_late_weak_foot_in_the_source_keeps_its_offset_out_of_the_split(self):
        plan = self.plan(split=0.6, weak_a=22)
        self.assertAlmostEqual(plan["source_split"], 20 / 36.0)
        self.assertAlmostEqual((plan["stance_out"]["l_foot"][0] - plan["stance_out"]["r_foot"][0]) % self.P,
                               0.6 * self.P, places=9)

    def test_a_stance_that_wraps_the_loop_is_planned(self):
        plan = self.plan(strong_a=30, weak_a=12)
        a, b = plan["stance_out"]["r_foot"]
        self.assertAlmostEqual(a, 30.0, places=9)
        self.assertAlmostEqual((plan["stance_out"]["l_foot"][0] - a) % self.P, 0.56 * self.P, places=9)

    def test_ankle_path_falls_at_exactly_the_planted_speed_inside_both_stances(self):
        y, _ = self.feet()
        plan = self.plan(split=0.56)
        speed = self.SPEED * 0.95
        for foot in ("r_foot", "l_foot"):
            track = rs.ankle_y_track(y[foot], plan["stance_src"][foot], plan["stance_out"][foot], speed, 0.01, self.P)
            frames = rs.stance_frames(plan["stance_out"][foot], self.P)
            self.assertGreater(len(frames), 8)
            for k in frames:
                self.assertAlmostEqual(track[(k + 1) % self.P] - track[k], -speed, places=9, msg="%s %d" % (foot, k))

    def test_a_foot_trailing_the_pelvis_keeps_the_planted_speed(self):
        y, _ = self.feet()
        plan = self.plan(split=0.58)
        foot = "l_foot"
        args = (y[foot], plan["stance_src"][foot], plan["stance_out"][foot], 0.045)
        base = rs.ankle_y_track(*args, 0.0, self.P, plan["warp"])
        trailed = rs.ankle_y_track(*args, 0.0 - 0.15, self.P, plan["warp"])
        for k in range(self.P):
            self.assertAlmostEqual(trailed[k], base[k] - 0.15, places=9)
        for k in rs.stance_frames(plan["stance_out"][foot], self.P):
            self.assertAlmostEqual(trailed[(k + 1) % self.P] - trailed[k], -0.045, places=9)

    def test_the_swing_read_through_the_time_map_still_meets_both_stances(self):
        y, _ = self.feet()
        plan = self.plan(split=0.58)
        for foot in ("r_foot", "l_foot"):
            a, b = plan["stance_out"][foot]
            track = rs.ankle_y_track(y[foot], plan["stance_src"][foot], (a, b), 0.045, 0.0, self.P, plan["warp"])
            steps = [abs(track[(k + 1) % self.P] - track[k]) for k in range(self.P)]
            self.assertLess(max(steps), 0.2)
            frames = rs.stance_frames((a, b), self.P)
            for k in frames:
                self.assertAlmostEqual(track[(k + 1) % self.P] - track[k], -0.045, places=9)

    def test_the_stance_is_centred_on_y_mid(self):
        y, _ = self.feet()
        plan = self.plan()
        a, b = plan["stance_out"]["r_foot"]
        track = rs.ankle_y_track(y["r_foot"], plan["stance_src"]["r_foot"], (a, b), 0.04, 0.02, self.P)
        mid = rs.interp_cyclic(track + [track[0]], 0.5 * (a + b), self.P)
        self.assertAlmostEqual(mid, 0.02, places=3)

    def test_a_longer_stance_covers_a_longer_step(self):
        plan = self.plan(split=0.58)
        sweeps = {}
        for foot in ("r_foot", "l_foot"):
            a, b = plan["stance_out"][foot]
            sweeps[foot] = 0.04 * (b - a)
        self.assertGreater(sweeps["r_foot"], sweeps["l_foot"])

    def test_the_two_steps_add_up_to_one_stride_per_loop(self):
        plan = self.plan(split=0.58)
        gap1 = (plan["stance_out"]["l_foot"][0] - plan["stance_out"]["r_foot"][0]) % self.P
        gap2 = (plan["stance_out"]["r_foot"][0] - plan["stance_out"]["l_foot"][0]) % self.P
        self.assertAlmostEqual(gap1 + gap2, self.P, places=9)
        self.assertGreater(gap1, gap2)
        speed = plan["planted_speed"] * 0.95
        self.assertAlmostEqual(speed * (gap1 + gap2), 0.95 * plan["planted_speed"] * self.P, places=9)

    def test_the_loop_wrap_step_is_no_bigger_than_any_other_step(self):
        y, _ = self.feet()
        plan = self.plan(split=0.58)
        foot = "l_foot"
        a, b = plan["stance_out"][foot]
        track = rs.ankle_y_track(y[foot], plan["stance_src"][foot], (a, b), 0.045, 0.0, self.P)
        steps = [abs(track[(k + 1) % self.P] - track[k]) for k in range(self.P)]
        # the swing returns the foot to where the next stance starts: stepping from the last frame to the first is
        # an ordinary step, not a jump (a swing frame moves up to about 0.2 m, so only the comparison means anything)
        self.assertLessEqual(steps[-1], max(steps[:-1]) + 1e-9)

    def test_the_rebuilt_track_has_no_velocity_jump_at_the_loop_seam(self):
        # the seam falls in a stance for some offsets and in a swing for others; across the strides a variant uses
        for stride in (0.85, 0.9, 0.92, 0.95, 1.0):
            for a in (0, 2, 9, 14, 20, 27, 33):
                ys, zs = synthetic_foot(self.P, a, 14, self.SPEED)
                other = synthetic_foot(self.P, (a + 18) % self.P, 14, self.SPEED)
                plan = rs.plan_contacts({"r_foot": ys, "l_foot": other[0]}, {"r_foot": zs, "l_foot": other[1]},
                                        self.P, "r_foot", "l_foot", 0.56)
                track = rs.ankle_y_track(ys, plan["stance_src"]["r_foot"], plan["stance_out"]["r_foot"],
                                         self.SPEED * stride, 0.0, self.P, plan["warp"])
                vel = [track[(k + 1) % self.P] - track[k] for k in range(self.P)]
                accel = [vel[k] - vel[k - 1] for k in range(self.P)]
                a_out, b_out = plan["stance_out"]["r_foot"]
                edges = {int(round(a_out)) % self.P, int(round(b_out)) % self.P}
                interior = [abs(accel[k]) for k in range(self.P)
                            if all((k - e) % self.P > 1 and (e - k) % self.P > 1 for e in edges)]
                near_seam = {0, 1, self.P - 1}
                if near_seam & {(e + d) % self.P for e in edges for d in (-1, 0, 1)}:
                    continue   # the seam sits on a stance edge: that kink is the stance's own, not the seam's
                self.assertLessEqual(abs(accel[0]), max(interior) + 1e-9, "stride %s stance at %s" % (stride, a))

    def test_a_flat_swing_falls_back_to_a_linear_profile(self):
        y = [0.0] * (self.P + 1)
        track = rs.ankle_y_track(y, (2.0, 16.0), (2.0, 17.0), 0.04, 0.0, self.P)
        self.assertEqual(len(track), self.P)
        self.assertTrue(all(math.isfinite(v) for v in track))


class PulseTests(unittest.TestCase):
    def test_eased_pulse_is_zero_outside_and_one_at_the_peak(self):
        self.assertEqual(rs.eased_pulse(None), 0.0)
        self.assertEqual(rs.eased_pulse(0.0), 0.0)
        self.assertEqual(rs.eased_pulse(1.0), 0.0)
        self.assertAlmostEqual(rs.eased_pulse(0.25), 1.0)
        self.assertGreater(rs.eased_pulse(0.1), 0.0)
        self.assertLess(rs.eased_pulse(0.9), rs.eased_pulse(0.5))

    def test_swing_ramp_leaves_the_planted_ends_alone(self):
        self.assertEqual(rs.swing_ramp(0.0), 0.0)
        self.assertEqual(rs.swing_ramp(1.0), 0.0)
        self.assertEqual(rs.swing_ramp(0.5), 1.0)
        self.assertEqual(rs.swing_ramp(None), 0.0)

    def test_sine_bumps(self):
        self.assertEqual(rs.sine_bump(None), 0.0)
        self.assertAlmostEqual(rs.sine_bump(0.5), 1.0)
        self.assertAlmostEqual(rs.sine_bump_sq(0.5), 1.0)
        self.assertAlmostEqual(rs.sine_bump(0.0), 0.0)

    def test_phase_in_window_wraps_and_excludes_the_end(self):
        self.assertEqual(rs.phase_in_window(10, 8, 4, 36), 0.5)
        self.assertEqual(rs.phase_in_window(1, 34, 6, 36), 0.5)
        self.assertIsNone(rs.phase_in_window(12, 8, 4, 36))
        self.assertIsNone(rs.phase_in_window(5, 8, 4, 36))
        self.assertIsNone(rs.phase_in_window(5, 8, 0, 36))


class SegmentDistanceTests(unittest.TestCase):
    def test_parallel_segments(self):
        self.assertAlmostEqual(rs.segment_distance((0, 0, 0), (1, 0, 0), (0, 2, 0), (1, 2, 0)), 2.0)

    def test_crossing_segments_in_different_planes(self):
        self.assertAlmostEqual(rs.segment_distance((-1, 0, 0), (1, 0, 0), (0, -1, 0.5), (0, 1, 0.5)), 0.5)

    def test_closest_points_clamped_to_the_ends(self):
        self.assertAlmostEqual(rs.segment_distance((0, 0, 0), (1, 0, 0), (3, 0, 0), (4, 1, 0)), 2.0)
        self.assertAlmostEqual(rs.segment_distance((0, 0, 0), (1, 0, 0), (1, 1, 0), (1, 5, 0)), 1.0)

    def test_collinear_and_overlapping_segments_touch(self):
        self.assertAlmostEqual(rs.segment_distance((0, 0, 0), (2, 0, 0), (1, 0, 0), (3, 0, 0)), 0.0)

    def test_a_point_against_a_segment(self):
        self.assertAlmostEqual(rs.segment_distance((0, 1, 0), (0, 1, 0), (-1, 0, 0), (1, 0, 0)), 1.0)
        self.assertAlmostEqual(rs.segment_distance((0, 0, 0), (0, 0, 0), (3, 4, 0), (3, 4, 0)), 5.0)

    def test_symmetric_in_its_arguments(self):
        a, b, c, d = (0.1, 0.2, 0.3), (0.9, -0.4, 0.2), (0.5, 0.5, -0.5), (-0.3, 0.1, 0.8)
        self.assertAlmostEqual(rs.segment_distance(a, b, c, d), rs.segment_distance(c, d, a, b))
        self.assertAlmostEqual(rs.segment_distance(a, b, c, d), rs.segment_distance(b, a, d, c))


class HandClearanceTests(unittest.TestCase):
    def pos(self):
        """A standing body: torso on the axis, thighs and calves straight down 0.1 m either side."""
        p = {"pelvis": (0.0, 0.0, 0.9), "spine2": (0.0, 0.0, 1.4)}
        for side, x in (("l", -0.1), ("r", 0.1)):
            p[side + "_thigh"], p[side + "_calf"], p[side + "_foot"] = (x, 0.0, 0.85), (x, 0.0, 0.45), (x, 0.0, 0.1)
        return p

    def arm(self, x):
        """A right arm hanging straight down at lateral offset x from the shoulder height 1.35."""
        return (x, 0.0, 1.05), (x, 0.0, 0.8), (x, 0.0, 0.7)

    def test_the_gap_is_distance_minus_both_radii(self):
        caps = rs.body_capsules(self.pos())
        far = rs.arm_gap(self.arm(0.5), caps)
        near = rs.arm_gap(self.arm(0.3), caps)
        self.assertAlmostEqual(far - near, 0.2, places=6)
        # the forearm hanging at x 0.3 against the right thigh at 0.1: 0.2 - 0.04 - 0.085, or the hand's 0.05
        self.assertAlmostEqual(near, 0.2 - 0.05 - rs.CAPSULE_R["thigh"], places=6)

    def test_an_arm_through_a_thigh_has_a_negative_gap(self):
        self.assertLess(rs.arm_gap(self.arm(0.1), rs.body_capsules(self.pos())), 0.0)

    def test_the_grip_runs_past_the_wrist_along_the_hand(self):
        pos = {"r_foretwist": (0, 0, 1.0), "r_hand": (0, 0, 0.8), "r_finger0": (0, 0.05, 0.8)}
        elbow, wrist, tip = rs.arm_points(pos, "r")
        self.assertAlmostEqual(tip[1], rs.HAND_GRIP_M)
        self.assertAlmostEqual(tip[2], 0.8)
        pos["r_finger0"] = pos["r_hand"]   # no hand direction: along the forearm
        self.assertAlmostEqual(rs.arm_points(pos, "r")[2][2], 0.8 - rs.HAND_GRIP_M)

    def test_swinging_outward_moves_the_right_arm_to_plus_x_and_the_left_to_minus_x(self):
        sho = (0.2, 0.0, 1.35)
        r_out = rs.swing_arm_points(((0.2, 0.0, 1.0),), sho, 1.0, 30.0, 0.0)[0]
        l_out = rs.swing_arm_points(((-0.2, 0.0, 1.0),), (-0.2, 0.0, 1.35), -1.0, 30.0, 0.0)[0]
        self.assertGreater(r_out[0], 0.2)
        self.assertLess(l_out[0], -0.2)
        self.assertAlmostEqual(r_out[0] - 0.2, 0.35 * math.sin(math.radians(30.0)), places=9)
        self.assertAlmostEqual(math.dist(r_out, sho), 0.35, places=9)   # a rigid swing about the shoulder

    def test_swinging_back_moves_a_hanging_arm_toward_minus_y(self):
        sho = (0.2, 0.0, 1.35)
        back = rs.swing_arm_points(((0.2, 0.0, 1.0),), sho, 1.0, 0.0, 20.0)[0]
        self.assertLess(back[1], 0.0)

    def test_the_smallest_outward_angle_that_reaches_the_clearance_is_found(self):
        caps = rs.body_capsules(self.pos())
        pts = self.arm(0.2)
        sho = (0.2, 0.0, 1.35)
        gap0 = rs.arm_gap(pts, caps)
        want = gap0 + 0.05
        a, b, gap = rs.clearance_swing(pts, sho, 1.0, caps, want)
        self.assertGreater(a, 0.0)
        self.assertEqual(b, 0.0)
        self.assertGreaterEqual(gap, want)
        # one degree less falls short: it is the smallest
        less = rs.arm_gap(rs.swing_arm_points(pts, sho, 1.0, a - rs.CLEARANCE_STEP_DEG, 0.0), caps)
        self.assertLess(less, want)

    def test_an_arm_already_clear_is_left_alone(self):
        caps = rs.body_capsules(self.pos())
        self.assertEqual(rs.clearance_swing(self.arm(0.6), (0.6, 0.0, 1.35), 1.0, caps, 0.08)[:2], (0.0, 0.0))

    def test_an_unreachable_clearance_ends_at_the_limits(self):
        caps = rs.body_capsules(self.pos())
        a, b, gap = rs.clearance_swing(self.arm(0.2), (0.2, 0.0, 1.35), 1.0, caps, 5.0)
        self.assertEqual((a, b), (rs.ABDUCT_MAX_DEG, rs.BACK_MAX_DEG))
        self.assertLess(gap, 5.0)

    def test_the_shield_arm_keeps_the_larger_clearance_only_in_a_shield_clip(self):
        self.assertEqual(rs.clearance_for("run_forward_1h_with_shield", "l", 0.08, 0.15), 0.15)
        self.assertEqual(rs.clearance_for("run_forward_1h_with_shield_left_stance", "l", 0.08, 0.15), 0.15)
        self.assertEqual(rs.clearance_for("run_forward_1h_with_shield", "r", 0.08, 0.15), 0.08)
        self.assertEqual(rs.clearance_for("run_forward_1h", "l", 0.08, 0.15), 0.08)
        self.assertEqual(rs.clearance_for("run_forward_1h_with_shield", "l", 0.08, 0.0), 0.08)   # 0 = same as hands


class ClearanceSmoothingTests(unittest.TestCase):
    RAW = [0.0, 0.0, 0.0, 12.0, 30.0, 8.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 5.0]

    def test_the_smoothed_curve_is_never_below_the_raw_one(self):
        for cyclic in (True, False):
            sm = rs.smooth_correction(self.RAW, cyclic)
            for r, v in zip(self.RAW, sm):
                self.assertGreaterEqual(v, r - 1e-9)

    def test_it_never_overshoots_the_raw_peak(self):
        self.assertLessEqual(max(rs.smooth_correction(self.RAW, True)), max(self.RAW) + 1e-9)

    def test_nothing_in_nothing_out(self):
        self.assertEqual(rs.smooth_correction([0.0] * 12, True), [0.0] * 12)

    def test_no_frame_to_frame_pop(self):
        sm = rs.smooth_correction(self.RAW, True)
        raw_step = max(abs(b - a) for a, b in zip(self.RAW, self.RAW[1:] + self.RAW[:1]))
        sm_step = max(abs(b - a) for a, b in zip(sm, sm[1:] + sm[:1]))
        self.assertLess(sm_step, 0.4 * raw_step)

    def test_a_correction_across_the_seam_wraps_when_cyclic_and_not_otherwise(self):
        raw = [20.0] + [0.0] * 11
        cyc = rs.smooth_correction(raw, True)
        open_ = rs.smooth_correction(raw, False)
        self.assertGreater(cyc[-1], 0.0)    # the last frame feels the first's correction
        self.assertAlmostEqual(cyc[1], cyc[-1], places=9)   # and the seam is symmetric
        self.assertGreater(open_[0], cyc[0] - 1e-9)

    def test_the_curve_is_continuous_in_slope_at_the_seam(self):
        raw = [0.0] * 8 + [10.0, 25.0, 25.0, 10.0]
        sm = rs.smooth_correction(raw, True)
        n = len(sm)
        slopes = [sm[(k + 1) % n] - sm[k] for k in range(n)]
        jumps = [abs(slopes[k] - slopes[k - 1]) for k in range(n)]
        self.assertLessEqual(jumps[0], max(jumps[1:]) + 1e-9)   # the seam is no sharper than any other frame

    def test_the_running_max_widens_a_peak_by_the_radius(self):
        out = rs.dilate_cyclic([0, 0, 0, 5, 0, 0, 0, 0], 2, True)
        self.assertEqual(out, [0, 5, 5, 5, 5, 5, 0, 0])
        self.assertEqual(rs.dilate_cyclic([5, 0, 0, 0, 0, 0, 0, 0], 1, True), [5, 5, 0, 0, 0, 0, 0, 5])
        self.assertEqual(rs.dilate_cyclic([5, 0, 0, 0], 1, False), [5, 5, 0, 0])


class HandClearanceKindTests(SpecFileCase):
    def test_it_applies_to_idles_as_well_as_gaits(self):
        spec = rs.load_spec(self.write({"clip_prefix": "x_", "hand_clearance_m": 0.08}))
        self.assertTrue(rs.hand_clearance_active(spec))
        self.assertTrue(rs.hand_clearance_active(rs.spec_for_clip(spec, "stand_1h")))
        self.assertTrue(rs.hand_clearance_active(rs.spec_for_clip(spec, "walk_forward_1h")))
        self.assertEqual(rs.clip_kind("stand_1h"), "idle")

    def test_it_is_off_without_the_key_and_in_the_identity_run(self):
        self.assertFalse(rs.hand_clearance_active(rs.load_spec(self.write({"clip_prefix": "x_"}))))
        spec = rs.load_spec(self.write({"clip_prefix": "x_", "hand_clearance_m": 0.08}))
        self.assertFalse(rs.hand_clearance_active(rs.identity_spec(spec)))

    def test_the_shield_arm_rule_covers_the_stand_clips(self):
        for clip in ("stand_1h_with_shield", "stand_1h_with_shield_left_stance"):
            self.assertEqual(rs.clearance_for(clip, "l", 0.08, 0.15), 0.15, clip)
            self.assertEqual(rs.clearance_for(clip, "r", 0.08, 0.15), 0.08, clip)
        for clip in ("stand_1h", "stand_1h_left_stance", "stand_non_combat", "stand_unarmed_left_stance"):
            self.assertEqual(rs.clearance_for(clip, "l", 0.08, 0.15), 0.08, clip)

    def test_an_open_idle_is_smoothed_without_wrap(self):
        raw = [20.0] + [0.0] * 20
        self.assertEqual(rs.smooth_correction(raw, False)[-1], 0.0)    # a long idle's last frame is not touched
        self.assertGreater(rs.smooth_correction(raw, True)[-1], 0.0)   # a closed loop's is


class HandClearanceSpecTests(SpecFileCase):
    def test_off_by_default_and_a_run_block_may_set_it(self):
        spec = rs.load_spec(self.write({"clip_prefix": "x_"}))
        self.assertEqual((spec["hand_clearance_m"], spec["shield_arm_clearance_m"]), (0.0, 0.0))
        spec = rs.load_spec(self.write({"clip_prefix": "x_", "hand_clearance_m": 0.08, "run": {"hand_clearance_m": 0.1}}))
        self.assertEqual(rs.spec_for_clip(spec, "run_forward_1h")["hand_clearance_m"], 0.1)
        self.assertEqual(spec["hand_clearance_m"], 0.08)

    def test_bad_values_are_refused(self):
        for bad in (-0.01, "wide", True):
            with self.subTest(bad):
                with self.assertRaises(SystemExit):
                    rs.load_spec(self.write({"clip_prefix": "x_", "hand_clearance_m": bad}))
        with self.assertRaises(SystemExit):
            rs.load_spec(self.write({"clip_prefix": "x_", "shield_arm_clearance_m": -1}))


class PackBSpecTests(SpecFileCase):
    def load(self, **kw):
        data = {"clip_prefix": "x_"}
        data.update(kw)
        return rs.load_spec(self.write(data))

    def test_mirror_sides_is_not_a_run_key(self):
        with self.assertRaises(SystemExit) as ctx:
            self.load(run={"mirror_sides": True})
        self.assertIn("mirror_sides", str(ctx.exception))

    def test_every_top_level_value_check_runs_on_the_merged_run_spec(self):
        for run in ({"hand_clearance_m": -0.1}, {"shield_arm_clearance_m": -1}, {"pelvis_tilt_deg": "steep"},
                    {"stance_width_m": True}, {"trunk_flex_deg": {"spine": True}}, {"stride_scale": 0.0}):
            with self.subTest(run):
                with self.assertRaises(SystemExit):
                    self.load(run=run)

    def test_booleans_are_not_numbers(self):
        for key in ("pelvis_tilt_deg", "stride_scale", "knee_bend_deg", "hand_clearance_m"):
            with self.subTest(key):
                with self.assertRaises(SystemExit):
                    self.load(**{key: True})

    def test_unknown_bones_in_the_flex_tables_are_refused(self):
        with self.assertRaises(SystemExit) as ctx:
            self.load(trunk_flex_deg={"spine": 5.0, "spine3": 4.0})
        self.assertIn("spine3", str(ctx.exception))
        with self.assertRaises(SystemExit):
            self.load(neck_head_flex_deg={"neck": 5.0, "skull": 4.0})
        with self.assertRaises(SystemExit):
            self.load(run={"trunk_flex_deg": {"spine9": 4.0}})
        self.assertEqual(self.load(trunk_flex_deg={"spine": 5.0})["trunk_flex_deg"], {"spine": 5.0})

    def test_the_run_override_of_a_valid_value_still_merges(self):
        spec = self.load(hand_clearance_m=0.08, run={"hand_clearance_m": 0.1, "trunk_flex_deg": {"spine2": 9.0}})
        merged = rs.spec_for_clip(spec, "run_forward_1h")
        self.assertEqual((merged["hand_clearance_m"], merged["trunk_flex_deg"]), (0.1, {"spine2": 9.0}))


class LimpForKindTests(unittest.TestCase):
    def full(self):
        return dict(rs.LIMP_DEFAULTS, strong_side="l", lead_shoulder_side="l", lead_shoulder_yaw_deg=12.0,
                    low_shoulder_deg=4.0, low_shoulder_lean_deg=3.0, low_arm_swing_scale=1.6, low_arm_forward_deg=6.0,
                    min_track_half_m=0.14, knee_bow_m=0.07, knee_max_deg=160.0, weak_trail_m=0.1, arm_forward_deg=10.0)

    def test_a_gait_keeps_the_whole_limp(self):
        self.assertEqual(rs.limp_for_kind(self.full(), "gait"), self.full())

    def test_an_idle_keeps_only_the_arms_forward_turn(self):
        idle = rs.limp_for_kind(self.full(), "idle")
        self.assertEqual(idle["arm_forward_deg"], 10.0)
        for key, value in rs.LIMP_DEFAULTS.items():
            if key != "arm_forward_deg":
                self.assertEqual(idle[key], value, key)

    def test_a_mirrored_low_shoulder_does_not_reach_an_idle(self):
        idle = rs.limp_for_kind(self.full(), "idle")
        self.assertFalse(rs.limp_static_active(dict(idle, arm_forward_deg=0.0)))

    def test_the_input_is_not_mutated(self):
        full = self.full()
        before = dict(full)
        rs.limp_for_kind(full, "idle")
        self.assertEqual(full, before)


class PackBHelperTests(unittest.TestCase):
    def test_equal_inputs_give_the_same_goal_and_only_the_centre_can_move_a_clamped_one(self):
        """foot_goal_x has exactly five inputs and the frame's pelvis is not among them, so what can be asserted is that
        it is a function of those five: equal inputs, equal goal, and changing only the centre changes the clamped goal.
        That the CALLER passes the clip centre (not a per-frame pelvis) is pinned by the source-text test below."""
        args = (0.20, 1.0, 0.08, 0.14, 0.01)
        self.assertEqual(rs.foot_goal_x(*args), rs.foot_goal_x(*args))
        for i, other in enumerate((-0.05, 0.12, 0.3)):
            changed = list(args)
            changed[4] = other
            self.assertNotEqual(rs.foot_goal_x(*changed), rs.foot_goal_x(*args), "centre %s" % other)
        # with no clamp set the centre is irrelevant
        self.assertEqual(rs.foot_goal_x(0.2, 1.0, 0.08, 0.0, 0.01), rs.foot_goal_x(0.2, 1.0, 0.08, 0.0, 0.5))

    def test_the_goal_moves_with_the_clip_centre_only(self):
        a = rs.foot_goal_x(0.10, 1.0, 0.08, 0.14, 0.00)
        b = rs.foot_goal_x(0.10, 1.0, 0.08, 0.14, 0.05)
        self.assertNotEqual(a, b)   # a different clip centre is a different clamp; nothing per frame enters

    def test_the_goal_is_the_widened_ankle_when_no_track_clamp_is_set(self):
        self.assertEqual(rs.foot_goal_x(0.10, 1.0, 0.08, 0.0, 0.01), 0.18)
        self.assertEqual(rs.foot_goal_x(-0.10, -1.0, 0.08, 0.0, 0.01), -0.18)

    def test_restyle_clip_passes_the_clip_mean_pelvis_line_to_the_goal(self):
        """The call site, not just the helper: the restyle must hand foot_goal_x pmean.x, never a per-frame pelvis."""
        import re
        text = open(os.path.join(BLENDER_DIR, "restyle_human_clip.py"), encoding="utf-8").read()
        calls = re.findall(r"foot_goal_x\(([^)]*)\)", text)
        calls = [c for c in calls if 'lg["side"]' in c]   # the call site, not the def
        self.assertEqual(len(calls), 1, calls)
        self.assertIn("pmean.x", calls[0])
        self.assertNotIn("pelvis", calls[0])
        self.assertNotIn("Mw[", calls[0])

    def test_the_track_half_measure_uses_the_same_centre(self):
        text = open(os.path.join(BLENDER_DIR, "restyle_human_clip.py"), encoding="utf-8").read()
        self.assertIn('track["half"] = min(track["half"], lg["side"] * (Af.x - pmean.x))', text)

    def test_the_clamp_holds_the_track_half_width_from_the_centre(self):
        for side in (-1.0, 1.0):
            for gx in (-0.3, -0.12, -0.05, 0.0, 0.05, 0.12, 0.3):
                out = rs.clamp_track_half(gx, 0.01, side, 0.14)
                self.assertGreaterEqual(side * (out - 0.01), 0.14 - 1e-9)

    def test_a_foot_already_wide_is_barely_moved(self):
        self.assertAlmostEqual(rs.clamp_track_half(0.40, 0.0, 1.0, 0.14), 0.40, places=3)
        self.assertAlmostEqual(rs.clamp_track_half(-0.40, 0.0, -1.0, 0.14), -0.40, places=3)

    def test_the_limp_key_lists_agree(self):
        keys = set(rs.LIMP_DEFAULTS)
        static = set(rs.LIMP_STATIC_KEYS)
        self.assertEqual(static & set(rs.LIMP_CYCLE_KEYS), set())
        self.assertEqual(static & set(rs.LIMP_SIDE_KEYS), set())
        self.assertTrue(static <= keys)
        for key in ("knee_bow_deg", "knee_bow_m", "arm_forward_deg", "min_track_half_m", "lead_shoulder_yaw_deg",
                    "low_shoulder_deg", "low_shoulder_lean_deg", "low_arm_forward_deg", "lead_shoulder_protract_deg"):
            self.assertIn(key, static)
        # every key is exactly one of: cycle, side, static or one of the two with a non-zero neutral
        special = {"low_arm_swing_scale", "knee_max_deg"}
        self.assertEqual(keys, static | set(rs.LIMP_CYCLE_KEYS) | set(rs.LIMP_SIDE_KEYS) | special)

    def test_the_knee_bow_offset_is_the_outward_distance_from_the_hip_ankle_line(self):
        hip, ankle = (0.1, 0.0, 0.9), (0.1, 0.0, 0.1)
        knee_out = (0.17, 0.0, 0.5)    # 7 cm outward on the right leg
        self.assertAlmostEqual(rs.knee_bow_offset(hip, knee_out, ankle, 1.0), 0.07)
        self.assertAlmostEqual(rs.knee_bow_offset(hip, knee_out, ankle, -1.0), -0.07)
        self.assertAlmostEqual(rs.knee_bow_offset(hip, (0.03, 0.0, 0.5), ankle, 1.0), -0.07)   # knock-kneed is negative
        self.assertIsNone(rs.knee_bow_offset(hip, knee_out, (0.1, 0.0, 0.95), 1.0))            # ankle above the hip


class NumberHelperTests(SpecFileCase):
    def test_is_number_refuses_bools_nan_infinity_and_null(self):
        for bad in (True, False, None, float("nan"), float("inf"), -float("inf"), "1.0", [1.0]):
            self.assertFalse(rs._is_number(bad), repr(bad))
        for good in (0, 1, -2.5, 0.0, 1e-9):
            self.assertTrue(rs._is_number(good), repr(good))

    def test_low_arm_swing_scale_goes_through_the_same_check(self):
        for bad in (True, False, float("nan"), "big"):
            with self.subTest(bad):
                with self.assertRaises(SystemExit):
                    rs.load_spec(self.write({"clip_prefix": "x_", "limp": {"low_arm_swing_scale": bad}}))
                with self.assertRaises(SystemExit):
                    rs.load_spec(self.write({"clip_prefix": "x_", "run": {"low_arm_swing_scale": bad}}))
        self.assertIsNone(rs.load_spec(self.write({"clip_prefix": "x_", "limp": {"low_arm_swing_scale": None}}))["limp"]["low_arm_swing_scale"])
        self.assertEqual(rs.load_spec(self.write({"clip_prefix": "x_", "run": {"low_arm_swing_scale": 1.5}}))["run"], {"low_arm_swing_scale": 1.5})

    def test_nan_is_not_a_limp_number_either(self):
        with self.assertRaises(SystemExit):
            rs.load_spec(self.write({"clip_prefix": "x_", "limp": {"weak_trail_m": float("nan")}}))


class StepPointsFileValueTests(unittest.TestCase):
    def load(self, row):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "sp.json")
            with open(path, "w") as fh:
                fh.write(json.dumps({"walk_forward_1h": row}).replace("NaN", "NaN"))
            return rs.load_step_points(path, required=True)

    def test_a_proper_row_loads(self):
        self.assertEqual(self.load([0.4, 0.898, -1, -1])["walk_forward_1h"], [0.4, 0.898, -1.0, -1.0])

    def test_booleans_null_nan_and_out_of_range_are_refused(self):
        for row in ([True, 0.9], [0.4, False], [0.4, None], [float("nan"), 0.9], [0.4, 1.5], [-2.0, 0.9], [0.4, "0.9"]):
            with self.subTest(row):
                with self.assertRaises(SystemExit):
                    self.load(row)

    def test_the_unused_marker_and_the_ends_are_in_range(self):
        self.assertEqual(self.load([0.0, 1.0])["walk_forward_1h"], [0.0, 1.0])
        self.assertEqual(self.load([-1, -1])["walk_forward_1h"], [-1.0, -1.0])


class SpecShaTests(unittest.TestCase):
    def test_the_hash_ignores_key_order_and_follows_the_values(self):
        a = rs.canonical_sha({"x": 1, "y": {"b": 2, "a": 3}})
        b = rs.canonical_sha({"y": {"a": 3, "b": 2}, "x": 1})
        self.assertEqual(a, b)
        self.assertEqual(len(a), 64)
        self.assertNotEqual(a, rs.canonical_sha({"x": 1, "y": {"b": 2, "a": 4}}))

    def test_a_clips_sha_covers_the_merged_spec_and_its_kind(self):
        spec = rs.load_spec_dict({"clip_prefix": "x_", "run": {"timing_split": 0.6}, "limp": {"timing_split": 0.55}})
        walk = rs.clip_spec_sha(spec, "walk_forward_1h", "gait")
        run = rs.clip_spec_sha(spec, "run_forward_1h", "gait")
        idle = rs.clip_spec_sha(spec, "stand_1h", "idle")
        self.assertEqual(len({walk, run, idle}), 3)      # the run override and the kind both count
        self.assertEqual(walk, rs.clip_spec_sha(spec, "walk_forward_unarmed", "gait"))   # same merged spec, same sha
        spec2 = rs.load_spec_dict({"clip_prefix": "x_", "run": {"timing_split": 0.6}, "limp": {"timing_split": 0.56}})
        self.assertNotEqual(walk, rs.clip_spec_sha(spec2, "walk_forward_1h", "gait"))

    def test_a_partial_run_keeps_the_old_entries_own_shas(self):
        old = {"walk_forward_1h": {"spec_sha": "old-w", "loaded_spec_sha": "L1"},
               "run_forward_1h": {"spec_sha": "old-r", "loaded_spec_sha": "L1"}}
        new = {"run_forward_1h": {"spec_sha": "new-r", "loaded_spec_sha": "L2"}}
        merged = rs.merge_report_clips(old, new)
        self.assertEqual(merged["walk_forward_1h"]["spec_sha"], "old-w")        # untouched
        self.assertEqual(merged["walk_forward_1h"]["loaded_spec_sha"], "L1")    # still says which spec it described
        self.assertEqual(merged["run_forward_1h"]["spec_sha"], "new-r")         # replaced
        self.assertEqual(old["run_forward_1h"]["spec_sha"], "old-r")            # inputs not mutated

    def test_stale_clips_are_the_ones_that_describe_another_spec(self):
        report = {"spec_sha": "L2", "clips": {"a": {"loaded_spec_sha": "L2"}, "b": {"loaded_spec_sha": "L1"}, "c": {}}}
        self.assertEqual(rs.stale_clips(report), ["b", "c"])
        self.assertEqual(rs.stale_clips({"spec_sha": "L2", "clips": {"a": {"loaded_spec_sha": "L2"}}}), [])


class IdleBlockTests(SpecFileCase):
    def idle(self, **kw):
        return rs.load_spec_dict({"clip_prefix": "x_", "idle": kw})

    def test_a_good_idle_block_loads(self):
        spec = self.idle(head_yaw_deg=12.0, head_cycles=2, breath_cycles=3, weight_shift_m=0.02, glance_harmonic=0.0)
        self.assertEqual((spec["idle"]["head_cycles"], spec["idle"]["breath_cycles"]), (2, 3))

    def test_booleans_null_strings_nan_and_infinity_are_refused(self):
        for key in ("head_yaw_deg", "breath_pitch_deg", "weight_shift_m", "glance_harmonic"):
            for bad in (True, False, None, "1.0", float("nan"), float("inf"), -float("inf")):
                with self.subTest(key=key, bad=bad):
                    with self.assertRaises(SystemExit):
                        self.idle(**{key: bad})

    def test_the_cycle_counts_must_be_whole_numbers(self):
        for key in ("head_cycles", "breath_cycles", "weight_cycles"):
            for bad in (1.5, "2", True, None, 0, -1, float("nan")):
                with self.subTest(key=key, bad=bad):
                    with self.assertRaises(SystemExit):
                        self.idle(**{key: bad})

    def test_the_shipped_main_orcs_idle_block_still_loads(self):
        rs.load_spec(os.path.join(VARIANTS_DIR, "orc.json"))


class ProvenanceWiringTests(unittest.TestCase):
    """main() needs Blender, so its provenance wiring is pinned in its source."""

    def main_source(self):
        import inspect
        return inspect.getsource(rs.main)

    def test_each_entry_carries_the_loaded_spec_sha(self):
        src = self.main_source()
        self.assertIn('"loaded_spec_sha": loaded_sha', src)
        self.assertIn('"spec_sha": clip_spec_sha(spec, clip, kind)', src)

    def test_the_top_level_sha_is_the_loaded_sha(self):
        self.assertIn('"spec_sha": loaded_sha', self.main_source())

    def test_the_only_path_merges_with_the_old_entries_own_shas(self):
        src = self.main_source()
        self.assertIn('merge_report_clips(data.get("clips", {}), report["clips"])', src)

    def test_the_loaded_sha_comes_from_the_provenance_function(self):
        self.assertIn("provenance_sha(spec", self.main_source())


class CodeProvenanceTests(unittest.TestCase):
    def test_the_two_tool_sources_are_hashed(self):
        shas = rs.code_shas()
        self.assertEqual(set(shas), {"restyle_human_clip.py", "retarget_mannequin_to_human.py"})
        for sha in shas.values():
            self.assertEqual(len(sha), 64)
        self.assertEqual(rs.code_shas(), shas)    # stable

    def test_a_code_change_with_an_unchanged_spec_changes_the_provenance(self):
        spec = {"clip_prefix": "x_", "stride_scale": 0.85}
        code = {"restyle_human_clip.py": "a" * 64, "retarget_mannequin_to_human.py": "b" * 64}
        base = rs.provenance_sha(spec, code)
        self.assertEqual(base, rs.provenance_sha(dict(spec), dict(code)))
        self.assertNotEqual(base, rs.provenance_sha(spec, dict(code, **{"restyle_human_clip.py": "c" * 64})))
        self.assertNotEqual(base, rs.provenance_sha(spec, dict(code, **{"retarget_mannequin_to_human.py": "c" * 64})))
        self.assertNotEqual(base, rs.provenance_sha(dict(spec, stride_scale=0.9), code))     # and a spec change still counts
        self.assertNotEqual(base, rs.canonical_sha(spec))                                       # it is not the bare spec hash

    def test_the_provenance_default_uses_the_real_sources(self):
        spec = {"clip_prefix": "x_"}
        self.assertEqual(rs.provenance_sha(spec), rs.provenance_sha(spec, rs.code_shas()))

    def test_stale_clips_documents_what_the_generator_checks(self):
        doc = rs.stale_clips.__doc__
        self.assertIn("every entry", doc)
        self.assertIn("idle", doc)
        self.assertIn("loaded_spec_sha", doc)


class ShippedDataTests(unittest.TestCase):
    def test_goblin_has_no_limp_run_or_clearance_block(self):
        """Proves only that the spec asks for none of the new features, so none of their code runs for it. It does not
        prove the output is unchanged: the neck and head pitch moved to the body's side axis (the vanilla neck leans 5
        to 7 degrees sideways, so that differs slightly from the old direction-based axis)."""
        for name in ("goblin.json",):
            with self.subTest(name):
                spec = rs.load_spec(os.path.join(VARIANTS_DIR, name))
                self.assertFalse(rs.limp_cycle_active(spec["limp"]))
                self.assertFalse(rs.limp_static_active(spec["limp"]))
                self.assertEqual(spec["run"], {})
                self.assertEqual(rs.all_cap_violations(spec), [])
                self.assertIs(rs.spec_for_clip(spec, "run_forward_1h"), spec)

    def test_the_three_shipped_orcs_load_with_their_known_caps_and_strides(self):
        """orc.json is over the combat caps on purpose: exactly the trunk flex (40 over 30) and the clavicle drop plus
        low shoulder (12 over 10), once for the base and once for the run override. orc_b and orc_c are clean."""
        main = rs.load_spec(os.path.join(VARIANTS_DIR, "orc.json"))
        over = rs.all_cap_violations(main)
        self.assertEqual(len(over), 4, over)
        self.assertEqual(sum("trunk flex 40.0" in v for v in over), 2)
        self.assertEqual(sum("clavicle_drop_deg + low_shoulder_deg 12.0" in v for v in over), 2)
        self.assertEqual(sum(v.startswith("run: ") for v in over), 2)
        b = rs.load_spec(os.path.join(VARIANTS_DIR, "orc_b.json"))
        c = rs.load_spec(os.path.join(VARIANTS_DIR, "orc_c.json"))
        self.assertEqual(rs.all_cap_violations(b), [])
        self.assertEqual(rs.all_cap_violations(c), [])
        self.assertEqual((main["stride_scale"], b["stride_scale"], c["stride_scale"]), (0.85, 1.0, 0.89))

    def test_the_vanilla_step_points_file_loads_and_names_every_gait(self):
        pts = rs.load_step_points(rs.DEFAULT_STEP_POINTS)
        self.assertEqual(len(pts), 10)
        for clip, p in pts.items():
            self.assertEqual(rs.clip_kind(clip), "gait", clip)
            self.assertAlmostEqual(p[0], 0.4, places=6)
            self.assertAlmostEqual(p[1], 229 / 255.0, places=6)

    def test_only_the_default_step_points_path_may_be_missing(self):
        missing = os.path.join(tempfile.gettempdir(), "no_such_step_points.json")
        self.assertEqual(rs.load_step_points(""), {})
        self.assertEqual(rs.load_step_points(missing), {})                 # the default path: absent means none given
        with self.assertRaises(SystemExit) as ctx:
            rs.load_step_points(missing, required=True)                      # a path the caller named
        self.assertIn(missing, str(ctx.exception))

    def test_a_malformed_step_points_file_is_an_error(self):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "sp.json")
            with open(path, "w") as fh:
                json.dump({"walk_forward_1h": "0.4"}, fh)
            with self.assertRaises(SystemExit):
                rs.load_step_points(path)


if __name__ == "__main__":
    unittest.main()
