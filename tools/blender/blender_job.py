"""Plumbing shared by TAOM's headless Blender scripts: a completion report that every exit writes,
the FBX import fallback, and the Kit FBX export settings.

The Store build of Blender (blender-launcher.exe) detaches with -b and prints nothing, so a caller
waits for a report file. job() makes that file trustworthy: it removes the previous report first,
writes "ok" only when the block returns, and writes "error" with the trace on any exception,
argparse's SystemExit included. A crash can no longer leave a stale "ok" or no file at all (the
Carn Dum review, 2026-10-16, found both in four scripts that each rolled their own).

A script reports failure by raising; whatever it put in the report before that is kept.

bpy is imported lazily, so this module is unit-tested without Blender
(tools/tests/test_blender_job.py).
"""
import json
import os
import sys
import traceback
from contextlib import contextmanager

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "oneoff"))
from kitbash_kits import FBX_EXPORT  # noqa: E402  (pure Python, no bpy)


def script_args(argv=None):
    """The arguments after Blender's "--"."""
    argv = sys.argv if argv is None else argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def flag_value(args, flag):
    """The value of `flag` in `args` (`--out x` or `--out=x`), read before argparse runs, so a bad
    argument still gets a fresh report in the right folder. None when absent."""
    for i, a in enumerate(args):
        if a == flag and i + 1 < len(args):
            return args[i + 1]
        if a.startswith(flag + "="):
            return a.split("=", 1)[1]
    return None


def report_path(out_dir):
    return os.path.join(out_dir, "_report", "DONE.txt")


@contextmanager
def job(path):
    """Yield a report dict and write it as JSON to `path` on every exit: "status" first, "ok"
    only when the block returned, "error" with "trace" on any exception."""
    if os.path.exists(path):
        os.remove(path)
    report = {"status": "running"}
    try:
        yield report
        report["status"] = "ok"
    except BaseException:  # argparse's SystemExit too: the report must still say what happened
        report["status"] = "error"
        report["trace"] = traceback.format_exc()
    finally:
        os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=1, default=str)


def import_fbx(path):
    """Import an FBX with Blender 5's importer, falling back to the legacy operator; return the
    new mesh objects."""
    import bpy
    before = set(bpy.data.objects)
    try:
        bpy.ops.wm.fbx_import(filepath=path)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before and o.type == "MESH"]


def export_fbx(path, objs):
    """Export `objs` with the Kit's proven settings (kitbash_kits.FBX_EXPORT)."""
    import bpy
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, **FBX_EXPORT)
