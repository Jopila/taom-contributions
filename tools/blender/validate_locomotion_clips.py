"""Validate restyled locomotion clips (restyle_human_clip.py output) against their vanilla sources. Read-only.

Headless (the launcher DETACHES; read <out>/validate_report.json and its .DONE twin):
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe -b -P tools/blender/validate_locomotion_clips.py -- ^
        --out "E:\\LOTRAOMAssets\\troll_clips_to_import\\orc_locomotion\\orc" ^
        --source-dir "E:\\LOTRAOMAssets\\orc_locomotion\\src" ^
        --engine-skeleton tools/blender/human_skeleton_engine.json [--only walk_forward_1h]

Each exported FBX is RE-IMPORTED (what the Kit will read), turned back to face +Y, and compared frame by frame with
its vanilla source (the master JSON keyed on the engine rig). Pass or fail per clip, per check:
  bones        the 28 bone names and parents of the engine skeleton, nothing more or less
  frames       N posed frames after the rest frame, N = source2 - source1 + 1; the scene runs at 30 fps
  finite       no NaN or infinite value in any bone matrix
  rest_frame   frame 0 within 0.5 deg of the engine rest (restyle report's engine check)
  flips        no bone turns more per frame than the source does plus 15 deg (a sign flip turns ~180)
  joints       each bone within 25 deg of the source's local rotation (legs 45, from the IK), knees 25 to 180
  loop         at the loop seam, each bone's step and change of step are each no larger than the larger of the
               clip's interior maximum and the source's seam value times the spec's largest gait deviation scale,
               plus 1 deg; a closed loop's last frame still repeats its first
  slide        gaits: in each foot's contact frames (taken from the SOURCE: ankle within 1.5 cm of its lowest and
               moving back) the ankle moves back at a steady rate (residual no worse than the source's plus 0.5 cm
               per frame) and that rate over the source's is within 0.05 of the stride scale;
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
       "slide_cm": 0.5, "stride_rel": 0.05, "idle_foot_cm": 0.3, "ground_m": 0.01}


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
    vel, pos = _velocities(src_frames, foot, closed)
    zmin = min(p.z for p in pos)
    return [k for k, v in enumerate(vel) if pos[k].z <= zmin + 0.015 and v.y < 0.0]


def _contact_stats(frames, foot, closed, idx):
    """Over the given contact frames: (median back rate, largest residual from it in cm per frame)."""
    vel, _ = _velocities(frames, foot, closed)
    sel = [vel[k] for k in idx]
    if len(sel) < 3:
        return None, None
    med = statistics.median(v.y for v in sel)
    resid = max(math.sqrt((v.y - med) ** 2 + v.x ** 2 + v.z ** 2) for v in sel)
    return med, resid * 100.0


def validate_clip(entry, src, info, eng_names, eng_parents, fbx, spec):
    names, parents, frames, fps = read_fbx(fbx)
    order = eng_names   # the engine dump lists every parent before its children
    s1, s2 = int(round(info["source1"])), int(round(info["source2"]))
    N = s2 - s1 + 1
    posed = frames[1:]
    src_w = [src.world(t) for t in range(s1, s2 + 1)]
    lo = [rs._locals(fr, order, eng_parents) for fr in posed]
    ls = [rs._locals(fr, order, eng_parents) for fr in src_w]
    res = {}

    res["bones"] = {"pass": sorted(names) == sorted(eng_names) and all(parents.get(n) == eng_parents[n] for n in eng_names),
                    "count": len(names)}
    res["frames"] = {"pass": len(posed) == N and abs(fps - 30.0) < 1e-6, "posed": len(posed), "want": N, "fps": fps}
    res["finite"] = {"pass": _finite(frames)}
    r0 = (entry.get("engine_check") or {}).get("frame0_rest_max_deg")
    res["rest_frame"] = {"pass": r0 is not None and r0 <= TOL["rest_deg"], "max_deg": r0}

    so, ss = _steps(lo, order), _steps(ls, order)
    bad = {t: round(so[t], 1) for t in order if so[t] > ss[t] + TOL["flip_extra_deg"]}
    res["flips"] = {"pass": not bad, "bones": bad}

    worst = {}
    for t in order:
        lim = TOL["joint_leg_deg"] if t in LEG_BONES else TOL["joint_deg"]
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
    amp = max(1.0, *(abs(spec.get(k, 1.0)) for k in ("arm_swing_scale", "sway_scale", "bob_scale"))) \
        if entry.get("kind") == "gait" else 1.0
    res["loop"] = _loop_check(lo, ls, order, closed, amp)
    if closed:   # the last frame must still repeat the first
        jo = max(rs._qangle_deg(lo[0][t].to_quaternion(), lo[-1][t].to_quaternion()) for t in order)
        js = max(rs._qangle_deg(ls[0][t].to_quaternion(), ls[-1][t].to_quaternion()) for t in order)
        res["loop"]["closed_pose_jump_deg"] = [round(jo, 2), round(js, 2)]
        res["loop"]["pass"] = res["loop"]["pass"] and jo <= js + TOL["loop_deg"]

    if entry.get("kind") == "gait":
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
                                os.path.join(args.out, entry["action"] + ".fbx"), report["spec"])
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
        _out = _argv[_argv.index("--out") + 1] if "--out" in _argv else os.getcwd()
        with open(os.path.join(_out, "validate_error.log"), "w") as fh:
            fh.write(traceback.format_exc())
        with open(os.path.join(_out, "validate_report.json.DONE"), "w") as fh:
            fh.write("fail: see validate_error.log\n")
        raise
