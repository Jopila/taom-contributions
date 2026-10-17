"""Painter-side script (run through painter_run.py): export every texture set
and save the project.

ARGS: out_dir (path), spp (path to save the project as), size (one size for
every set, a power of two from 128 to 4096; without it each set exports at its
own resolution), sets (a list of names to export, or one name; default all).

Painter 12.1.5: list_predefined_export_presets() offers no plain "PBR Metallic
Roughness" (a "PBR Metallic Roughness.spexp" ships under
resources/starter_assets/export-presets, but this script does not use it), so
it takes "glTF PBR Metal Roughness" by exact name: <set>_baseColor,
<set>_normal (OpenGL) and <set>_occlusionRoughnessMetallic (R AO, G roughness,
B metallic). tools/oneoff/convert_tripo_prop_textures.py reads that packed map.
"""
import math
import os

import substance_painter.export as exp
import substance_painter.project as proj
import substance_painter.textureset as ts

PRESET = "glTF PBR Metal Roughness"
out_dir = ARGS["out_dir"]  # noqa: F821  (injected by painter_run.py)
os.makedirs(out_dir, exist_ok=True)
params = {"fileFormat": "png", "bitDepth": "8", "dithering": True, "paddingAlgorithm": "infinite"}
if ARGS.get("size"):  # noqa: F821
    size = int(ARGS["size"])  # noqa: F821
    if size not in (128, 256, 512, 1024, 2048, 4096):
        raise ValueError(f"size {size} is not a power of two from 128 to 4096")
    params["sizeLog2"] = int(math.log2(size))
wanted = ARGS.get("sets")  # noqa: F821
if isinstance(wanted, str):
    wanted = [wanted]

presets = {p.name: p.url for p in exp.list_predefined_export_presets()}
if PRESET not in presets:
    raise LookupError(f'no "{PRESET}" export preset in ' + ", ".join(sorted(presets)))
status = exp.export_project_textures({
    "exportShaderParams": False,
    "exportPath": out_dir,
    "defaultExportPreset": presets[PRESET],
    "exportList": [{"rootPath": t.name()} for t in ts.all_texture_sets() if not wanted or t.name() in wanted],
    "exportParameters": [{"parameters": params}],
})
if ARGS.get("spp"):  # noqa: F821
    proj.save_as(ARGS["spp"])  # noqa: F821
RESULT = {
    "status": status.status.name, "preset": PRESET,
    "files": sorted(str(p) for paths in dict(status.textures).values() for p in paths),
    "saved": proj.file_path(),
}
