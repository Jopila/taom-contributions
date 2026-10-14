"""Export a rigged animal pack shipped as .blend files into the FBX layout the horse-skeleton tools read.

Headless (the Store launcher DETACHES: completion is <out>/export_report.json plus its .DONE twin):
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe -b --factory-startup -P tools/blender/export_blend_pack.py -- ^
        --pack boar="E:\\LOTRAOMAssets\\100 MODELS - WildMesh3D\\...\\Boar\\SK_Boar.blend" [--pack deer=... ...] ^
        --out "E:\\LOTRAOMAssets\\_export\\wildmesh" [--clips-regex REGEX] [--no-clips] [--fps 30]

Writes:
  <out>/meshes/<key>_<part>.fbx      one armature + one mesh each: the --src of reskin_animalia_to_horse.py
  <out>/clips/<key>/<action>.fbx     the rig, the body mesh and one baked take: the --clips of
                                     retarget_animalia_to_horse.py (the mesh is its side-by-side reference)

Why this exists: the Animalia elk and moose (#646) came out of Unreal through ue_export_cave_troll.py. The
WildMesh3D pack ships each animal as one .blend holding the rig, the meshes and every clip, so stage 1 of
docs/ai-includes/quadruped-pack-to-horse-skeleton-workflow.md becomes this script. The source file is opened,
never saved.

What it changes on the way out, and why:
  1. Bone-parented parts (antlers, tusks) carry no vertex groups, and the reskin moves an unweighted vertex
     with the global fit only, so a tusk would not follow the head. Each one is baked to world space in the
     REST pose (a bone-parented matrix_world follows the current pose), its normals flipped when the bake
     mirrors it (the pack has scale -1 and -10 parts), and it gets one group, its parent bone, at weight 1.
  2. The armature keeps its object scale (0.01 or 0.012): applying it would leave every location key in
     centimetres. The retarget reads the source rig through its scale only, so a rotated armature object is
     refused rather than exported.
  3. Clips are resampled to --fps (30) before export. The pack runs at 24 or 25 fps, and everything downstream
     counts 30: gen_animalia_anim_clips.ps1 sets a clip's Duration to its frames / 30, and the Animalia clips
     the Kit took were 30 fps. Each new frame samples the source at its own time (subframes), so a clip keeps
     its length in seconds. Then one key per frame, no simplification.
"""
import argparse
import json
import math
import os
import re
import sys
import traceback

import bpy
from mathutils import Matrix

EXPORT_COMMON = dict(use_selection=True, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False,
                     use_mesh_modifiers=False, path_mode="AUTO")


def _safe(name):
    return re.sub(r"[^A-Za-z0-9_]+", "_", name).strip("_")


def _select_only(objs, active):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.hide_set(False)
        o.hide_viewport = False
        o.select_set(True)
    bpy.context.view_layer.objects.active = active


def _heads(arm):
    return {b.name: (arm.matrix_world @ b.matrix_local).translation.copy() for b in arm.data.bones}


def _assign(arm, act):
    ad = arm.animation_data or arm.animation_data_create()
    ad.action = act
    if act is not None and getattr(ad, "action_slot", True) is None:
        for s in act.slots:
            ad.action_slot = s
            break


def resample(arm, act, s, e, src_fps, dst_fps):
    """Bake `act` (frames s..e at src_fps) into a new action keyed at frames 1..n at dst_fps. Returns (action, n)."""
    scn = bpy.context.scene
    n = int(round((e - s) * dst_fps / src_fps)) + 1
    _assign(arm, act)
    bones = [pb.name for pb in arm.pose.bones]
    samples = {b: ([], [], []) for b in bones}
    prev = {}
    for i in range(n):
        t = min(s + i * src_fps / dst_fps, float(e))
        fi = int(math.floor(t + 1e-6))
        scn.frame_set(fi, subframe=max(0.0, t - fi))
        for pb in arm.pose.bones:
            loc, rot, sca = pb.matrix_basis.decompose()
            if pb.name in prev and prev[pb.name].dot(rot) < 0.0:
                rot.negate()
            prev[pb.name] = rot
            samples[pb.name][0].append(loc)
            samples[pb.name][1].append(rot)
            samples[pb.name][2].append(sca)
    new = bpy.data.actions.new(act.name + "__%dfps" % dst_fps)
    _assign(arm, new)
    for b in bones:
        pb = arm.pose.bones[b]
        pb.rotation_mode = "QUATERNION"
        # one ordinary key creates the slot, layer and channels; the rest are written in bulk
        for path in ("location", "rotation_quaternion", "scale"):
            pb.keyframe_insert(path, frame=1)
        for path, vals in zip(("location", "rotation_quaternion", "scale"), samples[b]):
            for k in range(len(vals[0])):
                fc = new.fcurve_ensure_for_datablock(arm, 'pose.bones["%s"].%s' % (b, path), index=k)
                fc.keyframe_points.clear()
                fc.keyframe_points.add(n)
                fc.keyframe_points.foreach_set("co", [c for i, v in enumerate(vals) for c in (1.0 + i, v[k])])
                fc.update()
    return new, n


def bind_bone_parented(ob, arm):
    """Turn a bone-parented rigid part into a skinned mesh: weight 1 on its parent bone, rest-pose placement."""
    bone = ob.parent_bone
    mw = ob.matrix_world.copy()
    if ob.data.users > 1:
        ob.data = ob.data.copy()
    ob.parent = None
    ob.data.transform(mw)
    mirrored = mw.determinant() < 0.0
    if mirrored:
        ob.data.flip_normals()
    ob.matrix_world = Matrix.Identity(4)
    for vg in list(ob.vertex_groups):
        ob.vertex_groups.remove(vg)
    vg = ob.vertex_groups.new(name=bone)
    vg.add([v.index for v in ob.data.vertices], 1.0, "REPLACE")
    ob.parent = arm
    ob.matrix_parent_inverse = arm.matrix_world.inverted()
    md = ob.modifiers.new("Armature", "ARMATURE")
    md.object = arm
    return {"bone": bone, "mirrored": mirrored, "verts": len(ob.data.vertices)}


def export_pack(key, blend, out, clips_regex, do_clips, entry, dst_fps):
    bpy.ops.wm.open_mainfile(filepath=blend, load_ui=False)
    scn = bpy.context.scene
    arm = max((o for o in scn.objects if o.type == "ARMATURE"), key=lambda o: len(o.data.bones))
    if arm.matrix_world.to_quaternion().angle > 1e-5 or arm.parent is not None:
        raise RuntimeError("armature %s is rotated or parented; the retarget reads the rig through its scale only"
                           % arm.name)
    entry.update({"blend": blend, "armature": arm.name, "bones": len(arm.data.bones), "fps": scn.render.fps,
                  "scale": round(arm.scale[0], 5), "parts": {}, "clips": {}})
    _assign(arm, None)
    arm.data.pose_position = "REST"
    bpy.context.view_layer.update()
    meshes = [o for o in scn.objects if o.type == "MESH" and o.parent is arm]
    bound = {}
    for ob in meshes:
        if ob.parent_type == "BONE":
            bound[ob.name] = bind_bone_parented(ob, arm)
    bpy.context.view_layer.update()
    skinned = [o for o in meshes if any(m.type == "ARMATURE" and m.object is arm for m in o.modifiers)]
    body = max(skinned, key=lambda o: len(o.data.vertices))
    os.makedirs(os.path.join(out, "meshes"), exist_ok=True)
    for ob in meshes:
        part = "%s_%s" % (key, _safe(ob.name))
        path = os.path.join(out, "meshes", part + ".fbx")
        _select_only([arm, ob], arm)
        bpy.ops.export_scene.fbx(filepath=path, bake_anim=False, **EXPORT_COMMON)
        entry["parts"][part] = {"object": ob.name, "role": "body" if ob is body else "part",
                                "materials": [s.material.name if s.material else None for s in ob.material_slots],
                                "verts": len(ob.data.vertices), "bone_parented": bound.get(ob.name),
                                "path": path}
    entry["rest_heads"] = {n: list(v) for n, v in _heads(arm).items()}
    if do_clips:
        arm.data.pose_position = "POSE"
        cdir = os.path.join(out, "clips", key)
        os.makedirs(cdir, exist_ok=True)
        src_fps = scn.render.fps / scn.render.fps_base
        modes = {pb.name: pb.rotation_mode for pb in arm.pose.bones}
        for act in sorted(bpy.data.actions, key=lambda a: a.name):
            if not re.search(clips_regex, act.name) or act.name.endswith("fps"):
                continue
            s, e = (int(round(v)) for v in act.frame_range)
            scn.render.fps, scn.render.fps_base = int(round(src_fps)), 1.0
            baked, n = resample(arm, act, s, e, src_fps, dst_fps)
            scn.render.fps, scn.render.fps_base = dst_fps, 1.0
            scn.frame_start, scn.frame_end = 1, n
            scn.frame_set(1)
            path = os.path.join(cdir, _safe(act.name) + ".fbx")
            _select_only([arm, body], arm)
            bpy.ops.export_scene.fbx(filepath=path, bake_anim=True, bake_anim_use_all_actions=False,
                                     bake_anim_use_nla_strips=False, bake_anim_use_all_bones=True,
                                     bake_anim_force_startend_keying=True, bake_anim_step=1.0,
                                     bake_anim_simplify_factor=0.0, **EXPORT_COMMON)
            entry["clips"][_safe(act.name)] = {"action": act.name, "source_frames": [s, e], "source_fps": src_fps,
                                               "frames": [1, n], "fps": dst_fps,
                                               "seconds": round((e - s) / src_fps, 3), "path": path}
            _assign(arm, None)
            bpy.data.actions.remove(baked)
            for pb in arm.pose.bones:
                pb.rotation_mode = modes[pb.name]
        scn.render.fps, scn.render.fps_base = int(round(src_fps)), 1.0
    return body


def roundtrip_check(entry):
    """Re-import the body part and one clip: bone heads must land where the .blend had them."""
    want = entry["rest_heads"]
    checks = {}
    body = [p for p, d in entry["parts"].items() if d["role"] == "body"][0]
    targets = [("body", entry["parts"][body]["path"])]
    if entry["clips"]:
        targets.append(("clip", next(iter(entry["clips"].values()))["path"]))
    for label, path in targets:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        pre = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
        new = [o for o in bpy.data.objects if o not in pre]
        arms = [o for o in new if o.type == "ARMATURE"]
        got = _heads(arms[0])
        drift = max(((got[n] - type(got[n])(want[n])).length for n in want if n in got), default=None)
        ad = arms[0].animation_data
        scn = bpy.context.scene
        checks[label] = {"armatures": len(arms), "meshes": len([o for o in new if o.type == "MESH"]),
                         "bones": len(got), "missing_bones": [n for n in want if n not in got][:10],
                         "head_drift_max_m": round(drift, 5) if drift is not None else None,
                         "has_action": bool(ad and ad.action),
                         "fps_after_import": scn.render.fps / scn.render.fps_base,
                         "frame_range": list(ad.action.frame_range) if ad and ad.action else None}
    return checks


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--pack", action="append", required=True, help="key=path/to/file.blend")
    ap.add_argument("--out", required=True)
    ap.add_argument("--clips-regex", default=".")
    ap.add_argument("--no-clips", action="store_true")
    ap.add_argument("--fps", type=int, default=30, help="clip frame rate after the resample (the pipeline's 30)")
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    out_json = os.path.join(args.out, "export_report.json")
    report = {"args": vars(args), "packs": {}}
    for spec in args.pack:
        key, blend = spec.split("=", 1)
        entry = {}
        try:
            export_pack(key, blend, args.out, args.clips_regex, not args.no_clips, entry, args.fps)
            entry["roundtrip"] = roundtrip_check(entry)
        except Exception:
            entry["error"] = traceback.format_exc()
        entry.pop("rest_heads", None)
        report["packs"][key] = entry
        with open(out_json, "w") as fh:
            json.dump(report, fh, indent=1, default=str)
    failed = [k for k, e in report["packs"].items() if "error" in e]
    with open(out_json, "w") as fh:
        json.dump(report, fh, indent=1, default=str)
    with open(out_json + ".DONE", "w") as fh:
        fh.write("error: " + ", ".join(failed) if failed else "ok")


if __name__ == "__main__":
    main()
