"""Restyle vanilla human_skeleton clips (walk, run, stand) into a per-variant gait or idle on the SAME skeleton.

Headless (the launcher DETACHES and prints nothing; read <out>/restyle_report.json and its .DONE twin). Delete
<out>/restyle_report.json.DONE before you launch: until Blender reaches main(), or after an import-time failure, the
file there is the previous run's verdict.
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe -b -P tools/blender/restyle_human_clip.py -- ^
        --source-dir "E:\\LOTRAOMAssets\\orc_locomotion\\src" ^
        --variant tools/blender/locomotion_variants/orc.json ^
        --engine-skeleton tools/blender/human_skeleton_engine.json ^
        --out "E:\\LOTRAOMAssets\\troll_clips_to_import\\orc_locomotion\\orc" ^
        [--preview "E:\\LOTRAOMAssets\\orc_locomotion\\previews\\orc"] ^
        [--body "E:\\LOTRAOMAssets\\human_skeleton_with_male_body.fbx"] [--only walk_forward_1h stand_1h] ^
        [--vanilla-step-points tools/blender/vanilla_locomotion_step_points.json]
    (--only merges its clips into the clips_index.json and restyle_report.json already in --out;
     --vanilla-step-points defaults to that file when it exists: only a limp clip uses it)

Input: --source-dir is the output of tools/read_anim_keyframes_tpac.ps1 -ByClip: clips_index.json (vanilla clip ->
master, source1, source2) and one keyframe JSON per master. Every clip in the index is restyled, or the --only ones.
Clips named walk_* and run_* are gaits; stand_* are idles. Other names are refused.

Output, per clip: <prefix>mst_<clip>.fbx, a master that holds ONLY that clip's frames (frame 0 = rest, frames
1..N = vanilla source1..source2), so a long idle master (anim_onehanded_guard_idles, 940 frames) is not restyled
whole. Also clips_index.json in the gen_troll_anim_clips.ps1 -CloneByName format (master anim_mst_<clip>, source1 0,
source2 N-1): that tool then names each master <prefix>mst_<clip> and each clip <prefix><clip>, and clones each
clip's flags, priority and usages from its own vanilla clip. The generator takes the travel scale (stride_scale, the
LoopDisplacement factor) from this run's report with -RestyleReport, not from a typed -TravelScale.

Method, per frame, on the engine-frame rig (retarget_mannequin_to_human.build_engine_rig):
  1. Source pose: the vanilla master sampled straight from its keyframe JSON (retarget_mannequin_to_human.
     json_pose_sampler, the matrices key_source_from_json keys), once per clip.
  2. Gaits: the upper arm's rotation deviation scaled by arm_swing_scale about its cycle mean; sway_scale scales
     the pelvis's whole rotation deviation (roll, yaw and pitch) and its sway (X), bob_scale its bob (Z), all about
     their mean. A scaled periodic signal stays periodic, so the loop stays closed.
  3. Posture: world turns about each bone head, top-down, each bone turned toward a direction (spine toward +Y,
     clavicle toward -Z then +Y, upper arm toward the midline, forearm toward +Y). Children follow.
  4. Pelvis lowered by the drop that adds knee_bend_deg at the clip's mean knee angle.
  5. Legs: two-bone IK (retarget_mannequin_to_human.two_bone_ik, which _leg_ik also uses, with the goal given) to the SOURCE ankle path, widened by
     stance_width_m, its forward swing about the pelvis scaled by stride_scale (gaits only), and lowered with the
     pelvis while the foot is off the ground (SWING_LIFT_M). The foot keeps the source foot's world orientation,
     turned toe-out by foot_splay_deg. Contacts keep the vanilla timing and height.
  6. Idles: head yaw, breathing pitch and pelvis weight shift as sine curves whose period is the clip loop.
  7. The whole pose turned 180 deg about world Z (the Kit's root rule, ROOT_YAW_MODE "pose"), keyed, exported by
     retarget_mannequin_to_human.export_clip and re-read by check_export_against_engine.

Speed matching: the engine plays a gait clip at agent speed / BipMovIkUsage.LoopDisplacement. The feet sweep
stride_scale times the vanilla distance, so the clip needs LoopDisplacement = vanilla x stride_scale
(gen_troll_anim_clips.ps1 -TravelScale). Cadence then rises by 1 / stride_scale at the same speed.

Limp (optional "limp" block in the spec, gaits only; a spec without any of the new blocks and keys runs none of their
code, and restyles as before except that neck and head pitch about the body's side axis, which differs by a fraction of
a degree from the old direction-based axis where the vanilla neck leans sideways). Symmetric settings
treat both legs and both halves of the cycle alike; the limp block makes the gait lopsided, a heavy slightly injured
brawler. Every key is listed in LIMP_DEFAULTS (meaning and default beside it). Two groups:
  Static posture (no contact data needed). One side, lead_shoulder_side, drives the shoulder posture: that shoulder is
  the LEADING shoulder (lead_shoulder_yaw_deg turns the upper trunk about the vertical, 40 per cent on spine1 and 60 on
  spine2, so it comes forward; neck and head counter-turn so the face keeps its direction) and also the LOW one
  (low_shoulder_deg: extra clavicle drop; low_shoulder_lean_deg: spine2 bent toward that side; low_arm_forward_deg;
  low_arm_swing_scale; lead_shoulder_protract_deg: extra clavicle protraction). The clavicle cap applies per side to
  clavicle_drop_deg + low_shoulder_deg and to clavicle_protract_deg + lead_shoulder_protract_deg; the yaw is a turn,
  not flexion, so it is not in the 30 deg flex cap and has its own cap of 20 deg. knee_bow_deg turns each leg's IK
  pole outward about the hip-to-ankle axis. mirror_sides (a top-level bool) swaps strong_side and lead_shoulder_side,
  so one spec yields a mirrored variant.
  Asymmetric cycle ("stomp, drag"): strong_side, timing_split, strong_lift_scale, weak_lift_scale, strong_drop_m,
  weak_drop_m, strong_lean_deg, shoulder_roll_deg, weak_swing_out_m, weak_hip_hike_deg, weak_trail_m (the dragged
  foot's whole rebuilt y path sits this far behind where it would be, in stance and swing alike: a constant offset
  keeps the planted speed V, so nothing slides and the loop distance is unchanged; the leg IK clamps a trailing foot
  at its reach and the report's ik_max_unreached_m says by how much). These need to know where each
  foot is planted, so the source's contacts are found first (contact_indices: the validator's definition, the ankle
  within 1.5 cm of its lowest and moving back).
Uneven timing without foot slide. The engine moves the agent at a CONSTANT speed and plays the clip at speed /
LoopDisplacement, so a planted foot must move back relative to the pelvis at a constant rate V in every frame. Only
time-warping the source would make the planted foot slide. So the clip is built in three parts:
  1. A smooth, cyclic time map (Warp: a phase modulation, C1 so no bone's velocity jumps where the pace changes)
     keeps the STRONG foot's contact at its source phase and moves the
     weak foot's contact to strong + timing_split of the cycle. The output has the source's frame count and period,
     so the clip length and LoopDisplacement do not change. Each output frame samples the source at a fractional
     frame (warp_locals: slerp of every bone's local rotation, lerp of its translation, then world recomposed).
     Why the strong contact stays put: in diagonal movement the engine blends this clip with the vanilla side and
     back clips at one shared phase, so the fewer contacts move, the less the blend fights; the weak contact moves by
     (timing_split - source split) of the cycle and the strong one not at all.
  2. Each ankle's forward (y) path is REBUILT in the new time (ankle_y_track): during its warped contact interval it
     falls at exactly -V per frame (V = the source's mean planted backward speed x stride_scale), so the longer
     stance covers a longer step and the shorter stance a shorter one; during its swing it follows the source swing's
     y profile, normalised 0..1, between the stance's end and the next stance's start. One stride per loop is V x
     period, so the loop distance is stride_scale x vanilla and LoopDisplacement stays at -TravelScale stride_scale.
  3. Ankle x is the source plus stance width plus the weak foot's swing-out arc; ankle z is the source with the
     per-foot lift scaled during swing only (planted heel and toe heights stay vanilla, and a lowered foot never
     sinks its ankle or toe below the source's lowest point).
The report lists, per limp clip, the new contact intervals (frames and phases), timing_split, the planted speed, and
step_points: the vanilla clip's StepPoints (--vanilla-step-points) mapped through the warp (Warp.inverse), for
gen_troll_anim_clips.ps1 -RestyleReport (which also takes -TravelScale from the report's stride_scale).

Run override. A "run" block in the spec lays its keys over the top-level keys (and the limp keys) for clips named run_*
(spec_for_clip). The brief wants a forward-falling lope near 45 deg of torso. The trunk cap stays: 30 deg total flex
over spine, spine1 and spine2. The extra apparent lean comes from pelvis_tilt_deg, which is NOT capped (the whole
body pitches forward about the hips, so it does not offset the trunk against a combat clip). The caps are checked on
the merged run spec too. A run block replaces a dict value such as trunk_flex_deg whole.

Caps for combat blending (cap_violations): spine total 30, clavicle 10 per key (clavicle_drop_deg + low_shoulder_deg on
the low side), and trunk lateral bend: |low_shoulder_lean_deg| + |strong_lean_deg| at most 8 deg. shoulder_roll_deg is
a pulse that blends out within the stride and is not capped.

Self-check: every clip is also run with the identity spec; the report's identity_max_deg is how far that run lands
from the source (the IK and the yaw only). With --preview, both runs render on --body as a contact sheet:
rows vanilla side, restyle side, vanilla front, restyle front; columns at 0, 25, 50, 75 per cent of the clip.
"""
import argparse
import hashlib
import json
import math
import os
import statistics
import sys
import traceback

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import retarget_mannequin_to_human as rt  # noqa: E402

UP = Vector((0.0, 0.0, 1.0))
FWD = Vector((0.0, 1.0, 0.0))     # the engine human faces +Y (toes sit at +Y of the ankles at rest)
RIGHT = Vector((1.0, 0.0, 0.0))   # left bones sit at -X
TRUNK = ("spine", "spine1", "spine2")
NECK_HEAD = ("neck", "head")
SIDES = (("l", -1.0), ("r", 1.0))
YAW = rt.KIT_ROOT_YAW
CAPS = {"trunk_flex_total_deg": 30.0, "clavicle_deg": 10.0, "trunk_lateral_deg": 8.0, "lead_yaw_deg": 20.0}
LEAD_YAW_SPLIT = {"spine1": 0.4, "spine2": 0.6}      # of lead_shoulder_yaw_deg, over the upper trunk
LEAD_YAW_COUNTER = {"neck": 0.4, "head": 0.6}        # of the same yaw, turned back so the face keeps its direction
# A foot lifted this far (its lowest point, ankle or toe, above its floor) drops by the full pelvis drop.
# With the ankle on the vanilla path, the goblin run's swing knee folded up to 50 deg more than vanilla.
SWING_LIFT_M = 0.10
CONTACT_BAND_M = 0.015   # the validator's contact definition: ankle within 1.5 cm of its lowest and moving back
# The ankle moves back at the ground speed only on the plateau of a contact: the frames where the heel lands and the
# toe leaves are slower (the foot rolls). A limp rebuilds the ankle path at one constant speed over the plateau, the
# contact frames moving back at least this share of the median speed.
PLATEAU_FRACTION = 0.85
# A limp needs a closed loop (the last frame repeating the first). A source that misses closure by under this many
# degrees (the vanilla run_forward_1h_left_stance, _with_shield and _with_shield_left_stance miss by 1.1, 1.54, 5.21) is
# closed first: the gap is spread over its last CLOSURE_FRAMES frames. A bigger gap is refused.
LOOP_CLOSE_MAX_DEG = 6.0
CLOSURE_FRAMES = 6

# The limp block (see the module docstring). Sides are "l" or "r". "strong" is the leg that stomps; "weak" is the
# dragged one (always the other side). Angles in degrees, distances in metres.
LIMP_DEFAULTS = {
    "strong_side": "r",            # the stomping leg
    "timing_split": 0.5,           # share of the cycle from the strong contact to the weak contact (0.5 = even)
    "strong_lift_scale": 1.0,      # strong foot's swing height over the source's
    "weak_lift_scale": 1.0,        # weak foot's swing height (never below the source's lowest ground height)
    "strong_drop_m": 0.0,          # eased extra pelvis drop after the strong contact
    "weak_drop_m": 0.0,            # the same, smaller, after the weak contact
    "strong_lean_deg": 0.0,        # spine2 lateral lean toward the strong stance leg, peaking over its stance
    "shoulder_roll_deg": 0.0,      # clavicle protraction pulse at the strong contact (both shoulders)
    "weak_swing_out_m": 0.0,       # lateral sine arc of the weak foot over its swing, outward
    "weak_hip_hike_deg": 0.0,      # pelvis roll lifting the weak side hip over its swing
    "weak_trail_m": 0.0,           # the weak foot's whole ankle path shifted this far back of the pelvis (it trails)
    "lead_shoulder_side": None,    # the leading AND low shoulder; None = strong_side (one side drives both)
    "lead_shoulder_yaw_deg": 0.0,  # upper trunk yawed about the vertical so that shoulder comes forward
    "lead_shoulder_protract_deg": 0.0,  # extra clavicle protraction on that side (capped with clavicle_protract_deg)
    "low_shoulder_deg": 0.0,       # extra clavicle drop on that side (capped with clavicle_drop_deg)
    "low_shoulder_lean_deg": 0.0,  # static spine2 lateral bend toward the low side
    "low_arm_forward_deg": 0.0,    # that upper arm turned toward forward
    "low_arm_swing_scale": None,   # that arm's swing scale; None = arm_swing_scale (the other arm keeps it)
    "knee_bow_deg": 0.0,           # each leg's IK pole turned outward about the hip-to-ankle axis, a fixed angle
    # metric targets, solved per frame; the report's "achieved" block says what the clip reached
    "knee_bow_m": 0.0,             # knees track this far outside the hip-to-ankle line (front view), pole turn <= 70 deg
    "knee_max_deg": 180.0,         # the knee never straightens past this (180 = no limit): pelvis lowered, goal pulled in
    "weak_lift_max_m": 0.0,        # absolute cap on the weak ankle's height above its floor during swing (0 = off)
    "weak_toe_down_deg": 0.0,      # plantar flexion of the weak foot over its swing, blended out by contact
    "weak_hip_hike_m": 0.0,        # the weak-side hip joint rises this much in its swing: roll to 10 deg, then a pelvis raise
    "weight_over_foot": 0.0,       # pelvis shifted toward each stance foot by this share of the foot's offset from centre
    "foot_outer_edge_deg": 0.0,    # the foot lands heel and outer edge first, rolled flat by mid-stance
    "arm_forward_deg": 0.0,        # both upper arms turned toward forward (hang in front of the thighs)
    "min_track_half_m": 0.0,       # no ankle comes closer to the pelvis centreline than this, ever (0 = off)
}
# the keys that change the cycle and so need the source's contacts; the rest are static posture
LIMP_CYCLE_KEYS = ("timing_split", "strong_lift_scale", "weak_lift_scale", "strong_drop_m", "weak_drop_m",
                   "strong_lean_deg", "shoulder_roll_deg", "weak_swing_out_m", "weak_hip_hike_deg", "weak_trail_m",
                   "weak_lift_max_m", "weak_toe_down_deg", "weak_hip_hike_m", "weight_over_foot",
                   "foot_outer_edge_deg")
KNEE_BOW_MAX_DEG = 70.0
HIKE_ROLL_MAX_DEG = 10.0
LIMP_SIDE_KEYS = ("strong_side", "lead_shoulder_side")
TIMING_SPLIT_RANGE = (0.4, 0.7)

DEFAULTS = {
    "trunk_flex_deg": {}, "neck_head_flex_deg": {}, "pelvis_tilt_deg": 0.0,
    "clavicle_drop_deg": 0.0, "clavicle_protract_deg": 0.0, "upperarm_hang_deg": 0.0, "elbow_bend_deg": 0.0,
    "arm_swing_scale": 1.0, "knee_bend_deg": 0.0, "stance_width_m": 0.0, "foot_splay_deg": 0.0,
    "stride_scale": 1.0, "bob_scale": 1.0, "sway_scale": 1.0,
    "idle": {"head_yaw_deg": 0.0, "head_cycles": 1, "glance_harmonic": 0.0, "breath_pitch_deg": 0.0,
             "breath_cycles": 1, "weight_shift_m": 0.0, "weight_cycles": 1},
    "limp": dict(LIMP_DEFAULTS),
    "run": {},
    "mirror_sides": False,
    "hand_clearance_m": 0.0,         # per frame, each forearm and hand keeps this gap from the thighs, calves and torso (0 = off)
    "shield_arm_clearance_m": 0.0,   # the larger gap the shield (left) arm keeps in with_shield clips (0 = hand_clearance_m)
}
# a run block may override any top-level key except these, and any limp key
RUN_KEYS = (set(DEFAULTS) - {"idle", "limp", "run", "mirror_sides"}) | set(LIMP_DEFAULTS)
FLEX_TABLES = {"trunk_flex_deg": TRUNK, "neck_head_flex_deg": NECK_HEAD}   # the bones a flex table may name


def _is_number(v):
    """A real, finite number: not a bool (an int in Python), not null, not NaN or infinity."""
    return isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v)


def _check_idle_values(path, idle):
    """Every idle value must be a real, finite number (not a bool, null, string, NaN or infinity), and the *_cycles counts
    whole numbers of at least 1: a half cycle would leave the idle loop open at its seam."""
    for k, v in idle.items():
        if k.endswith("_cycles"):
            if isinstance(v, bool) or not isinstance(v, int) or v < 1:
                raise SystemExit("variant spec %s: idle %s must be a whole number of 1 or more, not %r" % (path, k, v))
        elif not _is_number(v):
            raise SystemExit("variant spec %s: idle %s must be a number, not %r" % (path, k, v))


def _check_top_level_values(path, where, spec):
    """Refuse a top-level value of the wrong kind or range, on the base spec and again on the spec merged with the run
    block (a run override reaches the restyle without any other check). bool is an int in Python, so it is refused
    wherever a number is expected."""
    for k, default in DEFAULTS.items():
        if k in ("idle", "limp", "run", "mirror_sides") or k not in spec:
            continue
        v = spec[k]
        if k in FLEX_TABLES:
            if not isinstance(v, dict):
                raise SystemExit("variant spec %s: %s %s must be a table of bone: degrees" % (path, where, k))
            for bone, deg in v.items():
                if bone not in FLEX_TABLES[k]:
                    raise SystemExit("variant spec %s: %s %s names unknown bone %r (known: %s)"
                                     % (path, where, k, bone, ", ".join(FLEX_TABLES[k])))
                if not _is_number(deg):
                    raise SystemExit("variant spec %s: %s %s.%s must be a number, not %r" % (path, where, k, bone, deg))
        elif not _is_number(v):
            raise SystemExit("variant spec %s: %s %s must be a number, not %r" % (path, where, k, v))
    for k in ("hand_clearance_m", "shield_arm_clearance_m"):
        if k in spec and spec[k] < 0.0:
            raise SystemExit("variant spec %s: %s %s must be a number of metres, 0 or more, not %r" % (path, where, k, spec[k]))
    if "stride_scale" in spec and spec["stride_scale"] <= 0.0:
        raise SystemExit("variant spec %s: %s stride_scale must be above 0, not %r" % (path, where, spec["stride_scale"]))


def _check_limp_values(path, where, limp):
    """Refuse a limp value that is the wrong kind or out of range (a typo'd side would silently pick the other leg)."""
    for k, v in limp.items():
        if k in LIMP_SIDE_KEYS and v not in ("l", "r") and not (k == "lead_shoulder_side" and v is None):
            raise SystemExit("variant spec %s: %s %s must be \"l\" or \"r\", not %r" % (path, where, k, v))
        if k not in LIMP_SIDE_KEYS and k != "low_arm_swing_scale" and not _is_number(v):
            raise SystemExit("variant spec %s: %s %s must be a number, not %r" % (path, where, k, v))
    lo, hi = TIMING_SPLIT_RANGE
    ts = limp.get("timing_split")
    if ts is not None and not lo <= ts <= hi:
        raise SystemExit("variant spec %s: %s timing_split %s outside %.2f to %.2f" % (path, where, ts, lo, hi))
    km = limp.get("knee_max_deg")
    if km is not None and not 60.0 <= km <= 180.0:
        raise SystemExit("variant spec %s: %s knee_max_deg %s outside 60 to 180" % (path, where, km))
    for k in ("knee_bow_m", "weak_lift_max_m", "weak_toe_down_deg", "weak_hip_hike_m", "weight_over_foot",
              "foot_outer_edge_deg", "min_track_half_m"):
        if limp.get(k) is not None and limp[k] < 0.0:
            raise SystemExit("variant spec %s: %s %s must not be negative" % (path, where, k))
    das = limp.get("low_arm_swing_scale")
    if das is not None and not _is_number(das):
        raise SystemExit("variant spec %s: %s low_arm_swing_scale must be a number or null, not %r" % (path, where, das))
    for k in ("strong_lift_scale", "weak_lift_scale", "low_arm_swing_scale"):
        if limp.get(k) is not None and limp[k] < 0.0:
            raise SystemExit("variant spec %s: %s %s must not be negative" % (path, where, k))


def canonical_sha(obj):
    """sha256 (hex) of the canonical JSON of obj: sorted keys, no whitespace, so key order never changes it."""
    return hashlib.sha256(json.dumps(obj, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")).hexdigest()


def code_shas():
    """sha256 of this tool's source and of retarget_mannequin_to_human.py (the rig, IK and exporter it runs through), by
    file name. A code change shows up here even when the spec file did not change."""
    out = {}
    for module_file in (__file__, rt.__file__):
        path = os.path.abspath(module_file)
        with open(path, "rb") as fh:
            out[os.path.basename(path)] = hashlib.sha256(fh.read()).hexdigest()
    return out


def provenance_sha(spec, code=None):
    """The fingerprint of a whole run: the loaded spec AND the sha256 of the tool's two source files (code_shas). It is the
    report's top-level spec_sha and every entry's loaded_spec_sha, so a restyle changed in code with the spec file
    unchanged cannot pass as the same provenance."""
    return canonical_sha({"spec": spec, "code": code if code is not None else code_shas()})


def clip_spec_sha(spec, clip, kind):
    """The fingerprint of what a clip was restyled with: the spec MERGED for that clip (run override and mirror applied, as
    the restyle uses it) together with the clip's kind (an idle takes only a slice of the limp). Two clips restyled with
    the same merged spec and kind share it."""
    return canonical_sha({"kind": kind, "spec": spec_for_clip(spec, clip)})


def merge_report_clips(old, new):
    """A partial run's report clips: the old entries kept as they are (each with its own spec_sha and loaded_spec_sha, so
    a consumer can see it describes an earlier spec) and the newly restyled ones replacing theirs."""
    merged = dict(old)
    merged.update(new)
    return merged


def stale_clips(report):
    """The clips whose entry does not describe this run: no loaded_spec_sha, or one that differs from the report's top-level
    spec_sha. The generator checks the same thing of every entry in the report, idles included, and refuses on any
    mismatch: each entry must carry a loaded_spec_sha equal to the top-level spec_sha (which covers the spec and the tool's
    source, provenance_sha)."""
    return sorted(c for c, e in report.get("clips", {}).items() if e.get("loaded_spec_sha") != report.get("spec_sha"))


def load_spec(path):
    """Read and check a variant spec file (see load_spec_dict)."""
    with open(path, encoding="utf-8-sig") as fh:
        return load_spec_dict(json.load(fh), path)


def load_spec_dict(raw_spec, path="<dict>"):
    """Variant spec merged over DEFAULTS. Keys that start with '_' are notes. A key DEFAULTS does not know (a typo
    such as stride_scal would silently run at the default) is refused, top level and inside idle, limp and run. A
    run block may hold any top-level key except idle, plus any limp key."""
    raw = {k: v for k, v in raw_spec.items() if not k.startswith("_")}
    for k in raw:
        if k != "clip_prefix" and k not in DEFAULTS:
            raise SystemExit("variant spec %s: unknown key %r" % (path, k))
    for ik in raw.get("idle", {}):
        if not ik.startswith("_") and ik not in DEFAULTS["idle"]:
            raise SystemExit("variant spec %s: unknown idle key %r" % (path, ik))
    for lk in raw.get("limp", {}):
        if not lk.startswith("_") and lk not in LIMP_DEFAULTS:
            raise SystemExit("variant spec %s: unknown limp key %r" % (path, lk))
    for rk in raw.get("run", {}):
        if not rk.startswith("_") and rk not in RUN_KEYS:
            raise SystemExit("variant spec %s: unknown run key %r" % (path, rk))
    spec = json.loads(json.dumps(DEFAULTS))
    for k, v in raw.items():
        if k in ("idle", "limp", "run"):
            spec[k].update({ik: iv for ik, iv in v.items() if not ik.startswith("_")})
        else:
            spec[k] = v
    if "stride_scale" in spec["run"] and spec["run"]["stride_scale"] != spec["stride_scale"]:
        # gen_troll_anim_clips.ps1 applies ONE -TravelScale to a whole variant, and LoopDisplacement is the stride:
        # a run clip with its own stride would slide its feet. (The alternative, per-clip scales read from the
        # report, was not built.)
        raise SystemExit("variant spec %s: a run block may not change stride_scale (%s against %s): the generator "
                         "applies one -TravelScale per variant" % (path, spec["run"]["stride_scale"], spec["stride_scale"]))
    _check_idle_values(path, spec["idle"])
    _check_top_level_values(path, "top level", spec)
    if spec["run"]:
        merged = dict(spec)
        merged.update({k: v for k, v in spec["run"].items() if k in DEFAULTS and k not in ("limp", "run", "idle")})
        _check_top_level_values(path, "run block (merged)", merged)
    if not isinstance(spec["mirror_sides"], bool):
        raise SystemExit("variant spec %s: mirror_sides must be true or false, not %r" % (path, spec["mirror_sides"]))
    _check_limp_values(path, "limp", spec["limp"])
    _check_limp_values(path, "run", {k: v for k, v in spec["run"].items() if k in LIMP_DEFAULTS})
    if not spec.get("clip_prefix"):
        raise SystemExit("variant spec %s has no clip_prefix" % path)
    return spec


def identity_spec(spec):
    out = json.loads(json.dumps(DEFAULTS))
    out["clip_prefix"] = spec["clip_prefix"]
    return out


def spec_for_clip(spec, clip):
    """The spec `clip` is restyled with: a run_* clip gets the spec's run block laid over the top-level keys (a limp
    key goes into the limp block), and mirror_sides swaps strong_side and lead_shoulder_side. Every other clip of a
    spec with neither is returned as the spec itself. The merged copy has an empty run block and mirror_sides off,
    so merging it again changes nothing."""
    run = clip.startswith("run_") and bool(spec.get("run"))
    if not run and not spec.get("mirror_sides"):
        return spec
    out = json.loads(json.dumps(spec))
    if run:
        for k, v in spec["run"].items():
            if k in LIMP_DEFAULTS:
                out["limp"][k] = v
            else:
                out[k] = v
        out["run"] = {}
    if out.get("mirror_sides"):
        swap = {"l": "r", "r": "l"}
        for k in LIMP_SIDE_KEYS:
            if out["limp"][k] is not None:
                out["limp"][k] = swap[out["limp"][k]]
        out["mirror_sides"] = False
    return out


def limp_cycle_active(limp):
    """True when the limp changes the cycle (any LIMP_CYCLE_KEYS value off neutral): the restyle then needs contacts."""
    return any(abs(limp[k] - LIMP_DEFAULTS[k]) > 1e-9 for k in LIMP_CYCLE_KEYS)


# the limp keys that need no contacts and have a neutral of 0: whatever is not a cycle key, a side, or one of the two
# keys with another neutral (low_arm_swing_scale None, knee_max_deg 180)
LIMP_STATIC_KEYS = tuple(k for k in LIMP_DEFAULTS
                         if k not in LIMP_CYCLE_KEYS and k not in LIMP_SIDE_KEYS and k not in ("low_arm_swing_scale", "knee_max_deg"))


def limp_static_active(limp):
    """True when a static limp posture key is off neutral."""
    if any(abs(limp[k]) > 1e-9 for k in LIMP_STATIC_KEYS):
        return True
    return limp["low_arm_swing_scale"] is not None or limp["knee_max_deg"] < LIMP_DEFAULTS["knee_max_deg"]


# the limp targets the report's achieved block and the validator's metrics check are about (0 or 180 = no target)
METRIC_KEYS = ("knee_bow_m", "weak_lift_max_m", "weak_toe_down_deg", "weak_hip_hike_m", "weight_over_foot",
               "foot_outer_edge_deg", "min_track_half_m")


def metric_targets_active(limp):
    """True when the (merged) limp sets any metric target; the validator then expects the report's achieved block."""
    return any((limp.get(k) or 0.0) > 0.0 for k in METRIC_KEYS) or (limp.get("knee_max_deg") or 180.0) < 180.0


def gait_scales(spec):
    """({bone: scale}, largest) of the deviation scales a gait spec applies: each arm's own swing scale (the lead arm's
    low_arm_swing_scale when set), the pelvis's sway scale, 1 for every other bone; `largest` also counts bob_scale. A
    scale under 1 counts as 1 (a smaller motion needs no extra allowance)."""
    limp = dict(LIMP_DEFAULTS, **(spec.get("limp") or {}))
    lead = limp_sides(limp)[1]
    per = {"pelvis": max(1.0, abs(spec.get("sway_scale", 1.0)))}
    for side, _ in SIDES:
        scale = spec.get("arm_swing_scale", 1.0)
        if side == lead and limp["low_arm_swing_scale"] is not None:
            scale = limp["low_arm_swing_scale"]
        per[side + "_upperarm_twist"] = max(1.0, abs(scale))
    return per, max([1.0, abs(spec.get("bob_scale", 1.0))] + list(per.values()))


def limp_for_kind(limp, kind):
    """The limp a clip of `kind` is restyled with. A gait gets all of it. An idle gets ONLY arm_forward_deg: the other
    keys are about the stride and the stomp, and a mirrored low shoulder (a spec that swaps the lead side) would flip
    its shoulder at every halt, because the variants inherit the main orc's idles."""
    if kind == "gait":
        return dict(limp)
    out = dict(LIMP_DEFAULTS)
    out["arm_forward_deg"] = limp["arm_forward_deg"]
    return out


def limp_sides(limp):
    """(strong side, lead shoulder side): the lead shoulder follows the strong side unless the spec names its own."""
    return limp["strong_side"], limp.get("lead_shoulder_side") or limp["strong_side"]


def cap_violations(spec):
    out = []
    trunk = sum(abs(v) for v in spec["trunk_flex_deg"].values())
    if trunk > CAPS["trunk_flex_total_deg"]:
        out.append("trunk flex %.1f deg over the %.0f cap" % (trunk, CAPS["trunk_flex_total_deg"]))
    for k in ("clavicle_drop_deg", "clavicle_protract_deg"):
        if abs(spec[k]) > CAPS["clavicle_deg"]:
            out.append("%s %.1f over the %.0f cap" % (k, spec[k], CAPS["clavicle_deg"]))
    limp = spec.get("limp") or LIMP_DEFAULTS
    if abs(limp["low_shoulder_deg"]) > 1e-9:
        low = abs(spec["clavicle_drop_deg"] + limp["low_shoulder_deg"])
        if low > CAPS["clavicle_deg"]:
            out.append("clavicle_drop_deg + low_shoulder_deg %.1f over the %.0f cap" % (low, CAPS["clavicle_deg"]))
    if abs(limp["lead_shoulder_protract_deg"]) > 1e-9:
        pro = abs(spec["clavicle_protract_deg"] + limp["lead_shoulder_protract_deg"])
        if pro > CAPS["clavicle_deg"]:
            out.append("clavicle_protract_deg + lead_shoulder_protract_deg %.1f over the %.0f cap"
                       % (pro, CAPS["clavicle_deg"]))
    if abs(limp["lead_shoulder_yaw_deg"]) > CAPS["lead_yaw_deg"]:
        out.append("lead_shoulder_yaw_deg %.1f over the %.0f cap" % (limp["lead_shoulder_yaw_deg"], CAPS["lead_yaw_deg"]))
    lateral = abs(limp["low_shoulder_lean_deg"]) + abs(limp["strong_lean_deg"])
    if lateral > CAPS["trunk_lateral_deg"]:
        out.append("trunk lateral bend (low_shoulder_lean_deg + strong_lean_deg) %.1f deg over the %.0f cap"
                   % (lateral, CAPS["trunk_lateral_deg"]))
    return out


def all_cap_violations(spec):
    """cap_violations of the spec, and of the spec merged with its run block (prefixed "run: ")."""
    out = cap_violations(spec)
    if spec.get("run"):
        out += ["run: " + v for v in cap_violations(spec_for_clip(spec, "run_x"))]
    return out


# ------------------------------------------------------------------ math helpers
def _toward(d, v, deg):
    """Rotation that turns direction d toward v by deg (a negative deg turns it away)."""
    if abs(deg) < 1e-9:
        return Quaternion()
    ax = d.cross(v)
    if ax.length < 1e-6:
        return Quaternion()
    return Quaternion(ax.normalized(), math.radians(deg))


def _turn(Mw, t, q):
    """Turn bone t by world rotation q about its own head."""
    M = (q.to_matrix() @ rt._rot(Mw[t])).to_4x4()
    M.translation = Mw[t].translation.copy()
    Mw[t] = M


def _yaw_subtree(Mw, bones, root, deg):
    """Turn `bones` (root and everything below it) rigidly about the vertical through root's head."""
    p = Mw[root].translation.copy()
    turn = Matrix.Translation(p) @ Quaternion(UP, math.radians(deg)).to_matrix().to_4x4() @ Matrix.Translation(-p)
    for t in bones:
        Mw[t] = turn @ Mw[t]


def _qmean(qs):
    ref = qs[0]
    acc = [0.0, 0.0, 0.0, 0.0]
    for q in qs:
        sgn = -1.0 if q.dot(ref) < 0.0 else 1.0
        for i, c in enumerate((q.w, q.x, q.y, q.z)):
            acc[i] += sgn * c
    m = Quaternion(acc)
    m.normalize()
    return m


def _qpow(q, s):
    q = q.copy()
    if q.w < 0.0:
        q.negate()
    ax, ang = q.to_axis_angle()
    return Quaternion(ax, ang * s)


def _scale_rot_dev(mats, s):
    """Scale each local rotation's deviation from the mean rotation by s; translations unchanged."""
    if abs(s - 1.0) < 1e-9:
        return mats
    qs = [m.to_quaternion() for m in mats]
    qm = _qmean(qs)
    out = []
    for m, q in zip(mats, qs):
        q2 = qm @ _qpow(qm.inverted() @ q, s)
        M = q2.to_matrix().to_4x4()
        M.translation = m.translation.copy()
        out.append(M)
    return out


def _qangle_deg(a, b):
    """Angle between two rotations in degrees; q and -q are the same rotation, so fold past 180."""
    ang = a.rotation_difference(b).angle
    return math.degrees(min(ang, 2.0 * math.pi - ang))


def _knee_angle(H, K, A):
    u, v = (H - K), (A - K)
    if u.length < 1e-6 or v.length < 1e-6:
        return math.pi
    return u.angle(v)


# ------------------------------------------------------------------ gait asymmetry (pure: no mathutils)
def smoothstep(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3.0 - 2.0 * x)


def eased_pulse(t, peak=0.25):
    """A pulse over a window t in (0, 1): 0 at both ends, 1 at t = peak, a fast rise and a slow fall. None (outside
    the window) reads 0."""
    if t is None or t <= 0.0 or t >= 1.0:
        return 0.0
    if t < peak:
        return smoothstep(t / peak)
    return 1.0 - smoothstep((t - peak) / (1.0 - peak))


def sine_bump(t):
    """sin(pi t) over a window; 0 outside it."""
    return 0.0 if t is None else math.sin(math.pi * min(1.0, max(0.0, t)))


def sine_bump_sq(t):
    return sine_bump(t) ** 2


def swing_ramp(t, edge=0.2):
    """1 through the middle of a swing, easing to 0 over its first and last `edge` share: a lift scale applied with
    this weight leaves the planted heel and toe heights exactly as they were."""
    if t is None:
        return 0.0
    return smoothstep(t / edge) * smoothstep((1.0 - t) / edge)


def phase_in_window(k, start, length, period):
    """How far (0..1) frame k is through the cyclic window [start, start + length) of a `period`-frame cycle, or None
    when k is outside it."""
    if length <= 0.0:
        return None
    rel = (k - start) % period
    return rel / length if rel < length else None


def contact_indices(zs, ys, closed, band=CONTACT_BAND_M):
    """Frame indices k whose ankle is within `band` of its lowest and moving back (y falls from k to k + 1): the
    validator's contact definition (validate_locomotion_clips.contact_frames). zs and ys hold one value per frame;
    a closed loop's last frame repeats its first, so the move out of frame n - 1 wraps to frame 0."""
    n = len(zs) - (1 if closed else 0)
    zmin = min(zs)
    out = []
    for k in range(n if closed else n - 1):
        vy = ys[(k + 1) % n if closed else k + 1] - ys[k]
        if zs[k] <= zmin + band and vy < 0.0:
            out.append(k)
    return out


def cyclic_runs(idx, period):
    """Maximal runs of consecutive frame indices in a cycle of `period` frames, as [(start, length)]; a run that
    passes the end of the cycle is one run."""
    have = set(idx)
    if not have:
        return []
    if len(have) >= period:
        return [(0, period)]
    runs = []
    for st in sorted(have):
        if (st - 1) % period in have:
            continue
        n = 0
        while (st + n) % period in have:
            n += 1
        runs.append((st, n))
    return runs


def longest_stance(idx, period, min_frames=3):
    """(start, length, runs) of the longest contact run: the stance occupies the frame POSITIONS start .. start +
    length (a contact index k moves the ankle from frame k to k + 1). Refuses a foot with no run of min_frames."""
    runs = cyclic_runs(idx, period)
    if not runs or max(n for _, n in runs) < min_frames:
        raise ValueError("no contact run of %d frames (runs: %s)" % (min_frames, runs))
    st, n = max(runs, key=lambda r: r[1])
    return st, n, len(runs)


def loop_closure_plan(delta_deg):
    """False when a gait source's seam delta already counts as closed (under 1 deg), True when the tool must close it
    first (1 deg up to LOOP_CLOSE_MAX_DEG); raises ValueError past that."""
    if delta_deg < 1.0:
        return False
    if delta_deg >= LOOP_CLOSE_MAX_DEG:
        raise ValueError("the source's last frame misses its first by %.2f deg, past the %.0f deg the tool will close"
                         % (delta_deg, LOOP_CLOSE_MAX_DEG))
    return True


def closure_weights(frames):
    """Weights of the closing correction over the last `frames` frames: rising smoothly to exactly 1 on the last."""
    return [smoothstep((j + 1) / frames) for j in range(frames)]


def plateau_window(ys, start, length, period, fraction=PLATEAU_FRACTION):
    """(start, length, speed) of the plateau inside the contact indices start .. start + length - 1: the ends trimmed
    while the ankle moves back slower than `fraction` of the contact's median speed, which is returned (m per frame,
    positive). The median, not the mean: the slow landing and take-off frames pull a mean down (vanilla walk: 3.2 to
    3.9 cm per frame against a 4.9 plateau), and the plateau rate is the one the clip's LoopDisplacement matches. A
    plateau under 3 frames falls back to the whole contact."""
    speeds = [ys[(start + i) % period] - ys[(start + i + 1) % period] for i in range(length)]
    med = statistics.median(speeds)
    lo, hi = 0, length
    while lo < hi and speeds[lo] < fraction * med:
        lo += 1
    while hi > lo and speeds[hi - 1] < fraction * med:
        hi -= 1
    if hi - lo < 3:
        lo, hi = 0, length
    return start + lo, hi - lo, statistics.median(speeds[lo:hi])


def interp_cyclic(vals, x, period):
    """vals holds period + 1 samples of a closed loop (the last repeats the first); x is any real frame position."""
    x = x % period
    i = int(x)
    if i >= period:
        i, x = period - 1, float(period)
    f = x - i
    return vals[i] * (1.0 - f) + vals[i + 1] * f


class Warp:
    """The cyclic, smooth time map from output phase u to source phase s: s(u) = u + g(u - S), with
    g(v) = R (sin(2 pi v - pi T) + sin(pi T)), S the strong contact's phase and T the timing split. g(0) = 0, so the
    strong contact stays at its source phase; g(T) = source_split - T, so the weak contact, which the source has
    source_split of the cycle after the strong one, lands at T. R is the smallest amplitude that does it. The map is
    smooth everywhere (its derivative is 1 + 2 pi R cos(2 pi v - pi T), continuous across every contact and the loop
    seam), so no bone's velocity jumps where the clip's pace changes, and it is strictly monotone while 2 pi |R| < 1,
    which `Warp` checks. s(u + 1) = s(u) + 1."""

    MAX_SLOPE_SWING = 0.95   # 2 pi |R| must stay under this so the derivative never reaches 0

    def __init__(self, strong_phase, source_split, timing_split):
        if not 0.0 < source_split < 1.0 or not 0.0 < timing_split < 1.0:
            raise ValueError("splits must lie inside (0, 1): source %s, timing %s" % (source_split, timing_split))
        self.strong_phase, self.source_split, self.timing_split = strong_phase, source_split, timing_split
        self.amplitude = (source_split - timing_split) / (2.0 * math.sin(math.pi * timing_split))
        if 2.0 * math.pi * abs(self.amplitude) >= self.MAX_SLOPE_SWING:
            raise ValueError("timing_split %.3f against a source split of %.3f needs a time map that is not monotone"
                             % (timing_split, source_split))

    def forward(self, u):
        """Source phase played at output phase u."""
        v = u - self.strong_phase
        return u + self.amplitude * (math.sin(2.0 * math.pi * v - math.pi * self.timing_split)
                                     + math.sin(math.pi * self.timing_split))

    def derivative(self, u):
        return 1.0 + 2.0 * math.pi * self.amplitude * math.cos(
            2.0 * math.pi * (u - self.strong_phase) - math.pi * self.timing_split)

    def inverse(self, s):
        """Output phase at which source phase s is played (bisection: the map is strictly increasing, and u is within
        2 |R| of s)."""
        lo, hi = s - 2.0 * abs(self.amplitude) - 1e-9, s + 2.0 * abs(self.amplitude) + 1e-9
        for _ in range(60):
            mid = 0.5 * (lo + hi)
            if self.forward(mid) < s:
                lo = mid
            else:
                hi = mid
        return 0.5 * (lo + hi)

    def report(self):
        return {"kind": "phase_modulation", "strong_phase": round(self.strong_phase, 6),
                "timing_split": self.timing_split, "source_split": round(self.source_split, 6),
                "amplitude": round(self.amplitude, 6)}

    @classmethod
    def from_report(cls, row):
        return cls(row["strong_phase"], row["source_split"], row["timing_split"])


def output_positions(period, warp):
    """Fractional source frame in [0, period) that each of the `period` output frames samples."""
    out = []
    for k in range(period):
        x = (warp.forward(k / period) % 1.0) * period
        out.append(x if x < period else x - period)
    return out


def map_step_points_checked(points, warp):
    """(mapped, wrapped): each vanilla step point (clip progress 0..1; a negative value is an unused point and is skipped)
    mapped IN PLACE to the phase the warped clip plays it, wrapped into [0, 1). The list keeps the vanilla order, so the
    first entry stays the image of vanilla X and the second of vanilla Y; a wrapped point is never swapped into another
    slot. `wrapped` says a point was played past the end of the loop and came back at the start."""
    mapped, wrapped = [], False
    for p in points:
        if p is None or p < 0.0:
            continue
        u = warp.inverse(p)
        wrapped = wrapped or not 0.0 <= u < 1.0
        mapped.append(u % 1.0)
    return mapped, wrapped


def map_step_points(points, warp):
    return map_step_points_checked(points, warp)[0]


def plan_contacts(src_y, src_z, period, strong_foot, weak_foot, timing_split):
    """Where each foot is planted, in the source and after the time map. src_y and src_z hold, per foot, period + 1
    ankle samples of a closed loop. A foot has two spans of frame positions a .. b (b > a, may pass the period): its
    CONTACT (the validator's definition, which the timing is judged on) and the PLATEAU inside it (the frames the
    ankle moves back at the ground speed, where the rebuilt path is linear). Returns {"contact_src", "contact_out",
    "stance_src", "stance_out" (the plateau), "warp", "source_split", "planted_speed" (the two feet's plateau median
    speeds averaged, m per frame), "runs": {foot: contact runs found}}. The strong contact keeps its source start."""
    contact_src, stance_src, runs, speeds = {}, {}, {}, []
    for foot in (strong_foot, weak_foot):
        idx = contact_indices(src_z[foot], src_y[foot], True)
        st, n, nruns = longest_stance(idx, period)
        contact_src[foot] = (float(st), float(st + n))
        ps, pn, speed = plateau_window(src_y[foot], st, n, period)
        stance_src[foot] = (float(ps), float(ps + pn))
        runs[foot] = nruns
        speeds.append(speed)
    source_split = ((contact_src[weak_foot][0] - contact_src[strong_foot][0]) % period) / period
    warp = Warp(contact_src[strong_foot][0] / period, source_split, timing_split)

    def mapped(span_by_foot):
        out = {}
        for foot, (a, b) in span_by_foot.items():
            ua = warp.inverse(a / period) * period
            ub = warp.inverse(b / period) * period
            shift = math.floor(ua / period) * period
            out[foot] = (ua - shift, ub - shift)
        return out

    return {"contact_src": contact_src, "contact_out": mapped(contact_src), "stance_src": stance_src,
            "stance_out": mapped(stance_src), "warp": warp, "source_split": source_split,
            "planted_speed": sum(speeds) / len(speeds), "runs": runs}


def ankle_y_track(src_y, stance_src, stance_out, speed, y_mid, period, warp=None):
    """One foot's ankle forward (y) position for each of the `period` output frames. Inside the output stance it falls
    by exactly `speed` per frame, centred on y_mid at the stance's middle: a longer stance covers a longer step.
    During the swing it follows the source swing's y profile, normalised 0..1 between the source stance's end and the
    next stance's start, from where this stance ended to where the next one starts. The profile is read at the source
    position the time map plays at each output frame (`warp`; None reads it linearly between the stance ends), so the
    foot stays in step with the body, which is warped by the same map: a mismatch of half a frame is 5 cm in a run.
    src_y: the source ankle y per frame (period + 1 values). The closed loop returns to where it began."""
    a_src, b_src = stance_src
    a_out, b_out = stance_out
    length = b_out - a_out
    y_a = y_mid + speed * length / 2.0
    y_b = y_a - speed * length
    ys_b = interp_cyclic(src_y, b_src, period)
    ys_a = interp_cyclic(src_y, a_src, period)
    span = ys_a - ys_b
    swing = period - length
    played_a = warp.forward(a_out / period) * period if warp is not None else 0.0
    out = []
    for k in range(period):
        rel = (k - a_out) % period
        if rel <= length + 1e-9:
            out.append(y_a - speed * rel)
            continue
        t = (rel - length) / swing
        if warp is not None:
            sigma = a_src + (warp.forward((a_out + rel) / period) * period - played_a)
        else:
            sigma = b_src + t * (a_src + period - b_src)
        prof = (interp_cyclic(src_y, sigma, period) - ys_b) / span if abs(span) > 1e-4 else t
        out.append(y_b + prof * (y_a - y_b))
    return out


def stance_frames(stance_out, period):
    """Output frames k whose move to k + 1 lies wholly inside the stance (the frames a planted-foot check may use)."""
    a, b = stance_out
    return [k for k in range(period) if (k - a) % period + 1.0 <= (b - a) + 1e-9]


def clamp_track_half(gx, centre, side, min_half, soft=0.004):
    """An ankle goal x kept at least min_half from `centre` on its own side (side -1 left, +1 right), by a smooth
    maximum (at most soft/2 above the limit). The reference is the clip's MEAN pelvis line, one fixed number for the whole
    clip: measured from the moving pelvis, the planted ankles followed the sway and weight shift sideways."""
    rel = side * (gx - centre)
    return centre + side * 0.5 * (rel + min_half + math.sqrt((rel - min_half) ** 2 + soft ** 2))


def foot_goal_x(ankle_x, side, stance_width_m, min_half_m, centre_x):
    """The x of a foot's IK goal: the (warped source) ankle widened by the stance, and, when min_half_m is set, kept that far
    from `centre_x` on its own side. The caller passes the clip's mean pelvis line as centre_x. The frame's own pelvis is
    deliberately not an input: sway and weight shift move it, and the planted ankles must not follow."""
    gx = ankle_x + side * stance_width_m
    if min_half_m:
        gx = clamp_track_half(gx, centre_x, side, min_half_m)
    return gx


def knee_bow_offset(hip, knee, ankle, side):
    """How far the knee sits OUTSIDE the hip-to-ankle line in the front view (m, positive outward for `side`), measured
    at the knee's height; None when the ankle is not below the hip."""
    if ankle[2] - hip[2] >= -1e-6:
        return None
    t = (knee[2] - hip[2]) / (ankle[2] - hip[2])
    return side * (knee[0] - (hip[0] + t * (ankle[0] - hip[0])))


# ------------------------------------------------------------------ hand clearance (pure: no mathutils)
# Capsule radii (m) that approximate the male body of the preview FBX (human_skeleton_with_male_body.fbx). They are
# round numbers, not measured from the mesh: weighting the body's vertices to each bone needs the body import, which
# only a --preview run does, and the check must work without it. The forearm radius is the arm's own.
CAPSULE_R = {"thigh": 0.085, "calf": 0.06, "torso": 0.14, "hand": 0.05, "forearm": 0.04}
HAND_GRIP_M = 0.10        # the grip: the hand segment runs this far past the wrist along the hand
ABDUCT_MAX_DEG = 45.0     # the most an upper arm is swung outward for clearance
BACK_MAX_DEG = 20.0       # and the most it is then also swung back
CLEARANCE_STEP_DEG = 1.0
CLEARANCE_SMOOTH_RADIUS = 3   # frames: the running max, then three [1 2 1] passes of the same reach
SHIELD_ARM = "l"          # the left forearm carries the shield in every with_shield clip, left stance too (measured:
                          # in all four vanilla shield clips the left hand is raised 0.2 to 0.45 m against its plain clip)


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def segment_distance(p1, q1, p2, q2):
    """Shortest distance between the segments p1 q1 and p2 q2 (3-tuples), closest points clamped to both."""
    d1, d2, r = _sub(q1, p1), _sub(q2, p2), _sub(p1, p2)
    a, e, f = _dot(d1, d1), _dot(d2, d2), _dot(d2, r)
    eps = 1e-12
    if a <= eps and e <= eps:
        s = t = 0.0
    elif a <= eps:
        s, t = 0.0, min(1.0, max(0.0, f / e))
    else:
        c = _dot(d1, r)
        if e <= eps:
            t, s = 0.0, min(1.0, max(0.0, -c / a))
        else:
            b = _dot(d1, d2)
            denom = a * e - b * b
            s = min(1.0, max(0.0, (b * f - c * e) / denom)) if denom > eps else 0.0
            t = (b * s + f) / e
            if t < 0.0:
                t, s = 0.0, min(1.0, max(0.0, -c / a))
            elif t > 1.0:
                t, s = 1.0, min(1.0, max(0.0, (b - c) / a))
    c1 = (p1[0] + d1[0] * s, p1[1] + d1[1] * s, p1[2] + d1[2] * s)
    c2 = (p2[0] + d2[0] * t, p2[1] + d2[1] * t, p2[2] + d2[2] * t)
    return math.sqrt(_dot(_sub(c1, c2), _sub(c1, c2)))


def body_capsules(pos):
    """The body's capsules (a, b, radius) from bone head positions {bone: (x, y, z)}: both thighs (hip to knee), both
    calves (knee to ankle) and the torso (pelvis to spine2)."""
    caps = [(pos["pelvis"], pos["spine2"], CAPSULE_R["torso"])]
    for side in ("l", "r"):
        caps.append((pos[side + "_thigh"], pos[side + "_calf"], CAPSULE_R["thigh"]))
        caps.append((pos[side + "_calf"], pos[side + "_foot"], CAPSULE_R["calf"]))
    return caps


def arm_points(pos, side):
    """(elbow, wrist, grip tip) of one arm from {bone: (x, y, z)}: the grip runs HAND_GRIP_M past the wrist along the
    hand (wrist to the finger bone), or along the forearm when the two coincide."""
    elbow, wrist, finger = pos[side + "_foretwist"], pos[side + "_hand"], pos[side + "_finger0"]
    d = _sub(finger, wrist)
    n = math.sqrt(_dot(d, d))
    if n < 1e-6:
        d = _sub(wrist, elbow)
        n = math.sqrt(_dot(d, d)) or 1.0
    return elbow, wrist, (wrist[0] + d[0] / n * HAND_GRIP_M, wrist[1] + d[1] / n * HAND_GRIP_M,
                          wrist[2] + d[2] / n * HAND_GRIP_M)


def arm_gap(points, caps):
    """The smallest gap (distance minus both radii, m) between the forearm (elbow to wrist) and the hand (wrist to grip
    tip) and the body capsules. Negative means they overlap."""
    elbow, wrist, tip = points
    best = 9.0
    for a, b, radius in caps:
        best = min(best, segment_distance(elbow, wrist, a, b) - CAPSULE_R["forearm"] - radius,
                   segment_distance(wrist, tip, a, b) - CAPSULE_R["hand"] - radius)
    return best


def swing_arm_points(points, shoulder, side, abduct_deg, back_deg):
    """The arm's points swung about the shoulder: outward (away from the midline) by abduct_deg about world forward,
    then back by back_deg about world right. Outward is -X for the left arm and +X for the right."""
    th = math.radians(-side * abduct_deg)   # a hanging right arm (0, 0, -1) goes to +X for a negative turn about +Y
    ph = math.radians(-back_deg)            # and a hanging arm goes toward -Y (back) for a negative turn about +X
    cth, sth, cph, sph = math.cos(th), math.sin(th), math.cos(ph), math.sin(ph)
    out = []
    for p in points:
        x, y, z = p[0] - shoulder[0], p[1] - shoulder[1], p[2] - shoulder[2]
        x, z = x * cth + z * sth, -x * sth + z * cth          # about +Y
        y, z = y * cph - z * sph, y * sph + z * cph           # about +X
        out.append((shoulder[0] + x, shoulder[1] + y, shoulder[2] + z))
    return tuple(out)


def clearance_swing(points, shoulder, side, caps, clearance):
    """(abduct_deg, back_deg, gap_m) of the smallest outward swing that gives the arm `clearance`, then, if the whole
    ABDUCT_MAX_DEG is not enough, the smallest swing back on top of it (gap_m is what was reached either way)."""
    gap = arm_gap(points, caps)
    if gap >= clearance:
        return 0.0, 0.0, gap
    for i in range(1, int(ABDUCT_MAX_DEG / CLEARANCE_STEP_DEG) + 1):
        a = i * CLEARANCE_STEP_DEG
        gap = arm_gap(swing_arm_points(points, shoulder, side, a, 0.0), caps)
        if gap >= clearance:
            return a, 0.0, gap
    for i in range(1, int(BACK_MAX_DEG / CLEARANCE_STEP_DEG) + 1):
        b = i * CLEARANCE_STEP_DEG
        gap = arm_gap(swing_arm_points(points, shoulder, side, ABDUCT_MAX_DEG, b), caps)
        if gap >= clearance:
            return ABDUCT_MAX_DEG, b, gap
    return ABDUCT_MAX_DEG, BACK_MAX_DEG, gap


def hand_clearance_active(spec):
    """True when the spec asks for hand clearance. It does not depend on the clip's kind: walks, runs and stand_* idles
    all keep their hands off the thighs, calves and torso (the identity spec has it off)."""
    return (spec.get("hand_clearance_m") or 0.0) > 0.0


def clearance_for(clip, side, hand_clearance_m, shield_arm_clearance_m):
    """The clearance one arm of `clip` must keep: hand_clearance_m, and for the shield arm of a with_shield clip the larger
    of that and shield_arm_clearance_m."""
    if "with_shield" in clip and side == SHIELD_ARM:
        return max(hand_clearance_m, shield_arm_clearance_m)
    return hand_clearance_m


def dilate_cyclic(vals, radius, cyclic):
    """Running maximum over +-radius frames (wrapping when cyclic, clamped at the ends otherwise)."""
    n = len(vals)
    out = []
    for k in range(n):
        window = [vals[(k + j) % n] if cyclic else vals[min(n - 1, max(0, k + j))] for j in range(-radius, radius + 1)]
        out.append(max(window))
    return out


def smooth_correction(vals, cyclic, radius=CLEARANCE_SMOOTH_RADIUS):
    """A correction curve that never pops: the running max over +-radius frames, then `radius` passes of [1 2 1] / 4.
    The passes reach no further than the max did, so the result is at least the input everywhere; when cyclic it wraps,
    so the loop seam is as smooth as any other frame."""
    n = len(vals)
    out = dilate_cyclic(list(vals), radius, cyclic)
    for _ in range(radius):
        if cyclic:
            out = [(out[k - 1] + 2.0 * out[k] + out[(k + 1) % n]) / 4.0 for k in range(n)]
        else:
            out = [(out[max(0, k - 1)] + 2.0 * out[k] + out[min(n - 1, k + 1)]) / 4.0 for k in range(n)]
    return out


# ------------------------------------------------------------------ source sampling
class Source:
    """A vanilla master evaluated straight from its keyframe JSON: no rig, no action, no frame_set. world(t) is
    {bone: armature-space 4x4} at JSON frame t, a fresh dict each call."""

    def __init__(self, json_path):
        with open(json_path, encoding="utf-8-sig") as fh:
            _, self._at, _ = rt.json_pose_sampler(json.load(fh))

    def world(self, t):
        return self._at(int(t))

    def frames(self, s1, s2):
        """The world poses of JSON frames s1..s2, in order."""
        return [self.world(t) for t in range(s1, s2 + 1)]


def _below(parent, bone, root):
    """True when `root` is an ancestor of `bone`."""
    b = parent[bone]
    while b:
        if b == root:
            return True
        b = parent[b]
    return False


def _locals(world, order, parent):
    return {t: (world[parent[t]].inverted() @ world[t]) if parent[t] else world[t].copy() for t in order}


def world_from_locals(lc, order, parent):
    """Armature-space 4x4 of every bone from its local matrices (parents come first in `order`)."""
    w = {}
    for t in order:
        w[t] = (w[parent[t]] @ lc[t]) if parent[t] else lc[t].copy()
    return w


def warp_locals(locs, positions, order):
    """Local matrices of a closed loop sampled at fractional source frames: each bone's rotation by slerp and its
    translation by lerp between the two neighbouring source frames. `locs` holds period + 1 frames (the last repeats
    the first); `positions` the source frame in [0, period) for each of the period output frames. Returns period + 1
    frames, the last a copy of the first."""
    out = []
    for x in positions:
        i = int(x)
        f = x - i
        a, b = locs[i], locs[i + 1]
        lc = {}
        for t in order:
            qa = a[t].to_quaternion()
            q = qa.slerp(b[t].to_quaternion(), f) if f > 0.0 else qa
            M = q.to_matrix().to_4x4()
            M.translation = a[t].translation.lerp(b[t].translation, f) if f > 0.0 else a[t].translation.copy()
            lc[t] = M
        out.append(lc)
    out.append({t: m.copy() for t, m in out[0].items()})
    return out


def close_loop_locals(locs, order, frames=CLOSURE_FRAMES):
    """A copy of `locs` whose last frame equals its first: each bone's local rotation and translation gets a share of
    the gap (last to first) over the last `frames` frames, the share rising smoothly to the whole gap on the last frame
    (closure_weights). Earlier frames are untouched."""
    out = [{t: m.copy() for t, m in lc.items()} for lc in locs]
    n = len(out)
    weights = closure_weights(frames)
    first, last = locs[0], locs[-1]
    for t in order:
        q_last, q_first = last[t].to_quaternion(), first[t].to_quaternion()
        gap_q = q_last.inverted() @ q_first
        if gap_q.w < 0.0:
            gap_q.negate()
        gap_t = first[t].translation - last[t].translation
        for j, w in enumerate(weights):
            m = out[n - frames + j][t]
            q = m.to_quaternion() @ Quaternion().slerp(gap_q, w)
            M = q.to_matrix().to_4x4()
            M.translation = m.translation + gap_t * w
            out[n - frames + j][t] = M
    return out


def _loop_closed(locs_first, locs_last, order):
    worst = 0.0
    for t in order:
        worst = max(worst, _qangle_deg(locs_first[t].to_quaternion(), locs_last[t].to_quaternion()))
    return worst


# ------------------------------------------------------------------ restyle
def _new_action(arm, name):
    old = bpy.data.actions.get(name)
    if old is not None:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    if act.name != name:   # a ".001" take name orphans the clip in the Kit
        raise RuntimeError("action name %s became %s" % (name, act.name))
    ad = arm.animation_data or arm.animation_data_create()
    ad.action = act
    try:
        ad.action_slot = act.slots.new(id_type="OBJECT", name=arm.name)
    except Exception:
        pass
    return act


def _key_pose(tgt, Mw, frame, prev_q):
    for t in tgt.order:
        par = tgt.parent[t]
        bone = tgt.arm.data.bones[t]
        if par is None:
            basis = bone.matrix_local.inverted() @ Mw[t]
        else:
            basis = (Mw[par] @ tgt.arm.data.bones[par].matrix_local.inverted() @ bone.matrix_local).inverted() @ Mw[t]
        q = basis.to_quaternion()
        if t in prev_q and prev_q[t].dot(q) < 0.0:
            q.negate()
        prev_q[t] = q.copy()
        pb = tgt.arm.pose.bones[t]
        pb.rotation_quaternion = q
        pb.location = basis.translation
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        pb.keyframe_insert("location", frame=frame)


def _limp_report(plan, speed, period, strong_foot, limp, vanilla_points):
    """The restyle report's rows for a limp clip (see the module docstring, "The report")."""
    rows = {}
    for foot, (a, b) in plan["stance_out"].items():
        ca, cb = plan["contact_out"][foot]
        rows[foot] = {"contact_start_frame": round(ca, 3), "contact_end_frame": round(cb, 3),
                      "contact_start_phase": round((ca / period) % 1.0, 4),
                      "contact_end_phase": round((cb / period) % 1.0, 4),
                      "source_contact_frames": list(plan["contact_src"][foot]),
                      "source_frames": list(plan["stance_src"][foot]),
                      "start_frame": round(a, 3), "end_frame": round(b, 3),
                      "start_phase": round((a / period) % 1.0, 4), "end_phase": round((b / period) % 1.0, 4),
                      "frames": stance_frames((a, b), period)}
    out = {"timing_split": limp["timing_split"], "source_split": round(plan["source_split"], 4),
           "strong_foot": strong_foot, "contacts": rows,
           "warp": plan["warp"].report(),
           "source_planted_speed_m_per_frame": round(plan["planted_speed"], 5),
           "planted_speed_m_per_frame": round(speed, 5),
           "source_loop_distance_m": round(plan["planted_speed"] * period, 4),
           "loop_distance_m": round(speed * period, 4), "contact_runs_found": plan["runs"]}
    if vanilla_points and vanilla_points[0] >= 0.0:
        out["step_points_vanilla"] = [round(p, 5) for p in vanilla_points if p >= 0.0]
        mapped, wrapped = map_step_points_checked(vanilla_points, plan["warp"])
        out["step_points"] = [round(p, 5) for p in mapped]
        out["step_points_wrapped"] = wrapped
    else:
        out["step_points_gap"] = ("no vanilla step points were given (--vanilla-step-points), so the clip's StepPoints "
                                  "cannot be warped; the warped foot contact phases are in contacts")
    return out


def _rotate_subtree(Mw, bones, root, deg_about_fwd, deg_about_right):
    """Turn `bones` rigidly about root's head: first about world forward, then about world right."""
    p = Mw[root].translation.copy()
    q = Quaternion(RIGHT, math.radians(deg_about_right)) @ Quaternion(FWD, math.radians(deg_about_fwd))
    turn = Matrix.Translation(p) @ q.to_matrix().to_4x4() @ Matrix.Translation(-p)
    for t in bones:
        Mw[t] = turn @ Mw[t]


CLEARANCE_BONES = ("pelvis", "spine2", "l_thigh", "r_thigh", "l_calf", "r_calf", "l_foot", "r_foot")
ARM_BONES = ("upperarm_twist", "foretwist", "hand", "finger0")


def frame_positions(Mw):
    pos = {b: tuple(Mw[b].translation) for b in CLEARANCE_BONES}
    for side in ("l", "r"):
        for b in ARM_BONES:
            pos[side + "_" + b] = tuple(Mw[side + "_" + b].translation)
    return pos


def _apply_hand_clearance(frames_Mw, clip, spec, subtree, closed, period):
    """Swing each upper arm outward (and if need be back) in every frame by the smallest angle that gives its forearm and
    hand the clearance from the thighs, calves and torso, smoothed over time (smooth_correction: it never pops, and wraps
    at a closed loop's seam), then re-checked on the smoothed curve. Edits frames_Mw in place; returns the report row."""
    n = len(frames_Mw)
    m = period if closed else n          # the frames that carry their own correction; a closed loop's last repeats its first
    base = [frame_positions(Mw) for Mw in frames_Mw[:m]]
    caps = [body_capsules(p) for p in base]
    row = {"hand_clearance_m": spec["hand_clearance_m"], "shield_arm": SHIELD_ARM if "with_shield" in clip else None,
           "min_gap_before_m": {}, "min_gap_after_m": {}, "max_correction_deg": {}, "frames_short_after": {}}
    for side, sgn in SIDES:
        want = clearance_for(clip, side, spec["hand_clearance_m"], spec["shield_arm_clearance_m"])
        pts = [arm_points(p, side) for p in base]
        sho = [p[side + "_upperarm_twist"] for p in base]
        raw = [clearance_swing(pts[k], sho[k], sgn, caps[k], want) for k in range(m)]
        row["min_gap_before_m"][side] = round(min(arm_gap(pts[k], caps[k]) for k in range(m)), 4)
        ab = [r[0] for r in raw]
        bk = [r[1] for r in raw]
        for _ in range(8):   # smooth, then re-check: bump any frame the smoothed curve leaves short
            sab, sbk = smooth_correction(ab, closed), smooth_correction(bk, closed)
            short = [k for k in range(m) if arm_gap(swing_arm_points(pts[k], sho[k], sgn, sab[k], sbk[k]), caps[k])
                     < want - 0.001 and (sab[k] < ABDUCT_MAX_DEG or sbk[k] < BACK_MAX_DEG)]
            if not short:
                break
            for k in short:
                if ab[k] < ABDUCT_MAX_DEG:
                    ab[k] = min(ABDUCT_MAX_DEG, max(ab[k], sab[k]) + CLEARANCE_STEP_DEG)
                else:
                    bk[k] = min(BACK_MAX_DEG, max(bk[k], sbk[k]) + CLEARANCE_STEP_DEG)
        sab, sbk = smooth_correction(ab, closed), smooth_correction(bk, closed)
        gaps = []
        root = side + "_upperarm_twist"
        for k in range(n):
            j = k % m if closed else k
            if sab[j] > 0.0 or sbk[j] > 0.0:
                _rotate_subtree(frames_Mw[k], subtree[root], root, -sgn * sab[j], -sbk[j])
        for k in range(m):
            gaps.append(arm_gap(arm_points(frame_positions(frames_Mw[k]), side), body_capsules(frame_positions(frames_Mw[k]))))
        row["min_gap_after_m"][side] = round(min(gaps), 4)
        row["frames_short_after"][side] = sum(1 for g in gaps if g < want - 0.005)
        row["max_correction_deg"][side] = {"abduct": round(max(sab), 1), "back": round(max(sbk), 1)}
    return row


def hike_rise_nominal(raise_m, roll_deg, hip_half):
    """The weak hip joint's rise a frame's pelvis roll and raise add up to."""
    return abs(hip_half * math.sin(math.radians(roll_deg))) + raise_m


def _achieved(track, knee_stats, limp):
    """The report's "achieved" block: what the metric targets of the limp reached, measured on the finished pose."""
    out = {"knee_max_deg": round(track["knee_max"], 1),
           "knee_max_pelvis_lowered_m": round(knee_stats["lowered"], 4),
           "knee_max_goal_pulled_in_m": round(knee_stats["pulled"], 4),
           "min_track_half_m": round(track["half"], 4)}
    if track["bow"]:
        out["knee_bow_m"] = {"min": round(min(track["bow"]), 4), "mean": round(sum(track["bow"]) / len(track["bow"]), 4)}
        out["thigh_splay_deg"] = {"mean": round(sum(track["splay"]) / len(track["splay"]), 1),
                                  "max": round(max(track["splay"]), 1)}
    if limp["weak_lift_max_m"]:
        out["weak_lift_max_m"] = round(track["weak_lift"], 4)   # the ankle
        out["weak_toe_height_max_m"] = round(track["weak_lift_toe"], 4)
    if limp["weak_toe_down_deg"] or limp["weak_lift_max_m"]:
        out["weak_toe_down_deg"] = round(track["toe_down"], 1)
    if limp["weak_hip_hike_m"]:
        out["weak_hip_rise_m"] = round(track["hip_rise"], 4)
    if limp["strong_drop_m"] or limp["weak_drop_m"]:
        out["pelvis_extra_drop_max_m"] = round(track["drop"], 4)
    if limp["foot_outer_edge_deg"]:
        out["foot_outer_edge_deg"] = round(track["edge"], 1)
    if track["pelvis_x"]:
        out["pelvis_lateral_travel_m"] = round(max(track["pelvis_x"]) - min(track["pelvis_x"]), 4)
    return out


def restyle_clip(tgt, worlds, clip, info, spec, kind, action_name, vanilla_step_points=None):
    """Key `action_name` on tgt.arm: frame 0 rest, frames 1..N the restyled source (`worlds`, Source.frames of
    source1..source2). Returns (act, entry, warped): warped is the time-warped source poses a limp clip started from
    (compare_to_source's reference), None for any other clip. vanilla_step_points: {clip: [x, y, z, w]} of the vanilla
    StepPoints, used only by a limp clip (the entry's step_points)."""
    s1, s2 = int(round(info["source1"])), int(round(info["source2"]))
    N = len(worlds)
    order, parent = tgt.order, tgt.parent
    rest_w = tgt.rest_w
    src_worlds = worlds
    locs = [_locals(w, order, parent) for w in worlds]
    gait = kind == "gait"
    entry = {"clip": clip, "kind": kind, "source": [s1, s2], "n_frames": N}
    limp = limp_for_kind(spec.get("limp") or LIMP_DEFAULTS, kind)
    lead_side = limp_sides(limp)[1]
    lead_sgn = 1.0 if lead_side == "r" else -1.0
    strong_sgn = 1.0 if limp["strong_side"] == "r" else -1.0
    strong_foot = limp["strong_side"] + "_foot"
    weak_foot = ("l" if limp["strong_side"] == "r" else "r") + "_foot"

    # loop shape: a closed loop repeats its first pose on its last frame
    close_deg = _loop_closed(locs[0], locs[-1], order)
    closure = None
    needs_closing = False
    if gait and limp_cycle_active(limp):
        # a limp needs a closed loop: a source that misses by a few degrees is closed first, a worse one refused
        try:
            needs_closing = loop_closure_plan(close_deg)
        except ValueError as exc:
            raise RuntimeError("a limp needs a closed gait loop: " + str(exc))
    if needs_closing:
        locs = close_loop_locals(locs, order)
        worlds = src_worlds = [world_from_locals(lc, order, parent) for lc in locs]
        closure = {"source_delta_deg": round(close_deg, 2), "frames": CLOSURE_FRAMES,
                   "delta_after_deg": round(_loop_closed(locs[0], locs[-1], order), 3)}
        close_deg_open = close_deg
        close_deg = closure["delta_after_deg"]
    closed = close_deg < 1.0
    period = (N - 1) if closed else N
    entry.update({"source_loop_delta_deg": round(close_deg if closure is None else close_deg_open, 2),
                  "closed_loop": closed, "period_frames": period})
    if closure is not None:
        entry["loop_closure"] = closure

    # 1b. limp timing: find each foot's stance in the source, then play the source through the time map so the weak
    # contact lands timing_split after the strong one (module docstring, "Uneven timing without foot slide")
    plan, warped = None, None
    if gait and limp_cycle_active(limp):
        if not closed:
            raise RuntimeError("a limp needs a closed gait loop; this clip's last frame does not repeat its first")
        src_y = {f: [w[f].translation.y for w in src_worlds] for f in (strong_foot, weak_foot)}
        src_z = {f: [w[f].translation.z for w in src_worlds] for f in (strong_foot, weak_foot)}
        plan = plan_contacts(src_y, src_z, period, strong_foot, weak_foot, limp["timing_split"])
        locs = warp_locals(locs, output_positions(period, plan["warp"]), order)
        worlds = [world_from_locals(lc, order, parent) for lc in locs]
        warped = worlds

    # 2. deviation scaling (gaits): arm swing, the pelvis's whole rotation (sway_scale), pelvis bob and sway
    if gait:
        for side, _ in SIDES:
            b = side + "_upperarm_twist"
            swing_scale = spec["arm_swing_scale"]
            if side == lead_side and limp["low_arm_swing_scale"] is not None:
                swing_scale = limp["low_arm_swing_scale"]
            scaled = _scale_rot_dev([lc[b] for lc in locs], swing_scale)
            for lc, m in zip(locs, scaled):
                lc[b] = m
        scaled = _scale_rot_dev([lc["pelvis"] for lc in locs], spec["sway_scale"])
        for lc, m in zip(locs, scaled):
            lc["pelvis"] = m
    pel = [lc["pelvis"].translation.copy() for lc in locs]
    pmean = sum(pel, Vector()) / N
    sx = spec["sway_scale"] if gait else 1.0
    sz = spec["bob_scale"] if gait else 1.0

    # 4. pelvis drop from the knee bend, at the clip's mean knee angle
    legs = []
    for th, ca, fo in rt.LEGS:
        a = (rest_w[ca].translation - rest_w[th].translation).length
        b = (rest_w[fo].translation - rest_w[ca].translation).length
        toe = fo.replace("foot", "toe0")
        legs.append({"t": (th, ca, fo, toe), "a": a, "b": b, "pole": rt._knee_pole(rest_w, th, ca, fo),
                     "side": -1.0 if th.startswith("l_") else 1.0})
    hip_half = abs(rest_w["r_thigh"].translation.x - rest_w["l_thigh"].translation.x) / 2.0
    drops, knees = [], []
    for w in worlds:
        for lg in legs:
            th, ca, fo, _ = lg["t"]
            H, K, A = w[th].translation, w[ca].translation, w[fo].translation
            phi = _knee_angle(H, K, A)
            # never past a 30 deg knee, and never straighter than the source
            phi2 = max(min(phi, math.radians(30.0)), phi - math.radians(spec["knee_bend_deg"]))
            a, b = lg["a"], lg["b"]
            D = math.sqrt(max(0.0, a * a + b * b - 2.0 * a * b * math.cos(phi)))
            D2 = math.sqrt(max(0.0, a * a + b * b - 2.0 * a * b * math.cos(phi2)))
            drops.append(D - D2)
            knees.append(math.degrees(phi))
    drop = sum(drops) / len(drops)
    entry["pelvis_drop_m"] = round(drop, 4)
    entry["source_mean_knee_deg"] = round(sum(knees) / len(knees), 1)

    # 5. stride: the ankle's forward offset from the pelvis, scaled about its cycle mean
    stride = spec["stride_scale"] if gait else 1.0
    mean_off, floor = {}, {}
    for lg in legs:
        fo, toe = lg["t"][2], lg["t"][3]
        mean_off[fo] = sum(w[fo].translation.y - w["pelvis"].translation.y for w in src_worlds) / N
        floor[fo] = min(w[fo].translation.z for w in src_worlds)
        floor[toe] = min(w[toe].translation.z for w in src_worlds)

    # chain directions in each bone's own rest frame (head to anatomical chain child)
    # an end bone (head) has no chain child: it takes the line from its parent's head to its own
    rest_dir = {}
    for t in order:
        c = rt.TARGET_CHAIN_CHILD.get(t)
        if c and c in rest_w:
            d = rest_w[c].translation - rest_w[t].translation
        elif parent[t]:
            d = rest_w[t].translation - rest_w[parent[t]].translation
        else:
            continue
        if d.length > 1e-6:
            rest_dir[t] = rt._rot(rest_w[t]).transposed() @ d.normalized()

    def bdir(Mw, t):
        return (rt._rot(Mw[t]) @ rest_dir[t]).normalized()

    # 5b. limp: each ankle's forward path rebuilt in the new time, falling at exactly speed per frame while planted
    ytrack, swing_win, a_strong, a_weak, w_strong = {}, {}, 0.0, 0.0, 0.0
    if plan is not None:
        # The pelvis tilt turns the pelvis about its origin, which carries the hip joints back: a big tilt (the run's
        # lope) leaves the heel-strike goal beyond the leg's reach. Each stance is therefore centred where the source
        # had it relative to the HIPS: the mean y shift the posture gives each hip is added to the stance centre.
        hip_dy = {}
        for lg in legs:
            th = lg["t"][0]
            shifts = []
            for lc, w in zip(locs, worlds):
                Mp = {"pelvis": lc["pelvis"].copy()}
                _turn(Mp, "pelvis", _toward(bdir(Mp, "pelvis"), FWD, spec["pelvis_tilt_deg"]))
                shifts.append((Mp["pelvis"] @ lc[th].translation).y - (w["pelvis"] @ lc[th].translation).y)
            hip_dy[th] = sum(shifts) / len(shifts)
        speed = plan["planted_speed"] * stride
        pel_src = [w["pelvis"].translation.y for w in src_worlds]
        pel_out = [lc["pelvis"].translation.y for lc in locs]
        for foot in (strong_foot, weak_foot):
            a_src, b_src = plan["stance_src"][foot]
            a_out, b_out = plan["stance_out"][foot]
            src_y = [w[foot].translation.y for w in src_worlds]
            mid_src = 0.5 * (a_src + b_src)
            off_mid = interp_cyclic(src_y, mid_src, period) - interp_cyclic(pel_src, mid_src, period)
            y_mid = (interp_cyclic(pel_out, 0.5 * (a_out + b_out), period)
                     + mean_off[foot] + (off_mid - mean_off[foot]) * stride + hip_dy[foot[0] + "_thigh"]
                     - (limp["weak_trail_m"] if foot == weak_foot else 0.0))
            ytrack[foot] = ankle_y_track(src_y, (a_src, b_src), (a_out, b_out), speed, y_mid, period, plan["warp"])
            swing_win[foot] = (b_out, period - (b_out - a_out))
        a_strong, a_weak = plan["contact_out"][strong_foot][0], plan["contact_out"][weak_foot][0]
        w_strong = (a_weak - a_strong) % period
        entry.update(_limp_report(plan, speed, period, strong_foot, limp,
                                  (vanilla_step_points or {}).get(clip)))

    subtree = {}   # each yaw root with the bones below it, in hierarchy order
    for root in ("spine1", "spine2", "neck", "head", "l_upperarm_twist", "r_upperarm_twist"):
        subtree[root] = [t for t in order if t == root or _below(parent, t, root)]

    idle = spec["idle"]
    act = _new_action(tgt.arm, action_name)
    tgt.clear_pose()
    prev_q = {}
    # frame 0: the rest pose, turned like every posed frame (the Kit zeroes the root position track against it)
    M0 = {t: YAW @ rest_w[t] for t in order}
    _key_pose(tgt, M0, 0, prev_q)
    stats = {"max_unreached_m": 0.0}
    lowest = {"foot": 9.0, "toe": 9.0}
    knee_stats = {"lowered": 0.0, "pulled": 0.0}
    frames_Mw = []   # every frame's pose, keyed after the arms have been checked against the legs and torso
    measure = gait and (plan is not None or limp_static_active(limp))
    track = {"edge": 0.0, "toe_down": 0.0, "knee_max": 0.0, "bow": [], "splay": [], "half": 9.0, "weak_lift": 0.0, "weak_lift_toe": 0.0,
             "pelvis_x": [], "hip_rise": 0.0, "drop": 0.0}
    for k in range(N):
        lc = locs[k]
        w = worlds[k]
        ph = (k / period) if period > 0 else 0.0
        # limp signals at this frame; the last frame repeats the first, so it reads the first's phase
        extra_drop, lean_dyn, roll, hike, hike_raise, weight_dx = 0.0, 0.0, 0.0, 0.0, 0.0, 0.0
        swing_t, stance_t = {}, {}
        if plan is not None:
            kk = k % period
            t_strong = phase_in_window(kk, a_strong, w_strong, period)
            t_weak = phase_in_window(kk, a_weak, period - w_strong, period)
            extra_drop = limp["strong_drop_m"] * eased_pulse(t_strong) + limp["weak_drop_m"] * eased_pulse(t_weak)
            lean_dyn = strong_sgn * limp["strong_lean_deg"] * sine_bump_sq(t_strong)
            roll = limp["shoulder_roll_deg"] * eased_pulse(t_strong)
            swing_t = {f: phase_in_window(kk, b, length, period) for f, (b, length) in swing_win.items()}
            bump = sine_bump_sq(swing_t[weak_foot])   # zero slope at both ends: no kink where the swing starts
            hike = strong_sgn * limp["weak_hip_hike_deg"] * bump
            if limp["weak_hip_hike_m"]:
                # the weak hip joint rises by hike_m x bump: a pelvis roll first (to 10 deg), the rest a pelvis raise
                want = limp["weak_hip_hike_m"] * bump
                roll_rise = min(want, hip_half * math.sin(math.radians(HIKE_ROLL_MAX_DEG)))
                hike += strong_sgn * math.degrees(math.asin(roll_rise / hip_half))
                hike_raise = want - roll_rise
            stance_t = {f: phase_in_window(kk, plan["contact_out"][f][0],
                                           plan["contact_out"][f][1] - plan["contact_out"][f][0], period)
                        for f in (strong_foot, weak_foot)}
            if limp["weight_over_foot"]:
                # toward each stance foot by a share of its offset from the centreline; both down, they cancel
                for lg in legs:
                    f = lg["t"][2]
                    off = w[f].translation.x + lg["side"] * spec["stance_width_m"] - w["pelvis"].translation.x
                    weight_dx += limp["weight_over_foot"] * (1.0 - swing_ramp(swing_t[f], 0.25)) * off
        lateral = lead_sgn * limp["low_shoulder_lean_deg"] + lean_dyn
        lead_yaw = lead_sgn * limp["lead_shoulder_yaw_deg"]
        Mw = {}
        for t in order:
            par = parent[t]
            if par is None:
                M = lc[t].copy()
                p = pel[k]
                M.translation = Vector((pmean.x + (p.x - pmean.x) * sx, p.y,
                                        pmean.z + (p.z - pmean.z) * sz - drop))
                if extra_drop:
                    M.translation.z -= extra_drop
                if hike_raise:
                    M.translation.z += hike_raise
                if weight_dx:
                    M.translation.x += weight_dx
                if not gait and idle["weight_shift_m"]:
                    M.translation.x += idle["weight_shift_m"] * math.sin(2.0 * math.pi * idle["weight_cycles"] * ph)
                Mw[t] = M
            else:
                Mw[t] = Mw[par] @ lc[t]
            # 3. posture turns, top-down
            if t == "pelvis":
                _turn(Mw, t, _toward(bdir(Mw, t), FWD, spec["pelvis_tilt_deg"]))
                if hike:   # a roll about forward raises one hip: +X goes down for a positive angle
                    _turn(Mw, t, Quaternion(FWD, math.radians(hike)))
            elif t in TRUNK:
                deg = spec["trunk_flex_deg"].get(t, 0.0)
                if not gait and idle["breath_pitch_deg"] and t in ("spine1", "spine2"):
                    deg += 0.5 * idle["breath_pitch_deg"] * math.sin(2.0 * math.pi * idle["breath_cycles"] * ph)
                _turn(Mw, t, _toward(bdir(Mw, t), FWD, deg))
                if t == "spine2" and lateral:   # a roll about forward leans the torso toward +X for a positive angle
                    _turn(Mw, t, Quaternion(FWD, math.radians(lateral)))
            elif t in NECK_HEAD:
                # a pitch about the body's side axis, not _toward(bdir, FWD): once trunk and neck lean far forward (a
                # hunch plus a run's tilt) the bone lies almost along FWD, d x FWD flips sign between frames and the head
                # jumped 63 deg in one frame. For an upright bone the axis d x FWD is Z x Y = -X, the same turn.
                _turn(Mw, t, Quaternion(-RIGHT, math.radians(spec["neck_head_flex_deg"].get(t, 0.0))))
                if not gait and idle["head_yaw_deg"]:
                    h = idle["glance_harmonic"]
                    c = idle["head_cycles"]
                    yaw = idle["head_yaw_deg"] * (math.sin(2.0 * math.pi * c * ph)
                                                  + h * math.sin(2.0 * math.pi * 3 * c * ph)) / (1.0 + h)
                    _turn(Mw, t, Quaternion(UP, math.radians(yaw * (0.4 if t == "neck" else 0.6))))
            else:
                for side, sgn in SIDES:
                    lead = side == lead_side
                    if t == side + "_clavicle":
                        drop_deg = spec["clavicle_drop_deg"] + (limp["low_shoulder_deg"] if lead else 0.0)
                        _turn(Mw, t, _toward(bdir(Mw, t), -UP, drop_deg))
                        protract = spec["clavicle_protract_deg"] + roll + (limp["lead_shoulder_protract_deg"] if lead else 0.0)
                        _turn(Mw, t, _toward(bdir(Mw, t), FWD, protract))
                    elif t == side + "_upperarm_twist":
                        _turn(Mw, t, _toward(bdir(Mw, t), RIGHT * -sgn, spec["upperarm_hang_deg"]))
                        if limp["arm_forward_deg"]:
                            _turn(Mw, t, _toward(bdir(Mw, t), FWD, limp["arm_forward_deg"]))
                        if lead and limp["low_arm_forward_deg"]:
                            _turn(Mw, t, _toward(bdir(Mw, t), FWD, limp["low_arm_forward_deg"]))
                    elif t == side + "_foretwist":
                        _turn(Mw, t, _toward(bdir(Mw, t), FWD, spec["elbow_bend_deg"]))
        # 3b. the lead shoulder's yaw, after every other turn of the upper body so that it is one rigid turn about the
        # vertical: the upper trunk by 40 per cent on spine1 and 60 on spine2 (+X comes forward for a positive angle),
        # then neck and head turned back by the same total, which leaves the head's orientation exactly as it was
        if lead_yaw:
            for root, frac in (("spine1", LEAD_YAW_SPLIT["spine1"]), ("spine2", LEAD_YAW_SPLIT["spine2"]),
                               ("neck", -LEAD_YAW_COUNTER["neck"]), ("head", -LEAD_YAW_COUNTER["head"])):
                _yaw_subtree(Mw, subtree[root], root, lead_yaw * frac)
        # 5. legs to the widened, stride-scaled source ankle path; foot keeps the source orientation. First every goal,
        # then the knee limit's pelvis lowering, then the IK and the feet (a leg's goal never depends on the other leg)
        goals = []
        for lg in legs:
            th, ca, fo, toe = lg["t"]
            A = w[fo].translation
            py = w["pelvis"].translation.y
            ax, az, toe_z = A.x, A.z, w[toe].translation.z
            gy = py + mean_off[fo] + (A.y - py - mean_off[fo]) * stride
            hang = drop
            t_sw = swing_t.get(fo)
            if plan is not None:
                # the rebuilt forward path; the swing lifted or lowered per foot, swing-out for the weak foot
                gy = ytrack[fo][k % period]
                hang = drop + extra_drop
                if t_sw is not None:
                    strong = fo == strong_foot
                    scale = limp["strong_lift_scale" if strong else "weak_lift_scale"]
                    ramp = swing_ramp(t_sw)
                    height = max(0.0, az - floor[fo])
                    if scale >= 1.0:
                        dz = height * (scale - 1.0) * ramp
                    else:   # never sink the ankle or the toe below the source's lowest point
                        dz = -min(height * (1.0 - scale) * ramp, max(0.0, toe_z - floor[toe]))
                    az, toe_z = az + dz, toe_z + dz
                    if not strong and limp["weak_lift_max_m"]:
                        # an absolute cap on the weak ankle's height above its floor (a scuff); the toe's height is
                        # held to the same cap by the foot's pitch, further down
                        over = (az - floor[fo]) - limp["weak_lift_max_m"]
                        if over > 0.0:
                            dz = -min(over, max(0.0, az - floor[fo]))   # a cap is not blended: it holds all swing
                            az, toe_z = az + dz, toe_z + dz
                    if not strong:
                        ax += lg["side"] * limp["weak_swing_out_m"] * sine_bump_sq(t_sw)
            # a lifted foot drops with the hips; a planted heel or toe keeps the vanilla height
            lift = min(az - floor[fo], toe_z - floor[toe])
            frac = max(0.0, min(1.0, lift / SWING_LIFT_M))
            # no ankle closer to the clip's mean pelvis line than min_track_half_m (one fixed reference, never the moving pelvis)
            gx = foot_goal_x(ax, lg["side"], spec["stance_width_m"], limp["min_track_half_m"], pmean.x)
            # the lowest the goal may go when the hips drop: neither ankle nor toe below its lowest source height
            lowest_goal = max(floor[fo], floor[toe] - (toe_z - az))
            if fo == weak_foot and limp["weak_lift_max_m"]:
                lowest_goal = floor[fo]   # its sole is turned flat, which keeps the toe up; the pitch guards the toe
            gz = az - hang * frac
            if plan is not None:   # a lifted foot capped low (weak_lift_max_m) must not be dropped through the floor
                gz = max(gz, lowest_goal)
            G = Vector((gx, gy, gz))
            goals.append({"lg": lg, "G": G, "frac": frac, "t_sw": t_sw, "floor": lowest_goal})
        if limp["knee_max_deg"] < LIMP_DEFAULTS["knee_max_deg"] and gait:
            # the knee never straightens past knee_max_deg: the pelvis goes down until each planted foot is within the
            # longest reach that angle allows, and a goal still past it (a swing foot, a far step) is pulled in
            need = 0.0
            for gl in goals:
                a, b = gl["lg"]["a"], gl["lg"]["b"]
                dmax = math.sqrt(a * a + b * b - 2.0 * a * b * math.cos(math.radians(limp["knee_max_deg"])))
                gl["dmax"] = dmax
                H, G = Mw[gl["lg"]["t"][0]].translation, gl["G"]
                dxy2 = (G.x - H.x) ** 2 + (G.y - H.y) ** 2
                if dxy2 < dmax * dmax:
                    # weighted by how planted the foot is (1 down to 0 as it lifts), so the lowering fades in and out
                    # with the foot instead of switching on at a threshold
                    need = max(need, (1.0 - gl["frac"]) * ((H.z - G.z) - math.sqrt(dmax * dmax - dxy2)))
            if need > 0.0:
                for t in Mw:
                    Mw[t].translation.z -= need
                for gl in goals:
                    # a lifted foot follows the hips down, but never to below its lowest source height
                    gl["G"].z = max(gl["G"].z - need * gl["frac"], min(gl["G"].z, gl["floor"]))
                knee_stats["lowered"] = max(knee_stats["lowered"], need)
            for gl in goals:
                H = Mw[gl["lg"]["t"][0]].translation
                v = gl["G"] - H
                if v.length > gl["dmax"]:
                    knee_stats["pulled"] = max(knee_stats["pulled"], v.length - gl["dmax"])
                    if abs(v.z) < gl["dmax"]:
                        # shorten the reach on the ground plane, so the foot keeps its height (a planted foot stays on
                        # the floor, a capped weak foot under its cap) instead of being lifted toward the hip
                        flat = math.sqrt(gl["dmax"] ** 2 - v.z ** 2)
                        horizontal = math.hypot(v.x, v.y)
                        gl["G"] = Vector((H.x + v.x * flat / horizontal, H.y + v.y * flat / horizontal, gl["G"].z))
                    else:
                        gl["G"] = H + v * (gl["dmax"] / v.length)
        for gl in goals:
            lg, G, t_sw = gl["lg"], gl["G"], gl["t_sw"]
            th, ca, fo, toe = lg["t"]
            pole_local = lg["pole"]
            bow = math.radians(limp["knee_bow_deg"]) if gait else 0.0
            axis = G - Mw[th].translation
            if gait and limp["knee_bow_m"] and axis.length > 1e-6:
                # the knee's distance from the hip-to-ankle line, from the leg's own triangle, sets the pole turn that
                # puts it knee_bow_m outside that line in the front view
                a, b = lg["a"], lg["b"]
                dc = min(max(axis.length, abs(a - b) + 1e-4), a + b - 1e-4)
                d_knee = a * math.sqrt(max(0.0, 1.0 - ((a * a + dc * dc - b * b) / (2.0 * a * dc)) ** 2))
                bow += min(math.radians(KNEE_BOW_MAX_DEG),
                           math.asin(min(1.0, limp["knee_bow_m"] / d_knee)) if d_knee > 1e-6 else math.pi / 2.0)
            if bow and axis.length > 1e-6:
                # turn the knee's bend plane outward about the hip-to-ankle axis (down, so a positive turn moves a
                # forward-pointing knee toward +X: outward on the right leg, inward on the left, hence the side sign)
                r_th = rt._rot(Mw[th])
                turned = Quaternion(axis.normalized(), lg["side"] * bow) @ (r_th @ pole_local)
                pole_local = r_th.transposed() @ turned
            rt.two_bone_ik(Mw, th, ca, fo, G, lg["a"], lg["b"], pole_local, stats)
            splay = Quaternion(UP, math.radians(-lg["side"] * spec["foot_splay_deg"]))
            F = (splay.to_matrix() @ rt._rot(w[fo])).to_4x4()
            # the foot lands heel and outer edge first (a roll about forward, flat by mid-stance); the weak foot's toe
            # points down over its swing, never so far that the toe sinks below its lowest point
            if plan is not None and gait:
                t_c = stance_t.get(fo)
                if limp["foot_outer_edge_deg"] and t_c is not None:
                    roll_deg = lg["side"] * limp["foot_outer_edge_deg"] * (1.0 - smoothstep(t_c / 0.5))
                    F = (Quaternion(FWD, math.radians(roll_deg)).to_matrix() @ F.to_3x3()).to_4x4()
                    track["edge"] = max(track["edge"], abs(roll_deg))
                if (limp["weak_toe_down_deg"] or limp["weak_lift_max_m"]) and fo == weak_foot and t_sw is not None:
                    # The dragged foot scuffs: over its swing its sole turns toward flat (the source's yaw, the rest
                    # pose's pitch and roll), which keeps the toe as low as the ankle and under the lift cap, and it
                    # is pitched toe-down by weak_toe_down_deg on top, never so far that the toe sinks below its lowest
                    # point. Both blends use the swing ramp, so they are zero at the contacts.
                    flat_w = swing_ramp(t_sw, 0.1) if limp["weak_lift_max_m"] else 0.0
                    if flat_w > 0.0:
                        # the yaw from the foot's lateral axis, which stays near horizontal as the foot pitches (the
                        # forward axis points straight up at a kicking run's toe-off and its yaw flips)
                        axis_f = rt._rot(rest_w[fo]).transposed() @ RIGHT
                        d_f = rt._rot(w[fo]) @ axis_f
                        yaw_src = math.atan2(d_f.y, d_f.x)
                        flat = (splay.to_matrix() @ Quaternion(UP, yaw_src).to_matrix() @ rt._rot(rest_w[fo]))
                        F = F.to_3x3().to_quaternion().slerp(flat.to_quaternion(), flat_w).to_matrix().to_4x4()
                    base3 = F.to_3x3()

                    def toe_height(pitch_deg):
                        Fp = Quaternion(RIGHT, math.radians(-pitch_deg)).to_matrix() @ base3
                        return G.z + (Fp @ lc[toe].translation).z - floor[toe]

                    pitch = limp["weak_toe_down_deg"] * swing_ramp(t_sw)
                    if pitch > 0.0 and toe_height(pitch) < 0.0:   # toe height falls with the pitch up to ~45 deg
                        lo_p, hi_p = 0.0, pitch
                        for _ in range(12):
                            mid = 0.5 * (lo_p + hi_p)
                            lo_p, hi_p = (mid, hi_p) if toe_height(mid) >= 0.0 else (lo_p, mid)
                        pitch = lo_p
                    F = (Quaternion(RIGHT, math.radians(-pitch)).to_matrix() @ base3).to_4x4()
                    track["toe_down"] = max(track["toe_down"], pitch)
            F.translation = Mw[ca] @ lc[fo].translation
            Mw[fo] = F
            Mw[toe] = Mw[fo] @ lc[toe]
            lowest["foot"] = min(lowest["foot"], Mw[fo].translation.z)
            lowest["toe"] = min(lowest["toe"], Mw[toe].translation.z)
            if measure:
                H, K, Af = Mw[th].translation, Mw[ca].translation, Mw[fo].translation
                track["knee_max"] = max(track["knee_max"], math.degrees(_knee_angle(H, K, Af)))
                bow_out = knee_bow_offset(tuple(H), tuple(K), tuple(Af), lg["side"])
                if bow_out is not None:
                    track["bow"].append(bow_out)
                    track["splay"].append(math.degrees(math.atan2(lg["side"] * (K.x - H.x), H.z - K.z)))
                track["half"] = min(track["half"], lg["side"] * (Af.x - pmean.x))
                if fo == weak_foot and t_sw is not None:
                    track["weak_lift"] = max(track["weak_lift"], Af.z - floor[fo])
                    track["weak_lift_toe"] = max(track["weak_lift_toe"], Mw[toe].translation.z - floor[toe])
        if measure and k < period:
            track["pelvis_x"].append(Mw["pelvis"].translation.x)
            track["hip_rise"] = max(track["hip_rise"], hike_rise_nominal(hike_raise, hike, hip_half))
            track["drop"] = max(track["drop"], extra_drop)
        frames_Mw.append(Mw)
    if hand_clearance_active(spec):   # gaits and idles alike: a standing orc holds a sword and a shield too
        entry["hand_clearance"] = _apply_hand_clearance(frames_Mw, clip, spec, subtree, closed, period)
    # 7. the Kit's root rule: the whole pose turned 180 deg about world Z
    for k, Mw in enumerate(frames_Mw):
        _key_pose(tgt, {t: YAW @ Mw[t] for t in order}, k + 1, prev_q)
    entry["ik_max_unreached_m"] = round(stats["max_unreached_m"], 4)
    entry["lowest_head_z_m"] = {kk: round(v, 4) for kk, v in lowest.items()}
    entry["stride_scale"] = stride
    if measure:
        entry["achieved"] = _achieved(track, knee_stats, limp)
        entry["track_centre_x_m"] = round(pmean.x, 5)
    return act, entry, warped


def compare_to_source(tgt, act, worlds):
    """Largest bone-local rotation difference (deg) between `act` (yaw undone) and the source (`worlds`, one pose per
    frame), over all frames."""
    worst, where = 0.0, None
    ad = tgt.arm.animation_data
    ad.action = act
    rt._assign_slot(tgt.arm)
    inv = YAW.inverted()
    n = int(round(act.frame_range[1]))
    for k in range(1, n + 1):
        bpy.context.scene.frame_set(k)
        cur = {pb.name: inv @ pb.matrix.copy() for pb in tgt.arm.pose.bones}
        lc = _locals(cur, tgt.order, tgt.parent)
        ls = _locals(worlds[k - 1], tgt.order, tgt.parent)
        for t in tgt.order:
            dg = _qangle_deg(lc[t].to_quaternion(), ls[t].to_quaternion())
            if dg > worst:
                worst, where = dg, (t, k)
    return round(worst, 2), where


# ------------------------------------------------------------------ preview
def attach_body(tgt_arm, body_fbx):
    """Bind the body meshes of the human FBX to the engine rig, turned to face +Y (as the retarget tool does)."""
    new_objs, _ = rt._import_fbx(body_fbx)
    meshes = [o for o in new_objs if o.type == "MESH"]
    turn = Matrix.Rotation(math.pi, 4, "Z")
    for o in meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        for md in o.modifiers:
            if md.type == "ARMATURE":
                md.object = tgt_arm
        o.parent = tgt_arm
        o.matrix_parent_inverse = Matrix.Identity(4)
        o.matrix_world = turn @ mw
    rt._delete_objects([o for o in new_objs if o.type != "MESH"])
    return meshes


def _ground():
    if bpy.data.objects.get("_ground") is None:
        bpy.ops.mesh.primitive_plane_add(size=4.0, location=(0.0, 0.0, 0.0))
        bpy.context.active_object.name = "_ground"


def contact_sheet(tgt, acts, out_png, tile=(300, 450)):
    """Rows: for each view (side, front), one row per action; columns at 0, 25, 50, 75 per cent of the clip."""
    import numpy as np
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_WORKBENCH"
    scn.display.shading.light = "STUDIO"
    scn.display.shading.color_type = "MATERIAL"
    scn.render.resolution_x, scn.render.resolution_y = tile
    scn.render.resolution_percentage = 100
    _ground()
    cam = rt._ensure_camera_light()
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.3
    target = Vector((0.0, 0.0, 0.95))
    # the keyed pose faces Blender -Y (turned for the Kit): side camera on +X, front camera on -Y
    views = (("side", Vector((4.0, 0.0, 0.95))), ("front", Vector((0.0, -4.0, 0.95))))
    n = int(round(acts[0].frame_range[1]))
    frames = [1 + int(round(i * (n - 1) / 4.0)) for i in range(4)]
    rows = [(v, a) for v in views for a in acts]
    W, H = tile
    grid = np.zeros((H * len(rows), W * len(frames), 4), dtype=np.float32)
    tmp = out_png[:-4] + "_tile.png"
    ad = tgt.arm.animation_data
    for r, ((vname, pos), act) in enumerate(rows):
        ad.action = act
        rt._assign_slot(tgt.arm)
        rt._aim(cam, pos, target)
        for c, f in enumerate(frames):
            scn.frame_set(f)
            scn.render.filepath = tmp
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(tmp)
            px = np.empty(W * H * 4, dtype=np.float32)
            img.pixels.foreach_get(px)
            bpy.data.images.remove(img)
            y0 = (len(rows) - 1 - r) * H     # Blender image rows start at the bottom
            grid[y0:y0 + H, c * W:(c + 1) * W, :] = px.reshape(H, W, 4)
    out = bpy.data.images.new("_sheet", W * len(frames), H * len(rows), alpha=True)
    out.pixels.foreach_set(grid.ravel())
    out.filepath_raw = out_png
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    if os.path.exists(tmp):
        os.remove(tmp)


# ------------------------------------------------------------------ main
def clip_kind(clip):
    if clip.startswith(("walk_", "run_")):
        return "gait"
    if clip.startswith("stand_"):
        return "idle"
    return None


def clear_stale_verdict(out):
    """Remove the previous run's .DONE and error log once a run reaches main(); the caller still deletes .DONE before
    launching, because the launcher detaches and an import-time failure never gets here."""
    for name in ("restyle_report.json.DONE", "restyle_error.log"):
        path = os.path.join(out, name)
        if os.path.exists(path):
            os.remove(path)


def error_out_dir(argv):
    """The --out folder named in argv (both `--out X` and `--out=X`), else the current directory. Never raises."""
    out = None
    for i, a in enumerate(argv):
        if a == "--out":
            out = argv[i + 1] if i + 1 < len(argv) else None
        elif a.startswith("--out="):
            out = a[len("--out="):]
    return out or os.getcwd()


DEFAULT_STEP_POINTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "vanilla_locomotion_step_points.json")


def load_step_points(path, required=False):
    """{vanilla clip: [x, y, z, w]} from the JSON at `path` (underscore keys are notes). An empty path, or a missing one
    when `required` is False (the DEFAULT file only), reads as {}. A path the caller named (required=True) that is missing
    raises, and so does a file that exists but is malformed: never a silent {}."""
    if required and not (path and os.path.exists(path)):
        raise SystemExit("--vanilla-step-points %s does not exist" % path)
    if not path or not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8-sig") as fh:
        raw = json.load(fh)
    out = {}
    for clip, pts in raw.items():
        if clip.startswith("_"):
            continue
        if not isinstance(pts, list) or len(pts) < 2 or not all(_is_number(p) for p in pts):
            raise SystemExit("step points file %s: %r must be a list of numbers, not %r" % (path, clip, pts))
        if not all(p == -1 or 0.0 <= p <= 1.0 for p in pts):   # a progress point, or -1 for an unused one
            raise SystemExit("step points file %s: %r holds a value outside 0 to 1 (or -1 for unused): %r" % (path, clip, pts))
        out[clip] = [float(p) for p in pts]
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--source-dir", required=True, help="read_anim_keyframes_tpac.ps1 -ByClip output folder")
    ap.add_argument("--variant", required=True, help="variant spec JSON (tools/blender/locomotion_variants/*.json)")
    ap.add_argument("--engine-skeleton", required=True, help="tools/blender/human_skeleton_engine.json")
    ap.add_argument("--out", required=True, help="staging folder for the FBX (never the Armory)")
    ap.add_argument("--preview", default=None, help="folder for the contact sheets")
    ap.add_argument("--body", default=r"E:\LOTRAOMAssets\human_skeleton_with_male_body.fbx")
    ap.add_argument("--only", nargs="*", default=None, help="vanilla clip names (exact)")
    ap.add_argument("--allow-over-cap", action="store_true", help="export even when the spec passes a combat cap")
    ap.add_argument("--vanilla-step-points", default=None,
                    help="JSON {vanilla clip: [x, y, z, w]} of the vanilla StepPoints; a limp clip maps them through "
                         "its time warp into the report's step_points. Default: tools/blender/"
                         "vanilla_locomotion_step_points.json when it exists; a path named here must exist")
    args = ap.parse_args(argv)

    spec = load_spec(args.variant)
    over = all_cap_violations(spec)
    if over and not args.allow_over_cap:
        raise SystemExit("spec over the combat caps: " + "; ".join(over))
    with open(os.path.join(args.source_dir, "clips_index.json"), encoding="utf-8-sig") as fh:
        index = json.load(fh)
    clips = sorted(index)
    if args.only:
        unknown = [c for c in args.only if c not in index]
        if unknown:
            raise SystemExit("--only names not in clips_index.json: " + ", ".join(unknown))
        clips = [c for c in clips if c in args.only]
    step_points = (load_step_points(args.vanilla_step_points, required=True) if args.vanilla_step_points
                   else load_step_points(DEFAULT_STEP_POINTS))
    os.makedirs(args.out, exist_ok=True)
    clear_stale_verdict(args.out)
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn = bpy.context.scene
    scn.render.fps, scn.render.fps_base = 30, 1.0
    tgt = rt.HumanTarget(rt.build_engine_rig(args.engine_skeleton))
    if tgt.Hw != Matrix.Identity(4):
        raise RuntimeError("engine rig object transform is not identity")
    if args.preview:
        attach_body(tgt.arm, args.body)
        # the FBX importer sets the scene rate from the body file (24 fps); the clips export at 30
        scn.render.fps, scn.render.fps_base = 30, 1.0

    prefix = spec["clip_prefix"]
    # the whole loaded spec plus the tool's own source; each clip's entry also carries its own merged-spec sha
    loaded_sha = provenance_sha(spec, code_shas())
    report = {"variant": args.variant, "spec": spec, "spec_sha": loaded_sha, "code_shas": code_shas(),
              "cap_violations": over, "clips": {}}
    gen_index = {}
    if args.only:
        # a partial run updates the other clips' index rows and report entries instead of dropping them
        # (gen_troll_anim_clips.ps1 -Verify would otherwise count the dropped clips as another pipeline's)
        for name, target in (("clips_index.json", gen_index), ("restyle_report.json", report["clips"])):
            path = os.path.join(args.out, name)
            if os.path.exists(path):
                with open(path, encoding="utf-8") as fh:
                    data = json.load(fh)
                if name == "restyle_report.json":
                    report["clips"] = merge_report_clips(data.get("clips", {}), report["clips"])
                else:
                    target.update(data)
    sources = {}
    for clip in clips:
        info = index[clip]
        kind = clip_kind(clip)
        action_name = prefix + "mst_" + clip
        entry = {"action": action_name, "spec_sha": clip_spec_sha(spec, clip, kind), "loaded_spec_sha": loaded_sha}
        try:
            if kind is None:
                raise RuntimeError("not a walk_, run_ or stand_ clip")
            if len(action_name) > 63 or len(prefix + clip) > 63:
                raise RuntimeError("name over the engine's 63 characters")
            master = info["master"]
            if master not in sources:
                sources[master] = Source(os.path.join(args.source_dir, master + ".json"))
            worlds = sources[master].frames(int(round(info["source1"])), int(round(info["source2"])))
            # a limp clip is compared with the source it was warped from
            act, e, warped = restyle_clip(tgt, worlds, clip, info, spec_for_clip(spec, clip), kind, action_name, step_points)
            entry.update(e)
            ident, _, _ = restyle_clip(tgt, worlds, clip, info, identity_spec(spec), kind, action_name + "_identity")
            entry["identity_max_deg"], entry["identity_worst_at"] = compare_to_source(tgt, ident, worlds)
            entry["restyle_max_deg"], entry["restyle_worst_at"] = compare_to_source(tgt, act, warped or worlds)
            if args.preview:
                contact_sheet(tgt, [ident, act], os.path.join(args.preview, clip + ".png"))
            tgt.arm.animation_data.action = act
            rt._assign_slot(tgt.arm)
            out_path = os.path.join(args.out, action_name + ".fbx")
            if scn.render.fps != 30 or scn.render.fps_base != 1.0:
                raise RuntimeError("scene rate is %s/%s, not 30 fps" % (scn.render.fps, scn.render.fps_base))
            entry["export"] = rt.export_clip(tgt, act, out_path)
            if entry["export"]["ok"]:
                entry["engine_check"] = rt.check_export_against_engine(out_path, args.engine_skeleton)
                gen_index[clip] = {"master": "anim_mst_" + clip, "master_guid": "", "source1": 0.0,
                                   "source2": float(entry["n_frames"] - 1), "duration_s": info.get("duration_s"),
                                   "master_root_keys": entry["n_frames"] + 1}
            bpy.data.actions.remove(ident)
        except Exception:
            entry["error"] = traceback.format_exc()
        if "error" in entry or not entry.get("export", {}).get("ok"):
            gen_index.pop(clip, None)   # never leave an older master's row for a clip that failed now
        report["clips"][clip] = entry
        with open(os.path.join(args.out, "restyle_report.json"), "w") as fh:
            json.dump(report, fh, indent=1)

    with open(os.path.join(args.out, "clips_index.json"), "w") as fh:
        json.dump(gen_index, fh, indent=1)
    failed = [c for c, e in report["clips"].items() if "error" in e or not e.get("export", {}).get("ok")]
    report["summary"] = {"clips": len(report["clips"]), "exported_ok": len(gen_index), "failed": failed}
    with open(os.path.join(args.out, "restyle_report.json"), "w") as fh:
        json.dump(report, fh, indent=1)
    with open(os.path.join(args.out, "restyle_report.json.DONE"), "w") as fh:
        if not report["clips"]:
            fh.write("fail: no clip matched the inputs\n")
        else:
            fh.write(("fail: %d clip(s): %s" % (len(failed), ", ".join(failed)) if failed else "done") + "\n")


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        if isinstance(exc, SystemExit) and not exc.code:
            raise
        # the launcher detaches, so write the error where the caller waits for .DONE
        _argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
        try:
            _out = error_out_dir(_argv)
            os.makedirs(_out, exist_ok=True)
            with open(os.path.join(_out, "restyle_error.log"), "w") as fh:
                fh.write(traceback.format_exc())
            with open(os.path.join(_out, "restyle_report.json.DONE"), "w") as fh:
                fh.write("fail: see restyle_error.log\n")
        except Exception:
            pass
        raise
