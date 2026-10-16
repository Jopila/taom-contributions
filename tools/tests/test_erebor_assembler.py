"""Tests for the pure parts of tools/oneoff/blender_assemble_erebor_buildings.py (issue #797).

Run:  python -m unittest tools.tests.test_erebor_assembler

The script imports bpy and mathutils at module level, so stub modules are installed before the
import and `main()` is behind a `__main__` guard. Blender-side behaviour (import, join, export)
is proven by the probe of the exported FBX; these cover the rules that decide what is exported:
the kit name split, the LOD tier choice, the planned openings, the probe layout, the export
refusals, the building specs and the run modes.
"""
import importlib.util
import json
import os
import shutil
import sys
import tempfile
import types
import unittest
from collections import Counter
from unittest import mock

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "oneoff", "blender_assemble_erebor_buildings.py")

# Stubs go in sys.modules for the load only (as test_export_blend_pack.py does), so no later
# test inherits them.
_mu = types.ModuleType("mathutils")
_mu.Matrix = type("Matrix", (), {})
_mu.Vector = type("Vector", (), {})
with mock.patch.dict(sys.modules, {"bpy": types.ModuleType("bpy"), "mathutils": _mu}):
    _spec = importlib.util.spec_from_file_location("blender_assemble_erebor_buildings", SCRIPT)
    asm = importlib.util.module_from_spec(_spec)
    _spec.loader.exec_module(asm)

# LAYOUT as it stood before eave, half sizes and chamfer were derived from the walls.
DECLARED = {
    "erebor_dwarf_house_01": {"eave": 3.0, "half": [4.5, 3.0], "chamfer": 0.0},
    "erebor_great_hall_01": {"eave": 6.0, "half": [10.5, 6.0], "chamfer": 3.0},
    "erebor_barracks_01": {"eave": 6.0, "half": [12.0, 6.0], "chamfer": 0.0},
}


def entry(lods=(), base="base", bo=None):
    return {"base": base, "lods": {n: f"lod{n}" for n in lods}, "bo": bo}


class KitNameSplitTests(unittest.TestCase):
    def test_numbered_lods_bodies_and_bases(self):
        self.assertEqual(asm.split_kit_name("sm_dw_wall_3m_a.lod2"), ("lod", "sm_dw_wall_3m_a", "2"))
        self.assertEqual(asm.split_kit_name("dw_pillar_moria_01_lod3"), ("lod", "dw_pillar_moria_01", "3"))
        self.assertEqual(asm.split_kit_name("bo_sm_dw_wall_3m_a"), ("bo", "sm_dw_wall_3m_a", None))
        self.assertEqual(asm.split_kit_name("sm_dw_wall_3m_a"), ("base", "sm_dw_wall_3m_a", None))

    def test_bare_lod_is_a_base_object(self):
        """lond-cirion-buildings.md rule 3: a gable plate named `.wall.lod` with no plain
        sibling is the only copy of that geometry; filing it as a LOD left the gable open."""
        self.assertEqual(asm.split_kit_name("gondor_roof_side.wall.lod"),
                         ("base", "gondor_roof_side.wall", None))


class TierMeshTests(unittest.TestCase):
    def test_ladders(self):
        e = entry(("2", "4", "5"))
        self.assertEqual(asm.tier_mesh(e, "sm_dw_x", "base"), ("base", "lod0"))
        self.assertEqual(asm.tier_mesh(e, "sm_dw_x", "lod3"), ("lod2", "lod2"))
        self.assertEqual(asm.tier_mesh(e, "sm_dw_x", "lod6"), ("lod5", "lod5"))
        self.assertEqual(asm.tier_mesh(entry(("4",)), "sm_dw_x", "lod3"), ("lod4", "lod4"))
        self.assertEqual(asm.tier_mesh(entry(()), "sm_dw_x", "lod6"), ("base", "lod0"))

    def test_props_use_lod2_as_base(self):
        e = entry(("1", "2", "3", "4"))
        self.assertEqual(asm.tier_mesh(e, "sd_dw_ih_statue_a", "base"), ("lod2", "lod2"))

    def test_base_lod_never_lets_a_lower_tier_be_more_detailed(self):
        e = entry(("2", "3", "4"))
        self.assertEqual(asm.tier_mesh(e, "sd_dw_ih_statue_a", "base", "3"), ("lod3", "lod3"))
        self.assertEqual(asm.tier_mesh(e, "sd_dw_ih_statue_a", "lod3", "3"), ("lod3", "lod3"))
        self.assertEqual(asm.tier_mesh(e, "sd_dw_ih_statue_a", "lod6", "3"), ("lod4", "lod4"))
        self.assertEqual(asm.tier_mesh(entry(("2",)), "sm_dw_ground_3m_a2", "lod6", "2"), ("lod2", "lod2"))

    def test_base_lod_the_piece_lacks_is_refused(self):
        with self.assertRaises(ValueError):
            asm.tier_mesh(entry(("2",)), "sm_dw_x", "base", "3")


class BodyTests(unittest.TestCase):
    def test_own_borrowed_none_and_missing(self):
        kit = {"a": entry(bo="boA"), "sm_dw_roof_str_a1": entry(bo="boStr"), "sm_dw_roof_str_b1": entry()}
        self.assertEqual(asm.bo_mesh(kit, "a"), ("boA", "own"))
        self.assertEqual(asm.bo_mesh(kit, "sm_dw_roof_str_b1")[0], "boStr")
        self.assertEqual(asm.bo_mesh(kit, "sm_dw_roof_side_str_a1"), (None, "none by design"))
        self.assertEqual(asm.bo_mesh({"x": entry()}, "x"), (None, "MISSING"))

    def test_ramp_merlon_01_finds_its_truncated_body(self):
        """The kit names this body bo_SM_DW_Castle_Wall_A1_Ramp_Mrln_0 (truncated)."""
        kit = {"sm_dw_castle_wall_a1_ramp_mrln_01": entry(),
               "sm_dw_castle_wall_a1_ramp_mrln_0": {"base": None, "lods": {}, "bo": "boTrunc"}}
        self.assertEqual(asm.bo_mesh(kit, "sm_dw_castle_wall_a1_ramp_mrln_01")[0], "boTrunc")


class PlannedOpeningsTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.dir, True)
        cat = {"pieces": [{"name": "SM_DW_Test_Win", "apertures": [
            {"through": "Y", "u_min": 0.2, "u_max": 0.6, "z_min": 1.0, "z_max": 2.0, "area": 0.4},
            {"through": "X", "u_min": -1, "u_max": 1, "z_min": 0, "z_max": 1, "area": 2}]}]}
        path = os.path.join(self.dir, "cat.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(cat, f)
        patcher = mock.patch.object(asm, "CATALOG", path)
        patcher.start()
        self.addCleanup(patcher.stop)

    def one(self, yaw, loc=(10.0, 20.0, 3.0)):
        ops = asm.planned_openings([asm.P("sm_dw_test_win", loc, yaw, role="wall")])
        self.assertEqual(len(ops), 1)  # the X-through aperture is not a wall opening
        return ops[0]

    def test_each_wall_yaw_maps_the_local_span_to_the_world(self):
        # local u 0.2..0.6 is +X of the pivot; Rz(yaw) carries local +X to +X, +Y, -X, -Y
        o = self.one(0)
        self.assertEqual((o["runs_along"], o["plane"], o["u_min"], o["u_max"]), ("x", 20.0, 10.2, 10.6))
        o = self.one(90)
        self.assertEqual((o["runs_along"], o["plane"], o["u_min"], o["u_max"]), ("y", 10.0, 20.2, 20.6))
        o = self.one(180)
        self.assertEqual((o["runs_along"], o["plane"], o["u_min"], o["u_max"]), ("x", 20.0, 9.4, 9.8))
        o = self.one(270)
        self.assertEqual((o["runs_along"], o["plane"], o["u_min"], o["u_max"]), ("y", 10.0, 19.4, 19.8))
        self.assertEqual((o["z_min"], o["z_max"]), (4.0, 5.0))

    def test_a_diagonal_wall_is_refused(self):
        with self.assertRaises(ValueError):
            asm.planned_openings([asm.P("sm_dw_test_win", (0, 0, 0), 45, role="wall")])


class ProbeLayoutTests(unittest.TestCase):
    def test_derived_layout_matches_the_declared_values(self):
        with mock.patch.object(asm, "planned_openings", lambda spec: []):
            for name, want in DECLARED.items():
                got = asm.probe_layout(name, asm.BUILDINGS[name]())
                self.assertEqual(got["eave"], want["eave"], name)
                self.assertEqual(got["half"], want["half"], name)
                self.assertEqual(got["chamfer"], want["chamfer"], name)

    def test_gables_carry_their_ridge_height(self):
        with mock.patch.object(asm, "planned_openings", lambda spec: []):
            self.assertEqual(asm.probe_layout("erebor_dwarf_house_01", asm.dwarf_house_01())["ridge_z"], 4.5)
            self.assertEqual(asm.probe_layout("erebor_barracks_01", asm.barracks_01())["ridge_z"], 9.0)
            hall = asm.probe_layout("erebor_great_hall_01", asm.great_hall_01())
            self.assertEqual(hall["roof"], "hip")
            self.assertEqual(len(hall["chamfers"]), 8)
            self.assertEqual(sum(c["window"] for c in hall["chamfers"]), 4)


class ExportProblemTests(unittest.TestCase):
    def stats(self, tris=10, expect=10, mats=("M_DW_Trim_A1",)):
        return {"x": {"tris": tris, "expected_sum": expect, "materials": list(mats)}}

    def test_clean_export_has_no_problems(self):
        self.assertEqual(asm.export_problems(self.stats(), []), [])

    def test_each_problem_is_named(self):
        self.assertIn("tris 9 != part sum 10", " ".join(asm.export_problems(self.stats(tris=9), [])))
        self.assertIn("M_DW_Trim_A1.001", " ".join(asm.export_problems(self.stats(mats=("M_DW_Trim_A1.001",)), [])))
        self.assertIn("sm_dw_x", " ".join(asm.export_problems(self.stats(), ["sm_dw_x"])))


class ExportRefusalTests(unittest.TestCase):
    def test_missing_piece_leaves_the_live_folder_untouched(self):
        live = tempfile.mkdtemp()
        out = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, live, True)
        self.addCleanup(shutil.rmtree, out, True)
        fbx = os.path.join(live, "erebor_dwarf_house_01.fbx")
        with open(fbx, "wb") as f:
            f.write(b"approved in the Kit")
        with mock.patch.object(asm, "EXPORT_DIR", live):
            with self.assertRaises(RuntimeError):
                asm.export("erebor_dwarf_house_01", asm.dwarf_house_01(), {}, out)
        self.assertEqual(sorted(os.listdir(live)), ["erebor_dwarf_house_01.fbx"])
        with open(fbx, "rb") as f:
            self.assertEqual(f.read(), b"approved in the Kit")

    def test_staging_in_the_live_folder_is_refused(self):
        """--out equal to the live folder would stage over the live FBX and back up the new bytes."""
        live = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, live, True)
        fbx = os.path.join(live, "erebor_dwarf_house_01.fbx")
        with open(fbx, "wb") as f:
            f.write(b"approved in the Kit")
        with mock.patch.object(asm, "EXPORT_DIR", live):
            with self.assertRaisesRegex(RuntimeError, "--out"):
                asm.export("erebor_dwarf_house_01", asm.dwarf_house_01(), {}, live + os.sep)
        self.assertEqual(sorted(os.listdir(live)), ["erebor_dwarf_house_01.fbx"])


class SpecTests(unittest.TestCase):
    """Mike's review decisions (2026-10-15), pinned for every building."""

    COUNTS = {"erebor_dwarf_house_01": 67, "erebor_great_hall_01": 247, "erebor_barracks_01": 236}

    def test_placement_counts(self):
        for name, n in self.COUNTS.items():
            self.assertEqual(len(asm.BUILDINGS[name]()), n, name)

    def test_review_rules(self):
        for name, build in asm.BUILDINGS.items():
            spec = build()
            walls = [p for p in spec if p["role"] in ("wall", "chamfer")]
            self.assertEqual(min(p["loc"][2] for p in walls), 0.0, f"{name}: no wall below grade")
            for p in spec:
                self.assertFalse(p["piece"].startswith(("sd_dw_", "kys_dw_", "dw_pillar")), f"{name}: no props")
            floors = [p["piece"] for p in spec if p["role"] == "floor"]
            self.assertTrue(floors and all(f.startswith("sm_dw_ground_3m_a") for f in floors),
                            f"{name}: stone floor tiles")
            caps = Counter((p["loc"][0], p["loc"][1]) for p in spec if p["piece"] == "sm_dw_wall_clmn_top_d")
            for p in spec:
                if p["piece"] == "sm_dw_wall_clmn_3m_c":
                    self.assertEqual(caps[(p["loc"][0], p["loc"][1])], 2, f"{name}: base and cap at {p['loc']}")


class MainModeTests(unittest.TestCase):
    def run_main(self, *flags, out_eq=False, stale=False):
        out = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, out, True)
        done = os.path.join(out, "DONE.txt")
        if stale:
            with open(done, "w", encoding="utf-8") as f:
                json.dump({"status": "ok"}, f)
        calls = []

        def record(mode):
            # a stale verdict must be gone before any mode runs
            return lambda *a: calls.append(mode if not os.path.exists(done) else mode + " with stale DONE")
        out_args = [f"--out={out}"] if out_eq else ["--out", out]
        argv = ["blender", "-b", "--", "--building", "erebor_dwarf_house_01", *out_args, *flags]
        with mock.patch.object(sys, "argv", argv), \
                mock.patch.object(asm, "OUT", None), mock.patch.object(asm, "LOG", []), \
                mock.patch.object(asm, "load_kit", lambda: {}), \
                mock.patch.object(asm, "preview", record("preview")), \
                mock.patch.object(asm, "export", record("export")), \
                mock.patch.object(asm, "inspect", record("inspect")):
            asm.main()
        with open(done, encoding="utf-8") as f:
            return calls, json.load(f)["status"]

    def test_modes(self):
        self.assertEqual(self.run_main(), (["preview"], "ok"))
        self.assertEqual(self.run_main("--export"), (["export"], "ok"))
        self.assertEqual(self.run_main("--preview", "--export"), (["preview", "export"], "ok"))
        self.assertEqual(self.run_main("--inspect"), (["inspect"], "ok"))

    def test_inspect_runs_alone(self):
        """--inspect reads the live FBX, so beside --export it would inspect the previous one."""
        self.assertEqual(self.run_main("--export", "--inspect"), ([], "failed"))

    def test_a_stale_verdict_is_cleared_for_either_out_spelling(self):
        self.assertEqual(self.run_main(stale=True), (["preview"], "ok"))
        self.assertEqual(self.run_main(stale=True, out_eq=True), (["preview"], "ok"))


if __name__ == "__main__":
    unittest.main()
