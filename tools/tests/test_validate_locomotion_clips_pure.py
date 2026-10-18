"""The gates of tools/blender/validate_locomotion_clips.py, with bpy and mathutils stubbed (no Blender).

Each gate is exercised in both directions on synthetic poses: a clip that must pass and one that must fail. A frame here
is {bone: object with .translation}, which is all the gates read of a re-imported FBX pose.
"""
import copy
import math
import os
import sys
import unittest
from unittest import mock

BLENDER_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "blender")


def _import_validator():
    stubs = {"bpy": mock.MagicMock(), "mathutils": mock.MagicMock()}
    names = ("validate_locomotion_clips", "restyle_human_clip", "retarget_mannequin_to_human")
    with mock.patch.dict(sys.modules, stubs), mock.patch.object(sys, "path", [BLENDER_DIR] + sys.path):
        for name in names:
            sys.modules.pop(name, None)
        module = __import__("validate_locomotion_clips")
        for name in names:
            sys.modules.pop(name, None)
    return module


v = _import_validator()
rs = v.rs


class P(tuple):
    """A point: a 3-tuple with .x .y .z, like a mathutils translation."""
    x = property(lambda self: self[0])
    y = property(lambda self: self[1])
    z = property(lambda self: self[2])

    @property
    def length(self):
        return math.sqrt(sum(c * c for c in self))

    def angle(self, other):   # like mathutils Vector.angle: the angle between two vectors, in radians
        dot = sum(a * b for a, b in zip(self, other))
        return math.acos(max(-1.0, min(1.0, dot / (self.length * P(other).length))))

    def __sub__(self, other):
        return P((self[0] - other[0], self[1] - other[1], self[2] - other[2]))


class Bone:
    def __init__(self, x, y, z):
        self.translation = P((x, y, z))


def frame(**bones):
    return {name: Bone(*xyz) for name, xyz in bones.items()}


# ---------------------------------------------------------------- declared_turn_deg
class DeclaredTurnTests(unittest.TestCase):
    def spec(self, **limp):
        return {"pelvis_tilt_deg": 12.0, "clavicle_drop_deg": 8.0, "clavicle_protract_deg": 10.0, "limp": limp}

    def test_the_lead_shoulder_follows_the_strong_side_when_it_names_none(self):
        spec = self.spec(strong_side="l", low_shoulder_deg=4.0)
        left = v.declared_turn_deg(spec, "l_clavicle")
        right = v.declared_turn_deg(spec, "r_clavicle")
        self.assertAlmostEqual(left - right, 4.0)

    def test_an_explicit_lead_side_wins_over_the_strong_side(self):
        spec = self.spec(strong_side="r", lead_shoulder_side="l", low_shoulder_deg=4.0, low_arm_forward_deg=6.0)
        self.assertGreater(v.declared_turn_deg(spec, "l_clavicle"), v.declared_turn_deg(spec, "r_clavicle"))
        self.assertAlmostEqual(v.declared_turn_deg(spec, "l_upperarm_twist") - v.declared_turn_deg(spec, "r_upperarm_twist"), 6.0)

    def test_a_default_limp_declares_nothing_beyond_the_base_posture(self):
        spec = self.spec()
        self.assertEqual(v.declared_turn_deg(spec, "pelvis"), 12.0)
        self.assertEqual(v.declared_turn_deg(spec, "l_clavicle"), v.declared_turn_deg(spec, "r_clavicle"))

    def test_the_hip_hike_in_metres_declares_the_most_roll_it_can_use(self):
        base = v.declared_turn_deg(self.spec(), "pelvis")
        hiked = v.declared_turn_deg(self.spec(weak_hip_hike_m=0.04), "pelvis")
        self.assertAlmostEqual(hiked - base, rs.HIKE_ROLL_MAX_DEG)

    def test_the_hip_hike_in_degrees_is_declared_as_before(self):
        self.assertAlmostEqual(v.declared_turn_deg(self.spec(weak_hip_hike_deg=4.0), "pelvis")
                               - v.declared_turn_deg(self.spec(), "pelvis"), 4.0)

    def test_a_spec_without_a_limp_block_does_not_crash(self):
        self.assertEqual(v.declared_turn_deg({"pelvis_tilt_deg": 5.0}, "pelvis"), 5.0)
        self.assertEqual(v.declared_turn_deg({}, "pelvis"), 0.0)

    def test_an_idle_is_allowed_only_the_arms_forward_turn_from_the_limp(self):
        spec = self.spec(strong_side="r", lead_shoulder_yaw_deg=12.0, low_shoulder_deg=4.0, low_shoulder_lean_deg=3.0,
                         lead_shoulder_protract_deg=3.0, weak_hip_hike_m=0.04, knee_bow_m=0.07, arm_forward_deg=10.0)
        spec["trunk_flex_deg"] = {"spine1": 14.0}
        for bone in ("spine1", "spine2", "neck", "head", "r_clavicle", "pelvis", "r_thigh", "r_calf"):
            gait = v.declared_turn_deg(spec, bone, kind="gait")
            idle = v.declared_turn_deg(spec, bone, kind="idle")
            self.assertGreaterEqual(gait, idle, bone)
        self.assertEqual(v.declared_turn_deg(spec, "spine1", kind="idle"), 14.0)            # the base flex only: no yaw share
        self.assertEqual(v.declared_turn_deg(spec, "r_clavicle", kind="idle"),
                         v.declared_turn_deg(self.spec(), "r_clavicle", kind="idle"))      # no low shoulder, no protract
        self.assertEqual(v.declared_turn_deg(spec, "pelvis", kind="idle"), 12.0)           # tilt only: no hike roll
        self.assertGreater(v.declared_turn_deg(spec, "spine1", kind="gait"), 14.0)
        # but the arms-forward turn is allowed on an idle's upper arm
        self.assertAlmostEqual(v.declared_turn_deg(spec, "r_upperarm_twist", kind="idle")
                               - v.declared_turn_deg(self.spec(), "r_upperarm_twist", kind="idle"), 10.0)

    def test_the_kind_defaults_to_a_gait(self):
        spec = self.spec(lead_shoulder_yaw_deg=12.0)
        self.assertEqual(v.declared_turn_deg(spec, "head"), v.declared_turn_deg(spec, "head", kind="gait"))

    def test_the_clearance_swing_is_added_to_the_upper_arm_only(self):
        spec = self.spec()
        extra = {"r_upperarm_twist": 16.0}
        self.assertAlmostEqual(v.declared_turn_deg(spec, "r_upperarm_twist", extra)
                               - v.declared_turn_deg(spec, "r_upperarm_twist"), 16.0)
        self.assertEqual(v.declared_turn_deg(spec, "l_upperarm_twist", extra), v.declared_turn_deg(spec, "l_upperarm_twist"))


# ---------------------------------------------------------------- contact_frames delegates to the restyle
class SlideDocstringTests(unittest.TestCase):
    def test_the_limp_slide_docstring_describes_the_independent_plateau(self):
        doc = v._limp_slide.__doc__
        self.assertNotIn("judged on the restyle report's contact intervals", doc)
        self.assertIn("independent", doc.lower())


class ContactFramesTests(unittest.TestCase):
    def test_it_is_the_restyles_definition(self):
        ys = [0.3 - 0.05 * k for k in range(8)] + [-0.1 + 0.1 * k for k in range(5)]
        ys = ys + [ys[0]]
        zs = [0.09] * 8 + [0.09 + 0.05 * k for k in range(5)] + [0.09]
        frames = [{"r_foot": Bone(0.1, y, z)} for y, z in zip(ys, zs)]
        self.assertEqual(v.contact_frames(frames, "r_foot", True), rs.contact_indices(zs, ys, True))
        self.assertEqual(v.contact_frames(frames, "r_foot", False), rs.contact_indices(zs, ys, False))
        self.assertEqual(v.contact_frames(frames, "r_foot", True), list(range(8)))


# ---------------------------------------------------------------- _clearance_check
def body(r_hand_x=0.45, l_hand_x=-0.45, hand_y=0.0):
    """A standing body with both hands hanging at the given x (the right thigh sits at x 0.1)."""
    pos = {"pelvis": (0.0, 0.0, 0.9), "spine2": (0.0, 0.0, 1.4)}
    for side, x in (("l", -0.1), ("r", 0.1)):
        pos[side + "_thigh"], pos[side + "_calf"], pos[side + "_foot"] = (x, 0.0, 0.85), (x, 0.0, 0.45), (x, 0.0, 0.1)
    for side, hx in (("l", l_hand_x), ("r", r_hand_x)):
        sx = 0.2 if side == "r" else -0.2
        pos[side + "_upperarm_twist"] = (sx, 0.0, 1.35)
        pos[side + "_foretwist"] = (hx, hand_y, 1.05)
        pos[side + "_hand"] = (hx, hand_y, 0.8)
        pos[side + "_finger0"] = (hx, hand_y, 0.7)
    return frame(**pos)


class ClearanceCheckTests(unittest.TestCase):
    ENTRY = {"clip": "run_forward_1h"}
    SPEC = {"hand_clearance_m": 0.08, "shield_arm_clearance_m": 0.15}

    def check(self, frames, entry=None):
        return v._clearance_check(entry or self.ENTRY, frames, self.SPEC)

    def test_arms_clear_of_the_body_pass(self):
        out = self.check([body()] * 3)
        self.assertTrue(out["pass"])
        self.assertGreater(out["arms"]["r"]["min_gap_m"], 0.08)

    def test_a_hand_through_the_thigh_fails_only_that_arm(self):
        out = self.check([body(), body(r_hand_x=0.1), body()])
        self.assertFalse(out["pass"])
        self.assertFalse(out["arms"]["r"]["pass"])
        self.assertTrue(out["arms"]["l"]["pass"])
        self.assertLess(out["arms"]["r"]["min_gap_m"], 0.0)

    def test_one_short_frame_is_enough(self):
        near = body(r_hand_x=0.1 + 0.085 + 0.05 + 0.03)   # a 3 cm gap
        self.assertFalse(self.check([body()] * 5 + [near])["pass"])

    def test_the_half_centimetre_allowance_holds_both_ways(self):
        edge = 0.1 + 0.085 + 0.05 + 0.08 - 0.004          # 4 mm under the clearance: inside the allowance
        self.assertTrue(self.check([body(r_hand_x=edge)])["arms"]["r"]["pass"])
        edge = 0.1 + 0.085 + 0.05 + 0.08 - 0.008          # 8 mm under: outside it
        self.assertFalse(self.check([body(r_hand_x=edge)])["arms"]["r"]["pass"])

    def test_the_left_arm_of_a_shield_clip_needs_the_larger_clearance(self):
        hand = -(0.1 + 0.085 + 0.05 + 0.10)                # a 10 cm gap: enough for 0.08, short of 0.15
        frames = [body(l_hand_x=hand)]
        self.assertTrue(self.check(frames)["arms"]["l"]["pass"])
        shield = {"clip": "run_forward_1h_with_shield"}
        out = self.check(frames, shield)
        self.assertFalse(out["arms"]["l"]["pass"])
        self.assertEqual(out["arms"]["l"]["clearance_m"], 0.15)
        self.assertEqual(out["arms"]["r"]["clearance_m"], 0.08)

    def test_it_reads_the_clearance_of_idle_clips_too(self):
        out = self.check([body(r_hand_x=0.1)], {"clip": "stand_1h"})
        self.assertFalse(out["pass"])


# ---------------------------------------------------------------- _metrics_check
def leg_pose(knee_x=0.1, side=1.0, pelvis_x=0.0, foot_x=0.1, hip_z=0.9, knee_z=0.5, foot_z=0.1, knee_y=0.1):
    pos = {"pelvis": (pelvis_x, 0.0, hip_z), "spine2": (pelvis_x, 0.0, hip_z + 0.5)}
    for s, sx in (("l", -1.0), ("r", 1.0)):
        for b, z in (("upperarm_twist", 1.35), ("foretwist", 1.05), ("hand", 0.8), ("finger0", 0.7)):
            pos[s + "_" + b] = (sx * 0.4, 0.0, z)
        pos[s + "_thigh"] = (sx * 0.1, 0.0, hip_z)
        pos[s + "_calf"] = (sx * 0.1 + (knee_x - 0.1) * sx, knee_y, knee_z)
        pos[s + "_foot"] = (sx * foot_x, 0.0, foot_z)
        pos[s + "_toe0"] = (sx * foot_x, 0.1, foot_z - 0.08)
    return frame(**pos)


class MetricsCheckTests(unittest.TestCase):
    def entry(self, **kw):
        base = {"achieved": {"knee_bow_m": {"min": 0.02, "mean": 0.08}}, "track_centre_x_m": 0.0, "period_frames": 24,
                "strong_foot": "r_foot", "contacts": {"r_foot": {"contact_start_frame": 2.0, "contact_end_frame": 8.0},
                                                       "l_foot": {"contact_start_frame": 14.0, "contact_end_frame": 20.0}}}
        base.update(kw)
        return base

    def run_check(self, limp, posed, entry=None, src_w=None):
        spec = {"limp": limp}
        return v._metrics_check(entry or self.entry(), src_w or posed, posed, spec)

    def test_no_targets_means_nothing_to_gate(self):
        self.assertTrue(self.run_check({}, [leg_pose()])["pass"])

    def test_the_knee_max_gate_holds_both_ways(self):
        straight = leg_pose(knee_y=0.0)                           # hip 0.9, knee 0.5, ankle 0.1 in line: 180 degrees
        bent = leg_pose(knee_y=0.1, hip_z=0.8)                    # knee forward of a lower hip: about 147 degrees
        self.assertFalse(self.run_check({"knee_max_deg": 160.0}, [straight])["pass"])
        self.assertTrue(self.run_check({"knee_max_deg": 160.0}, [bent])["pass"])

    def test_the_track_half_gate_measures_from_the_clip_centre_not_the_moving_pelvis(self):
        feet = [leg_pose(foot_x=0.15, pelvis_x=px) for px in (-0.1, 0.0, 0.12)]   # the pelvis wanders, the feet do not
        ok = self.run_check({"min_track_half_m": 0.14}, feet, self.entry(track_centre_x_m=0.0))
        self.assertTrue(ok["pass"])
        self.assertAlmostEqual(ok["checks"]["min_track_half_m"]["measured"], 0.15, places=4)
        narrow = self.run_check({"min_track_half_m": 0.14}, feet, self.entry(track_centre_x_m=0.05))
        self.assertFalse(narrow["pass"])    # from a centre 5 cm out, the right foot is only 10 cm away

    def test_a_missing_centre_fails_the_track_gate(self):
        entry = self.entry()
        del entry["track_centre_x_m"]
        out = self.run_check({"min_track_half_m": 0.14}, [leg_pose(foot_x=0.2)], entry)
        self.assertFalse(out["pass"])

    def test_the_weak_lift_cap_holds_both_ways(self):
        low = [leg_pose(foot_z=0.1 + 0.02) for _ in range(24)]
        high = [leg_pose(foot_z=0.1 + 0.02) for _ in range(24)]
        high[3] = leg_pose(foot_z=0.1 + 0.09)                     # frame 3 is in the weak swing (contact 14 to 20)
        src = [leg_pose(foot_z=0.1)]
        self.assertTrue(self.run_check({"weak_lift_max_m": 0.03}, low, src_w=src)["pass"])
        self.assertFalse(self.run_check({"weak_lift_max_m": 0.03}, high, src_w=src)["pass"])

    def test_the_knee_bow_is_measured_on_the_posed_frames_not_read_from_the_report(self):
        bowed = [leg_pose(knee_x=0.18) for _ in range(4)]         # knees 8 cm outside the hip-ankle line
        straight = [leg_pose(knee_x=0.10) for _ in range(4)]
        # the report claims a good bow in both cases: only the posed frames decide
        self.assertTrue(self.run_check({"knee_bow_m": 0.07}, bowed)["pass"])
        self.assertFalse(self.run_check({"knee_bow_m": 0.07}, straight)["pass"])

    def test_a_missing_achieved_block_fails_when_the_spec_has_metric_targets(self):
        entry = self.entry()
        del entry["achieved"]
        out = self.run_check({"weak_hip_hike_m": 0.04}, [leg_pose()], entry)
        self.assertFalse(out["pass"])
        self.assertIn("achieved", str(out))

    def test_the_gates_come_from_the_spec_not_from_the_report(self):
        out = self.run_check({"min_track_half_m": 0.14}, [leg_pose(foot_x=0.05)], self.entry(achieved={}))
        self.assertFalse(out["pass"])


# ---------------------------------------------------------------- _limp_slide
P_LOOP = 24


def foot_track(a, length, speed, sideways=0.0, slow_landing=0.0, x0=0.15, drift_from=0, landing_dx=0.0, takeoff_dx=0.0):
    """P_LOOP + 1 (x, y, z) samples of a foot planted for `length` frames from a: falling back `speed` a frame, drifting
    `sideways` a frame from the planted frame `drift_from` on, and, with slow_landing, a heel strike whose first frame
    moves that much LESS than the plateau (the foot rolls onto the ground), then a swing back to the start. landing_dx
    is a sideways step on the first planted frame and takeoff_dx one on the last (the transition frames)."""
    pts = []
    y_a = speed * length / 2.0
    y_b = y_a - speed * length + slow_landing
    for k in range(P_LOOP):
        rel = (k - a) % P_LOOP
        if rel <= length:
            y = y_a - speed * rel + (slow_landing if rel > 0 else 0.0)
            dx = (landing_dx if rel > 0 else 0.0) + (takeoff_dx if rel >= length else 0.0)
            pts.append((x0 + sideways * max(0, rel - drift_from) + dx, y, 0.09))
        else:
            t = (rel - length) / (P_LOOP - length)
            pts.append((x0 + sideways * max(0, length - drift_from) * (1.0 - t) + landing_dx * (1.0 - t) + takeoff_dx * (1.0 - t) ** 0,
                        y_b + (y_a - y_b) * (1.0 - math.cos(math.pi * t)) / 2.0, 0.09 + 0.12 * math.sin(math.pi * t)))
    pts.append(pts[0])
    return pts


def clip_frames(r, l):
    return [{"r_foot": Bone(*r[k]), "l_foot": Bone(*l[k])} for k in range(P_LOOP + 1)]


class LimpSlideTests(unittest.TestCase):
    SPEED = 0.10
    LEN = 6

    def entry(self, frames=None):
        def row(a):
            return {"frames": list(frames(a) if frames else range(a + 1, a + self.LEN)),
                    "contact_start_frame": float(a), "contact_end_frame": float(a + self.LEN)}
        return {"period_frames": P_LOOP, "planted_speed_m_per_frame": self.SPEED, "strong_foot": "r_foot",
                "timing_split": 0.5, "contacts": {"r_foot": row(2), "l_foot": row(14)}}

    def slide(self, sideways, src_slow=0.0, frames=None, drift_from=0, out_ends=None, src_ends=None):
        oe, se = out_ends or {}, src_ends or {}
        out = clip_frames(foot_track(2, self.LEN, self.SPEED, sideways, drift_from=drift_from, **oe),
                          foot_track(14, self.LEN, self.SPEED, sideways, drift_from=drift_from, **oe))
        src = clip_frames(foot_track(2, self.LEN, self.SPEED, 0.0, src_slow, **se), foot_track(14, self.LEN, self.SPEED, 0.0, src_slow, **se))
        return v._limp_slide(self.entry(frames), src, out, True, 1.0)

    def test_a_steady_planted_foot_passes(self):
        out = self.slide(0.0)
        self.assertTrue(out["feet"]["r_foot"]["pass"], out["feet"])
        self.assertTrue(out["feet"]["l_foot"]["pass"], out["feet"])

    def test_a_tenth_of_a_centimetre_sideways_still_passes(self):
        self.assertTrue(self.slide(0.001)["feet"]["r_foot"]["pass"])

    def test_a_foot_sliding_sideways_one_and_a_half_centimetres_a_frame_fails(self):
        out = self.slide(0.015)
        self.assertFalse(out["feet"]["r_foot"]["pass"])
        self.assertFalse(out["pass"])

    def test_the_lateral_and_vertical_speeds_are_reported_against_the_source_plateau(self):
        row = self.slide(0.0)["feet"]["r_foot"]
        self.assertIn("lateral_cm", row)
        self.assertIn("vertical_cm", row)
        self.assertLessEqual(row["lateral_cm"][0], row["lateral_cm"][1] + v.TOL["slide_cm"])

    def test_a_slow_heel_strike_in_the_source_contact_does_not_loosen_the_limit(self):
        # the source's whole contact opens with a frame 5 cm slower than its plateau; judged against the plateau, a clip
        # drifting 1.5 cm a frame still fails
        out = self.slide(0.015, src_slow=0.05)
        self.assertFalse(out["feet"]["r_foot"]["pass"])

    def test_the_source_plateau_residual_is_the_one_compared(self):
        row = self.slide(0.0, src_slow=0.05)["feet"]["r_foot"]
        self.assertLess(row["residual_cm"][1], 1.0)   # not the 5 cm of the heel strike

    # ---- the transition frames the plateau trim leaves out are still gated, on lateral speed
    def test_the_trimmed_end_frames_are_reported_with_a_source_reference(self):
        row = self.slide(0.0)["feet"]["r_foot"]
        self.assertIn("end_frame_lateral_cm", row)
        self.assertEqual(len(row["end_frames"]), 2)   # the landing frame and the take-off frame

    def test_a_gross_landing_skid_fails_though_the_trim_hides_it_from_the_plateau(self):
        out = self.slide(0.0, out_ends={"landing_dx": 0.03})           # 3 cm sideways on the landing frame, source quiet
        row = out["feet"]["r_foot"]
        self.assertFalse(row["pass"], row)
        self.assertGreater(row["end_frame_lateral_cm"][0], row["end_frame_lateral_cm"][1] + 1.0)
        self.assertTrue(row["residual_cm"][0] < 1.0)                    # the plateau itself is clean: only the ends fail

    def test_a_gross_take_off_skid_fails_too(self):
        self.assertFalse(self.slide(0.0, out_ends={"takeoff_dx": 0.03})["feet"]["r_foot"]["pass"])

    def test_the_sources_own_landing_motion_is_not_a_fault(self):
        # the same 1.8 cm sideways step on the landing frame in the clip and in the source (the vanilla run does this)
        out = self.slide(0.0, out_ends={"landing_dx": 0.018}, src_ends={"landing_dx": 0.018})
        self.assertTrue(out["feet"]["r_foot"]["pass"], out["feet"]["r_foot"])

    def test_a_small_excess_at_the_ends_is_within_the_bound(self):
        out = self.slide(0.0, out_ends={"landing_dx": 0.008}, src_ends={})
        self.assertTrue(out["feet"]["r_foot"]["pass"], out["feet"]["r_foot"])

    # ---- convergence D1: claimed frames the clip stalls, skids or lifts on must not go unjudged
    def test_scenario_a_a_claimed_frame_the_foot_stalls_and_drifts_on_fails(self):
        # the report names 3..7; on frames 6 and 7 the clip's foot stops (skates forward 10 cm a frame relative to the
        # ground) and drifts 2 cm a frame sideways. Those frames drop out of the plateau found on the poses.
        src = clip_frames(foot_track(2, self.LEN, self.SPEED), foot_track(14, self.LEN, self.SPEED))
        out = copy.deepcopy(src)
        y_hold = out[6]["r_foot"].translation.y
        for k in (7, 8):
            out[k]["r_foot"] = Bone(0.15 + 0.02 * (k - 6), y_hold, 0.09)
        res = v._limp_slide(self.entry(), src, out, True, 1.0)
        row = res["feet"]["r_foot"]
        self.assertFalse(row["pass"], row)
        self.assertTrue(row["claimed_outside"], row)    # the claimed frames that are on no found plateau are named

    def test_scenario_b_a_claimed_landing_frame_that_skids_back_fails(self):
        # the report claims the landing frame (2..7); the clip's ankle moves back 20 cm on it, against a planted speed of 10
        src = clip_frames(foot_track(2, self.LEN, self.SPEED), foot_track(14, self.LEN, self.SPEED))
        out = clip_frames(foot_track(2, self.LEN, self.SPEED, slow_landing=-0.10), foot_track(14, self.LEN, self.SPEED))
        res = v._limp_slide(self.entry(frames=lambda a: range(a, a + 6)), src, out, True, 1.0)
        row = res["feet"]["r_foot"]
        self.assertFalse(row["pass"], row)
        self.assertGreater(row["end_frame_back_cm"][0], row["end_frame_back_cm"][1])

    def test_an_end_frame_may_move_back_slower_than_the_plateau(self):
        # a heel that rolls onto the floor (slower than the planted speed) is the normal landing
        out = self.slide(0.0, src_slow=0.05, out_ends={})
        self.assertTrue(out["feet"]["r_foot"]["pass"], out["feet"]["r_foot"])

    def test_a_claimed_frame_on_the_end_of_the_found_plateau_is_not_outside(self):
        out = self.slide(0.0, frames=lambda a: range(a, a + 6))      # claims the landing frame too: it is an end frame
        self.assertEqual(out["feet"]["r_foot"]["claimed_outside"], [])
        self.assertTrue(out["feet"]["r_foot"]["pass"])

    # ---- convergence D6: the end-frame reference follows the clip's time map
    def test_the_end_frame_reference_is_read_where_the_warp_plays_the_source(self):
        # timing_split 0.40 against a source split of 0.5: the weak foot lands 2.4 frames earlier, so output frame 12
        # plays source position ~14.4. The source is quiet there; it has a big sideways step at index 12 instead. Read at
        # the same index (the old rule) that step loosens the gate; read through the warp it does not.
        warp = rs.Warp(2 / 24.0, 0.5, 0.40)
        src_l = foot_track(14, self.LEN, self.SPEED)
        src_l = [(x + (0.03 if k > 12 else 0.0), y, z) for k, (x, y, z) in enumerate(src_l)]    # a step at index 12
        src = clip_frames(foot_track(2, self.LEN, self.SPEED), src_l)
        out = clip_frames(foot_track(2, self.LEN, self.SPEED), foot_track(12, self.LEN, self.SPEED, landing_dx=0.03))
        entry = self.entry()
        entry["contacts"]["l_foot"] = {"frames": list(range(13, 18)), "contact_start_frame": 12.0, "contact_end_frame": 18.0}
        entry["warp"] = warp.report()
        res = v._limp_slide(entry, src, out, True, 1.0)
        self.assertFalse(res["feet"]["l_foot"]["pass"], res["feet"]["l_foot"])
        self.assertGreater(res["feet"]["l_foot"]["end_frame_lateral_cm"][0], res["feet"]["l_foot"]["end_frame_lateral_cm"][1] + 1.0)

    # ---- Codex F1: the producer's frame list is not trusted
    def two_quiet_frames(self, a):
        return [a + 1, a + 2]   # the report names the two quiet frames only

    def test_the_counterexample_two_quiet_frames_reported_while_the_foot_slides_in_the_rest_fails(self):
        # grounded for six frames; the first two are steady, the last four slide 1.5 cm a frame. The report lists only the
        # two steady ones. The validator finds the grounded frames itself and must fail it.
        out = self.slide(0.015, frames=self.two_quiet_frames, drift_from=2)
        row = out["feet"]["r_foot"]
        self.assertFalse(row["pass"], row)
        self.assertFalse(out["pass"])

    def test_the_independent_plateau_is_what_the_residual_is_measured_over(self):
        out = self.slide(0.015, frames=self.two_quiet_frames, drift_from=2)
        row = out["feet"]["r_foot"]
        self.assertGreater(row["residual_cm"][0], 1.0)           # the slide in the omitted frames is in it
        self.assertGreater(row["lateral_cm"][0], 1.0)

    def test_a_report_that_omits_most_of_the_plateau_fails_even_without_a_slide(self):
        out = self.slide(0.0, frames=self.two_quiet_frames)
        row = out["feet"]["r_foot"]
        self.assertFalse(row["pass"], row)
        self.assertGreater(row["frames_missing"], v.TOL["plateau_missing_frames"])

    def test_a_report_that_lists_the_whole_plateau_passes(self):
        out = self.slide(0.0)
        row = out["feet"]["r_foot"]
        self.assertTrue(row["pass"], row)
        self.assertLessEqual(row["frames_missing"], v.TOL["plateau_missing_frames"])

    def test_a_report_that_lists_extra_frames_is_not_penalised_for_coverage(self):
        out = self.slide(0.0, frames=lambda a: range(a, a + self.LEN + 2))
        self.assertEqual(out["feet"]["r_foot"]["frames_missing"], 0)

    def test_the_source_plateau_is_found_from_the_source_poses_not_the_report(self):
        # an entry with a lying source_frames changes nothing: the source plateau comes from the source poses
        base = self.slide(0.0)
        entry = self.entry()
        for row in entry["contacts"].values():
            row["source_frames"] = [0.0, 1.0]
        out = clip_frames(foot_track(2, self.LEN, self.SPEED), foot_track(14, self.LEN, self.SPEED))
        again = v._limp_slide(entry, out, out, True, 1.0)
        self.assertEqual(again["feet"]["r_foot"]["residual_cm"], base["feet"]["r_foot"]["residual_cm"])


if __name__ == "__main__":
    unittest.main()
