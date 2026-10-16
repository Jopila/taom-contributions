"""
Compare how two binary FBX files store the same mesh, without Blender: the
GlobalSettings axis header, each model's node transform, and the vertex bounds
before and after the node transform, in metres of the file's own axes.

Used for issue #797 to check that an Erebor building exported from Blender is
stored the same way as the kit FBX the pieces came from, so the engine reads
both the same way (docs/kitbash/erebor/design-patterns.md, "The frame rule").

    python tools/oneoff/fbx_frame_compare.py A.fbx[:model] B.fbx[:model] [...]

Each argument is a file, optionally followed by ':' and a model name (matched
without case); without a name, every mesh model in the file is listed.
Exit 0 always: this reports, it does not judge.

The node transform modelled is Lcl Translation, Lcl Rotation (eEulerXYZ only),
PreRotation and Lcl Scaling. A model that also sets a pivot, an offset,
PostRotation, a geometric transform, or hangs under a parent model is listed
with "unsupported": its node_bbox_m is then not what the engine reads.
"""

import math
import struct
import sys
import zlib

MAGIC = b"Kaydara FBX Binary  \x00"
SCALARS = {b"Y": "<h", b"C": "<?", b"I": "<i", b"F": "<f", b"D": "<d", b"L": "<q"}
ARRAYS = {b"f": ("f", 4), b"d": ("d", 8), b"l": ("q", 8), b"i": ("i", 4), b"b": ("?", 1)}
KEEP = ("GlobalSettings", "Objects", "Connections")
DECODE = ("Vertices",)  # the only array read; the rest (indices, normals, UVs) are skipped
# Transform properties describe() does not model, with their FBX defaults.
UNMODELLED = {"RotationOffset": (0, 0, 0), "RotationPivot": (0, 0, 0), "ScalingOffset": (0, 0, 0),
              "ScalingPivot": (0, 0, 0), "PostRotation": (0, 0, 0), "GeometricTranslation": (0, 0, 0),
              "GeometricRotation": (0, 0, 0), "GeometricScaling": (1, 1, 1)}


def parse(path):
    with open(path, "rb") as f:
        data = f.read()
    if not data.startswith(MAGIC):
        raise ValueError(f"{path}: not a binary FBX")
    wide = struct.unpack_from("<I", data, 23)[0] >= 7500
    null_len = 25 if wide else 13

    def prop(off, decode):
        t = data[off:off + 1]
        off += 1
        if t in SCALARS:
            fmt = SCALARS[t]
            return struct.unpack_from(fmt, data, off)[0], off + struct.calcsize(fmt)
        if t in ARRAYS:
            n, enc, clen = struct.unpack_from("<III", data, off)
            off += 12
            if not decode:
                return None, off + clen
            raw = data[off:off + clen]
            if enc:
                raw = zlib.decompress(raw)
            code, size = ARRAYS[t]
            return struct.unpack_from("<%d%s" % (n, code), raw[:n * size]), off + clen
        if t in (b"S", b"R"):
            n = struct.unpack_from("<I", data, off)[0]
            return data[off + 4:off + 4 + n], off + 4 + n
        raise ValueError(f"unknown property type {t!r}")

    def node(off, top):
        if wide:
            end, nprops, _ = struct.unpack_from("<QQQ", data, off)
            off += 24
        else:
            end, nprops, _ = struct.unpack_from("<III", data, off)
            off += 12
        nlen = data[off]
        name = data[off + 1:off + 1 + nlen].decode("latin-1")
        off += 1 + nlen
        if end == 0:
            return None, off
        if top and name not in KEEP:
            return (name, None, None), end
        props = []
        for _ in range(nprops):
            v, off = prop(off, name in DECODE)
            props.append(v)
        kids = []
        while off < end - null_len:
            k, off = node(off, False)
            if k is None:
                break
            kids.append(k)
        return (name, props, kids), end

    tree, off = {}, 27
    while off < len(data) - null_len:
        n, off = node(off, True)
        if n is None:
            break
        if n[1] is not None:
            tree[n[0]] = n
    return tree


def p70(node):
    out = {}
    for k in node[2]:
        if k[0] == "Properties70":
            for p in k[2]:
                key = p[1][0].decode("latin-1")
                out[key] = p[1][4:]
    return out


def name_of(raw):
    return raw.split(b"\x00\x01")[0].decode("utf-8", "replace")


def rot(deg, order=0):
    """FBX eEulerXYZ (order 0): R = Rz @ Ry @ Rx."""
    x, y, z = (math.radians(a) for a in deg)
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    rx = [[1, 0, 0], [0, cx, -sx], [0, sx, cx]]
    ry = [[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]
    rz = [[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]]
    mm = lambda a, b: [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
    if order != 0:
        raise ValueError("only eEulerXYZ is handled")
    return mm(mm(rz, ry), rx)


def describe(path, want=None):
    tree = parse(path)
    gs = p70(tree["GlobalSettings"])
    header = {k: gs[k][0] for k in ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign",
                                     "CoordAxis", "CoordAxisSign", "UnitScaleFactor") if k in gs}
    unit_m = header.get("UnitScaleFactor", 1.0) / 100.0
    objs = {o[1][0]: o for o in tree["Objects"][2] if o[1]}
    geo_of, has_parent = {}, set()
    for c in tree["Connections"][2]:
        if c[1] and c[1][0] == b"OO":
            child, parent = c[1][1], c[1][2]
            if child in objs and objs[child][0] == "Geometry" and parent in objs:
                geo_of[parent] = objs[child]
            if child in objs and objs[child][0] == "Model" and parent in objs and objs[parent][0] == "Model":
                has_parent.add(child)
    rows = []
    for oid, o in objs.items():
        if o[0] != "Model" or o[1][2] != b"Mesh":
            continue
        name = name_of(o[1][1])
        if want and name.lower() != want.lower():
            continue
        props = p70(o)
        t = list(props.get("Lcl Translation", (0, 0, 0)))
        r = list(props.get("Lcl Rotation", (0, 0, 0)))
        s = list(props.get("Lcl Scaling", (1, 1, 1)))
        pre = list(props.get("PreRotation", (0, 0, 0)))
        order = props.get("RotationOrder", (0,))[0]
        unsupported = [k for k, d in UNMODELLED.items()
                       if k in props and any(abs(a - b) > 1e-9 for a, b in zip(props[k], d))]
        if oid in has_parent:
            unsupported.append("parent model")
        g = geo_of.get(oid)
        verts = next((k[1][0] for k in g[2] if k[0] == "Vertices"), ()) if g else ()
        pts = [verts[i:i + 3] for i in range(0, len(verts), 3)]
        R = rot(r, order)
        Rp = rot(pre)
        M = [[sum(Rp[i][k] * R[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
        world = [[sum(M[i][j] * p[j] * s[j] for j in range(3)) + t[i] for i in range(3)] for p in pts]

        def box(ps):
            if not ps:
                return None
            return ([round(min(p[i] for p in ps) * unit_m, 3) for i in range(3)],
                    [round(max(p[i] for p in ps) * unit_m, 3) for i in range(3)])
        rows.append({"model": name, "lcl_t": [round(v, 4) for v in t], "lcl_r": [round(v, 3) for v in r],
                     "pre_r": [round(v, 3) for v in pre], "lcl_s": [round(v, 5) for v in s],
                     "raw_bbox_m": box(pts), "node_bbox_m": box(world), "unsupported": unsupported})
    return header, rows


def split_arg(arg):
    """'A.fbx:Model' -> ('A.fbx', 'Model'); the '.fbx' match ignores case and is added when absent."""
    i = arg.lower().find(".fbx:")
    if i >= 0:
        return arg[:i + 4], arg[i + 5:] or None
    return (arg if arg.lower().endswith(".fbx") else arg + ".fbx"), None


def main():
    for arg in sys.argv[1:]:
        path, model = split_arg(arg)
        header, rows = describe(path, model)
        print(f"== {path}")
        print(f"   header {header}")
        for r in rows:
            print(f"   {r['model']}: T {r['lcl_t']} R {r['lcl_r']} Pre {r['pre_r']} S {r['lcl_s']}")
            print(f"      raw  {r['raw_bbox_m']}")
            print(f"      node {r['node_bbox_m']}")
            if r["unsupported"]:
                print(f"      UNSUPPORTED {r['unsupported']}: node bbox is not what the engine reads")


if __name__ == "__main__":
    main()
