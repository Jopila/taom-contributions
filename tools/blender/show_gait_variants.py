"""Open a live Blender window that loops restyled gait clips side by side on the male body, for a visual review.

GUI only, never -b (the window is the point):
    %LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe --factory-startup -P tools/blender/show_gait_variants.py -- ^
        --column "Vanilla=<folder>" --column "Orc A=E:\\LOTRAOMAssets\\troll_clips_to_import\\orc_locomotion\\orc" ^
        --column "Orc B=...\\orc_b" --clip walk_forward_1h --clip run_forward_1h ^
        [--body "E:\\LOTRAOMAssets\\human_skeleton_with_male_body.fbx"]

Each --column is LABEL=FOLDER, a restyle_human_clip.py --out folder holding <prefix>mst_<clip>.fbx. A vanilla column comes
from the same tool run with a spec that sets only clip_prefix (every value at DEFAULTS, the identity restyle).
Everything stands in one line along X: one group per --clip, the columns side by side inside it, an empty slot
between groups. Every character faces -Y as exported for the Kit.

Each clip loops on its own: the rest frame 0 is dropped and every F-curve gets a Cycles modifier, so a 36-frame walk
and a 24-frame run stay in their own period. The scene range is a common multiple of both, so the wrap is seamless.
It writes nothing to disk except the optional --status line and --snapshot still (the smoke-run outputs). The
viewport looks through a review camera placed on the side the characters face.

Weapons (optional): --props sword,shield attaches those kinds from gait_props.json to every character, parented to the
engine rig's hand or forearm bone so they follow the clip. --props auto (or --props-per-clip) picks per clip: a clip
named *with_shield* gets sword + shield, any other *1h* clip the sword only, the rest nothing. The kinds, their FBX,
bone, rotation and offset are data in gait_props.json, with a note on how each transform was derived. For a close look
at one character, alone in view: --focus N (0-based, in placement order), --view front|side|side_left (his right or
left), --snapshot-frame F.
"""
import argparse
import glob
import json
import math
import os
import sys

import bpy
from mathutils import Euler, Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import retarget_mannequin_to_human as rt  # noqa: E402
import restyle_human_clip as rs  # noqa: E402

COLUMN_SPACING_M = 1.3
SCENE_FRAMES = 360  # a multiple of the walk (36) and run (24) periods


def _args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--column", action="append", required=True, help="LABEL=FOLDER")
    ap.add_argument("--clip", action="append", required=True, help="clip name, e.g. walk_forward_1h")
    ap.add_argument("--body", default=r"E:\LOTRAOMAssets\human_skeleton_with_male_body.fbx")
    ap.add_argument("--status", help="optional file that receives one line, 'placed N of M' or the error (the launcher "
                                     "detaches, so this is the only way to read the result of a -b smoke run)")
    ap.add_argument("--snapshot", help="optional PNG rendered through the review camera at frame 10")
    ap.add_argument("--props", help="comma list of kinds from the props file (sword,shield,axe,mace,spear,bow...), or 'auto'")
    ap.add_argument("--props-per-clip", action="store_true", help="same as --props auto")
    ap.add_argument("--props-file",
                    default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "gait_props.json"))
    ap.add_argument("--focus", type=int, help="aim the review camera at this one character (0-based) instead of the line")
    ap.add_argument("--view", choices=("front", "side", "side_left"), default="front",
                    help="with --focus: camera in front, or at the character's right (side) or left (side_left)")
    ap.add_argument("--snapshot-frame", type=int, default=10, help="frame the --snapshot is rendered at")
    ap.add_argument("--engine-skeleton",
                    default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "human_skeleton_engine.json"))
    return ap.parse_args(argv)


def _fcurves(act):
    out = []
    for layer in act.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                out.extend(bag.fcurves)
    return out


def _loop_without_rest_frame(act):
    for fc in _fcurves(act):
        pts = fc.keyframe_points
        for i in reversed(range(len(pts))):
            if pts[i].co.x < 0.5:
                pts.remove(pts[i])
        if not any(m.type == "CYCLES" for m in fc.modifiers):
            fc.modifiers.new("CYCLES")


def _find_fbx(folder, clip):
    hits = [p for p in glob.glob(os.path.join(folder, "*mst_" + clip + ".fbx"))
            if os.path.basename(p).split("mst_", 1)[1] == clip + ".fbx"]
    return hits[0] if len(hits) == 1 else None


def _label(text, location, facing):
    """A text that reads correctly from the side the characters face (facing -1 = -Y, +1 = +Y)."""
    curve = bpy.data.curves.new("label_" + text, type="FONT")
    curve.body = text
    # a long clip name (run_forward_1h_with_shield_left_stance) must fit the 1.3 m slot
    longest = max(len(line) for line in text.splitlines())
    curve.size = min(0.14, 1.25 / max(1, longest) * 1.6)
    curve.align_x = "CENTER"
    obj = bpy.data.objects.new("label_" + text, curve)
    obj.location = location
    obj.rotation_euler = Euler((math.radians(90.0), 0.0, 0.0 if facing < 0 else math.pi))
    bpy.context.scene.collection.objects.link(obj)


def _facing(arm):
    """-1 when the character faces -Y at frame 1 (the toe ahead of the ankle along -Y), else +1."""
    bpy.context.scene.frame_set(1)
    toe = arm.matrix_world @ arm.pose.bones["l_toe0"].head
    ankle = arm.matrix_world @ arm.pose.bones["l_foot"].head
    return -1 if toe.y < ankle.y else 1


def _auto_kinds(clip):
    """Props for --props auto: shield clips carry sword + shield, other one-handed clips the sword, the rest nothing."""
    if "with_shield" in clip:
        return ["sword", "shield"]
    return ["sword"] if "1h" in clip else []


def _load_kind(kind, spec, root):
    """Import a kind's FBX once and return [(mesh data, base matrix)] with the grip at the origin and the weapon's long
    axis along Z. A crafted weapon (several pieces, each centred on its own origin in the FBX) is stacked along Z in the
    listed order, butt first (overlap_m pulls a named piece back down into the one below, for a head that straddles
    its haft); a single-mesh item (shield, bow) keeps its authored placement."""
    fbx = os.path.join(root, spec["fbx"])
    objs, _ = rt._import_fbx(fbx)
    wanted = {o.name: o for o in objs if o.type == "MESH"}
    missing = [m for m in spec["meshes"] if m not in wanted]
    if missing:
        raise RuntimeError("props kind %s: no mesh %s in %s" % (kind, missing, fbx))
    pieces, z, grip = [], 0.0, None
    for name in spec["meshes"]:
        obj = wanted[name]
        base = obj.matrix_world.copy()
        zs = [(base @ v.co).z for v in obj.data.vertices]
        if spec.get("stack"):
            shift = z - min(zs) - spec.get("overlap_m", {}).get(name, 0.0)
            if grip is None and "handle" in name:
                grip = shift + (min(zs) + max(zs)) / 2.0
            z = shift + max(zs)
            base = Matrix.Translation((0.0, 0.0, shift)) @ base
        pieces.append((obj.data, base))
    if spec.get("stack"):
        grip = spec["grip_m"] if "grip_m" in spec else (grip or 0.0)
        pieces = [(me, Matrix.Translation((0.0, 0.0, -grip)) @ base) for me, base in pieces]
    mat = bpy.data.materials.new("gait_prop_" + kind)
    mat.diffuse_color = tuple(spec.get("tint", (0.55, 0.55, 0.6))) + (1.0,)
    for me, _ in pieces:
        me.use_fake_user = True
        me.materials.clear()
        me.materials.append(mat)
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    return pieces


def _attach_props(eng, kinds, props):
    """Parent each kind to its bone of the engine rig. The bone-parent matrix sits at the bone's tail, so the inverse
    moves it back to the head: the JSON offset and rotation are then in the bone's own frame, head as origin."""
    for kind in kinds:
        spec, pieces = props["kinds"][kind], props["loaded"][kind]
        bone = eng.data.bones[spec["bone"]]
        holder = bpy.data.objects.new("prop %s | %s" % (kind, eng.name), None)
        bpy.context.scene.collection.objects.link(holder)
        holder.parent, holder.parent_type, holder.parent_bone = eng, "BONE", spec["bone"]
        holder.matrix_parent_inverse = Matrix.Translation((0.0, -bone.length, 0.0))
        rot = Euler([math.radians(a) for a in spec["rotation_deg"]], "XYZ").to_matrix().to_4x4()
        holder.matrix_basis = Matrix.Translation(Vector(spec["offset_m"])) @ rot
        for i, (me, base) in enumerate(pieces):
            piece = bpy.data.objects.new("prop %s %d | %s" % (kind, i, eng.name), me)
            bpy.context.scene.collection.objects.link(piece)
            piece.parent = holder
            piece.matrix_basis = base


def _clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def _place(folder, label, clip, x, y, body, kinds=()):
    fbx = _find_fbx(folder, clip)
    if fbx is None:
        print("[show_gait_variants] no single *mst_%s.fbx in %s; column %s skipped" % (clip, folder, label))
        return False
    objs, arm = rt._import_fbx(fbx)
    if arm is None:
        print("[show_gait_variants] %s has no armature" % fbx)
        return False
    rt._assign_slot(arm)
    _loop_without_rest_frame(arm.animation_data.action)
    # The clip may key the armature object itself, which would overwrite a location set on it, so the
    # clip rig is moved by a parent empty instead.
    slot = bpy.data.objects.new("slot | %s | %s" % (label, clip), None)
    slot.location = (x, y, 0.0)
    bpy.context.scene.collection.objects.link(slot)
    arm.parent = slot
    arm.name = "clip | %s | %s" % (label, clip)
    # The body is bound to a fresh engine rig exactly as the restyle's contact sheets bind it, and that rig copies
    # every bone's WORLD transform from the clip rig. Skinning the body straight onto the imported Kit rig deformed
    # it wrongly, because that rig's rest differs from the body's bind pose.
    eng = rt.build_engine_rig(_engine_json, name="%s | %s" % (label, clip))
    rs.attach_body(eng, body)
    for pb in eng.pose.bones:
        if pb.name in arm.pose.bones:
            con = pb.constraints.new("COPY_TRANSFORMS")
            con.target, con.subtarget = arm, pb.name
    arm.hide_set(True)
    if kinds:
        _attach_props(eng, kinds, _props)
    facing = _facing(eng)
    _label("%s\n%s" % (label, clip), (x, y, 2.15), facing)
    _placed.append((eng, x, facing))
    return True


def _review_camera(facing, line_width_m, focus_x=None, view="front"):
    """A camera in front of the line (on the side the characters face), aimed at its middle, wide enough for all.
    With focus_x it stands 3 m from the character at that x instead, in front or at one side."""
    target = bpy.data.objects.new("review_target", None)
    target.location = (0.0 if focus_x is None else focus_x, 0.0, 0.9 if focus_x is None else 1.0)
    bpy.context.scene.collection.objects.link(target)
    cam_data = bpy.data.cameras.new("review_camera")
    cam_data.lens = 35.0
    cam = bpy.data.objects.new("review_camera", cam_data)
    half_fov = math.atan(cam_data.sensor_width / (2.0 * cam_data.lens))
    distance = (line_width_m / 2.0 + 1.0) / math.tan(half_fov)
    cam.location = (distance * 0.15, facing * distance, 1.6)
    if focus_x is not None:
        side = {"side": -3.0, "side_left": 3.0}.get(view)  # a -Y-facing character has his right at -X
        cam.location = (focus_x, facing * 3.0, 1.3) if side is None else (focus_x + side * -facing, 0.0, 1.3)
    bpy.context.scene.collection.objects.link(cam)
    track = cam.constraints.new("TRACK_TO")
    track.target = target
    track.track_axis, track.up_axis = "TRACK_NEGATIVE_Z", "UP_Y"
    scn = bpy.context.scene
    scn.camera = cam
    scn.render.resolution_x, scn.render.resolution_y = (1920, 720) if focus_x is None else (1280, 960)


def _snapshot(path, frame=10):
    """Render one Workbench still through the review camera (what the viewport will show), for a -b smoke run."""
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_WORKBENCH"
    scn.display.shading.light = "STUDIO"
    scn.render.filepath = path
    scn.frame_set(frame)
    bpy.ops.render.render(write_still=True)
    scn.frame_set(1)


def _start_playback():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != "VIEW_3D":
                continue
            space = area.spaces.active
            space.shading.type = "SOLID"
            space.overlay.show_bones = False
            # through the review camera, which stands on the side the characters face
            space.region_3d.view_perspective = "CAMERA"
            region = next(r for r in area.regions if r.type == "WINDOW")
            with bpy.context.temp_override(window=window, area=area, region=region):
                if not bpy.context.screen.is_animation_playing:
                    bpy.ops.screen.animation_play()
            return None
    return 0.5  # the window is not ready yet; try again


def _load_props(args):
    """Read the props file and import the FBX of every kind that will be used, once."""
    _props.update(kinds={}, loaded={})
    if not (args.props or args.props_per_clip):
        return
    with open(args.props_file, encoding="utf-8") as fh:
        spec = json.load(fh)
    _props["kinds"] = spec["kinds"]
    names = set()
    for clip in args.clip:
        names.update(_kinds_for(args, clip))
    for kind in sorted(names):
        if kind not in spec["kinds"]:
            raise RuntimeError("unknown props kind '%s' (known: %s)" % (kind, ", ".join(sorted(spec["kinds"]))))
        _props["loaded"][kind] = _load_kind(kind, spec["kinds"][kind], spec["asset_root"])


def _kinds_for(args, clip):
    if args.props_per_clip or args.props == "auto":
        return _auto_kinds(clip)
    return [k.strip() for k in (args.props or "").split(",") if k.strip()]


def main():
    args = _args()
    global _engine_json
    _engine_json = args.engine_skeleton
    columns = []
    for spec in args.column:
        label, _, folder = spec.partition("=")
        columns.append((label.strip(), folder.strip()))
    _clear_scene()
    _load_props(args)
    placed = 0
    # one line along X: each clip is a group of the columns, with an empty slot between groups
    slots = len(args.clip) * (len(columns) + 1) - 1
    for row, clip in enumerate(args.clip):
        for col, (label, folder) in enumerate(columns):
            slot = row * (len(columns) + 1) + col
            x = (slot - (slots - 1) / 2.0) * COLUMN_SPACING_M
            placed += _place(folder, label, clip, x, 0.0, args.body, _kinds_for(args, clip))
    bpy.ops.mesh.primitive_plane_add(size=60.0, location=(0.0, 0.0, 0.0))
    bpy.context.active_object.name = "_ground"

    scn = bpy.context.scene
    scn.render.fps, scn.render.fps_base = 30, 1.0  # the body FBX import sets 24
    scn.frame_start, scn.frame_end = 1, SCENE_FRAMES
    scn.frame_set(1)

    facing = _placed[0][2] if _placed else -1
    focus_x = _placed[args.focus][1] if args.focus is not None and 0 <= args.focus < len(_placed) else None
    _review_camera(facing, slots * COLUMN_SPACING_M, focus_x, args.view)
    if focus_x is not None:  # a neighbour's shield would otherwise stand between the camera and the character
        for eng, x, _ in _placed:
            if x != focus_x:
                for o in [eng] + list(eng.children_recursive):
                    o.hide_render = True
                    o.hide_set(True)
    if args.snapshot:
        _snapshot(args.snapshot, args.snapshot_frame)
    bpy.app.timers.register(_start_playback, first_interval=1.0)
    # where each pelvis really is at two frames, so a smoke run proves the spread and that it holds
    spots = []
    for frame in (1, 13):
        scn.frame_set(frame)
        spots.append(["%.2f" % (arm.matrix_world @ arm.pose.bones["pelvis"].head).x for arm, _, _ in _placed])
    line = "placed %d of %d; slot x %s; pelvis x f1 %s; pelvis x f13 %s; facing %s" % (
        placed, len(columns) * len(args.clip), ["%.2f" % x for _, x, _ in _placed], spots[0], spots[1],
        sorted({f for _, _, f in _placed}))
    scn.frame_set(1)
    if args.props or args.props_per_clip:
        line += "; props %s" % ("auto" if args.props_per_clip or args.props == "auto" else args.props)
    print("[show_gait_variants] %s; playback starts in the viewport" % line)
    return args.status, line


_placed = []
_props = {}
_engine_json = None
_status_path = None
try:
    _status_path = _args().status
    _status_path, _line = main()
except Exception as exc:  # noqa: BLE001 - reported through --status, the only channel a detached run has
    _line = "error: %s: %s" % (type(exc).__name__, exc)
    print("[show_gait_variants] " + _line)
if _status_path:
    with open(_status_path, "w", encoding="utf-8") as fh:
        fh.write(_line + "\n")
