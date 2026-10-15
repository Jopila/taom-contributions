"""Exact FBX tick times around Blender's FBX exporter (headless Blender scripts).

Blender 5.2.2's io_scene_fbx converts frames to FBX ticks by float product and truncation:
  export_fbx_bin.py:2328   (real_currframes / fps * FBX_KTIME).astype(np.int64)         key times
  export_fbx_bin.py:2064   int(convert_sec_to_ktime(f / fps))  (and :3480)              stack / take start, stop
For some frame counts the product lands just under the exact tick, so the last key and the stop time sit one
tick under their frame, and the Modding Kit compiled that master one frame short (RCA wildmesh-mounts 2026-10-15:
the deer RunSlow_F, goat HoppingSlow_F and warthog TrotFast_F_2 masters among 11). FBX_KTIME (v7) is
46186158000 ticks per second, so at 30 fps one frame is exactly 1539538600 ticks.

exact_ticks() is the pure rounding (no bpy). exact_fbx_times() wraps AnimationCurveNodeWrapper.set_keyframes and
export_fbx_bin.convert_sec_to_ktime for the length of a with block, and restores both on exit.

Usage inside Blender:
    with exact_fbx_times() as state:
        bpy.ops.export_scene.fbx(...)
    # state.patched is False (with state.reason) when fps_base != 1 or fps is not integral: nothing was patched
"""
import contextlib
import functools

import numpy as np

FBX_KTIME = 46186158000


def exact_ticks(ticks, fps, ktime=FBX_KTIME):
    """Snap tick values (an int or a numpy array) to the exact tick of their nearest frame at integral fps.

    frame = round(ticks * fps / ktime); the tick is frame * ktime / fps, exact in Python integers when fps
    divides frame * ktime, rounded half up otherwise. Idempotent on exact input.
    """
    fps = int(fps)
    if isinstance(ticks, np.ndarray):
        frames = np.rint(ticks.astype(np.float64) * fps / ktime).astype(np.int64)
        return (2 * frames * ktime + fps) // (2 * fps)
    frame = int(round(int(ticks) * fps / ktime))
    return (2 * frame * ktime + fps) // (2 * fps)


class State:
    """What exact_fbx_times() did: patched (bool) and, when False, the reason."""

    def __init__(self):
        self.patched = False
        self.reason = ""
        self.fps = None


@contextlib.contextmanager
def exact_fbx_times():
    import bpy
    from io_scene_fbx import export_fbx_bin, fbx_utils

    state = State()
    render = bpy.context.scene.render
    fps, base = render.fps, render.fps_base
    if base != 1:
        state.reason = "fps_base %s != 1: exporter left unpatched" % base
    elif fps != int(fps):
        state.reason = "fps %s is not integral: exporter left unpatched" % fps
    if state.reason:
        yield state
        return

    fps = int(fps)
    state.fps = fps
    wrapper = fbx_utils.AnimationCurveNodeWrapper
    orig_set = wrapper.set_keyframes
    orig_convert = export_fbx_bin.convert_sec_to_ktime
    ktime = getattr(fbx_utils, "FBX_KTIME", FBX_KTIME)

    @functools.wraps(orig_set)
    def set_keyframes(self, keyframe_times, keyframe_values):
        return orig_set(self, exact_ticks(np.asarray(keyframe_times), fps, ktime), keyframe_values)

    def convert_sec_to_ktime(seconds):
        return exact_ticks(int(round(seconds * ktime)), fps, ktime)

    wrapper.set_keyframes = set_keyframes
    export_fbx_bin.convert_sec_to_ktime = convert_sec_to_ktime
    state.patched = True
    try:
        yield state
    finally:
        wrapper.set_keyframes = orig_set
        export_fbx_bin.convert_sec_to_ktime = orig_convert
