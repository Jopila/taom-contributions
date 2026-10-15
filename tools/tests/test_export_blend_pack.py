"""Unit tests for the bpy-free parts of tools/blender/export_blend_pack.py.

Run:  python -m unittest tools.tests.test_export_blend_pack

The exporter imports bpy and mathutils at module level, so stubs go in sys.modules for the load only. The Blender
side (the bake, the FBX export, the re-import) is proven in a live session; these cover the sample times, the file
name collision check, the round-trip verdict and the detached-run handling (stale .DONE, bad arguments, empty clip
selection, a failed pack), none of which needs Blender.
"""
import importlib.util
import json
import os
import sys
import tempfile
import types
import unittest
from unittest import mock

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "export_blend_pack.py")

_mu = types.ModuleType("mathutils")
_mu.Matrix = object
_spec = importlib.util.spec_from_file_location("taom_export_blend_pack", SCRIPT)
ebp = importlib.util.module_from_spec(_spec)
with mock.patch.dict(sys.modules, {"bpy": types.ModuleType("bpy"), "mathutils": _mu}):
    _spec.loader.exec_module(ebp)


def old_times(s, e, src_fps, dst_fps):
    """The fixed-step times resample() used before the fix, for the exact cases."""
    n = int(round((e - s) * dst_fps / src_fps)) + 1
    return [min(s + i * src_fps / dst_fps, float(e)) for i in range(n)]


class SampleTimesTests(unittest.TestCase):
    def _check(self, s, e, src, dst):
        times = ebp.sample_times(s, e, src, dst)
        self.assertEqual(len(times), int(round((e - s) * dst / src)) + 1, "n is today's formula")
        self.assertEqual(times[0], s)
        self.assertEqual(times[-1], e, "the last sample is the source's own closing pose")
        steps = [b - a for a, b in zip(times, times[1:])]
        for st in steps:
            self.assertAlmostEqual(st, steps[0], places=9, msg="uniform steps")
        return times

    def test_24_fps_inexact_length_ends_on_the_last_frame(self):
        times = self._check(1, 40, 24, 30)   # 39 * 30 / 24 = 48.75 -> 49 intervals, 50 samples
        self.assertEqual(len(times), 50)
        self.assertAlmostEqual(times[1] - times[0], 39 / 49.0, places=9)   # not the fixed 0.8

    def test_25_fps_inexact_length_ends_on_the_last_frame(self):
        self._check(0, 21, 25, 30)

    def test_24_fps_inexact_with_nonzero_start(self):
        self._check(5, 38, 24, 30)

    def test_exact_case_equals_the_old_fixed_step_times(self):
        times = ebp.sample_times(0, 48, 24, 30)
        self.assertEqual(len(times), 61)
        for a, b in zip(times, old_times(0, 48, 24, 30)):
            self.assertAlmostEqual(a, b, places=9)

    def test_exact_case_with_an_offset_start(self):
        times = ebp.sample_times(3, 27, 24, 30)   # 24 frames -> 30 intervals
        self.assertEqual(len(times), 31)
        for a, b in zip(times, old_times(3, 27, 24, 30)):
            self.assertAlmostEqual(a, b, places=9)

    def test_same_rate_is_the_source_frames(self):
        self.assertEqual(ebp.sample_times(2, 6, 30, 30), [2.0, 3.0, 4.0, 5.0, 6.0])

    def test_a_single_frame_clip_is_one_sample(self):
        self.assertEqual(ebp.sample_times(4, 4, 24, 30), [4.0])
        self.assertEqual(ebp.sample_times(4, 4, 24, 30)[0], 4.0)


class CollisionTests(unittest.TestCase):
    def test_distinct_names_pass(self):
        self.assertIsNone(ebp.find_collision(["Idle", "Walk_F", "Walk_B"]))

    def test_two_names_with_one_safe_name_are_named_together(self):
        hit = ebp.find_collision(["Walk.001", "Idle", "Walk_001"])
        self.assertEqual(hit, ("Walk.001", "Walk_001"))

    def test_case_alone_collides_on_a_case_insensitive_disk(self):
        self.assertEqual(ebp.find_collision(["Idle", "idle"]), ("Idle", "idle"))

    def test_the_message_names_both(self):
        msg = ebp.collision_message("clip", ("Walk.001", "Walk_001"))
        self.assertIn("Walk.001", msg)
        self.assertIn("Walk_001", msg)


class PackSpecTests(unittest.TestCase):
    def test_a_good_spec(self):
        self.assertEqual(ebp.parse_pack_specs(["boar=C:\\a b\\SK_Boar.blend", "deer=D:\\d.blend"]),
                         [("boar", "C:\\a b\\SK_Boar.blend"), ("deer", "D:\\d.blend")])

    def test_a_spec_without_equals_is_refused_with_its_text(self):
        with self.assertRaises(ValueError) as cm:
            ebp.parse_pack_specs(["boar"])
        self.assertIn("boar", str(cm.exception))

    def test_an_empty_key_or_path_is_refused(self):
        for bad in ("=x.blend", "boar="):
            with self.assertRaises(ValueError, msg=bad):
                ebp.parse_pack_specs([bad])

    def test_a_repeated_key_is_refused(self):
        with self.assertRaises(ValueError):
            ebp.parse_pack_specs(["boar=a.blend", "boar=b.blend"])


class RoundtripVerdictTests(unittest.TestCase):
    def _good(self):
        return {"body": {"missing_bones": [], "head_drift_max_m": 0.0},
                "clip": {"missing_bones": [], "head_drift_max_m": 0.0, "has_action": True,
                         "fps_after_import": 30.0, "frame_range": [2.0, 37.0], "want_frame_range": [2, 37]}}

    def test_a_clean_round_trip_has_no_problem(self):
        self.assertEqual(ebp.roundtrip_verdict(self._good(), 30), [])

    def test_a_body_only_round_trip_is_judged_too(self):
        c = self._good()
        del c["clip"]
        self.assertEqual(ebp.roundtrip_verdict(c, 30), [])

    def test_missing_bones_fail(self):
        c = self._good()
        c["body"]["missing_bones"] = ["Spine"]
        self.assertTrue(any("Spine" in p for p in ebp.roundtrip_verdict(c, 30)))

    def test_rest_head_drift_above_1e4_fails(self):
        c = self._good()
        c["clip"]["head_drift_max_m"] = 0.00015
        self.assertTrue(ebp.roundtrip_verdict(c, 30))
        c["clip"]["head_drift_max_m"] = 0.0001
        self.assertEqual(ebp.roundtrip_verdict(c, 30), [])

    def test_no_compared_head_fails(self):
        c = self._good()
        c["body"]["head_drift_max_m"] = None
        self.assertTrue(ebp.roundtrip_verdict(c, 30))

    def test_a_clip_at_the_wrong_fps_fails(self):
        c = self._good()
        c["clip"]["fps_after_import"] = 24.0
        self.assertTrue(any("fps" in p for p in ebp.roundtrip_verdict(c, 30)))

    def test_a_clip_with_the_wrong_frame_range_fails(self):
        c = self._good()
        c["clip"]["frame_range"] = [1.0, 36.0]
        self.assertTrue(any("frame range" in p for p in ebp.roundtrip_verdict(c, 30)))

    def test_a_clip_that_imports_without_an_action_fails(self):
        c = self._good()
        c["clip"]["has_action"] = False
        c["clip"]["frame_range"] = None
        self.assertTrue(ebp.roundtrip_verdict(c, 30))


class DetachedRunTests(unittest.TestCase):
    def _run(self, argv):
        with mock.patch.object(sys, "argv", ["blender", "--"] + argv):
            ebp.main()

    def _done(self, out):
        with open(os.path.join(out, "export_report.json.DONE")) as fh:
            return fh.read()

    def test_the_stale_verdict_is_cleared(self):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "export_report.json.DONE")
            with open(path, "w") as fh:
                fh.write("ok")
            ebp.clear_stale_verdict(d)
            self.assertFalse(os.path.exists(path))
            ebp.clear_stale_verdict(d)   # nothing there: no error

    def test_a_bad_pack_spec_writes_a_fail_verdict_over_a_stale_ok(self):
        with tempfile.TemporaryDirectory() as d:
            with open(os.path.join(d, "export_report.json.DONE"), "w") as fh:
                fh.write("ok")
            self._run(["--pack", "noequals", "--out", d])
            self.assertTrue(self._done(d).startswith("fail: "), self._done(d))
            self.assertIn("noequals", self._done(d))

    def test_an_argparse_error_writes_a_fail_verdict(self):
        with tempfile.TemporaryDirectory() as d:
            self._run(["--pack", "a=b.blend", "--fps", "thirty", "--out", d])
            self.assertTrue(self._done(d).startswith("fail: "), self._done(d))

    def test_a_missing_required_argument_writes_a_fail_verdict(self):
        with tempfile.TemporaryDirectory() as d:
            self._run(["--out", d])   # no --pack
            self.assertTrue(self._done(d).startswith("fail: "), self._done(d))

    def test_a_failed_pack_is_recorded_in_the_report_and_the_verdict(self):
        with tempfile.TemporaryDirectory() as d:
            def boom(key, *a, **k):
                raise RuntimeError("no action matched --clips-regex")
            with mock.patch.object(ebp, "export_pack", boom):
                self._run(["--pack", "boar=x.blend", "--out", d])
            with open(os.path.join(d, "export_report.json")) as fh:
                report = json.load(fh)
            self.assertEqual(report["failed"], ["boar"])
            self.assertIn("no action matched", report["packs"]["boar"]["error"])
            self.assertEqual(self._done(d), "fail: boar")

    def test_a_round_trip_problem_fails_the_pack(self):
        with tempfile.TemporaryDirectory() as d:
            bad = {"body": {"missing_bones": ["Spine"], "head_drift_max_m": 0.0}}
            with mock.patch.object(ebp, "export_pack", lambda *a, **k: None), \
                    mock.patch.object(ebp, "roundtrip_check", lambda entry: bad):
                self._run(["--pack", "boar=x.blend", "--out", d])
            self.assertEqual(self._done(d), "fail: boar")
            with open(os.path.join(d, "export_report.json")) as fh:
                report = json.load(fh)
            self.assertIn("Spine", report["packs"]["boar"]["error"])

    def test_a_clean_run_still_writes_ok(self):
        with tempfile.TemporaryDirectory() as d:
            good = {"body": {"missing_bones": [], "head_drift_max_m": 0.0}}
            with mock.patch.object(ebp, "export_pack", lambda *a, **k: None), \
                    mock.patch.object(ebp, "roundtrip_check", lambda entry: good):
                self._run(["--pack", "boar=x.blend", "--out", d])
            self.assertEqual(self._done(d), "ok")
            with open(os.path.join(d, "export_report.json")) as fh:
                self.assertEqual(json.load(fh)["failed"], [])


class ClipSelectionTests(unittest.TestCase):
    def test_a_regex_matching_nothing_is_an_error(self):
        with self.assertRaises(RuntimeError) as cm:
            ebp.select_actions(["Idle", "Walk_F"], "^Run", )
        self.assertIn("^Run", str(cm.exception))

    def test_the_baked_leftovers_and_non_matches_are_skipped(self):
        self.assertEqual(ebp.select_actions(["Walk_F", "Walk_F__30fps", "Idle"], "Walk"), ["Walk_F"])

    def test_a_collision_among_the_selected_actions_is_an_error(self):
        with self.assertRaises(RuntimeError) as cm:
            ebp.select_actions(["Walk.001", "Walk_001"], ".")
        self.assertIn("Walk.001", str(cm.exception))
        self.assertIn("Walk_001", str(cm.exception))


if __name__ == "__main__":
    unittest.main()
