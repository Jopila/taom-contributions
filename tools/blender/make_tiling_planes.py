"""Write an FBX of flat 1 x 1 planes, one per material name, each UV-mapped 0..1, so a
Substance Painter project made from it has one texture set per material. Painter's
procedural materials repeat seamlessly across a 0..1 plane, so what is painted there tiles
on a mesh with world-scale UVs (tools/oneoff/blender_build_carn_dum_icon.py).

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b -P tools/blender/make_tiling_planes.py -- <out.fbx> <material> [<material> ...]

Writes <out.fbx> and <out.fbx>.done (the launcher detaches).
"""
import sys

import bmesh
import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
out, names = argv[0], argv[1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
for i, name in enumerate(names):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    x0 = i * 1.5
    verts = [bm.verts.new(v) for v in ((x0, 0, 0), (x0 + 1, 0, 0), (x0 + 1, 1, 0), (x0, 1, 0))]
    face = bm.faces.new(verts)
    uv = bm.loops.layers.uv.new("UVMap")
    for loop, coord in zip(face.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        loop[uv].uv = coord
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(bpy.data.materials.new(name))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"},
                         bake_space_transform=True, add_leaf_bones=False)
open(out + ".done", "w").write("ok")
