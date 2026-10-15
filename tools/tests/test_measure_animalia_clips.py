"""Unit tests for the bpy-free parts of tools/blender/measure_animalia_clips.py: on_ground, plant_start, stride_speed.

Run:  python -m unittest tools.tests.test_measure_animalia_clips

The tool imports bpy, mathutils (through the reskin module) and reskin_animalia_to_horse at module level, so stubs go
in sys.modules for the load only. The Blender side (importing the FBX, stepping frames) is proven in a live re-measure.

Frames: a clip of n frames loops over `loop` = n - 1 frames and its last frame repeats frame 0, so a z series has
loop + 1 values and a contact is judged on frames 0..loop-1 taken as a circle.
"""
import importlib.util
import math
import os
import sys
import types
import unittest
from unittest import mock

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "measure_animalia_clips.py")

_spec = importlib.util.spec_from_file_location("taom_measure_animalia_clips", SCRIPT)
mac = importlib.util.module_from_spec(_spec)
_stubs = {"bpy": types.ModuleType("bpy"), "mathutils": types.ModuleType("mathutils"),
          "reskin_animalia_to_horse": types.ModuleType("reskin_animalia_to_horse")}
with mock.patch.dict(sys.modules, _stubs), mock.patch.object(sys, "path", list(sys.path)):
    _spec.loader.exec_module(mac)

UP, DOWN = 0.40, 0.0   # hoof height in the air, on the ground (the threshold is lowest + 2 cm)


def z_series(loop, contacts):
    """A hoof z series of loop + 1 values: on the ground for each (start, length) contact taken as a circle, else up.
    The last value repeats frame 0, as a closed clip does."""
    zs = [UP] * loop
    for start, length in contacts:
        for k in range(length):
            zs[(start + k) % loop] = DOWN
    return zs + [zs[0]]


class V:
    """A point with the x, y, z and subtraction stride_speed reads."""
    def __init__(self, x, y, z):
        self.x, self.y, self.z = x, y, z

    def __sub__(self, o):
        return V(self.x - o.x, self.y - o.y, self.z - o.z)


class OnGroundTests(unittest.TestCase):
    def test_a_frame_is_on_the_ground_within_2_cm_of_the_lowest_point(self):
        self.assertEqual(mac.CONTACT_M, 0.02)
        zs = [0.10, 0.115, 0.1201, 0.5, 0.10]
        self.assertEqual(mac.on_ground(zs, 4), [True, True, False, False, True])

    def test_the_lowest_point_comes_from_the_loop_frames_only(self):
        # the closing frame repeats frame 0 in a clean clip; a lower value there must not move the threshold
        self.assertEqual(mac.on_ground([0.5, 0.1, 0.5, -1.0], 3), [False, True, False, True])


class PlantStartTests(unittest.TestCase):
    LOOP = 40

    def test_four_plants_on_a_synthetic_walk(self):
        starts = {"LB": 0, "LF": 10, "RB": 20, "RF": 30}
        plants = {k: mac.plant_start(z_series(self.LOOP, [(s, 12)]), self.LOOP) for k, s in starts.items()}
        self.assertEqual(plants, {"LB": 0.0, "LF": 0.25, "RB": 0.5, "RF": 0.75})
        self.assertEqual(sorted(plants.values()), [0.0, 0.25, 0.5, 0.75])

    def test_a_contact_starting_on_frame_zero_still_counts(self):
        # the 2026-10-14 wrap fix: frame loop - 1 is in the air, frame 0 is the first on the ground
        self.assertEqual(mac.plant_start(z_series(self.LOOP, [(0, 15)]), self.LOOP), 0.0)

    def test_a_short_scuff_before_a_long_stance_picks_the_stance(self):
        # boar WalkMedium_F RF: contacts (21, 2) and (34, 21) on a 35 frame loop were measured as 0.6, not 0.9714
        zs = z_series(35, [(21, 2), (34, 21)])
        self.assertEqual(mac.plant_start(zs, 35), round(34 / 35, 4))

    def test_a_stance_across_the_wrap_starts_where_it_began(self):
        zs = z_series(self.LOOP, [(36, 8)])   # frames 36..39 and 0..3
        self.assertEqual(mac.plant_start(zs, self.LOOP), round(36 / self.LOOP, 4))

    def test_a_long_stance_across_the_wrap_beats_an_earlier_scuff(self):
        zs = z_series(self.LOOP, [(5, 2), (30, 15)])   # the stance runs 30..39 and 0..4
        self.assertEqual(mac.plant_start(zs, self.LOOP), round(30 / self.LOOP, 4))

    def test_two_equal_strides_pick_the_earlier(self):
        zs = z_series(self.LOOP, [(8, 10), (28, 10)])
        self.assertEqual(mac.plant_start(zs, self.LOOP), 0.2)

    def test_a_contact_at_least_half_the_longest_is_kept(self):
        zs = z_series(self.LOOP, [(4, 5), (20, 10)])   # 5 is exactly half of 10
        self.assertEqual(mac.plant_start(zs, self.LOOP), 0.1)

    def test_a_contact_just_under_half_is_dropped(self):
        zs = z_series(self.LOOP, [(4, 4), (20, 10)])
        self.assertEqual(mac.plant_start(zs, self.LOOP), 0.5)

    def test_a_hoof_always_on_the_ground_has_no_plant(self):
        self.assertIsNone(mac.plant_start(z_series(self.LOOP, [(0, self.LOOP)]), self.LOOP))

    def test_the_result_is_rounded_to_four_places(self):
        self.assertEqual(mac.plant_start(z_series(7, [(1, 3)]), 7), round(1 / 7, 4))


class StrideSpeedTests(unittest.TestCase):
    def test_a_planted_hoof_sliding_back_gives_its_speed(self):
        loop = 20
        zs = z_series(loop, [(0, 12)])
        steps = [0.01] * 12 + [0.2] * (loop - 12) + [0.0]
        pos = {"LF": [V(sum(steps[:i]), 0.0, z) for i, z in enumerate(zs)]}
        # frames 1..11 are planted after a planted frame: eleven steps of 0.01; the swing frames are ignored
        self.assertAlmostEqual(mac.stride_speed(pos, loop), 0.01, places=9)

    def test_the_median_over_hooves_and_frames(self):
        loop = 10
        zs = z_series(loop, [(0, loop)])
        slow = {"LF": [V(0.01 * i, 0.0, 0.0) for i in range(loop + 1)]}
        fast = {"RF": [V(0.03 * i, 0.0, 0.0) for i in range(loop + 1)]}
        both = dict(slow, **fast)
        self.assertAlmostEqual(mac.stride_speed(slow, loop), 0.01, places=9)
        self.assertAlmostEqual(mac.stride_speed(both, loop), 0.03, places=9)   # median of ten 0.01 and ten 0.03 is the upper

    def test_horizontal_speed_ignores_height(self):
        loop = 4
        pos = {"LF": [V(0.0, 0.0, 0.0), V(0.0, 0.01, 0.015), V(0.0, 0.02, 0.0), V(0.0, 0.03, 0.01), V(0.0, 0.04, 0.0)]}
        self.assertAlmostEqual(mac.stride_speed(pos, loop), 0.01, places=9)

    def test_a_hoof_that_never_plants_two_frames_running_gives_zero(self):
        loop = 6
        zs = [0.0, 0.5, 0.0, 0.5, 0.0, 0.5, 0.0]
        pos = {"LF": [V(0.1 * i, 0.0, z) for i, z in enumerate(zs)]}
        self.assertEqual(mac.stride_speed(pos, loop), 0.0)

    def test_the_result_is_the_old_inline_formula(self):
        loop = 12
        zs = [0.0, 0.01, 0.019, 0.3, 0.3, 0.0, 0.0, 0.0, 0.02, 0.5, 0.0, 0.0, 0.0]
        pos = {"LF": [V(0.02 * i + 0.003 * (i % 3), 0.0, z) for i, z in enumerate(zs)]}
        speeds = []
        for ps in pos.values():
            lo = min(p.z for p in ps[:loop]) + 0.02
            for i in range(1, loop + 1):
                if ps[i].z <= lo and ps[i - 1].z <= lo:
                    dv = ps[i] - ps[i - 1]
                    speeds.append(math.hypot(dv.x, dv.y))
        speeds.sort()
        self.assertEqual(mac.stride_speed(pos, loop), speeds[len(speeds) // 2])


if __name__ == "__main__":
    unittest.main()
