"""Tests for tools/oneoff/kitbash_kits.py: the shared kit table and the pure naming and grouping
logic behind the Erebor and Gondor kitbash tools."""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))  # audit_fbx_lods
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import kitbash_kits as kk  # noqa: E402


class NamingTests(unittest.TestCase):
    def test_strip_dup_removes_only_a_trailing_three_digit_suffix(self):
        self.assertEqual(kk.strip_dup("p.wall.001"), "p.wall")
        self.assertEqual(kk.strip_dup("p.wall.01"), "p.wall.01")
        self.assertEqual(kk.strip_dup("p.001.wall"), "p.001.wall")
        self.assertEqual(kk.strip_dup("p"), "p")

    def test_split_name_matches_the_old_erebor_behaviour(self):
        self.assertEqual(kk.split_name("SM_DW_Wall"), ("base", "SM_DW_Wall", None))
        self.assertEqual(kk.split_name("SM_DW_Wall.lod2"), ("lod", "SM_DW_Wall", "2"))
        self.assertEqual(kk.split_name("SM_DW_Wall_LOD3"), ("lod", "SM_DW_Wall", "3"))
        self.assertEqual(kk.split_name("SM_DW_Wall.lod"), ("lod", "SM_DW_Wall", ""))
        self.assertEqual(kk.split_name("bo_SM_DW_Wall"), ("bo", "SM_DW_Wall", None))
        self.assertEqual(kk.split_name("BO_x"), ("bo", "x", None))
        # the object path never strips a duplicate suffix
        self.assertEqual(kk.split_name("A.001"), ("base", "A.001", None))

    def test_is_decal_is_case_insensitive(self):
        self.assertTrue(kk.is_decal("p.decalleak"))
        self.assertTrue(kk.is_decal("p.DecalLeak.001"))
        self.assertFalse(kk.is_decal("p.wall"))

    def test_part_key(self):
        self.assertEqual(kk.part_key("Gondor_Wall.wall.lod2", "prefix"), "gondor_wall")
        self.assertEqual(kk.part_key("gondor_wall.wall.001", "prefix"), "gondor_wall")
        self.assertEqual(kk.part_key("gondor_wall", "prefix"), "gondor_wall")
        self.assertEqual(kk.part_key("gondor_wall.lod", "prefix"), "gondor_wall")
        self.assertEqual(kk.part_key("SM_DW_Wall.lod2", "object"), "sm_dw_wall")
        self.assertEqual(kk.part_key("bo_SM_DW_Wall", "object"), "sm_dw_wall")


class ObjectGroupingTests(unittest.TestCase):
    def test_base_lods_and_bo_fold_under_one_lowercase_key(self):
        g = kk.group_parts(["A", "A.lod1", "A_lod2", "bo_A", "other"], "object")
        self.assertEqual(sorted(g), ["a", "other"])
        sub = g["a"]["subs"][""]
        self.assertEqual(sub["base"], "A")
        self.assertEqual(sub["lods"], {"1": "A.lod1", "2": "A_lod2"})
        self.assertEqual(g["a"]["bo"], "bo_A")
        self.assertIsNone(g["other"]["bo"])
        self.assertEqual(g["other"]["subs"][""]["lods"], {})

    def test_no_base_promotes_the_first_lod_and_warns(self):
        g = kk.group_parts(["B.lod2", "B.lod1", "B.lod"], "object")
        sub = g["b"]["subs"][""]
        # old sort key: n or "0", stable: the bare lod ("0") sorts first
        self.assertEqual(sub["base"], "B.lod")
        self.assertEqual(sub["lod_pairs"], [("1", "B.lod1"), ("2", "B.lod2")])
        self.assertEqual(g["b"]["warnings"], ["b has no base object; using B.lod"])

    def test_orphan_bo_has_no_base(self):
        g = kk.group_parts(["bo_C"], "object")
        self.assertIsNone(g["c"]["subs"][""]["base"])
        self.assertEqual(g["c"]["bo"], "bo_C")
        self.assertEqual(g["c"]["warnings"], [])

    def test_a_second_base_of_the_same_key_wins_like_before(self):
        g = kk.group_parts(["Wall", "wall"], "object")
        self.assertEqual(g["wall"]["subs"][""]["base"], "wall")


class PrefixGroupingTests(unittest.TestCase):
    NAMES = [
        "p.wall", "p.wall.lod2", "p.wall.lod5", "p.stonebrick", "p.stonebrick.lod3",
        "p.decalleak", "p.door", "bo_p",
        "q.wall.lod",
        "r", "r.lod2", "bo_r",
        "s.wall", "s.wall.001",
    ]

    def setUp(self):
        self.g = kk.group_parts(self.NAMES, "prefix")

    def test_keys_are_lowercase_prefixes(self):
        self.assertEqual(sorted(self.g), ["p", "q", "r", "s"])

    def test_part_family_with_lods_decal_and_body(self):
        p = self.g["p"]
        self.assertEqual(sorted(p["subs"]), ["door", "stonebrick", "wall"])
        self.assertEqual(p["subs"]["wall"]["base"], "p.wall")
        self.assertEqual(p["subs"]["wall"]["lods"], {"2": "p.wall.lod2", "5": "p.wall.lod5"})
        self.assertEqual(p["subs"]["stonebrick"]["lods"], {"3": "p.stonebrick.lod3"})
        self.assertEqual(p["decals"], ["p.decalleak"])
        self.assertEqual(p["bo"], "bo_p")
        self.assertEqual(p["bare_lod"], [])
        self.assertEqual(p["warnings"], [])

    def test_bare_lod_without_a_base_becomes_the_base(self):
        q = self.g["q"]
        self.assertEqual(q["subs"]["wall"]["base"], "q.wall.lod")
        self.assertEqual(q["subs"]["wall"]["lods"], {})
        self.assertEqual(q["bare_lod"], ["q.wall.lod"])
        self.assertIsNone(q["bo"])

    def test_prefix_alone_is_the_empty_sub(self):
        r = self.g["r"]
        self.assertEqual(list(r["subs"]), [""])
        self.assertEqual(r["subs"][""]["base"], "r")
        self.assertEqual(r["subs"][""]["lods"], {"2": "r.lod2"})
        self.assertEqual(r["bo"], "bo_r")

    def test_duplicate_suffix_is_stripped_and_the_plain_name_kept(self):
        s = self.g["s"]
        self.assertEqual(list(s["subs"]), ["wall"])
        self.assertEqual(s["subs"]["wall"]["base"], "s.wall")
        self.assertEqual(sum("s.wall.001" in w for w in s["warnings"]), 1)

    def test_a_dup_only_part_still_groups(self):
        g = kk.group_parts(["t.wall.001", "t.wall.lod2.001"], "prefix")
        self.assertEqual(g["t"]["subs"]["wall"]["base"], "t.wall.001")
        self.assertEqual(g["t"]["subs"]["wall"]["lods"], {"2": "t.wall.lod2.001"})

    def test_decals_are_never_subs(self):
        g = kk.group_parts(["u.wall", "u.DecalMoss", "u.decal.lod2"], "prefix")
        self.assertEqual(list(g["u"]["subs"]), ["wall"])
        self.assertEqual(g["u"]["decals"], ["u.DecalMoss", "u.decal.lod2"])


class KitTableTests(unittest.TestCase):
    def test_erebor_paths_are_the_ones_the_tools_always_used(self):
        e = kk.KITS["erebor"]
        self.assertEqual(e["catalog_json"],
                         r"E:\LOTRAOMAssets\_export\erebor\catalog\erebor_catalog.json")
        self.assertEqual(e["folders"], ["meshes", "walls", "statues"])
        self.assertEqual(e["group_by"], "object")
        self.assertTrue(e["kit_re"].match("SM_DW_Wall"))
        self.assertTrue(e["kit_re"].match("kys_dw_x"))
        self.assertFalse(e["kit_re"].match("other"))
        self.assertEqual(e["families"][0][0], "Ground tiles and floor trim")

    def test_gondor_kit(self):
        g = kk.KITS["gondor"]
        self.assertEqual(g["catalog_json"],
                         r"E:\LOTRAOMAssets\_export\gondor\catalog\gondor_catalog.json")
        self.assertEqual(g["group_by"], "prefix")
        self.assertIsNone(g["kit_re"])
        self.assertNotIn("blockout", g["folders"])
        self.assertIn(r"meshes\gate bak.fbx", g["exclude_files"])
        self.assertIn(r"meshes\VEG_Grass_Tussock_A.fbx", g["exclude_files"])
        self.assertTrue(g["export_dir"].endswith(r"Scenes\Gondor\buildings_taom"))

    def test_is_excluded(self):
        g = kk.KITS["gondor"]
        self.assertTrue(kk.is_excluded(g, "meshes", "Gate Bak.fbx"))
        self.assertFalse(kk.is_excluded(g, "walls", "gate bak.fbx"))

    def test_kit_matcher_uses_kit_re_or_the_part_names(self):
        e = kk.kit_matcher("erebor")
        self.assertTrue(e("sm_dw_wall_3m_c"))
        self.assertFalse(e("gondor_wall"))
        orig = kk.kit_part_names
        kk.kit_part_names = lambda kit: {"gondor_wall_3m"}
        try:
            g = kk.kit_matcher("gondor")
            self.assertTrue(g("Gondor_Wall_3m"))
            self.assertFalse(g("sm_dw_wall"))
        finally:
            kk.kit_part_names = orig


class DuplicateAndBaseNameTests(unittest.TestCase):
    def test_a_dot_nnn_copy_never_overrides_the_plain_body_or_lod(self):
        for names in (["p.wall", "p.wall.lod2", "p.wall.lod2.003", "bo_p", "bo_p.001"],
                      ["bo_p.001", "p.wall.lod2.003", "bo_p", "p.wall.lod2", "p.wall"]):
            g = kk.group_parts(names, "prefix")["p"]
            self.assertEqual(g["bo"], "bo_p")
            self.assertEqual(g["subs"]["wall"]["lods"], {"2": "p.wall.lod2"})
            self.assertEqual(len(g["subs"]["wall"]["lod_pairs"]), 1)
            self.assertEqual(sum("ignored" in w for w in g["warnings"]), 2)

    def test_two_bases_of_one_sub_keep_the_first_in_name_order_and_warn(self):
        # neither is the plain name, so the .NNN pre-pass keeps both and the collision rule decides
        for names in (["t.wall.002", "t.wall.001"], ["t.wall.001", "t.wall.002"]):
            g = kk.group_parts(names, "prefix")["t"]
            self.assertEqual(g["subs"]["wall"]["base"], "t.wall.001")
            self.assertEqual(sum("duplicate base" in w for w in g["warnings"]), 1)

    def test_numbered_lod_only_sub_promotes_the_lowest_and_warns(self):
        g = kk.group_parts(["d.wall.lod4", "d.wall.lod2", "bo_d"], "prefix")["d"]
        self.assertEqual(g["subs"]["wall"]["base"], "d.wall.lod2")
        self.assertEqual(g["subs"]["wall"]["lods"], {"4": "d.wall.lod4"})
        self.assertEqual(sum("no base object" in w for w in g["warnings"]), 1)

    def test_bare_lod_beside_a_base_stays_a_lod_and_warns(self):
        # the engine reads a bare .lod as level 0, so beside a base both draw at LOD0
        g = kk.group_parts(["e.wall", "e.wall.lod", "bo_e"], "prefix")["e"]
        self.assertEqual(g["subs"]["wall"]["base"], "e.wall")
        self.assertEqual(g["subs"]["wall"]["lods"], {"": "e.wall.lod"})
        self.assertEqual(sum("level 0" in w for w in g["warnings"]), 1)

    def test_prefix_grouping_does_not_depend_on_the_import_order(self):
        # Blender's native FBX importer creates objects in a varying order (measured: decal lists
        # changed order between two catalog runs of the same file)
        names = ["p.wall", "p.wall.lod4", "p.wall.lod2", "p.decalleak", "p.decalleak.lod2",
                 "p.decalleak.lod4", "bo_p", "p.stonebrick.lod2.001", "p.stonebrick", "p.stonebrick.lod2",
                 "p.stonebrick.lod2.003"]
        import json
        want = json.dumps(kk.group_parts(names, "prefix"), sort_keys=True)
        for order in (list(reversed(names)), names[5:] + names[:5], names[::2] + names[1::2]):
            self.assertEqual(json.dumps(kk.group_parts(order, "prefix"), sort_keys=True), want)

    def test_base_names_keeps_every_sub_base_including_a_bare_lod(self):
        names = ["p.wall.lod", "p.wall.lod2", "p.stonebrick", "p.decalleak", "bo_p",
                 "d.lod4", "s.wall", "s.wall.001"]
        self.assertEqual(kk.base_names(names, "prefix"), {"p.wall.lod", "p.stonebrick", "d.lod4", "s.wall"})

    def test_base_names_for_an_object_kit_is_the_old_loader_rule(self):
        self.assertEqual(kk.base_names(["A", "A.lod1", "bo_A", "B.lod2"], "object"), {"A"})


class KitHelperTests(unittest.TestCase):
    def test_get_kit_ignores_case_and_rejects_an_unknown_kit(self):
        self.assertIs(kk.get_kit("Gondor"), kk.KITS["gondor"])
        with self.assertRaises(ValueError):
            kk.get_kit("mordor")

    def test_catalog_name_keeps_the_erebor_names(self):
        e, g = kk.KITS["erebor"], kk.KITS["gondor"]
        self.assertEqual(kk.catalog_name(e, "erebor", None, None), "erebor_catalog.json")
        self.assertEqual(kk.catalog_name(e, "erebor", None, "Wall"), "erebor_catalog_only_Wall.json")
        self.assertEqual(kk.catalog_name(g, "gondor", "walls", None), "gondor_catalog_walls.json")
        self.assertEqual(kk.catalog_name(g, "gondor", "walls", "tower"), "gondor_catalog_walls_only_tower.json")

    def test_report_prefix_is_empty_only_for_a_whole_kit_run(self):
        e, g = kk.KITS["erebor"], kk.KITS["gondor"]
        self.assertEqual(kk.report_prefix(e, "erebor", None, None), "")
        self.assertEqual(kk.report_prefix(g, "gondor", "walls", None), "gondor_catalog_walls_")
        self.assertEqual(kk.report_prefix(e, "erebor", None, "Wall"), "erebor_catalog_only_Wall_")

    def test_pick_structural_prefers_wall_then_the_bare_prefix_then_the_largest(self):
        def box(sub, d):
            return {"sub": sub, "bbox": {"min": [0, 0, 0], "max": [d, d, d]}}
        self.assertEqual(kk.pick_structural([box("stonebrick", 9), box("wall", 1), box("", 2)])["sub"], "wall")
        self.assertEqual(kk.pick_structural([box("stonebrick", 9), box("", 2)])["sub"], "")
        self.assertEqual(kk.pick_structural([box("a", 1), box("b", 3)])["sub"], "b")

    def test_fbx_export_settings_are_the_ones_the_kit_import_was_proved_with(self):
        self.assertEqual(kk.FBX_EXPORT, {"use_selection": True, "object_types": {"MESH"},
                                         "use_mesh_modifiers": True, "bake_space_transform": True,
                                         "add_leaf_bones": False, "path_mode": "AUTO"})

    def test_kit_part_names_reads_the_kit_folders_and_skips_a_missing_one(self):
        import shutil
        import tempfile
        import types
        import audit_fbx_lods
        names = {"a.fbx": ["Gondor_Wall_A.wall", "bo_gondor_wall_a", "gondor_wall_a.decalleak"],
                 "gate bak.fbx": ["old_gate"], "c.FBX": ["Gondor_Roof_B.lod2"]}
        root = tempfile.mkdtemp()
        orig = audit_fbx_lods.read_fbx_meshes
        try:
            os.makedirs(os.path.join(root, "meshes"))
            for fn in names:
                open(os.path.join(root, "meshes", fn), "wb").close()
            audit_fbx_lods.read_fbx_meshes = lambda p: [types.SimpleNamespace(name=n)
                                                        for n in names[os.path.basename(p)]]
            kk.KITS["_tmp"] = dict(kk.KITS["gondor"], src=root, kit_folders=["meshes", "walls"])
            self.assertEqual(kk.kit_part_names("_tmp"), {"gondor_wall_a", "gondor_roof_b"})
        finally:
            audit_fbx_lods.read_fbx_meshes = orig
            kk.KITS.pop("_tmp", None)
            shutil.rmtree(root)


class ToolHelperTests(unittest.TestCase):
    """Pure helpers of the --kit tools that the Erebor tests do not reach."""

    @classmethod
    def setUpClass(cls):
        import erebor_catalog_to_md
        import mine_erebor_prefab_joins
        cls.cat, cls.mine = erebor_catalog_to_md, mine_erebor_prefab_joins

    def test_a_kit_predicate_reaches_nested_children(self):
        import xml.etree.ElementTree as ET
        xml = """<game_entity name="a">
          <components><meta_mesh_component name="gondor_wall_a"/></components>
          <children><game_entity name="b"><children><game_entity name="c">
            <components><meta_mesh_component name="Gondor_Roof_B"/>
                        <meta_mesh_component name="sm_dw_wall_3m_c"/></components>
          </game_entity></children></game_entity></children></game_entity>"""
        out = []
        self.mine.walk(ET.fromstring(xml), self.mine.Frame(), "t:a", out, "a",
                       lambda n: n.startswith("gondor_"))
        self.assertEqual(sorted(p["piece"] for p in out), ["gondor_roof_b", "gondor_wall_a"])

    def test_subs_cell_lists_each_sub_object_with_its_ladder(self):
        p = {"subs": [{"sub": "", "lods": [{"n": "2"}]}, {"sub": "wall", "lods": []},
                      {"sub": "x", "lods": [{"n": ""}, {"n": "4"}]}]}
        self.assertEqual(self.cat.subs_cell(p), "3: (base) [lod2], wall [-], x [lod/lod4]")

    def test_family_target_matches_on_the_name_or_on_folder_and_file(self):
        p = {"name": "gondor_roof_a_30_3m_straight", "folder": "meshes", "file": "gondor_roofs_a"}
        self.assertEqual(self.cat.family_target(p, "folder_file"), "meshes/gondor_roofs_a")
        self.assertEqual(self.cat.family_target(p, "name"), "gondor_roof_a_30_3m_straight")


if __name__ == "__main__":
    unittest.main()
