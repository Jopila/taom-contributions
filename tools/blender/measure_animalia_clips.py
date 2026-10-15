"""Measure Animalia (Fab) source clips for their Kit clip metadata (#646): travel per loop, hoof plants, fall time.

Headless (the Store launcher DETACHES: completion is the --out JSON plus its .DONE twin):
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe -b -P tools/blender/measure_animalia_clips.py -- ^
        --template "C:\\Users\\mikew\\Downloads\\horse.fbx" --map tools/blender/animalia_to_horse_map.json ^
        --clips "E:\\LOTRAOMAssets\\_export\\animalia_elk\\anims\\Animations" ^
        --gaits Loco_Walk Loco_Trot Loco_Run Loco_Sprint Loco_WalkBack --deaths Death_Stand_L Death_Stand_R ^
        --out tools/blender/animalia_elk_clip_measure.json [--profile moose]

Why: a horse gait clip carries a QuadMovementUsage whose LoopDisplacement is the ground the animal covers in one
loop (vanilla horse_walkfast 1.52 m in 0.8 s, anim_horse_trot_2 2.9 m, horse_canterfast 4.0 m), and StepPoints
at its hoof plants (make_walk_sound). The retarget dropped root motion (the UE root is the armature OBJECT), so
the travel comes back here as metadata, the way the troll's did (fab_cave_troll_clip_measure.json).

Frames: the retarget keys source frames f0..f1 as master frames 1..n (n = f1 - f0 + 1, frame 0 = rest) and a
clip plays Source1 = 1 .. Source2 = master Duration - 1, so ONE LOOP = n - 1 frames starting at f0.
  travel      horizontal root (armature object) speed over f0..f1, times the loop length, times the reskin's
              fit scale (the mesh was scaled to horse size by the same global fit), metres per loop. A clip that
              plays in place (root travel under IN_PLACE_M, every WildMesh3D gait, #794) has no root speed, so its
              speed is the median horizontal speed of a planted hoof instead: in place, a hoof on the ground
              slides back at the ground speed the animal would cover ("travel_mode": "stride").
  plants      per hoof (the map's 'measure' hooves, else the Animalia ankle joints), the first frame of its
              stance (a ground contact: height within its minimum + 2 cm) as a fraction of the loop, in [0, 1).
              A brief scuff before the stance is not a plant: only contacts at least half as long as the hoof's
              longest count (plant_start)
  fall        deaths: the fraction of the clip at which the pelvis first comes within 5 cm of its final height
The bone names come from the map's optional "measure": {"pelvis": ..., "hooves": {"LF", "RF", "LB", "RB"}}.
"""
import argparse
import json
import math
import os
import sys
import traceback

import bpy

TOOLS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOLS)
import reskin_animalia_to_horse as rk  # noqa: E402

DEFAULT_MEASURE = {"pelvis": "RigPelvis",
                   "hooves": {"LF": "RigLFLegAnkle", "RF": "RigRFLegAnkle", "LB": "RigLBLegAnkle", "RB": "RigRBLegAnkle"}}
IN_PLACE_M = 0.05   # root travel below this over the whole clip: the clip plays in place
CONTACT_M = 0.02    # a hoof is on the ground within this of its lowest point in the loop


def on_ground(zs, loop):
    """Per frame: is the hoof within CONTACT_M of its lowest point over the loop frames (0..loop-1)."""
    lo = min(zs[:loop]) + CONTACT_M
    return [z <= lo for z in zs]


def plant_start(zs, loop):
    """The loop fraction at which a hoof's stance starts, else None.

    Frames 0..loop-1 are a circle (frame `loop` repeats frame 0), so a contact that starts on frame 0 or runs across
    the wrap is one contact. A toe scuff of a few frames before the stance would win a first-crossing rule (boar
    WalkMedium_F RF 0.6 for a stance at 0.9714), so only contacts at least half as long as this hoof's longest count;
    the earliest of those starts. None when the hoof is always on the ground (or never is): no plant to place."""
    ground = on_ground(zs, loop)[:loop]
    if all(ground) or not any(ground):
        return None
    first_air = ground.index(False)
    contacts = []   # (start frame, length), walking the circle from just after an airborne frame
    start, length = None, 0
    for k in range(1, loop + 1):
        i = (first_air + k) % loop
        if ground[i]:
            if length == 0:
                start = i
            length += 1
        elif length:
            contacts.append((start, length))
            length = 0
    longest = max(n for _, n in contacts)
    return round(min(s for s, n in contacts if 2 * n >= longest) / loop, 4)


def stride_speed(pos, loop):
    """Median horizontal speed per frame of a hoof while it stays within CONTACT_M of its lowest point."""
    speeds = []
    for ps in pos.values():
        ground = on_ground([p.z for p in ps], loop)
        for i in range(1, loop + 1):
            if ground[i] and ground[i - 1]:
                dv = ps[i] - ps[i - 1]
                speeds.append(math.hypot(dv.x, dv.y))
    speeds.sort()
    return speeds[len(speeds) // 2] if speeds else 0.0


def _frame_range(arm):
    ad = arm.animation_data
    if ad and ad.action and getattr(ad, "action_slot", None) is None:
        for s in ad.action.slots:
            ad.action_slot = s
            break
    fr = ad.action.frame_range
    return int(round(fr[0])), int(round(fr[1]))


def _world_head(arm, bone):
    return (arm.matrix_world @ arm.pose.bones[bone].matrix).translation.copy()


def measure(path, template_heads, spec, is_death):
    _, arms, meshes = rk._import_fbx(path)
    arm = arms[0]
    scn = bpy.context.scene
    f0, f1 = _frame_range(arm)
    scn.frame_set(f0)
    S = arm.matrix_world.to_scale()
    heads = {b.name: (rk.Matrix.Diagonal((S.x, S.y, S.z, 1.0)) @ b.matrix_local).translation.copy() for b in arm.data.bones}
    _, fit = rk.global_fit(heads, template_heads, spec["joints"])
    n = f1 - f0 + 1
    loop = n - 1
    names = dict(DEFAULT_MEASURE, **spec.get("measure", {}))
    hooves = names["hooves"]
    root, pos = [], {k: [] for k in hooves}
    pelvis_z = []
    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        root.append(arm.matrix_world.translation.copy())
        for k, b in hooves.items():
            pos[k].append(_world_head(arm, b))
        pelvis_z.append(_world_head(arm, names["pelvis"]).z)
    z = {k: [p.z for p in ps] for k, ps in pos.items()}
    d = root[-1] - root[0]
    d.z = 0.0
    if d.length >= IN_PLACE_M:
        speed_per_frame, mode = d.length / max(1, f1 - f0), "root"
    else:
        speed_per_frame, mode = stride_speed(pos, loop), "stride"
    out = {"frames": [f0, f1], "n": n, "loop_frames": loop, "fit_scale": fit["scale"], "travel_mode": mode,
           "fps": scn.render.fps / scn.render.fps_base,
           "travel_source_m": round(d.length, 4) if mode == "root" else round(speed_per_frame * loop, 4),
           "loop_displacement_m": round(speed_per_frame * loop * fit["scale"], 4),
           "speed_mps_at_30fps": round(speed_per_frame * 30.0 * fit["scale"], 3)}
    plants = {}
    for k, zs in z.items():
        plants[k] = plant_start(zs, loop)
    out["plants"] = plants
    out["plants_sorted"] = sorted(v for v in plants.values() if v is not None)
    if is_death:
        final = pelvis_z[-1]
        hit = next((i for i, v in enumerate(pelvis_z) if v <= final + 0.05), len(pelvis_z) - 1)
        out["fall_fraction"] = round(hit / max(1, loop), 4)
        out["pelvis_z_start_end"] = [round(pelvis_z[0], 3), round(final, 3)]
    for o in arms + meshes:
        bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--template", required=True)
    ap.add_argument("--map", required=True)
    ap.add_argument("--clips", required=True)
    ap.add_argument("--gaits", nargs="*", default=[])
    ap.add_argument("--deaths", nargs="*", default=[])
    ap.add_argument("--profile")
    ap.add_argument("--out", required=True)
    args = ap.parse_args(argv)
    report = {"_comment": "tools/blender/measure_animalia_clips.py; keys are the pack's clip stems", "clips": {}}
    try:
        with open(args.map) as fh:
            spec = json.load(fh)
        if args.profile:
            spec["chains"] = spec["profiles"][args.profile]["chains"]
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.scene.render.fps = 30
        _, tarms, tmeshes = rk._import_fbx(args.template)
        template_heads = rk._heads(tarms[0])
        for o in tarms + tmeshes:
            bpy.data.objects.remove(o, do_unlink=True)
        for stem in args.gaits + args.deaths:
            try:
                report["clips"][stem] = measure(os.path.join(args.clips, stem + ".fbx"), template_heads, spec,
                                                stem in args.deaths)
            except Exception:
                report["clips"][stem] = {"error": traceback.format_exc()}
    except Exception:
        report["error"] = traceback.format_exc()
    # A clip that failed is kept as {"error": ...} so the rest still measure, but the launcher detaches and .DONE is
    # the only completion signal: it must say "error" for a failed clip too, not only for a failed run.
    failed = [stem for stem, clip in report["clips"].items() if "error" in clip]
    if failed:
        report["failed"] = failed
    with open(args.out, "w") as fh:
        json.dump(report, fh, indent=1)
    with open(args.out + ".DONE", "w") as fh:
        fh.write("error" if "error" in report or failed else "ok")


if __name__ == "__main__":
    main()
