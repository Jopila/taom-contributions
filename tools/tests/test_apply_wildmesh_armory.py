#!/usr/bin/env python3
"""Unit tests for the WildMesh3D mounts armory writer (tools/apply_wildmesh_armory.py, #794).

Pure stdlib, synthetic game dir, no real install needed (--game-dir points the whole script at a temp tree).
Sibling: test_apply_animalia_armory.py. Every case builds its own pristine tree, because the script mutates
files in place and a shared fixture would let one test's writes leak into the next.
"""
import io
import os
import re
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import apply_wildmesh_armory as wm  # noqa: E402

BOM = b"\xef\xbb\xbf"
ALL_HORSE_ACTIONS = sorted({action for overrides in wm.OVERRIDES.values() for action in overrides})

ARMORY_ACTION_SETS = (
    "<action_sets>\r\n"
    "\t<!-- ============================== WARG ============================== -->\r\n"
    "\t<action_set id=\"as_warg\" skeleton=\"warg_skeleton\" base_set=\"as_horse\" />\r\n"
    "\t<!-- ============================== /ANIMALIA ============================== -->\r\n"
    "\t<!-- ============================== /WARG ============================== -->\r\n"
    "</action_sets>\r\n"
)
ARMORY_HORSES = (
    "<Items>\r\n"
    "  <Item\r\n"
    "      id=\"taom_animalia_moose_a\"\r\n"
    "      name=\"{=taom_animalia_moose_a}Mirkwood Moose\"\r\n"
    "      mesh=\"animalia_moose_big\"\r\n"
    "      Type=\"Horse\">\r\n"
    "      <ItemComponent><Horse monster=\"Monster.taom_animalia_moose\" body_length=\"100\" /></ItemComponent>\r\n"
    "  </Item>\r\n"
    "</Items>\r\n"
)
ARMORY_SUBMODULE = (
    "<Module>\r\n"
    "\t<Xmls>\r\n"
    "\t\t<XmlNode>\r\n"
    "\t\t\t<XmlName id=\"Monsters\" path=\"Monsters/LOTR/lotr_monster_animalia\"/>\r\n"
    "\t\t\t<IncludedGameTypes>\r\n"
    "\t\t\t\t<GameType value = \"Campaign\"/>\r\n"
    "\t\t\t</IncludedGameTypes>\r\n"
    "\t\t</XmlNode>\r\n"
    "\t</Xmls>\r\n"
    "</Module>\r\n"
)
TRACKED = [
    "Modules/LOTRLOME_Armory/SubModule.xml",
    "Modules/LOTRLOME_Armory/ModuleData/action_sets.xml",
    "Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml",
    "Modules/LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_wildmesh.xml",
]


def _native_action_sets_xml(actions=ALL_HORSE_ACTIONS):
    rows = "".join('\t\t<action type="%s" animation="horse_template_%d" alternative_group="%d" />\n' % (a, i, i % 3)
                   for i, a in enumerate(actions))
    return ("<action_sets>\n\t<action_set id=\"as_horse\" skeleton=\"horse_skeleton\">\n" + rows
            + "\t</action_set>\n</action_sets>\n")


def _write_bom_crlf(path: Path, text: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(BOM + text.encode("utf-8"))


def _build(root: Path, clips=None, native_actions=ALL_HORSE_ACTIONS):
    native = root / "Modules" / "Native" / "ModuleData" / "action_sets.xml"
    native.parent.mkdir(parents=True, exist_ok=True)
    native.write_text(_native_action_sets_xml(native_actions), encoding="utf-8")
    armory = root / "Modules" / "LOTRLOME_Armory"
    _write_bom_crlf(armory / "ModuleData" / "Monsters" / "LOTR" / "lotr_monster_animalia.xml",
                    "<Monsters>\r\n\t<Monster id=\"taom_animalia_moose\" base_monster=\"horse\" />\r\n</Monsters>\r\n")
    _write_bom_crlf(armory / "SubModule.xml", ARMORY_SUBMODULE)
    _write_bom_crlf(armory / "ModuleData" / "action_sets.xml", ARMORY_ACTION_SETS)
    _write_bom_crlf(armory / "ModuleData" / "LOTRLOME_items" / "LOTRAOM_horses.xml", ARMORY_HORSES)
    for clip in (wm.wanted_clips() if clips is None else clips):
        animal = clip.split("_")[2]
        d = armory / "Assets" / "creature" / "creatures" / "animations" / animal
        d.mkdir(parents=True, exist_ok=True)
        (d / (clip + "_anm.tpac")).write_bytes(b"placeholder")


def _snapshot(root: Path):
    return {rel: ((root / rel).read_bytes() if (root / rel).is_file() else None) for rel in TRACKED}


def _backups(root: Path):
    return sorted(str(p.relative_to(root)) for p in (root / "Modules" / "LOTRLOME_Armory").rglob("*") if ".bak" in p.name)


class CliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _run(self, *argv, running=False):
        buf = io.StringIO()
        with redirect_stdout(buf), mock.patch.object(wm, "game_or_kit_running", return_value=running):
            code = wm.main(list(argv) + ["--game-dir", str(self.root)])
        return code, buf.getvalue()

    def test_dry_run_reports_four_steps_and_writes_nothing(self):
        _build(self.root)
        before = _snapshot(self.root)
        code, out = self._run()
        self.assertEqual(code, 0, out)
        for marker in ("Monsters: new lotr_monster_wildmesh.xml", "SubModule.xml: Monsters block inserted",
                       "action_sets.xml: as_wildmesh_boar", "LOTRAOM_horses.xml: 5 items", "dry run"):
            self.assertIn(marker, out)
        self.assertEqual(before, _snapshot(self.root))
        self.assertEqual(_backups(self.root), [])

    def test_apply_writes_the_approved_recipe_and_a_second_apply_changes_no_byte(self):
        _build(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        md = self.root / "Modules/LOTRLOME_Armory/ModuleData"
        monsters = (md / "Monsters/LOTR/lotr_monster_wildmesh.xml").read_text(encoding="utf-8")
        sizes = dict(re.findall(r'id="(taom_wildmesh_\w+)"[^/]*?taom_body_length="(\d+)"', monsters, re.S))
        self.assertEqual(sizes, {"taom_wildmesh_boar": "100", "taom_wildmesh_warthog": "100",
                                 "taom_wildmesh_mountaingoat": "100", "taom_wildmesh_deer": "100",
                                 "taom_wildmesh_moose": "150"})
        horses = (md / "LOTRLOME_items/LOTRAOM_horses.xml").read_bytes()
        self.assertTrue(horses.startswith(BOM))
        text = horses.decode("utf-8")
        self.assertNotRegex(text, r"(?<!\r)\n", "a bare LF entered a CRLF file")
        for animal, name in (("boar", "War Boar"), ("warthog", "Bristleback Boar"), ("mountaingoat", "Iron Hills Goat"),
                             ("deer", "Mirkwood Stag"), ("moose", "Greenwood Moose")):
            m = re.search(r'<Item\s+id="taom_wildmesh_%s_a".*?</Item>' % animal, text, re.S)
            self.assertIsNotNone(m, animal)
            self.assertIn('name="{=taom_wildmesh_%s_a}%s"' % (animal, name), m.group(0))
            self.assertIn('mesh="wildmesh_%s"' % animal, m.group(0))
            self.assertIn('monster="Monster.taom_wildmesh_%s"' % animal, m.group(0))
            self.assertIn('body_length="100"', m.group(0), "the placeholder Items.xsd requires")
        before = _snapshot(self.root)
        code2, out2 = self._run("--apply")
        self.assertEqual(code2, 0, out2)
        self.assertIn("nothing to write", out2)
        self.assertEqual(before, _snapshot(self.root))

    def test_borrowed_gaits_bind_the_lenders_clips(self):
        _build(self.root)
        self.assertEqual(self._run("--apply")[0], 0)
        sets = (self.root / "Modules/LOTRLOME_Armory/ModuleData/action_sets.xml").read_text(encoding="utf-8")

        def body(sid):
            return re.search(r'<action_set id="%s" [^>]*>(.*?)</action_set>' % sid, sets, re.S).group(1)
        self.assertIn('type="act_horse_forward_canter" animation="anim_wildmesh_boar_runslow_f"', body("as_wildmesh_warthog"))
        self.assertIn('type="act_horse_forward_gallop_left_foot" animation="anim_wildmesh_boar_runfast_f"', body("as_wildmesh_warthog"))
        self.assertIn('type="act_horse_forward_gallop_right_foot_stand" animation="anim_wildmesh_deer_runfast_f_stand"',
                      body("as_wildmesh_mountaingoat"))
        for animal in wm.ANIMALS:
            for twin in ("_town_and_village", "_map"):
                self.assertIn('id="as_wildmesh_%s%s"' % (animal, twin), sets)

    def test_each_override_keeps_the_vanilla_attributes(self):
        _build(self.root)
        self.assertEqual(self._run("--apply")[0], 0)
        sets = (self.root / "Modules/LOTRLOME_Armory/ModuleData/action_sets.xml").read_text(encoding="utf-8")
        self.assertRegex(sets, r'<action type="act_horse_forward_walk" animation="anim_wildmesh_boar_walkmedium_f" alternative_group="\d" />')

    def test_a_missing_clip_refuses_and_writes_nothing(self):
        gone = "anim_wildmesh_deer_runfast_f"
        _build(self.root, clips=wm.wanted_clips() - {gone})
        before = _snapshot(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 1, out)
        self.assertIn("CLIP MISSING: %s" % gone, out)
        self.assertEqual(before, _snapshot(self.root))
        self.assertEqual(_backups(self.root), [])

    def test_an_override_that_is_not_an_as_horse_action_refuses(self):
        _build(self.root, native_actions=[a for a in ALL_HORSE_ACTIONS if a != "act_horse_strike_front"])
        before = _snapshot(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 1, out)
        self.assertIn("NOT AN ACTION: ", out)
        self.assertEqual(before, _snapshot(self.root))

    def test_missing_anchors_refuse_and_name_what_is_missing(self):
        for rel, old, expect in (
                ("Modules/LOTRLOME_Armory/SubModule.xml", "lotr_monster_animalia", "lotr_monster_animalia"),
                ("Modules/LOTRLOME_Armory/ModuleData/action_sets.xml", " /WARG ", "/WARG"),
                ("Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml", 'id="taom_animalia_moose_a"',
                 "taom_animalia_moose_a")):
            with self.subTest(rel=rel):
                self.tearDown()
                self.setUp()
                _build(self.root)
                p = self.root / rel
                p.write_bytes(p.read_bytes().replace(old.encode(), b"gone_for_the_test"))
                before = _snapshot(self.root)
                code, out = self._run("--apply")
                self.assertEqual(code, 1, out)
                self.assertIn(expect, out)
                self.assertEqual(before, _snapshot(self.root))

    def test_half_present_items_are_refused_not_read_as_done(self):
        _build(self.root)
        self.assertEqual(self._run("--apply")[0], 0)
        p = self.root / "Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml"
        p.write_bytes(p.read_bytes().replace(b'id="taom_wildmesh_moose_a"', b'id="taom_gone_moose_a"'))
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("taom_wildmesh_moose_a", out)

    def test_apply_backs_up_each_edited_file_once(self):
        _build(self.root)
        pristine = _snapshot(self.root)
        self.assertEqual(self._run("--apply")[0], 0)
        for rel in TRACKED[:3]:
            backup = self.root / (rel + wm.BACKUP_SUFFIX)
            self.assertEqual(backup.read_bytes(), pristine[rel], rel)
        self.assertEqual(len(_backups(self.root)), 3, "the new Monsters file needs no backup")

    def test_apply_refuses_while_the_game_or_kit_runs(self):
        _build(self.root)
        before = _snapshot(self.root)
        code, out = self._run("--apply", running=True)
        self.assertEqual(code, 2, out)
        self.assertEqual(before, _snapshot(self.root))

    def test_an_empty_game_dir_exits_2(self):
        buf = io.StringIO()
        with redirect_stdout(buf), mock.patch.object(wm, "game_dir", side_effect=AssertionError("fell back")):
            self.assertEqual(wm.main(["--game-dir", ""]), 2)


class LiveRecipeParityTests(unittest.TestCase):
    """A replay of this script must not revert a live hand edit (a resize): the recipe and the live Armory agree.
    Skipped where the install or the WildMesh Monsters are absent."""

    def setUp(self):
        import xml.etree.ElementTree as ET
        self.ET = ET
        root = os.environ.get("BANNERLORD_GAME_DIR") or wm._DEFAULT_GAME_DIR
        self.md = Path(root) / "Modules" / "LOTRLOME_Armory" / "ModuleData"
        if not (self.md / "Monsters" / "LOTR" / "lotr_monster_wildmesh.xml").is_file():
            self.skipTest("LOTRLOME_Armory with the WildMesh Monsters is not installed here")

    def test_recipe_monsters_and_items_match_the_live_armory(self):
        recipe = {m.get("id"): m.attrib for m in self.ET.fromstring(wm.MONSTER_FILE.split("?>", 1)[1]).iter("Monster")}
        live = {m.get("id"): m.attrib for m in
                self.ET.parse(self.md / "Monsters" / "LOTR" / "lotr_monster_wildmesh.xml").getroot().iter("Monster")}
        self.assertEqual(recipe, live)
        items = {i.get("id"): i for i in self.ET.fromstring("<Items>" + wm.ITEMS + "</Items>").iter("Item")}
        live_items = {i.get("id"): i for i in
                      self.ET.parse(self.md / "LOTRLOME_items" / "LOTRAOM_horses.xml").getroot().iter("Item")}
        for iid, item in items.items():
            self.assertEqual(dict(item.attrib), dict(live_items[iid].attrib), iid)
            self.assertEqual(dict(item.find(".//Horse").attrib), dict(live_items[iid].find(".//Horse").attrib), iid)


if __name__ == "__main__":
    unittest.main(verbosity=2)
