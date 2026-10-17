"""Painter-side script (run through painter_run.py): new project from a low
FBX in OCIO color management, then bake mesh maps for every texture set.

ARGS:
  low            path of the mesh (one texture set per material)
  high           path of a high-poly bake source
  low_as_high    true: bake the low against itself (a mesh with no high, such as a
                 rebuilt map icon: AO, curvature, thickness and position still come
                 from its own geometry). One of high and low_as_high is required.
  resolution     default texture set size (1024)
  uv_report      path of tools/blender/unwrap_texture_sets.py's <out>/_report/DONE.txt:
                 each set takes the resolution its unwrap margins were cut for
  set_resolutions  {"set name": size} overrides, e.g. {"carn_dum_iron": 256}; wins
                 over uv_report
  bake_scale     bake size as a multiple of the set size, downsampled into it (2);
                 bake_size (a fixed size, the old argument) wins when given
  ao_rays (128), ao_distance (relative to the mesh size, 0.05: at 0.15 with a
  ground plane a 10 m wall counted as occluded top to bottom), ao_ground (false),
  curvature_radius (relative, 0.002), subsampling (1)
  ocio           default: Painter's shipped "substance" config
  discard_unsaved  true: close an open project that has unsaved changes (default
                 false: the script refuses, since close() loses unsaved data)

Why OCIO: a Legacy project (Painter's default) refuses every Color the Python
API sends, whatever its color space ("Can't use a non legacy color space with
a legacy color managed project"), so no layer could be colored. The OCIO
environment variable is set only for create() and put back after it.
Bakes: World Space Normal, AO, Curvature (from the mesh), Position, Thickness,
and Normal unless low_as_high, for every texture set in one pass. create()
returns before the mesh is loaded, so the script pumps Qt events until the
texture sets exist. painter_run exits 0 on any string RESULT, so a mesh that
does not load or a bake that does not succeed raises instead.
"""
import json
import math
import os
import time

import substance_painter.baking as bk
import substance_painter.event as ev
import substance_painter.project as proj
import substance_painter.textureset as ts
from PySide6.QtCore import QCoreApplication

OCIO_DEFAULT = (r"C:\Program Files\Adobe\Adobe Substance 3D Painter"
                r"\resources\ocio\substance\config.ocio")
A = ARGS  # noqa: F821  (injected by painter_run.py)
low = A["low"]
high = A.get("high")
low_as_high = bool(A.get("low_as_high", False))
resolution = int(A.get("resolution", 1024))
set_res = {}
if A.get("uv_report"):
    with open(A["uv_report"], encoding="utf-8") as f:
        uv_report = json.load(f)
    # the unwrap writes its sets before the export: a failed run can leave an older FBX on disk
    if uv_report.get("status") != "ok":
        raise RuntimeError(f"uv_report {A['uv_report']} has status {uv_report.get('status')!r}, "
                           "not ok: re-run the unwrap")
    set_res.update({k: int(v["resolution"]) for k, v in uv_report["sets"].items()})
set_res.update({k: int(v) for k, v in A.get("set_resolutions", {}).items()})
ocio = A.get("ocio", OCIO_DEFAULT)


def pump(seconds, until):
    t0 = time.time()
    while not until() and time.time() - t0 < seconds:
        QCoreApplication.processEvents()
        time.sleep(0.05)
    return until()


def configure(tset):
    size = set_res.get(tset.name(), resolution)
    log2 = int(math.log2(size))
    tset.set_resolution(ts.Resolution(size, size))
    bake_log2 = (int(math.log2(int(A["bake_size"]))) if "bake_size" in A
                 else min(12, log2 + int(math.log2(int(A.get("bake_scale", 2))))))
    params = bk.BakingParameters.from_texture_set(tset)
    common = params.common()
    values = {common["SubSampling"]: int(A.get("subsampling", 1)),
              common["OutputSize"]: (bake_log2, bake_log2)}
    if low_as_high:
        # rays along the face normal over a short distance: with averaged normals and the
        # default 1% of the mesh size (most of a metre on Carn Dum) rays from one face hit
        # its 90-degree neighbour, and 47% of the texels came out tilted
        values[common["LowAsHigh"]] = True
        values[common["SmoothNormals"]] = False
        values[common["MaxHeight"]] = 0.001
        values[common["MaxDepth"]] = 0.001
    else:
        values[common["HipolyMesh"]] = "file:///" + high.replace("\\", "/")
    params.set(values)
    U = bk.MeshMapUsage
    ao = params.baker(U.AO)
    params.set({ao["NbSecondary"]: int(A.get("ao_rays", 128)),
                ao["MaxDistance"]: float(A.get("ao_distance", 0.05)),
                ao["GroundPlane"]: bool(A.get("ao_ground", False))})
    cv = params.baker(U.Curvature)
    params.set({cv["SamplingRadius"]: float(A.get("curvature_radius", 0.002))})
    params.set_curvature_method(bk.CurvatureMethod.FromMesh)
    # a mesh baked against itself has no detail for a normal map: skip it, Painter then
    # uses a flat one
    bakers = [U.WorldSpaceNormal, U.AO, U.Curvature, U.Position, U.Thickness]
    params.set_enabled_bakers(bakers if low_as_high else [U.Normal] + bakers)
    params.set_textureset_enabled(True)
    return {"size": size, "bake": 2 ** bake_log2}


def bake_all():
    state = {"status": None}

    def on_end(e):
        s = getattr(e, "status", None)
        state["status"] = s.name if hasattr(s, "name") else str(s)

    ev.DISPATCHER.connect_strong(ev.BakingProcessEnded, on_end)
    try:
        bk.bake_selected_textures_async()
        pump(3000, lambda: state["status"] is not None)
    finally:
        ev.DISPATCHER.disconnect(ev.BakingProcessEnded, on_end)
    return state["status"] or "timeout"


if not (high or low_as_high):
    raise ValueError("pass high (a bake source) or low_as_high=true")
for path in (low, high):
    if path and not os.path.isfile(path):
        raise FileNotFoundError(path)
if proj.is_open():
    if proj.needs_saving() and not A.get("discard_unsaved", False):
        raise RuntimeError(f"{proj.file_path() or 'the open project'} has unsaved changes: "
                           "save it, or pass discard_unsaved=true")
    proj.close()
previous_ocio = os.environ.get("OCIO")
os.environ["OCIO"] = ocio
try:
    proj.create(mesh_file_path=low, settings=proj.Settings(
        default_texture_resolution=resolution,
        normal_map_format=proj.NormalMapFormat.OpenGL))
finally:
    if previous_ocio is None:
        os.environ.pop("OCIO", None)
    else:
        os.environ["OCIO"] = previous_ocio
loaded = pump(120, lambda: proj.is_open() and len(ts.all_texture_sets()) > 0)
if not loaded:
    raise RuntimeError(f"{low} did not load within 120 s")

sets = ts.all_texture_sets()
t0 = time.time()
sizes = {t.name(): configure(t) for t in sets}
status = bake_all()
if status != "Success":
    raise RuntimeError(f"bake ended with {status}")
RESULT = {
    "loaded": loaded, "bake": status, "bake_seconds": round(time.time() - t0, 1),
    "texture_sets": sizes,
    "maps_per_set": {t.name(): [k for k in ts.MeshMapUsage.__members__
                                if t.get_mesh_map_resource(getattr(ts.MeshMapUsage, k)) is not None]
                     for t in sets},
}
