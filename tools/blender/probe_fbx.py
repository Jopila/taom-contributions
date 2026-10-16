"""Read-only FBX probe: per-mesh triangle count, dimensions (local axes),
location, scale, rotation, materials and UV layers, as JSON. Used to measure a reference asset (an
existing map icon) before setting a decimation budget.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/blender/probe_fbx.py -- <in.fbx> <out.json>

The Store launcher detaches with no stdout: wait for <out.json>.
"""
import json
import math
import sys
import traceback

import bpy

src, out = sys.argv[sys.argv.index("--") + 1:][:2]
result = {"src": src}
try:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.wm.fbx_import(filepath=src)
    except Exception:
        bpy.ops.import_scene.fbx(filepath=src)
    bpy.context.view_layer.update()
    meshes = []
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        o.data.calc_loop_triangles()
        meshes.append({
            "name": o.name,
            "parent": o.parent.name if o.parent else None,
            "tris": len(o.data.loop_triangles),
            "dims": [round(v, 3) for v in o.dimensions],
            "loc": [round(v, 3) for v in o.matrix_world.translation],
            "scale": [round(v, 4) for v in o.matrix_world.to_scale()],
            "rot_deg": [round(math.degrees(v), 2) for v in o.matrix_world.to_euler()],
            "materials": [m.name for m in o.data.materials if m],
            # how hard edges are made: flat faces, sharp edges or custom split normals
            "smooth_faces": sum(1 for p in o.data.polygons if p.use_smooth),
            "faces": len(o.data.polygons),
            "sharp_edges": sum(1 for e in o.data.edges if e.use_edge_sharp),
            "custom_normals": o.data.has_custom_normals,
            "uv_layers": [u.name for u in o.data.uv_layers],
        })
    result["meshes"] = sorted(meshes, key=lambda m: m["name"])
    result["status"] = "ok"
except Exception:
    result["status"] = "error"
    result["trace"] = traceback.format_exc()
with open(out, "w", encoding="utf-8") as f:
    json.dump(result, f, indent=1)
