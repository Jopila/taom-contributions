"""Pure parts of tools/blender/restyle_human_clip.py, with bpy and mathutils stubbed (no Blender).

Reachable without real mathutils: load_spec, identity_spec, cap_violations, clip_kind, error_out_dir and
clear_stale_verdict. The pose math (IK, quaternion means, the source sampler) needs mathutils and is proven by the
headless parity run, not here.
"""
import json
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
                self.assertEqual(rs.cap_violations(spec), [])

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


if __name__ == "__main__":
    unittest.main()
