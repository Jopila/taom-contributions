"""Restyle vanilla human_skeleton clips (walk, run, stand) into a per-variant gait or idle on the SAME skeleton.

Headless (the launcher DETACHES and prints nothing; read <out>/restyle_report.json and its .DONE twin):
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe -b -P tools/blender/restyle_human_clip.py -- ^
        --source-dir "E:\\LOTRAOMAssets\\orc_locomotion\\src" ^
        --variant tools/blender/locomotion_variants/orc.json ^
        --engine-skeleton tools/blender/human_skeleton_engine.json ^
        --out "E:\\LOTRAOMAssets\\troll_clips_to_import\\orc_locomotion\\orc" ^
        [--preview "E:\\LOTRAOMAssets\\orc_locomotion\\previews\\orc"] ^
        [--body "E:\\LOTRAOMAssets\\human_skeleton_with_male_body.fbx"] [--only walk_forward_1h stand_1h]
    (--only merges its clips into the clips_index.json and restyle_report.json already in --out)

Input: --source-dir is the output of tools/read_anim_keyframes_tpac.ps1 -ByClip: clips_index.json (vanilla clip ->
master, source1, source2) and one keyframe JSON per master. Every clip in the index is restyled, or the --only ones.
Clips named walk_* and run_* are gaits; stand_* are idles. Other names are refused.

Output, per clip: <prefix>mst_<clip>.fbx, a master that holds ONLY that clip's frames (frame 0 = rest, frames
1..N = vanilla source1..source2), so a long idle master (anim_onehanded_guard_idles, 940 frames) is not restyled
whole. Also clips_index.json in the gen_troll_anim_clips.ps1 -CloneByName format (master anim_mst_<clip>, source1 0,
source2 N-1): that tool then names each master <prefix>mst_<clip> and each clip <prefix><clip>, and clones each
clip's flags, priority and usages from its own vanilla clip. Pass -TravelScale <stride_scale> to it.

Method, per frame, on the engine-frame rig (retarget_mannequin_to_human.build_engine_rig):
  1. Source pose: the vanilla master keyed by retarget_mannequin_to_human.key_source_from_json.
  2. Gaits: arm swing (upper arm) and pelvis roll/yaw deviation scaled about their cycle mean; pelvis bob (Z) and
     sway (X) scaled about their mean. A scaled periodic signal stays periodic, so the loop stays closed.
  3. Posture: world turns about each bone head, top-down, each bone turned toward a direction (spine toward +Y,
     clavicle toward -Z then +Y, upper arm toward the midline, forearm toward +Y). Children follow.
  4. Pelvis lowered by the drop that adds knee_bend_deg at the clip's mean knee angle.
  5. Legs: two-bone IK (the retarget tool's _leg_ik math, with the goal given) to the SOURCE ankle path, widened by
     stance_width_m, its forward swing about the pelvis scaled by stride_scale (gaits only), and lowered with the
     pelvis while the foot is off the ground (SWING_LIFT_M). The foot keeps the source foot's world orientation,
     turned toe-out by foot_splay_deg. Contacts keep the vanilla timing and height.
  6. Idles: head yaw, breathing pitch and pelvis weight shift as sine curves whose period is the clip loop.
  7. The whole pose turned 180 deg about world Z (the Kit's root rule, ROOT_YAW_MODE "pose"), keyed, exported by
     retarget_mannequin_to_human.export_clip and re-read by check_export_against_engine.

Speed matching: the engine plays a gait clip at agent speed / BipMovIkUsage.LoopDisplacement. The feet sweep
stride_scale times the vanilla distance, so the clip needs LoopDisplacement = vanilla x stride_scale
(gen_troll_anim_clips.ps1 -TravelScale). Cadence then rises by 1 / stride_scale at the same speed.

Self-check: every clip is also run with the identity spec; the report's identity_max_deg is how far that run lands
from the source (the IK and the yaw only). With --preview, both runs render on --body as a contact sheet:
rows vanilla side, restyle side, vanilla front, restyle front; columns at 0, 25, 50, 75 per cent of the clip.
"""
import argparse
import json
import math
import os
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
CAPS = {"trunk_flex_total_deg": 30.0, "clavicle_deg": 10.0}
# A foot lifted this far (its lowest point, ankle or toe, above its floor) drops by the full pelvis drop.
# With the ankle on the vanilla path, the goblin run's swing knee folded up to 50 deg more than vanilla.
SWING_LIFT_M = 0.10

DEFAULTS = {
    "trunk_flex_deg": {}, "neck_head_flex_deg": {}, "pelvis_tilt_deg": 0.0,
    "clavicle_drop_deg": 0.0, "clavicle_protract_deg": 0.0, "upperarm_hang_deg": 0.0, "elbow_bend_deg": 0.0,
    "arm_swing_scale": 1.0, "knee_bend_deg": 0.0, "stance_width_m": 0.0, "foot_splay_deg": 0.0,
    "stride_scale": 1.0, "bob_scale": 1.0, "sway_scale": 1.0,
    "idle": {"head_yaw_deg": 0.0, "head_cycles": 1, "glance_harmonic": 0.0, "breath_pitch_deg": 0.0,
             "breath_cycles": 1, "weight_shift_m": 0.0, "weight_cycles": 1},
}


def load_spec(path):
    """Variant spec merged over DEFAULTS. Keys that start with '_' are notes."""
    with open(path, encoding="utf-8-sig") as fh:
        raw = {k: v for k, v in json.load(fh).items() if not k.startswith("_")}
    spec = json.loads(json.dumps(DEFAULTS))
    for k, v in raw.items():
        if k == "idle":
            spec["idle"].update({ik: iv for ik, iv in v.items() if not ik.startswith("_")})
        else:
            spec[k] = v
    if not spec.get("clip_prefix"):
        raise SystemExit("variant spec %s has no clip_prefix" % path)
    return spec


def identity_spec(spec):
    out = json.loads(json.dumps(DEFAULTS))
    out["clip_prefix"] = spec["clip_prefix"]
    return out


def cap_violations(spec):
    out = []
    trunk = sum(abs(v) for v in spec["trunk_flex_deg"].values())
    if trunk > CAPS["trunk_flex_total_deg"]:
        out.append("trunk flex %.1f deg over the %.0f cap" % (trunk, CAPS["trunk_flex_total_deg"]))
    for k in ("clavicle_drop_deg", "clavicle_protract_deg"):
        if abs(spec[k]) > CAPS["clavicle_deg"]:
            out.append("%s %.1f over the %.0f cap" % (k, spec[k], CAPS["clavicle_deg"]))
    return out


# ------------------------------------------------------------------ math helpers
def _rot3(m):
    return m.to_3x3().normalized()


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
    M = (q.to_matrix() @ _rot3(Mw[t])).to_4x4()
    M.translation = Mw[t].translation.copy()
    Mw[t] = M


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


def _two_bone_ik(Mw, th, ca, fo, G, a, b, pole_local, stats):
    """Two-bone IK, the math of retarget_mannequin_to_human._leg_ik with the goal G given. The knee bends in the
    plane of its rest pole carried by the thigh; the calf follows the thigh's turn, then aims at the goal."""
    H, K0, A0 = Mw[th].translation.copy(), Mw[ca].translation.copy(), Mw[fo].translation.copy()
    dv = G - H
    d = dv.length
    if d < 1e-6:
        return
    u = dv / d
    dc = min(max(d, abs(a - b) + 1e-4), a + b - 1e-4)
    pole = _rot3(Mw[th]) @ pole_local
    v = pole - u * pole.dot(u)
    if v.length < 1e-6:
        return
    v.normalize()
    alpha = math.acos(max(-1.0, min(1.0, (a * a + dc * dc - b * b) / (2.0 * a * dc))))
    K = H + (u * math.cos(alpha) + v * math.sin(alpha)) * a
    reach = H + u * dc
    q1 = (K0 - H).rotation_difference(K - H)
    q2 = (q1 @ (A0 - K0)).rotation_difference(reach - K)
    M = (q1.to_matrix() @ _rot3(Mw[th])).to_4x4()
    M.translation = H
    Mw[th] = M
    M = (q2.to_matrix() @ q1.to_matrix() @ _rot3(Mw[ca])).to_4x4()
    M.translation = K
    Mw[ca] = M
    stats["max_unreached_m"] = max(stats["max_unreached_m"], (G - reach).length)


# ------------------------------------------------------------------ source sampling
class Source:
    """One vanilla master keyed on its own engine rig; frames read on demand (JSON t is Blender frame t + 1)."""

    def __init__(self, json_path):
        with open(json_path, encoding="utf-8-sig") as fh:
            spec = json.load(fh)
        self.arm = rt.build_engine_rig(json_path, name="src_" + os.path.splitext(os.path.basename(json_path))[0][:40])
        self.action, self.frames = rt.key_source_from_json(self.arm, spec, self.arm.name + "_act")
        self.order = rt._hierarchy_order(self.arm)
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in self.arm.data.bones}

    def world(self, t):
        ad = self.arm.animation_data
        ad.action = self.action
        rt._assign_slot(self.arm)
        bpy.context.scene.frame_set(int(t) + 1)
        return {pb.name: pb.matrix.copy() for pb in self.arm.pose.bones}

    def remove(self):
        act = self.action
        rt._delete_objects([self.arm])
        try:
            bpy.data.actions.remove(act)
        except Exception:
            pass


def _locals(world, order, parent):
    return {t: (world[parent[t]].inverted() @ world[t]) if parent[t] else world[t].copy() for t in order}


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


def restyle_clip(tgt, src, clip, info, spec, kind, action_name):
    """Key `action_name` on tgt.arm: frame 0 rest, frames 1..N the restyled source1..source2. Returns (act, entry)."""
    s1, s2 = int(round(info["source1"])), int(round(info["source2"]))
    worlds = [src.world(t) for t in range(s1, s2 + 1)]
    N = len(worlds)
    order, parent = tgt.order, tgt.parent
    rest_w = tgt.rest_w
    locs = [_locals(w, order, parent) for w in worlds]
    gait = kind == "gait"
    entry = {"clip": clip, "kind": kind, "source": [s1, s2], "n_frames": N}

    # loop shape: a closed loop repeats its first pose on its last frame
    close_deg = _loop_closed(locs[0], locs[-1], order)
    closed = close_deg < 1.0
    period = (N - 1) if closed else N
    entry.update({"source_loop_delta_deg": round(close_deg, 2), "closed_loop": closed, "period_frames": period})

    # 2. deviation scaling (gaits): arm swing, pelvis roll/yaw, pelvis bob and sway
    if gait:
        for side, _ in SIDES:
            b = side + "_upperarm_twist"
            scaled = _scale_rot_dev([lc[b] for lc in locs], spec["arm_swing_scale"])
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
        mean_off[fo] = sum(w[fo].translation.y - w["pelvis"].translation.y for w in worlds) / N
        floor[fo] = min(w[fo].translation.z for w in worlds)
        floor[toe] = min(w[toe].translation.z for w in worlds)

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
            rest_dir[t] = _rot3(rest_w[t]).transposed() @ d.normalized()

    def bdir(Mw, t):
        return (_rot3(Mw[t]) @ rest_dir[t]).normalized()

    idle = spec["idle"]
    act = _new_action(tgt.arm, action_name)
    tgt.clear_pose()
    prev_q = {}
    # frame 0: the rest pose, turned like every posed frame (the Kit zeroes the root position track against it)
    M0 = {t: YAW @ rest_w[t] for t in order}
    _key_pose(tgt, M0, 0, prev_q)
    stats = {"max_unreached_m": 0.0}
    lowest = {"foot": 9.0, "toe": 9.0}
    for k in range(N):
        lc = locs[k]
        w = worlds[k]
        ph = (k / period) if period > 0 else 0.0
        Mw = {}
        for t in order:
            par = parent[t]
            if par is None:
                M = lc[t].copy()
                p = pel[k]
                M.translation = Vector((pmean.x + (p.x - pmean.x) * sx, p.y,
                                        pmean.z + (p.z - pmean.z) * sz - drop))
                if not gait and idle["weight_shift_m"]:
                    M.translation.x += idle["weight_shift_m"] * math.sin(2.0 * math.pi * idle["weight_cycles"] * ph)
                Mw[t] = M
            else:
                Mw[t] = Mw[par] @ lc[t]
            # 3. posture turns, top-down
            if t == "pelvis":
                _turn(Mw, t, _toward(bdir(Mw, t), FWD, spec["pelvis_tilt_deg"]))
            elif t in TRUNK:
                deg = spec["trunk_flex_deg"].get(t, 0.0)
                if not gait and idle["breath_pitch_deg"] and t in ("spine1", "spine2"):
                    deg += 0.5 * idle["breath_pitch_deg"] * math.sin(2.0 * math.pi * idle["breath_cycles"] * ph)
                _turn(Mw, t, _toward(bdir(Mw, t), FWD, deg))
            elif t in NECK_HEAD:
                _turn(Mw, t, _toward(bdir(Mw, t), FWD, spec["neck_head_flex_deg"].get(t, 0.0)))
                if not gait and idle["head_yaw_deg"]:
                    h = idle["glance_harmonic"]
                    c = idle["head_cycles"]
                    yaw = idle["head_yaw_deg"] * (math.sin(2.0 * math.pi * c * ph)
                                                  + h * math.sin(2.0 * math.pi * 3 * c * ph)) / (1.0 + h)
                    _turn(Mw, t, Quaternion(UP, math.radians(yaw * (0.4 if t == "neck" else 0.6))))
            else:
                for side, sgn in SIDES:
                    if t == side + "_clavicle":
                        _turn(Mw, t, _toward(bdir(Mw, t), -UP, spec["clavicle_drop_deg"]))
                        _turn(Mw, t, _toward(bdir(Mw, t), FWD, spec["clavicle_protract_deg"]))
                    elif t == side + "_upperarm_twist":
                        _turn(Mw, t, _toward(bdir(Mw, t), RIGHT * -sgn, spec["upperarm_hang_deg"]))
                    elif t == side + "_foretwist":
                        _turn(Mw, t, _toward(bdir(Mw, t), FWD, spec["elbow_bend_deg"]))
        # 5. legs to the widened, stride-scaled source ankle path; foot keeps the source orientation
        for lg in legs:
            th, ca, fo, toe = lg["t"]
            A = w[fo].translation
            py = w["pelvis"].translation.y
            # a lifted foot drops with the hips; a planted heel or toe keeps the vanilla height
            lift = min(A.z - floor[fo], w[toe].translation.z - floor[toe])
            G = Vector((A.x + lg["side"] * spec["stance_width_m"],
                        py + mean_off[fo] + (A.y - py - mean_off[fo]) * stride,
                        A.z - drop * max(0.0, min(1.0, lift / SWING_LIFT_M))))
            _two_bone_ik(Mw, th, ca, fo, G, lg["a"], lg["b"], lg["pole"], stats)
            splay = Quaternion(UP, math.radians(-lg["side"] * spec["foot_splay_deg"]))
            F = (splay.to_matrix() @ _rot3(w[fo])).to_4x4()
            F.translation = Mw[ca] @ lc[fo].translation
            Mw[fo] = F
            Mw[toe] = Mw[fo] @ lc[toe]
            lowest["foot"] = min(lowest["foot"], Mw[fo].translation.z)
            lowest["toe"] = min(lowest["toe"], Mw[toe].translation.z)
        # 7. the Kit's root rule: the whole pose turned 180 deg about world Z
        _key_pose(tgt, {t: YAW @ Mw[t] for t in order}, k + 1, prev_q)
    entry["ik_max_unreached_m"] = round(stats["max_unreached_m"], 4)
    entry["lowest_head_z_m"] = {kk: round(v, 4) for kk, v in lowest.items()}
    entry["stride_scale"] = stride
    return act, entry


def compare_to_source(tgt, act, src, info):
    """Largest bone-local rotation difference (deg) between `act` (yaw undone) and the source, over all frames."""
    s1 = int(round(info["source1"]))
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
        ls = _locals(src.world(s1 + k - 1), tgt.order, tgt.parent)
        ad.action = act
        rt._assign_slot(tgt.arm)
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
    args = ap.parse_args(argv)

    spec = load_spec(args.variant)
    over = cap_violations(spec)
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
    os.makedirs(args.out, exist_ok=True)
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
    report = {"variant": args.variant, "spec": spec, "cap_violations": over, "clips": {}}
    gen_index = {}
    if args.only:
        # a partial run updates the other clips' index rows and report entries instead of dropping them
        # (gen_troll_anim_clips.ps1 -Verify would otherwise count the dropped clips as another pipeline's)
        for name, target in (("clips_index.json", gen_index), ("restyle_report.json", report["clips"])):
            path = os.path.join(args.out, name)
            if os.path.exists(path):
                with open(path, encoding="utf-8") as fh:
                    data = json.load(fh)
                target.update(data.get("clips", {}) if name == "restyle_report.json" else data)
    sources = {}
    for clip in clips:
        info = index[clip]
        kind = clip_kind(clip)
        action_name = prefix + "mst_" + clip
        entry = {"action": action_name}
        try:
            if kind is None:
                raise RuntimeError("not a walk_, run_ or stand_ clip")
            if len(action_name) > 63 or len(prefix + clip) > 63:
                raise RuntimeError("name over the engine's 63 characters")
            master = info["master"]
            if master not in sources:
                sources[master] = Source(os.path.join(args.source_dir, master + ".json"))
            src = sources[master]
            act, e = restyle_clip(tgt, src, clip, info, spec, kind, action_name)
            entry.update(e)
            ident, _ = restyle_clip(tgt, src, clip, info, identity_spec(spec), kind, action_name + "_identity")
            entry["identity_max_deg"], entry["identity_worst_at"] = compare_to_source(tgt, ident, src, info)
            entry["restyle_max_deg"], entry["restyle_worst_at"] = compare_to_source(tgt, act, src, info)
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
        _out = _argv[_argv.index("--out") + 1] if "--out" in _argv else os.getcwd()
        os.makedirs(_out, exist_ok=True)
        with open(os.path.join(_out, "restyle_error.log"), "w") as fh:
            fh.write(traceback.format_exc())
        with open(os.path.join(_out, "restyle_report.json.DONE"), "w") as fh:
            fh.write("fail: see restyle_error.log\n")
        raise
