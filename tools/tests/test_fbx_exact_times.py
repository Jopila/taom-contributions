#!/usr/bin/env python3
"""Unit tests for tools/blender/fbx_exact_times.py (exact FBX tick times around Blender 5.2's exporter).

Run:  python -m unittest tools.tests.test_fbx_exact_times -v

Blender's FBX exporter turns frames into ticks with a float product and truncates, so for some frame counts
the last key and the take stop land one tick under their frame. These tests cover the pure rounding and the
patch/restore logic against fake io_scene_fbx modules; the Blender-side proof is a real export.
"""
import importlib.util
import os
import sys
import types
import unittest
from unittest import mock

import numpy as np

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "fbx_exact_times.py")

_spec = importlib.util.spec_from_file_location("taom_fbx_exact_times", SCRIPT)
fet = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fet)

KTIME = 46186158000
FRAME30 = 1539538600


class ExactTicksTests(unittest.TestCase):
    def test_every_frame_count_30fps_from_truncated_and_exact_input(self):
        for n in range(1, 2001):
            want = n * FRAME30
            truncated = int(n / 30 * KTIME)
            self.assertEqual(fet.exact_ticks(truncated, 30, KTIME), want, "truncated n=%d" % n)
            self.assertEqual(fet.exact_ticks(want, 30, KTIME), want, "exact n=%d" % n)

    def test_some_frame_counts_really_truncate(self):
        self.assertTrue(any(int(n / 30 * KTIME) < n * FRAME30 for n in range(1, 2001)))

    def test_24_and_25_fps(self):
        for fps in (24, 25):
            for n in range(1, 500):
                exact = n * KTIME // fps if (n * KTIME) % fps == 0 else round(n * KTIME / fps)
                got = fet.exact_ticks(int(n / fps * KTIME), fps, KTIME)
                self.assertEqual(got, exact, "fps=%d n=%d" % (fps, n))

    def test_numpy_array_input(self):
        frames = np.arange(0, 400)
        truncated = (frames / 30 * KTIME).astype(np.int64)
        got = fet.exact_ticks(truncated, 30, KTIME)
        self.assertEqual(list(got), [int(n) * FRAME30 for n in frames])
        self.assertEqual(got.dtype, np.int64)

    def test_idempotent_on_exact_input(self):
        arr = np.arange(0, 100, dtype=np.int64) * FRAME30
        once = fet.exact_ticks(arr, 30, KTIME)
        self.assertEqual(list(fet.exact_ticks(once, 30, KTIME)), list(once))

    def test_scalar_returns_int(self):
        self.assertIsInstance(fet.exact_ticks(5 * FRAME30 - 1, 30, KTIME), int)


class _FakeFbxModules:
    """Installs fake bpy, io_scene_fbx.fbx_utils and io_scene_fbx.export_fbx_bin into sys.modules."""

    def __init__(self, fps=30, fps_base=1.0):
        self.seen = []
        seen = self.seen

        class Wrapper:
            def set_keyframes(self_inner, keyframe_times, keyframe_values):
                seen.append(list(keyframe_times))

        self.Wrapper = Wrapper
        self.orig_set = Wrapper.set_keyframes
        self.fbx_utils = types.ModuleType("io_scene_fbx.fbx_utils")
        self.fbx_utils.AnimationCurveNodeWrapper = Wrapper
        self.export = types.ModuleType("io_scene_fbx.export_fbx_bin")
        self.orig_convert = lambda v: v * 46186158000.0
        self.export.convert_sec_to_ktime = self.orig_convert
        self.pkg = types.ModuleType("io_scene_fbx")
        self.pkg.fbx_utils = self.fbx_utils
        self.pkg.export_fbx_bin = self.export
        self.bpy = types.ModuleType("bpy")
        render = types.SimpleNamespace(fps=fps, fps_base=fps_base)
        self.bpy.context = types.SimpleNamespace(scene=types.SimpleNamespace(render=render))

    def patch(self):
        return mock.patch.dict(sys.modules, {
            "bpy": self.bpy, "io_scene_fbx": self.pkg,
            "io_scene_fbx.fbx_utils": self.fbx_utils, "io_scene_fbx.export_fbx_bin": self.export})


class ContextManagerTests(unittest.TestCase):
    def test_patched_inside_restored_after(self):
        fake = _FakeFbxModules()
        with fake.patch():
            with fet.exact_fbx_times() as state:
                self.assertTrue(state.patched)
                self.assertIsNot(fake.Wrapper.set_keyframes, fake.orig_set)
                self.assertIsNot(fake.export.convert_sec_to_ktime, fake.orig_convert)
            self.assertIs(fake.Wrapper.set_keyframes, fake.orig_set)
            self.assertIs(fake.export.convert_sec_to_ktime, fake.orig_convert)

    def test_restored_after_exception(self):
        fake = _FakeFbxModules()
        with fake.patch():
            with self.assertRaises(RuntimeError):
                with fet.exact_fbx_times():
                    raise RuntimeError("boom")
            self.assertIs(fake.Wrapper.set_keyframes, fake.orig_set)
            self.assertIs(fake.export.convert_sec_to_ktime, fake.orig_convert)

    def test_patched_set_keyframes_passes_exact_times(self):
        fake = _FakeFbxModules()
        n = next(k for k in range(1, 2001) if int(k / 30 * KTIME) < k * FRAME30)
        truncated = np.array([0, int(n / 30 * KTIME)], dtype=np.int64)
        with fake.patch():
            with fet.exact_fbx_times():
                fake.Wrapper().set_keyframes(truncated, [1, 2])
        self.assertEqual(list(fake.seen[0]), [0, n * FRAME30])

    def test_convert_sec_to_ktime_returns_exact_tick(self):
        fake = _FakeFbxModules()
        n = next(k for k in range(1, 2001) if int(k / 30 * KTIME) < k * FRAME30)
        with fake.patch():
            with fet.exact_fbx_times():
                self.assertEqual(int(fake.export.convert_sec_to_ktime(n / 30)), n * FRAME30)

    def test_non_unit_fps_base_patches_nothing(self):
        fake = _FakeFbxModules(fps=30, fps_base=1.001)
        with fake.patch():
            with fet.exact_fbx_times() as state:
                self.assertFalse(state.patched)
                self.assertIn("fps_base", state.reason)
                self.assertIs(fake.Wrapper.set_keyframes, fake.orig_set)
                self.assertIs(fake.export.convert_sec_to_ktime, fake.orig_convert)

    def test_non_integral_fps_patches_nothing(self):
        fake = _FakeFbxModules(fps=29.97)
        with fake.patch():
            with fet.exact_fbx_times() as state:
                self.assertFalse(state.patched)
                self.assertIs(fake.Wrapper.set_keyframes, fake.orig_set)


if __name__ == "__main__":
    unittest.main()
