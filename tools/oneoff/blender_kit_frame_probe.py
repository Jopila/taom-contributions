"""
Frame probe for a kitbash kit (#797 method): export untouched source objects with the assembler's
FBX settings, so `fbx_frame_compare.py <source.fbx> <probe.fbx>` shows how the engine reads our
exports against the kit's own files.

    blender-launcher.exe -b -P tools/oneoff/blender_kit_frame_probe.py -- --kit gondor \
        --objects "meshes/gondor_wall_3m_straight_a.fbx:gondor_wall_3m_a_clean.wall;..." --out <dir>

Each object is copied with its imported world transform baked into the mesh (as the assembler
places parts); all of them go into one FBX with kitbash_kits.FBX_EXPORT. Writes
<out>/frame_probe.fbx, frame_probe.json, frame_probe_log.txt and frame_probe_DONE.txt (the DONE
file is removed before the run).
"""

import argparse
import json
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kitbash_kits import FBX_EXPORT, get_kit  # noqa: E402

LOG = []


def log(msg):
    LOG.append(msg)
    print(msg)


def export_fbx(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, **FBX_EXPORT)  # the assemblers' settings


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--kit", default="gondor")
    ap.add_argument("--objects", required=True, help="folder/file.fbx:object;...")
    ap.add_argument("--out", required=True)
    a = ap.parse_args(argv)
    cfg = get_kit(a.kit)
    os.makedirs(a.out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    probe, report = [], []
    for spec in a.objects.split(";"):
        rel, name = spec.rsplit(":", 1)
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(cfg["src"], rel))
        new = [o for o in bpy.data.objects if o not in before]
        src = next((o for o in new if o.name.lower() == name.lower()), None)
        if src is None:
            raise RuntimeError(f"{name} not in {rel}: {[o.name for o in new][:20]}")
        me = src.data.copy()
        me.transform(src.matrix_world)
        o = bpy.data.objects.new(src.name + "_probe", me)
        bpy.context.scene.collection.objects.link(o)
        probe.append(o)
        report.append({"source": rel, "object": src.name, "probe": o.name,
                       "import_matrix": [list(r) for r in src.matrix_world]})
        log(f"[probe] {rel}:{src.name} -> {o.name}")
        bpy.data.batch_remove(new)
    path = os.path.join(a.out, "frame_probe.fbx")
    export_fbx(probe, path)
    log(f"[probe] wrote {path}")
    with open(os.path.join(a.out, "frame_probe.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)


def run():
    out = sys.argv[sys.argv.index("--out") + 1] if "--out" in sys.argv else None
    done = os.path.join(out, "frame_probe_DONE.txt") if out else None
    if done and os.path.exists(done):  # before the run: a poller never reads an older result
        os.remove(done)
    try:
        main()
        status = "ok"
    except Exception as e:  # report through DONE, the launcher detaches
        import traceback
        log(traceback.format_exc())
        status = f"error: {e}"
    if out:
        os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, "frame_probe_log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG))
        with open(done, "w", encoding="utf-8") as f:
            f.write(status)


if __name__ == "__main__":
    run()
