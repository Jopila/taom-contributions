"""Validate restyled locomotion clips (restyle_human_clip.py output) against their vanilla sources. Read-only.

Headless (the launcher DETACHES; read <out>/validate_report.json and its .DONE twin). Delete
<out>/validate_report.json.DONE before you launch: until Blender reaches main(), or after an import-time failure, the
file there is the previous run's verdict.
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe -b -P tools/blender/validate_locomotion_clips.py -- ^
        --out "E:\\LOTRAOMAssets\\troll_clips_to_import\\orc_locomotion\\orc" ^
        --source-dir "E:\\LOTRAOMAssets\\orc_locomotion\\src" ^
        --engine-skeleton tools/blender/human_skeleton_engine.json [--only walk_forward_1h]

Each exported FBX is RE-IMPORTED (what the Kit will read), turned back to face +Y, and compared frame by frame with
its vanilla source (the master JSON sampled straight from its keyframes). Pass or fail per clip, per check:
  bones        the 28 bone names and parents of the engine skeleton, nothing more or less
  frames       N posed frames after the rest frame, N = source2 - source1 + 1; the scene runs at 30 fps
  finite       no NaN or infinite value in any bone matrix
  rest_frame   frame 0 within 0.5 deg of the engine rest (restyle report's engine check)
  flips        no bone turns more per frame than the source does times that bone's OWN declared gait scale (each arm's
               swing scale, the pelvis's sway scale, 1 for the rest) plus 15 deg (a sign flip turns ~180)
  joints       each bone within 25 deg of the source's local rotation (legs 45, from the IK), plus the posture turn the
               spec declares for that bone (declared_turn_deg); knees 25 to 180. A limp clip is compared with the
               source played through the report's time map (flips and loop too)
  loop         at the loop seam, each bone's step and change of step are each no larger than the larger of the
               clip's interior maximum and the source's seam value times the spec's largest gait deviation scale,
               plus 1 deg; a closed loop's last frame still repeats its first
  slide        gaits: in each foot's contact frames (taken from the SOURCE: ankle within 1.5 cm of its lowest and
               moving back) the ankle moves back at a steady rate (residual no worse than the source's plus 0.5 cm
               per frame) and that rate over the source's is within 0.05 of the stride scale;
               a LIMP clip (the restyle report lists contact intervals) is judged on each foot's grounded PLATEAU, found
               again independently on the exported poses and on the source poses (rs.contact_indices, the longest stance,
               rs.plateau_window), never taken from the report: the report's `frames` must cover the plateau found here
               (all but TOL plateau_missing_frames), and over the two plateaus the planted ankle moves back at a steady
               rate (residual no worse than the source plateau's plus 0.5 cm per frame), its sideways and vertical speed
               (the largest |vx| and |vz|) stay within the source plateau's plus 0.5 cm, and its rate is the report's
               planted speed within 0.05, the two steps (strong contact to weak contact, and back) sum to the stride scale
               times the source's loop distance within 0.05, the planted ankle stays within 2 cm of the foot's lowest in
               those frames, and the strong foot's contact found again in the exported clip starts no more than 2.5
               frames after the report's and no more than 4 before it (the weak foot's start and the measured split are
               reported, not gated: a barely lifted weak foot is inside the contact band early). The landing and
               take-off frame at each end of a plateau of 4 or more are left out of the rate, residual, lateral and
               vertical gates on BOTH clips: the foot is still arriving or already leaving there, and the restyle's pelvis
               drop (knee_bend_deg) lands a swing foot on the floor a frame or two before the source's, so the band
               definition catches the source's own landing step (the vanilla run steps 1.8 cm sideways on its landing
               frame). They are not free: the clip's sideways speed on those frames may exceed the source's (its largest
               within one frame of the source position the report's time map plays there) by at most TOL
               end_frame_lateral_cm, 1.0 cm per frame, and an end frame may move back no faster than the planted speed
               times 1 + stride_rel plus 0.5 cm (a foot that rolls onto the floor is slower, a backward skid is faster).
               Both ways, every frame the report claims as planted must lie on the plateau found here or on one of its
               two end frames, so a frame the foot stalls, skids or lifts on cannot be claimed past the gates. weak_trail_m moves a whole ankle path and changes none of these;
               idles: no foot faster than the source's foot plus 0.3 cm per frame
  ground       no ankle or toe head lower than the source's lowest minus 1 cm
"""
import argparse
import json
import math
import os
import statistics
import sys
import traceback

import bpy
from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import retarget_mannequin_to_human as rt  # noqa: E402
import restyle_human_clip as rs  # noqa: E402

LEG_BONES = {"l_thigh", "l_calf", "l_foot", "l_toe0", "r_thigh", "r_calf", "r_foot", "r_toe0"}
TOL = {"rest_deg": 0.5, "flip_extra_deg": 15.0, "joint_deg": 25.0, "joint_leg_deg": 45.0, "loop_deg": 1.0,
       "slide_cm": 0.5, "stride_rel": 0.05, "idle_foot_cm": 0.3, "ground_m": 0.01, "contact_start_frames": 2.5,
       "early_contact_frames": 4.0,
       "stance_height_cm": 2.0, "plateau_missing_frames": 1, "plateau_trim_frames": 1,
       "end_frame_lateral_cm": 1.0}


def read_fbx(path):
    """Re-import an exported clip. Returns (bone names, parents, per-frame world matrices facing +Y, frame count)."""
    objs, arm = rt._import_fbx(path)
    rt._assign_slot(arm)
    act = arm.animation_data.action
    f0, f1 = (int(round(v)) for v in act.frame_range)
    scn = bpy.context.scene
    names = [b.name for b in arm.data.bones]
    parents = {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}
    frames = []
    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        frames.append({pb.name: arm.matrix_world @ pb.matrix for pb in arm.pose.bones})
    # the export turns the character to face -Y for the Kit; turn it back for the comparison
    rest = frames[0]
    unturn = rs.YAW.inverted() if rest["l_toe0"].translation.y < rest["l_foot"].translation.y else Matrix.Identity(4)
    frames = [{t: unturn @ m for t, m in fr.items()} for fr in frames]
    fps = scn.render.fps / scn.render.fps_base
    rt._delete_objects(objs)
    try:
        bpy.data.actions.remove(act)
    except Exception:
        pass
    return names, parents, frames, fps


def _finite(frames):
    return all(math.isfinite(v) for fr in frames for m in fr.values() for row in m for v in row)


def _steps(locs, order):
    """Per bone, the largest frame-to-frame turn (deg)."""
    out = {}
    for t in order:
        out[t] = max((rs._qangle_deg(a[t].to_quaternion(), b[t].to_quaternion()) for a, b in zip(locs, locs[1:])),
                     default=0.0)
    return out


def _seam(locs, order, closed):
    """Per bone, how the loop seam compares with the clip's own interior frames, in degrees:
    {bone: (seam step, largest interior step, seam velocity change, largest interior velocity change)}.
    The step is the turn from one frame to the next; the velocity change is the turn between two
    consecutive steps. A closed loop's last frame repeats its first, so it is dropped before wrapping."""
    out = {}
    for t in order:
        q = [lc[t].to_quaternion() for lc in locs]
        cyc = q[:-1] if closed else q
        m = len(cyc)
        steps = [cyc[i].rotation_difference(cyc[(i + 1) % m]) for i in range(m)]
        ang = [rs._qangle_deg(cyc[i], cyc[(i + 1) % m]) for i in range(m)]
        acc = [rs._qangle_deg(steps[i - 1], steps[i]) for i in range(m)]
        # the wrap step is steps[m - 1]; the velocity changes that touch it are acc[m - 1] and acc[0]
        out[t] = (ang[m - 1], max(ang[:m - 1]), max(acc[m - 1], acc[0]), max(acc[1:m - 1]))
    return out


def _loop_check(lo, ls, order, closed, amp):
    """Pass when, for every bone, the seam step and the seam velocity change are each no larger than the larger of
    the clip's interior maximum and the source's seam value times amp, plus the tolerance. amp is the spec's largest
    gait deviation scale: scaling a motion by s scales every change of velocity by up to s, the source's own seam
    kink included (vanilla walk_forward_unarmed: 2.74 deg on r_thigh, 4.65 at sway 1.6)."""
    so, ss = _seam(lo, order, closed), _seam(ls, order, closed)
    tol = TOL["loop_deg"]
    bad = {}
    for t in order:
        step, step_in, acc, acc_in = so[t]
        step_ok = step <= max(step_in, ss[t][0] * amp) + tol
        acc_ok = acc <= max(acc_in, ss[t][2] * amp) + tol
        if not (step_ok and acc_ok):
            bad[t] = {"seam_step": round(step, 2), "interior_step": round(step_in, 2), "seam_vel": round(acc, 2),
                      "interior_vel": round(acc_in, 2), "source_seam": [round(ss[t][0], 2), round(ss[t][2], 2)]}
    worst = max(order, key=lambda t: so[t][2] - so[t][3])
    return {"pass": not bad, "bones": bad, "source_amp": amp,
            "worst_bone": {worst: [round(v, 2) for v in so[worst]], "_fields": "seam step, interior step max, "
                           "seam vel change, interior vel change max"}}


def _velocities(frames, foot, closed):
    pos = [fr[foot].translation for fr in frames]
    n = len(pos) - (1 if closed else 0)
    return [(pos[(k + 1) % n] if closed else pos[k + 1]) - pos[k] for k in range(n if closed else n - 1)], pos


def contact_frames(src_frames, foot, closed):
    """Frames where the SOURCE ankle is within 1.5 cm of its lowest and moving back. The restyle keeps the vanilla
    contact timing, so these frames judge both clips (a restyled swing foot that drops with the hips would
    otherwise enter the window and count as contact)."""
    pos = [fr[foot].translation for fr in src_frames]
    return rs.contact_indices([p.z for p in pos], [p.y for p in pos], closed)


def _contact_stats(frames, foot, closed, idx, min_frames=3):
    """Over the given contact frames: (median back rate, largest residual from it in cm per frame)."""
    vel, _ = _velocities(frames, foot, closed)
    sel = [vel[k] for k in idx]
    if len(sel) < min_frames:
        return None, None
    med = statistics.median(v.y for v in sel)
    resid = max(math.sqrt((v.y - med) ** 2 + v.x ** 2 + v.z ** 2) for v in sel)
    return med, resid * 100.0


def declared_turn_deg(spec, bone, extra=None, kind="gait"):
    """The posture turn restyle_human_clip.py applies to `bone` on purpose (absolute degrees, 0 for a bone it does
    not turn). The joints check adds it to the bone's limit, so a deliberate posture is not reported as a fault."""
    if not spec:
        return 0.0
    if extra and bone in extra:   # e.g. the hand clearance's swing of an upper arm, from the restyle report
        return extra[bone] + declared_turn_deg(spec, bone, kind=kind)
    # an idle takes only arm_forward_deg from the limp (rs.limp_for_kind), so only that is allowed on it
    limp = rs.limp_for_kind(dict(rs.LIMP_DEFAULTS, **(spec.get("limp") or {})), kind)
    strong_side, lead_side = rs.limp_sides(limp)   # the lead shoulder follows the strong side when the spec names none

    def lp(key):
        return abs(limp.get(key) or 0.0)

    yaw = lp("lead_shoulder_yaw_deg")
    if bone in rs.TRUNK:
        extra = lp("low_shoulder_lean_deg") + lp("strong_lean_deg") if bone == "spine2" else 0.0
        return abs(spec.get("trunk_flex_deg", {}).get(bone, 0.0)) + extra + yaw * rs.LEAD_YAW_SPLIT.get(bone, 0.0)
    if bone in rs.NECK_HEAD:
        return abs(spec.get("neck_head_flex_deg", {}).get(bone, 0.0)) + yaw * rs.LEAD_YAW_COUNTER[bone]
    if bone == "pelvis":
        # a hike in metres is a pelvis roll of up to HIKE_ROLL_MAX_DEG (the rest of the rise is a raise)
        return (abs(spec.get("pelvis_tilt_deg", 0.0)) + lp("weak_hip_hike_deg")
                + (rs.HIKE_ROLL_MAX_DEG if lp("weak_hip_hike_m") else 0.0))
    side_bone = bone[2:] if bone[:2] in ("l_", "r_") else None
    low = bone[:1] == lead_side
    if side_bone == "clavicle":
        return (abs(spec.get("clavicle_drop_deg", 0.0)) + abs(spec.get("clavicle_protract_deg", 0.0))
                + ((lp("low_shoulder_deg") + lp("lead_shoulder_protract_deg")) if low else 0.0)
                + lp("shoulder_roll_deg"))
    if side_bone == "upperarm_twist":
        # hang, the arms-forward turn, and a lead arm that swings more than the source's by its scale
        swing = max(0.0, (limp.get("low_arm_swing_scale") or 1.0) - 1.0) * 25.0 if low else 0.0
        return (abs(spec.get("upperarm_hang_deg", 0.0)) + lp("arm_forward_deg")
                + (lp("low_arm_forward_deg") if low else 0.0) + swing)
    if side_bone == "foretwist":
        return abs(spec.get("elbow_bend_deg", 0.0))
    if side_bone == "foot":   # the weak foot's plantar flexion, and the outer-edge landing roll
        weak = bone[:1] != strong_side
        return lp("foot_outer_edge_deg") + (lp("weak_toe_down_deg") + (30.0 if lp("weak_lift_max_m") else 0.0)
                                            if weak else 0.0)
    if side_bone in ("thigh", "calf"):   # the knee bow turns the IK pole, which turns both
        weak = bone[:1] != strong_side
        # a foot trailing weak_trail_m behind the pelvis swings the thigh back by about that far over a 0.8 m leg;
        # a knee knee_bow_m outside the hip-to-ankle line is a pole turn of about asin(bow / 0.12 m)
        bow = lp("knee_bow_deg") + math.degrees(math.asin(min(1.0, lp("knee_bow_m") / 0.12)))
        trail = math.degrees(lp("weak_trail_m") / 0.8) if weak else 0.0
        # the declared crouch (knee_bend_deg) bends the calf that much further than the source
        return bow + trail + (abs(spec.get("knee_bend_deg", 0.0)) if side_bone == "calf" else 0.0)
    return 0.0


def _clearance_check(entry, posed, spec, positions=None):
    """hand_clearance_m (shield_arm_clearance_m on the shield arm of a with_shield clip): in no frame may a forearm or hand
    come nearer the thighs, calves or torso than that, less 0.5 cm, on the same capsule model the restyle used.
    `positions` is rs.frame_positions of every posed frame, built once by the caller (built here when absent)."""
    positions = positions or [rs.frame_positions(fr) for fr in posed]
    caps = [rs.body_capsules(p) for p in positions]
    rows, ok = {}, True
    for side in ("l", "r"):
        want = rs.clearance_for(entry["clip"], side, spec["hand_clearance_m"], spec.get("shield_arm_clearance_m", 0.0))
        gaps = [rs.arm_gap(rs.arm_points(p, side), c) for p, c in zip(positions, caps)]
        good = min(gaps) >= want - 0.005
        ok = ok and good
        rows[side] = {"clearance_m": want, "min_gap_m": round(min(gaps), 4), "pass": good}
    return {"pass": ok, "arms": rows}


def _metrics_check(entry, src_w, posed, spec, positions=None):
    """The limp's metric targets against the exported clip. Which gates run comes from the MERGED SPEC, never from what
    the report happens to hold, and each is measured on the posed frames: knee_max_deg (no knee straighter),
    min_track_half_m (no ankle nearer than that to the clip's mean pelvis line, entry track_centre_x_m, the same fixed
    reference the restyle clamps against), weak_lift_max_m (the weak ANKLE never higher than the cap over its floor
    outside the weak contact; the toe's height is reported beside it, because a heel-first landing keeps the toe up and
    folding the foot to hide that costs more than it earns) and knee_bow_m (the mean outward offset of the knees, by
    rs.knee_bow_offset, at least half the target: a straight leg has no bow to show). The other targets are shown beside
    the report's achieved values, not gated. A gate whose evidence the report lacks fails; a spec with metric targets
    and a report with no achieved block fails."""
    limp = dict(rs.LIMP_DEFAULTS, **(spec.get("limp") or {}))
    rows, ok = {}, True
    ach = entry.get("achieved")
    if rs.metric_targets_active(limp) and not ach:
        rows["achieved"] = {"error": "the spec sets metric targets but the report has no achieved block", "pass": False}
        ok = False
    ach = ach or {}
    if limp["knee_max_deg"] < 180.0:
        worst = max(math.degrees(rs._knee_angle(fr[th].translation, fr[ca].translation, fr[fo].translation))
                    for fr in posed for th, ca, fo in rt.LEGS)
        good = worst <= limp["knee_max_deg"] + 0.5
        rows["knee_max_deg"] = {"target": limp["knee_max_deg"], "measured": round(worst, 1), "pass": good}
        ok = ok and good
    if limp["min_track_half_m"] > 0.0:
        centre = entry.get("track_centre_x_m")
        if centre is None:
            rows["min_track_half_m"] = {"target": limp["min_track_half_m"], "pass": False,
                                        "error": "the report has no track_centre_x_m to measure from"}
            ok = False
        else:
            half = min(side * (fr[foot].translation.x - centre)
                       for fr in posed for foot, side in (("l_foot", -1.0), ("r_foot", 1.0)))
            good = half >= limp["min_track_half_m"] - 0.005
            rows["min_track_half_m"] = {"target": limp["min_track_half_m"], "measured": round(half, 4),
                                        "centre_x_m": centre, "pass": good}
            ok = ok and good
    if limp["weak_lift_max_m"] > 0.0:
        if not entry.get("contacts") or "strong_foot" not in entry:
            rows["weak_lift_max_m"] = {"target": limp["weak_lift_max_m"], "pass": False,
                                       "error": "the report has no contact intervals"}
            ok = False
        else:
            weak = [f for f in entry["contacts"] if f != entry["strong_foot"]][0]
            toe = weak.replace("foot", "toe0")
            c = entry["contacts"][weak]
            period, length = entry["period_frames"], c["contact_end_frame"] - c["contact_start_frame"]
            floor_a, floor_t = min(fr[weak].translation.z for fr in src_w), min(fr[toe].translation.z for fr in src_w)
            swing = [k for k in range(len(posed)) if (k - c["contact_start_frame"]) % period >= length]
            worst = max(posed[k][weak].translation.z - floor_a for k in swing)
            toe_worst = max(posed[k][toe].translation.z - floor_t for k in swing)
            good = worst <= limp["weak_lift_max_m"] + 0.005
            rows["weak_lift_max_m"] = {"target": limp["weak_lift_max_m"], "measured_ankle": round(worst, 4),
                                       "measured_toe": round(toe_worst, 4), "pass": good}
            ok = ok and good
    if limp["knee_bow_m"] > 0.0:
        positions = positions or [rs.frame_positions(fr) for fr in posed]
        offsets = [rs.knee_bow_offset(p[side + "_thigh"], p[side + "_calf"], p[side + "_foot"], sgn)
                   for p in positions for side, sgn in (("l", -1.0), ("r", 1.0))]
        offsets = [o for o in offsets if o is not None]
        if not offsets:
            rows["knee_bow_m"] = {"target": limp["knee_bow_m"], "pass": False, "error": "no frame with the ankle below the hip"}
            ok = False
        else:
            mean = sum(offsets) / len(offsets)
            good = mean >= 0.5 * limp["knee_bow_m"]
            rows["knee_bow_m"] = {"target": limp["knee_bow_m"], "measured_mean": round(mean, 4),
                                  "measured_min": round(min(offsets), 4), "pass": good}
            ok = ok and good
    for key, got in (("weak_toe_down_deg", "weak_toe_down_deg"), ("weak_hip_hike_m", "weak_hip_rise_m"),
                     ("foot_outer_edge_deg", "foot_outer_edge_deg"), ("weight_over_foot", "pelvis_lateral_travel_m")):
        if limp.get(key) and got in ach:
            rows[key] = {"target": limp[key], "achieved": ach[got], "gated": False}
    return {"pass": ok, "checks": rows}


def _grounded_plateau(frames, foot, closed, period):
    """(core, ends) of a foot's grounded plateau FOUND ON THE POSES, by the restyle's own definitions: the contact (ankle
    within 1.5 cm of its lowest and moving back, the longest run), trimmed to the frames moving back at 85 per cent of the
    median speed or more, as velocity indices. `core` is the plateau without its landing and take-off frame (a plateau of
    4 or more); `ends` are those two frames, judged separately (_end_frame_lateral). None when the foot has no stable
    contact. Nothing the report says about frames is used."""
    pos = [fr[foot].translation for fr in frames]
    ys, zs = [p.y for p in pos], [p.z for p in pos]
    try:
        start, length, _ = rs.longest_stance(rs.contact_indices(zs, ys, closed), period)
    except ValueError:
        return None
    first, count, _ = rs.plateau_window(ys, start, length, period)
    frames = [(first + i) % period for i in range(count)]
    # The frames at each end are the landing and the take-off, where the foot is still arriving or already leaving: a
    # fast approach inside the contact band, or a fast first frame of the swing. The rate and residual gates leave them
    # out (the same trim on both clips) so those gates are about the steady middle; their sideways speed has its own gate.
    trim = TOL["plateau_trim_frames"]
    if len(frames) >= 2 * trim + 2:
        return frames[trim:len(frames) - trim], frames[:trim] + frames[len(frames) - trim:]
    return frames, []


def _end_frame_lateral(posed, src_w, foot, closed, ends, period, positions=None):
    """(worst clip sideways speed over the plateau's landing and take-off frames, the source's reference), cm per frame. The
    reference is the source's largest sideways speed within one frame of the source position the clip's time map plays at
    that output frame (`positions`, rs.output_positions of the report's warp; the same index when absent): the clip and the
    source land a frame or two apart (the restyle's pelvis drop puts a swing foot on the floor sooner, and a timing split
    below the source's moves the weak landing earlier), and reading the source at the clip's own index could lean on an
    unrelated frame. The vanilla run itself steps 1.8 cm sideways on its landing frame, so the gate compares with the source
    and does not ask for zero."""
    if not ends:
        return 0.0, 0.0
    out_v, _ = _velocities(posed, foot, closed)
    src_v, _ = _velocities(src_w, foot, closed)
    worst, ref = 0.0, 0.0
    for k in ends:
        at = positions[k] if positions else float(k)
        base = int(math.floor(at))
        near = {base - 1, base, base + 1, int(math.ceil(at))}
        o = abs(out_v[k].x) * 100.0
        r = max(abs(src_v[j % period].x) for j in near) * 100.0
        if o - r >= worst - ref:
            worst, ref = o, r
    return worst, ref


def _lateral_vertical(frames, foot, closed, idx):
    """The largest sideways and vertical ankle speed (cm per frame) over the given velocity indices."""
    vel, _ = _velocities(frames, foot, closed)
    sel = [vel[k] for k in idx]
    if not sel:
        return 0.0, 0.0
    return max(abs(v.x) for v in sel) * 100.0, max(abs(v.z) for v in sel) * 100.0


def _limp_slide(entry, src_w, posed, closed, stride):
    """The slide check for a limp clip. Each foot's grounded plateau is found INDEPENDENTLY on the exported poses and on the
    source poses (_grounded_plateau), never taken from the report; the report's `frames` are checked against it both ways:
    every found core frame must be claimed (all but TOL plateau_missing_frames) and every claimed frame must lie on the
    found plateau or one of its end frames (`claimed_outside` lists those that do not: a frame the foot stalls, skids or
    lifts on drops out of the found plateau, and would otherwise go unjudged). Over the two core plateaus the planted
    ankle's rate, residual, sideways and vertical speed are gated against the source's; the landing and take-off frame
    of each plateau are gated separately (sideways against the source's through the time map, and backward speed no
    faster than the planted speed times 1 + stride_rel plus 0.5 cm). The module docstring has the details."""
    period = entry["period_frames"]
    speed = entry["planted_speed_m_per_frame"]
    positions = rs.output_positions(period, rs.Warp.from_report(entry["warp"])) if entry.get("warp") else None
    rows, ok, rates, src_rates = {}, True, [], []
    for foot, c in entry["contacts"].items():
        # The producer's frame list is not trusted. Each foot's grounded plateau is found again on the exported poses, and
        # the SOURCE's on the source poses, both by the restyle's definitions (plateau against plateau: the source's
        # whole contact holds heel strike and toe-off and made the limit about twice too loose). The report's `frames`
        # must cover the plateau found here, and every residual below is measured over the plateau found here.
        found_mine = _grounded_plateau(posed, foot, closed, period)
        found_theirs = _grounded_plateau(src_w, foot, closed, period)
        if found_mine is None or found_theirs is None:
            rows[foot] = {"error": "no grounded plateau found on the %s poses" % ("clip's" if found_mine is None else "source's"),
                          "pass": False}
            ok = False
            continue
        (mine, mine_ends), (theirs, _) = found_mine, found_theirs
        end_o, end_s = _end_frame_lateral(posed, src_w, foot, closed, mine_ends, period, positions)
        # an end frame may move back slower than the planted speed (the foot rolls onto the floor), never faster (a skid)
        out_vel, _ = _velocities(posed, foot, closed)
        end_back = max([-out_vel[k].y * 100.0 for k in mine_ends] or [0.0])
        end_back_limit = (speed * (1.0 + TOL["stride_rel"]) + TOL["slide_cm"] / 100.0) * 100.0
        missing = [k for k in mine if k not in set(c["frames"])]
        # and the other way: every claimed frame lies on the plateau found here (core or end frame)
        window = set(mine) | set(mine_ends)
        outside = [k for k in c["frames"] if int(round(k)) % period not in window]
        covered = len(missing) <= TOL["plateau_missing_frames"] and not outside
        # a short stance (the weak foot of a run) can hold only two whole frames
        mo, ro = _contact_stats(posed, foot, closed, mine, min_frames=2)
        ms, rsd = _contact_stats(src_w, foot, closed, theirs, min_frames=2)
        lat_o, vert_o = _lateral_vertical(posed, foot, closed, mine)
        lat_s, vert_s = _lateral_vertical(src_w, foot, closed, theirs)
        zs = [fr[foot].translation.z for fr in posed]
        height = (max(zs[k] for k in mine) - min(zs)) * 100.0
        if mo is None or ms is None:
            rows[foot] = {"error": "too few plateau frames", "pass": False}
            ok = False
            continue
        rel = -mo / speed
        # the claimed frames must be on the ground, not only steady: ankle within 2 cm of the foot's lowest
        good = (covered and ro <= rsd + TOL["slide_cm"] and abs(rel - 1.0) <= TOL["stride_rel"]
                and lat_o <= lat_s + TOL["slide_cm"] and vert_o <= vert_s + TOL["slide_cm"]
                and end_o <= end_s + TOL["end_frame_lateral_cm"] and end_back <= end_back_limit
                and height <= TOL["stance_height_cm"])
        ok = ok and good
        rates.append(-mo)
        src_rates.append(-ms)
        rows[foot] = {"residual_cm": [round(ro, 2), round(rsd, 2)], "rate_over_report": round(rel, 3),
                      "lateral_cm": [round(lat_o, 2), round(lat_s, 2)], "vertical_cm": [round(vert_o, 2), round(vert_s, 2)],
                      "end_frames": mine_ends, "end_frame_lateral_cm": [round(end_o, 2), round(end_s, 2)],
                      "end_frame_back_cm": [round(end_back, 2), round(end_back_limit, 2)], "claimed_outside": outside,
                      "contact_frames": len(c["frames"]), "independent_frames": mine, "frames_missing": len(missing),
                      "stance_height_cm": round(height, 2), "pass": good}
    out = {"feet": rows, "limp_intervals": True}
    if len(rates) == 2:
        strong = entry["strong_foot"]
        weak = [f for f in entry["contacts"] if f != strong][0]
        a_s = entry["contacts"][strong]["contact_start_frame"]
        a_w = entry["contacts"][weak]["contact_start_frame"]
        gap = (a_w - a_s) % period
        rate = sum(rates) / 2.0
        steps = [rate * gap, rate * (period - gap)]
        want = stride * (sum(src_rates) / 2.0) * period
        loop_ok = abs(sum(steps) / want - 1.0) <= TOL["stride_rel"]
        # the contacts found again in the exported clip (the validator's definition, longest run per foot). Only the
        # strong foot's start is gated: the weak foot barely lifts, so its late swing already sits inside the 1.5 cm
        # band and the definition finds its contact early (its error and the measured split are reported)
        found, timing_ok = {}, True
        for foot in (strong, weak):
            ys = [fr[foot].translation.y for fr in posed]
            zs = [fr[foot].translation.z for fr in posed]
            try:
                st, n, _ = rs.longest_stance(rs.contact_indices(zs, ys, closed), period)
            except ValueError:
                found[foot] = None
                timing_ok = timing_ok and foot != strong
                continue
            claimed = entry["contacts"][foot]["contact_start_frame"]
            err = (st - claimed + period / 2.0) % period - period / 2.0
            found[foot] = {"start": st, "length": n, "start_error_frames": round(err, 2)}
            # the band definition fires early on a foot that is merely skimming the floor: an early find is allowed up
            # to TOL early_contact_frames, a late one (the claimed contact not on the ground yet) only up to 2.5
            timing_ok = timing_ok and (foot != strong or -TOL["early_contact_frames"] <= err <= TOL["contact_start_frames"])
        if all(found.values()):
            found["measured_timing_split"] = round(((found[weak]["start"] - found[strong]["start"]) % period) / period, 3)
        out.update({"steps_m": [round(v, 3) for v in steps], "loop_m": round(sum(steps), 3),
                    "want_loop_m": round(want, 3), "loop_pass": loop_ok, "found_contacts": found,
                    "timing_pass": timing_ok})
        ok = ok and loop_ok and timing_ok
    else:
        ok = False
    out["pass"] = ok
    return out


def validate_clip(entry, src, info, eng_names, eng_parents, fbx, spec):
    names, parents, frames, fps = read_fbx(fbx)
    order = eng_names   # the engine dump lists every parent before its children
    s1, s2 = int(round(info["source1"])), int(round(info["source2"]))
    N = s2 - s1 + 1
    posed = frames[1:]
    src_w = src.frames(s1, s2)
    lo = [rs._locals(fr, order, eng_parents) for fr in posed]
    ls = [rs._locals(fr, order, eng_parents) for fr in src_w]
    if entry.get("loop_closure"):
        # the restyle closed an open source first (its last frame did not repeat its first): judge against the same
        ls = rs.close_loop_locals(ls, order, entry["loop_closure"]["frames"])
        src_w = [rs.world_from_locals(lc, order, eng_parents) for lc in ls]
    if entry.get("kind") == "gait" and entry.get("contacts") and entry.get("warp"):
        # a limp clip is the source played through a time map: judge bones, flips and the loop against that
        # warped source, or a fast swing's time shift alone would read as a joint fault (the slide and ground
        # checks keep the unwarped source)
        ls = rs.warp_locals(ls, rs.output_positions(entry["period_frames"], rs.Warp.from_report(entry["warp"])), order)
    res = {}

    res["bones"] = {"pass": sorted(names) == sorted(eng_names) and all(parents.get(n) == eng_parents[n] for n in eng_names),
                    "count": len(names)}
    res["frames"] = {"pass": len(posed) == N and abs(fps - 30.0) < 1e-6, "posed": len(posed), "want": N, "fps": fps}
    res["finite"] = {"pass": _finite(frames)}
    r0 = (entry.get("engine_check") or {}).get("frame0_rest_max_deg")
    res["rest_frame"] = {"pass": r0 is not None and r0 <= TOL["rest_deg"], "max_deg": r0}

    so, ss = _steps(lo, order), _steps(ls, order)
    # a declared swing scale scales the per-frame turn of ITS bones (each arm's own, the pelvis's sway), computed once
    # for the flips (per bone) and the loop check (the largest, as the source's seam kink scales with it)
    bone_scale, amp = rs.gait_scales(spec) if entry.get("kind") == "gait" else ({}, 1.0)
    bad = {t: round(so[t], 1) for t in order if so[t] > ss[t] * bone_scale.get(t, 1.0) + TOL["flip_extra_deg"]}
    res["flips"] = {"pass": not bad, "bones": bad}

    worst = {}
    hc = entry.get("hand_clearance") or {}
    extra_turn = {s + "_upperarm_twist": c["abduct"] + c["back"] for s, c in (hc.get("max_correction_deg") or {}).items()}
    for t in order:
        # a bone the spec turns on purpose (a chin-up head, a hunched spine) may sit that far past the base limit
        lim = (TOL["joint_leg_deg"] if t in LEG_BONES else TOL["joint_deg"]) + declared_turn_deg(spec, t, extra_turn, entry.get("kind", "gait"))
        dev = max(rs._qangle_deg(a[t].to_quaternion(), b[t].to_quaternion()) for a, b in zip(lo, ls))
        if dev > lim:
            worst[t] = round(dev, 1)
    knees = []
    for fr in posed:
        for th, ca, fo in rt.LEGS:
            knees.append(math.degrees(rs._knee_angle(fr[th].translation, fr[ca].translation, fr[fo].translation)))
    res["joints"] = {"pass": not worst and 25.0 <= min(knees) and max(knees) <= 180.0,
                     "over_limit": worst, "knee_min_max_deg": [round(min(knees), 1), round(max(knees), 1)]}

    closed = bool(entry.get("closed_loop"))
    res["loop"] = _loop_check(lo, ls, order, closed, amp)
    if closed:   # the last frame must still repeat the first
        jo = max(rs._qangle_deg(lo[0][t].to_quaternion(), lo[-1][t].to_quaternion()) for t in order)
        js = max(rs._qangle_deg(ls[0][t].to_quaternion(), ls[-1][t].to_quaternion()) for t in order)
        res["loop"]["closed_pose_jump_deg"] = [round(jo, 2), round(js, 2)]
        res["loop"]["pass"] = res["loop"]["pass"] and jo <= js + TOL["loop_deg"]

    if entry.get("kind") == "gait" and entry.get("contacts"):
        if not closed:
            raise RuntimeError("a limp clip must be a closed loop")
        res["slide"] = _limp_slide(entry, src_w, posed, closed, entry.get("stride_scale", 1.0))
    elif entry.get("kind") == "gait":
        rows, ok = {}, True
        for foot in ("l_foot", "r_foot"):
            idx = contact_frames(src_w, foot, closed)
            mo, ro = _contact_stats(posed, foot, closed, idx)
            ms, rsd = _contact_stats(src_w, foot, closed, idx)
            if mo is None or ms is None:
                rows[foot] = "no contact frames found"
                ok = False
                continue
            ratio = mo / ms
            good = ro <= rsd + TOL["slide_cm"] and abs(ratio - entry.get("stride_scale", 1.0)) <= TOL["stride_rel"]
            ok = ok and good
            rows[foot] = {"residual_cm": [round(ro, 2), round(rsd, 2)], "stride_ratio": round(ratio, 3),
                          "contact_frames": len(idx)}
        res["slide"] = {"pass": ok, "feet": rows, "want_stride": entry.get("stride_scale")}
    else:
        def top_speed(fr_list):
            return max(((b[f].translation - a[f].translation).length * 100.0
                        for a, b in zip(fr_list, fr_list[1:]) for f in ("l_foot", "r_foot")), default=0.0)
        so_, ss_ = top_speed(posed), top_speed(src_w)
        res["slide"] = {"pass": so_ <= ss_ + TOL["idle_foot_cm"], "foot_cm_per_frame": [round(so_, 2), round(ss_, 2)]}

    positions = None
    limp = dict(rs.LIMP_DEFAULTS, **(spec.get("limp") or {}))
    if entry.get("kind") == "gait" and rs.metric_targets_active(limp) or rs.hand_clearance_active(spec):
        positions = [rs.frame_positions(fr) for fr in posed]   # once per frame, for both checks
    if entry.get("kind") == "gait" and rs.metric_targets_active(limp):
        res["metrics"] = _metrics_check(entry, src_w, posed, spec, positions)
    if rs.hand_clearance_active(spec):   # every kind: the idles keep their hands clear too
        res["clearance"] = _clearance_check(entry, posed, spec, positions)

    low_o = min(min(fr["l_foot"].translation.z, fr["r_foot"].translation.z, fr["l_toe0"].translation.z,
                    fr["r_toe0"].translation.z) for fr in posed)
    low_s = min(min(fr["l_foot"].translation.z, fr["r_foot"].translation.z, fr["l_toe0"].translation.z,
                    fr["r_toe0"].translation.z) for fr in src_w)
    res["ground"] = {"pass": low_o >= low_s - TOL["ground_m"], "lowest_m": [round(low_o, 4), round(low_s, 4)]}
    return res


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True, help="restyle_human_clip.py --out folder")
    ap.add_argument("--source-dir", required=True)
    ap.add_argument("--engine-skeleton", required=True)
    ap.add_argument("--only", nargs="*", default=None)
    args = ap.parse_args(argv)

    os.makedirs(args.out, exist_ok=True)
    for stale in ("validate_report.json.DONE", "validate_error.log"):   # the previous run's verdict
        if os.path.exists(os.path.join(args.out, stale)):
            os.remove(os.path.join(args.out, stale))
    with open(os.path.join(args.out, "restyle_report.json"), encoding="utf-8") as fh:
        report = json.load(fh)
    with open(os.path.join(args.source_dir, "clips_index.json"), encoding="utf-8-sig") as fh:
        index = json.load(fh)
    with open(args.engine_skeleton, encoding="utf-8-sig") as fh:
        eng = json.load(fh)["bones"]
    eng_names = [b["name"] for b in eng]
    eng_parents = {b["name"]: b["parent"] for b in eng}

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps, bpy.context.scene.render.fps_base = 30, 1.0
    out = {"tolerances": TOL, "clips": {}}
    sources = {}
    for clip, entry in sorted(report["clips"].items()):
        if args.only and clip not in args.only:
            continue
        row = {}
        try:
            if "error" in entry or not entry.get("export", {}).get("ok"):
                raise RuntimeError("the restyle run did not export this clip")
            info = index[clip]
            if info["master"] not in sources:
                sources[info["master"]] = rs.Source(os.path.join(args.source_dir, info["master"] + ".json"))
            row = validate_clip(entry, sources[info["master"]], info, eng_names, eng_parents,
                                os.path.join(args.out, entry["action"] + ".fbx"), rs.spec_for_clip(report["spec"], clip))
            row["pass"] = all(v["pass"] for v in row.values() if isinstance(v, dict))
        except Exception:
            row = {"pass": False, "error": traceback.format_exc()}
        out["clips"][clip] = row
        with open(os.path.join(args.out, "validate_report.json"), "w") as fh:
            json.dump(out, fh, indent=1)
    failed = [c for c, r in out["clips"].items() if not r.get("pass")]
    out["summary"] = {"clips": len(out["clips"]), "passed": len(out["clips"]) - len(failed), "failed": failed}
    with open(os.path.join(args.out, "validate_report.json"), "w") as fh:
        json.dump(out, fh, indent=1)
    with open(os.path.join(args.out, "validate_report.json.DONE"), "w") as fh:
        if not out["clips"]:
            fh.write("fail: no clip validated\n")
        else:
            fh.write(("fail: %d clip(s): %s" % (len(failed), ", ".join(failed)) if failed else
                      "done: %d clip(s) passed" % len(out["clips"])) + "\n")


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        if isinstance(exc, SystemExit) and not exc.code:
            raise
        _argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
        try:
            _out = rs.error_out_dir(_argv)
            os.makedirs(_out, exist_ok=True)
            with open(os.path.join(_out, "validate_error.log"), "w") as fh:
                fh.write(traceback.format_exc())
            with open(os.path.join(_out, "validate_report.json.DONE"), "w") as fh:
                fh.write("fail: see validate_error.log\n")
        except Exception:
            pass
        raise
