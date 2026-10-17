#!/usr/bin/env python3
"""Unit tests for tools/blender/blender_job.py (no Blender needed: bpy is imported lazily).

Run:  python -m unittest tools.tests.test_blender_job
"""
import argparse
import importlib.util
import json
import os
import tempfile
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location("blender_job", os.path.join(TOOLS, "blender", "blender_job.py"))
bj = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bj)


class JobTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.path = os.path.join(self.tmp.name, "_report", "DONE.txt")

    def tearDown(self):
        self.tmp.cleanup()

    def read(self):
        with open(self.path, encoding="utf-8") as f:
            return json.load(f)

    def test_ok_only_after_the_block_returns(self):
        with bj.job(self.path) as report:
            report["tris"] = 84053
            self.assertFalse(os.path.exists(self.path))
        self.assertEqual(self.read(), {"status": "ok", "tris": 84053})
        self.assertEqual(next(iter(self.read())), "status")

    def test_stale_report_is_removed_on_entry(self):
        os.makedirs(os.path.dirname(self.path))
        with open(self.path, "w") as f:
            f.write('{"status": "ok", "old": true}')
        with bj.job(self.path):
            self.assertFalse(os.path.exists(self.path))

    def test_raise_writes_error_and_keeps_the_fields(self):
        with bj.job(self.path) as report:
            report["tris"] = 1
            raise RuntimeError("export failed")
        r = self.read()
        self.assertEqual(r["status"], "error")
        self.assertEqual(r["tris"], 1)
        self.assertIn("export failed", r["trace"])

    def test_argparse_exit_writes_error(self):
        with bj.job(self.path):
            argparse.ArgumentParser().parse_args(["--nope"])
        self.assertEqual(self.read()["status"], "error")


class ArgTests(unittest.TestCase):
    def test_script_args_after_dashes(self):
        self.assertEqual(bj.script_args(["blender", "-b", "-P", "s.py", "--", "a", "--out", "x"]), ["a", "--out", "x"])
        self.assertEqual(bj.script_args(["blender", "-b"]), [])

    def test_flag_value_both_forms(self):
        self.assertEqual(bj.flag_value(["--blend", "b", "--out", "o"], "--out"), "o")
        self.assertEqual(bj.flag_value(["--out=o"], "--out"), "o")
        self.assertIsNone(bj.flag_value(["--out"], "--out"))

    def test_fbx_settings_are_the_kit_constant(self):
        self.assertTrue(bj.FBX_EXPORT["bake_space_transform"])
        self.assertFalse(bj.FBX_EXPORT["add_leaf_bones"])


if __name__ == "__main__":
    unittest.main()
