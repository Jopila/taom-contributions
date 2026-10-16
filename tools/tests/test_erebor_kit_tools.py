"""Tests for the pure-Python Erebor kit tools in tools/oneoff/ (issue #797):
fbx_frame_compare.py (binary FBX reader and node transform), mine_erebor_prefab_joins.py
(prefab transform walk and relative offsets) and erebor_catalog_to_md.py (table text)."""
import math
import os
import shutil
import struct
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zlib

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import erebor_catalog_to_md as cat  # noqa: E402
import fbx_frame_compare as ffc  # noqa: E402
import mine_erebor_prefab_joins as mine  # noqa: E402


# --------------------------------------------------------------- a minimal binary FBX writer

def _prop(v):
    if isinstance(v, bytes):
        return b"S" + struct.pack("<I", len(v)) + v
    if isinstance(v, float):
        return b"D" + struct.pack("<d", v)
    if isinstance(v, tuple) and v[0] == "I":
        return b"I" + struct.pack("<i", v[1])
    if isinstance(v, int):
        return b"L" + struct.pack("<q", v)
    if isinstance(v, list):
        raw = struct.pack("<%dd" % len(v), *v)
        return b"d" + struct.pack("<III", len(v), 0, len(raw)) + raw
    if isinstance(v, tuple) and v[0] == "dz":  # a zlib-compressed double array, as Blender writes
        raw = zlib.compress(struct.pack("<%dd" % len(v[1]), *v[1]))
        return b"d" + struct.pack("<III", len(v[1]), 1, len(raw)) + raw
    raise TypeError(v)


def _node(name, props, children, offset):
    pb = b"".join(_prop(p) for p in props)
    head = 13 + len(name)
    cb = b""
    for c in children:
        cb += _node(*c, offset + head + len(pb) + len(cb))
    if children:
        cb += b"\x00" * 13
    end = offset + head + len(pb) + len(cb)
    return struct.pack("<III", end, len(props), len(pb)) + bytes([len(name)]) + name.encode() + pb + cb


def _p(name, kind, *values):
    return ("P", [name.encode(), kind.encode(), b"", b"A"] + list(values), [])


def write_fbx(path, model_props, vertices, unit=1.0, compressed=False, parent=False):
    """parent=True hangs the mesh model under a null model, as a nested kit scene does."""
    objects = [
        ("Model", [10, b"Piece\x00\x01Model", b"Mesh"], [("Properties70", [], model_props)]),
        ("Geometry", [20, b"Geo\x00\x01Geometry", b"Mesh"],
         [("Vertices", [("dz", vertices) if compressed else vertices], [])]),
    ]
    links = [("C", [b"OO", 20, 10], [])]
    if parent:
        objects.append(("Model", [30, b"Root\x00\x01Model", b"Null"], [("Properties70", [], [])]))
        links.append(("C", [b"OO", 10, 30], []))
    nodes = [
        ("GlobalSettings", [], [("Properties70", [], [
            _p("UpAxis", "int", ("I", 1)), _p("FrontAxis", "int", ("I", 2)),
            _p("CoordAxis", "int", ("I", 0)), _p("UnitScaleFactor", "double", unit)])]),
        ("Objects", [], objects),
        ("Connections", [], links),
    ]
    data = ffc.MAGIC + b"\x1a\x00" + struct.pack("<I", 7400)
    for n in nodes:
        data += _node(*n, len(data))
    data += b"\x00" * 13 + b"\x00" * 16
    with open(path, "wb") as f:
        f.write(data)


class FbxFrameCompareTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.dir, True)

    def test_kit_style_node_transform_matches_a_baked_export(self):
        # The kit stores 1.5 cm of data under a node rotation X -90 and scale 100;
        # an export stores 150 cm with an identity node. Both are the same 1.5 m.
        kit = os.path.join(self.dir, "kit.fbx")
        write_fbx(kit, [_p("Lcl Rotation", "Lcl Rotation", -90.0, 0.0, 0.0),
                        _p("Lcl Scaling", "Lcl Scaling", 100.0, 100.0, 100.0)],
                  [0.0, 0.0, 0.0, 1.5, 0.0, -1.0])
        export = os.path.join(self.dir, "export.fbx")
        write_fbx(export, [], [0.0, 0.0, 0.0, 150.0, -100.0, 0.0])
        h1, (r1,) = ffc.describe(kit)
        h2, (r2,) = ffc.describe(export)
        self.assertEqual(h1, h2)
        self.assertEqual(h1["UpAxis"], 1)
        self.assertEqual(r1["model"], "Piece")
        self.assertEqual(r1["raw_bbox_m"], ([0.0, 0.0, -0.01], [0.015, 0.0, 0.0]))
        self.assertEqual(r1["node_bbox_m"], r2["node_bbox_m"])
        self.assertEqual(r1["node_bbox_m"], ([0.0, -1.0, 0.0], [1.5, 0.0, 0.0]))

    def test_model_filter_is_case_insensitive(self):
        path = os.path.join(self.dir, "one.fbx")
        write_fbx(path, [], [0.0, 0.0, 0.0])
        self.assertEqual(len(ffc.describe(path, "piece")[1]), 1)
        self.assertEqual(ffc.describe(path, "other")[1], [])

    def test_euler_xyz_order(self):
        # FBX eEulerXYZ applies X first: R = Rz Ry Rx, so (0,1,0) under X 90 then Z 90 -> (0,0,1)
        r = ffc.rot((90, 0, 90))
        v = [sum(r[i][j] * (0, 1, 0)[j] for j in range(3)) for i in range(3)]
        self.assertEqual([round(x, 6) + 0.0 for x in v], [0.0, 0.0, 1.0])

    def test_compressed_vertex_arrays_read_the_same(self):
        """Blender writes Vertices zlib-compressed (encoding 1); frame_probe.fbx is one."""
        plain, packed = os.path.join(self.dir, "plain.fbx"), os.path.join(self.dir, "packed.fbx")
        verts = [0.0, 0.0, 0.0, 150.0, -100.0, 25.0]
        write_fbx(plain, [], verts)
        write_fbx(packed, [], verts, compressed=True)
        self.assertEqual(ffc.describe(plain), ffc.describe(packed))

    def test_transforms_it_does_not_model_are_flagged(self):
        clean = os.path.join(self.dir, "clean.fbx")
        write_fbx(clean, [], [0.0, 0.0, 0.0])
        self.assertEqual(ffc.describe(clean)[1][0]["unsupported"], [])
        odd = os.path.join(self.dir, "odd.fbx")
        write_fbx(odd, [_p("GeometricTranslation", "Vector3D", 0.0, 5.0, 0.0),
                        _p("RotationPivot", "Vector3D", 0.0, 0.0, 0.0)], [0.0, 0.0, 0.0], parent=True)
        flags = ffc.describe(odd)[1][0]["unsupported"]
        self.assertIn("GeometricTranslation", flags)
        self.assertIn("parent model", flags)
        self.assertNotIn("RotationPivot", flags)  # present but at its default

    def test_model_argument_split_ignores_case(self):
        self.assertEqual(ffc.split_arg(r"E:\k\SM_DW_Roof_3m_A.FBX:SM_DW_Roof_Str_A1"),
                         (r"E:\k\SM_DW_Roof_3m_A.FBX", "SM_DW_Roof_Str_A1"))
        self.assertEqual(ffc.split_arg(r"E:\k\probe.fbx"), (r"E:\k\probe.fbx", None))
        self.assertEqual(ffc.split_arg(r"E:\k\probe"), (r"E:\k\probe.fbx", None))

    def test_refuses_a_text_file(self):
        path = os.path.join(self.dir, "ascii.fbx")
        with open(path, "w") as f:
            f.write("; FBX 7.4.0 project file")
        with self.assertRaises(ValueError):
            ffc.parse(path)


class PrefabJoinTests(unittest.TestCase):
    XML = """<prefabs><game_entity name="house">
      <transform position="10.000, 0.000, 0.000" rotation_euler="0.000, 0.000, 1.5707963"/>
      <components><meta_mesh_component name="sm_dw_wall_3m_c"/></components>
      <children>
        <game_entity name="roof">
          <transform position="3.000, 0.000, 3.000" scale="1.000, 0.900, 1.000"/>
          <components><meta_mesh_component name="sm_dw_roof_str_b1"/>
                      <meta_mesh_component name="not_a_kit_mesh"/></components>
        </game_entity>
      </children></game_entity></prefabs>"""

    def placements(self):
        out = []
        mine.walk(ET.fromstring(self.XML).find("game_entity"), mine.Frame(), "t:house", out, "house")
        return {p["piece"]: p for p in out}

    def test_children_take_the_parent_frame(self):
        ps = self.placements()
        self.assertEqual(set(ps), {"sm_dw_wall_3m_c", "sm_dw_roof_str_b1"})
        roof = ps["sm_dw_roof_str_b1"]
        self.assertEqual([round(v, 3) for v in roof["pos"]], [10.0, 3.0, 3.0])
        self.assertEqual(round(roof["yaw"]), 90)
        self.assertEqual(roof["scale"], [1.0, 0.9, 1.0])
        self.assertFalse(roof["tilt"])

    def test_offset_is_in_the_first_piece_frame(self):
        ps = self.placements()
        off, dyaw = mine.to_local(ps["sm_dw_wall_3m_c"], ps["sm_dw_roof_str_b1"])
        self.assertEqual([round(v, 2) for v in off], [3.0, 0.0, 3.0])
        self.assertEqual(dyaw, 0)

    def test_engine_euler_order_is_z_then_x_then_y(self):
        f, _, _ = mine.local_frame(ET.fromstring(
            '<e><transform rotation_euler="1.5707963, 0.0, 1.5707963"/></e>'))
        # Rz Rx: local +Z goes to +X after X 90 then Z 90
        col_z = [round(f.m[i][2], 6) + 0.0 for i in range(3)]
        self.assertEqual(col_z, [1.0, 0.0, 0.0])
        # Y before X: under Rx Ry, local +X goes to -Z, then to +Y; Ry Rx would leave it at -Z
        f, _, _ = mine.local_frame(ET.fromstring(
            '<e><transform rotation_euler="1.5707963, 1.5707963, 0.0"/></e>'))
        col_x = [round(f.m[i][0], 6) + 0.0 for i in range(3)]
        self.assertEqual(col_x, [0.0, 1.0, 0.0])


class CatalogTableTests(unittest.TestCase):
    def test_rise_direction_is_opposite_the_normal(self):
        self.assertEqual(cat.rise_dir(-90), "+Y")
        self.assertEqual(cat.rise_dir(0), "-X")
        self.assertEqual(cat.rise_dir(180), "+X")
        self.assertEqual(cat.rise_dir(-45), "-X+Y")

    def test_row_prints_no_negative_zero_and_flags_a_missing_body(self):
        piece = {"idx": 7, "name": "SM_DW_Test", "size": [3.0, 0.5, 3.0], "min": [-1.5, -0.25, -0.002],
                 "max": [1.5, 0.25, 3.0], "pivot": ["mid", "mid", "min"], "top": {"z_mode": -0.001},
                 "tris": 12, "lods": [{"n": "2"}], "apertures": [], "slopes": [],
                 "node": {"identity": True, "rot_deg": [0, 0, 0], "scale": [1, 1, 1]}, "bo": None}
        row = cat.row(piece)
        self.assertNotIn("-0.00", row)
        self.assertIn("0.00 to 3.00", row)
        self.assertIn("**no bo_**", row)
        self.assertIn("lod2", row)


if __name__ == "__main__":
    unittest.main()
