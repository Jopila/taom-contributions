"""Painter-side script (run through painter_run.py): export every texture set
and save the project.

ARGS: out_dir (path), spp (path to save the project as), size (one size for
every set; without it each set exports at its own resolution), sets (names to
export; default all).

Painter 12.0.3 ships no plain "PBR Metallic Roughness" preset; the closest
is "glTF PBR Metal Roughness": <set>_baseColor, <set>_normal (OpenGL) and
<set>_occlusionRoughnessMetallic (R AO, G roughness, B metallic).
tools/oneoff/convert_tripo_prop_textures.py reads that packed map.
"""
import math
import os

import substance_painter.export as exp
import substance_painter.project as proj
import substance_painter.textureset as ts

out_dir = ARGS["out_dir"]  # noqa: F821  (injected by painter_run.py)
os.makedirs(out_dir, exist_ok=True)
params = {"fileFormat": "png", "bitDepth": "8", "dithering": True, "paddingAlgorithm": "infinite"}
if ARGS.get("size"):  # noqa: F821
    params["sizeLog2"] = int(math.log2(int(ARGS["size"])))  # noqa: F821
wanted = ARGS.get("sets")  # noqa: F821

presets = {p.name: p.url for p in exp.list_predefined_export_presets()}
metal_rough = sorted((n for n in presets if "metal" in n.lower() and "rough" in n.lower()), key=len)
if not metal_rough:
    raise LookupError("no metal/rough export preset in " + ", ".join(sorted(presets)))
status = exp.export_project_textures({
    "exportShaderParams": False,
    "exportPath": out_dir,
    "defaultExportPreset": presets[metal_rough[0]],
    "exportList": [{"rootPath": t.name()} for t in ts.all_texture_sets() if not wanted or t.name() in wanted],
    "exportParameters": [{"parameters": params}],
})
if ARGS.get("spp"):  # noqa: F821
    proj.save_as(ARGS["spp"])  # noqa: F821
RESULT = {
    "status": status.status.name, "preset": metal_rough[0],
    "files": sorted(str(p) for paths in dict(status.textures).values() for p in paths),
    "saved": proj.file_path(),
}
