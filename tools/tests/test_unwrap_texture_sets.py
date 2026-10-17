#!/usr/bin/env python3
"""Unit tests for the pure parts of tools/blender/unwrap_texture_sets.py.

Run:  python -m unittest tools.tests.test_unwrap_texture_sets

The script imports bpy, bmesh and mathutils at module level, so stub modules are installed before
the import and `main()` sits behind a `__main__` guard (Blender runs a -P script as __main__). The
Blender side (visibility, seams, packing) is proven by the report every run writes.
"""
import importlib.util
import json
import math
import os
import sys
import types
import unittest

try:
    import numpy  # noqa: F401  (the script imports it at module level)
except ImportError:
    raise unittest.SkipTest("numpy is not installed")

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "unwrap_texture_sets.py")
RULES = os.path.join(TOOLS, "oneoff", "carn_dum_texture_sets.json")
BUILDER = os.path.join(TOOLS, "oneoff", "blender_build_carn_dum_icon.py")


class _Vector(tuple):
    def __new__(cls, xyz):
        return super().__new__(cls, xyz)


for name in ("bpy", "bmesh", "bpy_extras", "bpy_extras.bmesh_utils", "mathutils", "mathutils.bvhtree"):
    sys.modules.setdefault(name, types.ModuleType(name))
sys.modules["bpy_extras.bmesh_utils"].bmesh_linked_uv_islands = lambda *a: []
sys.modules["mathutils"].Vector = getattr(sys.modules["mathutils"], "Vector", _Vector)
sys.modules["mathutils.bvhtree"].BVHTree = object
_spec = importlib.util.spec_from_file_location("unwrap_texture_sets", SCRIPT)
uts = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(uts)


class MatchRuleTests(unittest.TestCase):
    RULES = [{"name": "towers", "materials": ["stone"], "parts": ["towers"]},
             {"name": "walls", "materials": ["stone"]},
             {"name": "paving", "materials": ["paving"]},
             {"name": "rest"}]

    def test_first_matching_rule_wins(self):
        self.assertEqual(uts.match_rule(self.RULES, "stone", "towers"), 0)
        self.assertEqual(uts.match_rule(self.RULES, "stone", "keep"), 1)
        self.assertEqual(uts.match_rule(self.RULES, "paving", "towers"), 2)

    def test_a_rule_with_no_keys_takes_everything_left(self):
        self.assertEqual(uts.match_rule(self.RULES, "roof", "houses"), 3)

    def test_no_match_is_minus_one(self):
        self.assertEqual(uts.match_rule(self.RULES[:3], "roof", "houses"), -1)


class CarnDumRulesTests(unittest.TestCase):
    """The rules file must name exactly the builder's parts and materials, or faces fall through."""

    @staticmethod
    def builder_list(name):
        with open(BUILDER, encoding="utf-8") as f:
            src = f.read()
        start = src.index(f"\n{name} = [") + len(name) + 4
        return json.loads(src[start - 1:src.index("]", start) + 1].replace("\n", " "))

    def test_every_builder_face_matches_a_set(self):
        rules = json.load(open(RULES, encoding="utf-8"))["sets"]
        for mat in self.builder_list("MATS"):
            for part in self.builder_list("PARTS"):
                self.assertGreaterEqual(uts.match_rule(rules, mat, part), 0, (mat, part))

    def test_rule_parts_are_builder_parts(self):
        parts = set(self.builder_list("PARTS"))
        for r in json.load(open(RULES, encoding="utf-8"))["sets"]:
            self.assertLessEqual(set(r.get("parts", [])), parts, r["name"])


class HemisphereTests(unittest.TestCase):
    def test_directions_stay_above_the_floor(self):
        dirs = uts.hemisphere_dirs()
        self.assertEqual(len(dirs), uts.N_DIRS)
        self.assertGreaterEqual(min(d[2] for d in dirs), math.sin(math.radians(uts.MIN_ELEV_DEG)) - 1e-9)


if __name__ == "__main__":
    unittest.main()
