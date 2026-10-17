"""Read-only FBX probe: per-mesh triangle count, dimensions (local axes),
location, scale, rotation, materials and UV layers, as JSON. Used to measure a reference asset (an
existing map icon) before setting a decimation budget.

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/blender/probe_fbx.py -- <in.fbx> <out.json>

The Store launcher detaches with no stdout: wait for <out.json>. tools/blender/blender_job.py
removes an old one first and writes it on every exit, "status" first ("ok" or "error" with the
trace).
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blender_job import import_fbx, job, script_args  # noqa: E402


def main():
    src, out = script_args()[:2]
    with job(out) as result:
        result["src"] = src
        bpy.ops.wm.read_factory_settings(use_empty=True)
        import_fbx(src)
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


if __name__ == "__main__":  # Blender runs a -P script as __main__
    main()
